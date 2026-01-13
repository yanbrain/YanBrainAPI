using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YanBrainAPI.Networking;

namespace YanBrainAPI.RAG
{
    /// <summary>
    /// Handles querying indexed documents (read-only)
    /// </summary>
    public sealed class DocumentSearcher
    {
        private readonly YanBrainApi _api;
        private readonly YanBrainConfig _config;
        private readonly EmbeddingStorage _storage;
        private readonly SimilaritySearch _search;
        private readonly RAGConfig _ragConfig;

        public DocumentSearcher(YanBrainApi api, YanBrainConfig config, RAGConfig ragConfig = null)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _ragConfig = ragConfig ?? new RAGConfig();

            _storage = new EmbeddingStorage(
                _config.GetEmbeddingsPath(),
                _config.GetIndexPath(),
                _config.GetConvertedDocumentsPath()
            );
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
            var summaries = _storage.LoadDocumentSummaries();
            if (summaries.Count == 0)
                throw new Exception("No index found. Run IndexBuilder.BuildIndex() first.");

            Debug.Log($"[DocumentSearcher] Query: \"{userPrompt}\"");

            // Embed query
            var queryItems = new List<EmbeddingItem> { new EmbeddingItem { Id = "query", Text = userPrompt } };
            var queryResult = await _api.EmbeddingsAsync(queryItems, ct);
            var queryEmbedding = queryResult.Items[0].Embedding;

            // Stage 1: Find relevant documents
            var relevantDocSummaries = _search.SearchDocuments(
                queryEmbedding,
                summaries,
                _ragConfig.TopDocsStage1
            );

            Debug.Log($"[DocumentSearcher] Stage 1: {relevantDocSummaries.Count} docs");

            // Load full embeddings for those docs
            var relevantDocs = relevantDocSummaries
                .Select(s => _storage.LoadDocumentEmbeddings(s.Filename))
                .Where(d => d != null)
                .ToList();

            if (relevantDocs.Count == 0)
                throw new Exception("Failed to load document embeddings");

            // Stage 2: Find relevant chunks
            var searchResults = _search.SearchChunks(
                queryEmbedding,
                relevantDocs,
                _ragConfig.TopChunksStage2,
                _ragConfig.SimilarityThreshold
            );

            Debug.Log($"[DocumentSearcher] Stage 2: {searchResults.Count} docs with chunks");

            // Smart pack into budget
            var result = _search.SmartPack(
                searchResults,
                _ragConfig.MaxTotalChars,
                _ragConfig.MaxDocs
            );

            var totalChars = result.Sum(r => r.Text?.Length ?? 0);
            Debug.Log($"[DocumentSearcher] Packed {result.Count} docs, {totalChars} chars");

            return result;
        }

        // ==================== Info ====================

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

        public bool IsIndexReady()
        {
            return _storage.LoadDocumentSummaries().Count > 0;
        }
    }
}