using System;
using System.Reflection;
using CloudServices;
using Gameplay;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTests
{
    internal static class CloudPowerUpBoundaryTests
    {
        [MenuItem("Tools/City Blocks/Tests/Cloud Power Up Boundary Checks")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Run these checks in Edit Mode."); return; }
            const string cacheKey = "cityblocks_wallet_cache";
            bool hadCache = PlayerPrefs.HasKey(cacheKey);
            string oldCache = PlayerPrefs.GetString(cacheKey, "");
            var current = typeof(PlayerWallet).GetField("current", BindingFlags.NonPublic | BindingFlags.Static);
            var loaded = typeof(PlayerWallet).GetField("cacheLoaded", BindingFlags.NonPublic | BindingFlags.Static);
            object oldCurrent = current.GetValue(null), oldLoaded = loaded.GetValue(null);
            int purchaseRequests = 0;
            Action<string> onPurchase = id => purchaseRequests++;
            PowerUpStore.PurchaseRequested += onPurchase;
            try
            {
                PlayerPrefs.SetString(cacheKey, "{\"playerId\":\"boundary-test\",\"coins\":500,\"powerUps\":[{\"name\":\"delete\",\"count\":3},{\"name\":\"lookAhead\",\"count\":2}]}" );
                current.SetValue(null, null); loaded.SetValue(null, false);
                Check(PowerUpStore.Uses(GameController.PowerUpType.delete) == 3, "Uses must read the cloud cache.");
                Check(PowerUpStore.Uses(GameController.PowerUpType.lookAhead) == 2, "Look Ahead cloud mapping failed.");
                Check(!PowerUpStore.TryUse(GameController.PowerUpType.delete) && PowerUpStore.Uses(GameController.PowerUpType.delete) == 3, "Local debit must not occur.");
                Check(!PowerUpStore.CanUse && !PowerUpStore.CanAccessStore, "Signed-out player must not access power-ups or store.");
                PowerUpStore.RequestPurchase("test-product");
                Check(purchaseRequests == 0, "Blocked store must not initiate purchases.");
                SessionState.SetBool("CloudPowerUpBoundaryTests.Passed", true);
                Debug.Log("[CloudPowerUpBoundaryTests] 5 checks passed. No backend calls, purchases or real inventory changes made.");
            }
            finally
            {
                PowerUpStore.PurchaseRequested -= onPurchase;
                if (hadCache) PlayerPrefs.SetString(cacheKey, oldCache); else PlayerPrefs.DeleteKey(cacheKey);
                PlayerPrefs.Save();
                current.SetValue(null, oldCurrent); loaded.SetValue(null, oldLoaded);
            }
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
