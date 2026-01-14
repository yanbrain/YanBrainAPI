using System.Collections.Generic;
using Newtonsoft.Json;

namespace YanBrainAPI.RAG
{
    /// <summary>
    /// Single chunk of text with its embedding
    /// </summary>
    public sealed class RagChunk
    {
        [JsonProperty("chunkIndex")]
        public int ChunkIndex { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("embedding")]
        public float[] Embedding { get; set; }
    }

    /// <summary>
    /// All embeddings for one document
    /// </summary>
    public sealed class DocumentEmbeddings
    {
        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonProperty("chunkSizeTokens")]
        public int ChunkSizeTokens { get; set; }

        [JsonProperty("overlapTokens")]
        public int OverlapTokens { get; set; }

        [JsonProperty("chunks")]
        public List<RagChunk> Chunks { get; set; } = new();
    }

    /// <summary>
    /// Document-level summary embedding (for Stage 1 search)
    /// </summary>
    public sealed class DocumentSummary
    {
        [JsonProperty("filename")]
        public string Filename { get; set; }

        [JsonProperty("embedding")]
        public float[] Embedding { get; set; }

        [JsonProperty("lastModified")]
        public long LastModifiedUtc { get; set; }
    }

    /// <summary>
    /// Search result with merged relevant chunks
    /// </summary>
    public sealed class SearchResult
    {
        public string Filename { get; set; }
        public string MergedText { get; set; }
        public float Score { get; set; }
        public List<int> ChunkIndices { get; set; } = new();
    }

    /// <summary>
    /// Configuration for RAG - tweak these in one place
    /// </summary>
    public sealed class RAGConfig
    {
        // Chunking
        public int ChunkSizeTokens { get; set; } = 400;
        public int OverlapTokens { get; set; } = 50;

        // Stage 1: Document-level search
        public int TopDocsStage1 { get; set; } = 8;

        // Stage 2: Chunk-level search
        public int TopChunksStage2 { get; set; } = 50;
        public float SimilarityThreshold { get; set; } = 0.65f;

        // Output: Use the 50K budget smartly
        public int MaxTotalChars { get; set; } = 45000;
        public int MaxDocs { get; set; } = 10;
    }
}