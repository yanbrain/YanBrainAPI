using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace YanBrainAPI.Networking
{
    public interface ITokenProvider
    {
        Task<string> GetIdTokenAsync(CancellationToken ct = default);
    }

    public sealed class YanHttp
    {
        private readonly string _baseUrl;
        private readonly int _timeoutSeconds;
        private readonly ITokenProvider _tokenProvider;

        public YanHttp(string baseUrl, int timeoutSeconds, ITokenProvider tokenProvider)
        {
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "" : baseUrl.Trim().TrimEnd('/');
            _timeoutSeconds = Mathf.Clamp(timeoutSeconds, 5, 120);
            _tokenProvider = tokenProvider;
        }

        public Task<T> GetJsonAsync<T>(string path, bool authRequired, CancellationToken ct = default)
            => SendAsync<T>("GET", path, null, authRequired, ct);

        public Task<T> PostJsonAsync<T>(string path, object body, bool authRequired, CancellationToken ct = default)
            => SendAsync<T>("POST", path, body, authRequired, ct);

        private async Task<T> SendAsync<T>(string method, string path, object body, bool authRequired, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_baseUrl))
                throw new Exception("YanHttp: BaseUrl is empty.");

            var url = _baseUrl + (path.StartsWith("/") ? path : "/" + path);

            using var req = new UnityWebRequest(url, method);
            req.timeout = _timeoutSeconds;

            if (body != null)
            {
                var json = JsonConvert.SerializeObject(body);
                var bytes = Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(bytes);
                req.SetRequestHeader("Content-Type", "application/json");
            }

            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Accept", "application/json");

            if (authRequired)
            {
                if (_tokenProvider == null)
                    throw new Exception("YanHttp: authRequired but no ITokenProvider provided.");

                var token = await _tokenProvider.GetIdTokenAsync(ct);
                if (string.IsNullOrWhiteSpace(token))
                    throw new Exception("YanHttp: empty Firebase ID token.");

                req.SetRequestHeader("Authorization", "Bearer " + token);
            }

            var op = req.SendWebRequest();

            // ✅ FIXED: Proper Unity async handling
            // Use TaskCompletionSource to bridge UnityWebRequest callback → Task
            var tcs = new TaskCompletionSource<bool>();

            op.completed += _ => tcs.TrySetResult(true);

            // Register cancellation
            using var ctr = ct.Register(() =>
            {
                req.Abort();
                tcs.TrySetCanceled();
            });

            await tcs.Task;

            var raw = req.downloadHandler?.text ?? "";

            // Transport error
            if (req.result != UnityWebRequest.Result.Success)
            {
                TryThrowApiEnvelopeError(raw);
                throw new Exception($"HTTP {(int)req.responseCode}: {req.error ?? "Request failed"} | {Trim(raw)}");
            }

            // HTTP OK but server might return {success:false}
            TryThrowApiEnvelopeError(raw);

            // Deserialize
            try
            {
                return JsonConvert.DeserializeObject<T>(raw);
            }
            catch (Exception ex)
            {
                throw new Exception($"YanHttp: JSON parse failed into {typeof(T).Name}. Raw: {Trim(raw)} | {ex.Message}");
            }
        }

        private static void TryThrowApiEnvelopeError(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;

            try
            {
                var probe = JsonConvert.DeserializeObject<ApiResponse<object>>(raw);
                if (probe != null && probe.Success == false && probe.Error != null)
                    throw new Exception($"API {probe.Error.StatusCode} {probe.Error.Code}: {probe.Error.Message}");
            }
            catch (JsonException)
            {
                // not envelope JSON -> ignore
            }
        }

        private static string Trim(string s, int max = 400)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}