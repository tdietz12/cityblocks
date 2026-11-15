using System;
using System.Collections.Generic;
using System.Reflection;
using Gameplay;
using UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CityBlocks.EditorTests
{
    internal static class UIMotionConsistencyTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static int _checks;

        private static void Check(bool valid, string message)
        {
            if (!valid) throw new Exception(message);
            _checks++;
        }

        [MenuItem("Tools/City Blocks/Tests/UI Motion Consistency Checks")]
        private static void Run()
        {
            _checks = 0;
            GameObject instance = null;
            try
            {
                GameObject rulesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/LevelRulesUI.prefab");
                Check(rulesPrefab != null, "LevelRulesUI.prefab must exist");

                instance = UnityEngine.Object.Instantiate(rulesPrefab);
                var rulesUI = instance.GetComponent<LevelRulesUI>();
                Check(rulesUI != null, "LevelRulesUI component must exist on prefab");

                // 1. Verify all Button components have UIButtonAnimator with consistent settings
                var buttons = instance.GetComponentsInChildren<Button>(true);
                Check(buttons.Length >= 12, "Expected at least 12 buttons in LevelRulesUI, found " + buttons.Length);

                foreach (var btn in buttons)
                {
                    var anim = btn.GetComponent<UIButtonAnimator>();
                    Check(anim != null, "Button '" + btn.name + "' is missing UIButtonAnimator");
                    Check(Mathf.Abs(anim.PressScale - 0.92f) < 0.001f && Mathf.Abs(anim.ReleaseOvershoot - 1.08f) < 0.001f,
                        "Button '" + btn.name + "' does not use consistent press/release parameters");
                }

                // 2. Verify UIButtonAnimator press squash, release overshoot bounce, and NO idle pulse
                Transform startPlayBtnTransform = instance.transform.Find("StartPanel/PlayButton");
                Check(startPlayBtnTransform != null, "StartPanel/PlayButton must exist");
                var playBtn = startPlayBtnTransform.GetComponent<Button>();
                var playBtnAnim = startPlayBtnTransform.GetComponent<UIButtonAnimator>();
                Check(!playBtnAnim.EnableIdlePulse, "Selectable buttons must not have a looping idle pulse");

                // Snap parent panel to end so external multiplier is 1.0
                var startPanelAnim = instance.transform.Find("StartPanel").GetComponent<UIPanelAnimator>();
                Check(startPanelAnim != null, "StartPanel must have UIPanelAnimator");
                startPanelAnim.SnapToEnd();

                playBtnAnim.ResetState();
                playBtnAnim.Advance(0.25f);
                Check(Mathf.Abs(startPlayBtnTransform.localScale.x - 1f) < 0.001f,
                    "Idle button must remain steady at 1.0x without looping pulse, got: " + startPlayBtnTransform.localScale.x);

                // Pointer press squashes below 1.0
                playBtnAnim.TriggerPress();
                playBtnAnim.Advance(0.07f);
                Check(Mathf.Abs(startPlayBtnTransform.localScale.x - 0.92f) < 0.01f,
                    "Pressed button must squash to ~0.92x, got: " + startPlayBtnTransform.localScale.x);

                // Pointer release overshoots > 1.0 and settles back to 1.0
                playBtnAnim.TriggerRelease();
                playBtnAnim.Advance(0.08f);
                Check(startPlayBtnTransform.localScale.x > 1.02f,
                    "Released button must overshoot > 1.0x, got: " + startPlayBtnTransform.localScale.x);
                playBtnAnim.Advance(0.15f);
                Check(Mathf.Abs(startPlayBtnTransform.localScale.x - 1f) < 0.02f,
                    "Released button must settle back to 1.0x, got: " + startPlayBtnTransform.localScale.x);

                // Disabled button must not squash or pulse
                playBtn.interactable = false;
                playBtnAnim.TriggerPress();
                playBtnAnim.Advance(0.07f);
                Check(Mathf.Abs(startPlayBtnTransform.localScale.x - 1f) < 0.001f,
                    "Non-interactable button must remain at 1.0x scale");
                playBtn.interactable = true;

                // 3. Verify UIPanelAnimator open AND close animations on all modal & HUD panels
                string[] expectedAnimatedPanels =
                {
                    "StartPanel",
                    "LevelUpPanel",
                    "LevelWinPanel",
                    "LevelLossPanel",
                    "LosePanel",
                    "PlayPanel",
                    "PowerUpPanel_Delete",
                    "PowerUpPanel_LevelUp",
                    "PowerUpPanel_LookAhead"
                };

                foreach (string panelName in expectedAnimatedPanels)
                {
                    Transform panelTransform = instance.transform.Find(panelName);
                    Check(panelTransform != null, "Missing panel: " + panelName);
                    var panelAnim = panelTransform.GetComponent<UIPanelAnimator>();
                    Check(panelAnim != null && panelAnim.PanelCanvasGroup != null && panelAnim.CardTransform != null,
                        "Panel '" + panelName + "' must have UIPanelAnimator with CanvasGroup and CardTransform wired");

                    panelTransform.gameObject.SetActive(true);
                    panelAnim.PlayEntrance();
                    Check(panelAnim.IsAnimating && !panelAnim.IsClosing, "Panel '" + panelName + "' must start entrance animation on PlayEntrance()");
                    Check(panelAnim.CardTransform.localScale.x < 0.9f && panelAnim.PanelCanvasGroup.alpha < 0.2f,
                        "Panel '" + panelName + "' must start from reduced scale and low alpha");

                    // Advance to mid-overshoot
                    panelAnim.Advance(0.14f);
                    Check(panelAnim.CardTransform.localScale.x > 1.0f && panelAnim.PanelCanvasGroup.alpha > 0.7f,
                        "Panel '" + panelName + "' must overshoot scale and fade in at 0.14s");

                    if (panelName == "LevelLossPanel" || panelName == "LosePanel")
                    {
                        Check(panelAnim.ShakeOnEnable, "Defeat panel '" + panelName + "' must have ShakeOnEnable active");
                    }

                    // Advance past entrance completion
                    panelAnim.Advance(0.5f);
                    Check(!panelAnim.IsAnimating && panelAnim.PanelCanvasGroup.alpha == 1f &&
                          Mathf.Abs(panelAnim.CardTransform.localScale.x - 1f) < 0.03f,
                        "Panel '" + panelName + "' must settle cleanly at alpha=1 and scale=1");

                    // Verify Close / Exit animation
                    panelAnim.PlayExit();
                    Check(panelAnim.IsAnimating && panelAnim.IsClosing && !panelAnim.PanelCanvasGroup.blocksRaycasts,
                        "Panel '" + panelName + "' must begin closing animation and disable raycasts on PlayExit()");
                    panelAnim.Advance(panelAnim.ExitDuration * 0.5f);
                    Check(panelTransform.gameObject.activeSelf && panelAnim.CardTransform.localScale.x < 0.98f && panelAnim.PanelCanvasGroup.alpha < 0.9f,
                        "Panel '" + panelName + "' must shrink and fade out during exit animation");
                    panelAnim.Advance(panelAnim.ExitDuration);
                    Check(!panelTransform.gameObject.activeSelf && !panelAnim.IsAnimating,
                        "Panel '" + panelName + "' must deactivate automatically after exit animation finishes");
                }

                // 4. Verify ObjectiveCardUI row progress pulse
                var scoreObjCard = instance.GetComponentInChildren<ObjectiveCardUI>(true);
                Check(scoreObjCard != null, "ObjectiveCardUI must exist in LevelRulesUI");
                var dummyDef = ScriptableObject.CreateInstance<LevelDefinition>();
                dummyDef.objectives = new List<LevelObjective>
                {
                    new LevelObjective { type = LevelObjectiveType.ReachTowerLevel, target = 5 }
                };
                scoreObjCard.Bind(dummyDef, true, null, null);
                Transform row1 = scoreObjCard.transform.Find("Row1");
                Check(row1 != null && row1.localScale == Vector3.one, "Objective Row1 must start at scale 1.0");
                scoreObjCard.Refresh(3, 100);
                scoreObjCard.Advance(0.05f);
                Check(row1.localScale.x > 1.1f, "Objective row must pulse up when progress increases, got: " + row1.localScale.x);
                scoreObjCard.Advance(0.25f);
                Check(row1.localScale == Vector3.one, "Objective row pulse must return to 1.0x");
                UnityEngine.Object.DestroyImmediate(dummyDef);

                // 5. Verify StoreOffersWindow and DailyLoginRewards prefabs have UIButtonAnimator & UIPanelAnimator
                var storePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/StoreOffersWindow.prefab");
                var dailyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/DailyLoginRewards.prefab");
                var levelsBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/LevelsMenuButton.prefab");
                Check(storePrefab.GetComponentsInChildren<UIButtonAnimator>(true).Length >= 3 &&
                      storePrefab.GetComponentsInChildren<UIPanelAnimator>(true).Length >= 1,
                    "StoreOffersWindow.prefab must have UIButtonAnimator and UIPanelAnimator");
                Check(dailyPrefab.GetComponentsInChildren<UIButtonAnimator>(true).Length >= 3 &&
                      dailyPrefab.GetComponentsInChildren<UIPanelAnimator>(true).Length >= 1,
                    "DailyLoginRewards.prefab must have UIButtonAnimator and UIPanelAnimator");
                Check(levelsBtnPrefab.GetComponent<UIButtonAnimator>() != null && !levelsBtnPrefab.GetComponent<UIButtonAnimator>().EnableIdlePulse,
                    "LevelsMenuButton.prefab must have UIButtonAnimator without looping idle pulse");

                // 6. Verify BoardCameraFeedback default magnification is 0.03f (half of previous 0.06f)
                var tempCamGo = new GameObject("TempCam", typeof(Camera), typeof(BoardCameraFeedback));
                try
                {
                    var camFeedback = tempCamGo.GetComponent<BoardCameraFeedback>();
                    float mag = (float)typeof(BoardCameraFeedback).GetField("_magnification", Flags).GetValue(camFeedback);
                    Check(Mathf.Abs(mag - 0.03f) < 0.0001f, "BoardCameraFeedback _magnification must be 0.03f, got: " + mag);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tempCamGo);
                }

                Debug.Log("[UIMotionConsistencyTests] " + _checks + " checks passed: button squash/bounce without idle pulse, modal open & close animations, objective row pulses, and halved camera merge pulse.");
                SessionState.SetString("UIMotionConsistencyTest.Result", "passed " + _checks);
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
