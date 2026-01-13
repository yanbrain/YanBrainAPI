using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sisus.Init;
using UnityEngine;
using YanBrainAPI.Interfaces;
using YanBrainAPI.Networking;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI.RAG.Indexer
{
    [EnableLogger]
    public class IndexManager : MonoBehaviour<YanBrainApiConfig,ITokenProvider>
    {
        private YanBrainApiConfig _apiConfig;
        private ITokenProvider _tokenProvider;

        [Title("Status")]
        [ShowInInspector, ReadOnly]
        private int IndexedDocumentCount => _indexBuilder?.GetIndexedCount() ?? 0;

        [ShowInInspector, ReadOnly, PropertySpace(SpaceBefore = 10)]
        private List<string> IndexedDocuments => _indexBuilder?.GetIndexedDocuments() ?? new List<string>();

        private RAGContext _context;
        private IndexBuilder _indexBuilder;

        protected override void Init(YanBrainApiConfig apiConfig, ITokenProvider tokenProvider) {
            _apiConfig = apiConfig;
            _tokenProvider = tokenProvider;
        }
        
        private void Start()
        {
            InitializeServices();
        }

        private void InitializeServices()
        {

            _apiConfig.EnsureFoldersExist();

            var http = new YanHttp(_apiConfig.GetBaseUrl(), _apiConfig.TimeoutSeconds, _tokenProvider);
            var api = new YanBrainApi(http);

            _context = new RAGContext(api, _apiConfig);
            _indexBuilder = new IndexBuilder(_context);

            Log("[IndexManager] Services initialized");
        }

        [Button("Build Index", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        [PropertyOrder(1)]
        private void BuildIndex()
        {
            if (_indexBuilder == null)
            {
                LogError("[IndexManager] Services not initialized!");
                return;
            }

            Log("[IndexManager] Building index...");
            _indexBuilder.BuildIndex();
            Log($"[IndexManager] ✅ Index built! {IndexedDocumentCount} documents indexed");
        }

        [Button("Clear Index", ButtonSizes.Large)]
        [GUIColor(0.8f, 0.3f, 0.3f)]
        [PropertyOrder(2)]
        private void ClearIndex()
        {
            if (_indexBuilder == null)
            {
                LogError("[IndexManager] Services not initialized!");
                return;
            }

            _indexBuilder.ClearIndex();
            Log("[IndexManager] Index cleared");
        }
        
    }
}