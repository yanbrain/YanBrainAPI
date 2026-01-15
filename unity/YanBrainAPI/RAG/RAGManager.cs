using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using Sisus.Init;
using UnityEngine;
using YanBrain.YLogger;
using YanBrainAPI.Interfaces;
using YanBrainAPI.Networking;
using YanBrainAPI.Utils;
using static YanBrain.YLogger.YLog;

namespace YanBrainAPI.RAG
{
    [EnableLogger]
    [RequireComponent(typeof(AudioPlayer))]
    public sealed class RAGManager : MonoBehaviour<YanBrainService, IRagAudioProvider>
    {
        private YanBrainService _service;
        private IRagAudioProvider _provider;
        private DocumentSearcher _searcher;
        private AudioPlayer _audioPlayer;
        private CancellationTokenSource _cts;

        #region Inspector - Testing
        [Title("Testing")]
        [TextArea(3, 10)]
        public string userQuestion;

        [Space(5)]
        public string systemPrompt;
        public string additionalInstructions;
        public string voiceId;

        [Range(100, 1000)]
        public int maxResponseChars = 300;
        #endregion

        #region Inspector - Status
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

        [ShowInInspector, ReadOnly]
        private bool IsProcessing => _isProcessing;

        [ShowInInspector, ReadOnly]
        private string Status => _statusMessage;

        [Title("Results")]
        [ShowInInspector, ReadOnly, TextArea(5, 15)]
        private string LastResponse => _lastLLMResponse;

        [ShowInInspector, ReadOnly]
        private string AudioDataSize => _lastAudioData != null ? $"{_lastAudioData.Length / 1024}KB" : "No audio";
        #endregion

        #region State
        private bool _isProcessing;
        private string _statusMessage = "Ready";
        private string _lastLLMResponse;
        private byte[] _lastAudioData;
        private string IndexFilePath => Path.Combine(_service.Config.GetIndexPath(), "index");
        #endregion

        protected override void Init(YanBrainService service, IRagAudioProvider provider)
        {
            _service = service;
            _provider = provider;
        }

        protected override void OnAwake()
        {
            _searcher = new DocumentSearcher(_service.Api, _service.Storage, _service.RagConfig);
            _audioPlayer = GetComponent<AudioPlayer>();

            Log("[RAGManager] Initialized");

            if (File.Exists(IndexFilePath))
            {
                _ = AutoLoadIndexAsync();
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _searcher?.Dispose();
        }

        #region Public API
        public async Task<(RagAudioPayload payload, float ragTime, float serverTime)> QueryAsync(
            string userPrompt,
            string systemPrompt = null,
            string additionalInstructions = null,
            string voiceId = null,
            int? maxResponseChars = null,
            CancellationToken ct = default)
        {
            if (!IsIndexBuilt)
            {
                LogError("[RAGManager] Index not ready");
                throw new InvalidOperationException("Index not ready. Build index first.");
            }

            var ragStart = Time.realtimeSinceStartup;
            var relevantDocs = await _searcher.QueryAsync(userPrompt);
            var docTexts = relevantDocs.Select(d => d.Text);
            var ragContext = string.Join("\n\n---\n\n", docTexts);
            var ragTime = Time.realtimeSinceStartup - ragStart;

            var serverStart = Time.realtimeSinceStartup;
            var payload = await _provider.GetRagAudioAsync(
                userPrompt,
                ragContext,
                systemPrompt,
                additionalInstructions,
                voiceId,
                maxResponseChars,
                ct
            );
            var serverTime = Time.realtimeSinceStartup - serverStart;

            return (payload, ragTime, serverTime);
        }

        public bool IsReady() => IsIndexBuilt;

        public async Task BuildIndexAsync()
        {
            await _searcher.BuildIndexAsync();
        }

        public async Task SaveIndexAsync()
        {
            if (!IsIndexBuilt) return;

            var indexData = _searcher.ExportIndexData();
            var json = JsonConvert.SerializeObject(indexData, Formatting.None);

            await Task.Run(() =>
            {
                Directory.CreateDirectory(_service.Config.GetIndexPath());
                File.WriteAllText(IndexFilePath, json, System.Text.Encoding.UTF8);
            });
        }

        public async Task LoadIndexAsync()
        {
            if (!File.Exists(IndexFilePath)) return;

            string json = await Task.Run(() => File.ReadAllText(IndexFilePath, System.Text.Encoding.UTF8));
            var indexData = JsonConvert.DeserializeObject<IndexData>(json);
            _searcher.ImportIndexData(indexData);
        }
        #endregion

        #region Index Management Buttons
        [Button("Build Index from Embeddings", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        [PropertyOrder(1)]
        public async void BuildIndex()
        {
            try
            {
                Log("[RAGManager] Building index...");
                var startTime = Time.realtimeSinceStartup;

                await BuildIndexAsync();

                var elapsed = Time.realtimeSinceStartup - startTime;
                Log($"[RAGManager] ✅ Index built in {elapsed:F2}s: {IndexedChunks} chunks from {IndexedDocuments} docs");
            }
            catch (Exception ex)
            {
                LogError($"[RAGManager] Build failed: {ex.Message}");
            }
        }

        [Button("Save Index to Disk", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.6f, 0.9f)]
        [PropertyOrder(2)]
        [EnableIf(nameof(IsIndexBuilt))]
        public async void SaveIndex()
        {
            try
            {
                Log("[RAGManager] Saving index...");
                await SaveIndexAsync();
                Log($"[RAGManager] ✅ Index saved: {IndexFilePath}");
            }
            catch (Exception ex)
            {
                LogError($"[RAGManager] Save failed: {ex.Message}");
            }
        }

        [Button("Load Index from Disk", ButtonSizes.Large)]
        [GUIColor(0.9f, 0.6f, 0.3f)]
        [PropertyOrder(3)]
        public async void LoadIndex()
        {
            if (!File.Exists(IndexFilePath))
            {
                LogWarning($"[RAGManager] No index file at {IndexFilePath}");
                return;
            }

            await AutoLoadIndexAsync();
        }

        [Button("Clear Index Memory", ButtonSizes.Medium)]
        [GUIColor(0.9f, 0.5f, 0.2f)]
        [PropertyOrder(4)]
        [EnableIf(nameof(IsIndexBuilt))]
        public void ClearIndexMemory()
        {
            _searcher?.Dispose();
            _searcher = new DocumentSearcher(_service.Api, _service.Storage, _service.RagConfig);
            Log("[RAGManager] Index cleared from memory");
        }

        [Button("Delete Index File", ButtonSizes.Medium)]
        [GUIColor(1.0f, 0.3f, 0.3f)]
        [PropertyOrder(5)]
        public void DeleteIndexFile()
        {
            if (File.Exists(IndexFilePath))
            {
                File.Delete(IndexFilePath);
                Log($"[RAGManager] Index file deleted");
            }
        }
        #endregion

        #region Testing Buttons
        [Button("Ask Question (Text)", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        [PropertyOrder(10)]
        [DisableIf(nameof(_isProcessing))]
        private async void AskQuestionText()
        {
            if (!ValidateBeforeQuery()) return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _isProcessing = true;
            _lastLLMResponse = null;

            try
            {
                _statusMessage = "Querying documents...";
                var relevantDocs = await _searcher.QueryAsync(userQuestion);
                var docTexts = relevantDocs.Select(d => d.Text);
                var ragContext = string.Join("\n\n---\n\n", docTexts);

                _statusMessage = "Calling LLM...";
                var payload = await _service.Api.RagTextAsync(
                    userQuestion,
                    ragContext,
                    systemPrompt,
                    additionalInstructions,
                    maxResponseChars,
                    _cts.Token
                );

                _lastLLMResponse = payload.TextResponse;
                _statusMessage = "✓ Complete";
                Log($"[RAGManager] Answer: {_lastLLMResponse}");
            }
            catch (Exception ex)
            {
                _statusMessage = $"Error: {ex.Message}";
                LogError($"[RAGManager] Failed: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        [Button("Ask Question (Audio)", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.6f, 0.9f)]
        [PropertyOrder(11)]
        [DisableIf(nameof(_isProcessing))]
        private async void AskQuestionAudio()
        {
            if (!ValidateBeforeQuery()) return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _isProcessing = true;
            _lastLLMResponse = null;
            _lastAudioData = null;

            try
            {
                _statusMessage = "Querying RAG...";
                
                var (payload, ragTime, serverTime) = await QueryAsync(
                    userQuestion,
                    systemPrompt,
                    additionalInstructions,
                    voiceId,
                    maxResponseChars,
                    _cts.Token
                );

                _lastAudioData = Convert.FromBase64String(payload.AudioBase64);
                _lastLLMResponse = payload.TextResponse;
                _statusMessage = "✓ Complete";
                
                var totalTime = ragTime + serverTime;
                Log($"[RAGManager] 📊 RAG: {ragTime:F2}s | Server: {serverTime:F2}s | Total: {totalTime:F2}s");
                Log($"[RAGManager] Answer: {_lastLLMResponse}");
            }
            catch (Exception ex)
            {
                _statusMessage = $"Error: {ex.Message}";
                LogError($"[RAGManager] Failed: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        [Button("Play Audio", ButtonSizes.Large)]
        [GUIColor(0.9f, 0.6f, 0.3f)]
        [PropertyOrder(12)]
        [EnableIf(nameof(HasAudioData))]
        private async void PlayAudio()
        {
            if (_lastAudioData == null) return;

            try
            {
                _statusMessage = "♪ Playing audio...";
                await _audioPlayer.PlayAudioAsync(_lastAudioData);
                _statusMessage = "✓ Playback complete";
            }
            catch (Exception ex)
            {
                _statusMessage = $"Playback error: {ex.Message}";
                LogError($"[RAGManager] Playback failed: {ex.Message}");
            }
        }
        #endregion

        #region Helpers
        private async Task AutoLoadIndexAsync()
        {
            try
            {
                Log("[RAGManager] Auto-loading index...");
                var startTime = Time.realtimeSinceStartup;

                await LoadIndexAsync();

                var elapsed = Time.realtimeSinceStartup - startTime;
                Log($"[RAGManager] ✅ Index loaded in {elapsed:F2}s: {IndexedChunks} chunks");
            }
            catch (Exception ex)
            {
                LogError($"[RAGManager] Auto-load failed: {ex.Message}");
            }
        }

        private bool ValidateBeforeQuery()
        {
            if (string.IsNullOrWhiteSpace(userQuestion))
            {
                LogError("[RAGManager] User question is empty");
                return false;
            }

            if (!IsIndexBuilt)
            {
                LogError("[RAGManager] Index not ready");
                _statusMessage = "⚠ Index not ready";
                return false;
            }

            return true;
        }

        private bool HasAudioData() => _lastAudioData != null && _lastAudioData.Length > 0;

        private string CalculateIndexSize()
        {
            if (!IsIndexBuilt || _searcher == null) return "N/A";

            var chunks = IndexedChunks;
            var dimension = _searcher.GetDimension();
            var totalBytes = (chunks * dimension * 4) + (chunks * 8);

            return $"{totalBytes / 1024f / 1024f:F2} MB";
        }

        private string GetIndexFileStatus()
        {
            if (_service == null || string.IsNullOrEmpty(IndexFilePath))
                return "Not initialized";

            if (!File.Exists(IndexFilePath))
                return "No saved index";

            var fileInfo = new FileInfo(IndexFilePath);
            var sizeMB = fileInfo.Length / 1024f / 1024f;
            var age = DateTime.Now - fileInfo.LastWriteTime;

            if (age.TotalDays >= 1)
                return $"{sizeMB:F2} MB ({age.TotalDays:F0}d ago)";
            else if (age.TotalHours >= 1)
                return $"{sizeMB:F2} MB ({age.TotalHours:F0}h ago)";
            else
                return $"{sizeMB:F2} MB ({age.TotalMinutes:F0}m ago)";
        }
        #endregion
    }
}