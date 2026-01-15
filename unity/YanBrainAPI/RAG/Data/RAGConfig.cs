public sealed class RAGConfig
{
    // Chunking parameters
    public int ChunkSizeTokens { get; set; } = 400;
    public int OverlapTokens { get; set; } = 50;

    // Search parameters
    public int TopChunks { get; set; } = 50;

    // Output limits
    public int MaxTotalChars { get; set; } = 45000;
    public int MaxDocs { get; set; } = 10;
}