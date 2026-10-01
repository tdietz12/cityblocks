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

        public static int Uses(GameController.PowerUpType type) => LoadProgress().powerUps.Get(type);

        public static bool TryUse(GameController.PowerUpType type)
        {
            LevelProgressData data = LoadProgress();
            if (!data.powerUps.TryUse(type)) return false;
            LevelProgressStore.Save(data);
            return true;
        }

        /// <summary>Called only after the platform store confirms a non-consumable/bundle purchase.</summary>
        public static void ConfirmBundlePurchase(string productId, PowerUpInventory uses)
        {
            if (string.IsNullOrWhiteSpace(productId) || uses == null) return;
            LevelProgressData data = LoadProgress();
            data.powerUps.Add(uses);
            LevelProgressStore.Save(data);
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
            LevelProgressData progress = LevelProgressStore.Load();
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
            if (!string.IsNullOrWhiteSpace(productId)) PurchaseRequested?.Invoke(productId);
        }

        public static void RequestInterstitialAfterWin(int levelNumber)
        {
            if (!AdsRemoved) InterstitialRequested?.Invoke(levelNumber);
        }
    }
}
