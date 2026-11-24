using System;
using System.Collections.Generic;
using System.IO;
using Data_Persistence;
using Gameplay;
using UI;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    public static class SaveDataEditorMenu
    {
        private const string WindowMenuPath = "Tools/Player Reset Tool";
        private const string CityBlocksWindowMenuPath = "Tools/City Blocks/Player Reset Tool";
        private const string SetLevelMenuPath = "Tools/City Blocks/Set Player Level...";
        private const string DeleteAllMenuPath = "Tools/City Blocks/Delete All Save Files (Reset Player)";

        [MenuItem(WindowMenuPath, false, 10)]
        [MenuItem(CityBlocksWindowMenuPath, false, 10)]
        [MenuItem(SetLevelMenuPath, false, 11)]
        public static void OpenResetToolWindow()
        {
            SaveDataManagerWindow.ShowWindow();
        }

        [MenuItem(DeleteAllMenuPath, false, 20)]
        public static void DeleteAllSaveFilesAndReset()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Reset Save Data?",
                "Are you sure you want to delete all save files and reset the game back to the new player state?\n\n" +
                "This will:\n" +
                "• Delete level progress, active runs, and endless saves\n" +
                "• Delete login streak data\n" +
                "• Clear player preferences (e.g. endless attempts)\n" +
                "• Start from Level 1 tutorial on next play",
                "Reset to New Player",
                "Cancel"
            );

            if (!confirmed) return;

            string persistentPath = Application.persistentDataPath;
            string[] filesToDelete =
            {
                Path.Combine(persistentPath, "level-progress.json"),
                Path.Combine(persistentPath, "level-progress.json.tmp"),
                Path.Combine(persistentPath, "SaveGameData"),
                Path.Combine(persistentPath, "ProgressionData"),
                Path.Combine(persistentPath, "daily-login-streak.json")
            };

            int deletedCount = 0;
            foreach (var filePath in filesToDelete)
            {
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[SaveDataEditorMenu] Failed to delete file {filePath}: {ex.Message}");
                    }
                }
            }

            // Clear PlayerPrefs
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            // Reset active in-memory controller if running in Play Mode
            if (Application.isPlaying)
            {
                if (DataPersistenceController.instance != null)
                {
                    DataPersistenceController.instance.NewGame();
                }

                if (LevelFlow.ActiveSession != null)
                {
                    LevelFlow.ActiveSession.Progress.activeRun = null;
                    LevelFlow.GoToMenu();
                }

                RefreshActiveUI();
            }

            Debug.Log($"[SaveDataEditorMenu] Save data reset successfully. ({deletedCount} files deleted, PlayerPrefs cleared). The game is back to new player state.");
            EditorUtility.DisplayDialog(
                "Save Data Reset",
                "All save data has been deleted.\nThe game is now reset back to a clean new player state.",
                "OK"
            );
        }

        public static LevelProgressData ResetPlayerToLevel(
            int targetLevel,
            bool markPreviousCompleted = true,
            bool completeTutorial = true,
            bool resetPowerUps = false,
            bool clearEndlessSave = true,
            bool clearPlayerPrefsAndStreaks = false,
            bool showDialog = false)
        {
            int minLevel = 1;
            int maxLevel = 100;
            if (LevelCatalog.Levels != null && LevelCatalog.Levels.Count > 0)
            {
                minLevel = LevelCatalog.Levels[0].levelNumber;
                maxLevel = LevelCatalog.Levels[LevelCatalog.Levels.Count - 1].levelNumber;
            }

            targetLevel = Mathf.Clamp(targetLevel, minLevel, maxLevel);

            LevelProgressData existing = LevelProgressStore.Load();
            LevelProgressData data = new LevelProgressData();
            data.highestUnlockedLevel = targetLevel;
            data.completedLevels = new List<int>();

            if (markPreviousCompleted)
            {
                for (int lvl = minLevel; lvl < targetLevel; lvl++)
                {
                    data.completedLevels.Add(lvl);
                }
            }

            data.levelTutorialComplete = completeTutorial;
            data.activeRun = null;

            if (!resetPowerUps && existing != null && existing.powerUps != null)
            {
                data.powerUps = existing.powerUps;
            }
            else
            {
                data.powerUps = new PowerUpInventory();
            }

            if (!clearPlayerPrefsAndStreaks && existing != null)
            {
                data.adsRemoved = existing.adsRemoved;
                data.hasMadePurchase = existing.hasMadePurchase;
                if (existing.processedPurchaseIds != null)
                {
                    data.processedPurchaseIds = new List<string>(existing.processedPurchaseIds);
                }
            }

            LevelProgressStore.Save(data);

            string persistentPath = Application.persistentDataPath;

            // Delete temporary save file if it exists
            string tmpFile = Path.Combine(persistentPath, "level-progress.json.tmp");
            if (File.Exists(tmpFile))
            {
                try { File.Delete(tmpFile); } catch (Exception ex) { Debug.LogWarning($"[SaveDataEditorMenu] Failed to delete {tmpFile}: {ex.Message}"); }
            }

            // Clear endless run & session data
            if (clearEndlessSave)
            {
                string[] endlessFiles =
                {
                    Path.Combine(persistentPath, "SaveGameData"),
                    Path.Combine(persistentPath, "ProgressionData")
                };
                foreach (string f in endlessFiles)
                {
                    if (File.Exists(f))
                    {
                        try { File.Delete(f); } catch (Exception ex) { Debug.LogWarning($"[SaveDataEditorMenu] Failed to delete {f}: {ex.Message}"); }
                    }
                }
            }

            // Clear daily streak & PlayerPrefs
            if (clearPlayerPrefsAndStreaks)
            {
                string streakFile = Path.Combine(persistentPath, "daily-login-streak.json");
                if (File.Exists(streakFile))
                {
                    try { File.Delete(streakFile); } catch (Exception ex) { Debug.LogWarning($"[SaveDataEditorMenu] Failed to delete {streakFile}: {ex.Message}"); }
                }

                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
            }

            // Sync active runtime state if playing
            if (Application.isPlaying)
            {
                if (DataPersistenceController.instance != null)
                {
                    DataPersistenceController.instance.NewGame();
                }

                if (LevelFlow.ActiveSession != null)
                {
                    if (LevelFlow.ActiveSession.Progress != null)
                    {
                        LevelFlow.ActiveSession.Progress.activeRun = null;
                    }
                    LevelFlow.GoToMenu();
                }

                RefreshActiveUI();
            }

            Debug.Log($"[SaveDataEditorMenu] Player set to Level {targetLevel}. (Highest unlocked: {data.highestUnlockedLevel}, Completed count: {data.completedLevels.Count}, Tutorial complete: {data.levelTutorialComplete})");

            if (showDialog)
            {
                string completedText = data.completedLevels.Count > 0
                    ? $"1 through {targetLevel - 1} ({data.completedLevels.Count} levels)"
                    : "None";

                EditorUtility.DisplayDialog(
                    "Player Level Set",
                    $"Player successfully set to Level {targetLevel}!\n\n" +
                    $"• Highest Unlocked: Level {targetLevel}\n" +
                    $"• Completed Levels: {completedText}\n" +
                    $"• Tutorial Complete: {data.levelTutorialComplete}\n" +
                    $"• Endless Mode: {(data.completedLevels.Contains(3) ? "Unlocked" : "Locked (requires Level 3)")}",
                    "OK"
                );
            }

            return data;
        }

        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string ForceDailyModalOnPlayKey = "CityBlocks.Debug.ForceDailyModalOnPlay";

        /// <summary>
        /// Prepares a daily reward test. Makes today's reward claimable (next streak day if already claimed today).
        /// In Edit Mode it opens MainMenu and arms the modal so it shows when you press Play yourself (even for a
        /// brand-new tutorial player); it does not enter Play Mode. In Play Mode the modal reopens immediately.
        /// Returns the streak day that will be shown.
        /// </summary>
        public static int PrepareDailyLoginReward()
        {
            DailyLoginStreakService.DebugMakeClaimableToday();
            int day = DailyLoginStreakService.CurrentStreakDay;

            if (Application.isPlaying)
            {
                if (!ForceOpenDailyModal())
                    Debug.LogWarning("[SaveDataEditorMenu] No DailyLoginRewards modal in the open scene. Go to the main menu and press the button again.");
                Debug.Log($"[SaveDataEditorMenu] Showing daily login reward as Day {day}.");
                return day;
            }

            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return day;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != MainMenuScenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainMenuScenePath);
            SessionState.SetBool(ForceDailyModalOnPlayKey, true);

            Debug.Log($"[SaveDataEditorMenu] Daily login reward prepared as Day {day}. Press Play to see it.");
            return day;
        }

        /// <summary>True when a prepared daily reward is waiting for the next Play.</summary>
        public static bool DailyRewardPrepared => SessionState.GetBool(ForceDailyModalOnPlayKey, false);

        /// <summary>Clears the daily login streak: next claim is Day 1. Progress, power-ups and PlayerPrefs are kept.</summary>
        public static void ResetDailyStreak()
        {
            DailyLoginStreakService.DebugReset();
            Debug.Log("[SaveDataEditorMenu] Daily login streak reset. Next claim is Day 1.");
        }

        private static bool ForceOpenDailyModal()
        {
            bool opened = false;
            foreach (var ui in UnityEngine.Object.FindObjectsByType<DailyLoginStreakUI>(FindObjectsInactive.Include))
                if (ui != null && ui.gameObject.scene.IsValid() && ui.DebugForceOpen()) opened = true;
            return opened;
        }

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeHook()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(ForceDailyModalOnPlayKey, false)) return;
                SessionState.EraseBool(ForceDailyModalOnPlayKey);
                // Wait a frame so the scene's Start() has run; if it already opened the modal (reward claimed), nothing more to do.
                EditorApplication.delayCall += () =>
                {
                    if (Application.isPlaying && DailyLoginStreakService.CanClaim) ForceOpenDailyModal();
                };
            };
        }

public static void RefreshActiveUI()
        {
            if (!Application.isPlaying) return;

            LevelButton_MainMenu[] levelButtons = UnityEngine.Object.FindObjectsByType<LevelButton_MainMenu>(FindObjectsInactive.Exclude);
            foreach (var btn in levelButtons)
            {
                if (btn != null) btn.RefreshButton();
            }

            PlayButton_MainMenu[] playButtons = UnityEngine.Object.FindObjectsByType<PlayButton_MainMenu>(FindObjectsInactive.Exclude);
            foreach (var btn in playButtons)
            {
                if (btn != null) btn.SendMessage("RefreshUnlockState", SendMessageOptions.DontRequireReceiver);
            }

            LevelRulesUI[] rulesUIs = UnityEngine.Object.FindObjectsByType<LevelRulesUI>(FindObjectsInactive.Exclude);
            foreach (var ui in rulesUIs)
            {
                if (ui != null) ui.Refresh();
            }
        }
    }

    public class SaveDataManagerWindow : EditorWindow
    {
        private int targetLevel = 1;
        private bool markPreviousCompleted = true;
        private bool completeTutorial = true;
        private bool resetPowerUps = false;
        private bool clearEndlessSave = true;
        private bool clearPlayerPrefsAndStreaks = false;
        private bool resetTargetLevelReward = false;
        private bool settingPlayerLevel;
        // Deliberately not serialized or saved to EditorPrefs/PlayerPrefs.
        private string rewardResetKeyId = "";
        private string rewardResetKeySecret = "";
        private int claimableDay = 2;

        private string statusMessage = "";
        private MessageType statusMessageType = MessageType.Info;
        private Vector2 scrollPos;

        public static void ShowWindow()
        {
            SaveDataManagerWindow window = GetWindow<SaveDataManagerWindow>("Player Reset Tool");
            window.minSize = new Vector2(360, 520);
            window.Show();
        }

        private void OnEnable()
        {
            LevelProgressData data = LevelProgressStore.Load();
            if (data != null && data.highestUnlockedLevel > 0)
            {
                targetLevel = data.highestUnlockedLevel;
            }
            completeTutorial = targetLevel > 1;
            markPreviousCompleted = targetLevel > 1;
        }

        private string CurrentRewardResetPlayerId()
        {
            string signedInPlayer = CloudServices.AccountService.PlayerId;
            if (!string.IsNullOrEmpty(signedInPlayer)) return signedInPlayer;
            string cache = PlayerPrefs.GetString("cityblocks_wallet_cache", "");
            if (string.IsNullOrEmpty(cache)) return null;
            try { return (string)Newtonsoft.Json.Linq.JObject.Parse(cache)["playerId"]; }
            catch (Exception) { return null; }
        }

        private async void SetPlayerLevelWithRewardReset()
        {
            if (settingPlayerLevel) return;
            int level = targetLevel;
            bool previousCompleted = markPreviousCompleted;
            bool tutorialCompleted = completeTutorial;
            bool powerUpsReset = resetPowerUps;
            bool endlessCleared = clearEndlessSave;
            bool prefsCleared = clearPlayerPrefsAndStreaks;
            bool resetReward = resetTargetLevelReward;
            string playerId = CurrentRewardResetPlayerId();
            string keyId = rewardResetKeyId;
            string secret = rewardResetKeySecret;
            settingPlayerLevel = true;
            statusMessage = resetReward ? "Resetting selected level reward in development..." : "Setting player level...";
            statusMessageType = MessageType.Info;
            Repaint();
            try
            {
                string cloudMessage = "";
                if (resetReward)
                {
                    cloudMessage = await CityBlocks.EditorTools.LevelRewardResetAdmin.ResetAsync(playerId, level, keyId, secret);
                    if (this == null) return;
                    if (CurrentRewardResetPlayerId() != playerId)
                        throw new InvalidOperationException("The active player changed during the reset. Local progression was not changed.");
                }
                SaveDataEditorMenu.ResetPlayerToLevel(
                    targetLevel: level,
                    markPreviousCompleted: previousCompleted,
                    completeTutorial: tutorialCompleted,
                    resetPowerUps: powerUpsReset,
                    clearEndlessSave: endlessCleared,
                    clearPlayerPrefsAndStreaks: prefsCleared,
                    showDialog: false);
                statusMessage = $"Player successfully set to Level {level}!" + (resetReward ? "\n" + cloudMessage : "");
                statusMessageType = MessageType.Info;
            }
            catch (Exception exception)
            {
                if (this == null) return;
                // Do not print credential-bearing HTTP bodies or tokens to the Console.
                statusMessage = "Player setup failed: " + exception.Message;
                statusMessageType = MessageType.Error;
            }
            finally
            {
                if (this != null)
                {
                    rewardResetKeySecret = "";
                    settingPlayerLevel = false;
                    Repaint();
                }
            }
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Player Reset & Level Setup Tool", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Set player progression to a specific level, or reset the player completely.", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);

            // Level Catalog bounds
            int minLevel = 1;
            int maxLevel = 100;
            if (LevelCatalog.Levels != null && LevelCatalog.Levels.Count > 0)
            {
                minLevel = LevelCatalog.Levels[0].levelNumber;
                maxLevel = LevelCatalog.Levels[LevelCatalog.Levels.Count - 1].levelNumber;
            }

            // Current Player State Box
            DrawCurrentStateBox();

            EditorGUILayout.Space(10);

            // Daily Login Streak Box
            DrawDailyStreakBox();

            EditorGUILayout.Space(10);

            // Target Level Configuration Box
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Set Specific Level", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            int newTarget = EditorGUILayout.IntSlider("Target Level", targetLevel, minLevel, maxLevel);
            if (newTarget != targetLevel)
            {
                targetLevel = newTarget;
                completeTutorial = targetLevel > 1;
                markPreviousCompleted = targetLevel > 1;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Quick Jump", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            DrawQuickLevelButton(1, "1 (New)");
            DrawQuickLevelButton(2, "2");
            DrawQuickLevelButton(3, "3 (Endless)");
            DrawQuickLevelButton(5, "5");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawQuickLevelButton(10, "10");
            DrawQuickLevelButton(20, "20");
            DrawQuickLevelButton(50, "50");
            DrawQuickLevelButton(maxLevel, $"{maxLevel} (Max)");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Progression Options", EditorStyles.miniBoldLabel);

            resetTargetLevelReward = EditorGUILayout.Toggle(
                new GUIContent("Reset Target Level Reward", "Make only this level's first-win reward claimable again in development. Does not remove coins or power-ups."),
                resetTargetLevelReward);
            if (resetTargetLevelReward)
            {
                EditorGUILayout.HelpBox("Development only. Cloud reset runs first; local level setup is cancelled if it fails. Other progression options below keep their existing behavior.", MessageType.Info);
                EditorGUILayout.LabelField("Cloud Player ID", CurrentRewardResetPlayerId() ?? "Unknown — sign in once in Play Mode");
                rewardResetKeyId = EditorGUILayout.TextField("Service Account Key ID", rewardResetKeyId);
                rewardResetKeySecret = EditorGUILayout.PasswordField("Key Secret", rewardResetKeySecret);
                EditorGUILayout.LabelField("Credentials are not saved. Secret is cleared after each attempt.", EditorStyles.wordWrappedMiniLabel);
            }

            markPreviousCompleted = EditorGUILayout.Toggle(
                new GUIContent("Mark Prior Levels Done", "Marks levels 1 through (Target - 1) as completed. Unlocks Endless mode (requires Level 3)."),
                markPreviousCompleted
            );

            completeTutorial = EditorGUILayout.Toggle(
                new GUIContent("Tutorial Completed", "If true, skips the Level 1 tutorial introduction."),
                completeTutorial
            );

            resetPowerUps = EditorGUILayout.Toggle(
                new GUIContent("Reset Power-Ups (0)", "If checked, clears power-up inventory. If unchecked, preserves existing power-ups."),
                resetPowerUps
            );

            clearEndlessSave = EditorGUILayout.Toggle(
                new GUIContent("Clear Endless Run Save", "Clears saved active endless board and session state."),
                clearEndlessSave
            );

            clearPlayerPrefsAndStreaks = EditorGUILayout.Toggle(
                new GUIContent("Clear Prefs & Login Streak", "Clears daily login streak file and all PlayerPrefs."),
                clearPlayerPrefsAndStreaks
            );

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Primary Action: Set to Level
            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
            using (new EditorGUI.DisabledScope(settingPlayerLevel))
            {
                if (GUILayout.Button(settingPlayerLevel ? "Setting player level..." : $"Set Player to Level {targetLevel}", GUILayout.Height(36)))
                    SetPlayerLevelWithRewardReset();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(8);

            // Danger Zone: Full Reset
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("Full Reset to New Player (Delete All Saves)", GUILayout.Height(28)))
            {
                SaveDataEditorMenu.DeleteAllSaveFilesAndReset();
                targetLevel = 1;
                completeTutorial = false;
                markPreviousCompleted = false;
                statusMessage = "All save data deleted. Player reset to clean new player state.";
                statusMessageType = MessageType.Warning;
                Repaint();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusMessageType);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCurrentStateBox()
        {
            LevelProgressData data = LevelProgressStore.Load();
            if (data == null) data = new LevelProgressData();
            if (data.powerUps == null) data.powerUps = new PowerUpInventory();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current Player State", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh", GUILayout.Width(65)))
            {
                statusMessage = "";
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            EditorGUILayout.LabelField($"Highest Unlocked Level:", $"Level {data.highestUnlockedLevel}", EditorStyles.boldLabel);

            string completedSummary = data.completedLevels == null || data.completedLevels.Count == 0
                ? "None"
                : $"{data.completedLevels.Count} level(s)";
            EditorGUILayout.LabelField("Completed Levels:", completedSummary);

            EditorGUILayout.LabelField("Tutorial Status:", data.levelTutorialComplete ? "Completed" : "Not Completed (will launch tutorial)");

            string activeRunSummary = data.activeRun != null && data.activeRun.levelNumber > 0
                ? $"Level {data.activeRun.levelNumber} (in progress)"
                : "None";
            EditorGUILayout.LabelField("Active Run:", activeRunSummary);

            bool endlessUnlocked = data.completedLevels != null && data.completedLevels.Contains(3);
            EditorGUILayout.LabelField("Endless Mode:", endlessUnlocked ? "Unlocked" : "Locked (requires Level 3 clear)");

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"Power-Ups:  Del: {data.powerUps.deleteUses} | Look: {data.powerUps.deleteRowUses} | LvlUp: {data.powerUps.levelUpUses} | Extra: {data.powerUps.extraTurnsUses}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void OnInspectorUpdate()
        {
            // Keep the streak status live while the game claims rewards in Play Mode.
            if (Application.isPlaying) Repaint();
        }

        private void DrawDailyStreakBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Daily Login Reward", EditorStyles.boldLabel);
            EditorGUILayout.Space(3);

            DateTime? lastClaim = DailyLoginStreakService.DebugLastClaimDate;
            string streakText = lastClaim.HasValue
                ? $"Day {DailyLoginStreakService.DebugStoredStreakDay} (last claimed {lastClaim.Value:yyyy-MM-dd})"
                : "Never claimed";
            EditorGUILayout.LabelField("Streak:", streakText);

            int nextDay = DailyLoginStreakService.CanClaim
                ? DailyLoginStreakService.CurrentStreakDay
                : DailyLoginStreakService.DebugNextDayAfterToday;
            string nextReward = DailyLoginStreakUI.Describe(DailyLoginStreakService.RewardForDay(nextDay), DailyLoginStreakService.CoinsForDay(nextDay));
            EditorGUILayout.LabelField("Next Test Shows:", $"Day {nextDay} (+{nextReward})");

            EditorGUILayout.Space(6);
            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
            if (GUILayout.Button(new GUIContent("Prepare Daily Login Reward",
                    "Makes today's reward claimable (next streak day if already claimed today). " +
                    "In Edit Mode this opens MainMenu but does NOT enter Play Mode: press Play yourself and the modal shows. " +
                    "In Play Mode the modal reopens immediately."),
                    GUILayout.Height(32)))
            {
                int day = SaveDataEditorMenu.PrepareDailyLoginReward();
                statusMessage = Application.isPlaying
                    ? $"Daily reward: showing Day {day}. Press again to advance to the next day."
                    : $"Daily reward prepared: Day {day}. Press Play to see it.";
                statusMessageType = MessageType.Info;
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = Color.white;

            if (!Application.isPlaying && SaveDataEditorMenu.DailyRewardPrepared)
                EditorGUILayout.HelpBox("Prepared — the daily reward will show when you press Play.", MessageType.None);

            if (GUILayout.Button(new GUIContent("Reset Streak",
                    "Clears the daily login streak so the next test is Day 1. Level progress, power-ups and PlayerPrefs are kept."),
                    GUILayout.Height(24)))
            {
                SaveDataEditorMenu.ResetDailyStreak();
                statusMessage = "Daily login streak reset. Next test shows Day 1.";
                statusMessageType = MessageType.Info;
                Repaint();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawQuickLevelButton(int lvl, string label)
        {
            if (GUILayout.Button(label, GUILayout.Height(22)))
            {
                targetLevel = lvl;
                completeTutorial = targetLevel > 1;
                markPreviousCompleted = targetLevel > 1;
                GUI.FocusControl(null);
            }
        }
    }
}


