using System;

namespace YanPlay.YanCreditSystem.Data
{
    /// <summary>
    /// User's credit balance information
    /// </summary>
    [Serializable]
    public class CreditBalanceData
    {
        public string userId;
        public int creditsBalance;
        public DateTime creditsUpdatedAt;

        public CreditBalanceData(string userId, int balance, DateTime updatedAt)
        {
            this.userId = userId;
            this.creditsBalance = balance;
            this.creditsUpdatedAt = updatedAt;
        }

        public override string ToString()
        {
            return $"User: {userId}, Balance: {creditsBalance}, Updated: {creditsUpdatedAt}";
        }
    }
}