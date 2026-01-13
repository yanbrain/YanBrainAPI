using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YanBrainAPI.Networking;

namespace YanBrainAPI
{
    public sealed class YanBrainApi
    {
        private readonly YanHttp _http;

        public YanBrainApi(YanHttp http)
        {
            _http = http;
        }

        // -----------------
        // Health
        // -----------------
        public async Task<HealthPayload> HealthAsync(CancellationToken ct = default)
        {
            // /health is not under /api and your server doesn't require auth for it
            return await _http.GetJsonAsync<HealthPayload>("/health", authRequired: false, ct);
        }

        // -----------------
        // LLM
        // -----------------
        public async Task<LlmPayload> LlmAsync(string prompt, string systemPrompt = null, CancellationToken ct = default)
        {
            var req = new LlmRequest { Prompt = prompt, SystemPrompt = systemPrompt };
            var res = await _http.PostJsonAsync<ApiResponse<LlmPayload>>("/api/llm", req, authRequired: true, ct);
            return res.Data;
        }

        // -----------------
        // TTS
        // -----------------
        public async Task<TtsPayload> TtsAsync(string text, string voiceId = null, CancellationToken ct = default)
        {
            var req = new TtsRequest { Text = text, VoiceId = voiceId };
            var res = await _http.PostJsonAsync<ApiResponse<TtsPayload>>("/api/tts", req, authRequired: true, ct);
            return res.Data;
        }

        // -----------------
        // Image
        // -----------------
        public async Task<ImagePayload> ImageAsync(string prompt, string imageBase64 = null, CancellationToken ct = default)
        {
            var req = new ImageRequest { Prompt = prompt, ImageBase64 = imageBase64 };
            var res = await _http.PostJsonAsync<ApiResponse<ImagePayload>>("/api/image", req, authRequired: true, ct);
            return res.Data;
        }

        // -----------------
        // Document Convert
        // -----------------
        public async Task<DocumentConvertPayload> DocumentConvertAsync(List<FileUpload> files, CancellationToken ct = default)
        {
            var req = new DocumentConvertRequest { Files = files };
            var res = await _http.PostJsonAsync<ApiResponse<DocumentConvertPayload>>("/api/documents/convert", req, authRequired: true, ct);
            return res.Data;
        }

        // -----------------
        // Embeddings
        // -----------------
        public async Task<EmbeddingPayload> EmbeddingsAsync(List<EmbeddingItem> items, CancellationToken ct = default)
        {
            var req = new EmbeddingRequest { Items = items };
            var res = await _http.PostJsonAsync<ApiResponse<EmbeddingPayload>>("/api/embeddings", req, authRequired: true, ct);
            return res.Data;
        }

        // -----------------
        // YanAvatar (server-side LLM+TTS; you MUST send relevantDocuments)
        // -----------------
        public async Task<YanAvatarPayload> YanAvatarAsync(
            string userPrompt,
            List<RelevantDocument> relevantDocuments,
            string systemPrompt = null,
            string voiceId = null,
            CancellationToken ct = default)
        {
            var req = new YanAvatarRequest
            {
                UserPrompt = userPrompt,
                RelevantDocuments = relevantDocuments,
                SystemPrompt = systemPrompt,
                VoiceId = voiceId
            };

            var res = await _http.PostJsonAsync<ApiResponse<YanAvatarPayload>>("/api/yanavatar", req, authRequired: true, ct);
            return res.Data;
        }
    }
}
