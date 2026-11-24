using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTools
{
    public static class FullPlayerTestReset
    {
        private const string JournalKey = "welcome_granted";
        private const string ResetReceiptId = "editor_full_test_reset";

        public static string AuthKey(string projectId, string name) => projectId + ".development.unity.services.authentication." + name;

        public static string PrepareResetJournal(string json)
        {
            var journal = string.IsNullOrEmpty(json) || !json.TrimStart().StartsWith("{")
                ? new JObject { ["WelcomeGrantedAt"] = json ?? "" } : JObject.Parse(json);
            foreach (string name in new[] { "Receipts", "LevelClaims", "DailyClaims" })
            {
                var entries = journal[name] as JArray;
                if (entries != null && entries.OfType<JObject>().Any(r => (string)r["Status"] == "pending" && (string)r["UseId"] != ResetReceiptId))
                    throw new InvalidOperationException("Resolve pending reward/use operations before a complete reset. Stop all test clients first.");
            }
            var receipts = journal["Receipts"] as JArray ?? new JArray();
            journal["Receipts"] = receipts;
            if (!receipts.OfType<JObject>().Any(r => (string)r["UseId"] == ResetReceiptId))
                receipts.Add(new JObject { ["UseId"] = ResetReceiptId, ["PowerUp"] = "test_reset", ["Status"] = "pending" });
            return journal.ToString(Newtonsoft.Json.Formatting.None);
        }

        public static async Task ResetAsync(string playerId, string keyId, string secret, Action<string> progress)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before resetting local and cloud data.");
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException("Enter a service account key ID and secret, and sign in once to identify the development player.");
            if (Application.internetReachability == NetworkReachability.NotReachable) throw new InvalidOperationException("Internet is required. No local data changed.");
            string projectId = CloudProjectSettings.projectId;
            if (string.IsNullOrEmpty(projectId)) throw new InvalidOperationException("Project must be linked to Unity Services.");
            playerId = playerId.Trim();
            var admin = Unity.Services.Apis.ApiService.CreateAdminClient();
            admin.SetServiceAccount(keyId.Trim(), secret.Trim());
            progress?.Invoke("Checking development environment and permissions...");
            var environments = await admin.Environment.GetEnvironments(projectId);
            Require(environments.IsSuccessful, environments.StatusCode, "Read environments");
            var development = environments.Data.Results.SingleOrDefault(e => e.Name == "development");
            if (development == null) throw new InvalidOperationException("Development environment missing. Production is never used.");
            var trusted = Unity.Services.Apis.ApiService.CreateTrustedClient();
            trusted.SetServiceAccount(keyId.Trim(), secret.Trim());
            var signIn = await trusted.SignInWithServiceAccount(projectId, development.Id);
            Require(signIn.IsSuccessful, signIn.StatusCode, "Authorize development Economy access");

            var balances = await trusted.EconomyCurrencies.GetPlayerCurrencies(projectId, playerId, limit: 100);
            Require(balances.IsSuccessful, balances.StatusCode, "Read Economy balances");
            if (balances.Data.Results.Count >= 100) throw new InvalidOperationException("Too many currencies for this test reset; no data changed.");
            var inventory = await trusted.EconomyInventory.GetPlayerInventory(projectId, playerId, limit: 100);
            Require(inventory.IsSuccessful, inventory.StatusCode, "Read inventory");
            if (inventory.Data.Results.Count >= 100) throw new InvalidOperationException("Too many inventory items for this test reset; no data changed.");
            var read = await admin.CloudSaveData.GetProtectedItems(projectId, development.Id, playerId, new List<string> { JournalKey });
            Require(read.IsSuccessful, read.StatusCode, "Read claim journal");
            var marker = read.Data.Results.FirstOrDefault(i => i.Key == JournalKey);
            string lockJson = PrepareResetJournal(marker?.Value?.ToString());
            var reservation = await admin.CloudSaveData.SetProtectedItem(projectId, development.Id, playerId,
                new Unity.Services.Apis.Admin.CloudSave.SetItemBody(JournalKey, lockJson, marker?.WriteLock));
            Require(reservation.IsSuccessful, reservation.StatusCode, "Reserve test reset");

            // Keep history/pending reset marker until all balance operations succeed. If interrupted, retry this tool;
            // repeated sets-to-zero and deletes are safe. Never claim that the whole reset is one transaction.
            progress?.Invoke("Resetting cloud coins and power-ups...");
            foreach (var balance in balances.Data.Results)
            {
                var reset = await trusted.EconomyCurrencies.SetPlayerCurrencyBalance(projectId, playerId, balance.CurrencyId,
                    new Unity.Services.Apis.Economy.CurrencyBalanceRequest(balance.CurrencyId, 0, balance.WriteLock));
                Require(reset.IsSuccessful, reset.StatusCode, "Reset currency " + balance.CurrencyId);
            }
            progress?.Invoke("Removing development inventory, including No Ads...");
            foreach (var item in inventory.Data.Results)
            {
                var remove = await trusted.EconomyInventory.DeleteInventoryItem(projectId, playerId, item.PlayersInventoryItemId, writeLock: item.WriteLock);
                Require(remove.IsSuccessful, remove.StatusCode, "Remove inventory item");
            }
            progress?.Invoke("Deleting cloud progress and reward history...");
            var defaultData = await admin.CloudSaveData.DeleteItems(projectId, development.Id, playerId);
            Require(defaultData.IsSuccessful, defaultData.StatusCode, "Delete default player data");
            var publicData = await admin.CloudSaveData.DeletePublicItems(projectId, development.Id, playerId);
            Require(publicData.IsSuccessful, publicData.StatusCode, "Delete public player data");
            var protectedData = await admin.CloudSaveData.DeleteProtectedItems(projectId, development.Id, playerId);
            Require(protectedData.IsSuccessful, protectedData.StatusCode, "Delete protected player data");

            progress?.Invoke("Cloud reset complete. Clearing local saves while preserving sign-in...");
            DeleteLocalData(projectId);
        }

        public static void DeleteLocalData(string projectId)
        {
            var auth = new Dictionary<string, string>();
            foreach (string name in new[] { "session_token", "player_id", "player_name" })
            {
                string key = AuthKey(projectId, name);
                if (PlayerPrefs.HasKey(key)) auth[key] = PlayerPrefs.GetString(key);
            }
            foreach (string name in new[] { "level-progress.json", "level-progress.json.tmp", "SaveGameData", "ProgressionData", "daily-login-streak.json" })
            {
                string file = Path.Combine(Application.persistentDataPath, name);
                if (File.Exists(file)) File.Delete(file);
            }
            PlayerPrefs.DeleteAll();
            foreach (var entry in auth) PlayerPrefs.SetString(entry.Key, entry.Value);
            PlayerPrefs.Save();
        }

        private static void Require(bool success, int code, string stage)
        {
            if (!success) throw new InvalidOperationException(stage + " failed (HTTP " + code + "). Reset may be partial; local data was not cleared. Check permissions and retry with all clients stopped.");
        }
    }
}
