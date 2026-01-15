using Sisus.Init;
using UnityEngine;
using YanBrain.YLogger;
using YanBrainAPI.Interfaces;
using YanBrainAPI.Networking;
using YanBrainAPI.RAG;
using YanBrainAPI.RAG.Data;
using static YanBrain.YLogger.YLog;

namespace YanBrainAPI
{
    [EnableLogger]
    public sealed class YanBrainService : MonoBehaviour<YanBrainApiConfig, ITokenProvider>
    {
        private YanBrainApiConfig _config;
        private ITokenProvider _tokenProvider;
        
        private YanBrainApi _api;
        private FileStorage _storage;
        private RAGConfig _ragConfig;
        
        public YanBrainApi Api => _api;
        public FileStorage Storage => _storage;
        public YanBrainApiConfig Config => _config;
        public RAGConfig RagConfig => _ragConfig;
        
        protected override void Init(YanBrainApiConfig config, ITokenProvider tokenProvider)
        {
            _config = config;
            _tokenProvider = tokenProvider;
        }
        
        protected override void OnAwake()
        {
            _config.EnsureFoldersExist();
            
            var http = new YanHttp(_config.GetBaseUrl(), _config.TimeoutSeconds, _tokenProvider);
            _api = new YanBrainApi(http);
            
            _storage = new FileStorage(
                _config.GetEmbeddingsPath(),
                _config.GetIndexPath(),
                _config.GetConvertedDocumentsPath()
            );
            
            _ragConfig = new RAGConfig();
            
            Log("[YanBrainService] Initialized");
        }
    }
}