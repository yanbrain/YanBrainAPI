using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YanBrainAPI.Networking;
using YanBrainAPI.RAG;
using YanBrainAPI.Utils; // ✅ uses FileFilter
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI.Embedding
{
    /// <summary>
    /// Generates embeddings from converted documents
    /// </summary>
    [EnableLogger]
    public sealed class EmbeddingService
    {
        private readonly RAGContext _context;
        private readonly TextChunker _chunker;

        public EmbeddingService(RAGContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _chunker = new TextChunker();
        }

        // ==================== Generate Embeddings ====================

        /// <summary>
        /// Generate embeddings for a single document
        /// </summary>
        public async Task GenerateEmbeddingsAsync(string filename, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("Filename required");

            var filePath = Path.Combine(_context.ApiConfig.GetConvertedDocumentsPath(), filename);

            // ✅ Ignore junk if someone passes it in
            if (!FileFilter.IsValid(filePath))
            {
                Log($"[EmbeddingService] Ignoring unsupported file: {filename}");
                return;
            }

            // Skip if already embedded and not modified
            if (!_context.Storage.NeedsReindex(filename))
            {
                Log($"[EmbeddingService] {filename} up to date, skipping");
                return;
            }

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Document not found: {filePath}");

            Log($"[EmbeddingService] Generating embeddings for {filename}...");

            // Load and chunk
            var text = File.ReadAllText(filePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(text))
                throw new Exception($"Document empty: {filename}");

            var chunks = _chunker.ChunkText(
                text,
                _context.RagConfig.ChunkSizeTokens,
                _context.RagConfig.OverlapTokens
            );

            if (chunks.Count == 0)
                throw new Exception($"No chunks produced: {filename}");

            Log($"[EmbeddingService] {filename}: {chunks.Count} chunks");

            // Embed all chunks (batch request)
            var items = chunks.Select((chunk, i) => new EmbeddingItem
            {
                Id = $"{filename}::chunk_{i}",
                Filename = filename,
                Text = chunk
            }).ToList();

            var embedded = await _context.Api.EmbeddingsAsync(items, ct);

            if (embedded.Items.Count != chunks.Count)
                throw new Exception($"Embedding count mismatch: {embedded.Items.Count} vs {chunks.Count}");

            // Save embeddings
            var docEmbeddings = new DocumentEmbeddings
            {
                Filename = filename,
                ChunkSizeTokens = _context.RagConfig.ChunkSizeTokens,
                OverlapTokens = _context.RagConfig.OverlapTokens,
                Chunks = embedded.Items.Select((item, i) => new DocumentChunk
                {
                    ChunkIndex = i,
                    Text = chunks[i],
                    Embedding = item.Embedding
                }).ToList()
            };

            _context.Storage.SaveDocumentEmbeddings(docEmbeddings);

            Log($"[EmbeddingService] ✓ {filename} embedded");
        }

        /// <summary>
        /// Generate embeddings for all documents in ConvertedDocuments folder
        /// </summary>
        public async Task GenerateAllEmbeddingsAsync(CancellationToken ct = default)
        {
            var dir = _context.ApiConfig.GetConvertedDocumentsPath();
            if (!Directory.Exists(dir))
            {
                LogWarning($"[EmbeddingService] ConvertedDocuments not found: {dir}");
                return;
            }

            // ✅ Filter out junk and only keep server-supported formats (your ConvertedDocuments should be .txt anyway)
            var files = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly)
                .Where(FileFilter.IsValid)
                .Select(Path.GetFileName)
                .Where(f => !string.IsNullOrEmpty(f))
                .OrderBy(f => f)
                .ToList();

            if (files.Count == 0)
            {
                LogWarning("[EmbeddingService] No documents to embed");
                return;
            }

            Log($"[EmbeddingService] Generating embeddings for {files.Count} documents...");

            int embeddedCount = 0;
            foreach (var filename in files)
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    await GenerateEmbeddingsAsync(filename, ct);
                    embeddedCount++;
                }
                catch (Exception ex)
                {
                    LogError($"[EmbeddingService] Failed {filename}: {ex.Message}");
                }
            }

            Log($"[EmbeddingService] Complete: {embeddedCount}/{files.Count}");
        }

        // ==================== Management ====================

        public List<string> GetEmbeddedDocuments()
        {
            return _context.Storage.GetEmbeddedDocuments();
        }

        public void RemoveEmbeddings(string filename)
        {
            _context.Storage.RemoveDocument(filename);
            Log($"[EmbeddingService] Removed embeddings for {filename}");
        }

        public void ClearAllEmbeddings()
        {
            _context.Storage.ClearAll();
            Log("[EmbeddingService] All embeddings cleared");
        }
    }
}