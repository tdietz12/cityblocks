using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay
{
    /// <summary>Analytics event facade. Sends every event to UGS Analytics via CloudServices.AnalyticsSink.</summary>
    public static class LevelAnalytics
    {
        public static event Action<string, IDictionary<string, object>> EventRecorded;

        public static void Record(string eventName, params object[] keyValuePairs)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            for (int i = 0; i + 1 < keyValuePairs.Length; i += 2)
                parameters[(string)keyValuePairs[i]] = keyValuePairs[i + 1];
            AddCommonParameters(parameters);

            try { EventRecorded?.Invoke(eventName, parameters); }
            catch (Exception exception) { Debug.LogException(exception); }

            try { CloudServices.AnalyticsSink.Record(eventName, parameters); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        /// <summary>
        /// Parameters attached to every event. is_online is reachability-only until the heartbeat (Step 24);
        /// account_kind becomes gamecenter/apple/guest in Step 15. Call-site values with the same key win.
        /// </summary>
        private static void AddCommonParameters(Dictionary<string, object> parameters)
        {
            AddIfMissing(parameters, "player_level_max", LevelProgressStore.HighestUnlockedLevel);
            AddIfMissing(parameters, "session_number", CloudServices.AnalyticsSink.SessionNumber);
            AddIfMissing(parameters, "is_online", Application.internetReachability != NetworkReachability.NotReachable ? 1 : 0);
            AddIfMissing(parameters, "account_kind", CloudServices.CloudBootstrap.AccountKind);
        }

        private static void AddIfMissing(Dictionary<string, object> parameters, string key, object value)
        {
            if (!parameters.ContainsKey(key)) parameters[key] = value;
        }

        public static void LevelStarted(int level, int attempt) => Record("level_start", "level_number", level, "attempt_number", attempt);
        public static void LevelCompleted(int level, int attempt, int movesRemaining) => Record("level_complete",
            "level_number", level, "attempt_number", attempt, "moves_remaining", movesRemaining);
        public static void LevelFailed(int level, int attempt, string objective) => Record("level_fail",
            "level_number", level, "attempt_number", attempt, "failed_objective", objective ?? "unknown");
        public static void LevelQuit(int level, int attempt) => Record("level_quit", "level_number", level, "attempt_number", attempt);
        public static void EndlessStarted(int attempt) => Record("endless_level_start", "attempt_number", attempt);
        public static void EndlessFailed(int level, int attempt, int totalMoves) => Record("endless_level_fail",
            "level_reached", level, "attempt_number", attempt, "total_moves", totalMoves);
        public static void EndlessQuit(int level, int attempt, int totalMoves) => Record("endless_level_quit",
            "level_reached", level, "attempt_number", attempt, "total_moves", totalMoves);
        public static void BoosterUsed(string boosterId, int level, int attempt, bool endless) => Record("booster_used",
            "booster_id", boosterId, "level_number", level, "attempt_number", attempt, "is_endless_level", endless ? 1 : 0);
        public static void InsufficientFunds(string currency, double shortfall, string item, string levelName) => Record("insufficient_funds",
            "currency", currency ?? "", "shortfall", shortfall, "item_name", item ?? "", "level_name", levelName ?? "");
        public static void StoreOpened(string entryPoint, string levelName) => Record("store_open", "entry_point", entryPoint ?? "", "level_name", levelName ?? "");
        public static void ItemListViewed(string category) => Record("view_item_list", "item_category", category ?? "");
        public static void ItemViewed(string itemId, string category) => Record("view_item", "item_id", itemId ?? "", "item_category", category ?? "");
        public static void CheckoutStarted(string itemId, double value, string currency) => Record("begin_checkout",
            "item_id", itemId ?? "", "value", value, "currency", currency ?? "");
        public static void PurchaseCompleted(string transactionId, double value, string currency, string items,
            bool firstPurchase, int levelsCompleted) => Record("purchase", "transaction_id", transactionId ?? "",
            "value", value, "currency", currency ?? "", "items", items ?? "", "is_first_purchase", firstPurchase ? 1 : 0,
            "levels_completed", levelsCompleted);
        public static void PurchaseFailed(string itemId) => Record("purchase_failed", "item_id", itemId ?? "");
        public static void AdImpression(string platform, string format, int level, bool endless) => Record("ad_impression",
            "ad_platform", platform ?? "", "ad_format", format ?? "", "level_number", level, "is_endless_level", endless ? 1 : 0);
        public static void AdLoadFailed(string format, string code) => Record("ad_load_failed", "ad_format", format ?? "", "error_code", code ?? "");
        public static void RecordInsufficientFunds(string currency, double shortfall, string itemName, string levelName) =>
            InsufficientFunds(currency, shortfall, itemName, levelName);
    }
}
