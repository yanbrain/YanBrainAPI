// File: Assets/Scripts/YanBrainAPI/Documents/DocumentChunker.cs

using System;
using System.Collections.Generic;
using YanBrainAPI.RAG;

namespace YanBrainAPI.Documents
{
    /// <summary>
    /// Splits plain text into chunks for embeddings / RAG indexing.
    /// Wraps your existing TextChunker so chunking rules stay consistent.
    /// </summary>
    public sealed class DocumentChunker
    {
        private readonly TextChunker _chunker;

        public DocumentChunker()
        {
            _chunker = new TextChunker();
        }

        public List<string> Chunk(string text, int chunkSizeTokens, int overlapTokens)
        {
            if (text == null) text = string.Empty;
            if (chunkSizeTokens <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSizeTokens));
            if (overlapTokens < 0) throw new ArgumentOutOfRangeException(nameof(overlapTokens));

            return _chunker.ChunkText(text, chunkSizeTokens, overlapTokens);
        }
    }
}