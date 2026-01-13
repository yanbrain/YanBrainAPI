using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YanBrainAPI.Networking;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI.RAG
{
    /// <summary>
    /// Searches indexed documents (read-only)
    /// </summary>
    [EnableLogger]
    public sealed class DocumentSearcher
    {
        private readonly RAGContext _context;
        private readonly SimilaritySearch _search;

        public DocumentSearcher(RAGContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _search = new SimilaritySearch();
        }

        // ==================== Querying ====================

        /// <summary>
        /// Query indexed documents and return relevant chunks
        /// </summary>
        public async Task<List<RelevantDocument>> QueryAsync(string userPrompt, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userPrompt))
                throw new ArgumentException("User prompt required");

            // Load index
            var summaries = _context.Storage.LoadIndex();
            if (summaries.Count == 0)
                throw new Exception("No index found. Run IndexBuilder.BuildIndex() first.");

            Log($"[DocumentSearcher] Query: \"{userPrompt}\"");

            // Embed query
            var queryItems = new List<EmbeddingItem> { new EmbeddingItem { Id = "query", Text = userPrompt } };
            var queryResult = await _context.Api.EmbeddingsAsync(queryItems, ct);
            var queryEmbedding = queryResult.Items[0].Embedding;

            // Stage 1: Find relevant documents
            var relevantDocSummaries = _search.SearchDocuments(
                queryEmbedding,
                summaries,
                _context.RagConfig.TopDocsStage1
            );

            Log($"[DocumentSearcher] Stage 1: {relevantDocSummaries.Count} docs");

            // Load full embeddings for those docs
            var relevantDocs = relevantDocSummaries
                .Select(s => _context.Storage.LoadDocumentEmbeddings(s.Filename))
                .Where(d => d != null)
                .ToList();

            if (relevantDocs.Count == 0)
                throw new Exception("Failed to load document embeddings");

            // Stage 2: Find relevant chunks
            var searchResults = _search.SearchChunks(
                queryEmbedding,
                relevantDocs,
                _context.RagConfig.TopChunksStage2,
                _context.RagConfig.SimilarityThreshold
            );

            Log($"[DocumentSearcher] Stage 2: {searchResults.Count} docs with chunks");

            // Smart pack into budget
            var result = _search.SmartPack(
                searchResults,
                _context.RagConfig.MaxTotalChars,
                _context.RagConfig.MaxDocs
            );

            var totalChars = result.Sum(r => r.Text?.Length ?? 0);
            Log($"[DocumentSearcher] Packed {result.Count} docs, {totalChars} chars");

            return result;
        }

        // ==================== Info ====================

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

        public bool IsIndexReady()
        {
            return _context.Storage.LoadIndex().Count > 0;
        }
    }
}