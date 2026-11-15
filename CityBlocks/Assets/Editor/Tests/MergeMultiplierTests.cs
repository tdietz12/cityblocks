using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTests
{
    internal static class MergeMultiplierTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static object Get(object obj, string field) => obj.GetType().GetField(field, Flags).GetValue(obj);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
        private static object Property(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
        private static object Call(object obj, string name, params object[] args)
        {
            foreach (var method in obj.GetType().GetMethods(Flags))
                if (method.Name == name && method.GetParameters().Length == args.Length) return method.Invoke(obj, args);
            throw new MissingMethodException(name);
        }
        private static int _checks;
        private static void Check(bool valid, string message) { if (!valid) throw new Exception(message); _checks++; }

        [MenuItem("Tools/City Blocks/Tests/Merge Multiplier Checks")]
        private static void Run()
        {
            const BindingFlags statics = BindingFlags.Static | BindingFlags.NonPublic;
            var fixture = typeof(ScoreFeedbackTests);
            GameObject root = null;
            _checks = 0;
            try
            {
                fixture.GetMethod("Fixture", statics).Invoke(null, null);
                root = (GameObject)fixture.GetField("_root", statics).GetValue(null);
                var game = (Component)fixture.GetField("_game", statics).GetValue(null);
                var feedback = (Component)fixture.GetField("_feedback", statics).GetValue(null);
                var counter = (Component)Get(feedback, "_multiplierCounter");
                var group = counter.GetComponent<CanvasGroup>();
                var badgeLabel = counter.GetComponentInChildren<TMPro.TMP_Text>(true);
                var active = (IList)Get(feedback, "_active");
                var tokens = (IList)Get(feedback, "_tokens");
                float multiplierFlight = (float)Get(feedback, "_multiplierFlightDuration");
                float rise = (float)Get(feedback, "_multipliedRiseDuration");
                Check((float)Get(feedback, "_riseDuration") < rise, "Ordinary scores should have the shorter rise");
                Set(game, "score", 0);Set(game, "recordScore", 0); Call(feedback, "ClearAndSynchronize");
                Call(game, "BeginComboTurn");
                Call(game, "AwardMergeScore", 2, Vector3.zero);
                Check((int)Get(game, "score") == 20 && group.alpha == 0f && tokens.Count == 0, "First merge must be x1 with no badge/token");
                Call(game, "AwardMergeScore", 3, Vector3.zero);
                Check((int)Get(game, "score") == 80 && badgeLabel.text == "x2" && group.alpha == 1f && tokens.Count == 1, "Second merge must award 30x2 and show x2");
                var second = (Component)active[1];
                Check((int)Property(second, "Points") == 60 && second.GetComponent<TMPro.TMP_Text>().text == "+30", "Popup must reserve final points while displaying the base amount");
                Call(feedback, "Advance", multiplierFlight * 0.5f);
                Check(second.GetComponent<TMPro.TMP_Text>().text == "+30", "Multiplier applied before miniature token arrived");
                Call(feedback, "Advance", multiplierFlight * 0.5f);
                Check(second.GetComponent<TMPro.TMP_Text>().text == "+60" && tokens.Count == 1 && (int)Property(game, "DisplayedScore") == 0, "Token must apply on contact before score collection");
                var shrinkingToken = (Component)tokens[0];
                Check((bool)Property(shrinkingToken, "HasArrived"), "Token has not reached the score popup");
                Call(feedback, "Advance", 0.05f);
                Check(shrinkingToken.transform.localScale.x > 0f && shrinkingToken.transform.localScale.x < 1f && shrinkingToken.GetComponent<CanvasGroup>().alpha < 1f, "Arrived token did not shrink/fade");
                Call(feedback, "Advance", 0.06f);
                Check(tokens.Count == 0, "Token was not recycled after shrink completed");
                Call(game, "AwardMergeScore", 4, Vector3.zero);
                Check((int)Get(game, "score") == 200 && badgeLabel.text == "x3" && tokens.Count == 1, "Third merge must award 40x3 without retroactive changes");
                var third = (Component)active[active.Count - 1];
                Check(third.GetComponent<TMPro.TMP_Text>().text == "+40", "Third popup did not begin at base points");
                Call(game, "EndComboTurn");
                Check((int)Property(game, "CurrentMergeMultiplier") == 1 && tokens.Count == 1, "Turn reset must not cancel already-earned token flights");
                Call(game, "BeginComboTurn"); Call(game, "AwardMergeScore", 2, Vector3.zero);
                Check(group.alpha == 1f && (int)Get(game, "score") == 220 && (int)Property(game, "CurrentMergeMultiplier") == 1, "New turn carried over a scoring multiplier or prematurely removed the held badge");
                Call(feedback, "Advance", multiplierFlight);
                Check(third.GetComponent<TMPro.TMP_Text>().text == "+120" && badgeLabel.text == "x3", "Old token changed the held badge or wrong popup");
                Call(feedback, "Advance", 1f); Call(feedback, "Advance", 0.16f); Call(feedback, "Advance", 0.2f);
                Check((int)Property(game, "DisplayedScore") == 220 && !(bool)Property(game, "HasPendingScoreFeedback"), "Final collected score or pending ledger mismatch");
                Call(counter, "ShowMultiplier", 2); Call(counter, "EndTurn");
                Call(counter, "Advance", (float)Get(feedback, "_pulseDuration"));
                Check(group.alpha == 1f && !(bool)Property(game, "HasPendingScoreFeedback"), "Badge hold should be visible but cosmetic");
                Call(counter, "Advance", 1.9f); Check(group.alpha == 1f, "Badge disappeared before its two-second hold");
                Call(counter, "Advance", 0.11f);
                Check(group.alpha > 0f && group.alpha < 1f && counter.transform.localScale.x < 1f, "Badge should shrink/fade after the hold, not abruptly turn off");
                Call(counter, "Advance", 0.2f); Check(group.alpha == 0f, "Badge did not finish shrinking away");
                Set(game, "hasActiveRun", true);
                var assembly = Assembly.Load("Assembly-CSharp");
                var saved = new object[] { Activator.CreateInstance(assembly.GetType("Data_Persistence.GameSaveData")), Activator.CreateInstance(assembly.GetType("Data_Persistence.ProgressionData")) };
                game.GetType().GetMethod("Save").Invoke(game, saved);
                Check((int)Get(saved[0], "score") == 220, "Saved score omitted multiplied awards");
                Call(game, "EndComboTurn");
                Set(feedback, "_multiplierFlightDuration", rise + 0.3f);
                Call(game, "BeginComboTurn"); Call(game, "AwardMergeScore", 2, Vector3.zero); Call(game, "AwardMergeScore", 3, Vector3.zero);
                var held = (Component)active[1];
                Call(feedback, "Advance", rise + 0.1f);
                Check(held.GetComponent<TMPro.TMP_Text>().text == "+30" && active.Count == 1 && (int)Property(game, "DisplayedScore") == 240, "Only the multiplied score should wait for the slow token");
                Call(feedback, "Advance", 0.2f);
                Check(held.GetComponent<TMPro.TMP_Text>().text == "+60" && (int)Property(game, "DisplayedScore") == 240, "Slow token application or pending accounting failed");
                Call(feedback, "Advance", 0.05f);
                float popupBurstScale = (float)Get(held, "_multiplierBurstScale");
                float feedbackPulseScale = (float)Get(feedback, "_pulseScale");
                Check(popupBurstScale >= 2f && feedbackPulseScale >= 2f, "Multiplier, score, and flying number burst scales must be at least 2x");
                Check(held.transform.localScale.x > 1.5f && held.transform.localScale.x <= popupBurstScale + 0.001f, "Multiplied score did not burst to double size");
                Check((int)Property(game, "DisplayedScore") == 240, "Score collected during its multiplication burst");
                Call(feedback, "Advance", 0.11f);
                Call(feedback, "Advance", 0.14f);
                Check((int)Property(game, "DisplayedScore") == 240, "Held popup skipped its final flight interval");
                Call(feedback, "Advance", 0.02f); Call(feedback, "Advance", 0.2f);
                Check((int)Property(game, "DisplayedScore") == 300, "Held multiplier score did not collect correctly");
                Set(feedback, "_multiplierFlightDuration", multiplierFlight); Call(game, "EndComboTurn");

                var layer = (RectTransform)Get(feedback, "_popupLayer");
                var popupPrefab = (Component)Get(feedback, "_popupPrefab");
                var tokenPrefab = (Component)Get(feedback, "_multiplierTokenPrefab");
                var leasePopup = UnityEngine.Object.Instantiate(popupPrefab, layer);
                var leaseToken = UnityEngine.Object.Instantiate(tokenPrefab, layer);
                Call(leasePopup, "Begin", 60, Vector2.zero, 0.3f, 0.15f, 45f);
                Call(leasePopup, "PrepareMultiplier", 30);
                Call(leaseToken, "Begin", 2, Vector2.up * 40f, leasePopup, 0.15f);
                Call(leasePopup, "Cancel");
                Call(leasePopup, "Begin", 999, Vector2.zero, 0.3f, 0.15f, 45f);
                Check(!(bool)Property(leaseToken, "HasValidTarget"), "Stale token retained a pooled popup lease");
                Call(leaseToken, "Advance", 1f);
                Check(leasePopup.GetComponent<TMPro.TMP_Text>().text == "+999", "Stale token modified reused popup text");
                UnityEngine.Object.DestroyImmediate(leasePopup.gameObject); UnityEngine.Object.DestroyImmediate(leaseToken.gameObject);
                Check(tokenPrefab.GetComponent<TMPro.TMP_Text>().fontSize == popupPrefab.GetComponent<TMPro.TMP_Text>().fontSize, "Flying multiplier must match score text size");
                Check(!counter.GetComponent<UnityEngine.UI.Image>().enabled && badgeLabel.fontSharedMaterial.IsKeywordEnabled("UNDERLAY_ON"), "Multiplier must use drop shadow without a background");
                Check(!group.blocksRaycasts && !badgeLabel.raycastTarget && !tokenPrefab.GetComponent<TMPro.TMP_Text>().raycastTarget, "Multiplier UI blocks input");
                var counterRect = (RectTransform)counter.transform;
                var grid = (Component)Get(game, "grid");
                var camera = (Camera)fixture.GetField("_camera", statics).GetValue(null);
                var fitter = assembly.GetType("Gameplay.BoardCameraFitter").GetMethod("Fit");
                foreach (float aspect in new[] { 9f / 16f, 9f / 19.5f })
                for (int columns = 3; columns <= 7; columns++) for (int rows = 3; rows <= 7; rows++)
                {
                    Set(grid, "totalColumns", columns); Set(grid, "totalRows", rows + 1); camera.aspect = aspect;
                    fitter.Invoke(null, new object[] { camera, columns, rows, Vector3.zero });
                    Call(counter, "Reposition");
                    Rect bounds = layer.rect;
                    Vector2 half = new Vector2((float)Get(counter, "_textHalfWidth"), counterRect.rect.height * 0.5f) * (float)Get(feedback, "_pulseScale");
                    Vector2 position = counterRect.anchoredPosition;
                    Check(position.x - half.x >= bounds.xMin - 0.001f && position.x + half.x <= bounds.xMax + 0.001f && position.y - half.y >= bounds.yMin - 0.001f && position.y + half.y <= bounds.yMax + 0.001f, "Projected badge clips at pulse peak");
                }
                Call(game, "ResetValuesOnLoss");
                Check((int)Property(game, "CurrentMergeMultiplier") == 1 && group.alpha == 0f && !(bool)Property(game, "HasPendingScoreFeedback"), "Retry/reset left stale combo feedback");
                Call(game, "BeginComboTurn"); Call(game, "AwardMergeScore", int.MaxValue, Vector3.zero); Call(game, "AwardMergeScore", 3, Vector3.zero);
                Check((int)Get(game, "score") == int.MaxValue, "New multiplier scoring overflowed existing integer storage");
                Call(feedback, "ClearAndSynchronize");
                Debug.Log("[MultiplierTest] " + _checks + " checks passed: x1/x2/x3 scoring, token application, next-turn isolation, save totals, popup leases, and badge layout.");
                SessionState.SetString("MergeMultiplierTest.Result", "passed " + _checks);
            }
            finally { if (root != null) UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
