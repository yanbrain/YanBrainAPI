
using System;
using System.Collections.Generic;
using System.Threading;
using Sirenix.OdinInspector;
using Sisus.Init;
using UnityEngine;
using YanBrain.YLogger;
using YanBrainAPI.Documents;
using YanBrainAPI.Interfaces;
using YanBrainAPI.Networking;
using static YanBrain.YLogger.YLog;

namespace YanBrainAPI.Conversion
{
    [EnableLogger]
    public sealed class ConversionManager : MonoBehaviour<YanBrainApiConfig, ITokenProvider>
    {
        private YanBrainApiConfig _apiConfig;
        private ITokenProvider _tokenProvider;

        private ConversionService _conversionService;
        private CancellationTokenSource _cts;

        private DocumentProgress _uiProgress;

        [Title("Conversion Progress")]
        [ShowInInspector, ReadOnly] private int Total => _uiProgress?.Total ?? 0;
        [ShowInInspector, ReadOnly] private int Done => _uiProgress?.Done ?? 0;
        [ShowInInspector, ReadOnly] private int OK => _uiProgress?.Ok ?? 0;
        [ShowInInspector, ReadOnly] private int Failed => _uiProgress?.Failed ?? 0;

        [ShowInInspector, ReadOnly]
        private string NowConverting => string.IsNullOrWhiteSpace(_uiProgress?.CurrentItem) ? "-" : _uiProgress.CurrentItem;

        [ShowInInspector, ReadOnly] private string Status => _uiProgress?.StatusMessage ?? "Ready";
        [ShowInInspector, ReadOnly] private bool IsRunning => _uiProgress?.IsRunning ?? false;
        [ShowInInspector, ReadOnly] private bool IsPaused => _uiProgress?.IsPaused ?? false;

        // ✅ FIXED: Cached instead of property that calls expensive method every frame
        [Title("Converted Files")]
        [ShowInInspector, ReadOnly]
        private int _cachedConvertedCount = 0;

        [ShowInInspector, ReadOnly, PropertySpace(SpaceBefore = 10)]
        private List<string> _cachedConvertedFiles = new List<string>();

        [Button("Refresh Converted List", ButtonSizes.Medium)]
        [GUIColor(0.7f, 0.7f, 0.9f)]
        [PropertyOrder(20)]
        private void RefreshConvertedList()
        {
            if (_conversionService == null)
            {
                LogError("[ConversionManager] Services not initialized!");
                return;
            }
            
            _cachedConvertedFiles = _conversionService.GetConvertedTextFiles();
            _cachedConvertedCount = _cachedConvertedFiles.Count;
            Log($"[ConversionManager] Refreshed list: {_cachedConvertedCount} files");
        }

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
                if (_conversionService != null)
                    _conversionService.OnProgressChanged -= HandleProgressChanged;
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

            _conversionService = new ConversionService(api, _apiConfig);

            _conversionService.OnProgressChanged += HandleProgressChanged;
            _uiProgress = _conversionService.GetProgressSnapshot();

            Log("[ConversionManager] Services initialized");
            
            // Initial refresh
            RefreshConvertedList();
        }

        private void HandleProgressChanged(DocumentProgress p)
        {
            _uiProgress = p;

            if (p != null && p.Total > 0)
            {
                Log($"[Conversion] Progress: {p.Done} / {p.Total} | OK: {p.Ok} | Failed: {p.Failed} | Now: {p.CurrentItem}");
            }
        }

        // ==================== Controls ====================

        [Button("Convert All Source Documents", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        [DisableIf(nameof(IsRunning))]
        [PropertyOrder(1)]
        private async void ConvertAll()
        {
            if (_conversionService == null)
            {
                LogError("[ConversionManager] Services not initialized!");
                return;
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                Log("[ConversionManager] Starting conversion...");
                await _conversionService.ConvertAllAsync(_cts.Token);
                
                // ✅ Refresh list after completion
                RefreshConvertedList();
                
                Log($"[ConversionManager] ✅ Done. Converted files: {_cachedConvertedCount}");
            }
            catch (Exception ex)
            {
                LogError($"[ConversionManager] Failed: {ex.Message}");
            }
        }

        [Button("Pause", ButtonSizes.Medium)]
        [GUIColor(0.9f, 0.7f, 0.2f)]
        [EnableIf(nameof(CanPause))]
        [PropertyOrder(2)]
        private void Pause()
        {
            _conversionService?.Pause();
        }

        [Button("Resume", ButtonSizes.Medium)]
        [GUIColor(0.2f, 0.7f, 0.9f)]
        [EnableIf(nameof(CanResume))]
        [PropertyOrder(3)]
        private void Resume()
        {
            _conversionService?.Resume();
        }

        [Button("Cancel", ButtonSizes.Medium)]
        [GUIColor(0.9f, 0.3f, 0.3f)]
        [EnableIf(nameof(IsRunning))]
        [PropertyOrder(4)]
        private void Cancel()
        {
            _conversionService?.Cancel();
            _cts?.Cancel();
        }

        [Button("Reset Progress (UI Only)", ButtonSizes.Small)]
        [GUIColor(0.6f, 0.6f, 0.6f)]
        [DisableIf(nameof(IsRunning))]
        [PropertyOrder(5)]
        private void ResetProgress()
        {
            _conversionService?.ResetProgress();
        }

        private bool CanPause() => IsRunning && !IsPaused;
        private bool CanResume() => IsRunning && IsPaused;

        // ==================== Existing destructive op ====================

        [Button("Clear ConvertedDocuments", ButtonSizes.Large)]
        [GUIColor(0.8f, 0.3f, 0.3f)]
        [DisableIf(nameof(IsRunning))]
        [PropertyOrder(10)]
        private void ClearConverted()
        {
            if (_conversionService == null)
            {
                LogError("[ConversionManager] Services not initialized!");
                return;
            }

            _conversionService.ClearConverted();
            _conversionService.ResetProgress();
            
            // ✅ Refresh list after clearing
            RefreshConvertedList();
            
            Log("[ConversionManager] ConvertedDocuments cleared");
        }
    }
}