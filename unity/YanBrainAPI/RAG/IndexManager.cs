using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using Sisus.Init;
using UnityEngine;
using YanBrainAPI.Interfaces;
using YanBrainAPI.Networking;
using YanBrainAPI.RAG;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI
{
    [EnableLogger]
    public sealed class IndexManager : MonoBehaviour<YanBrainApiConfig, ITokenProvider>
    {
        private YanBrainApiConfig _config;
        private ITokenProvider _tokenProvider;
        private RAGContext _context;
        private DocumentSearcher _searcher;
        private YanBrainApi _api;

        [Title("Index Status")]
        [ShowInInspector, ReadOnly]
        private bool IsIndexBuilt => _searcher?.IsIndexReady() ?? false;

        [ShowInInspector, ReadOnly]
        private int IndexedChunks => _searcher?.GetIndexedCount() ?? 0;

        [ShowInInspector, ReadOnly]
        private int IndexedDocuments => _searcher?.GetIndexedDocuments()?.Count ?? 0;

        [ShowInInspector, ReadOnly]
        private string IndexMemorySize => CalculateIndexSize();

        [ShowInInspector, ReadOnly]
        private string IndexFileStatus => GetIndexFileStatus();

        private string IndexFilePath => _config != null ? Path.Combine(_config.GetIndexPath(), "index") : string.Empty;

        protected override void Init(YanBrainApiConfig config, ITokenProvider tokenProvider)
        {
            _config = config;
            _tokenProvider = tokenProvider;
        }

        protected override void OnAwake()
        {
            _config.EnsureFoldersExist();

            var http = new YanHttp(_config.GetBaseUrl(), _config.TimeoutSeconds, _tokenProvider);
            _api = new YanBrainApi(http);
            _context = new RAGContext(_api, _config);
    
            // Create DocumentSearcher with context
            _searcher = new DocumentSearcher(_context);

            Log("[IndexManager] Initialized");
    
            // Auto-load index if available
            if (File.Exists(IndexFilePath))
            {
                _ = AutoLoadIndexAsync();
            }
            else
            {
                Log("[IndexManager] No saved index found. You'll need to build one.");
            }
        }

        private async Task AutoLoadIndexAsync()
        {
            try
            {
                Log("[IndexManager] Auto-loading index from disk...");
                var startTime = Time.realtimeSinceStartup;

                string json = await Task.Run(() => File.ReadAllText(IndexFilePath, System.Text.Encoding.UTF8));
                var indexData = JsonConvert.DeserializeObject<IndexData>(json);

                _searcher.ImportIndexData(indexData);

                var elapsed = Time.realtimeSinceStartup - startTime;
                Log($"[IndexManager] ✅ Index auto-loaded in {elapsed:F2}s");
                Log($"[IndexManager] {IndexedChunks} chunks from {IndexedDocuments} documents ready");
            }
            catch (Exception ex)
            {
                LogError($"[IndexManager] Auto-load failed: {ex.Message}");
                Log("[IndexManager] You can manually rebuild the index using the 'Build Index' button");
            }
        }


        public DocumentSearcher GetSearcher() => _searcher;

        [Button("Build Index from Embeddings", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        [PropertyOrder(1)]
        public async void BuildIndex()
        {
            try
            {
                Log("[IndexManager] Building index from embeddings...");
                var startTime = Time.realtimeSinceStartup;

                await _searcher.BuildIndexAsync();

                var elapsed = Time.realtimeSinceStartup - startTime;
                Log($"[IndexManager] ✅ Index built in {elapsed:F2}s: {IndexedChunks} chunks from {IndexedDocuments} documents");
                Log($"[IndexManager] Memory usage: {IndexMemorySize}");
            }
            catch (Exception ex)
            {
                LogError($"[IndexManager] Build failed: {ex.Message}");
            }
        }

        [Button("Save Index to Disk", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.6f, 0.9f)]
        [PropertyOrder(2)]
        [EnableIf(nameof(IsIndexBuilt))]
        public async void SaveIndex()
        {
            if (!IsIndexBuilt)
            {
                LogWarning("[IndexManager] No index to save. Build first.");
                return;
            }

            try
            {
                Log("[IndexManager] Saving index to disk...");
                var startTime = Time.realtimeSinceStartup;

                var indexData = _searcher.ExportIndexData();
                var json = JsonConvert.SerializeObject(indexData, Formatting.None);

                await Task.Run(() =>
                {
                    Directory.CreateDirectory(_config.GetIndexPath());
                    File.WriteAllText(IndexFilePath, json, System.Text.Encoding.UTF8);
                });

                var elapsed = Time.realtimeSinceStartup - startTime;
                var fileSize = new FileInfo(IndexFilePath).Length / 1024f / 1024f;
                Log($"[IndexManager] ✅ Index saved in {elapsed:F2}s: {IndexFilePath}");
                Log($"[IndexManager] File size: {fileSize:F2} MB");
            }
            catch (Exception ex)
            {
                LogError($"[IndexManager] Save failed: {ex.Message}");
            }
        }

        [Button("Load Index from Disk", ButtonSizes.Large)]
        [GUIColor(0.9f, 0.6f, 0.3f)]
        [PropertyOrder(3)]
        public async void LoadIndex()
        {
            if (!File.Exists(IndexFilePath))
            {
                LogWarning($"[IndexManager] No index file found at {IndexFilePath}");
                return;
            }

            try
            {
                Log("[IndexManager] Loading index from disk...");
                var startTime = Time.realtimeSinceStartup;

                string json = await Task.Run(() => File.ReadAllText(IndexFilePath, System.Text.Encoding.UTF8));
                var indexData = JsonConvert.DeserializeObject<IndexData>(json);

                _searcher.ImportIndexData(indexData);

                var elapsed = Time.realtimeSinceStartup - startTime;
                Log($"[IndexManager] ✅ Index loaded in {elapsed:F2}s");
                Log($"[IndexManager] {IndexedChunks} chunks from {IndexedDocuments} documents");
                Log($"[IndexManager] Memory usage: {IndexMemorySize}");
            }
            catch (Exception ex)
            {
                LogError($"[IndexManager] Load failed: {ex.Message}");
            }
        }

        [Button("Clear Index (Memory Only)", ButtonSizes.Medium)]
        [GUIColor(0.9f, 0.5f, 0.2f)]
        [PropertyOrder(4)]
        [EnableIf(nameof(IsIndexBuilt))]
        public void ClearIndexMemory()
        {
            if (_searcher != null)
            {
                _searcher.Dispose();
                _searcher = new DocumentSearcher(_context); // Recreate with context
                Log("[IndexManager] Index cleared from memory");
            }
        }

        [Button("Delete Index File", ButtonSizes.Medium)]
        [GUIColor(1.0f, 0.3f, 0.3f)]
        [PropertyOrder(5)]
        public void DeleteIndexFile()
        {
            if (File.Exists(IndexFilePath))
            {
                File.Delete(IndexFilePath);
                Log($"[IndexManager] Index file deleted: {IndexFilePath}");
            }
            else
            {
                LogWarning("[IndexManager] No index file to delete");
            }
        }

        private string CalculateIndexSize()
        {
            if (!IsIndexBuilt || _searcher == null) return "N/A";

            var chunks = IndexedChunks;
            var dimension = _searcher.GetDimension();

            var embeddingsBytes = chunks * dimension * 4;
            var metadataBytes = chunks * 8;
            var totalBytes = embeddingsBytes + metadataBytes;

            return $"{totalBytes / 1024f / 1024f:F2} MB";
        }

        private string GetIndexFileStatus()
        {
            if (_config == null || string.IsNullOrEmpty(IndexFilePath))
                return "Not initialized";
                
            if (!File.Exists(IndexFilePath))
                return "No saved index";

            var fileInfo = new FileInfo(IndexFilePath);
            var sizeMB = fileInfo.Length / 1024f / 1024f;
            var age = DateTime.Now - fileInfo.LastWriteTime;

            if (age.TotalDays >= 1)
                return $"{sizeMB:F2} MB (saved {age.TotalDays:F0}d ago)";
            else if (age.TotalHours >= 1)
                return $"{sizeMB:F2} MB (saved {age.TotalHours:F0}h ago)";
            else
                return $"{sizeMB:F2} MB (saved {age.TotalMinutes:F0}m ago)";
        }

        private void OnDestroy()
        {
            _searcher?.Dispose();
        }
    }
}
