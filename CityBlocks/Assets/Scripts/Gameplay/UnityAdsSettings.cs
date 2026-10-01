using UnityEngine;

namespace Gameplay
{
    /// <summary>Unity Ads identifiers are supplied from a Resources asset after dashboard setup.</summary>
    [CreateAssetMenu(menuName = "CityBlocks/Unity Ads Settings", fileName = "UnityAdsSettings")]
    public sealed class UnityAdsSettings : ScriptableObject
    {
        [Header("Leave blank until Unity Monetization dashboard setup is complete")]
        public string androidGameId = "";
        public string iOSGameId = "";
        public string androidInterstitialAdUnitId = "";
        public string iOSInterstitialAdUnitId = "";
        public bool testMode = true;
    }
}
