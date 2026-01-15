using Newtonsoft.Json;

namespace YanBrainAPI.RAG.Data
{
    /// <summary>
    /// Single chunk of text with its embedding
    /// </summary>
    public sealed class RAGChunkData
    {
        [JsonProperty("chunkIndex")]
        public int ChunkIndex { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("embedding")]
        public float[] Embedding { get; set; }
    }
}