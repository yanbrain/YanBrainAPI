using System;
using System.Collections.Generic;

namespace YanPlay.YanCreditSystem.Data
{
    /// <summary>
    /// User's credit usage information across periods
    /// Uses dictionaries which are fully supported by Newtonsoft.Json
    /// </summary>
    [Serializable]
    public class CreditUsageData
    {
        public string userId;
        public Dictionary<string, int> totalsByProduct;
        public int totalCredits;
        public List<UsagePeriodData> usagePeriods;

        public CreditUsageData()
        {
            totalsByProduct = new Dictionary<string, int>();
            usagePeriods = new List<UsagePeriodData>();
        }

        public int GetProductTotal(string productId)
        {
            return totalsByProduct.ContainsKey(productId) ? totalsByProduct[productId] : 0;
        }

        public override string ToString()
        {
            return $"User: {userId}, Total Credits: {totalCredits}, Periods: {usagePeriods?.Count ?? 0}";
        }
    }

    /// <summary>
    /// Usage data for a specific time period (month)
    /// Uses dictionaries which are fully supported by Newtonsoft.Json
    /// </summary>
    [Serializable]
    public class UsagePeriodData
    {
        public string id;
        public string period; // Format: "YYYY-MM"
        public Dictionary<string, int> totals;
        public int totalCredits;

        public UsagePeriodData()
        {
            totals = new Dictionary<string, int>();
        }

        public int GetProductTotal(string productId)
        {
            return totals.ContainsKey(productId) ? totals[productId] : 0;
        }

        public override string ToString()
        {
            return $"Period: {period}, Total: {totalCredits}";
        }
    }
}