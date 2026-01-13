using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace YanBrainAPI.RAG
{
    /// <summary>
    /// Splits text into semantic chunks respecting paragraph boundaries
    /// </summary>
    public sealed class TextChunker
    {
        public List<string> ChunkText(string text, int targetTokens = 400, int overlapTokens = 50)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            text = text.Trim();
            targetTokens = Math.Max(100, targetTokens);
            overlapTokens = Math.Max(0, Math.Min(overlapTokens, targetTokens / 2));

            var paragraphs = SplitIntoParagraphs(text);
            var chunks = new List<string>();
            var currentChunk = new StringBuilder();
            var currentTokens = 0;

            foreach (var para in paragraphs)
            {
                var paraTokens = EstimateTokens(para);

                // If paragraph too large, split by sentences
                if (paraTokens > targetTokens * 1.5)
                {
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        currentChunk.Clear();
                        currentTokens = 0;
                    }

                    var sentences = SplitIntoSentences(para);
                    foreach (var sent in sentences)
                    {
                        var sentTokens = EstimateTokens(sent);

                        if (currentTokens + sentTokens > targetTokens && currentChunk.Length > 0)
                        {
                            chunks.Add(currentChunk.ToString().Trim());

                            var overlap = GetLastTokens(currentChunk.ToString(), overlapTokens);
                            currentChunk.Clear();
                            currentChunk.Append(overlap);
                            currentTokens = EstimateTokens(overlap);
                        }

                        currentChunk.Append(sent).Append(" ");
                        currentTokens += sentTokens;
                    }
                    continue;
                }

                // Add paragraph to current chunk
                if (currentTokens + paraTokens <= targetTokens)
                {
                    if (currentChunk.Length > 0)
                        currentChunk.Append("\n\n");
                    currentChunk.Append(para);
                    currentTokens += paraTokens;
                }
                else
                {
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().Trim());

                        var overlap = GetLastTokens(currentChunk.ToString(), overlapTokens);
                        currentChunk.Clear();
                        currentChunk.Append(overlap).Append("\n\n");
                        currentTokens = EstimateTokens(overlap);
                    }

                    currentChunk.Append(para);
                    currentTokens += paraTokens;
                }
            }

            if (currentChunk.Length > 0)
                chunks.Add(currentChunk.ToString().Trim());

            return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        }

        public int EstimateTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return Math.Max(1, text.Length / 4);
        }

        private List<string> SplitIntoParagraphs(string text)
        {
            return text
                .Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();
        }

        private List<string> SplitIntoSentences(string text)
        {
            var sentences = new List<string>();
            var current = new StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                current.Append(text[i]);

                if ((text[i] == '.' || text[i] == '!' || text[i] == '?') &&
                    i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))
                {
                    sentences.Add(current.ToString().Trim());
                    current.Clear();
                }
            }

            if (current.Length > 0)
                sentences.Add(current.ToString().Trim());

            return sentences.Where(s => s.Length > 0).ToList();
        }

        private string GetLastTokens(string text, int tokens)
        {
            var targetChars = tokens * 4;
            if (text.Length <= targetChars)
                return text;

            var start = Math.Max(0, text.Length - targetChars * 2);
            var substring = text.Substring(start);
            var lastPara = substring.LastIndexOf("\n\n");

            if (lastPara > 0)
                return substring.Substring(lastPara + 2).Trim();

            return text.Substring(text.Length - targetChars).Trim();
        }
    }
}
