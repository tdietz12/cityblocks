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
        /// <summary>method: session (cached token), gamecenter or anonymous (Step 16 adds apple / guest).</summary>
        public static void SignIn(string method, bool isNewPlayer) => Record("sign_in",
            "method", method ?? "", "is_new_player", isNewPlayer ? 1 : 0);
        /// <summary>provider: apple or gamecenter. source: settings, signin_panel or background (Game Center auto-link).</summary>
        public static void AccountLinked(string provider, string source) => Record("account_linked",
            "provider", provider ?? "", "source", source ?? "");
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
        public static void DailyRewardClaimed(int streakDay, int rewardAmount) => Record("daily_reward_claimed",
            "streak_day", streakDay, "reward_amount", rewardAmount);
        /// <summary>Sent when the automatic daily reward popup is dismissed. dismissMethod: "collect" or "close".</summary>
        public static void DailyRewardPopupShown(int streakDay, string dismissMethod, int durationSeconds) => Record("daily_reward_popup_shown",
            "streak_day", streakDay, "dismiss_method", dismissMethod ?? "", "duration_s", durationSeconds);
        /// <summary>The player came back after missing at least one day, resetting a streak of previousStreak days.</summary>
        public static void DailyStreakBroken(int previousStreak, int daysMissed) => Record("daily_streak_broken",
            "previous_streak", previousStreak, "days_missed", daysMissed);
        /// <summary>The app was cold-started by tapping a local notification.</summary>
        public static void NotificationOpened(string notificationId, int hoursSinceFire) => Record("notification_opened",
            "notification_id", notificationId ?? "", "hours_since_fire", hoursSinceFire);
        public static void EndlessStarted(int attempt) => Record("endless_level_start", "attempt_number", attempt);
        public static void EndlessFailed(int level, int attempt, int totalMoves) => Record("endless_level_fail",
            "level_reached", level, "attempt_number", attempt, "total_moves", totalMoves);
        public static void EndlessQuit(int level, int attempt, int totalMoves) => Record("endless_level_quit",
            "level_reached", level, "attempt_number", attempt, "total_moves", totalMoves);
        /// <summary>Analytics id for a power-up; shared by booster_used and powerup_granted.</summary>
        public static string BoosterId(GameController.PowerUpType type) => type == GameController.PowerUpType.lookAhead ? "look_ahead" : type.ToString();

        public static void BoosterUsed(string boosterId, int level, int attempt, bool endless, string attemptId, int balanceAfter) => Record("booster_used",
            "booster_id", boosterId ?? "", "level_number", level, "attempt_number", attempt, "is_endless_level", endless ? 1 : 0,
            "attempt_id", attemptId ?? "", "balance_after", balanceAfter);
public static void PowerUpGranted(GameController.PowerUpType type, int amount, string source, int balanceAfter) => Record("powerup_granted",
            "booster_id", BoosterId(type), "amount", amount, "source", source ?? "", "balance_after", balanceAfter);
        /// <summary>A continue (extra moves) was shown on the loss screen. continueIndex: 1 = first continue in this attempt.</summary>
        public static void ContinueOffered(int level, int attempt, string attemptId, int continueIndex, int movesOffered) => Record("continue_offered",
            "level_number", level, "attempt_number", attempt, "attempt_id", attemptId ?? "", "continue_index", continueIndex, "moves_offered", movesOffered);
        /// <summary>currency: "iap" today; "coins" from Step 43.</summary>
        public static void ContinuePurchased(int level, int attempt, string attemptId, int continueIndex, string currency, int movesGranted) => Record("continue_purchased",
            "level_number", level, "attempt_number", attempt, "attempt_id", attemptId ?? "", "continue_index", continueIndex,
            "currency", currency ?? "", "moves_granted", movesGranted);
        /// <summary>choice: "retry" or "menu".</summary>
        public static void ContinueDeclined(int level, int attempt, string attemptId, int continueIndex, string choice) => Record("continue_declined",
            "level_number", level, "attempt_number", attempt, "attempt_id", attemptId ?? "", "continue_index", continueIndex, "choice", choice ?? "");
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
        /// <summary>Name the built-in ad event is reported under (UGS standard event, not registered in Event Manager).</summary>
        public const string AdImpressionEventName = "adImpression";

        /// <summary>
        /// UGS built-in adImpression event. It has a fixed schema, so the common custom params are NOT attached.
        /// Send it once per shown ad, when the ad closes, so completion and click are known.
        /// provider: AdProvider name (e.g. "UnityAds"). placementType: "interstitial", "rewarded" or "banner".
        /// completion: "completed", "partial" (skipped) or "incomplete".
        /// </summary>
        public static void AdImpression(string provider, string placementId, string placementName, string placementType,
            string completion, bool clicked, long watchedMs)
        {
            Unity.Services.Analytics.AdProvider adProvider =
                Enum.TryParse(provider ?? "", true, out Unity.Services.Analytics.AdProvider parsedProvider) ? parsedProvider : Unity.Services.Analytics.AdProvider.Other;
            Unity.Services.Analytics.AdPlacementType adPlacementType =
                Enum.TryParse(placementType ?? "", true, out Unity.Services.Analytics.AdPlacementType parsedType) ? parsedType : Unity.Services.Analytics.AdPlacementType.OTHER;
            Unity.Services.Analytics.AdCompletionStatus adCompletion =
                Enum.TryParse(completion ?? "", true, out Unity.Services.Analytics.AdCompletionStatus parsedCompletion) ? parsedCompletion : Unity.Services.Analytics.AdCompletionStatus.Incomplete;

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "adProvider", adProvider.ToString() },
                { "placementId", placementId ?? "" },
                { "placementName", placementName ?? "" },
                { "placementType", adPlacementType.ToString() },
                { "adCompletionStatus", adCompletion.ToString() },
                { "adHasClicked", clicked },
                { "adTimeWatchedMs", watchedMs }
            };
            try { EventRecorded?.Invoke(AdImpressionEventName, parameters); }
            catch (Exception exception) { Debug.LogException(exception); }

            try
            {
                CloudServices.AnalyticsSink.RecordStandard(new Unity.Services.Analytics.AdImpressionEvent
                {
                    AdProvider = adProvider,
                    PlacementId = placementId ?? "",
                    PlacementName = placementName ?? "",
                    PlacementType = adPlacementType,
                    AdCompletionStatus = adCompletion,
                    AdHasClicked = clicked,
                    AdTimeWatchedMs = watchedMs
                });
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        public static void AdLoadFailed(string format, string code) => Record("ad_load_failed", "ad_format", format ?? "", "error_code", code ?? "");
        public static void RecordInsufficientFunds(string currency, double shortfall, string itemName, string levelName) =>
            InsufficientFunds(currency, shortfall, itemName, levelName);
    }
}
