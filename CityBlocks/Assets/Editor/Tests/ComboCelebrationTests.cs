using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CityBlocks.EditorTests
{
    internal static class ComboCelebrationTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static object Get(object obj, string field) => obj.GetType().GetField(field, Flags).GetValue(obj);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
        private static object Property(object obj, string name) => obj.GetType().GetProperty(name, Flags).GetValue(obj);
        private static object Call(object obj, string name, params object[] args)
        {
            foreach (var method in obj.GetType().GetMethods(Flags))
                if (method.Name == name && method.GetParameters().Length == args.Length) return method.Invoke(obj, args);
            throw new MissingMethodException(obj.GetType().Name + "." + name);
        }

        private static int _checks;
        private static void Check(bool valid, string message)
        {
            if (!valid) throw new Exception(message);
            _checks++;
        }

        [MenuItem("Tools/City Blocks/Tests/Combo Celebration Checks")]
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
                var celebration = (Component)Property(feedback, "ComboCelebration");
                Check(celebration != null, "ComboCelebrationController must be wired on ScoreFeedbackController");

                var group = (CanvasGroup)Get(celebration, "_canvasGroup");
                var bannerRoot = (RectTransform)Get(celebration, "_bannerRoot");
                var headlineLabel = (TMP_Text)Get(celebration, "_headlineLabel");
                float startDelay = (float)Property(celebration, "StartDelay");
                float totalDuration = (float)Property(celebration, "TotalDuration");

                // Verify timing configuration: 0.2s start delay and 2.25s celebration duration (extended by 0.8s from 1.45s)
                Check(Mathf.Abs(startDelay - 0.2f) < 0.001f, "StartDelay must be 0.2s, got: " + startDelay);
                Check(Mathf.Abs(totalDuration - 2.25f) < 0.001f, "TotalDuration must be extended by 0.8s to 2.25s, got: " + totalDuration);

                // Verify spinning sunburst, combo length subtitle, and caution tape banners are removed
                Check(celebration.transform.Find("SunburstRays") == null, "Spinning SunburstRays must be removed");
                Check(celebration.transform.Find("CautionTapeContainer") == null &&
                      celebration.transform.Find("TopCautionTape") == null &&
                      celebration.transform.Find("BottomCautionTape") == null, "Caution tape banners must be removed");
                Check(bannerRoot.Find("SubtitleBadge") == null, "Combo length SubtitleBadge must be removed");
                Check(celebration.GetComponentsInChildren<TMP_Text>(true).Length == 1, "Only the congratulatory headline text should exist in ComboCelebrationOverlay");

                // 1. Verify initial idle state
                Check(!(bool)Property(celebration, "IsCelebrating"), "Celebration must start inactive");
                Check(group.alpha == 0f && !group.blocksRaycasts, "Idle celebration must be invisible and not block raycasts");

                // 2. Verify 1 or 2 merges on a tower placement do NOT trigger celebration
                Call(game, "BeginComboTurn", true);
                Call(game, "AwardMergeScore", 2, Vector3.zero);
                Call(game, "AwardMergeScore", 3, Vector3.zero);
                Check(!(bool)Property(celebration, "IsCelebrating"), "Celebration must not trigger while merges are still in progress");
                Call(game, "CompleteComboTurnAfterSettlement");
                Check(!(bool)Property(celebration, "IsCelebrating"), "2 merges on a tower placement must not trigger celebration");

                // 3. Verify 3 merges on a tower placement triggers celebration ONLY after settlement, starting in the 0.2s start delay
                Call(game, "BeginComboTurn", true);
                Call(game, "AwardMergeScore", 2, Vector3.zero);
                Call(game, "AwardMergeScore", 3, Vector3.zero);
                Call(game, "AwardMergeScore", 4, Vector3.zero);
                Check(!(bool)Property(celebration, "IsCelebrating"), "Celebration must wait until merging has settled");

                Call(game, "CompleteComboTurnAfterSettlement");
                Check((bool)Property(celebration, "IsCelebrating"), "3 merges on a tower placement must trigger celebration once settled");
                Check((bool)Property(celebration, "IsInStartDelay"), "Celebration must begin in the 0.2s start delay");
                Check((bool)Property(game, "HasPendingComboCelebration") && (bool)Property(game, "HasPendingScoreFeedback"), "GameController must report pending combo celebration during start delay");
                Check(group.blocksRaycasts && group.alpha == 0f && bannerRoot.localScale.x == 0f, "During 0.2s start delay, input is blocked while visuals wait to appear");
                Check((int)Property(celebration, "ActiveConfettiCount") == 0, "Confetti burst must wait for the 0.2s start delay to finish");

                // Advance halfway through the 0.2s delay (0.1s) - still delayed
                Call(feedback, "Advance", 0.1f);
                Check((bool)Property(celebration, "IsInStartDelay") && group.alpha == 0f, "Visuals must remain hidden at 0.1s (< 0.2s start delay)");

                // Advance past the 0.2s delay (+0.15s -> 0.05s into active celebration)
                Call(feedback, "Advance", 0.15f);
                Check(!(bool)Property(celebration, "IsInStartDelay") && (bool)Property(celebration, "IsCelebrating"), "Celebration must exit start delay after 0.2s");
                Check((int)Property(celebration, "LastCelebratedMergeCount") == 3, "LastCelebratedMergeCount must be 3");
                Check((string)Property(celebration, "CurrentHeadline") == "AWESOME!", "3-merge tier headline should start with AWESOME!");
                Check((int)Property(celebration, "ActiveConfettiCount") >= 16, "Confetti VFX burst must spawn active particles once start delay completes");

                // 4. Advance animation to mid-pop and verify banner animation
                Call(feedback, "Advance", 0.25f);
                Check((bool)Property(celebration, "IsCelebrating"), "Celebration should be active at 0.3s into celebration");
                Check(bannerRoot.localScale.x > 0.7f, "Headline banner must animate in");

                // Verify celebration is still active at 1.6s into active celebration (since duration was extended to 2.25s)
                Call(feedback, "Advance", 1.3f);
                Check((bool)Property(celebration, "IsCelebrating"), "Extended celebration must still be active at 1.6s (total duration 2.25s)");

                // 5. Advance past totalDuration and verify auto-dismissal
                Call(feedback, "Advance", 0.8f);
                Check(!(bool)Property(celebration, "IsCelebrating"), "Celebration must auto-dismiss after startDelay + totalDuration");
                Check(!group.blocksRaycasts && group.alpha == 0f, "Dismissed celebration must restore input and hide overlay");
                Check((int)Property(celebration, "ActiveConfettiCount") == 0, "Confetti particles must deactivate after celebration ends");

                // 6. Verify 4 merges on a tower placement uses Tier 4 congratulatory word
                Call(game, "BeginComboTurn", true);
                for (int i = 0; i < 4; i++) Call(game, "AwardMergeScore", 2 + i, Vector3.zero);
                Call(game, "CompleteComboTurnAfterSettlement");
                Call(feedback, "Advance", startDelay + 0.1f);
                Check((bool)Property(celebration, "IsCelebrating"), "4 merges must trigger celebration");
                string word4 = (string)Property(celebration, "CurrentHeadline");
                Check(word4 == "AMAZING!" || word4 == "SPECTACULAR!" || word4 == "FANTASTIC!", "4 merges must use Tier 4 congratulatory word, got: " + word4);
                Call(feedback, "Advance", totalDuration + 0.1f);
                Check(!(bool)Property(celebration, "IsCelebrating"), "4-merge celebration must auto-dismiss");

                // 7. Verify 5+ merges on a tower placement uses Tier 5+ congratulatory word
                Call(game, "BeginComboTurn", true);
                for (int i = 0; i < 5; i++) Call(game, "AwardMergeScore", 2 + i, Vector3.zero);
                Call(game, "CompleteComboTurnAfterSettlement");
                Call(feedback, "Advance", startDelay + 0.1f);
                Check((bool)Property(celebration, "IsCelebrating"), "5 merges must trigger celebration");
                string word5 = (string)Property(celebration, "CurrentHeadline");
                Check(word5 == "UNBELIEVABLE!" || word5 == "LEGENDARY!" || word5 == "INCREDIBLE!", "5+ merges must use Tier 5+ congratulatory word, got: " + word5);

                // 8. Verify every congratulatory word across all tiers spans nearly edge-to-edge AND fits strictly within the screen width at peak overshoot scale
                var overlayRect = (RectTransform)celebration.transform;
                foreach (float testWidth in new[] { 1080f, 828f, 750f })
                {
                    overlayRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, testWidth);
                    for (int tier = 3; tier <= 5; tier++)
                    {
                        for (int v = 0; v < 3; v++)
                        {
                            string word = (string)celebration.GetType().GetMethod("SelectCongratulatoryWord", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { tier, v });
                            headlineLabel.text = word;
                            Call(celebration, "FitHeadlineToScreen");
                            Call(celebration, "EvaluateFrame", 0.15f); // Near peak overshoot
                            headlineLabel.ForceMeshUpdate();
                            float effectiveTextWidth = headlineLabel.renderedWidth * bannerRoot.localScale.x;
                            Check(effectiveTextWidth <= testWidth && headlineLabel.textInfo.lineCount <= 1,
                                $"Word '{word}' overflowed or wrapped on screen width {testWidth}: effectiveWidth={effectiveTextWidth}");
                            Check(effectiveTextWidth >= testWidth * 0.78f,
                                $"Word '{word}' is too small on screen width {testWidth}: effectiveWidth={effectiveTextWidth}");
                        }
                    }
                }

                // 9. Verify ResetValuesOnLoss / ClearAndSynchronize clears active celebration immediately
                Call(feedback, "ClearAndSynchronize");
                Check(!(bool)Property(celebration, "IsCelebrating") && !group.blocksRaycasts, "ClearAndSynchronize must immediately clear active celebration");

                // 10. Verify power-up turns (isTowerPlacement: false) with 3+ merges do NOT trigger celebration
                Call(game, "BeginComboTurn", false);
                for (int i = 0; i < 4; i++) Call(game, "AwardMergeScore", 2 + i, Vector3.zero);
                Call(game, "CompleteComboTurnAfterSettlement");
                Check(!(bool)Property(celebration, "IsCelebrating"), "Power-up cascades (isTowerPlacement = false) must not trigger tower-placement celebration");

                // 11. Verify FallingTowerController vertical vs horizontal merge shrink/slide behavior
                var proxyGo = new GameObject("TempFallingTower", typeof(Gameplay.FallingTowerController));
                var otherGo = new GameObject("TempStationaryTarget");
                try
                {
                    var motion = proxyGo.GetComponent<Gameplay.FallingTowerController>();
                    // Vertical merge: sliding tower does NOT shrink (shrinkSelf=false), stationary lower tower DOES shrink (shrinkTarget=otherGo.transform)
                    proxyGo.transform.position = new Vector3(1f, 0f, 2f);
                    proxyGo.transform.localScale = Vector3.one;
                    otherGo.transform.localScale = Vector3.one;
                    Call(motion, "BeginSlide", new Vector3(1f, 0f, 3f), 0.1f, false, otherGo.transform, false);
                    Call(motion, "Step", 0.05f);
                    Check(proxyGo.transform.localScale == Vector3.one, "Vertical sliding tower must remain full scale as it slides down");
                    Check(otherGo.transform.localScale.x > 0f && otherGo.transform.localScale.x < 1f, "Stationary vertical target tower must shrink as the sliding tower merges down into it");
                    Check(proxyGo.transform.position.z > 2f && proxyGo.transform.position.z < 3f, "Vertical sliding tower must move down toward target cell");

                    // Horizontal merge: horizontal neighbour slides toward falling tower AND shrinks (shrinkSelf=true, shrinkTarget=null)
                    proxyGo.transform.position = new Vector3(0f, 0f, 3f);
                    proxyGo.transform.localScale = Vector3.one;
                    otherGo.transform.localScale = Vector3.one;
                    Call(motion, "BeginSlide", new Vector3(1f, 0f, 3f), 0.1f, true, null, true);
                    Call(motion, "Step", 0.05f);
                    Check(proxyGo.transform.localScale.x > 0f && proxyGo.transform.localScale.x < 1f, "Horizontal merging tower must shrink as it slides into the falling tower");
                    Check(otherGo.transform.localScale == Vector3.one, "Falling survivor tower must not shrink during horizontal merge");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(proxyGo);
                    UnityEngine.Object.DestroyImmediate(otherGo);
                }

                // 12. Verify TowerController _preMergeDelay is 0.1s before every merge
                var towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tower.prefab");
                var towerComponent = towerPrefab.GetComponent<Gameplay.TowerController>();
                Check(Mathf.Abs((float)Get(towerComponent, "_preMergeDelay") - 0.1f) < 0.0001f, "Tower.prefab _preMergeDelay must be 0.1s");

                Call(feedback, "ClearAndSynchronize");
                Debug.Log("[ComboCelebrationTest] " + _checks + " checks passed: 0.2s delay, 2.25s extended duration, caution tape aesthetic, screen-fit text, and tier checks.");
                SessionState.SetString("ComboCelebrationTest.Result", "passed " + _checks);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}

