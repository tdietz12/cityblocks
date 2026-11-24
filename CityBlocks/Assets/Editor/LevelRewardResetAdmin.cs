using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTools
{
    public static class LevelRewardResetAdmin
    {
        private const string JournalKey = "welcome_granted";

        // Preserve unknown journal fields too: a reset must not rebuild the document from a partial DTO.
        public static string ResetClaimJson(string json, int levelNumber, out bool changed)
        {
            if (levelNumber < 1) throw new ArgumentOutOfRangeException(nameof(levelNumber));
            JObject journal = JObject.Parse(json);
            foreach (string field in new[] { "Receipts", "LevelClaims" })
            {
                var entries = journal[field] as JArray;
                if (entries != null && entries.OfType<JObject>().Any(r => (string)r["Status"] == "pending"))
                    throw new InvalidOperationException("A pending Economy operation must be resolved before resetting reward eligibility.");
            }
            var claims = journal["LevelClaims"] as JArray;
            changed = false;
            if (claims != null)
            {
                foreach (var claim in claims.OfType<JObject>().Where(r => (int?)r["LevelNumber"] == levelNumber).ToArray())
                {
                    if ((string)claim["Status"] != "applied")
                        throw new InvalidOperationException("Only completed reward claims can be reset.");
                    claim.Remove();
                    changed = true;
                }
            }
            return journal.ToString(Newtonsoft.Json.Formatting.None);
        }

        public static async Task<string> ResetAsync(string playerId, int levelNumber, string keyId, string keySecret)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
                throw new InvalidOperationException("Enter the development player ID and service account key ID/secret.");
            if (Application.internetReachability == NetworkReachability.NotReachable)
                throw new InvalidOperationException("Connect to the internet before resetting cloud reward eligibility.");
            string projectId = CloudProjectSettings.projectId;
            if (string.IsNullOrEmpty(projectId)) throw new InvalidOperationException("Project must be linked to Unity Services.");

            var admin = Unity.Services.Apis.ApiService.CreateAdminClient();
            admin.SetServiceAccount(keyId.Trim(), keySecret.Trim());
            var environments = await admin.Environment.GetEnvironments(projectId);
            if (!environments.IsSuccessful) throw new InvalidOperationException("Cannot read environments (HTTP " + environments.StatusCode + "). Check service-account permissions.");
            var development = environments.Data.Results.SingleOrDefault(e => e.Name == "development");
            if (development == null) throw new InvalidOperationException("The development environment was not found; no data changed.");
            // No user-selectable environment, and never fall back to production.
            var read = await admin.CloudSaveData.GetProtectedItems(projectId, development.Id, playerId.Trim(), new List<string> { JournalKey });
            if (!read.IsSuccessful) throw new InvalidOperationException("Cannot read claim history (HTTP " + read.StatusCode + "). No data changed.");
            var item = read.Data.Results.FirstOrDefault(i => i.Key == JournalKey);
            if (item == null) return "No claim history yet; Level " + levelNumber + " is already eligible (development).";
            string value = item.Value?.ToString() ?? "";
            // Original Bootstrap marker predates the journal: no first-win receipts exist yet.
            if (!value.TrimStart().StartsWith("{")) return "Level " + levelNumber + " has no recorded reward claim (development).";
            string updated = ResetClaimJson(value, levelNumber, out bool changed);
            if (!changed) return "Level " + levelNumber + " is already eligible (development).";
            if (string.IsNullOrEmpty(item.WriteLock)) throw new InvalidOperationException("Claim history has no write lock; reset refused.");
            var body = new Unity.Services.Apis.Admin.CloudSave.SetItemBody(JournalKey, updated, item.WriteLock);
            var write = await admin.CloudSaveData.SetProtectedItem(projectId, development.Id, playerId.Trim(), body);
            if (!write.IsSuccessful) throw new InvalidOperationException("Reward reset failed (HTTP " + write.StatusCode + "). Claim history may have changed; reload and retry. No local progression reset performed.");
            return "Level " + levelNumber + " reward eligibility reset in development. Balances and other claims unchanged.";
        }
    }
}
