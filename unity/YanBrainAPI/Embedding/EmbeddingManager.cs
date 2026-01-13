using System;
using System.Collections.Generic;
using System.Threading;
using Sirenix.OdinInspector;
using Sisus.Init;
using YanBrainAPI.Interfaces;
using YanBrainAPI.Networking;
using YanBrainAPI.RAG;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI.Embedding
{
    [EnableLogger]
    public class EmbeddingManager : MonoBehaviour<YanBrainApiConfig, ITokenProvider>
    {
        private YanBrainApiConfig _apiConfig;
        private ITokenProvider _tokenProvider;

        [Title("Status")]
        [ShowInInspector, ReadOnly]
        private int EmbeddedDocumentCount => _embeddingService?.GetEmbeddedDocuments().Count ?? 0;

        [ShowInInspector, ReadOnly, PropertySpace(SpaceBefore = 10)]
        private List<string> EmbeddedDocuments => _embeddingService?.GetEmbeddedDocuments() ?? new List<string>();

        private RAGContext _context;
        private EmbeddingService _embeddingService;
        private CancellationTokenSource _cts;

        protected override void Init(YanBrainApiConfig apiConfig, ITokenProvider tokenProvider) {
            _apiConfig = apiConfig;
            _tokenProvider = tokenProvider;
        }
        
        private void Start()
        {
            InitializeServices();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void InitializeServices()
        {
            _apiConfig.EnsureFoldersExist();

            var http = new YanHttp(_apiConfig.GetBaseUrl(), _apiConfig.TimeoutSeconds, _tokenProvider);
            var api = new YanBrainApi(http);

            _context = new RAGContext(api, _apiConfig);
            _embeddingService = new EmbeddingService(_context);

            Log("[EmbeddingManager] Services initialized");
        }

        [Button("Generate Embeddings", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        [PropertyOrder(1)]
        private async void GenerateEmbeddings()
        {
            if (_embeddingService == null)
            {
                LogError("[EmbeddingManager] Services not initialized!");
                return;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                Log("[EmbeddingManager] Starting embedding generation...");
                await _embeddingService.GenerateAllEmbeddingsAsync(_cts.Token);
                Log($"[EmbeddingManager] ✅ Complete! {EmbeddedDocumentCount} documents embedded");
            }
            catch (Exception ex)
            {
                LogError($"[EmbeddingManager] Failed: {ex.Message}");
            }
        }

        [Button("Clear Embeddings", ButtonSizes.Large)]
        [GUIColor(0.8f, 0.3f, 0.3f)]
        [PropertyOrder(2)]
        private void ClearEmbeddings()
        {
            if (_embeddingService == null)
            {
                LogError("[EmbeddingManager] Services not initialized!");
                return;
            }

            _embeddingService.ClearAllEmbeddings();
            Log("[EmbeddingManager] All embeddings cleared");
        }
        
    }
}