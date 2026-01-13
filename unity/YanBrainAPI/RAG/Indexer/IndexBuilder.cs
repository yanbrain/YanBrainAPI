using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI.RAG.Indexer
{
    /// <summary>
    /// Builds search index from existing embeddings
    /// </summary>
    [EnableLogger]
    public sealed class IndexBuilder
    {
        private readonly RAGContext _context;

        public IndexBuilder(RAGContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // ==================== Build Index ====================

        /// <summary>
        /// Build search index from all embeddings
        /// </summary>
        public void BuildIndex()
        {
            var embeddingsDir = _context.ApiConfig.GetEmbeddingsPath();
            if (!Directory.Exists(embeddingsDir))
            {
                LogWarning($"[IndexBuilder] Embeddings folder not found: {embeddingsDir}");
                return;
            }

            var embeddingFiles = Directory.GetFiles(embeddingsDir, "*.embeddings");
            if (embeddingFiles.Length == 0)
            {
                LogWarning("[IndexBuilder] No embeddings found. Run EmbeddingService first.");
                return;
            }

            Log($"[IndexBuilder] Building index from {embeddingFiles.Length} embeddings...");

            var summaries = new List<DocumentSummary>();

            foreach (var filePath in embeddingFiles)
            {
                try
                {
                    var filename = Path.GetFileName(filePath).Replace(".embeddings", "");
                    var docEmbeddings = _context.Storage.LoadDocumentEmbeddings(filename);

                    if (docEmbeddings == null || docEmbeddings.Chunks.Count == 0)
                    {
                        LogWarning($"[IndexBuilder] Skipping {filename}: no chunks");
                        continue;
                    }

                    // Average ALL chunks to create doc summary
                    var summaryEmbedding = AverageEmbeddings(
                        docEmbeddings.Chunks.Select(c => c.Embedding).ToList()
                    );

                    if (summaryEmbedding == null)
                    {
                        LogWarning($"[IndexBuilder] Skipping {filename}: failed to average");
                        continue;
                    }

                    summaries.Add(new DocumentSummary
                    {
                        Filename = filename,
                        Embedding = summaryEmbedding,
                        LastModifiedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });

                    Log($"[IndexBuilder] ✓ {filename} indexed");
                }
                catch (Exception ex)
                {
                    LogError($"[IndexBuilder] Failed to index {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            // Save index
            _context.Storage.SaveIndex(summaries);
            Log($"[IndexBuilder] Index built: {summaries.Count} documents");
        }

        /// <summary>
        /// Add or update a single document in the index
        /// </summary>
        public void UpdateIndex(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("Filename required");

            Log($"[IndexBuilder] Updating index for {filename}...");

            var docEmbeddings = _context.Storage.LoadDocumentEmbeddings(filename);
            if (docEmbeddings == null || docEmbeddings.Chunks.Count == 0)
            {
                LogWarning($"[IndexBuilder] No embeddings found for {filename}");
                return;
            }

            // Average ALL chunks
            var summaryEmbedding = AverageEmbeddings(
                docEmbeddings.Chunks.Select(c => c.Embedding).ToList()
            );

            if (summaryEmbedding == null)
            {
                LogWarning($"[IndexBuilder] Failed to create summary for {filename}");
                return;
            }

            // Load existing index
            var summaries = _context.Storage.LoadIndex();

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
            _context.Storage.SaveIndex(summaries);
            Log($"[IndexBuilder] ✓ {filename} updated in index");
        }

        /// <summary>
        /// Remove document from index
        /// </summary>
        public void RemoveFromIndex(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("Filename required");

            var summaries = _context.Storage.LoadIndex();
            var removed = summaries.RemoveAll(s => s.Filename == filename);

            if (removed > 0)
            {
                _context.Storage.SaveIndex(summaries);
                Log($"[IndexBuilder] Removed {filename} from index");
            }
            else
            {
                LogWarning($"[IndexBuilder] {filename} not found in index");
            }
        }

        /// <summary>
        /// Clear entire index
        /// </summary>
        public void ClearIndex()
        {
            _context.Storage.SaveIndex(new List<DocumentSummary>());
            Log("[IndexBuilder] Index cleared");
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
                    LogWarning($"[IndexBuilder] Skipping invalid embedding (expected {dims}D)");
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
            return _context.Storage.LoadIndex().Count;
        }

        public List<string> GetIndexedDocuments()
        {
            return _context.Storage.LoadIndex()
                .Select(s => s.Filename)
                .ToList();
        }
    }
}