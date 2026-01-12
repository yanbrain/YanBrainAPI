using System;

namespace YanPlay.YanCreditSystem.Data
{
    /// <summary>
    /// Base API response wrapper
    /// </summary>
    [Serializable]
    public class ApiResponse
    {
        public bool success;
        public string error;
    }

    /// <summary>
    /// Firestore timestamp from backend
    /// </summary>
    [Serializable]
    public class FirestoreTimestamp
    {
        public long _seconds;
        public long _nanoseconds;
        
        public DateTime ToDateTime()
        {
            return DateTimeOffset.FromUnixTimeSeconds(_seconds).DateTime;
        }
    }

    /// <summary>
    /// Response from /credits/balance endpoint
    /// </summary>
    [Serializable]
    public class CreditBalanceResponse : ApiResponse
    {
        public string userId;
        public int creditsBalance;
        public FirestoreTimestamp creditsUpdatedAt;
    }

    /// <summary>
    /// Response from /credits/consume endpoint
    /// 
    /// NOTE: The consume endpoint does NOT accept a credits parameter in the request.
    /// The server determines the credit cost based on CREDIT_COSTS configuration.
    /// 
    /// Request body:
    /// {
    ///   "productId": "yanDraw" | "yanPhotobooth" | "yanAvatar"
    /// }
    /// 
    /// Response body:
    /// {
    ///   "success": true,
    ///   "userId": "abc123",
    ///   "productId": "yanDraw",
    ///   "creditsSpent": 1
    /// }
    /// </summary>
    [Serializable]
    public class ConsumeCreditsResponse : ApiResponse
    {
        public string userId;
        public string productId;
        public int creditsSpent;
    }

    /// <summary>
    /// Response from /credits/usage endpoint
    /// </summary>
    [Serializable]
    public class CreditUsageResponse : ApiResponse
    {
        public string userId;
        public int totalCredits;
        // Note: totalsByProduct and usagePeriods are parsed manually in YanCreditManager
        // See ParseUsageResponse() method for implementation
    }
}