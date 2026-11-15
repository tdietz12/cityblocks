using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CityBlocks.EditorTests
{
    // Editor-hosted integration harness: no NUnit/assembly-definition changes are required.
    [InitializeOnLoad]
    internal static class TowerAnimationFeedbackTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static Component _game, _grid, _queue, _feedback;
        private static Camera _camera, _overlay;
        private static GameObject _root;
        private static IEnumerator _test;
        private static int _frame = -1, _merges, _resolved, _assertions;
        private static float _caseStart;
        private static double _start;
        private static readonly List<string> Logs = new List<string>();

        [Serializable] private class SavedScenes { public SceneSetup[] scenes; }
        [Serializable] private class Report { public bool success; public int assertions; public string error; public string[] logs; }

        static TowerAnimationFeedbackTests()
        {
            string state = SessionState.GetString("TowerAnimationTest.State", "Idle");
            if (state == "WaitingForCompile")
                EditorApplication.delayCall += () =>
                {
                    SessionState.SetString("TowerAnimationTest.State", "EnteringPlayMode");
                    EditorApplication.playModeStateChanged += OnPlayState;
                    EditorApplication.isPlaying = true;
                };
            else if (state == "EnteringPlayMode") EditorApplication.playModeStateChanged += OnPlayState;
            else if (state == "Done") EditorApplication.delayCall += RestoreScenes;
        }

        [MenuItem("Tools/City Blocks/Tests/Tower Animation and Camera Feedback")]
        private static void Launch()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (EditorSettings.enterPlayModeOptionsEnabled &&
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0)
                throw new InvalidOperationException("These tests require Domain Reload to be enabled.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save your edited scenes before running isolated tests.");
            SessionState.SetString("TowerAnimationTest.Scenes", JsonUtility.ToJson(new SavedScenes { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.Refresh();
            SessionState.EraseString("TowerAnimationTest.Result");
            SessionState.EraseString("TowerAnimationTest.ScreenshotPath");
            SessionState.SetString("TowerAnimationTest.State", "WaitingForCompile");
            EditorUtility.RequestScriptReload();
        }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= OnPlayState;
            SessionState.SetString("TowerAnimationTest.State", "InPlayMode");
            Logs.Clear();
            Application.logMessageReceived += CaptureLog;
            _start = EditorApplication.timeSinceStartup;
            _test = RunCases();
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if (EditorApplication.timeSinceStartup - _start > 30d)
                    throw new Exception("Test deadline exceeded. merges=" + _merges + ", settled=" + Call(_grid, "IsSettled"));
                if (Time.frameCount == _frame) return;
                _frame = Time.frameCount;
                if (!_test.MoveNext()) Finish(null);
            }
            catch (Exception e) { Finish(e.ToString()); }
        }

        private static void Finish(string error)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= CaptureLog;
            SessionState.SetString("TowerAnimationTest.Result", JsonUtility.ToJson(new Report
            { success = error == null, assertions = _assertions, error = error, logs = Logs.ToArray() }));
            SessionState.SetString("TowerAnimationTest.State", "Done");
            Debug.Log("TEST_RESULT " + SessionState.GetString("TowerAnimationTest.Result", ""));
            Application.targetFrameRate = -1;
            EditorApplication.playModeStateChanged += RestoreOnExit;
            EditorApplication.isPlaying = false;
        }

        private static void RestoreOnExit(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= RestoreOnExit;
            EditorApplication.delayCall += RestoreScenes;
        }

        [MenuItem("Tools/City Blocks/Tests/Restore Scenes After Animation Test")]
        private static void RestoreScenes()
        {
            string json = SessionState.GetString("TowerAnimationTest.Scenes", "");
            if (json.Length == 0 || EditorApplication.isPlaying) return;
            var setup = JsonUtility.FromJson<SavedScenes>(json);
            EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
            SessionState.EraseString("TowerAnimationTest.Scenes");
        }

        [MenuItem("Tools/City Blocks/Tests/Camera Feedback Deterministic Checks")]
        private static void CheckCameraMath()
        {
            var holder = new GameObject("Temporary camera check");
            holder.SetActive(false);
            var game = holder.AddComponent(TypeOf("Gameplay.GameController"));
            var camera = holder.AddComponent<Camera>();
            var overlayObject = new GameObject("Temporary overlay check");
            overlayObject.transform.SetParent(holder.transform, false);
            var overlay = overlayObject.AddComponent<Camera>();
            var feedback = holder.AddComponent(TypeOf("Gameplay.BoardCameraFeedback"));
            Set(feedback, "_camera", camera); Set(feedback, "_game", game); Set(feedback, "_overlayCamera", overlay);
            camera.fieldOfView = overlay.fieldOfView = 45f;
            holder.transform.SetPositionAndRotation(new Vector3(1.5f, 9.5f, -4.5f), Quaternion.Euler(60f, 0f, 0f));
            Set(game, "state", Enum.ToObject(TypeOf("Gameplay.GameController+GameState"), 1));
            try
            {
                Call(feedback, "CaptureDefaultState");
                Set(feedback, "_punching", true); Set(feedback, "_phaseTime", (float)Get(feedback, "_punchInDuration"));
                Call(feedback, "LateUpdate");
                float expectedFov = 2f * Mathf.Atan(Mathf.Tan(45f * Mathf.Deg2Rad * 0.5f) / (1f + (float)Get(feedback, "_magnification"))) * Mathf.Rad2Deg;
                Check(Mathf.Abs(camera.fieldOfView - expectedFov) < 0.0001f, "Lens magnification math incorrect");
                Check(camera.fieldOfView == overlay.fieldOfView, "Overlay FOV not synchronized");
                Set(feedback, "_phaseTime", (float)Get(feedback, "_returnDuration"));
                Call(feedback, "LateUpdate");
                Check(Mathf.Abs(camera.fieldOfView - 45f) < 0.0001f, "Lens did not restore exactly");
                Vector3 baseline = (Vector3)Get(feedback, "_defaultPosition");
                Call(feedback, "ResetFeedback");
                Check(holder.transform.localPosition == baseline && overlay.fieldOfView == 45f, "Camera reset mismatch");
                int fittedBoards = 0;
                foreach (float aspect in new[] { 9f / 16f, 9f / 19.5f })
                for (int cols = 3; cols <= 7; cols++)
                for (int rows = 3; rows <= 7; rows++)
                {
                    camera.aspect = aspect;
                    TypeOf("Gameplay.BoardCameraFitter").GetMethod("Fit").Invoke(null, new object[] { camera, cols, rows, Vector3.zero });
                    camera.fieldOfView = expectedFov;
                    foreach (float x in new[] { -0.5f, cols - 0.5f })
                    foreach (float y in new[] { 0f, 2.5f })
                    foreach (float z in new[] { -1.5f, rows - 0.5f })
                    {
                        Vector3 vp = camera.WorldToViewportPoint(new Vector3(x, y, z));
                        Check(vp.z > 0f && vp.x > 0.03f && vp.x < 0.97f && vp.y > 0.03f && vp.y < 0.97f,
                            "Portrait framing clipped board/entrance at punch peak: " + cols + "x" + rows + " aspect " + aspect + " " + vp);
                    }
                    Call(feedback, "ResetFeedback");
                    fittedBoards++;
                }
                Debug.Log("[Test] Deterministic camera checks passed: tuned lens punch (" + expectedFov.ToString("F3") +
                    " degrees), overlay alignment/reset, and " + fittedBoards + " portrait board fits at punch peak.");
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
        }

        private static Type TypeOf(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
        private static object Get(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
        private static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
        private static object Call(object o, string name, params object[] args)
        {
            foreach (var method in o.GetType().GetMethods(Flags))
                if (method.Name == name && method.GetParameters().Length == args.Length) return method.Invoke(o, args);
            throw new MissingMethodException(name);
        }
        private static bool Settled => (bool)Call(_grid, "IsSettled");
        private static GameObject[,] Board => (GameObject[,])Get(_grid, "grid");
        private static Vector3 Position(int col, int row) => (Vector3)Call(_grid, "WorldPosition", col, row);
        private static Component Tower(GameObject o) => o.GetComponent(TypeOf("Gameplay.TowerController"));
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            _assertions++;
        }
        private static void Log(string message) { Logs.Add(message); Debug.Log("[Test] " + message); }
        private static void CaptureLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) Logs.Add(type + ": " + message);
        }

        private static void Setup()
        {
            _root = new GameObject("Isolated animation tests");
            _root.SetActive(false);
            _queue = _root.AddComponent(TypeOf("Gameplay.QueueController"));
            _grid = _root.AddComponent(TypeOf("Gameplay.GridController"));
            _game = _root.AddComponent(TypeOf("Gameplay.GameController"));
            ((Behaviour)_game).enabled = false; // No Start/load/progress/analytics side effects.
            Set(_game, "queue", _queue); Set(_game, "grid", _grid);
            Set(_game, "tower", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tower.prefab"));
            Set(_grid, "game", _game);
            Set(_grid, "cell", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cell/Cell.prefab"));
            var cameraObject = new GameObject("Test MainCamera");
            cameraObject.SetActive(false);
            cameraObject.transform.SetParent(_root.transform);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.fieldOfView = 45f;
            cameraObject.transform.position = new Vector3(1.5f, 9.5f, -4.5f);
            cameraObject.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            var overlayObject = new GameObject("Test overlay");
            overlayObject.transform.SetParent(cameraObject.transform, false);
            _overlay = overlayObject.AddComponent<Camera>();
            _overlay.enabled = false;
            _feedback = cameraObject.AddComponent(TypeOf("Gameplay.BoardCameraFeedback"));
            Set(_feedback, "_camera", _camera); Set(_feedback, "_overlayCamera", _overlay); Set(_feedback, "_game", _game);
            cameraObject.SetActive(true);
            _root.SetActive(true);
            _grid.GetType().GetEvent("onMergeCompleted").AddEventHandler(_grid, (Action)(() => _merges++));
            _game.GetType().GetEvent("onMoveResolved").AddEventHandler(_game, (Action)(() => _resolved++));
            var light = new GameObject("Test light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40f, -25f, 0f);
        }

        private static void ResetBoard(int columns, int rows, Vector3[] starting, bool bottomHole = false)
        {
            ((MonoBehaviour)_game).StopAllCoroutines();
            Call(_grid, "ClearBoard");
            Set(_grid, "grid", null);
            var queued = (IList)Get(_queue, "towerQueue");
            foreach (GameObject tower in queued) UnityEngine.Object.Destroy(tower);
            queued.Clear();
            Set(_grid, "totalColumns", columns); Set(_grid, "totalRows", rows + 1);
            bool[,] available = new bool[columns, rows];
            for (int c = 0; c < columns; c++) for (int r = 0; r < rows; r++) available[c, r] = true;
            if (bottomHole) available[0, 0] = false;
            Set(_grid, "available", available);
            var save = Activator.CreateInstance(TypeOf("Data_Persistence.GameSaveData"));
            Set(save, "boardState", new List<Vector3>(starting));
            var progress = Activator.CreateInstance(TypeOf("Data_Persistence.ProgressionData"));
            Call(_grid, "Load", save, progress);
            Set(_game, "score", 0); Set(_game, "level", 0); Set(_game, "moveResolving", false);
            Set(_game, "state", Enum.ToObject(TypeOf("Gameplay.GameController+GameState"), 1));
            _merges = _resolved = 0;
            Call(_game, "AddTowerToQueue", 1); Call(_game, "AddTowerToQueue", 1); Call(_game, "AddTowerToQueue", 1);
            var fitter = TypeOf("Gameplay.BoardCameraFitter");
            fitter.GetMethod("Fit", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { _camera, columns, rows, Vector3.zero });
            _caseStart = Time.time;
        }

        private static GameObject Place(int column)
        {
            var tower = (GameObject)((IList)Get(_queue, "towerQueue"))[0];
            Call(_game, "GetCellHit", column, (int)Call(_grid, "TopPlayableRow", column));
            Check(!Settled, "Board reported settled immediately after placement");
            Check(Vector3.Distance(tower.transform.position, (Vector3)Call(_grid, "VisualEntryPosition", column)) < 0.0001f, "Incorrect top-of-board entrance");
            Check(_camera.WorldToViewportPoint(tower.transform.position).y < _camera.WorldToViewportPoint(Position(column, 0)).y,
                "Top-row entrance must appear below the landing area from the player's camera");
            Check(tower.transform.localScale == Vector3.one, "Incoming queue scale was not restored");
            return tower;
        }

        private static IEnumerator RunCases()
        {
            Setup();
            yield return null;
            foreach (int fps in new[] { 30, 60, 120 })
            {
                Application.targetFrameRate = fps;
                ResetBoard(3, 3, new Vector3[0]);
                var incoming = Place(1);
                while (!Settled) { Check(Time.time - _caseStart < 4f, "Placement did not settle"); yield return null; }
                Check(Board[1, 0] == incoming && Vector3.Distance(incoming.transform.position, Position(1, 0)) < 0.0001f, "Placement endpoint mismatch");
                Check(_merges == 0 && _resolved == 1, "Non-merge emitted feedback or move resolution mismatch");
                Log("Empty placement at target FPS " + fps + " completed in " + (Time.time - _caseStart).ToString("F3") + "s");

                ResetBoard(3, 3, new[] { new Vector3(0, 0, 1), new Vector3(2, 0, 1) });
                incoming = Place(1);
                bool sawAbsorption = false;
                Vector3 heldPosition = Vector3.zero;
                while (!Settled)
                {
                    var motions = (IList)Get(Tower(incoming), "_absorptions");
                    if (motions.Count > 0)
                    {
                        if (!sawAbsorption) { heldPosition = incoming.transform.position; sawAbsorption = true; }
                        Check(Vector3.Distance(incoming.transform.position, heldPosition) < 0.0001f, "Survivor moved during absorption");
                        Check((int)Get(Tower(incoming), "level") == 1, "Tier upgraded before arrivals");
                    }
                    Check(Time.time - _caseStart < 4f, "Multiple merge did not settle");
                    yield return null;
                }
                Check(sawAbsorption, "Absorption phase was never visible");
                Check((int)Get(Tower(incoming), "level") == 3 && (int)Get(_game, "score") == 30, "Batch tier/score semantics changed");
                Check(_merges == 1 && _resolved == 1 && Board[0, 0] == null && Board[2, 0] == null, "Batch ownership or resolution mismatch");
                Log("Simultaneous two-neighbor merge at target FPS " + fps + " passed");
            }

            ResetBoard(3, 3, new[] { new Vector3(1, 0, 1), new Vector3(0, 0, 2) });
            var chain = Place(1);
            while (!Settled) { Check(Time.time - _caseStart < 5f, "Chain did not settle"); yield return null; }
            Check(_merges == 2 && (int)Get(_game, "score") == 50 && (int)Get(Tower(chain), "level") == 3 && Board[1, 0] == chain, "Support removal and chain results changed");
            Log("Support removal -> settle -> chain: two batches, score 50, tier 3");

            ResetBoard(3, 3, new[] { new Vector3(1, 0, 4), new Vector3(1, 1, 3), new Vector3(1, 2, 1) });
            var full = Place(1);
            while (!Settled) { Check(Time.time - _caseStart < 4f, "Full-column merge did not settle"); yield return null; }
            Check(_merges == 1 && Board[1, 2] == full && Board[1, 3] == null && (int)Get(_game, "score") == 20, "Full-column forced merge mismatch");
            Log("Full-column matching top: visible absorption, score 20, no stale entry");

            ResetBoard(3, 3, new[] { new Vector3(1, 2, 2) });
            int totalMoves = (int)_game.GetType().GetProperty("TotalMoves").GetValue(_game);
            var queueHead = ((IList)Get(_queue, "towerQueue"))[0];
            Call(_game, "GetCellHit", 1, 2);
            Check(Settled && _merges == 0 && totalMoves == (int)_game.GetType().GetProperty("TotalMoves").GetValue(_game) && queueHead == ((IList)Get(_queue, "towerQueue"))[0], "Invalid move consumed queue/move");

            ResetBoard(3, 3, new Vector3[0], true);
            var hole = Place(0);
            Check(Vector3.Distance(hole.transform.position, Position(0, 3)) < 0.0001f, "Entry must remain above the board even with a bottom hole");
            while (!Settled) { Check(Time.time - _caseStart < 4f, "Hole placement did not settle"); yield return null; }
            Check(Board[0, 1] == hole && Board[0, 0] == null, "Hole landing altered rules");
            Log("Bottom hole and invalid-column rejection passed");

            ResetBoard(3, 3, new[] { new Vector3(0, 0, 1), new Vector3(1, 0, 1) });
            Set(Tower(Board[0, 0]), "merge", true); Set(Tower(Board[1, 0]), "merge", true);
            while (!Settled) { Check(Time.time - _caseStart < 4f, "Competing merges deadlocked"); yield return null; }
            Check(_merges == 1, "Two competing survivors committed duplicate batches");
            Log("Competing merge requests serialized without duplicate absorption");

            ResetBoard(7, 7, new[] { new Vector3(1, 0, 1) });
            var zeroHead = Tower((GameObject)((IList)Get(_queue, "towerQueue"))[0]);
            Set(zeroHead, "_slideDuration", 0f); Set(zeroHead, "_absorptionDuration", 0f); Set(zeroHead, "fallingTowerPrefab", null);
            var zero = Place(1);
            while (!Settled) { Check(Time.time - _caseStart < 4f, "Zero-duration/fallback merge did not settle"); yield return null; }
            Check(_merges == 1 && (int)Get(Tower(zero), "level") == 2, "Fallback merge wrong result");
            Log("7x7 board, zero durations, missing proxy fallback passed");

            ResetBoard(3, 3, new[] { new Vector3(0, 0, 1), new Vector3(2, 0, 1) });
            var cancel = Place(1);
            while (((IList)Get(Tower(cancel), "_absorptions")).Count == 0)
            { Check(Time.time - _caseStart < 3f, "Cancellation never reached absorption"); yield return null; }
            ((MonoBehaviour)_game).StopAllCoroutines();
            var canceledMotions = (IList)Get(Tower(cancel), "_absorptions");
            Call(_grid, "ClearBoard");
            yield return null;
            Check(Settled && canceledMotions.Count == 0, "ClearBoard left a sequence/proxy alive");
            Log("ClearBoard during absorption cleaned ownership/proxies");

            ResetBoard(3, 3, new[] { new Vector3(0, 0, 1), new Vector3(2, 0, 1) });
            var final = Place(1);
            float baseFov = _camera.fieldOfView;
            float minimumFov = baseFov;
            while (!Settled || Time.time - _caseStart < 1.3f)
            {
                minimumFov = Mathf.Min(minimumFov, _camera.fieldOfView);
                Check(Mathf.Abs(_camera.fieldOfView - _overlay.fieldOfView) < 0.0001f, "Overlay label projection diverged");
                Check(Time.time - _caseStart < 4f, "Final visual test did not settle");
                yield return null;
            }
            float expectedPeak = 2f * Mathf.Atan(Mathf.Tan(baseFov * Mathf.Deg2Rad * 0.5f) / (1f + (float)Get(_feedback, "_magnification"))) * Mathf.Rad2Deg;
            Check(minimumFov < baseFov - 0.3f && minimumFov >= expectedPeak - 0.001f, "Merge lens punch missing or exceeded configured peak: " + minimumFov);
            Check(Mathf.Abs(baseFov - _camera.fieldOfView) < 0.0001f, "Lens did not return to default");
            Vector3 defaultPosition = (Vector3)Get(_feedback, "_defaultPosition");
            Check(Vector3.Distance(_camera.transform.localPosition, defaultPosition) < 0.018f, "Drift exceeded bounded amplitude");
            Call(_feedback, "PunchMerge"); Call(_feedback, "PunchMerge");
            float pulseStart = Time.time;
            while (Time.time - pulseStart < 0.4f) yield return null;
            Check(Mathf.Abs(_camera.fieldOfView - baseFov) < 0.0001f, "Repeated punches accumulated zoom");
            Set(_game, "state", Enum.ToObject(TypeOf("Gameplay.GameController+GameState"), 0));
            yield return null;
            Check(_camera.transform.localPosition == defaultPosition && Mathf.Abs(_camera.fieldOfView - baseFov) < 0.0001f, "Outside gameplay camera did not restore baseline");
            Set(_game, "state", Enum.ToObject(TypeOf("Gameplay.GameController+GameState"), 1));
            yield return null;
            Log("Camera FOV " + minimumFov.ToString("F3") + " -> " + baseFov.ToString("F3") + "; overlays, drift, repeated pulses, pause/reset passed");
            string screenshot = "Assets/TowerAnimationFeedbackTest.png";
            ScreenCapture.CaptureScreenshot(screenshot);
            SessionState.SetString("TowerAnimationTest.ScreenshotPath", screenshot);
            yield return null;
            Log("All isolated animation regression cases completed; no user progress/save services instantiated");
        }
    }
}
