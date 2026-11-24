using System.Collections.Generic;

namespace CityBlocks.CloudCode
{
    /// <summary>Snapshot returned by Bootstrap: current balances plus what this call granted.</summary>
    public class BootstrapResult
    {
        public long Coins { get; set; }
        /// <summary>Game power-up name → balance.</summary>
        public Dictionary<string, long> PowerUps { get; set; } = new Dictionary<string, long>();
        public bool AdsRemoved { get; set; }
        public string ServerTimeUtc { get; set; } = "";
        /// <summary>Coins granted by this call's welcome bundle (0 if it was already given).</summary>
        public long WelcomeCoinsGranted { get; set; }
        /// <summary>Game power-up name → amount moved from the local save by this call (empty if none / already done).</summary>
        public Dictionary<string, long> Migrated { get; set; } = new Dictionary<string, long>();
    }
}
