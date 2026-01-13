using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YanBrainAPI.Networking;
using YanBrainAPI.RAG;

namespace YanBrainAPI.Embedding
{
    /// <summary>
    /// Generates embeddings from converted documents
    /// </summary>
    public sealed class EmbeddingService
    {
        private readonly YanBrainApi _api;
        private readonly YanBrainConfig _config;
        private readonly TextChunker _chunker;
        private readonly EmbeddingStorage _storage;
        private readonly int _chunkSizeTokens;
        private readonly int _overlapTokens;

        public EmbeddingService(
            YanBrainApi api,
            YanBrainConfig config,
            int chunkSizeTokens = 400,
            int overlapTokens = 50)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _chunkSizeTokens = chunkSizeTokens;
            _overlapTokens = overlapTokens;

            _chunker = new TextChunker();
            _storage = new EmbeddingStorage(
                _config.GetEmbeddingsPath(),
                _config.GetIndexPath(),
                _config.GetConvertedDocumentsPath()
            );

            _config.EnsureFoldersExist();
        }

        // ==================== Generate Embeddings ====================

        /// <summary>
        /// Generate embeddings for a single document
        /// </summary>
        public async Task GenerateEmbeddingsAsync(string filename, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("Filename required");

            // Skip if already embedded and not modified
            if (!_storage.NeedsReindex(filename))
            {
                Debug.Log($"[EmbeddingService] {filename} up to date, skipping");
                return;
            }

            var filePath = Path.Combine(_config.GetConvertedDocumentsPath(), filename);
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Document not found: {filePath}");

            Debug.Log($"[EmbeddingService] Generating embeddings for {filename}...");

            // Load and chunk
            var text = File.ReadAllText(filePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(text))
                throw new Exception($"Document empty: {filename}");

            var chunks = _chunker.ChunkText(text, _chunkSizeTokens, _overlapTokens);
            if (chunks.Count == 0)
                throw new Exception($"No chunks produced: {filename}");

            Debug.Log($"[EmbeddingService] {filename}: {chunks.Count} chunks");

            // Embed all chunks (batch request)
            var items = chunks.Select((chunk, i) => new EmbeddingItem
            {
                Id = $"{filename}::chunk_{i}",
                Filename = filename,
                Text = chunk
            }).ToList();

            var embedded = await _api.EmbeddingsAsync(items, ct);

            if (embedded.Items.Count != chunks.Count)
                throw new Exception($"Embedding count mismatch: {embedded.Items.Count} vs {chunks.Count}");

            // Save embeddings
            var docEmbeddings = new DocumentEmbeddings
            {
                Filename = filename,
                ChunkSizeTokens = _chunkSizeTokens,
                OverlapTokens = _overlapTokens,
                Chunks = embedded.Items.Select((item, i) => new DocumentChunk
                {
                    ChunkIndex = i,
                    Text = chunks[i],
                    Embedding = item.Embedding
                }).ToList()
            };

            _storage.SaveDocumentEmbeddings(docEmbeddings);

            Debug.Log($"[EmbeddingService] ✓ {filename} embedded");
        }

        /// <summary>
        /// Generate embeddings for all documents in ConvertedDocuments folder
        /// </summary>
        public async Task GenerateAllEmbeddingsAsync(CancellationToken ct = default)
        {
            var dir = _config.GetConvertedDocumentsPath();
            if (!Directory.Exists(dir))
            {
                Debug.LogWarning($"[EmbeddingService] ConvertedDocuments not found: {dir}");
                return;
            }

            var files = Directory.GetFiles(dir)
                .Select(Path.GetFileName)
                .Where(f => !string.IsNullOrEmpty(f))
                .ToList();

            if (files.Count == 0)
            {
                Debug.LogWarning("[EmbeddingService] No documents to embed");
                return;
            }

            Debug.Log($"[EmbeddingService] Generating embeddings for {files.Count} documents...");

            int embedded = 0;
            foreach (var filename in files)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    await GenerateEmbeddingsAsync(filename, ct);
                    embedded++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EmbeddingService] Failed {filename}: {ex.Message}");
                }
            }

            Debug.Log($"[EmbeddingService] Complete: {embedded}/{files.Count}");
        }

        // ==================== Management ====================

        public List<string> GetEmbeddedDocuments()
        {
            return _storage.GetIndexedDocuments();
        }

        public void RemoveEmbeddings(string filename)
        {
            _storage.RemoveDocument(filename);
            Debug.Log($"[EmbeddingService] Removed embeddings for {filename}");
        }

        public void ClearAllEmbeddings()
        {
            _storage.ClearAll();
            Debug.Log("[EmbeddingService] All embeddings cleared");
        }
    }
}