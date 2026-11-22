using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Gameplay;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTests
{
    /// <summary>
    /// Calls every public LevelAnalytics event method and checks the event name, the exact parameter set
    /// (call-site params + the 4 common params), value types (Integer/Float/String in Event Manager terms),
    /// the values themselves, and that null string arguments never produce null parameters.
    /// Fails if a public method is added to LevelAnalytics without a case here.
    /// Test events never reach UGS: in Edit Mode services are not initialized, so AnalyticsSink only buffers them,
    /// and the buffer is cleared at the end of the run.
    /// </summary>
    internal static class LevelAnalyticsTests
    {
        private static readonly string[] CommonParams = { "player_level_max", "session_number", "is_online", "account_kind" };

        private sealed class Case
        {
            public string Method;
            public string EventName;
            public Action Invoke;
            public object[] Expected; // key, value pairs (call-site params only)
            public bool Standard;     // UGS built-in event: fixed schema, no common params, not registered in Event Manager
        }

        private static Case S(string method, string eventName, Action invoke, params object[] expected)
        {
            Case testCase = C(method, eventName, invoke, expected);
            testCase.Standard = true;
            return testCase;
        }

        private static int _checks;
        private static void Check(bool valid, string message) { if (!valid) throw new Exception(message); _checks++; }

        private static Case C(string method, string eventName, Action invoke, params object[] expected) =>
            new Case { Method = method, EventName = eventName, Invoke = invoke, Expected = expected };

        private static List<Case> Cases()
        {
            const GameController.PowerUpType look = GameController.PowerUpType.lookAhead;
            return new List<Case>
            {
                C("SessionStarted", "session_start", () => LevelAnalytics.SessionStarted(true, 3, 1),
                    "is_first_launch", 1, "days_since_install", 3, "days_since_last_session", 1),
                C("SessionEnded", "session_end", () => LevelAnalytics.SessionEnded(125, 4, "menu"),
                    "duration_s", 125, "levels_played", 4, "screen", "menu"),
                C("LevelStarted", "level_start", () => LevelAnalytics.LevelStarted(5, 2, "a1"),
                    "level_number", 5, "attempt_number", 2, "attempt_id", "a1"),
                C("LevelResumed", "level_resume", () => LevelAnalytics.LevelResumed(5, 2, "a1", 7),
                    "level_number", 5, "attempt_number", 2, "attempt_id", "a1", "moves_remaining", 7),
                C("LevelCompleted", "level_complete", () => LevelAnalytics.LevelCompleted(5, 2, "a1", 3, 900, 1),
                    "level_number", 5, "attempt_number", 2, "attempt_id", "a1", "moves_remaining", 3, "score", 900, "continues_used", 1),
                C("LevelFailed", "level_fail", () => LevelAnalytics.LevelFailed(5, 2, "a1", "score", 400, 0),
                    "level_number", 5, "attempt_number", 2, "attempt_id", "a1", "failed_objective", "score", "score", 400, "continues_used", 0),
                C("LevelRestarted", "level_restart", () => LevelAnalytics.LevelRestarted(5, 2, "a1", "fail"),
                    "level_number", 5, "attempt_number", 2, "attempt_id", "a1", "from_state", "fail"),
                C("LevelQuit", "level_quit", () => LevelAnalytics.LevelQuit(5, 2, "a1", 6),
                    "level_number", 5, "attempt_number", 2, "attempt_id", "a1", "moves_remaining", 6),
                C("LevelSelected", "level_select", () => LevelAnalytics.LevelSelected(0, 9, "endless_button"),
                    "level_number", 0, "highest_unlocked", 9, "source", "endless_button"),
                C("TutorialStepShown", "tutorial_step", () => LevelAnalytics.TutorialStepShown(1, 2, "Step_Merge"),
                    "level_number", 1, "step_index", 2, "step_id", "Step_Merge"),
                C("TutorialCompleted", "tutorial_complete", () => LevelAnalytics.TutorialCompleted(1, 4, 61),
                    "level_number", 1, "steps_total", 4, "duration_s", 61),
                C("EndlessNewRecord", "endless_new_record", () => LevelAnalytics.EndlessNewRecord(5000, 12, 4000, 10, 3),
                    "score", 5000, "level_reached", 12, "previous_record_score", 4000, "previous_record_level", 10, "attempt_number", 3),
                C("DailyRewardClaimed", "daily_reward_claimed", () => LevelAnalytics.DailyRewardClaimed(4, 2),
                    "streak_day", 4, "reward_amount", 2),
                C("DailyRewardPopupShown", "daily_reward_popup_shown", () => LevelAnalytics.DailyRewardPopupShown(4, "collect", 6),
                    "streak_day", 4, "dismiss_method", "collect", "duration_s", 6),
                C("DailyStreakBroken", "daily_streak_broken", () => LevelAnalytics.DailyStreakBroken(5, 2),
                    "previous_streak", 5, "days_missed", 2),
                C("NotificationOpened", "notification_opened", () => LevelAnalytics.NotificationOpened("daily_reminder", -1),
                    "notification_id", "daily_reminder", "hours_since_fire", -1),
                C("EndlessStarted", "endless_level_start", () => LevelAnalytics.EndlessStarted(3),
                    "attempt_number", 3),
                C("EndlessFailed", "endless_level_fail", () => LevelAnalytics.EndlessFailed(8, 3, 140),
                    "level_reached", 8, "attempt_number", 3, "total_moves", 140),
                C("EndlessQuit", "endless_level_quit", () => LevelAnalytics.EndlessQuit(8, 3, 140),
                    "level_reached", 8, "attempt_number", 3, "total_moves", 140),
                C("BoosterUsed", "booster_used", () => LevelAnalytics.BoosterUsed(LevelAnalytics.BoosterId(look), 5, 2, true, "a1", 0),
                    "booster_id", "look_ahead", "level_number", 5, "attempt_number", 2, "is_endless_level", 1, "attempt_id", "a1", "balance_after", 0),
                C("PowerUpGranted", "powerup_granted", () => LevelAnalytics.PowerUpGranted(GameController.PowerUpType.delete, 2, "daily", 3),
                    "booster_id", "delete", "amount", 2, "source", "daily", "balance_after", 3),
                C("ContinueOffered", "continue_offered", () => LevelAnalytics.ContinueOffered(5, 2, "a1", 1, 5),
                    "level_number", 5, "attempt_number", 2, "attempt_id", "a1", "continue_index", 1, "moves_offered", 5),
                C("ContinuePurchased", "continue_purchased", () => LevelAnalytics.ContinuePurchased(5, 2, "a1", 1, "iap", 5),
                    "level_number", 5, "attempt_number", 2, "attempt_id", "a1", "continue_index", 1, "currency", "iap", "moves_granted", 5),
                C("ContinueDeclined", "continue_declined", () => LevelAnalytics.ContinueDeclined(5, 2, "a1", 1, "retry"),
                    "level_number", 5, "attempt_number", 2, "attempt_id", "a1", "continue_index", 1, "choice", "retry"),
                C("InsufficientFunds", "insufficient_funds", () => LevelAnalytics.InsufficientFunds("coins", 25.0, "continue", "Level 5"),
                    "currency", "coins", "shortfall", 25.0, "item_name", "continue", "level_name", "Level 5"),
                C("RecordInsufficientFunds", "insufficient_funds", () => LevelAnalytics.RecordInsufficientFunds("coins", 25.0, "continue", "Level 5"),
                    "currency", "coins", "shortfall", 25.0, "item_name", "continue", "level_name", "Level 5"),
                C("StoreOpened", "store_open", () => LevelAnalytics.StoreOpened("daily_special", "Level 5"),
                    "entry_point", "daily_special", "level_name", "Level 5"),
                C("ItemListViewed", "view_item_list", () => LevelAnalytics.ItemListViewed("daily_special"),
                    "item_category", "daily_special"),
                C("ItemViewed", "view_item", () => LevelAnalytics.ItemViewed("no_ads", "no_ads"),
                    "item_id", "no_ads", "item_category", "no_ads"),
                C("CheckoutStarted", "begin_checkout", () => LevelAnalytics.CheckoutStarted("no_ads", 2.99, "USD"),
                    "item_id", "no_ads", "value", 2.99, "currency", "USD"),
                C("PurchaseCompleted", "purchase", () => LevelAnalytics.PurchaseCompleted("t1", 2.99, "USD", "no_ads", true, 12),
                    "transaction_id", "t1", "value", 2.99, "currency", "USD", "items", "no_ads", "is_first_purchase", 1, "levels_completed", 12),
                C("PurchaseFailed", "purchase_failed", () => LevelAnalytics.PurchaseFailed("no_ads"),
                    "item_id", "no_ads"),
                S("AdImpression", LevelAnalytics.AdImpressionEventName,
                    () => LevelAnalytics.AdImpression("UnityAds", "Interstitial_iOS", "return_to_menu", "interstitial", "partial", true, 4200L),
                    "adProvider", "UnityAds", "placementId", "Interstitial_iOS", "placementName", "return_to_menu",
                    "placementType", "INTERSTITIAL", "adCompletionStatus", "Partial", "adHasClicked", true, "adTimeWatchedMs", 4200L),
                C("AdLoadFailed", "ad_load_failed", () => LevelAnalytics.AdLoadFailed("interstitial", "load:NO_FILL:x"),
                    "ad_format", "interstitial", "error_code", "load:NO_FILL:x"),
            };
        }

        private static string TypeName(object value)
        {
            switch (value)
            {
                case int _: return "Integer";
                case double _: return "Float";
                case string _: return "String";
                default: return value == null ? "null" : value.GetType().Name;
            }
        }

        [MenuItem("Tools/City Blocks/Tests/Level Analytics Checks")]
        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[LevelAnalyticsTest] Run this in Edit Mode so test events can't reach UGS.");
                return;
            }

            _checks = 0;
            string lastName = null;
            IDictionary<string, object> lastParams = null;
            int recorded = 0;
            Action<string, IDictionary<string, object>> hook = (name, parameters) =>
            {
                lastName = name;
                lastParams = new Dictionary<string, object>(parameters);
                recorded++;
            };

            LevelAnalytics.EventRecorded += hook;
            try
            {
                List<Case> cases = Cases();

                // 1. Coverage: every public static void method (except Record) has a case.
                string[] methods = typeof(LevelAnalytics).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.ReturnType == typeof(void) && m.Name != "Record" && !m.IsSpecialName)
                    .Select(m => m.Name).Distinct().ToArray();
                foreach (string method in methods)
                    Check(cases.Any(c => c.Method == method), "No test case for LevelAnalytics." + method);
                Check(LevelAnalytics.BoosterId(GameController.PowerUpType.lookAhead) == "look_ahead" &&
                      LevelAnalytics.BoosterId(GameController.PowerUpType.levelUp) == "levelUp", "BoosterId mapping changed");

                // 2. Name, exact parameter set, values and types.
                var schemas = new SortedDictionary<string, SortedDictionary<string, string>>();
                foreach (Case testCase in cases)
                {
                    lastName = null; lastParams = null;
                    int before = recorded;
                    testCase.Invoke();
                    Check(recorded == before + 1, testCase.Method + " must record exactly one event");
                    Check(lastName == testCase.EventName, testCase.Method + " sent '" + lastName + "', expected '" + testCase.EventName + "'");

                    var expectedKeys = testCase.Standard ? new HashSet<string>() : new HashSet<string>(CommonParams);
                    for (int i = 0; i < testCase.Expected.Length; i += 2)
                    {
                        string key = (string)testCase.Expected[i];
                        object value = testCase.Expected[i + 1];
                        expectedKeys.Add(key);
                        Check(lastParams.ContainsKey(key), testCase.Method + " is missing '" + key + "'");
                        Check(Equals(lastParams[key], value), testCase.Method + "." + key + " = " + lastParams[key] + " (" + TypeName(lastParams[key]) + "), expected " + value + " (" + TypeName(value) + ")");
                    }
                    string extra = string.Join(", ", lastParams.Keys.Where(k => !expectedKeys.Contains(k)));
                    Check(extra.Length == 0, testCase.Method + " sent unexpected params: " + extra);
                    if (testCase.Standard)
                    {
                        Check(CommonParams.All(k => !lastParams.ContainsKey(k)), testCase.Method + ": built-in events must not carry custom common params");
                        continue;
                    }

                    Check(lastParams["player_level_max"]is int && lastParams["session_number"] is int &&
                          lastParams["is_online"] is int && lastParams["account_kind"] is string, testCase.Method + ": common param types changed");

                    if (!schemas.TryGetValue(lastName, out var schema)) schemas[lastName] = schema = new SortedDictionary<string, string>();
                    foreach (var pair in lastParams)
                    {
                        string type = TypeName(pair.Value);
                        Check(type == "Integer" || type == "Float" || type == "String", testCase.Method + "." + pair.Key + " has unsupported type " + type);
                        Check(!schema.TryGetValue(pair.Key, out string previous) || previous == type, lastName + "." + pair.Key + " type differs between methods");
                        schema[pair.Key] = type;
                    }
                }

                // 3. Every parameter name has one type across all events (Event Manager params are global).
                var paramTypes = new SortedDictionary<string, string>();
                foreach (var schema in schemas.Values)
                foreach (var pair in schema)
                {
                    Check(!paramTypes.TryGetValue(pair.Key, out string type) || type == pair.Value, "Param '" + pair.Key + "' is " + type + " in one event and " + pair.Value + " in another");
                    paramTypes[pair.Key] = pair.Value;
                }

                // 4. Null string arguments never produce null parameters (UGS would drop them → invalid event).
                foreach (MethodInfo method in typeof(LevelAnalytics).GetMethods(BindingFlags.Public | BindingFlags.Static)
                             .Where(m => m.ReturnType == typeof(void) && m.Name != "Record" && !m.IsSpecialName))
                {
                    object[] args = method.GetParameters().Select(p => p.ParameterType == typeof(string) ? null
                        : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
                    lastParams = null;
                    method.Invoke(null, args);
                    Check(lastParams != null && lastParams.Values.All(v => v != null), method.Name + " produced a null parameter from null arguments");
                }

                var report = new StringBuilder();
                report.AppendLine("[LevelAnalyticsTest] Event schemas (common params on every event: " + string.Join(", ", CommonParams) + ")");
                foreach (var schema in schemas)
                    report.AppendLine("  " + schema.Key + ": " + string.Join(", ", schema.Value.Keys.Where(k => !CommonParams.Contains(k))));
                report.AppendLine("Parameters: " + string.Join(", ", paramTypes.Select(p => p.Key + " (" + p.Value + ")")));
                Debug.Log(report.ToString());
                Debug.Log("[LevelAnalyticsTest] " + _checks + " checks passed: " + methods.Length + " methods, " + schemas.Count + " events, " + paramTypes.Count + " params.");
                SessionState.SetString("LevelAnalyticsTest.Result", "passed " + _checks);
            }
            catch (Exception exception)
            {
                Exception inner = exception is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : exception;
                SessionState.SetString("LevelAnalyticsTest.Result", "failed: " + inner.Message);
                Debug.LogError("[LevelAnalyticsTest] FAILED: " + inner.Message);
            }
            finally
            {
                LevelAnalytics.EventRecorded -= hook;
                ClearSinkBuffer();
            }
        }

        /// <summary>Drops the events this run buffered in AnalyticsSink so they can never be uploaded.</summary>
        private static void ClearSinkBuffer()
        {
            FieldInfo pending = typeof(CloudServices.AnalyticsSink).GetField("Pending", BindingFlags.NonPublic | BindingFlags.Static);
            if (pending?.GetValue(null) is System.Collections.IEnumerable queue)
                queue.GetType().GetMethod("Clear")?.Invoke(queue, null);
        }
    }
}
