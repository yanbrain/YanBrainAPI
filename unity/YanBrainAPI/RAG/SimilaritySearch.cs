using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YanBrainAPI.Networking;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI.RAG
{
    /// <summary>
    /// Performs cosine similarity search and smart packing
    /// </summary>
    
    [EnableLogger]
    public sealed class SimilaritySearch
    {
        /// <summary>
        /// Stage 1: Find relevant documents
        /// </summary>
        public List<DocumentSummary> SearchDocuments(
            float[] queryEmbedding,
            List<DocumentSummary> allDocs,
            int topK)
        {
            if (queryEmbedding == null || allDocs == null || allDocs.Count == 0)
                return new List<DocumentSummary>();

            var scored = allDocs
                .Where(doc => doc.Embedding != null && doc.Embedding.Length == queryEmbedding.Length)
                .Select(doc => new { Doc = doc, Score = CosineSimilarity(queryEmbedding, doc.Embedding) })
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Doc)
                .ToList();

            return scored;
        }

        /// <summary>
        /// Stage 2: Find relevant chunks
        /// </summary>
        public List<SearchResult> SearchChunks(
            float[] queryEmbedding,
            List<DocumentEmbeddings> documents,
            int topK,
            float threshold)
        {
            if (queryEmbedding == null || documents == null || documents.Count == 0)
                return new List<SearchResult>();

            var allChunks = new List<(DocumentChunk chunk, string filename, float score)>();

            foreach (var doc in documents)
            {
                foreach (var chunk in doc.Chunks)
                {
                    if (chunk?.Embedding == null || chunk.Embedding.Length == 0)
                        continue;

                    var score = CosineSimilarity(queryEmbedding, chunk.Embedding);
                    if (score >= threshold)
                        allChunks.Add((chunk, doc.Filename, score));
                }
            }

            // Fallback: if nothing above threshold, take top results anyway
            if (allChunks.Count == 0)
            {
                allChunks = documents
                    .SelectMany(doc => doc.Chunks.Select(c => (chunk: c, filename: doc.Filename)))
                    .Where(x => x.chunk?.Embedding != null)
                    .Select(x => (x.chunk, x.filename, score: CosineSimilarity(queryEmbedding, x.chunk.Embedding)))
                    .OrderByDescending(x => x.score)
                    .Take(topK)
                    .ToList();
            }

            var topChunks = allChunks
                .OrderByDescending(x => x.score)
                .Take(topK)
                .ToList();

            // Group by document
            var results = topChunks
                .GroupBy(x => x.filename)
                .Select(g => new SearchResult
                {
                    Filename = g.Key,
                    Score = g.Average(x => x.score),
                    ChunkIndices = g.Select(x => x.chunk.ChunkIndex).OrderBy(i => i).ToList(),
                    MergedText = string.Join("\n\n---\n\n",
                        g.OrderBy(x => x.chunk.ChunkIndex).Select(x => x.chunk.Text))
                })
                .OrderByDescending(r => r.Score)
                .ToList();

            return results;
        }

        /// <summary>
        /// Smart pack: Fill budget with best chunks
        /// </summary>
        public List<RelevantDocument> SmartPack(
            List<SearchResult> results,
            int maxTotalChars,
            int maxDocs)
        {
            var packed = new List<RelevantDocument>();
            var currentChars = 0;

            // Sort by score (best first)
            foreach (var result in results.OrderByDescending(r => r.Score).Take(maxDocs))
            {
                var textLength = result.MergedText?.Length ?? 0;

                if (textLength == 0)
                    continue;

                // Add if fits in budget
                if (currentChars + textLength <= maxTotalChars)
                {
                    packed.Add(new RelevantDocument
                    {
                        Filename = result.Filename,
                        Text = result.MergedText
                    });
                    currentChars += textLength;
                }
                else
                {
                    // Partial fit: truncate to fit remaining budget
                    var remaining = maxTotalChars - currentChars;
                    if (remaining > 500) // Only add if we can fit meaningful text
                    {
                        packed.Add(new RelevantDocument
                        {
                            Filename = result.Filename,
                            Text = result.MergedText.Substring(0, remaining)
                        });
                        currentChars += remaining;
                        break; // Budget full
                    }
                }

                // Stop if 90% full
                if (currentChars >= maxTotalChars * 0.9)
                    break;
            }

            return packed;
        }

        /// <summary>
        /// Calculate cosine similarity between two vectors
        /// </summary>
        public float CosineSimilarity(float[] vec1, float[] vec2)
        {
            if (vec1 == null || vec2 == null)
                return 0f;

            if (vec1.Length != vec2.Length)
            {
                LogWarning($"[SimilaritySearch] Dimension mismatch: {vec1.Length} vs {vec2.Length}");
                return 0f;
            }

            double dot = 0, mag1 = 0, mag2 = 0;

            for (int i = 0; i < vec1.Length; i++)
            {
                dot += vec1[i] * vec2[i];
                mag1 += vec1[i] * vec1[i];
                mag2 += vec2[i] * vec2[i];
            }

            var magnitude = Math.Sqrt(mag1) * Math.Sqrt(mag2);
            return magnitude < 1e-10 ? 0f : (float)(dot / magnitude);
        }
    }
}