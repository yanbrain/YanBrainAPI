// YanBrainAPI/YanBrainApi.cs - UPDATE

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YanBrainAPI.Networking;

namespace YanBrainAPI
{
    public sealed class YanBrainApi
    {
        private readonly YanHttp _http;

        #region Events
        /// <summary>
        /// Fired when server returns 401 Unauthorized
        /// </summary>
        public event Action OnAuthenticationFailed;
        
        /// <summary>
        /// Fired when server returns 402 Payment Required (out of credits)
        /// </summary>
        public event Action OnCreditsExhausted;
        
        /// <summary>
        /// Fired when network/timeout errors occur
        /// </summary>
        public event Action<string> OnNetworkError;
        #endregion

        public YanBrainApi(YanHttp http)
        {
            _http = http;
            
            // Subscribe to YanHttp events
            _http.OnAuthenticationFailed += () => OnAuthenticationFailed?.Invoke();
            _http.OnCreditsExhausted += () => OnCreditsExhausted?.Invoke();
            _http.OnNetworkError += (error) => OnNetworkError?.Invoke(error);
        }

        public async Task<HealthPayload> HealthAsync(CancellationToken ct = default)
        {
            return await _http.GetJsonAsync<HealthPayload>("/health", authRequired: false, ct);
        }

        public async Task<LlmPayload> LlmAsync(
            string prompt,
            string systemPrompt = null,
            string additionalInstructions = null,
            string ragContext = null,
            CancellationToken ct = default)
        {
            var req = new LlmRequest
            {
                Prompt = prompt,
                SystemPrompt = systemPrompt,
                AdditionalInstructions = additionalInstructions,
                RagContext = ragContext
            };
            var res = await _http.PostJsonAsync<ApiResponse<LlmPayload>>("/api/llm", req, authRequired: true, ct);
            return res.Data;
        }

        public async Task<TtsPayload> TtsAsync(string text, string voiceId = null, CancellationToken ct = default)
        {
            var req = new TtsRequest { Text = text, VoiceId = voiceId };
            var res = await _http.PostJsonAsync<ApiResponse<TtsPayload>>("/api/tts", req, authRequired: true, ct);
            return res.Data;
        }

        public async Task<ImagePayload> ImageAsync(string prompt, string imageBase64 = null, CancellationToken ct = default)
        {
            var req = new ImageRequest { Prompt = prompt, ImageBase64 = imageBase64 };
            var res = await _http.PostJsonAsync<ApiResponse<ImagePayload>>("/api/image", req, authRequired: true, ct);
            return res.Data;
        }

        public async Task<DocumentConvertPayload> DocumentConvertAsync(List<FileUpload> files, CancellationToken ct = default)
        {
            var req = new DocumentConvertRequest { Files = files };
            var res = await _http.PostJsonAsync<ApiResponse<DocumentConvertPayload>>("/api/documents/convert", req, authRequired: true, ct);
            return res.Data;
        }

        public async Task<EmbeddingPayload> EmbeddingsAsync(List<EmbeddingItem> items, CancellationToken ct = default)
        {
            var req = new EmbeddingRequest { Items = items };
            var res = await _http.PostJsonAsync<ApiResponse<EmbeddingPayload>>("/api/embeddings", req, authRequired: true, ct);
            return res.Data;
        }

        public async Task<RagTextPayload> RagTextAsync(
            string userPrompt,
            string ragContext,
            string systemPrompt = null,
            string additionalInstructions = null,
            int? maxResponseChars = null,
            CancellationToken ct = default)
        {
            var req = new RagTextRequest
            {
                UserPrompt = userPrompt,
                RagContext = ragContext,
                SystemPrompt = systemPrompt,
                AdditionalInstructions = additionalInstructions,
                MaxResponseChars = maxResponseChars
            };
            var res = await _http.PostJsonAsync<ApiResponse<RagTextPayload>>("/api/rag/text", req, authRequired: true, ct);
            return res.Data;
        }

        public async Task<RagAudioPayload> RagAudioAsync(
            string userPrompt,
            string ragContext,
            string systemPrompt = null,
            string additionalInstructions = null,
            string voiceId = null,
            int? maxResponseChars = null,
            CancellationToken ct = default)
        {
            var req = new RagAudioRequest
            {
                UserPrompt = userPrompt,
                RagContext = ragContext,
                SystemPrompt = systemPrompt,
                AdditionalInstructions = additionalInstructions,
                VoiceId = voiceId,
                MaxResponseChars = maxResponseChars
            };
            var res = await _http.PostJsonAsync<ApiResponse<RagAudioPayload>>("/api/rag/audio", req, authRequired: true, ct);
            return res.Data;
        }
    }
}