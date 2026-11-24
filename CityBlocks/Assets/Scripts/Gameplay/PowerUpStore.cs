using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay
{
    /// <summary>Inventory and purchase boundary for power-up use bundles.</summary>
    public static class PowerUpStore
    {
        private const string NoAdsEntitlementKey = "cityblocks_no_ads_owned";
        public static event Action<string> PurchaseRequested;
        public static event Action<int> InterstitialRequested;

        public static LevelProgressData LoadProgress()
        {
            LevelProgressData data = LevelProgressStore.Load();
            if (data.powerUps == null) data.powerUps = new PowerUpInventory();
            return data;
        }

        private static bool cloudUseBusy;
        public static bool IsBusy => cloudUseBusy;
        public static string Feedback { get; private set; } = "";
        public static bool CanUse => CloudServices.ConnectivityService.IsOnline && CloudServices.CloudBootstrap.IsSignedIn &&
            CloudServices.PlayerWallet.HasValue && MigrationReady && !cloudUseBusy;
        public static bool CanAccessStore => CloudServices.ConnectivityService.IsOnline && CloudServices.CloudBootstrap.IsSignedIn;
        private static bool MigrationReady => !string.IsNullOrEmpty(CloudServices.AccountService.PlayerId) &&
            PlayerPrefs.GetInt("cityblocks_powerups_migrated_" + CloudServices.AccountService.PlayerId, 0) == 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCloudStatics() { cloudUseBusy = false; Feedback = ""; }

        public static Dictionary<string, int> LegacyMigrationPayload(string playerId)
        {
            if (PlayerPrefs.GetInt("cityblocks_powerups_migrated_" + playerId, 0) == 1) return null;
            string owner = PlayerPrefs.GetString("cityblocks_powerup_legacy_owner", "");
            if (string.IsNullOrEmpty(owner))
            {
                owner = playerId;
                PlayerPrefs.SetString("cityblocks_powerup_legacy_owner", owner);
                PlayerPrefs.Save();
            }
            var result = new Dictionary<string, int>();
            if (owner != playerId) return result;
            var inventory = LoadProgress().powerUps;
            int testCopies = PlayerPrefs.GetInt("cityblocks_powerup_test_excluded_" + playerId, 0);
            foreach (GameController.PowerUpType type in new[] { GameController.PowerUpType.delete, GameController.PowerUpType.levelUp,
                GameController.PowerUpType.lookAhead, GameController.PowerUpType.extraTurns })
                result[type.ToString()] = Mathf.Max(0, inventory.Get(type) - testCopies);
            return result;
        }

        public static void MarkMigrated(string playerId)
        {
            PlayerPrefs.SetInt("cityblocks_powerups_migrated_" + playerId, 1);
            PlayerPrefs.Save();
        }

        public static int Uses(GameController.PowerUpType type) =>
            (int)Math.Min(int.MaxValue, CloudServices.PlayerWallet.PowerUp(type.ToString()));

        // Retained for old callers, but it must never deduct local inventory now.
        public static bool TryUse(GameController.PowerUpType type) => false;

        [Serializable] private class PendingUse { public string useId; public string type; public string context; }

        public static async System.Threading.Tasks.Task<bool> TryUseAsync(GameController.PowerUpType type, string contextId)
        {
            if (!CanUse) { Feedback = CloudServices.ConnectivityService.IsOnline ? "Inventory is syncing or a use is pending." : "Offline — connect to use power-ups."; return false; }
            cloudUseBusy = true;
            Feedback = "Confirming power-up...";
            string owner = CloudServices.AccountService.PlayerId;
            string key = "cityblocks_powerup_pending_" + owner;
            try
            {
                PendingUse pending = null;
                string saved = PlayerPrefs.GetString(key, "");
                if (!string.IsNullOrEmpty(saved)) pending = JsonUtility.FromJson<PendingUse>(saved);
                if (pending == null)
                {
                    if (Uses(type) <= 0) { Feedback = "No uses available."; return false; }
                    if (!await CloudServices.ConnectivityService.EnsureOnlineAsync("powerup")) { Feedback = "Offline — connect to use power-ups."; return false; }
                    DateTime? time = await CloudServices.CloudFunctions.GetServerTimeAsync();
                    if (!time.HasValue || CloudServices.AccountService.PlayerId != owner) { Feedback = "Couldn't confirm server time."; return false; }
                    pending = new PendingUse
                    {
                        useId = new DateTimeOffset(time.Value).ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + Guid.NewGuid().ToString("N"),
                        type = type.ToString(), context = contextId
                    };
                    PlayerPrefs.SetString(key, JsonUtility.ToJson(pending));
                    PlayerPrefs.Save();
                }
                var response = await CloudServices.CloudFunctions.UsePowerUpAsync(pending.type, pending.useId, owner);
                if (CloudServices.AccountService.PlayerId != owner) return false;
                if (response == null) { CloudServices.ConnectivityService.ReportCloudResult(false, "powerup"); Feedback = "Use saved for retry. Connect and try again."; return false; }
                CloudServices.ConnectivityService.ReportCloudResult(true, "powerup");
                if (response.Status == "applied" || response.Status == "insufficient")
                {
                    PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.Save(); // Persist completion before the caller activates an effect.
                    CloudServices.PlayerWallet.ApplyPowerUpBalance(owner, pending.type, response.BalanceAfter);
                    _ = CloudServices.PlayerWallet.RefreshAsync();
                    if (response.Status == "insufficient"){ Feedback = "No uses available."; return false; }
                    if (pending.context != contextId || pending.type != type.ToString())
                    {
                        Feedback = "Previous request resolved. Retry your current power-up.";
                        return false;
                    }
                    Feedback = "";
                    return true;
                }
                Feedback = response.Status == "pending" ? "Use pending — no extra charge. Resolve it before using another." : "Use needs recovery before retrying.";
                return false;
            }
            catch (Exception exception)
            {
                Feedback = "Couldn't confirm use. Request retained for retry.";
                Debug.LogWarning("[PowerUps] " + exception.Message);
                return false;
            }
            finally { cloudUseBusy = false; }
        }

        /// <summary>Called only after the platform store confirms a non-consumable/bundle purchase.</summary>
        public static void ConfirmBundlePurchase(string productId, PowerUpInventory uses, string source = "purchase")
        {
            if (string.IsNullOrWhiteSpace(productId)) return;
            Grant(uses, source);
        }

        /// <summary>Adds power-up uses to the inventory and reports powerup_granted for each type granted.</summary>
        /// <param name="source">purchase, daily, welcome, migration…</param>
        public static void Grant(PowerUpInventory uses, string source)
        {
            Debug.LogWarning("[PowerUps] Local grants are disabled. Rewards must be granted through Cloud Code.");
        }

        /// <summary>Called only after the platform store confirms the no-ads product.</summary>
        public static void ConfirmNoAdsPurchase()
        {
            LevelProgressData data = LoadProgress();
            data.adsRemoved = true;
            LevelProgressStore.Save(data);
        }

        public static bool AdsRemoved => LoadProgress().adsRemoved || PlayerPrefs.GetInt(NoAdsEntitlementKey, 0) == 1;

        public static bool FulfillPurchase(StoreOfferDefinition offer, string transactionId, double value, string currency)
        {
            if (offer == null || string.IsNullOrWhiteSpace(transactionId)) return false;
            if (!offer.noAdsOffer)
            {
                Debug.LogWarning("[PowerUps] Consumable purchases require server redemption (Step 37); leaving the transaction pending.");
                return false;
            }
            LevelProgressData progress= LevelProgressStore.Load();
            if (progress.processedPurchaseIds == null) progress.processedPurchaseIds = new List<string>();
            if (progress.processedPurchaseIds.Contains(transactionId)) return true;
            bool firstPurchase = !progress.hasMadePurchase;

            // Persist the receipt ID before granting so a redelivered pending order cannot double grant.
            progress.processedPurchaseIds.Add(transactionId);
            progress.hasMadePurchase = true;
            LevelProgressStore.Save(progress);
            if (offer.noAdsOffer)
            {
                ConfirmNoAdsPurchase();
                PlayerPrefs.SetInt(NoAdsEntitlementKey, 1);
                PlayerPrefs.Save();
            }
            else ConfirmBundlePurchase(offer.productId, offer.includedUses);

            if (offer.extraMovesOnPurchase > 0)
            {
                bool continuedCurrentSession = LevelFlow.ActiveSession != null &&
                                               LevelFlow.ActiveSession.ContinueAfterExtraMoves(offer.extraMovesOnPurchase);
                if (!continuedCurrentSession)
                {
                    progress = LevelProgressStore.Load();
                    // A purchase can be redelivered after relaunch before a LevelSession exists.
                    if (progress.activeRun != null)
                    {
                        progress.activeRun.movesRemaining += offer.extraMovesOnPurchase;
                        LevelProgressStore.Save(progress);
                    }
                }
            }

            LevelAnalytics.PurchaseCompleted(transactionId, value, currency, offer.productId,
                firstPurchase, progress.completedLevels != null ? progress.completedLevels.Count : 0);
            return true;
        }

        public static void RestoreNoAdsEntitlement()
        {
            PlayerPrefs.SetInt(NoAdsEntitlementKey, 1);
            PlayerPrefs.Save();
            ConfirmNoAdsPurchase();
        }

        public static void RequestPurchase(string productId)
        {
            if (!CanAccessStore) { Feedback = "Offline — connect to open the store."; return; }
            if (!string.IsNullOrWhiteSpace(productId)) PurchaseRequested?.Invoke(productId);
        }

        public static void RequestInterstitialAfterWin(int levelNumber)
        {
            if (!AdsRemoved) InterstitialRequested?.Invoke(levelNumber);
        }
    }
}
