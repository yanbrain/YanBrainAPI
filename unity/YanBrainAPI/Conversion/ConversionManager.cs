using System;
using System.Collections.Generic;
using System.Threading;
using Sirenix.OdinInspector;
using Sisus.Init;
using UnityEngine;
using YanBrainAPI.Conversion;
using YanBrainAPI.Interfaces;
using YanBrainAPI.Networking;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI.Conversion
{
    [EnableLogger]
    public sealed class ConversionManager : MonoBehaviour<YanBrainApiConfig, ITokenProvider>
    {
        private YanBrainApiConfig _apiConfig;
        private ITokenProvider _tokenProvider;

        private ConversionService _conversionService;
        private CancellationTokenSource _cts;

        [Title("Status")]
        [ShowInInspector, ReadOnly]
        private int ConvertedCount => _conversionService?.GetConvertedTextFiles().Count ?? 0;

        [ShowInInspector, ReadOnly, PropertySpace(SpaceBefore = 10)]
        private List<string> ConvertedFiles => _conversionService?.GetConvertedTextFiles() ?? new List<string>();

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
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void InitializeServices()
        {
            _apiConfig.EnsureFoldersExist();

            var http = new YanHttp(_apiConfig.GetBaseUrl(), _apiConfig.TimeoutSeconds, _tokenProvider);
            var api = new YanBrainApi(http);

            _conversionService = new ConversionService(api, _apiConfig);

            Log("[ConversionManager] Services initialized");
        }

        [Button("Convert All Source Documents", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        [PropertyOrder(1)]
        private async void ConvertAll()
        {
            if (_conversionService == null)
            {
                LogError("[ConversionManager] Services not initialized!");
                return;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                Log("[ConversionManager] Starting conversion...");
                await _conversionService.ConvertAllAsync(_cts.Token);
                Log($"[ConversionManager] ✅ Complete! Converted files: {ConvertedCount}");
            }
            catch (OperationCanceledException)
            {
                LogWarning("[ConversionManager] Conversion cancelled");
            }
            catch (Exception ex)
            {
                LogError($"[ConversionManager] Failed: {ex.Message}");
            }
        }

        [Button("Clear ConvertedDocuments", ButtonSizes.Large)]
        [GUIColor(0.8f, 0.3f, 0.3f)]
        [PropertyOrder(2)]
        private void ClearConverted()
        {
            if (_conversionService == null)
            {
                LogError("[ConversionManager] Services not initialized!");
                return;
            }

            _conversionService.ClearConverted();
            Log("[ConversionManager] ConvertedDocuments cleared");
        }
    }
}
