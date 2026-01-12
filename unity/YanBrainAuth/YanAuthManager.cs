using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;
using YanPlay.YLogger;
using static YanPlay.YLogger.YLog;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

namespace YanPlay.YanCreditSystem
{
    /// <summary>
    /// Manages Firebase Authentication for the YanPlay Credit System
    /// Handles token refresh and auth state changes
    /// </summary>
    [EnableLogger]
    public class YanAuthManager : MonoBehaviour
    {
        private FirebaseAuth _auth;
        private FirebaseUser _currentUser;
        private DateTime _tokenFetchTime = DateTime.MinValue;
        private string _cachedToken;

        public event Action<FirebaseUser> OnAuthStateChanged;
        public event Action OnUserSignedOut;
        
        public bool IsAuthenticated => _currentUser != null;

#if UNITY_EDITOR
        [ShowInInspector, ReadOnly, PropertyOrder(-1)]
        [BoxGroup("Auth Status")]
        [LabelText("User ID")]
#endif
        public string UserId => _currentUser?.UserId ?? "Not Authenticated";

#if UNITY_EDITOR
        [ShowInInspector, ReadOnly, PropertyOrder(-1)]
        [BoxGroup("Auth Status")]
        [LabelText("Is Authenticated")]
        private bool AuthStatus => IsAuthenticated;

        [ShowInInspector, ReadOnly, PropertyOrder(-1)]
        [BoxGroup("Auth Status")]
        [LabelText("Token Cache Status")]
        private string TokenCacheStatus => _cachedToken != null 
            ? $"Valid (fetched {(DateTime.Now - _tokenFetchTime).TotalSeconds:F0}s ago)" 
            : "No token";

        [BoxGroup("Debug - Login"), PropertyOrder(100)]
        [InfoBox("Testing only works in Play Mode", InfoMessageType.Warning)]
        [LabelText("Email")]
        [SerializeField]
        private string debugEmail = "";

        [BoxGroup("Debug - Login"), PropertyOrder(101)]
        [LabelText("Password")]
        [SerializeField]
        private string debugPassword = "";

        [BoxGroup("Debug - Login"), PropertyOrder(102)]
        [Button("Login", ButtonSizes.Large)]
        [GUIColor(0.4f, 0.8f, 1f)]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private async void DebugLogin()
        {
            if (string.IsNullOrEmpty(debugEmail) || string.IsNullOrEmpty(debugPassword))
            {
                LogError("❌ Please enter both email and password");
                return;
            }

            Log("🔄 Logging in...");
            bool success = await SignInWithEmailPasswordAsync(debugEmail, debugPassword);
            
            if (success)
            {
                Log($"✓ Login successful! User ID: {UserId}");
            }
            else
            {
                LogError("❌ Login failed! Check console for details.");
            }
        }

        [BoxGroup("Debug - Login"), PropertyOrder(103)]
        [Button("Sign Up (Create Account)", ButtonSizes.Large)]
        [GUIColor(0.4f, 1f, 0.6f)]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private async void DebugSignUp()
        {
            if (string.IsNullOrEmpty(debugEmail) || string.IsNullOrEmpty(debugPassword))
            {
                LogError("❌ Please enter both email and password");
                return;
            }

            Log("🔄 Creating account...");
            bool success = await CreateUserWithEmailPasswordAsync(debugEmail, debugPassword);
            
            if (success)
            {
                Log($"✓ Account created successfully! User ID: {UserId}");
            }
            else
            {
                LogError("❌ Account creation failed! Check console for details.");
            }
        }

        [BoxGroup("Debug - Login"), PropertyOrder(104)]
        [Button("Sign Out", ButtonSizes.Medium)]
        [GUIColor(1f, 0.5f, 0.5f)]
        [EnableIf("@UnityEngine.Application.isPlaying")]
        private void DebugSignOut()
        {
            SignOut();
            Log("✓ Signed out successfully");
        }
#endif

        private void Awake()
        {
            InitializeAuth();
        }

        private void InitializeAuth()
        {
            _auth = FirebaseAuth.DefaultInstance;
            _auth.StateChanged += HandleFirebaseAuthStateChanged;
            _currentUser = _auth.CurrentUser;

            if (_currentUser != null)
            {
                Log($"[YanAuth] User already signed in: {_currentUser.UserId}");
                OnAuthStateChanged?.Invoke(_currentUser);
            }
        }

        private void HandleFirebaseAuthStateChanged(object sender, EventArgs eventArgs)
        {
            if (_auth.CurrentUser != _currentUser)
            {
                bool signedIn = _currentUser != _auth.CurrentUser && _auth.CurrentUser != null;
                bool signedOut = _currentUser != null && _auth.CurrentUser == null;
                
                _currentUser = _auth.CurrentUser;

                if (signedIn)
                {
                    Log($"[YanAuth] User signed in: {_currentUser.UserId}");
                    ClearTokenCache();
                    OnAuthStateChanged?.Invoke(_currentUser);
                }
                else if (signedOut)
                {
                    Log("[YanAuth] User signed out");
                    ClearTokenCache();
                    OnUserSignedOut?.Invoke();
                }
            }
        }

        /// <summary>
        /// Sign in with email and password
        /// </summary>
        public async Task<bool> SignInWithEmailPasswordAsync(string email, string password)
        {
            try
            {
                var result = await _auth.SignInWithEmailAndPasswordAsync(email, password);
                _currentUser = result.User;
                Log($"[YanAuth] Sign in successful: {_currentUser.UserId}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"[YanAuth] Sign in failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Create new account with email and password
        /// </summary>
        public async Task<bool> CreateUserWithEmailPasswordAsync(string email, string password)
        {
            try
            {
                var result = await _auth.CreateUserWithEmailAndPasswordAsync(email, password);
                _currentUser = result.User;
        
                // Immediately create Firestore doc to trigger Cloud Function
                await FirebaseFirestore.DefaultInstance
                    .Collection("users")
                    .Document(_currentUser.UserId)
                    .SetAsync(new Dictionary<string, object>
                    {
                        { "email", email },
                        { "createdAt", FieldValue.ServerTimestamp }
                    });
            
                Log($"[YanAuth] Account created: {_currentUser.UserId}");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"[YanAuth] Account creation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the current user's auth token for API requests
        /// Automatically refreshes token if older than 50 minutes
        /// </summary>
        public async Task<string> GetAuthTokenAsync()
        {
            if (_currentUser == null)
            {
                LogWarning("[YanAuth] Cannot get token: No user signed in");
                return null;
            }

            // Check if we have a valid cached token (less than 50 minutes old)
            bool tokenIsValid = _cachedToken != null && 
                               (DateTime.Now - _tokenFetchTime).TotalMinutes < 50;

            if (tokenIsValid)
            {
                return _cachedToken;
            }

            // Fetch fresh token
            try
            {
                bool forceRefresh = _cachedToken != null; // Force refresh if we had an old token
                _cachedToken = await _currentUser.TokenAsync(forceRefresh);
                _tokenFetchTime = DateTime.Now;
                
                if (forceRefresh)
                {
                    Log("[YanAuth] Token refreshed");
                }
                
                return _cachedToken;
            }
            catch (Exception ex)
            {
                LogError($"[YanAuth] Failed to get auth token: {ex.Message}");
                ClearTokenCache();
                return null;
            }
        }

        /// <summary>
        /// Force refresh the auth token
        /// </summary>
        public async Task<bool> RefreshTokenAsync()
        {
            ClearTokenCache();
            string token = await GetAuthTokenAsync();
            return !string.IsNullOrEmpty(token);
        }

        /// <summary>
        /// Sign out the current user
        /// </summary>
        public void SignOut()
        {
            if (_currentUser != null)
            {
                _auth.SignOut();
                _currentUser = null;
                ClearTokenCache();
                Log("[YanAuth] User signed out");
            }
        }

        private void ClearTokenCache()
        {
            _cachedToken = null;
            _tokenFetchTime = DateTime.MinValue;
        }

        private void OnDestroy()
        {
            if (_auth != null)
            {
                _auth.StateChanged -= HandleFirebaseAuthStateChanged;
            }
        }
    }
}