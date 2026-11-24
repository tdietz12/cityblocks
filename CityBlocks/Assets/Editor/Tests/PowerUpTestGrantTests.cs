using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTests
{
    internal static class PowerUpTestGrantTests
    {
        [MenuItem("Tools/City Blocks/Tests/Power Up Test Grant Checks")]
        private static void Run()
        {
            EditorTools.PowerUpTestGrantAdmin.RefuseOtherPending(new JObject(), "own");
            EditorTools.PowerUpTestGrantAdmin.RefuseOtherPending(JObject.Parse("{\"Receipts\":[{\"Status\":\"pending\",\"UseId\":\"own\"}]}"), "own");
            ExpectRefusal(() => EditorTools.PowerUpTestGrantAdmin.RefuseOtherPending(JObject.Parse("{\"Receipts\":[{\"Status\":\"pending\",\"UseId\":\"other\"}]}"), "own"));
            ExpectRefusal(() => EditorTools.PowerUpTestGrantAdmin.RefuseOtherPending(JObject.Parse("{\"LevelClaims\":[{\"Status\":\"pending\"}]}"), "own"));
            ExpectRefusal(() => EditorTools.PowerUpTestGrantAdmin.RefuseOtherPending(JObject.Parse("{\"DailyClaims\":[{\"Status\":\"pending\"}]}"), "own"));
            ExpectRefusal(() => EditorTools.PowerUpTestGrantAdmin.GiveTenAsync("", "").GetAwaiter().GetResult());
            var metadata = new Unity.Services.Apis.Economy.ModifiedMetadata(DateTime.UtcNow);
            var definitions = new[]
            {
                new Unity.Services.Apis.Economy.CurrencyResource(id: "PU_DELETE", name: "Delete", created: metadata, modified: metadata, initial: 0),
                new Unity.Services.Apis.Economy.CurrencyResource(id: "PU_LOOKAHEAD", name: "Look Ahead", created: metadata, modified: metadata, initial: 0),
                new Unity.Services.Apis.Economy.CurrencyResource(id: "PU_LEVELUP", name: "Level Up", created: metadata, modified: metadata, initial: 0),
                new Unity.Services.Apis.Economy.CurrencyResource(id: "PU_EXTRATURNS", name: "Extra Turns", created: metadata, modified: metadata, initial: 0)
            };
            var catalog = EditorTools.PowerUpTestGrantAdmin.ValidateCurrencyDefinitions(definitions);
            long missing = EditorTools.PowerUpTestGrantAdmin.BalanceOrInitial(Array.Empty<Unity.Services.Apis.Economy.CurrencyBalanceResponse>(), catalog["PU_LOOKAHEAD"]);
            if (missing != 0 || checked(missing + 10) != 10) throw new Exception("Missing Look Ahead balance must plan a grant to 10.");
            var existing = new[] { new Unity.Services.Apis.Economy.CurrencyBalanceResponse(currencyId: "PU_LOOKAHEAD", balance: 7, writeLock: "lock", created: metadata, modified: metadata) };
            if (EditorTools.PowerUpTestGrantAdmin.BalanceOrInitial(existing, catalog["PU_LOOKAHEAD"]) != 7) throw new Exception("Existing balance must be preserved.");
            definitions[1].Initial = 3;
            if (EditorTools.PowerUpTestGrantAdmin.BalanceOrInitial(Array.Empty<Unity.Services.Apis.Economy.CurrencyBalanceResponse>(), definitions[1]) != 3) throw new Exception("Configured initial balance must be respected.");
            ExpectRefusal(() => EditorTools.PowerUpTestGrantAdmin.ValidateCurrencyDefinitions(new[] { definitions[0], definitions[2], definitions[3] }));
            var journal = JObject.Parse("{\"Receipts\":[]}");
            var receipts = EditorTools.PowerUpTestGrantAdmin.GetOrCreateArray(journal, "Receipts");
            receipts.Add(new JObject { ["UseId"] = "test-operation", ["Status"] = "pending" });
            var roundTrip = JObject.Parse(journal.ToString());
            var recorded = EditorTools.PowerUpTestGrantAdmin.RequireReceipt(roundTrip, "test-operation");
            if ((string)recorded["Status"] != "pending") throw new Exception("Reservation was not included in serialized journal.");
            var wallet = JObject.Parse("{\"powerUps\":[{\"name\":\"delete\",\"count\":2}]}");
            var entries = EditorTools.PowerUpTestGrantAdmin.GetOrCreateArray(wallet, "powerUps");
            entries[0]["count"] = 12;
            entries.Add(new JObject { ["name"] = "lookAhead", ["count"] = 10 });
            var savedWallet = JObject.Parse(wallet.ToString());
            if ((int)savedWallet["powerUps"][0]["count"] != 12 || ((JArray)savedWallet["powerUps"]).Count != 2)
                throw new Exception("Wallet cache mutations were not persisted.");
            var fresh = new JObject();
            EditorTools.PowerUpTestGrantAdmin.GetOrCreateArray(fresh, "Receipts").Add(new JObject { ["UseId"] = "new" });
            if ((string)JObject.Parse(fresh.ToString())["Receipts"][0]["UseId"] != "new") throw new Exception("New array was not attached.");
            ExpectRefusal(() => EditorTools.PowerUpTestGrantAdmin.GetOrCreateArray(JObject.Parse("{\"Receipts\":42}"), "Receipts"));
            ExpectRefusal(() => EditorTools.PowerUpTestGrantAdmin.RequireReceipt(roundTrip, "missing"));
            SessionState.SetBool("PowerUpTestGrantTests.Passed", true);
            Debug.Log("[PowerUpTestGrantTests] 15 safety/regression checks passed. No real local/cloud grants made.");
        }
        private static void ExpectRefusal(Action action)
        {
            bool refused = false;
            try { action(); } catch (InvalidOperationException) { refused = true; }
            if (!refused) throw new Exception("Unsafe grant was not refused.");
        }
    }
}
