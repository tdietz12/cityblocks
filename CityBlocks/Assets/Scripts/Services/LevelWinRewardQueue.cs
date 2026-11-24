using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace CloudServices
{
    public class LevelWinRewardQueue : MonoBehaviour
    {
        private const string QueueKey = "cityblocks_level_reward_queue";
        private const float RetrySeconds = 30f;
        private static LevelWinRewardQueue instance;
        private static PendingClaims pending;

        [Serializable] private class Entry { public string playerId; public int level; }
        [Serializable] private class PendingClaims { public List<Entry> entries = new List<Entry>(); }
        [Serializable] private class WalletOwner { public string playerId; }

        private bool sending;
        private float retryAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; pending = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("LevelWinRewardQueue");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LevelWinRewardQueue>();
        }

        public static void Enqueue(int levelNumber)
        {
            if (levelNumber < 1) return;
            Load();
            string owner = KnownPlayerId();
            if (!pending.entries.Exists(e => e.level == levelNumber && e.playerId == owner))
            {
                pending.entries.Add(new Entry { playerId = owner, level = levelNumber });
                Save(); // Persist before sending so a quit/network interruption cannot lose the win.
            }
            if (instance != null) instance.retryAt = 0f;
        }

        private void OnEnable()
        {
            ConnectivityService.OnlineChanged += OnOnlineChanged;
            AccountService.KindChanged += OnAccountChanged;
            Load();
        }
        private void OnDisable()
        {
            ConnectivityService.OnlineChanged -= OnOnlineChanged;
            AccountService.KindChanged -= OnAccountChanged;
        }
        private void OnOnlineChanged(bool online) { if (online) retryAt = 0f; }
        private void OnAccountChanged(string kind) => retryAt = 0f;
        private void OnApplicationPause(bool paused) { if (!paused) retryAt = 0f; }

        private void Update()
        {
            if (sending || Time.realtimeSinceStartup < retryAt || pending == null || pending.entries.Count == 0 ||
                !CloudBootstrap.IsSignedIn || !ConnectivityService.IsOnline) return;
            _ = FlushAsync();
        }

        private async Task FlushAsync()
        {
            if (sending) return;
            sending = true;
            bool processed = false;
            try
            {
                string owner = AccountService.PlayerId;
                if (string.IsNullOrEmpty(owner)) return;
                // An install that has never signed in can earn levels offline. Assign those to its first account.
                foreach (var e in pending.entries) if (string.IsNullOrEmpty(e.playerId)) e.playerId = owner;
                Save();
                var entry = pending.entries.Find(e => e.playerId == owner);
                if (entry == null) return; // Never submit another player's queued wins under this account.

                LevelWinRewardResult response = await CloudFunctions.AwardLevelRewardsAsync(entry.level);
                if (this == null || AccountService.PlayerId != owner) return;
                if (response == null)
                {
                    ConnectivityService.ReportCloudResult(false, "level_reward");
                    return;
                }
                ConnectivityService.ReportCloudResult(true, "level_reward");
                if (response.Status == "not_initialized")
                {
                    await PlayerWallet.RefreshAsync(); // Bootstrap prepares the journal; next retry claims the win.
                    return;
                }
                if (response.Status == "applied" || response.Status == "already_claimed")
                {
                    pending.entries.Remove(entry);
                    Save();
                    processed = true;
                    if (response.Status == "applied") LogGrants(response);
                    await PlayerWallet.RefreshAsync();
                    Debug.Log("[LevelRewards] Level " + entry.level + ": " + response.Status);
                }
                else if (response.Status == "invalid")
                {
                    Debug.LogWarning("[LevelRewards] Level " + entry.level + " is not in the deployed catalog; retained for retry after deployment.");
                }
                // pending is retained: never re-credit locally or assume that the server grant failed.
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[LevelRewards] Claim retained for retry: " + exception.Message);
            }
            finally
            {
                sending = false;
                retryAt = processed ? 0f : Time.realtimeSinceStartup + RetrySeconds;
            }
        }

        private static void LogGrants(LevelWinRewardResult result)
        {
            if (result.Granted == null || result.BalancesAfter == null) return;
            foreach (var grant in result.Granted)
            {
                if (grant.Value <= 0 || !result.BalancesAfter.TryGetValue(grant.Key, out long balance)) continue;
                int amount = (int)Math.Min(int.MaxValue, grant.Value);
                int after = (int)Math.Min(int.MaxValue, balance);
                if (grant.Key == "COINS") Gameplay.LevelAnalytics.CoinsEarned(amount, "level_win", after);
                else
                {
                    Gameplay.GameController.PowerUpType type;
                    switch (grant.Key)
                    {
                        case "PU_DELETE": type = Gameplay.GameController.PowerUpType.delete; break;
                        case "PU_LEVELUP": type = Gameplay.GameController.PowerUpType.levelUp; break;
                        case "PU_LOOKAHEAD": type = Gameplay.GameController.PowerUpType.lookAhead; break;
                        case "PU_EXTRATURNS": type = Gameplay.GameController.PowerUpType.extraTurns; break;
                        default: continue;
                    }
                    Gameplay.LevelAnalytics.PowerUpGranted(type, amount, "level_win", after);
                }
            }
        }

        private static string KnownPlayerId()
        {
            string owner = AccountService.PlayerId;
            if (!string.IsNullOrEmpty(owner)) return owner;
            string cache = PlayerPrefs.GetString("cityblocks_wallet_cache", "");
            if (string.IsNullOrEmpty(cache)) return "";
            try { return JsonUtility.FromJson<WalletOwner>(cache)?.playerId ?? ""; }
            catch (Exception) { return ""; }
        }
        private static void Load()
        {
            if (pending != null) return;
            string json = PlayerPrefs.GetString(QueueKey, "");
            try { pending = string.IsNullOrEmpty(json) ? new PendingClaims() : JsonUtility.FromJson<PendingClaims>(json); }
            catch (Exception exception) { Debug.LogWarning("[LevelRewards] Unreadable pending queue: " + exception.Message); }
            if (pending == null) pending = new PendingClaims();
            if (pending.entries == null) pending.entries = new List<Entry>();
        }
        private static void Save()
        {
            PlayerPrefs.SetString(QueueKey, JsonUtility.ToJson(pending));
            PlayerPrefs.Save();
        }
    }
}
