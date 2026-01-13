using System;

namespace YanBrainAPI.RAG
{
    /// <summary>
    /// Shared context for all RAG components.
    /// Create once, use everywhere - no repetition.
    /// </summary>
    public sealed class RAGContext
    {
        public YanBrainApi Api { get; }
        public YanBrainApiConfig ApiConfig { get; }
        public FileStorage Storage { get; }
        public RAGConfig RagConfig { get; }

        public RAGContext(YanBrainApi api, YanBrainApiConfig apiConfig, RAGConfig ragConfig = null)
        {
            Api = api ?? throw new ArgumentNullException(nameof(api));
            ApiConfig = apiConfig ?? throw new ArgumentNullException(nameof(apiConfig));
            RagConfig = ragConfig ?? new RAGConfig();

            Storage = new FileStorage(
                apiConfig.GetEmbeddingsPath(),
                apiConfig.GetIndexPath(),
                apiConfig.GetConvertedDocumentsPath()
            );

            apiConfig.EnsureFoldersExist();
        }
    }
}