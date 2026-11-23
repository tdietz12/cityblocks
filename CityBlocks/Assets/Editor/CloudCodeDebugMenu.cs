using System;
using CloudServices;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTools
{
    /// <summary>Play Mode helpers for checking the deployed Cloud Code module.</summary>
    internal static class CloudCodeDebugMenu
    {
        [MenuItem("Tools/City Blocks/Cloud/Log Server Time")]
        private static async void LogServerTime()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[CloudCode] Enter Play Mode first (the call needs the game's signed-in player).");
                return;
            }
            DateTime? serverTime = await CloudFunctions.GetServerTimeAsync();
            if (serverTime == null)
            {
                Debug.LogWarning("[CloudCode] GetServerTime failed — is the module deployed (Services ▸ Deployment)?");
                SessionState.SetString("CloudCode.LastServerTime", "failed");
                return;
            }
            double drift = (DateTime.UtcNow - serverTime.Value).TotalSeconds;
            Debug.Log("[CloudCode] Server time (UTC): " + serverTime.Value.ToString("u") + " — device clock differs by " + drift.ToString("F1") + " s");
            SessionState.SetString("CloudCode.LastServerTime", serverTime.Value.ToString("o"));
        }

        [MenuItem("Tools/City Blocks/Cloud/Log Economy Balances")]
        private static async void LogBalances()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[Economy] Enter Play Mode first."); return; }
            if (!await CloudBootstrap.EnsureSignedInAsync()) { Debug.LogWarning("[Economy] Not signed in."); return; }
            try
            {
                var balances = await Unity.Services.Economy.EconomyService.Instance.PlayerBalances.GetBalancesAsync();
                var parts = new System.Collections.Generic.List<string>();
                foreach (var balance in balances.Balances) parts.Add(balance.CurrencyId + "=" + balance.Balance);
                var inventory = await Unity.Services.Economy.EconomyService.Instance.PlayerInventory.GetInventoryAsync();
                Debug.Log("[Economy] Balances: " + string.Join(", ", parts) + " | inventory items: " + inventory.PlayersInventoryItems.Count);
                SessionState.SetString("Economy.LastRead", "ok: " + string.Join(", ", parts));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Economy] Reading balances failed: " + exception.Message);
                SessionState.SetString("Economy.LastRead", "failed: " + exception.Message);
            }
        }

        /// <summary>Access Control check (Step 21): the client must NOT be able to change its own balance.</summary>
        [MenuItem("Tools/City Blocks/Cloud/Try Client Coin Increment (should be refused)")]
        private static async void TryClientCoinIncrement()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[Economy] Enter Play Mode first."); return; }
            if (!await CloudBootstrap.EnsureSignedInAsync()) { Debug.LogWarning("[Economy] Not signed in."); return; }
            try
            {
                await Unity.Services.Economy.EconomyService.Instance.PlayerBalances.IncrementBalanceAsync("COINS", 1);
                Debug.LogError("[Economy] ACCESS CONTROL NOT ACTIVE: the client increased its own COINS balance. Deploy CityBlocks.ac.");
                SessionState.SetString("Economy.LastWriteCheck", "allowed (bad)");
            }
            catch (Unity.Services.Economy.EconomyException exception)
            {
                bool refused = exception.ErrorCode == 403 || exception.Reason == Unity.Services.Economy.EconomyExceptionReason.Forbidden || exception.Reason == Unity.Services.Economy.EconomyExceptionReason.Unauthorized;
                if (refused) Debug.Log("[Economy] Correctly refused: the client can't change its own balance (" + exception.Reason + ").");
                else Debug.LogWarning("[Economy] Increment failed for another reason (" + exception.Reason + ", code " + exception.ErrorCode + "): " + exception.Message);
                SessionState.SetString("Economy.LastWriteCheck", (refused ? "refused: " : "other: ") + exception.Reason + " " + exception.ErrorCode);
            }
        }
    }
}
