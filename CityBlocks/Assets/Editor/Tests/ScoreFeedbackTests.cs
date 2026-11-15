using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CityBlocks.EditorTests
{
    [InitializeOnLoad]
    internal static class ScoreFeedbackTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static GameObject _root;
        private static Component _game, _grid, _queue, _ui, _feedback;
        private static Camera _camera;
        private static TMPro.TMP_Text _score;
        private static IEnumerator _routine;
        private static double _started;
        private static int _frame = -1;
        private static int _checks;
        private static readonly List<string> Logs = new List<string>();
        [Serializable] private class Scenes { public SceneSetup[] scenes; }
        [Serializable] private class Report { public bool success; public int checks; public string error; public string[] logs; }

        static ScoreFeedbackTests()
        {
            string state = SessionState.GetString("ScoreCollectionTest.State", "Idle");
            if (state == "WaitingForCompile") EditorApplication.delayCall += () =>
            {
                SessionState.SetString("ScoreCollectionTest.State", "EnteringPlayMode");
                EditorApplication.playModeStateChanged += PlayChanged;
                EditorApplication.isPlaying = true;
            };
            else if (state == "EnteringPlayMode") EditorApplication.playModeStateChanged += PlayChanged;
            else if (state == "Done") EditorApplication.delayCall += Restore;
        }

        private static Type T(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
        private static object Get(object obj, string field) => obj.GetType().GetField(field, Flags).GetValue(obj);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Flags).SetValue(obj, value);
        private static object Prop(object obj, string property) => obj.GetType().GetProperty(property).GetValue(obj);
        private static object Call(object obj, string name, params object[] args)
        {
            foreach (var method in obj.GetType().GetMethods(Flags))
                if (method.Name == name && method.GetParameters().Length == args.Length) return method.Invoke(obj, args);
            throw new MissingMethodException(name);
        }
        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception(message);
            _checks++;
        }
        private static void Log(string message) { Logs.Add(message); Debug.Log("[ScoreTest] " + message); }
        private static int Displayed => (int)Prop(_game, "DisplayedScore");
        private static bool Pending => (bool)Prop(_game, "HasPendingScoreFeedback");
        private static IList Active => (IList)Get(_feedback, "_active");
        private static void Advance(float time) => Call(_feedback, "Advance", time);
        private static void Award(int tier) => Call(_game, "AwardMergeScore", tier, Vector3.zero);

        private static void Fixture()
        {
            _root = new GameObject("TemporaryScoreCollectionFixture");
            _root.SetActive(false);
            _queue = _root.AddComponent(T("Gameplay.QueueController"));
            _grid = _root.AddComponent(T("Gameplay.GridController"));
            _game = _root.AddComponent(T("Gameplay.GameController"));
            ((Behaviour)_game).enabled = false;
            Set(_game, "queue", _queue); Set(_game, "grid", _grid); Set(_grid, "game", _game);
            Set(_game, "score", 780); Set(_game, "recordScore", 800);
            Set(_game, "tower", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tower.prefab"));
            Set(_grid, "cell", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cell/Cell.prefab"));
            var uiRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/LevelRulesUI.prefab"), _root.transform);
            _ui = uiRoot.GetComponent(T("UI.LevelRulesUI"));
            _feedback = uiRoot.GetComponentInChildren(T("UI.ScoreFeedbackController"), true);
            foreach (var behaviour in uiRoot.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != _feedback && behaviour.GetType().Namespace == "UI") behaviour.enabled = false;
            var cameraObject = new GameObject("ScoreTestCamera");
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.transform.SetPositionAndRotation(new Vector3(1.5f, 9.5f, -4.5f), Quaternion.Euler(60f, 0f, 0f));
            _camera.fieldOfView = 45f;
            Set(_game, "_gameplayCamera", _camera);
            Set(_game, "_scoreFeedback", _feedback);
            _score = (TMPro.TMP_Text)Get(_ui, "scoreText");
            Set(_game, "scoreText", _score);
            Set(_game, "recordScoreText", Get(_ui, "recordScoreText"));
            Set(_game, "state", Enum.ToObject(T("Gameplay.GameController+GameState"), 1));
            _root.SetActive(true);
            Call(_game, "BindScoreFeedback", _feedback, _camera);
            Call(_ui, "Bind", null, _game);
            Canvas.ForceUpdateCanvases();
        }

        [MenuItem("Tools/City Blocks/Tests/Score Collection Deterministic Checks")]
        private static void Deterministic()
        {
            _checks = 0; Logs.Clear();
            try
            {
                Fixture();
                float rise = (float)Get(_feedback, "_riseDuration");
                float flight = (float)Get(_feedback, "_flightDuration");
                Award(3);
                Check((int)Get(_game, "score") == 810 && Displayed == 780 && _score.text == "780", "Model/HUD delayed award mismatch");
                Check((int)Prop(_game, "DisplayedRecordScore") == 800, "Record revealed pending points");
                var popup = (Component)Active[0];
                var rect = (RectTransform)popup.transform;
                float startY = rect.anchoredPosition.y;
                Check(popup.GetComponent<TMPro.TMP_Text>().text == "+30", "Popup points text incorrect");
                Check(popup.GetComponent<TMPro.TMP_Text>().fontSize > 36f && popup.GetComponent<TMPro.TMP_Text>().fontSharedMaterial.IsKeywordEnabled("UNDERLAY_ON"), "Popup enlargement or drop shadow is missing");
                Check(!popup.GetComponent<TMPro.TMP_Text>().raycastTarget && !popup.GetComponent<CanvasGroup>().blocksRaycasts, "Popup intercepts input");
                Advance(rise * 0.5f);
                Check(Mathf.Abs(rect.anchoredPosition.y - startY - (float)Get(_feedback, "_riseDistance") * 0.5f) < 0.1f && Displayed == 780, "Popup did not rise slowly without collecting early");
                Advance(rise * 0.5f + flight - 0.01f);
                Check(Pending && Displayed == 780, "Popup collected before flight finished");
                Advance(0.02f);
                Check(Active.Count == 0 && Displayed == 810 && _score.text == "810" && !popup.gameObject.activeSelf, "Arrival did not update/hide exactly once");
                var pulse = (RectTransform)Get(_feedback, "_pulseTarget");
                Check(pulse.localScale.x > 1f && pulse.localScale.x <= (float)Get(_feedback, "_pulseScale"), "Arrival pulse absent or excessive");
                Advance(0.2f);
                Check(pulse.localScale == Vector3.one, "Pulse did not restore exact baseline");
                Log("780 +30: model immediate, HUD delayed through " + rise + "s rise / " + flight + "s flight, arrival pulse and record passed");

                Set(_game, "score", 100); Set(_game, "recordScore", 150); Call(_feedback, "ClearAndSynchronize");
                Award(2); Advance(0.2f); Award(3);
                Check(Displayed == 100 && (int)Get(_game, "score") == 150 && Active.Count == 2, "Concurrent awards accounting mismatch");
                Set(Active[1], "_age", rise + flight - 0.01f); Advance(0.02f);
                Check(Displayed == 130 && Active.Count == 1, "Out-of-order arrival lost pending award");
                Advance(2f);
                Check(Displayed == 150 && !Pending && (int)Get(_game, "score") == 150, "Concurrent final total mismatch");
                Award(2); Call(_feedback, "ClearAndSynchronize");
                Check(Displayed == 170 && !Pending, "Clear lost authoritative points");
                var target = Get(_feedback, "_scoreTarget"); Set(_feedback, "_scoreTarget", null); Award(3);
                Check(Displayed == 200 && !Pending, "Missing target stranded points"); Set(_feedback, "_scoreTarget", target);
                Log("Concurrent/out-of-order collection, cancel, and missing-target fallback passed");

                Set(_feedback, "_riseDuration", 0f); Set(_feedback, "_flightDuration", 0f); Award(2); Advance(0f);
                Check(Displayed == 220 && Active.Count == 0, "Zero-duration collection failed");
                Set(_feedback, "_riseDuration", rise); Set(_feedback, "_flightDuration", flight);
                Award(2); Set(_game, "state", Enum.ToObject(T("Gameplay.GameController+GameState"), 5));
                var win = (GameObject)Get(_ui, "winPanel"); Call(_ui, "ShowWin");
                Check(!win.activeSelf && Pending, "Win overlay covered unfinished flight");
                Advance(rise + flight - 0.01f); Advance(0.02f); Call(_ui, "Update");
                Check(!win.activeSelf && Pending && Active.Count == 0, "Win overlay covered the final score pulse");
                Advance(0.2f); Call(_ui, "Update");
                Check(win.activeSelf, "Win overlay did not appear after collection");
                Log("Zero durations and deferred terminal overlay passed; " + _checks + " deterministic assertions");
                SessionState.SetString("ScoreCollectionTest.Deterministic", "passed " + _checks);
            }
            finally { if (_root != null) UnityEngine.Object.DestroyImmediate(_root); }
        }

        [MenuItem("Tools/City Blocks/Tests/Single EventSystem Checks")]
        private static void EventSystemChecks()
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new Exception("Save scene edits before isolated EventSystem checks.");
            var originalScenes = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var host = new GameObject("TemporaryEventSystemGuard");
                Component guard = host.AddComponent(T("UI.SingleEventSystemGuard"));
                Call(guard, "EnsureSingleEventSystem");
                var systems = UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
                Check(systems.Length == 1 && systems[0].isActiveAndEnabled, "Missing EventSystem was not recovered");
                Check(systems[0].GetComponent<UnityEngine.EventSystems.StandaloneInputModule>().enabled, "Legacy UI input module is missing/disabled");
                var primary = systems[0];
                var duplicate = new GameObject("TemporaryDuplicateEventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
                Call(guard, "EnsureSingleEventSystem");
                Check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include).Length == 1 && duplicate == null, "Standalone duplicate input host was not removed");
                var content = new GameObject("TemporaryContentWithDuplicateInput", typeof(BoxCollider), typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
                var child = new GameObject("PreservedChild"); child.transform.SetParent(content.transform);
                Call(guard, "EnsureSingleEventSystem");
                Check(content != null && child != null && content.GetComponent<BoxCollider>() != null, "EventSystem cleanup destroyed unrelated content");
                Check(content.GetComponent<UnityEngine.EventSystems.EventSystem>() == null && content.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null, "Duplicate UI input components survived cleanup");
                UnityEngine.Object.DestroyImmediate(primary.gameObject);
                Call(guard, "EnsureSingleEventSystem");
                Check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include).Length == 1, "Removed primary EventSystem was not replaced");
                SessionState.SetString("ScoreCollectionTest.EventSystems", "passed");
                Debug.Log("[ScoreTest] Single EventSystem creation, duplicate removal, legacy module, content preservation, and recovery checks passed.");
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(originalScenes); }
        }

        [MenuItem("Tools/City Blocks/Tests/Instant Merge Camera Checks")]
        private static void CameraImpact()
        {
            var holder = new GameObject("TemporaryMergeCameraCheck");
            holder.SetActive(false);
            Component game = holder.AddComponent(T("Gameplay.GameController"));
            ((Behaviour)game).enabled = false;
            Component grid = holder.AddComponent(T("Gameplay.GridController"));
            Set(game, "grid", grid);
            Set(game, "state", Enum.ToObject(T("Gameplay.GameController+GameState"), 1));
            var camera = holder.AddComponent<Camera>();
            camera.fieldOfView = 45f;
            var overlayObject = new GameObject("TemporaryOverlay");
            overlayObject.transform.SetParent(holder.transform, false);
            var overlay = overlayObject.AddComponent<Camera>(); overlay.fieldOfView = 45f;
            Component feedback = holder.AddComponent(T("Gameplay.BoardCameraFeedback"));
            Set(feedback, "_camera", camera); Set(feedback, "_overlayCamera", overlay); Set(feedback, "_game", game);
            holder.SetActive(true);
            try
            {
                Call(feedback, "CaptureDefaultState"); Call(feedback, "OnEnable");
                float peak = 2f * Mathf.Atan(Mathf.Tan(45f * Mathf.Deg2Rad * 0.5f) / (1f + (float)Get(feedback, "_magnification"))) * Mathf.Rad2Deg;
                Call(grid, "NotifyMergeStarted");
                Check(Mathf.Abs(camera.fieldOfView - peak) < 0.0001f && camera.fieldOfView == overlay.fieldOfView, "Merge onset did not immediately snap both cameras to peak");
                Call(feedback, "LateUpdate");
                Check(Mathf.Abs(camera.fieldOfView - peak) < 0.0001f, "Peak zoom was reduced before the initial rendered frame");
                Set(feedback, "_pulseStartFrame", -1); Set(feedback, "_phaseTime", 0.04f); Call(feedback, "LateUpdate");
                Check(camera.fieldOfView > peak && camera.fieldOfView <= 45f, "Fast recovery did not move toward baseline");
                Call(grid, "NotifyMergeStarted");
                Check(Mathf.Abs(camera.fieldOfView - peak) < 0.0001f && (float)Get(feedback, "_phaseTime") == 0f, "Consecutive merge did not cancel/restart current pulse immediately");
                Set(feedback, "_pulseStartFrame", -1); Set(feedback, "_phaseTime", (float)Get(feedback, "_returnDuration"));
                Call(feedback, "LateUpdate");
                Check(Mathf.Abs(camera.fieldOfView - 45f) < 0.0001f && overlay.fieldOfView == camera.fieldOfView, "Recovery did not restore exact lens baseline");
                Call(grid, "NotifyMergeCompleted");
                Check(Mathf.Abs(camera.fieldOfView - 45f) < 0.0001f, "Completion emitted an unwanted second camera pulse");
                bool pitchMoved = false, rollMoved = false;
                float rotationLimit = (float)Get(feedback, "_rotationDegrees");
                Call(feedback, "ResetFeedback");
                camera.aspect = 1080f / 1920f;
                Vector3 probe = new Vector3(2f, 1f, 10f);
                Vector3 baselineViewport = camera.WorldToViewportPoint(probe);
                float maxIdlePixels = 0f;
                for (int i = 0; i < 12; i++)
                {
                    Set(feedback, "_driftTime", i * 2f); Set(feedback, "_driftWeight", 1f);
                    Call(feedback, "LateUpdate");
                    Vector3 angles = holder.transform.localEulerAngles;
                    float pitch = Mathf.DeltaAngle(0f, angles.x);
                    float yaw = Mathf.DeltaAngle(0f, angles.y);
                    float roll = Mathf.DeltaAngle(0f, angles.z);
                    Check(Mathf.Abs(pitch) <= rotationLimit + 0.001f && Mathf.Abs(roll) <= rotationLimit + 0.001f, "Idle rotation exceeded subtle bounds");
                    Check(Mathf.Abs(yaw) < 0.001f, "Idle camera introduced unwanted yaw");
                    Check(holder.transform.localPosition.magnitude <= (float)Get(feedback, "_translation") * 1.415f, "Idle translation accumulated beyond bounds");
                    pitchMoved |= Mathf.Abs(pitch) > 0.0001f;
                    rollMoved |= Mathf.Abs(roll) > 0.0001f;
                    Vector3 viewport = camera.WorldToViewportPoint(probe);
                    maxIdlePixels = Mathf.Max(maxIdlePixels, new Vector2((viewport.x - baselineViewport.x) * 1080f, (viewport.y - baselineViewport.y) * 1920f).magnitude);
                }
                Check(pitchMoved && rollMoved, "Both X and Z idle rotations must move");
                Check(maxIdlePixels >= 1f, "Idle movement remains sub-pixel");
                Call(feedback, "ResetFeedback");
                Check(holder.transform.localPosition == Vector3.zero && holder.transform.localRotation == Quaternion.identity, "Idle feedback failed to restore baseline pose");
                Debug.Log("[ScoreTest] Stabilized X/Z idle movement reaches " + maxIdlePixels.ToString("F2") + " reference-screen pixels; bounds, baseline reset, and merge impacts passed.");
                SessionState.SetString("ScoreCollectionTest.CameraImpact", "passed");
            }
            finally { Call(feedback, "OnDisable"); UnityEngine.Object.DestroyImmediate(holder); }
        }

        [MenuItem("Tools/City Blocks/Tests/Score Collection Runtime Checks")]
        private static void Launch()
        {
            if (EditorSettings.enterPlayModeOptionsEnabled && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0)
                throw new Exception("Score collection runtime checks require Domain Reload.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new Exception("Save edited scenes before testing.");
            SessionState.SetString("ScoreCollectionTest.Scenes", JsonUtility.ToJson(new Scenes { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.Refresh();
            SessionState.EraseString("ScoreCollectionTest.Result");
            SessionState.SetString("ScoreCollectionTest.State", "EnteringPlayMode");
            EditorApplication.playModeStateChanged -= PlayChanged;
            EditorApplication.playModeStateChanged += PlayChanged;
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetString("ScoreCollectionTest.State", "Idle") == "EnteringPlayMode")
                    EditorApplication.isPlaying = true;
            };
        }

        private static void PlayChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= PlayChanged;
            SessionState.SetString("ScoreCollectionTest.State", "InPlayMode");
            _started = EditorApplication.timeSinceStartup; _frame = -1; _checks = 0; Logs.Clear();
            _routine = RuntimeCases(); EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if (EditorApplication.timeSinceStartup - _started > 20d) throw new Exception("Score collection test deadline exceeded");
                if (_frame == Time.frameCount) return;
                _frame = Time.frameCount;
                if (!_routine.MoveNext()) Finish(null);
            }
            catch (Exception e) { Finish(e.ToString()); }
        }
        private static void Finish(string error)
        {
            EditorApplication.update -= Tick;
            string report = JsonUtility.ToJson(new Report { success = error == null, checks = _checks, error = error, logs = Logs.ToArray() });
            SessionState.SetString("ScoreCollectionTest.Result", report);
            SessionState.SetString("ScoreCollectionTest.State", "Done");
            Debug.Log("SCORE_COLLECTION_TEST_COMPLETE " + report);
            EditorApplication.playModeStateChanged += RestoreChanged;
            EditorApplication.isPlaying = false;
        }
        private static void RestoreChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= RestoreChanged;
            EditorApplication.delayCall += Restore;
        }
        [MenuItem("Tools/City Blocks/Tests/Restore Score Collection Test Scenes")]
        private static void Restore()
        {
            string saved = SessionState.GetString("ScoreCollectionTest.Scenes", "");
            if (saved.Length == 0 || EditorApplication.isPlaying) return;
            EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Scenes>(saved).scenes);
            SessionState.EraseString("ScoreCollectionTest.Scenes");
        }

        private static IEnumerator RuntimeCases()
        {
            yield return null; Fixture(); yield return null;
            float rise = (float)Get(_feedback, "_riseDuration");
            float flight = (float)Get(_feedback, "_flightDuration");
            float began = Time.unscaledTime; Award(3);
            Check((int)Get(_game, "score") == 810 && Displayed == 780, "Runtime model/HUD mismatch at award");
            while (Time.unscaledTime - began < rise * 0.35f) yield return null;
            Check(Pending && _score.text == "780" && Active.Count == 1, "Rise phase did not retain popup/pending HUD");
            ScreenCapture.CaptureScreenshot("Assets/ScoreCollectionRiseTest.png");
            SessionState.SetString("ScoreCollectionTest.RiseScreenshot", "Assets/ScoreCollectionRiseTest.png");
            while (Time.unscaledTime - began < rise + flight * 0.25f) yield return null;
            Check(Pending && _score.text == "780", "HUD collected before flight arrival");
            ScreenCapture.CaptureScreenshot("Assets/ScoreCollectionFlightTest.png");
            SessionState.SetString("ScoreCollectionTest.FlightScreenshot", "Assets/ScoreCollectionFlightTest.png");
            while (Pending) { Check(Time.unscaledTime - began < 3f, "Runtime popup failed to arrive"); yield return null; }
            Check(Displayed == 810 && _score.text == "810", "Runtime arrival total mismatch");
            float pulseWait = Time.unscaledTime;
            while (Time.unscaledTime - pulseWait < 0.25f) yield return null;
            Check(((RectTransform)Get(_feedback, "_pulseTarget")).localScale == Vector3.one, "Runtime pulse remained enlarged");
            Log("Runtime +30: " + rise + "s rise, " + flight + "s flight, collected once, and restored pulse scale");

            Component cameraFeedback = _camera.gameObject.AddComponent(T("Gameplay.BoardCameraFeedback"));
            Set(cameraFeedback, "_game", _game); Call(cameraFeedback, "OnEnable");
            int mergeStarts = 0;
            float peakFov = 2f * Mathf.Atan(Mathf.Tan(45f * Mathf.Deg2Rad * 0.5f) / (1f + (float)Get(cameraFeedback, "_magnification"))) * Mathf.Rad2Deg;
            _grid.GetType().GetEvent("onMergeStarted").AddEventHandler(_grid, (Action)(() =>
            {
                Check(Mathf.Abs(_camera.fieldOfView - peakFov) < 0.0001f, "Real merge did not snap lens on onset");
                Check((int)Get(_game, "score") == (mergeStarts == 0 ? 0 : 20), "Camera impact happened after merge scoring, not at onset");
                mergeStarts++;
            }));
            Call(_feedback, "ClearAndSynchronize"); Set(_game, "score", 0); Set(_game, "recordScore", 0); Call(_feedback, "ClearAndSynchronize");
            Set(_grid, "totalColumns", 3); Set(_grid, "totalRows", 4);
            var save = Activator.CreateInstance(T("Data_Persistence.GameSaveData"));
            Set(save, "boardState", new List<Vector3> { new Vector3(1, 0, 1), new Vector3(0, 0, 2) });
            Call(_grid, "Load", save, Activator.CreateInstance(T("Data_Persistence.ProgressionData")));
            Call(_game, "AddTowerToQueue", 1); Call(_game, "AddTowerToQueue", 1); Call(_game, "AddTowerToQueue", 1);
            began = Time.unscaledTime; Call(_game, "GetCellHit", 1, 2);
            while (!(bool)Call(_grid, "IsSettled")) { Check(Time.unscaledTime - began < 4f, "Chain board did not settle"); yield return null; }
            Check((int)Get(_game, "score") == 50, "Actual merge chain scoring changed");
            Check(Pending && Displayed < 50 && !(bool)Prop(_game, "IsResolvingMove"), "Cosmetic collection blocked board settlement or revealed points early");
            var saveOut = Activator.CreateInstance(T("Data_Persistence.GameSaveData"));
            Set(_game, "hasActiveRun", true);
            var args = new object[] { saveOut, Activator.CreateInstance(T("Data_Persistence.ProgressionData")) };
            _game.GetType().GetMethod("Save").Invoke(_game, args);
            Check((int)Get(args[0], "score") == 50, "Save omitted in-flight points");
            while (Pending) { Check(Time.unscaledTime - began < 4f, "Chain collection stalled"); yield return null; }
            Check(Displayed == 50, "Chain popup totals did not reach 50");
            Check(mergeStarts == 2, "Chain camera onset emitted extra or missing pulses");
            Log("Real restored-tower chain +20/+30: model 50, instant camera impact at each merge start, visual collection independent of board settlement, full score saved mid-flight");
            Award(2); Set(_game, "state", Enum.ToObject(T("Gameplay.GameController+GameState"), 0));
            while (Pending) yield return null;
            Check(Displayed == 70, "Pause stranded cosmetic points");
            Log("Unscaled pause collection and no user save/progress service fixture passed");

            Call(_grid, "ClearBoard"); Set(_grid, "grid", null);
            Set(_game, "score", 0); Set(_game, "recordScore", 0); Set(_game, "moveResolving", false);
            Set(_game, "state", Enum.ToObject(T("Gameplay.GameController+GameState"), 1));
            Call(_feedback, "ClearAndSynchronize"); mergeStarts = 0;
            var horizontalSave = Activator.CreateInstance(T("Data_Persistence.GameSaveData"));
            Set(horizontalSave, "boardState", new List<Vector3> { new Vector3(0, 0, 1), new Vector3(2, 0, 2) });
            Call(_grid, "Load", horizontalSave, Activator.CreateInstance(T("Data_Persistence.ProgressionData")));
            var horizontalStarts = new List<float>();
            float firstHorizontalCompletion = -1f;
            _grid.GetType().GetEvent("onMergeStarted").AddEventHandler(_grid, (Action)(() => horizontalStarts.Add(Time.time)));
            _grid.GetType().GetEvent("onMergeCompleted").AddEventHandler(_grid, (Action)(() => { if ((int)Get(_game, "score") == 20) firstHorizontalCompletion = Time.time; }));
            var head = (GameObject)((IList)Get(_queue, "towerQueue"))[0];
            Set(head.GetComponent(T("Gameplay.TowerController")), "level", 1);
            float requiredPause = (float)Get(head.GetComponent(T("Gameplay.TowerController")), "_horizontalChainDelay");
            began = Time.unscaledTime; Call(_game, "GetCellHit", 1, 2);
            while (!(bool)Call(_grid, "IsSettled")) { Check(Time.unscaledTime - began < 4f, "Horizontal chain did not settle"); yield return null; }
            Check(horizontalStarts.Count == 2 && firstHorizontalCompletion >= 0f, "Horizontal test did not produce two merge batches");
            Check(horizontalStarts[1] - firstHorizontalCompletion >= requiredPause - 0.001f, "Stationary horizontal chain omitted artificial pause");
            Check((int)Get(_game, "score") == 50, "Horizontal delay changed merge scoring");
            Log("Horizontal-only stationary chain paused " + (horizontalStarts[1] - firstHorizontalCompletion) + "s before next merge (configured " + requiredPause + "s)");
            while (Pending) { Check(Time.unscaledTime - began < 4f, "Horizontal score collection stalled"); yield return null; }
            yield return null;
        }
    }
}
