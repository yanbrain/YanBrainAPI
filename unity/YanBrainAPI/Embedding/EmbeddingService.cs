using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YanBrain.YLogger;
using YanBrainAPI.Documents;
using YanBrainAPI.Networking;
using YanBrainAPI.RAG;
using YanBrainAPI.RAG.Data;
using YanBrainAPI.Utils;
using static YanBrain.YLogger.YLog;

namespace YanBrainAPI.Embedding
{
    [EnableLogger]
    public sealed class EmbeddingService
    {
        private readonly YanBrainApi _api;
        private readonly FileStorage _storage;
        private readonly YanBrainApiConfig _config;
        private readonly RAGConfig _ragConfig;
        private readonly DocumentReader _reader;
        private readonly DocumentChunker _chunker;
        private readonly DocumentPathMapper _paths;

        private readonly object _stateLock = new object();
        private readonly ManualResetEventSlim _pauseGate = new ManualResetEventSlim(true);

        private CancellationTokenSource _runCts;

        private readonly DocumentProgressReporter _progress;

        public event Action<DocumentProgress> OnProgressChanged;

        private const int MAX_CHARS_PER_BATCH = 500_000;
        private const int MAX_ITEMS_PER_BATCH = 1000;

        public EmbeddingService(YanBrainApi api, FileStorage storage, YanBrainApiConfig config, RAGConfig ragConfig)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _ragConfig = ragConfig ?? throw new ArgumentNullException(nameof(ragConfig));
            _reader = new DocumentReader();
            _chunker = new DocumentChunker();
            _paths = new DocumentPathMapper(_config);

            _progress = new DocumentProgressReporter(DocumentWorkStage.Embedding);
            _progress.OnChanged += p => OnProgressChanged?.Invoke(p);
        }

        public DocumentProgress GetProgressSnapshot() => _progress.Snapshot();

        public bool IsRunning => _progress.Snapshot().IsRunning;
        public bool IsPaused => _progress.Snapshot().IsPaused;

        public void Pause()
        {
            lock (_stateLock)
            {
                if (!IsRunning) return;
            }
            _pauseGate.Reset();
            _progress.SetPaused(true);
            Log("[EmbeddingService] Paused");
        }

        public void Resume()
        {
            lock (_stateLock)
            {
                if (!IsRunning) return;
            }
            _pauseGate.Set();
            _progress.SetPaused(false);
            Log("[EmbeddingService] Resumed");
        }

        public void Cancel()
        {
            CancellationTokenSource ctsToCancel = null;

            lock (_stateLock)
            {
                if (!IsRunning) return;
                ctsToCancel = _runCts;
            }

            _pauseGate.Set();
            _progress.SetCancelling();

            try { ctsToCancel?.Cancel(); } catch { }

            LogWarning("[EmbeddingService] Cancel requested");
        }

        public void ResetProgress()
        {
            _pauseGate.Set();
            _progress.Reset("Ready");
        }

        public async Task GenerateEmbeddingsAsync(string convertedRelativeTxtPath, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(convertedRelativeTxtPath))
                throw new ArgumentException("Filename required", nameof(convertedRelativeTxtPath));

            var convertedRel = RelativePath.Normalize(convertedRelativeTxtPath);
            RelativePath.AssertSafe(convertedRel, nameof(convertedRelativeTxtPath));

            var fileAbs = _paths.ConvertedAbsolute(convertedRel);

            if (!FileFilter.IsValid(fileAbs))
            {
                Log($"[EmbeddingService] Ignoring unsupported file: {convertedRel}");
                return;
            }

            if (!_storage.NeedsReindex(convertedRel))
            {
                Log($"[EmbeddingService] {convertedRel} up to date, skipping");
                return;
            }

            if (!File.Exists(fileAbs))
                throw new FileNotFoundException($"Document not found: {fileAbs}");

            await WaitIfPausedAsync(ct);

            Log($"[EmbeddingService] Generating embeddings for {convertedRel}...");

            var text = _reader.ReadAllText(fileAbs);
            if (string.IsNullOrWhiteSpace(text))
                throw new Exception($"Document empty: {convertedRel}");

            await WaitIfPausedAsync(ct);

            var chunks = _chunker.Chunk(
                text,
                _ragConfig.ChunkSizeTokens,
                _ragConfig.OverlapTokens
            );

            if (chunks.Count == 0)
                throw new Exception($"No chunks produced: {convertedRel}");

            Log($"[EmbeddingService] {convertedRel}: {chunks.Count} chunks");

            await WaitIfPausedAsync(ct);

            var items = chunks.Select((chunk, i) => new EmbeddingItem
            {
                Id = $"{convertedRel}::chunk_{i}",
                Filename = convertedRel,
                Text = chunk
            }).ToList();

            await WaitIfPausedAsync(ct);

            var embedded = await _api.EmbeddingsAsync(items, ct);

            if (embedded.Items == null || embedded.Items.Count != chunks.Count)
                throw new Exception($"Embedding count mismatch: {embedded?.Items?.Count ?? 0} vs {chunks.Count}");

            var docEmbeddings = new DocumentEmbeddingData
            {
                Filename = convertedRel,
                ChunkSizeTokens = _ragConfig.ChunkSizeTokens,
                OverlapTokens = _ragConfig.OverlapTokens,
                Chunks = embedded.Items.Select((item, i) => new RAGChunkData
                {
                    ChunkIndex = i,
                    Text = chunks[i],
                    Embedding = item.Embedding
                }).ToList()
            };

            await _storage.SaveDocumentEmbeddingsAsync(docEmbeddings);

            Log($"[EmbeddingService] ✓ {convertedRel} embedded");
        }

        public async Task GenerateAllEmbeddingsAsync(CancellationToken externalCt = default)
        {
            _pauseGate.Set();

            _runCts?.Dispose();
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            var ct = _runCts.Token;

            try
            {
                var convertedRoot = _config.GetConvertedDocumentsPath();
                if (!Directory.Exists(convertedRoot))
                {
                    _progress.Start(0, $"ConvertedDocuments not found: {convertedRoot}");
                    LogWarning($"[EmbeddingService] ConvertedDocuments not found: {convertedRoot}");
                    _progress.Stop();
                    return;
                }

                var files = DocumentFileEnumerator.EnumerateRelativeFiles(
                        convertedRoot,
                        abs =>
                        {
                            var ext = Path.GetExtension(abs);
                            return (ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
                                    ext.Equals(".md", StringComparison.OrdinalIgnoreCase))
                                   && FileFilter.IsValid(abs);
                        },
                        useCache: true
                    )
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _progress.Start(files.Count, files.Count == 0 ? "No documents to embed" : "Running");

                if (files.Count == 0)
                {
                    LogWarning("[EmbeddingService] No documents to embed");
                    _progress.Stop();
                    return;
                }

                Log($"[EmbeddingService] Generating embeddings for {files.Count} documents...");

                var allDocuments = new List<DocumentWithChunks>();

                foreach (var convertedRel in files)
                {
                    ct.ThrowIfCancellationRequested();
                    await WaitIfPausedAsync(ct);

                    try
                    {
                        var fileAbs = _paths.ConvertedAbsolute(convertedRel);

                        if (!FileFilter.IsValid(fileAbs))
                        {
                            Log($"[EmbeddingService] Ignoring unsupported file: {convertedRel}");
                            continue;
                        }

                        if (!_storage.NeedsReindex(convertedRel))
                        {
                            Log($"[EmbeddingService] {convertedRel} up to date, skipping");
                            continue;
                        }

                        if (!File.Exists(fileAbs))
                        {
                            LogWarning($"[EmbeddingService] File not found: {convertedRel}");
                            continue;
                        }

                        var text = _reader.ReadAllText(fileAbs);
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            LogWarning($"[EmbeddingService] Document empty: {convertedRel}");
                            continue;
                        }

                        var chunks = _chunker.Chunk(
                            text,
                            _ragConfig.ChunkSizeTokens,
                            _ragConfig.OverlapTokens
                        );

                        if (chunks.Count == 0)
                        {
                            LogWarning($"[EmbeddingService] No chunks produced: {convertedRel}");
                            continue;
                        }

                        Log($"[EmbeddingService] {convertedRel}: {chunks.Count} chunks");

                        allDocuments.Add(new DocumentWithChunks
                        {
                            DocumentPath = convertedRel,
                            Chunks = chunks
                        });
                    }
                    catch (Exception ex)
                    {
                        LogError($"[EmbeddingService] Failed to prepare {convertedRel}: {ex.Message}");
                    }
                }

                if (allDocuments.Count == 0)
                {
                    LogWarning("[EmbeddingService] No documents to process after filtering");
                    _progress.Stop();
                    return;
                }

                var batches = CreateFileOrderedBatches(allDocuments);
                Log($"[EmbeddingService] Created {batches.Count} file-ordered batches");

                int processedBatches = 0;
                
                foreach (var batch in batches)
                {
                    ct.ThrowIfCancellationRequested();
                    await WaitIfPausedAsync(ct);

                    try
                    {
                        Log($"[EmbeddingService] Processing batch {++processedBatches}/{batches.Count}: {batch.Documents.Count} files, {batch.TotalChunks} chunks, {batch.TotalChars} chars");

                        var batchItems = new List<EmbeddingItem>();
                        var itemToDocMap = new List<(string docPath, int chunkIndex)>();

                        foreach (var doc in batch.Documents)
                        {
                            for (int i = 0; i < doc.Chunks.Count; i++)
                            {
                                batchItems.Add(new EmbeddingItem
                                {
                                    Id = $"{doc.DocumentPath}::chunk_{i}",
                                    Filename = doc.DocumentPath,
                                    Text = doc.Chunks[i]
                                });
                                itemToDocMap.Add((doc.DocumentPath, i));
                            }
                        }

                        var embedded = await _api.EmbeddingsAsync(batchItems, ct);

                        if (embedded.Items == null || embedded.Items.Count != batchItems.Count)
                        {
                            throw new Exception($"Embedding count mismatch: {embedded?.Items?.Count ?? 0} vs {batchItems.Count}");
                        }

                        var docResults = new Dictionary<string, List<(int chunkIndex, string text, float[] embedding)>>();

                        for (int i = 0; i < embedded.Items.Count; i++)
                        {
                            var (docPath, chunkIndex) = itemToDocMap[i];

                            if (!docResults.ContainsKey(docPath))
                            {
                                docResults[docPath] = new List<(int, string, float[])>();
                            }

                            docResults[docPath].Add((
                                chunkIndex,
                                batchItems[i].Text,
                                embedded.Items[i].Embedding
                            ));
                        }

                        // Build all document embeddings for this batch
                        var batchDocEmbeddings = new List<DocumentEmbeddingData>();

                        foreach (var doc in batch.Documents)
                        {
                            ct.ThrowIfCancellationRequested();
                            await WaitIfPausedAsync(ct);

                            _progress.SetCurrent(doc.DocumentPath);

                            try
                            {
                                if (!docResults.ContainsKey(doc.DocumentPath))
                                {
                                    throw new Exception($"No embeddings returned for {doc.DocumentPath}");
                                }

                                var chunks = docResults[doc.DocumentPath].OrderBy(c => c.chunkIndex).ToList();

                                var docEmbeddings = new DocumentEmbeddingData
                                {
                                    Filename = doc.DocumentPath,
                                    ChunkSizeTokens = _ragConfig.ChunkSizeTokens,
                                    OverlapTokens = _ragConfig.OverlapTokens,
                                    Chunks = chunks.Select(c => new RAGChunkData
                                    {
                                        ChunkIndex = c.chunkIndex,
                                        Text = c.text,
                                        Embedding = c.embedding
                                    }).ToList()
                                };

                                batchDocEmbeddings.Add(docEmbeddings);
                                _progress.MarkOk();

                                Log($"[EmbeddingService] ✓ {doc.DocumentPath} prepared ({chunks.Count} chunks)");
                            }
                            catch (Exception ex)
                            {
                                _progress.MarkFailed();
                                LogError($"[EmbeddingService] Failed to prepare {doc.DocumentPath}: {ex.Message}");
                            }
                        }

                        // Save entire batch at once (optimization)
                        if (batchDocEmbeddings.Count > 0)
                        {
                            await _storage.SaveDocumentEmbeddingsBatchAsync(batchDocEmbeddings);
                            Log($"[EmbeddingService] ✓ Batch saved: {batchDocEmbeddings.Count} documents");
                        }

                        _progress.FlushPending();

                        Log($"[EmbeddingService] ✓ Batch {processedBatches}/{batches.Count} complete");
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        LogError($"[EmbeddingService] Batch failed: {ex.Message}");
                        foreach (var doc in batch.Documents)
                        {
                            _progress.MarkFailed();
                        }
                        throw;
                    }
                }

                _progress.Complete();
                Log($"[EmbeddingService] ✅ Complete: {_progress.Snapshot().Done}/{_progress.Snapshot().Total} (OK: {_progress.Snapshot().Ok}, Failed: {_progress.Snapshot().Failed})");
            }
            catch (OperationCanceledException)
            {
                _progress.Cancelled();
                LogWarning("[EmbeddingService] Cancelled");
            }
            finally
            {
                _pauseGate.Set();

                try { _runCts?.Dispose(); }
                catch { }
                finally { _runCts = null; }

                _progress.Stop();
            }
        }

        public List<string> GetEmbeddedDocuments()
        {
            return _storage.GetEmbeddedDocuments();
        }

        public void RemoveEmbeddings(string convertedRelativeTxtPath)
        {
            var rel = RelativePath.Normalize(convertedRelativeTxtPath);
            RelativePath.AssertSafe(rel, nameof(convertedRelativeTxtPath));

            _storage.RemoveDocument(rel);
            Log($"[EmbeddingService] Removed embeddings for {rel}");
        }

        public void ClearAllEmbeddings()
        {
            _storage.ClearAll();
            Log("[EmbeddingService] All embeddings cleared");
        }

        private async Task WaitIfPausedAsync(CancellationToken ct)
        {
            if (_pauseGate.IsSet) return;

            while (!_pauseGate.IsSet)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(50, ct);
            }
        }

        private List<DocumentBatch> CreateFileOrderedBatches(List<DocumentWithChunks> documents)
        {
            var batches = new List<DocumentBatch>();
            var currentBatch = new DocumentBatch();

            foreach (var doc in documents)
            {
                int docChunks = doc.Chunks.Count;
                int docChars = doc.Chunks.Sum(c => c.Length);

                bool wouldExceedChars = (currentBatch.TotalChars + docChars) > MAX_CHARS_PER_BATCH;
                bool wouldExceedItems = (currentBatch.TotalChunks + docChunks) > MAX_ITEMS_PER_BATCH;

                if (wouldExceedChars || wouldExceedItems)
                {
                    if (currentBatch.Documents.Count > 0)
                    {
                        batches.Add(currentBatch);
                        currentBatch = new DocumentBatch();
                    }

                    if (docChars > MAX_CHARS_PER_BATCH || docChunks > MAX_ITEMS_PER_BATCH)
                    {
                        LogWarning($"[EmbeddingService] Document {doc.DocumentPath} exceeds single batch limits ({docChunks} chunks, {docChars} chars)");
                        var oversizedBatch = new DocumentBatch();
                        oversizedBatch.Documents.Add(doc);
                        oversizedBatch.TotalChunks = docChunks;
                        oversizedBatch.TotalChars = docChars;
                        batches.Add(oversizedBatch);
                        continue;
                    }
                }

                currentBatch.Documents.Add(doc);
                currentBatch.TotalChunks += docChunks;
                currentBatch.TotalChars += docChars;
            }

            if (currentBatch.Documents.Count > 0)
            {
                batches.Add(currentBatch);
            }

            return batches;
        }

        private class DocumentWithChunks
        {
            public string DocumentPath { get; set; }
            public List<string> Chunks { get; set; }
        }

        private class DocumentBatch
        {
            public List<DocumentWithChunks> Documents { get; set; } = new List<DocumentWithChunks>();
            public int TotalChunks { get; set; } = 0;
            public int TotalChars { get; set; } = 0;
        }
    }
}