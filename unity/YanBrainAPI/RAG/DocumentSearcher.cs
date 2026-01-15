using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using YanBrain.YLogger;
using YanBrainAPI.Networking;
using YanBrainAPI.RAG.Data;
using static YanBrain.YLogger.YLog;

namespace YanBrainAPI.RAG
{
    [EnableLogger]
    public sealed class DocumentSearcher : IDisposable
    {
        private readonly YanBrainApi _api;
        private readonly FileStorage _storage;
        private readonly RAGConfig _config;
        
        private NativeArray<float> _embeddings;
        private NativeArray<ChunkMetadata> _metadata;
        private int _chunkCount;
        private int _dimension;
        private bool _isIndexed;
        private bool _isDisposed;

        private List<string> _documentPaths;
        private List<string> _chunkTexts;

        private struct ChunkMetadata
        {
            public int DocumentIndex;
            public int ChunkIndex;
        }

        public DocumentSearcher(YanBrainApi api, FileStorage storage, RAGConfig config)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _documentPaths = new List<string>();
            _chunkTexts = new List<string>();
        }

        public async Task BuildIndexAsync()
        {
            Log("[DocumentSearcher] Building index...");

            var embeddedDocs = _storage.GetEmbeddedDocuments();
            if (embeddedDocs.Count == 0)
            {
                LogWarning("[DocumentSearcher] No embeddings found");
                return;
            }

            DisposeNativeCollections();

            var tempEmbeddings = new List<float>();
            var tempMetadata = new List<ChunkMetadata>();
            _documentPaths.Clear();
            _chunkTexts.Clear();

            foreach (var docRel in embeddedDocs)
            {
                var docEmb = await _storage.LoadDocumentEmbeddingsAsync(docRel);
                if (docEmb?.Chunks == null) continue;

                int docIndex = _documentPaths.Count;
                _documentPaths.Add(docRel);

                foreach (var chunk in docEmb.Chunks)
                {
                    var emb = chunk.Embedding;
                    if (_dimension == 0) _dimension = emb.Length;

                    float norm = CalculateNorm(emb);
                    if (norm > 1e-6f)
                    {
                        for (int i = 0; i < _dimension; i++)
                            tempEmbeddings.Add(emb[i] / norm);

                        tempMetadata.Add(new ChunkMetadata 
                        { 
                            DocumentIndex = docIndex, 
                            ChunkIndex = _chunkTexts.Count 
                        });
                        _chunkTexts.Add(chunk.Text);
                    }
                }
            }

            _chunkCount = tempMetadata.Count;
            if (_chunkCount == 0)
            {
                LogWarning("[DocumentSearcher] No valid embeddings after normalization");
                return;
            }

            _embeddings = new NativeArray<float>(tempEmbeddings.ToArray(), Allocator.Persistent);
            _metadata = new NativeArray<ChunkMetadata>(tempMetadata.ToArray(), Allocator.Persistent);
            _isIndexed = true;

            Log($"[DocumentSearcher] ✅ Index built: {_chunkCount} chunks, {_dimension}D, {_documentPaths.Count} docs");
        }

        public async Task<List<RelevantDocument>> QueryAsync(string userPrompt)
        {
            if (string.IsNullOrWhiteSpace(userPrompt))
                throw new ArgumentException("User prompt required", nameof(userPrompt));
    
            if (!_isIndexed)
                throw new InvalidOperationException("Index not built. Call BuildIndexAsync first.");

            Log($"[DocumentSearcher] Query: \"{userPrompt}\" ({_chunkCount} chunks)");

            var queryVector = new NativeArray<float>(_dimension, Allocator.Persistent);
            var scores = new NativeArray<float>(_chunkCount, Allocator.Persistent);

            try
            {
                var qEmb = await GetQueryEmbedding(userPrompt);
                float qNorm = CalculateNorm(qEmb);
        
                if (qNorm <= 1e-6f)
                {
                    LogWarning("[DocumentSearcher] Query vector has zero magnitude");
                    return new List<RelevantDocument>();
                }

                for (int i = 0; i < _dimension; i++)
                    queryVector[i] = qEmb[i] / qNorm;

                var job = new SimilarityJob
                {
                    QueryVector = queryVector,
                    Embeddings = _embeddings,
                    Scores = scores,
                    Dimension = _dimension
                };

                int workerCount = math.max(1, Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount);
                int batchSize = math.max(1, _chunkCount / (workerCount * 4));
        
                JobHandle handle = job.Schedule(_chunkCount, batchSize);
                handle.Complete();

                var topIndices = GetTopK(scores, _config.TopChunks);
                var results = BuildResults(topIndices);

                Log($"[DocumentSearcher] Found {results.Count} docs ({results.Sum(r => r.Text.Length)} chars)");
                return results;
            }
            finally
            {
                if (queryVector.IsCreated) queryVector.Dispose();
                if (scores.IsCreated) scores.Dispose();
            }
        }
        
        [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
        private struct SimilarityJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> QueryVector;
            [ReadOnly] public NativeArray<float> Embeddings;
            [WriteOnly] public NativeArray<float> Scores;
            public int Dimension;

            public void Execute(int index)
            {
                int offset = index * Dimension;
                float sum = 0f;

                int vecCount = Dimension / 4;
                for (int i = 0; i < vecCount; i++)
                {
                    int idx = i * 4;
                    float4 q = new float4(
                        QueryVector[idx],
                        QueryVector[idx + 1],
                        QueryVector[idx + 2],
                        QueryVector[idx + 3]
                    );
                    float4 e = new float4(
                        Embeddings[offset + idx],
                        Embeddings[offset + idx + 1],
                        Embeddings[offset + idx + 2],
                        Embeddings[offset + idx + 3]
                    );
                    sum += math.dot(q, e);
                }

                int remainder = Dimension % 4;
                for (int i = Dimension - remainder; i < Dimension; i++)
                    sum += QueryVector[i] * Embeddings[offset + i];

                Scores[index] = sum;
            }
        }

        private async Task<float[]> GetQueryEmbedding(string text)
        {
            var queryItems = new List<EmbeddingItem> { new() { Id = "query", Text = text } };
            var queryResult = await _api.EmbeddingsAsync(queryItems);
            return queryResult.Items[0].Embedding;
        }

        private List<int> GetTopK(NativeArray<float> scores, int k)
        {
            var scorePairs = new List<(int index, float score)>(_chunkCount);
            for (int i = 0; i < _chunkCount; i++)
                scorePairs.Add((i, scores[i]));

            return scorePairs
                .OrderByDescending(x => x.score)
                .Take(k)
                .Select(x => x.index)
                .ToList();
        }

        private List<RelevantDocument> BuildResults(List<int> topIndices)
        {
            var docGroups = topIndices
                .Select(i => _metadata[i])
                .GroupBy(m => m.DocumentIndex)
                .Take(_config.MaxDocs);

            var results = new List<RelevantDocument>();
            int totalChars = 0;
            int maxChars = _config.MaxTotalChars;

            foreach (var group in docGroups)
            {
                var chunkList = group.Select(m => new 
                { 
                    Index = m.ChunkIndex, 
                    Text = _chunkTexts[m.ChunkIndex] 
                }).ToList();

                var chunks = chunkList.Select(c => c.Text);
                string mergedText = string.Join("\n\n---\n\n", chunks);

                if (totalChars + mergedText.Length > maxChars)
                {
                    int remaining = maxChars - totalChars;
                    if (remaining > 500)
                    {
                        results.Add(new RelevantDocument
                        {
                            Filename = _documentPaths[group.Key],
                            Text = mergedText.Substring(0, remaining)
                        });
                    }
                    break;
                }

                results.Add(new RelevantDocument
                {
                    Filename = _documentPaths[group.Key],
                    Text = mergedText
                });
                totalChars += mergedText.Length;
            }

            return results;
        }

        private static float CalculateNorm(float[] vector)
        {
            float sumSquares = 0f;
            for (int i = 0; i < vector.Length; i++)
                sumSquares += vector[i] * vector[i];
            return (float)Math.Sqrt(sumSquares);
        }

        public IndexData ExportIndexData()
        {
            if (!_isIndexed)
                throw new InvalidOperationException("Index not built. Call BuildIndexAsync first.");

            return new IndexData
            {
                Embeddings = _embeddings.ToArray(),
                Metadata = _metadata.Select(m => new IndexData.ChunkMetadataSerializable
                {
                    DocumentIndex = m.DocumentIndex,
                    ChunkIndex = m.ChunkIndex
                }).ToArray(),
                DocumentPaths = new List<string>(_documentPaths),
                ChunkTexts = new List<string>(_chunkTexts),
                ChunkCount = _chunkCount,
                Dimension = _dimension
            };
        }

        public void ImportIndexData(IndexData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            DisposeNativeCollections();

            _embeddings = new NativeArray<float>(data.Embeddings, Allocator.Persistent);
            
            var metadata = data.Metadata.Select(m => new ChunkMetadata
            {
                DocumentIndex = m.DocumentIndex,
                ChunkIndex = m.ChunkIndex
            }).ToArray();
            _metadata = new NativeArray<ChunkMetadata>(metadata, Allocator.Persistent);

            _documentPaths = new List<string>(data.DocumentPaths);
            _chunkTexts = new List<string>(data.ChunkTexts);
            _chunkCount = data.ChunkCount;
            _dimension = data.Dimension;
            _isIndexed = true;

            Log($"[DocumentSearcher] ✅ Index imported: {_chunkCount} chunks, {_dimension}D, {_documentPaths.Count} docs");
        }

        public int GetDimension() => _dimension;

        private void DisposeNativeCollections()
        {
            if (_embeddings.IsCreated) _embeddings.Dispose();
            if (_metadata.IsCreated) _metadata.Dispose();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            DisposeNativeCollections();
            _isDisposed = true;
            Log("[DocumentSearcher] Disposed native resources");
        }

        public bool IsIndexReady() => _isIndexed;
        public int GetIndexedCount() => _isIndexed ? _chunkCount : 0;
        public List<string> GetIndexedDocuments() => new List<string>(_documentPaths);
    }

    [Serializable]
    public sealed class IndexData
    {
        public float[] Embeddings { get; set; }
        public ChunkMetadataSerializable[] Metadata { get; set; }
        public List<string> DocumentPaths { get; set; }
        public List<string> ChunkTexts { get; set; }
        public int ChunkCount { get; set; }
        public int Dimension { get; set; }

        [Serializable]
        public struct ChunkMetadataSerializable
        {
            public int DocumentIndex;
            public int ChunkIndex;
        }
    }
}