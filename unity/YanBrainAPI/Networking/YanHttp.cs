// YanBrainAPI/Networking/YanHttp.cs - ADD EVENT EMISSION

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using YanBrainAPI.Interfaces;

namespace YanBrainAPI.Networking
{
    public sealed class YanHttp
    {
        private readonly string _baseUrl;
        private readonly int _timeoutSeconds;
        private readonly ITokenProvider _tokenProvider;
        private readonly HttpClient _httpClient;

        #region Events
        public event Action OnAuthenticationFailed;
        public event Action OnCreditsExhausted;
        public event Action<string> OnNetworkError;
        #endregion

        public YanHttp(string baseUrl, int timeoutSeconds, ITokenProvider tokenProvider)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _timeoutSeconds = timeoutSeconds;
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));

            // Ensure base URL has trailing slash for proper URL joining
            if (!_baseUrl.EndsWith("/"))
            {
                _baseUrl += "/";
            }

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds),
                BaseAddress = new Uri(_baseUrl)
            };
        }

        public async Task<T> GetJsonAsync<T>(string endpoint, bool authRequired, CancellationToken ct = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            if (authRequired) await AddAuthHeaderAsync(request);

            return await SendRequestAsync<T>(request, ct);
        }

        public async Task<T> PostJsonAsync<T>(string endpoint, object body, bool authRequired, CancellationToken ct = default)
        {
            var json = JsonConvert.SerializeObject(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
            if (authRequired) await AddAuthHeaderAsync(request);

            return await SendRequestAsync<T>(request, ct);
        }

        private async Task AddAuthHeaderAsync(HttpRequestMessage request)
        {
            try
            {
                var token = await _tokenProvider.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    throw new UnauthorizedAccessException("No authentication token available");
                }
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[YanHttp] Failed to get auth token: {ex.Message}");
                throw;
            }
        }

private async Task<T> SendRequestAsync<T>(HttpRequestMessage request, CancellationToken ct)
{
    HttpResponseMessage response = null;

    try
    {
        response = await _httpClient.SendAsync(request, ct);

        // READ RESPONSE BODY BEFORE THROWING (for error details)
        var responseBody = await response.Content.ReadAsStringAsync();

        // Check status code and emit events
        if (response.StatusCode == HttpStatusCode.Unauthorized) // 401
        {
            Debug.LogError($"[YanHttp] Authentication failed (401): {responseBody}");
            OnAuthenticationFailed?.Invoke();
            throw new UnauthorizedAccessException("Authentication failed");
        }

        if (response.StatusCode == HttpStatusCode.PaymentRequired) // 402
        {
            Debug.LogError($"[YanHttp] Credits exhausted (402): {responseBody}");
            OnCreditsExhausted?.Invoke();
            throw new InvalidOperationException("Credits exhausted");
        }

        if (!response.IsSuccessStatusCode)
        {
            Debug.LogError($"[YanHttp] Request failed ({response.StatusCode}): {responseBody}");
            throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}).");
        }

        return JsonConvert.DeserializeObject<T>(responseBody);
    }
    catch (TaskCanceledException)
    {
        var errorMsg = "Request timed out";
        Debug.LogError($"[YanHttp] {errorMsg}");
        OnNetworkError?.Invoke(errorMsg);
        throw new TimeoutException(errorMsg);
    }
    catch (HttpRequestException ex)
    {
        var errorMsg = $"Network error: {ex.Message}";
        Debug.LogError($"[YanHttp] {errorMsg}");
        OnNetworkError?.Invoke(errorMsg);
        throw;
    }
    catch (UnauthorizedAccessException)
    {
        throw; // Already logged and event emitted
    }
    catch (InvalidOperationException)
    {
        throw; // Already logged and event emitted
    }
    catch (Exception ex)
    {
        var errorMsg = $"Request failed: {ex.Message}";
        Debug.LogError($"[YanHttp] {errorMsg}");
        OnNetworkError?.Invoke(errorMsg);
        throw;
    }
    finally
    {
        response?.Dispose();
    }
}
    }
}