using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gameplay;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTools
{
    public static class PowerUpTestGrantAdmin
    {
        private const string JournalKey = "welcome_granted";
        private static readonly string[] Names = { "delete", "lookAhead", "levelUp", "extraTurns" };
        private static readonly string[] Currencies = { "PU_DELETE", "PU_LOOKAHEAD", "PU_LEVELUP", "PU_EXTRATURNS" };
        [Serializable] private class Intent
        {
            public string id;
            public long[] original;
            public long[] targets;
            public int[] localTargets;
            public int exclusionTarget;
        }

        public static string CurrentPlayerId()
        {
            string id = CloudServices.AccountService.PlayerId;
            if (!string.IsNullOrEmpty(id)) return id;
            return PlayerPrefs.GetString(FullPlayerTestReset.AuthKey(CloudProjectSettings.projectId, "player_id"), "");
        }

        public static async Task<string> GiveTenAsync(string keyId, string secret)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode and other test clients before changing both inventories.");
            string player = CurrentPlayerId(), project = CloudProjectSettings.projectId;
            if (string.IsNullOrEmpty(player) || string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException("Sign in once, then enter the service account key ID and secret.");
            if (Application.internetReachability == NetworkReachability.NotReachable) throw new InvalidOperationException("Connect to the internet first.");
            var admin = Unity.Services.Apis.ApiService.CreateAdminClient();
            admin.SetServiceAccount(keyId.Trim(), secret.Trim());
            var envs = await admin.Environment.GetEnvironments(project);
            Require(envs.IsSuccessful, envs.StatusCode, "Read environments");
            var env = envs.Data.Results.SingleOrDefault(e => e.Name == "development");
            if (env == null) throw new InvalidOperationException("Development environment not found. Production is never used.");
            var trusted = Unity.Services.Apis.ApiService.CreateTrustedClient();
            trusted.SetServiceAccount(keyId.Trim(), secret.Trim());
            var auth = await trusted.SignInWithServiceAccount(project, env.Id);
            Require(auth.IsSuccessful, auth.StatusCode, "Authorize Economy");
            var configuration = await trusted.EconomyConfiguration.GetPlayerConfiguration(project, player);
            Require(configuration.IsSuccessful, configuration.StatusCode, "Read published Economy configuration");
            var currencies = ValidateCurrencyDefinitions(configuration.Data.Results
                .Select(r => r.ActualInstance).OfType<Unity.Services.Apis.Economy.CurrencyResource>());
            string assignment = configuration.Data.Metadata?.ConfigAssignmentHash;
            var balances = await trusted.EconomyCurrencies.GetPlayerCurrencies(project, player, configAssignmentHash: assignment, limit: 100);
            Require(balances.IsSuccessful, balances.StatusCode, "Read balances");
            var read = await admin.CloudSaveData.GetProtectedItems(project, env.Id, player, new List<string> { JournalKey });
            Require(read.IsSuccessful, read.StatusCode, "Read journal");
            var item = read.Data.Results.FirstOrDefault(i => i.Key == JournalKey);
            if (item == null) throw new InvalidOperationException("Run Bootstrap once in Play Mode before granting test inventory.");
            string value = item.Value?.ToString() ?? "";
            var journal = value.TrimStart().StartsWith("{") ? JObject.Parse(value) : new JObject { ["WelcomeGrantedAt"] = value };
            string intentKey = "cityblocks_editor_powerup_grant_" + player;
            string saved = PlayerPrefs.GetString(intentKey, "");
            Intent intent = string.IsNullOrEmpty(saved) ? null : JsonUtility.FromJson<Intent>(saved);
            if (intent == null)
            {
                RefuseOtherPending(journal, "");
                var local = PowerUpStore.LoadProgress().powerUps;
                intent = new Intent { id = "editor_grant:" + Guid.NewGuid().ToString("N"), original = new long[4], targets = new long[4], localTargets = new int[4],
                    exclusionTarget = checked(PlayerPrefs.GetInt("cityblocks_powerup_test_excluded_" + player, 0) + 10) };
                for (int i = 0; i < 4; i++)
                {
                    long balance = BalanceOrInitial(balances.Data.Results, currencies[Currencies[i]]);
                    intent.original[i] = balance;
                    intent.targets[i] = checked(balance + 10);
                    intent.localTargets[i] = checked(local.Get((GameController.PowerUpType)Enum.Parse(typeof(GameController.PowerUpType), Names[i])) + 10);
                }
                PlayerPrefs.SetString(intentKey, JsonUtility.ToJson(intent));
                PlayerPrefs.Save();
            }
            RefuseOtherPending(journal, intent.id);
            var receipts = GetOrCreateArray(journal, "Receipts");
            var receipt = receipts.OfType<JObject>().FirstOrDefault(r => (string)r["UseId"] == intent.id);
            if (receipt == null)
            {
                receipt = new JObject { ["UseId"] = intent.id, ["PowerUp"] = "editor_grant", ["Status"] = "pending" };
                receipts.Add(receipt);
                if (string.IsNullOrEmpty(item.WriteLock)) throw new InvalidOperationException("Journal has no write lock.");
                var reserve = await admin.CloudSaveData.SetProtectedItem(project, env.Id, player,
                    new Unity.Services.Apis.Admin.CloudSave.SetItemBody(JournalKey, journal.ToString(Newtonsoft.Json.Formatting.None), item.WriteLock));
                Require(reserve.IsSuccessful, reserve.StatusCode, "Reserve test grant");
            }
            for (int i = 0; i < 4; i++)
            {
                var balance = balances.Data.Results.SingleOrDefault(b => b.CurrencyId == Currencies[i]);
                long current = BalanceOrInitial(balances.Data.Results, currencies[Currencies[i]]);
                if (current == intent.targets[i]) continue; // Previous attempt already set it.
                if (current != intent.original[i]) throw new InvalidOperationException("Balance changed outside this test operation; stop clients and inspect before retrying.");
                var change = await trusted.EconomyCurrencies.SetPlayerCurrencyBalance(project, player, Currencies[i],
                    new Unity.Services.Apis.Economy.CurrencyBalanceRequest(Currencies[i], intent.targets[i], balance?.WriteLock),
                    configAssignmentHash: assignment);
                Require(change.IsSuccessful, change.StatusCode, "Grant " + Currencies[i]);
            }
            // Local test copies must not be imported into Economy a second time by legacy migration.
            PlayerPrefs.SetInt("cityblocks_powerup_test_excluded_" + player, intent.exclusionTarget);
            PlayerPrefs.Save();
            for (int i = 0; i < 4; i++)
                global::EditorTools.PowerUpEditorMenu.SetSpecificPowerUp((GameController.PowerUpType)Enum.Parse(typeof(GameController.PowerUpType), Names[i]), intent.localTargets[i]);

            var latest = await admin.CloudSaveData.GetProtectedItems(project, env.Id, player, new List<string> { JournalKey });
            Require(latest.IsSuccessful, latest.StatusCode, "Read completion journal");
            var latestItem = latest.Data.Results.SingleOrDefault(i => i.Key == JournalKey);
            if (latestItem == null) throw new InvalidOperationException("Claim journal disappeared during the test grant. Saved targets are retained; stop other test clients and retry.");
            var latestJournal = JObject.Parse(latestItem.Value.ToString());
            RefuseOtherPending(latestJournal, intent.id);
            var latestReceipt = RequireReceipt(latestJournal, intent.id);
            if ((string)latestReceipt["Status"] != "applied")
            {
                latestReceipt["Status"] = "applied";
                var complete = await admin.CloudSaveData.SetProtectedItem(project, env.Id, player,
                    new Unity.Services.Apis.Admin.CloudSave.SetItemBody(JournalKey, latestJournal.ToString(Newtonsoft.Json.Formatting.None), latestItem.WriteLock));
                Require(complete.IsSuccessful, complete.StatusCode, "Complete test grant");
            }
            string cachedWallet = PlayerPrefs.GetString("cityblocks_wallet_cache", "");
            try
            {
                if (!string.IsNullOrEmpty(cachedWallet))
                {
                    var wallet = JObject.Parse(cachedWallet);
                    if ((string)wallet["playerId"] == player)
                    {
                        var entries = GetOrCreateArray(wallet, "powerUps");
                        for (int i = 0; i < 4; i++)
                        {
                            var entry = entries.OfType<JObject>().FirstOrDefault(e => (string)e["name"] == Names[i]);
                            if (entry == null) { entry = new JObject { ["name"] = Names[i] }; entries.Add(entry); }
                            entry["count"] = intent.targets[i];
                        }
                        PlayerPrefs.SetString("cityblocks_wallet_cache", wallet.ToString(Newtonsoft.Json.Formatting.None));
                    }
                }
            }
            catch (Newtonsoft.Json.JsonException) { Debug.LogWarning("[PowerUp Test Grant] Wallet cache could not be updated; it will refresh from cloud on launch."); }
            catch (InvalidOperationException) { Debug.LogWarning("[PowerUp Test Grant] Wallet cache has an invalid schema; it will refresh from cloud on launch."); }
            PlayerPrefs.DeleteKey(intentKey);
            PlayerPrefs.Save();
            return "+10 each added locallyand in development cloud. Cloud totals: " + string.Join(", ", Names.Select((n, i) => n + "=" + intent.targets[i]));
        }

        public static JArray GetOrCreateArray(JObject document, string property)
        {
            if (document[property] is JArray existing) return existing;
            if (document[property] != null && document[property].Type != JTokenType.Null)
                throw new InvalidOperationException("Expected an array for " + property + "; existing data was left untouched.");
            var created = new JArray();
            document[property] = created;
            return created;
        }

        public static JObject RequireReceipt(JObject journal, string useId)
        {
            var matches = (journal["Receipts"] as JArray)?.OfType<JObject>().Where(r => (string)r["UseId"] == useId).ToArray();
            if (matches == null || matches.Length != 1)
                throw new InvalidOperationException("Test grant receipt is missing or ambiguous. Saved targets are retained; do not add another grant or reset the pending operation.");
            return matches[0];
        }

        public static Dictionary<string, Unity.Services.Apis.Economy.CurrencyResource> ValidateCurrencyDefinitions(
            IEnumerable<Unity.Services.Apis.Economy.CurrencyResource> definitions)
        {
            var result = definitions.ToDictionary(c => c.Id);
            foreach (string id in Currencies)
                if (!result.ContainsKey(id))
                    throw new InvalidOperationException("Currency is not in published development configuration: " + id + ". Deploy/publish Economy configuration before granting.");
            return result;
        }

        public static long BalanceOrInitial(IEnumerable<Unity.Services.Apis.Economy.CurrencyBalanceResponse> balances,
            Unity.Services.Apis.Economy.CurrencyResource definition)
            => balances.SingleOrDefault(b => b.CurrencyId == definition.Id)?.Balance ?? definition.Initial;

        public static void RefuseOtherPending(JObject journal, string allowedId)
        {
            foreach (var key in new[] { "Receipts", "LevelClaims", "DailyClaims" })
                if (journal[key] is JArray entries && entries.OfType<JObject>().Any(r => (string)r["Status"] == "pending" && (string)r["UseId"] != allowedId))
                    throw new InvalidOperationException("Another cloud operation is pending. Resolve it before granting test inventory.");
        }
        private static void Require(bool success, int code, string stage)
        {
            if (!success) throw new InvalidOperationException(stage + " failed (HTTP " + code + "). Operation may be partial; retry the same +10 action after fixing permissions/connection. Saved targets prevent duplicate grants.");
        }
    }
}
