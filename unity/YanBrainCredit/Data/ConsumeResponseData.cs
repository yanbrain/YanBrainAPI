using System;

namespace YanPlay.YanCreditSystem.Data
{
    /// <summary>
    /// Response data when consuming credits
    /// </summary>
    [Serializable]
    public class ConsumeResponseData
    {
        public string userId;
        public string productId;
        public int creditsSpent;

        public override string ToString()
        {
            return $"User: {userId}, Product: {productId}, Spent: {creditsSpent}";
        }
    }
}