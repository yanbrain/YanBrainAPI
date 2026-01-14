// File: Assets/Scripts/YanBrainAPI/Embedding/EmbeddingManager.cs

using System;
using System.Collections.Generic;
using System.Threading;
using Sirenix.OdinInspector;
using Sisus.Init;
using YanBrainAPI.Documents;
using YanBrainAPI.Interfaces;
using YanBrainAPI.Networking;
using YanBrainAPI.RAG;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI.Embedding
{
    [EnableLogger]
    public class EmbeddingManager : MonoBehaviour<YanBrainApiConfig, ITokenProvider>
    {
        private YanBrainApiConfig _apiConfig;
        private ITokenProvider _tokenProvider;

        private RAGContext _context;
        private EmbeddingService _embeddingService;

        private CancellationTokenSource _cts;

        // ===== UI/Inspector status =====
        [Title("Embedding Progress")]
        [ShowInInspector, ReadOnly] private int Total => _uiProgress?.Total ?? 0;
        [ShowInInspector, ReadOnly] private int Done => _uiProgress?.Done ?? 0;
        [ShowInInspector, ReadOnly] private int OK => _uiProgress?.Ok ?? 0;
        [ShowInInspector, ReadOnly] private int Failed => _uiProgress?.Failed ?? 0;

        [ShowInInspector, ReadOnly] private string NowEmbedding =>
            string.IsNullOrWhiteSpace(_uiProgress?.CurrentItem) ? "-" : _uiProgress.CurrentItem;

        [ShowInInspector, ReadOnly] private string Status => _uiProgress?.StatusMessage ?? "Ready";
        [ShowInInspector, ReadOnly] private bool IsRunning => _uiProgress?.IsRunning ?? false;
        [ShowInInspector, ReadOnly] private bool IsPaused => _uiProgress?.IsPaused ?? false;

        // ✅ FIXED: Cached instead of property that calls expensive method every frame
        [Title("Embedded Documents")]
        [ShowInInspector, ReadOnly]
        private int _cachedEmbeddedCount = 0;

        [ShowInInspector, ReadOnly, PropertySpace(SpaceBefore = 10)]
        private List<string> _cachedEmbeddedDocs = new List<string>();

        [Button("Refresh Embedded List", ButtonSizes.Medium)]
        [GUIColor(0.7f, 0.7f, 0.9f)]
        [PropertyOrder(20)]
        private void RefreshEmbeddedList()
        {
            if (_embeddingService == null)
            {
                LogError("[EmbeddingManager] Services not initialized!");
                return;
            }
            
            _cachedEmbeddedDocs = _embeddingService.GetEmbeddedDocuments();
            _cachedEmbeddedCount = _cachedEmbeddedDocs.Count;
            Log($"[EmbeddingManager] Refreshed list: {_cachedEmbeddedCount} documents");
        }

        private DocumentProgress _uiProgress;

        protected override void Init(YanBrainApiConfig apiConfig, ITokenProvider tokenProvider)
        {
            _apiConfig = apiConfig;
            _tokenProvider = tokenProvider;
        }

        private void Start()
        {
            InitializeServices();
        }

        private void OnDestroy()
        {
            try
            {
                if (_embeddingService != null)
                    _embeddingService.OnProgressChanged -= HandleProgressChanged;
            }
            catch { /* ignore */ }

            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void InitializeServices()
        {
            _apiConfig.EnsureFoldersExist();

            var http = new YanHttp(_apiConfig.GetBaseUrl(), _apiConfig.TimeoutSeconds, _tokenProvider);
            var api = new YanBrainApi(http);

            _context = new RAGContext(api, _apiConfig);
            _embeddingService = new EmbeddingService(_context);

            _embeddingService.OnProgressChanged += HandleProgressChanged;
            _uiProgress = _embeddingService.GetProgressSnapshot();

            Log("[EmbeddingManager] Services initialized");
            
            // Initial refresh
            RefreshEmbeddedList();
        }

        private void HandleProgressChanged(DocumentProgress p)
        {
            _uiProgress = p;

            if (p != null && p.Total > 0)
            {
                Log($"[Embedding] Progress: {p.Done} / {p.Total} | OK: {p.Ok} | Failed: {p.Failed} | Now: {p.CurrentItem}");
            }
        }

        // ==================== Controls ====================

        [Button("Generate Embeddings", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        [DisableIf(nameof(IsRunning))]
        [PropertyOrder(1)]
        private async void GenerateEmbeddings()
        {
            if (_embeddingService == null)
            {
                LogError("[EmbeddingManager] Services not initialized!");
                return;
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                Log("[EmbeddingManager] Starting embedding generation...");
                await _embeddingService.GenerateAllEmbeddingsAsync(_cts.Token);
                
                // ✅ Refresh list after completion
                RefreshEmbeddedList();
                
                Log($"[EmbeddingManager] ✅ Done. Embedded docs: {_cachedEmbeddedCount}");
            }
            catch (Exception ex)
            {
                LogError($"[EmbeddingManager] Failed: {ex.Message}");
            }
        }

        [Button("Pause", ButtonSizes.Medium)]
        [GUIColor(0.9f, 0.7f, 0.2f)]
        [EnableIf(nameof(CanPause))]
        [PropertyOrder(2)]
        private void Pause()
        {
            _embeddingService?.Pause();
        }

        [Button("Resume", ButtonSizes.Medium)]
        [GUIColor(0.2f, 0.7f, 0.9f)]
        [EnableIf(nameof(CanResume))]
        [PropertyOrder(3)]
        private void Resume()
        {
            _embeddingService?.Resume();
        }

        [Button("Cancel", ButtonSizes.Medium)]
        [GUIColor(0.9f, 0.3f, 0.3f)]
        [EnableIf(nameof(IsRunning))]
        [PropertyOrder(4)]
        private void Cancel()
        {
            _embeddingService?.Cancel();
            _cts?.Cancel();
        }

        [Button("Reset Progress (UI Only)", ButtonSizes.Small)]
        [GUIColor(0.6f, 0.6f, 0.6f)]
        [DisableIf(nameof(IsRunning))]
        [PropertyOrder(5)]
        private void ResetProgress()
        {
            _embeddingService?.ResetProgress();
        }

        private bool CanPause() => IsRunning && !IsPaused;
        private bool CanResume() => IsRunning && IsPaused;

        // ==================== Existing destructive ops ====================

        [Button("Clear Embeddings", ButtonSizes.Large)]
        [GUIColor(0.8f, 0.3f, 0.3f)]
        [DisableIf(nameof(IsRunning))]
        [PropertyOrder(10)]
        private void ClearEmbeddings()
        {
            if (_embeddingService == null)
            {
                LogError("[EmbeddingManager] Services not initialized!");
                return;
            }

            _embeddingService.ClearAllEmbeddings();
            _embeddingService.ResetProgress();
            
            // ✅ Refresh list after clearing
            RefreshEmbeddedList();
            
            Log("[EmbeddingManager] All embeddings cleared");
        }
    }
}