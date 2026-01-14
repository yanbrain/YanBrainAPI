// File: Assets/Scripts/YanBrainAPI/RAG/FileStorage.cs
// SIMPLIFIED VERSION - Index removed (SharpVector handles it)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using YanBrainAPI.Utils;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI.RAG
{
    /// <summary>
    /// Handles file I/O for embeddings only (index removed - SharpVector handles it)
    /// </summary>
    [EnableLogger]
    public sealed class FileStorage
    {
        private readonly string _embeddingsRoot;
        private readonly string _convertedDocsRoot;
        private readonly object _lock = new object();

        public FileStorage(string embeddingsPath, string indexPath, string convertedDocsPath)
        {
            _embeddingsRoot = embeddingsPath;
            _convertedDocsRoot = convertedDocsPath;

            Directory.CreateDirectory(_embeddingsRoot);
        }

        // ==================== Document Embeddings - ASYNC ONLY ====================

        public async Task SaveDocumentEmbeddingsAsync(DocumentEmbeddings doc)
        {
            if (doc == null || string.IsNullOrEmpty(doc.Filename))
                throw new ArgumentException("Invalid document embeddings");

            var rel = RelativePath.Normalize(doc.Filename);
            RelativePath.AssertSafe(rel, nameof(doc.Filename));
            doc.Filename = rel;

            var path = GetEmbeddingsFileAbsolute(rel);
            
            await Task.Run(() =>
            {
                try
                {
                    var json = JsonConvert.SerializeObject(doc, Formatting.None);
                    RelativePath.EnsureParentDirectory(path);
                    File.WriteAllText(path, json, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    LogError($"[FileStorage] Save failed {rel}: {ex.Message}");
                    throw;
                }
            });
        }

        public async Task SaveDocumentEmbeddingsBatchAsync(List<DocumentEmbeddings> documents)
        {
            if (documents == null || documents.Count == 0)
                return;

            Log($"[FileStorage] Batch saving {documents.Count} embeddings...");

            const int batchSize = 20;
            for (int i = 0; i < documents.Count; i += batchSize)
            {
                var batch = documents.Skip(i).Take(batchSize).ToList();
                var tasks = batch.Select(doc => SaveDocumentEmbeddingsAsync(doc)).ToList();
                await Task.WhenAll(tasks);
            }

            Log($"[FileStorage] Batch save complete: {documents.Count} files");
        }

        public async Task<DocumentEmbeddings> LoadDocumentEmbeddingsAsync(string convertedRelativeTxtPath)
        {
            var rel = RelativePath.Normalize(convertedRelativeTxtPath);
            RelativePath.AssertSafe(rel, nameof(convertedRelativeTxtPath));

            var path = GetEmbeddingsFileAbsolute(rel);
            if (!File.Exists(path))
                return null;

            return await Task.Run(() =>
            {
                try
                {
                    var json = File.ReadAllText(path, Encoding.UTF8);
                    var doc = JsonConvert.DeserializeObject<DocumentEmbeddings>(json);
                    if (doc != null)
                        doc.Filename = RelativePath.Normalize(doc.Filename);
                    return doc;
                }
                catch (Exception ex)
                {
                    LogWarning($"[FileStorage] Load failed {rel}: {ex.Message}");
                    return null;
                }
            });
        }

        public DocumentEmbeddings LoadDocumentEmbeddings(string convertedRelativeTxtPath)
        {
            return LoadDocumentEmbeddingsAsync(convertedRelativeTxtPath).GetAwaiter().GetResult();
        }

        public bool NeedsReindex(string convertedRelativeTxtPath)
        {
            var rel = RelativePath.Normalize(convertedRelativeTxtPath);
            RelativePath.AssertSafe(rel, nameof(convertedRelativeTxtPath));

            var embedAbs = GetEmbeddingsFileAbsolute(rel);
            var docAbs = RelativePath.CombineAbsolute(_convertedDocsRoot, rel);

            if (!File.Exists(embedAbs))
                return true;

            if (!File.Exists(docAbs))
                return false;

            try
            {
                var embedTime = File.GetLastWriteTimeUtc(embedAbs);
                var docTime = File.GetLastWriteTimeUtc(docAbs);
                return docTime > embedTime;
            }
            catch
            {
                return true;
            }
        }

        public void RemoveDocument(string convertedRelativeTxtPath)
        {
            var rel = RelativePath.Normalize(convertedRelativeTxtPath);
            RelativePath.AssertSafe(rel, nameof(convertedRelativeTxtPath));

            var path = GetEmbeddingsFileAbsolute(rel);
            if (File.Exists(path))
                File.Delete(path);
        }

        // ==================== Management ====================

        public List<string> GetEmbeddedDocuments()
        {
            try
            {
                if (!Directory.Exists(_embeddingsRoot))
                    return new List<string>();

                var files = DocumentFileEnumerator.EnumerateRelativeFiles(
                    _embeddingsRoot,
                    abs => abs.EndsWith(".embeddings", StringComparison.OrdinalIgnoreCase),
                    useCache: true
                ).ToList();

                var result = new List<string>(files.Count);
                foreach (var relEmb in files)
                {
                    var rel = RelativePath.Normalize(relEmb);

                    if (!rel.EndsWith(".embeddings", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var relDoc = rel.Substring(0, rel.Length - ".embeddings".Length);
                    relDoc = RelativePath.Normalize(relDoc);

                    result.Add(relDoc);
                }

                return result.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception ex)
            {
                LogError($"[FileStorage] List failed: {ex.Message}");
                return new List<string>();
            }
        }

        public void ClearAll()
        {
            try
            {
                if (Directory.Exists(_embeddingsRoot))
                {
                    Directory.Delete(_embeddingsRoot, true);
                    Directory.CreateDirectory(_embeddingsRoot);
                }

                DocumentFileEnumerator.InvalidateCache();

                Log("[FileStorage] All data cleared");
            }
            catch (Exception ex)
            {
                LogError($"[FileStorage] Clear failed: {ex.Message}");
                throw;
            }
        }

        private string GetEmbeddingsFileAbsolute(string convertedRelativeTxtPath)
        {
            var rel = RelativePath.Normalize(convertedRelativeTxtPath);
            RelativePath.AssertSafe(rel, nameof(convertedRelativeTxtPath));

            var relEmb = rel + ".embeddings";
            return RelativePath.CombineAbsolute(_embeddingsRoot, relEmb);
        }
    }
}