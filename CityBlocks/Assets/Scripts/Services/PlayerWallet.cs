using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace CloudServices
{
    /// <summary>
    /// Last known cloud balances (coins, power-ups, no-ads) from Bootstrap. Read-only for the game: balances only
    /// change through Cloud Code. Cached per player in PlayerPrefs so the UI shows a value instantly on launch and
    /// while offline. Fires Changed whenever new values arrive.
    /// </summary>
    public static class PlayerWallet
    {
        private const string CacheKey = "cityblocks_wallet_cache";

        [Serializable]
        private class PowerUpEntry { public string name; public long count; }

        [Serializable]
        private class Cache
        {
            public string playerId;
            public long coins;
            public bool adsRemoved;
            public List<PowerUpEntry> powerUps = new List<PowerUpEntry>();
            public string updatedUtc;
        }

        private static Cache current;
        private static bool cacheLoaded;
        private static Task<bool> refreshTask;
        private static int revision;

        /// <summary>Raisedon the main thread when balances change (refresh or cache load for a new player).</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = null;
            cacheLoaded = false;
            refreshTask = null;
            revision = 0;
            Changed = null;
        }

        /// <summary>True once balances for the current player are known (from the cloud or the cache).</summary>
        public static bool HasValue { get { EnsureCacheLoaded(); return current != null && current.playerId == CurrentPlayerId; } }
        public static long Coins => HasValue ? current.coins : 0;
        public static bool AdsRemoved => HasValue && current.adsRemoved;

        /// <summary>Balance of a power-up by game name (GameController.PowerUpType), 0 if unknown.</summary>
        public static long PowerUp(string name)
        {
            if (!HasValue) return 0;
            foreach (var entry in current.powerUps) if (entry.name == name) return entry.count;
            return 0;
        }

        /// <summary>
        /// Fetches fresh balances via Bootstrap (concurrent callers share one call). Returns false when offline /
        /// failed; the cached values stay in place.
        /// </summary>
        public static Task<bool> RefreshAsync()
        {
            if (refreshTask == null || refreshTask.IsCompleted) refreshTask = RefreshInternalAsync();
            return refreshTask;
        }

        private static async Task<bool> RefreshInternalAsync()
        {
            if (!await CloudBootstrap.EnsureSignedInAsync()) return false;
            string owner = AccountService.PlayerId;
            var migration = Gameplay.PowerUpStore.LegacyMigrationPayload(owner);
            int startedRevision = revision;
            PlayerSnapshot snapshot = await CloudFunctions.BootstrapAsync(migration);
            if (snapshot == null || AccountService.PlayerId != owner || revision != startedRevision) return false;
            Apply(snapshot);
            if (migration != null) Gameplay.PowerUpStore.MarkMigrated(owner);
            return true;
        }

        public static void ApplyPowerUpBalance(string playerId, string name, long balance)
        {
            if (!HasValue || AccountService.PlayerId != playerId) return;
            var entry = current.powerUps.Find(p => p.name == name);
            if (entry == null) current.powerUps.Add(new PowerUpEntry { name = name, count = balance });
            else entry.count = balance;
            revision++;
            PlayerPrefs.SetString(CacheKey, JsonUtility.ToJson(current));
            PlayerPrefs.Save();
            RaiseChanged();
        }

        /// <summary>Stores a snapshot returned by any cloud call and notifies listeners.</summary>
        public static void Apply(PlayerSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrEmpty(CurrentPlayerId)) return;
            var cache = new Cache
            {
                playerId = CurrentPlayerId,
                coins = snapshot.Coins,
                adsRemoved = snapshot.AdsRemoved,
                updatedUtc = DateTime.UtcNow.ToString("o")
            };
            if (snapshot.PowerUps != null)
                foreach (var entry in snapshot.PowerUps) cache.powerUps.Add(new PowerUpEntry { name = entry.Key, count = entry.Value });
            current = cache;
            PlayerPrefs.SetString(CacheKey, JsonUtility.ToJson(cache));
            PlayerPrefs.Save();
            RaiseChanged();
        }

        private static string CurrentPlayerId => AccountService.PlayerId ?? (current != null ? current.playerId : null);

        private static void EnsureCacheLoaded()
        {
            if (cacheLoaded) return;
            cacheLoaded = true;
            string json = PlayerPrefs.GetString(CacheKey, "");
            if (string.IsNullOrEmpty(json)) return;
            try { current = JsonUtility.FromJson<Cache>(json); }
            catch (Exception exception) { Debug.LogWarning("[PlayerWallet] Ignoring unreadable cache: " + exception.Message); }
        }

        private static void RaiseChanged()
        {
            try { Changed?.Invoke(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
