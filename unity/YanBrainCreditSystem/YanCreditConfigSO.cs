using Sisus.Init;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

namespace YanPlay.YanCreditSystem
{
    /// <summary>
    /// Configuration for YanPlay Credit System API
    /// </summary>
    [CreateAssetMenu(fileName = "YanCreditConfig", menuName = "YanPlay/YanCreditSystem/YanCreditConfig")]
    [Service(typeof(YanCreditConfigSO), ResourcePath = "YanCreditConfig")]
    public class YanCreditConfigSO : ScriptableObject
    {
        // Available product IDs in the YanPlay ecosystem, used by odin dropdown
        public static readonly string[] PRODUCT_IDS = new string[] 
        { 
            "yanDraw", 
            "yanPhotobooth", 
            "yanAvatar" 
        };

#if UNITY_EDITOR
        [BoxGroup("API Configuration")]
        [LabelText("API URL")]
#endif
        [Tooltip("Your Firebase Cloud Functions API URL")]
        public string apiUrl = "https://us-central1-yanbrainserver.cloudfunctions.net/api";

#if UNITY_EDITOR
        [BoxGroup("Request Settings")]
        [LabelText("Timeout (seconds)")]
#endif
        [Tooltip("Request timeout in seconds")]
        public int requestTimeout = 30;

#if UNITY_EDITOR
        [BoxGroup("Product Configuration")]
        [LabelText("Product ID")]
        [ValueDropdown("@YanCreditConfigSO.PRODUCT_IDS")]
#endif
        [Tooltip("Which product this app is (used for tracking credit consumption)")]
        public string productId = "yanDraw";
    }
}
