// Assets/Scripts/YanBrainAPI/RAG/RAGManager.cs - UPDATE
using System;
using System.Linq;
using System.Threading;
using Sirenix.OdinInspector;
using Sisus.Init;
using UnityEngine;
using YanBrainAPI.Interfaces;
using YanBrainAPI.Networking;
using YanBrainAPI.Utils;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;

namespace YanBrainAPI.RAG
{
    [EnableLogger]
    [RequireComponent(typeof(AudioPlayer))]
    public sealed class RAGManager : MonoBehaviour<YanBrainApiConfig, ITokenProvider>
    {
        private YanBrainApiConfig _apiConfig;
        private ITokenProvider _tokenProvider;

        private RAGContext _context;
        private RAGService _ragService;
        private YanBrainApi _api;
        private AudioPlayer _audioPlayer;
        private CancellationTokenSource _cts;

        [Title("Input")]
        [TextArea(3, 10)]
        public string userQuestion;

        [Space(5)]
        public string systemPrompt;
        public string voiceId;

        [Space(5)]
        [Range(100, 1000)]
        [Tooltip("Maximum character limit for LLM response (server enforces 300 max)")]
        public int maxResponseChars = 300;

        [Title("Status")]
        [ShowInInspector, ReadOnly]
        private bool IsProcessing => _isProcessing;

        [ShowInInspector, ReadOnly]
        private string Status => _statusMessage;

        [Title("Results")]
        [ShowInInspector, ReadOnly, TextArea(5, 15)]
        private string LastResponse => _lastLLMResponse;

        [ShowInInspector, ReadOnly]
        private string AudioDataSize => _lastAudioData != null ? $"{_lastAudioData.Length / 1024}KB" : "No audio";

        private bool _isProcessing;
        private string _statusMessage = "Ready";
        private string _lastLLMResponse;
        private byte[] _lastAudioData;

        protected override void Init(YanBrainApiConfig apiConfig, ITokenProvider tokenProvider)
        {
            _apiConfig = apiConfig;
            _tokenProvider = tokenProvider;
        }

        protected override void OnAwake()
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
            _api = new YanBrainApi(http);
            _context = new RAGContext(_api, _apiConfig);
            _ragService = new RAGService(_context);
            _audioPlayer = GetComponent<AudioPlayer>();

            Log("[RAGManager] Initialized");
        }

        [Button("Ask Question (Text)", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        [PropertyOrder(1)]
        [DisableIf(nameof(_isProcessing))]
        private async void AskQuestionText()
        {
            if (string.IsNullOrWhiteSpace(userQuestion))
            {
                LogError("[RAGManager] User question is empty");
                return;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            _isProcessing = true;
            _lastLLMResponse = null;

            try
            {
                _statusMessage = "Querying documents...";
                var relevantDocs = await _ragService.QueryAsync(userQuestion, _cts.Token);

                _statusMessage = "Merging context...";
                var ragContext = string.Join("\n\n---\n\n", relevantDocs.Select(d => d.Text));

                _statusMessage = "Calling LLM...";
                var payload = await _api.RagTextAsync(
                    userQuestion, 
                    ragContext, 
                    systemPrompt, 
                    maxResponseChars, 
                    _cts.Token
                );

                _lastLLMResponse = payload.TextResponse;
                _statusMessage = "✓ Complete";

                Log($"[RAGManager] Answer: {_lastLLMResponse}");
            }
            catch (OperationCanceledException)
            {
                _statusMessage = "Cancelled";
                LogWarning("[RAGManager] Question cancelled");
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
        [PropertyOrder(2)]
        [DisableIf(nameof(_isProcessing))]
        private async void AskQuestionAudio()
        {
            if (string.IsNullOrWhiteSpace(userQuestion))
            {
                LogError("[RAGManager] User question is empty");
                return;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            _isProcessing = true;
            _lastLLMResponse = null;
            _lastAudioData = null;

            try
            {
                _statusMessage = "Querying documents...";
                var relevantDocs = await _ragService.QueryAsync(userQuestion, _cts.Token);

                _statusMessage = "Merging context...";
                var ragContext = string.Join("\n\n---\n\n", relevantDocs.Select(d => d.Text));

                _statusMessage = "Calling RAG Audio (LLM + TTS)...";
                var payload = await _api.RagAudioAsync(
                    userQuestion,
                    ragContext,
                    systemPrompt,
                    voiceId,
                    maxResponseChars,
                    _cts.Token
                );

                _statusMessage = "Decoding audio...";
                _lastAudioData = Convert.FromBase64String(payload.AudioBase64);
                _lastLLMResponse = payload.TextResponse;

                _statusMessage = "✓ Complete - Ready to play";

                Log($"[RAGManager] Answer: {_lastLLMResponse}");
                Log($"[RAGManager] Audio ready: {_lastAudioData.Length} bytes");
            }
            catch (OperationCanceledException)
            {
                _statusMessage = "Cancelled";
                LogWarning("[RAGManager] Question cancelled");
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
        [PropertyOrder(3)]
        [EnableIf(nameof(HasAudioData))]
        private async void PlayAudio()
        {
            if (_lastAudioData == null || _lastAudioData.Length == 0)
            {
                LogError("[RAGManager] No audio data available");
                return;
            }

            try
            {
                _statusMessage = "♪ Playing audio...";
                Log($"[RAGManager] Playing {_lastAudioData.Length} bytes of audio");

                await _audioPlayer.PlayAudioAsync(_lastAudioData);

                _statusMessage = "✓ Playback complete";
                Log("[RAGManager] Audio playback finished");
            }
            catch (Exception ex)
            {
                _statusMessage = $"Playback error: {ex.Message}";
                LogError($"[RAGManager] Playback failed: {ex.Message}");
            }
        }

        private bool HasAudioData() => _lastAudioData != null && _lastAudioData.Length > 0;
    }
}