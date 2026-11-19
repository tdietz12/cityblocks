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
            AddIfMissing(parameters, "session_number", CloudServices.SessionTracker.SessionNumber);
            AddIfMissing(parameters, "is_online", Application.internetReachability != NetworkReachability.NotReachable ? 1 : 0);
            AddIfMissing(parameters, "account_kind", CloudServices.CloudBootstrap.AccountKind);
        }

        private static void AddIfMissing(Dictionary<string, object> parameters, string key, object value)
        {
            if (!parameters.ContainsKey(key)) parameters[key] = value;
        }

        public static void SessionStarted(bool isFirstLaunch, int daysSinceInstall, int daysSinceLastSession) => Record("session_start",
            "is_first_launch", isFirstLaunch ? 1 : 0, "days_since_install", daysSinceInstall, "days_since_last_session", daysSinceLastSession);
        public static void SessionEnded(int durationSeconds, int levelsPlayed, string screen) => Record("session_end",
            "duration_s", durationSeconds, "levels_played", levelsPlayed, "screen", screen ?? "");
        public static void LevelStarted(int level, int attempt, string attemptId) => Record("level_start",
            "level_number", level, "attempt_number", attempt, "attempt_id", attemptId ?? "");
        public static void LevelResumed(int level, int attempt, string attemptId, int movesRemaining) => Record("level_resume",
            "level_number", level, "attempt_number", attempt, "attempt_id", attemptId ?? "", "moves_remaining", movesRemaining);
        public static void LevelCompleted(int level, int attempt, string attemptId, int movesRemaining, int score, int continuesUsed) => Record("level_complete",
            "level_number", level, "attempt_number", attempt, "attempt_id", attemptId ?? "", "moves_remaining", movesRemaining,
            "score", score, "continues_used", continuesUsed);
        public static void LevelFailed(int level, int attempt, string attemptId, string objective, int score, int continuesUsed) => Record("level_fail",
            "level_number", level, "attempt_number", attempt, "attempt_id", attemptId ?? "", "failed_objective", objective ?? "unknown",
            "score", score, "continues_used", continuesUsed);
        /// <summary>fromState: "playing" (restart mid-level), "fail" (Retry on the loss screen) or "win" (replay after winning).</summary>
        public static void LevelRestarted(int level, int attempt, string attemptId, string fromState) => Record("level_restart",
            "level_number", level, "attempt_number", attempt, "attempt_id", attemptId ?? "", "from_state", fromState ?? "playing");
        public static void LevelQuit(int level, int attempt, string attemptId, int movesRemaining) => Record("level_quit",
            "level_number", level, "attempt_number", attempt, "attempt_id", attemptId ?? "", "moves_remaining", movesRemaining);
        /// <summary>source: level_button, endless_button or auto_first_launch. level_number is 0 for Endless.</summary>
        public static void LevelSelected(int level, int highestUnlocked, string source) => Record("level_select",
            "level_number", level, "highest_unlocked", highestUnlocked, "source", source ?? "");
        public static void TutorialStepShown(int level, int stepIndex, string stepId) => Record("tutorial_step",
            "level_number", level, "step_index", stepIndex, "step_id", stepId ?? "");
        public static void TutorialCompleted(int level, int stepsTotal, int durationSeconds) => Record("tutorial_complete",
            "level_number", level, "steps_total", stepsTotal, "duration_s", durationSeconds);
        public static void EndlessNewRecord(int score, int levelReached, int previousRecordScore, int previousRecordLevel, int attempt) =>
            Record("endless_new_record", "score", score, "level_reached", levelReached,
                "previous_record_score", previousRecordScore, "previous_record_level", previousRecordLevel, "attempt_number", attempt);
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
