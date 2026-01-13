using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI.RAG
{
    /// <summary>
    /// Handles file I/O for embeddings and index
    /// </summary>
    [EnableLogger]
    public sealed class FileStorage
    {
        private readonly string _embeddingsPath;
        private readonly string _indexPath;
        private readonly string _convertedDocsPath;
        private const string INDEX_FILE = "index.json";
        private readonly object _lock = new object();

        public FileStorage(string embeddingsPath, string indexPath, string convertedDocsPath)
        {
            _embeddingsPath = embeddingsPath;
            _indexPath = indexPath;
            _convertedDocsPath = convertedDocsPath;
            
            Directory.CreateDirectory(_embeddingsPath);
            Directory.CreateDirectory(_indexPath);
        }

        // ==================== Document Embeddings ====================

        public void SaveDocumentEmbeddings(DocumentEmbeddings doc)
        {
            if (doc == null || string.IsNullOrEmpty(doc.Filename))
                throw new ArgumentException("Invalid document embeddings");

            lock (_lock)
            {
                try
                {
                    var path = GetEmbeddingsFilePath(doc.Filename);
                    var json = JsonConvert.SerializeObject(doc, Formatting.Indented);
                    File.WriteAllText(path, json, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    LogError($"[FileStorage] Save failed {doc.Filename}: {ex.Message}");
                    throw;
                }
            }
        }

        public DocumentEmbeddings LoadDocumentEmbeddings(string filename)
        {
            lock (_lock)
            {
                var path = GetEmbeddingsFilePath(filename);
                if (!File.Exists(path))
                    return null;

                try
                {
                    var json = File.ReadAllText(path, Encoding.UTF8);
                    return JsonConvert.DeserializeObject<DocumentEmbeddings>(json);
                }
                catch (Exception ex)
                {
                    LogWarning($"[FileStorage] Load failed {filename}: {ex.Message}");
                    return null;
                }
            }
        }

        public bool NeedsReindex(string filename)
        {
            var embedPath = GetEmbeddingsFilePath(filename);
            var docPath = Path.Combine(_convertedDocsPath, filename);

            if (!File.Exists(embedPath))
                return true;

            if (!File.Exists(docPath))
                return false;

            try
            {
                var embedTime = File.GetLastWriteTimeUtc(embedPath);
                var docTime = File.GetLastWriteTimeUtc(docPath);
                return docTime > embedTime;
            }
            catch
            {
                return true;
            }
        }

        public void RemoveDocument(string filename)
        {
            lock (_lock)
            {
                var path = GetEmbeddingsFilePath(filename);
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        // ==================== Index ====================

        public void SaveIndex(List<DocumentSummary> summaries)
        {
            lock (_lock)
            {
                try
                {
                    var path = Path.Combine(_indexPath, INDEX_FILE);
                    var json = JsonConvert.SerializeObject(summaries, Formatting.Indented);
                    File.WriteAllText(path, json, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    LogError($"[FileStorage] Save index failed: {ex.Message}");
                    throw;
                }
            }
        }

        public List<DocumentSummary> LoadIndex()
        {
            lock (_lock)
            {
                var path = Path.Combine(_indexPath, INDEX_FILE);
                if (!File.Exists(path))
                    return new List<DocumentSummary>();

                try
                {
                    var json = File.ReadAllText(path, Encoding.UTF8);
                    return JsonConvert.DeserializeObject<List<DocumentSummary>>(json) ?? new List<DocumentSummary>();
                }
                catch (Exception ex)
                {
                    LogWarning($"[FileStorage] Load index failed: {ex.Message}");
                    return new List<DocumentSummary>();
                }
            }
        }

        // ==================== Management ====================

        public List<string> GetEmbeddedDocuments()
        {
            lock (_lock)
            {
                try
                {
                    return Directory.GetFiles(_embeddingsPath, "*.embeddings")
                        .Select(Path.GetFileName)
                        .Select(f => f.Replace(".embeddings", ""))
                        .ToList();
                }
                catch (Exception ex)
                {
                    LogError($"[FileStorage] List failed: {ex.Message}");
                    return new List<string>();
                }
            }
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                try
                {
                    if (Directory.Exists(_embeddingsPath))
                    {
                        Directory.Delete(_embeddingsPath, true);
                        Directory.CreateDirectory(_embeddingsPath);
                    }
                    
                    if (Directory.Exists(_indexPath))
                    {
                        Directory.Delete(_indexPath, true);
                        Directory.CreateDirectory(_indexPath);
                    }
                }
                catch (Exception ex)
                {
                    LogError($"[FileStorage] Clear failed: {ex.Message}");
                    throw;
                }
            }
        }

        // ==================== Helpers ====================

        private string GetEmbeddingsFilePath(string filename)
        {
            var safe = filename
                .Replace(Path.DirectorySeparatorChar, '_')
                .Replace(Path.AltDirectorySeparatorChar, '_')
                .Replace(':', '_')
                .Replace('*', '_')
                .Replace('?', '_')
                .Replace('"', '_')
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace('|', '_');

            return Path.Combine(_embeddingsPath, safe + ".embeddings");
        }
    }
}