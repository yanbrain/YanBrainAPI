using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using YanBrainAPI.RAG;

namespace YanBrainAPI.RAG
{
    /// <summary>
    /// Builds search index from existing embeddings
    /// </summary>
    public sealed class IndexBuilder
    {
        private readonly YanBrainConfig _config;
        private readonly EmbeddingStorage _storage;

        public IndexBuilder(YanBrainConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _storage = new EmbeddingStorage(
                _config.GetEmbeddingsPath(),
                _config.GetIndexPath(),
                _config.GetConvertedDocumentsPath()
            );

            _config.EnsureFoldersExist();
        }

        // ==================== Build Index ====================

        /// <summary>
        /// Build search index from all embeddings
        /// </summary>
        public void BuildIndex()
        {
            var embeddingsDir = _config.GetEmbeddingsPath();
            if (!Directory.Exists(embeddingsDir))
            {
                Debug.LogWarning($"[IndexBuilder] Embeddings folder not found: {embeddingsDir}");
                return;
            }

            var embeddingFiles = Directory.GetFiles(embeddingsDir, "*.embeddings");
            if (embeddingFiles.Length == 0)
            {
                Debug.LogWarning("[IndexBuilder] No embeddings found. Run EmbeddingService first.");
                return;
            }

            Debug.Log($"[IndexBuilder] Building index from {embeddingFiles.Length} embeddings...");

            var summaries = new List<DocumentSummary>();

            foreach (var filePath in embeddingFiles)
            {
                try
                {
                    var filename = Path.GetFileName(filePath).Replace(".embeddings", "");
                    var docEmbeddings = _storage.LoadDocumentEmbeddings(filename);

                    if (docEmbeddings == null || docEmbeddings.Chunks.Count == 0)
                    {
                        Debug.LogWarning($"[IndexBuilder] Skipping {filename}: no chunks");
                        continue;
                    }

                    // Average ALL chunks to create doc summary
                    var summaryEmbedding = AverageEmbeddings(
                        docEmbeddings.Chunks.Select(c => c.Embedding).ToList()
                    );

                    if (summaryEmbedding == null)
                    {
                        Debug.LogWarning($"[IndexBuilder] Skipping {filename}: failed to average");
                        continue;
                    }

                    summaries.Add(new DocumentSummary
                    {
                        Filename = filename,
                        Embedding = summaryEmbedding,
                        LastModifiedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });

                    Debug.Log($"[IndexBuilder] ✓ {filename} indexed");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[IndexBuilder] Failed to index {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            // Save index
            _storage.SaveDocumentSummaries(summaries);
            Debug.Log($"[IndexBuilder] Index built: {summaries.Count} documents");
        }

        /// <summary>
        /// Add or update a single document in the index
        /// </summary>
        public void UpdateIndex(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("Filename required");

            Debug.Log($"[IndexBuilder] Updating index for {filename}...");

            var docEmbeddings = _storage.LoadDocumentEmbeddings(filename);
            if (docEmbeddings == null || docEmbeddings.Chunks.Count == 0)
            {
                Debug.LogWarning($"[IndexBuilder] No embeddings found for {filename}");
                return;
            }

            // Average ALL chunks
            var summaryEmbedding = AverageEmbeddings(
                docEmbeddings.Chunks.Select(c => c.Embedding).ToList()
            );

            if (summaryEmbedding == null)
            {
                Debug.LogWarning($"[IndexBuilder] Failed to create summary for {filename}");
                return;
            }

            // Load existing index
            var summaries = _storage.LoadDocumentSummaries();

            // Remove old entry if exists
            summaries.RemoveAll(s => s.Filename == filename);

            // Add new entry
            summaries.Add(new DocumentSummary
            {
                Filename = filename,
                Embedding = summaryEmbedding,
                LastModifiedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });

            // Save updated index
            _storage.SaveDocumentSummaries(summaries);
            Debug.Log($"[IndexBuilder] ✓ {filename} updated in index");
        }

        /// <summary>
        /// Remove document from index
        /// </summary>
        public void RemoveFromIndex(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("Filename required");

            var summaries = _storage.LoadDocumentSummaries();
            var removed = summaries.RemoveAll(s => s.Filename == filename);

            if (removed > 0)
            {
                _storage.SaveDocumentSummaries(summaries);
                Debug.Log($"[IndexBuilder] Removed {filename} from index");
            }
            else
            {
                Debug.LogWarning($"[IndexBuilder] {filename} not found in index");
            }
        }

        /// <summary>
        /// Clear entire index
        /// </summary>
        public void ClearIndex()
        {
            _storage.SaveDocumentSummaries(new List<DocumentSummary>());
            Debug.Log("[IndexBuilder] Index cleared");
        }

        // ==================== Helpers ====================

        private float[] AverageEmbeddings(List<float[]> embeddings)
        {
            if (embeddings == null || embeddings.Count == 0)
                return null;

            var dims = embeddings[0].Length;
            var result = new float[dims];

            foreach (var emb in embeddings)
            {
                if (emb == null || emb.Length != dims)
                {
                    Debug.LogWarning($"[IndexBuilder] Skipping invalid embedding (expected {dims}D)");
                    continue;
                }

                for (int i = 0; i < dims; i++)
                    result[i] += emb[i];
            }

            for (int i = 0; i < dims; i++)
                result[i] /= embeddings.Count;

            return result;
        }

        public int GetIndexedCount()
        {
            return _storage.LoadDocumentSummaries().Count;
        }

        public List<string> GetIndexedDocuments()
        {
            return _storage.LoadDocumentSummaries()
                .Select(s => s.Filename)
                .ToList();
        }
    }
}