using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay
{
    /// <summary>Firebase Analytics event facade.</summary>
    public static class LevelAnalytics
    {
        public static event Action<string, IDictionary<string, object>> EventRecorded;

        public static void Record(string eventName, params object[] keyValuePairs)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            for (int i = 0; i + 1 < keyValuePairs.Length; i += 2)
                parameters[(string)keyValuePairs[i]] = keyValuePairs[i + 1];

            try { EventRecorded?.Invoke(eventName, parameters); }
            catch (Exception exception) { Debug.LogException(exception); }

            Firebase.Analytics.Parameter[] firebaseParameters = new Firebase.Analytics.Parameter[parameters.Count];
            int index = 0;
            foreach (KeyValuePair<string, object> parameter in parameters)
            {
                if (parameter.Value is string stringValue)
                    firebaseParameters[index++] = new Firebase.Analytics.Parameter(parameter.Key, stringValue);
                else if (parameter.Value is double doubleValue)
                    firebaseParameters[index++] = new Firebase.Analytics.Parameter(parameter.Key, doubleValue);
                else if (parameter.Value is float floatValue)
                    firebaseParameters[index++] = new Firebase.Analytics.Parameter(parameter.Key, (double)floatValue);
                else
                    firebaseParameters[index++] = new Firebase.Analytics.Parameter(parameter.Key, Convert.ToInt64(parameter.Value));
            }
            FirebaseBootstrap.LogEvent(eventName, firebaseParameters);
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
