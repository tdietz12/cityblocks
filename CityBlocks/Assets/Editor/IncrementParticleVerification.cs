using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using System.Collections;

namespace CityBlocks.ParticleVerification
{
    [InitializeOnLoad]
    internal static class IncrementParticleVerification
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static int frames, stage;
        private static Component feedback, game, celebration;
        private static GameObject fixtureRoot;
        private static object Get(object o, string n) => o.GetType().GetField(n, Instance).GetValue(o);
        private static object Call(object o, string n, params object[] a)
        {
            foreach (var method in o.GetType().GetMethods(Instance))
                if (method.Name == n && method.GetParameters().Length == a.Length) return method.Invoke(o, a);
            throw new MissingMethodException(n);
        }
        private static int Count => ((IList)Get(feedback, "_incrementRings")).Count;
        private static void Check(bool ok, string message)
        {
            Debug.Log("[Test] " + message + ": " + ok);
            if (!ok) throw new Exception(message);
        }
        static IncrementParticleVerification()
        {
            string state = SessionState.GetString("IncrementTest.State", "Idle");
            if (state == "WaitingForCompile") EditorApplication.delayCall += () =>
            {
                SessionState.SetString("IncrementTest.State", "EnteringPlayMode");
                EditorApplication.playModeStateChanged += Changed;
                EditorApplication.isPlaying = true;
            };
            else if (state == "EnteringPlayMode")
            {
                EditorApplication.playModeStateChanged += Changed;
                if (EditorApplication.isPlaying) Changed(PlayModeStateChange.EnteredPlayMode);
            }
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= Changed;
            SessionState.SetString("IncrementTest.State", "Running");
            EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            if (++frames < 5) return;
            try
            {
                if (stage == 0)
                {
                    // Existing deterministic fixture keeps gameplay disabled and all assertions reproducible.
                    var type = typeof(IncrementParticleVerification).Assembly.GetType("CityBlocks.EditorTests.ScoreFeedbackTests", true);
                    type.GetMethod("Fixture", Static).Invoke(null, null);
                    fixtureRoot = (GameObject)type.GetField("_root", Static).GetValue(null);
                    feedback = (Component)type.GetField("_feedback", Static).GetValue(null);
                    game = (Component)type.GetField("_game", Static).GetValue(null);
                    celebration = (Component)Get(feedback, "_comboCelebration");
                    Call(game, "BeginComboTurn", true);
                    Call(game, "AwardMergeScore", 2, Vector3.zero);
                    Check(Count == 0, "No score burst before arrival (count=" + Count + ")");
                    Call(feedback, "Advance", 0.5f);
                    Check(Count == 10, "Score arrival emits 10 rings (count=" + Count + ")");
                    Call(feedback, "Advance", 0.6f);
                    Check(Count == 0, "Rings expire and recycle");
                    Call(game, "AwardMergeScore", 3, Vector3.zero);
                    Check(Count == 10, "Multiplier increment emits 10 rings (count=" + Count + ")");
                    Call(feedback, "Advance", 0.08f);
                    foreach (var ring in (IList)Get(feedback, "_incrementRings"))
                    {
                        var image = (UnityEngine.UI.Image)Get(ring, "Image");
                        Check(image.color.r == 1f && image.color.g == 1f && image.color.b == 1f && !image.raycastTarget, "Increment ring white and nonblocking");
                        Check(image.rectTransform.sizeDelta.x <= 92f, "Increment ring is compact");
                    }
                    Call(feedback, "ClearAndSynchronize");
                    Check(Count == 0, "Reset clears all increment rings");
                    Call(celebration, "TriggerCelebration", 3);
                    Call(celebration, "Advance", 0.3f);
                    foreach (var ring in (IList)Get(celebration, "_rubble"))
                    {
                        var color = (Color)Get(ring, "BaseColor");
                        Check(color.r == 1f && color.g == 1f && color.b == 1f, "Celebration particles white");
                        Check(Mathf.Abs((float)Get(ring, "AngularVelocity")) <= 27f, "Celebration spin reduced");
                    }
                    Call(celebration, "ClearImmediate");
                    // Isolate fixture UI for a readable screenshot; changes exist only in Play Mode.
                    foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    {
                        if (root == fixtureRoot) continue;
                        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
                    }
                    ((Camera)type.GetField("_camera", Static).GetValue(null)).enabled = false;
                    Call(game, "BeginComboTurn", true);
                    Call(game, "AwardMergeScore", 2, Vector3.zero);
                    Call(feedback, "Advance", 0.5f);
                    Call(game, "AwardMergeScore", 3, Vector3.zero);
                    Call(feedback, "Advance", 0.1f);
                    Canvas.ForceUpdateCanvases();
                    stage = 1;
                    return;
                }
                if (stage == 1)
                {
                    ScreenCapture.CaptureScreenshot("Assets/IncrementParticleVerification.png");
                    stage = 2;
                    return;
                }
                if (stage == 2) { stage = 3; return; }
                Finish("PASSED: score arrival, multiplier increments, white colors, reduced spin, compact size, expiration and reset. Screenshot captured.");
            }
            catch (Exception e) { Finish("FAILED: " + e); }
        }
        private static void Finish(string message)
        {
            EditorApplication.update -= Tick;
            SessionState.SetString("IncrementTest.Result", message);
            SessionState.SetString("IncrementTest.State", "Done");
            EditorApplication.isPlaying = false;
        }
    }
}
