using System.Collections.Generic;

namespace CloudServices
{
    /// <summary>Balances returned by the Cloud Code Bootstrap function (mirrors CityBlocksCCM BootstrapResult).</summary>
    public class PlayerSnapshot
    {
        public long Coins;
        /// <summary>Game power-up name (GameController.PowerUpType) → balance.</summary>
        public Dictionary<string, long> PowerUps = new Dictionary<string, long>();
        public bool AdsRemoved;
        public string ServerTimeUtc = "";
        /// <summary>Coins this call's welcome bundle granted (0 if already given).</summary>
        public long WelcomeCoinsGranted;
        /// <summary>Power-ups moved from the local save by this call.</summary>
        public Dictionary<string, long> Migrated = new Dictionary<string, long>();

        public long PowerUp(string name) => PowerUps != null && PowerUps.TryGetValue(name, out long value) ? value : 0;
    }
}
