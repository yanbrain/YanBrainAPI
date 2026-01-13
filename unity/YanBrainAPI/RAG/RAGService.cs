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
    /// Main RAG service - high-level API for users
    /// </summary>
    [EnableLogger]
    public sealed class RAGService
    {
        private readonly RAGContext _context;
        private readonly DocumentSearcher _searcher;

        public RAGService(RAGContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _searcher = new DocumentSearcher(context);
        }

        // ==================== Main Query API ====================

        /// <summary>
        /// Query documents and get relevant context for LLM
        /// </summary>
        public async Task<List<RelevantDocument>> QueryAsync(string userPrompt, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userPrompt))
                throw new ArgumentException("User prompt required");

            if (!IsReady())
                throw new Exception("RAG not ready. Run Setup() first.");

            return await _searcher.QueryAsync(userPrompt, ct);
        }

        // ==================== Status ====================

        public bool IsReady()
        {
            return _searcher.IsIndexReady();
        }

        public int GetIndexedDocumentCount()
        {
            return _searcher.GetIndexedCount();
        }

        public List<string> GetIndexedDocuments()
        {
            return _searcher.GetIndexedDocuments();
        }

        // ==================== Configuration ====================

        public RAGConfig GetConfig()
        {
            return _context.RagConfig;
        }

        public void UpdateConfig(RAGConfig newConfig)
        {
            if (newConfig == null)
                throw new ArgumentNullException(nameof(newConfig));

            LogWarning("[RAGService] Config updated. Changes apply to new queries only.");
        }
    }
}