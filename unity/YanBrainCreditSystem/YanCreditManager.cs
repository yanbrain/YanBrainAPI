using System;
using System.Text;
using System.Threading.Tasks;
using Firebase.Auth;
using Sisus.Init;
using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using YanPlay.YanCreditSystem.Data;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

namespace YanPlay.YanCreditSystem
{
    /// <summary>
    /// Manages credit operations for YanPlay products
    /// Auto-refreshes on login, handles token expiry, and manages insufficient credits
    /// 
    /// IMPORTANT: Credit costs are defined server-side in CREDIT_COSTS.
    /// Unity only sends productId - the server determines how many credits to consume.
    /// </summary>
    [EnableLogger]
    public class YanCreditManager : MonoBehaviour<YanCreditConfigSO, YanAuthManager>
    {
        private YanCreditConfigSO _config;
        private YanAuthManager _authManager;

        public event Action<CreditBalanceData> OnBalanceUpdated;
        public event Action<string> OnError;
        public event Action OnInsufficientCredits;
        public event Action<int, string> OnCreditsConsumed; // amount, productId

        private CreditBalanceData _cachedBalance;
        private DateTime _lastBalanceFetch = DateTime.MinValue;
        private bool _isRefreshing;
        private const int WELCOME_CREDITS_DELAY_MS = 2500; // Wait for Cloud Function

#if UNITY_EDITOR
        [ShowInInspector, ReadOnly, PropertyOrder(-1)]
        [BoxGroup("Credit Status")]
        [LabelText("Current Balance")]
        private int CurrentBalance => _cachedBalance?.creditsBalance ?? 0;

        [ShowInInspector, ReadOnly, PropertyOrder(-1)]
        [BoxGroup("Credit Status")]
        [LabelText("User ID")]
        private string CurrentUserId => _cachedBalance?.userId ?? "Not Loaded";

        [ShowInInspector, ReadOnly, PropertyOrder(-1)]
        [BoxGroup("Credit Status")]
        [LabelText("Last Updated")]
        private string LastUpdated => _cachedBalance?.creditsUpdatedAt.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never";

        [ShowInInspector, ReadOnly, PropertyOrder(-1)]
        [BoxGroup("Credit Status")]
        [LabelText("Last Fetch")]
        private string LastFetch => _lastBalanceFetch != DateTime.MinValue 
            ? $"{(DateTime.Now - _lastBalanceFetch).TotalSeconds:F0}s ago" 
            : "Never";

        [ShowInInspector, ReadOnly, PropertyOrder(-1)]
        [BoxGroup("Credit Status")]
        [LabelText("Product ID (from Config)")]
        private string ConfigProductId => _config?.productId ?? "Not Set";

        [BoxGroup("Debug - Credits"), PropertyOrder(100)]
        [InfoBox("Testing only works in Play Mode", InfoMessageType.Warning)]
        [Button("Refresh Balance", ButtonSizes.Large)]
        [GUIColor(0.4f, 1f, 0.4f)]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private async void DebugRefreshBalance()
        {
            if (_authManager == null || !_authManager.IsAuthenticated)
            {
                LogError("❌ Not authenticated! Please login first.");
                return;
            }

            Log("🔄 Fetching credit balance...");
            var balance = await GetBalanceAsync();
            
            if (balance != null)
            {
                Log($"✓ Balance: {balance.creditsBalance} credits");
            }
            else
            {
                LogError("❌ Failed to fetch balance");
            }
        }

        [BoxGroup("Debug - Credits"), PropertyOrder(102)]
        [Button("Consume Credits (using Config Product ID)", ButtonSizes.Large)]
        [GUIColor(1f, 0.4f, 0.4f)]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private async void DebugConsumeCredits()
        {
            if (_authManager == null || !_authManager.IsAuthenticated)
            {
                LogError("❌ Not authenticated! Please login first.");
                return;
            }

            string productId = _config?.productId ?? "yanDraw";
            Log($"🔄 Consuming credits for product '{productId}' (server will determine amount)...");
            
            var response = await ConsumeCreditsAsync();
            
            if (response != null)
            {
                Log($"✓ Successfully consumed {response.creditsSpent} credits");
                Log($"✓ Product tracked as: {response.productId}");
            }
            else
            {
                LogError("❌ Failed to consume credits");
            }
        }

        [BoxGroup("Debug - Credits"), PropertyOrder(103)]
        [Button("Get Usage Statistics", ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.8f, 1f)]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private async void DebugGetUsage()
        {
            if (_authManager == null || !_authManager.IsAuthenticated)
            {
                LogError("❌ Not authenticated! Please login first.");
                return;
            }

            Log("🔄 Fetching usage statistics...");
            var usage = await GetUsageAsync();
            
            if (usage != null)
            {
                Log($"✓ Total Credits Used: {usage.totalCredits}");
                Log($"✓ Usage Periods: {usage.usagePeriods.Count}");
                
                foreach (var kvp in usage.totalsByProduct)
                {
                    Log($"  - {kvp.Key}: {kvp.Value} credits");
                }
            }
            else
            {
                LogError("❌ Failed to fetch usage");
            }
        }
#endif

        protected override void Init(YanCreditConfigSO config, YanAuthManager authManager)
        {
            _config = config;
            _authManager = authManager;

            // Subscribe to auth events
            _authManager.OnAuthStateChanged += HandleAuthStateChanged;
            _authManager.OnUserSignedOut += HandleUserSignedOut;

            // If user already authenticated, fetch balance
            if (_authManager.IsAuthenticated)
            {
                _ = RefreshBalanceOnLoginAsync();
            }
        }

        private async void HandleAuthStateChanged(FirebaseUser user)
        {
            if (user != null)
            {
                await RefreshBalanceOnLoginAsync();
            }
        }

        private void HandleUserSignedOut()
        {
            _cachedBalance = null;
            _lastBalanceFetch = DateTime.MinValue;
        }

        private async Task RefreshBalanceOnLoginAsync()
        {
            if (_isRefreshing) return;
            
            _isRefreshing = true;
            
            try
            {
                // Wait for welcome credits Cloud Function to process
                await Task.Delay(WELCOME_CREDITS_DELAY_MS);
                
                var balance = await GetBalanceAsync();
                if (balance != null)
                {
                    Log($"[YanCredit] Balance loaded: {balance.creditsBalance} credits");
                }
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        #region Public API Methods

        /// <summary>
        /// Get the current user's credit balance
        /// Uses cached value if recent (less than 60 seconds old)
        /// </summary>
        public async Task<CreditBalanceData> GetBalanceAsync(bool forceRefresh = false)
        {
            if (!_authManager.IsAuthenticated)
            {
                LogWarning("[YanCredit] Cannot get balance: User not authenticated");
                OnError?.Invoke("User not authenticated");
                return null;
            }

            // Return cached balance if recent and not forcing refresh
            bool balanceIsRecent = _cachedBalance != null && 
                                   (DateTime.Now - _lastBalanceFetch).TotalSeconds < 60;

            if (balanceIsRecent && !forceRefresh)
            {
                return _cachedBalance;
            }

            try
            {
                var response = await SendRequestWithRetryAsync(() =>
                    SendGetRequestAsync("/credits/balance")
                );
                
                if (response.success)
                {
                    var balanceResponse = JsonConvert.DeserializeObject<CreditBalanceResponse>(response.json);
                    
                    _cachedBalance = new CreditBalanceData(
                        balanceResponse.userId,
                        balanceResponse.creditsBalance,
                        balanceResponse.creditsUpdatedAt?.ToDateTime() ?? DateTime.Now
                    );
                    
                    _lastBalanceFetch = DateTime.Now;
                    OnBalanceUpdated?.Invoke(_cachedBalance);
                    
                    return _cachedBalance;
                }
                else
                {
                    LogError($"[YanCredit] Failed to get balance: {response.error}");
                    OnError?.Invoke(response.error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogError($"[YanCredit] Exception getting balance: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Get detailed credit usage statistics
        /// </summary>
        /// <param name="limit">Number of monthly periods to retrieve (default: 6, max: 12)</param>
        public async Task<CreditUsageData> GetUsageAsync(int limit = 6)
        {
            if (!_authManager.IsAuthenticated)
            {
                LogWarning("[YanCredit] Cannot get usage: User not authenticated");
                OnError?.Invoke("User not authenticated");
                return null;
            }

            try
            {
                var response = await SendRequestWithRetryAsync(() =>
                    SendGetRequestAsync($"/credits/usage?limit={limit}")
                );
                
                if (response.success)
                {
                    var usageData = ParseUsageResponse(response.json);
                    return usageData;
                }
                else
                {
                    LogError($"[YanCredit] Failed to get usage: {response.error}");
                    OnError?.Invoke(response.error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogError($"[YanCredit] Exception getting usage: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Consume credits for the configured product
        /// The server determines how many credits to consume based on CREDIT_COSTS
        /// </summary>
        public async Task<ConsumeResponseData> ConsumeCreditsAsync()
        {
            string productId = _config?.productId ?? "yanDraw";
            return await ConsumeCreditsAsync(productId);
        }

        /// <summary>
        /// Consume credits for a specific product
        /// The server determines how many credits to consume based on CREDIT_COSTS
        /// Credits are deducted from the global pool, but tracked per product for analytics
        /// </summary>
        /// <param name="productId">The product ID that is consuming credits</param>
        public async Task<ConsumeResponseData> ConsumeCreditsAsync(string productId)
        {
            if (!_authManager.IsAuthenticated)
            {
                LogWarning("[YanCredit] Cannot consume credits: User not authenticated");
                OnError?.Invoke("User not authenticated");
                return null;
            }

            try
            {
                var requestData = new
                {
                    productId = productId
                    // NOTE: No credits field - server decides based on CREDIT_COSTS
                };

                var response = await SendRequestWithRetryAsync(() =>
                    SendPostRequestAsync("/credits/consume", JsonConvert.SerializeObject(requestData))
                );
                
                if (response.success)
                {
                    var consumeResponse = JsonConvert.DeserializeObject<ConsumeCreditsResponse>(response.json);
                    var responseData = new ConsumeResponseData
                    {
                        userId = consumeResponse.userId,
                        productId = consumeResponse.productId,
                        creditsSpent = consumeResponse.creditsSpent
                    };

                    Log($"[YanCredit] Credits consumed: {responseData.creditsSpent} for {productId}");
                    
                    // Emit consumption event
                    OnCreditsConsumed?.Invoke(responseData.creditsSpent, responseData.productId);
                    
                    // Update cached balance
                    await GetBalanceAsync();
                    
                    return responseData;
                }
                else
                {
                    // Check for insufficient credits error
                    if (IsInsufficientCreditsError(response.error))
                    {
                        LogWarning($"[YanCredit] Insufficient credits");
                        OnInsufficientCredits?.Invoke();
                    }
                    
                    LogError($"[YanCredit] Failed to consume credits: {response.error}");
                    OnError?.Invoke(response.error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogError($"[YanCredit] Exception consuming credits: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Send request with automatic retry on failure (up to 3 attempts)
        /// </summary>
        private async Task<(bool success, string json, string error)> SendRequestWithRetryAsync(
            Func<Task<(bool success, string json, string error)>> requestFunc,
            int maxRetries = 3)
        {
            int attempt = 0;
            
            while (attempt < maxRetries)
            {
                attempt++;
                
                var result = await requestFunc();
                
                // Success - return immediately
                if (result.success)
                {
                    return result;
                }
                
                // Check if error is 401 (token expired) - try refreshing token
                if (result.error.Contains("401") || result.error.Contains("Invalid or expired token"))
                {
                    LogWarning($"[YanCredit] Token expired, refreshing... (attempt {attempt}/{maxRetries})");
                    
                    bool tokenRefreshed = await _authManager.RefreshTokenAsync();
                    if (!tokenRefreshed)
                    {
                        return (false, null, "Failed to refresh auth token");
                    }
                    
                    continue; // Retry with new token
                }
                
                // Other errors - retry with exponential backoff
                if (attempt < maxRetries)
                {
                    int delayMs = 500 * attempt; // 500ms, 1000ms, 1500ms
                    LogWarning($"[YanCredit] Request failed, retrying in {delayMs}ms... (attempt {attempt}/{maxRetries})");
                    await Task.Delay(delayMs);
                }
                else
                {
                    // Final attempt failed
                    return result;
                }
            }
            
            return (false, null, "Max retries exceeded");
        }

        private async Task<(bool success, string json, string error)> SendGetRequestAsync(string endpoint)
        {
            string token = await _authManager.GetAuthTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return (false, null, "Failed to get auth token");
            }

            string url = _config.apiUrl + endpoint;
            
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Authorization", $"Bearer {token}");
                request.timeout = _config.requestTimeout;

                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    return (true, request.downloadHandler.text, null);
                }
                else
                {
                    string errorMsg = $"Request failed: {request.error}";
                    if (!string.IsNullOrEmpty(request.downloadHandler.text))
                    {
                        errorMsg += $" - {request.downloadHandler.text}";
                    }
                    return (false, null, errorMsg);
                }
            }
        }

        private async Task<(bool success, string json, string error)> SendPostRequestAsync(string endpoint, string jsonData)
        {
            string token = await _authManager.GetAuthTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return (false, null, "Failed to get auth token");
            }

            string url = _config.apiUrl + endpoint;
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {token}");
                request.timeout = _config.requestTimeout;

                var operation = request.SendWebRequest();
                
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    return (true, request.downloadHandler.text, null);
                }
                else
                {
                    string errorMsg = $"Request failed: {request.error}";
                    if (!string.IsNullOrEmpty(request.downloadHandler.text))
                    {
                        errorMsg += $" - {request.downloadHandler.text}";
                    }
                    return (false, null, errorMsg);
                }
            }
        }

        private bool IsInsufficientCreditsError(string error)
        {
            if (string.IsNullOrEmpty(error)) return false;
            
            string errorLower = error.ToLower();
            return errorLower.Contains("insufficient credits") || 
                   errorLower.Contains("not enough credits") ||
                   errorLower.Contains("insufficient") && errorLower.Contains("credit");
        }

        private CreditUsageData ParseUsageResponse(string json)
        {
            var jsonObj = JObject.Parse(json);
            
            var usageData = new CreditUsageData
            {
                userId = jsonObj["userId"]?.ToString(),
                totalCredits = jsonObj["totalCredits"]?.ToObject<int>() ?? 0
            };

            // Parse totalsByProduct dictionary
            var totalsByProduct = jsonObj["totalsByProduct"];
            if (totalsByProduct != null)
            {
                foreach (var prop in totalsByProduct.Children<JProperty>())
                {
                    usageData.totalsByProduct[prop.Name] = prop.Value.ToObject<int>();
                }
            }

            // Parse usage periods array
            var usagePeriodsArray = jsonObj["usagePeriods"];
            if (usagePeriodsArray != null)
            {
                foreach (var periodObj in usagePeriodsArray)
                {
                    var period = new UsagePeriodData
                    {
                        id = periodObj["id"]?.ToString(),
                        period = periodObj["period"]?.ToString(),
                        totalCredits = periodObj["totalCredits"]?.ToObject<int>() ?? 0
                    };

                    // Parse totals for this period
                    var totals = periodObj["totals"];
                    if (totals != null)
                    {
                        foreach (var prop in totals.Children<JProperty>())
                        {
                            period.totals[prop.Name] = prop.Value.ToObject<int>();
                        }
                    }

                    usageData.usagePeriods.Add(period);
                }
            }

            return usageData;
        }

        #endregion

        private void OnDestroy()
        {
            // Unsubscribe from auth events
            if (_authManager != null)
            {
                _authManager.OnAuthStateChanged -= HandleAuthStateChanged;
                _authManager.OnUserSignedOut -= HandleUserSignedOut;
            }
        }
    }
}