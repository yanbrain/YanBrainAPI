using System.Collections.Generic;
using Newtonsoft.Json;

namespace YanBrainAPI.RAG.Data {
    /// <summary>
    /// All embeddings for one document
    /// </summary>
    public sealed class DocumentEmbeddingData
    {
        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonProperty("chunkSizeTokens")]
        public int ChunkSizeTokens { get; set; }

        [JsonProperty("overlapTokens")]
        public int OverlapTokens { get; set; }

        [JsonProperty("chunks")]
        public List<RAGChunkData> Chunks { get; set; } = new();
    }
}