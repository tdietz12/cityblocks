using System.Collections.Generic;

namespace CityBlocks.CloudCode
{
    /// <summary>Economy IDs (Assets/CloudServices/Economy) and the game's power-up names they map to.</summary>
    public static class EconomyIds
    {
        public const string Coins = "COINS";
        public const string NoAds = "NO_ADS";

        /// <summary>Game power-up name (GameController.PowerUpType) → Economy currency ID.</summary>
        public static readonly IReadOnlyDictionary<string, string> PowerUpCurrencies = new Dictionary<string, string>
        {
            { "delete", "PU_DELETE" },
            { "lookAhead", "PU_LOOKAHEAD" },
            { "levelUp", "PU_LEVELUP" },
            { "extraTurns", "PU_EXTRATURNS" },
        };
    }

    /// <summary>Tuning constants (option B: no Remote Config) and pure helpers that are unit-tested.</summary>
    public static class Rules
    {
        /// <summary>Welcome bundle: coins only. Power-ups come from level-up rewards.</summary>
        public const long WelcomeCoins = 500;

        /// <summary>Most of each power-up an updating player can bring over from their local save (guards against tampered saves).</summary>
        public const int MaxMigratedPerPowerUp = 20;

        /// <summary>Protected Cloud Save keys marking one-time grants (only Cloud Code can write Protected data).</summary>
        public const string WelcomeGrantedKey = "welcome_granted";
        public const string LegacyMigratedKey = "legacy_migrated";

        /// <summary>
        /// Turns the client's local power-up counts (by game name) into Economy grants: unknown names and
        /// non-positive counts are ignored, each type is capped at MaxMigratedPerPowerUp.
        /// Returns game name → amount to grant.
        /// </summary>
        public static Dictionary<string, long> PlanMigration(IDictionary<string, int>? localPowerUps)
        {
            var plan = new Dictionary<string, long>();
            if (localPowerUps == null) return plan;
            foreach (var entry in localPowerUps)
            {
                if (!EconomyIds.PowerUpCurrencies.ContainsKey(entry.Key) || entry.Value <= 0) continue;
                plan[entry.Key] = System.Math.Min(entry.Value, MaxMigratedPerPowerUp);
            }
            return plan;
        }
    }
}
