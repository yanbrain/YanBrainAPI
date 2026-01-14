using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YanBrainAPI.Networking;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI.RAG
{
    /// <summary>
    /// Main RAG service with Burst-optimized index
    /// </summary>
    [EnableLogger]
    public sealed class RAGService : IDisposable
    {
        private readonly RAGContext _context;
        private readonly DocumentSearcher _searcher;
        private bool _indexBuilt = false;
        private bool _isDisposed = false;

        public RAGService(RAGContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _searcher = new DocumentSearcher(context);
        }

        /// <summary>
        /// Build index at startup - call this once before querying
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_indexBuilt) return;

            Log("[RAGService] Initializing Burst index...");
            await _searcher.BuildIndexAsync();
            _indexBuilt = true;
            Log("[RAGService] ✅ Ready");
        }

        /// <summary>
        /// Query documents and get relevant context for LLM
        /// </summary>
        public async Task<List<RelevantDocument>> QueryAsync(string userPrompt)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RAGService));

            if (string.IsNullOrWhiteSpace(userPrompt))
                throw new ArgumentException("User prompt required", nameof(userPrompt));

            if (!_indexBuilt)
            {
                Log("[RAGService] Index not built, building now...");
                await InitializeAsync();
            }

            return await _searcher.QueryAsync(userPrompt);
        }

        public bool IsReady() => _indexBuilt && _searcher.IsIndexReady();

        public int GetIndexedDocumentCount() => _searcher.GetIndexedCount();

        public List<string> GetIndexedDocuments() => _searcher.GetIndexedDocuments();

        public RAGConfig GetConfig() => _context.RagConfig;

        public void Dispose()
        {
            if (_isDisposed) return;

            _searcher?.Dispose();
            _isDisposed = true;
            Log("[RAGService] Disposed");
        }
    }
}
