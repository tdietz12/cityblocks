using Gameplay;
using UI;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    public static class PowerUpEditorMenu
    {
        private const string ToolsMenuPath = "Tools/Give 10 of Every Powerup";
        private const string CityBlocksMenuPath = "Tools/City Blocks/Give 10 of Every Powerup";

        [MenuItem(ToolsMenuPath, false, 1)]
        [MenuItem(CityBlocksMenuPath, false, 1)]
        public static void GiveTenOfEveryPowerUp()
        {
            AddPowerUpsToInventory(10);
        }

        public static LevelProgressData AddPowerUpsToInventory(int count, bool showDialog = true)
        {
            LevelProgressData data = LevelProgressStore.Load();
            if (data == null)
            {
                data = new LevelProgressData();
            }

            if (data.powerUps == null)
            {
                data.powerUps = new PowerUpInventory();
            }

            data.powerUps.deleteUses += count;
            data.powerUps.deleteRowUses += count;
            data.powerUps.levelUpUses += count;
            data.powerUps.extraTurnsUses += count;

            LevelProgressStore.Save(data);

            // Synchronize active in-memory session if in Play Mode
            if (LevelFlow.ActiveSession != null && LevelFlow.ActiveSession.Progress != null)
            {
                if (LevelFlow.ActiveSession.Progress.powerUps == null)
                {
                    LevelFlow.ActiveSession.Progress.powerUps = new PowerUpInventory();
                }

                LevelFlow.ActiveSession.Progress.powerUps.deleteUses = data.powerUps.deleteUses;
                LevelFlow.ActiveSession.Progress.powerUps.deleteRowUses = data.powerUps.deleteRowUses;
                LevelFlow.ActiveSession.Progress.powerUps.levelUpUses = data.powerUps.levelUpUses;
                LevelFlow.ActiveSession.Progress.powerUps.extraTurnsUses = data.powerUps.extraTurnsUses;
            }

            // Refresh UI in Play Mode
            if (Application.isPlaying)
            {
                LevelRulesUI[] rulesUIs = Object.FindObjectsByType<LevelRulesUI>(FindObjectsInactive.Exclude);
                foreach (var ui in rulesUIs)
                {
                    if (ui != null) ui.Refresh();
                }

                DeleteButton_PlayPanel[] deleteButtons = Object.FindObjectsByType<DeleteButton_PlayPanel>(FindObjectsInactive.Exclude);
                foreach (var btn in deleteButtons)
                {
                    if (btn != null) btn.SendMessage("Refresh", SendMessageOptions.DontRequireReceiver);
                }

                LookAheadButton_PlayPanel[] lookAheadButtons = Object.FindObjectsByType<LookAheadButton_PlayPanel>(FindObjectsInactive.Exclude);
                foreach (var btn in lookAheadButtons)
                {
                    if (btn != null) btn.SendMessage("Refresh", SendMessageOptions.DontRequireReceiver);
                }

                LevelUpButton_PlayPanel[] levelUpButtons = Object.FindObjectsByType<LevelUpButton_PlayPanel>(FindObjectsInactive.Exclude);
                foreach (var btn in levelUpButtons)
                {
                    if (btn != null) btn.SendMessage("Refresh", SendMessageOptions.DontRequireReceiver);
                }
            }

            Debug.Log($"[PowerUpEditorTool] Added {count} of every power-up. Current totals -> Delete: {data.powerUps.deleteUses}, Look Ahead (Delete Row): {data.powerUps.deleteRowUses}, Level Up: {data.powerUps.levelUpUses}, Extra Turns: {data.powerUps.extraTurnsUses}");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Power-Ups Granted",
                    $"Added {count} of every power-up!\n\nCurrent totals:\n" +
                    $"• Delete: {data.powerUps.deleteUses}\n" +
                    $"• Look Ahead (Delete Row): {data.powerUps.deleteRowUses}\n" +
                    $"• Level Up: {data.powerUps.levelUpUses}\n" +
                    $"• Extra Turns: {data.powerUps.extraTurnsUses}",
                    "OK"
                );
            }

            return data;
        }

        public static void SetSpecificPowerUp(GameController.PowerUpType type, int count)
        {
            LevelProgressData data = LevelProgressStore.Load();
            if (data == null) data = new LevelProgressData();
            if (data.powerUps == null) data.powerUps = new PowerUpInventory();

            switch (type)
            {
                case GameController.PowerUpType.delete:
                    data.powerUps.deleteUses = Mathf.Max(0, count);
                    break;
                case GameController.PowerUpType.lookAhead:
                    data.powerUps.deleteRowUses = Mathf.Max(0, count);
                    break;
                case GameController.PowerUpType.levelUp:
                    data.powerUps.levelUpUses = Mathf.Max(0, count);
                    break;
                case GameController.PowerUpType.extraTurns:
                    data.powerUps.extraTurnsUses = Mathf.Max(0, count);
                    break;
            }

            LevelProgressStore.Save(data);

            if (LevelFlow.ActiveSession != null && LevelFlow.ActiveSession.Progress != null)
            {
                if (LevelFlow.ActiveSession.Progress.powerUps == null)
                    LevelFlow.ActiveSession.Progress.powerUps = new PowerUpInventory();

                LevelFlow.ActiveSession.Progress.powerUps.deleteUses = data.powerUps.deleteUses;
                LevelFlow.ActiveSession.Progress.powerUps.deleteRowUses = data.powerUps.deleteRowUses;
                LevelFlow.ActiveSession.Progress.powerUps.levelUpUses = data.powerUps.levelUpUses;
                LevelFlow.ActiveSession.Progress.powerUps.extraTurnsUses = data.powerUps.extraTurnsUses;
            }

            if (Application.isPlaying)
            {
                LevelRulesUI[] rulesUIs = Object.FindObjectsByType<LevelRulesUI>(FindObjectsInactive.Exclude);
                foreach (var ui in rulesUIs)
                {
                    if (ui != null) ui.Refresh();
                }
            }
        }
    }

    public class PowerUpManagerWindow : EditorWindow
    {
        private const string WindowMenuPath = "Tools/Power-Up Manager";
        private const string CityBlocksWindowMenuPath = "Tools/City Blocks/Power-Up Manager";

        [MenuItem(WindowMenuPath, false, 2)]
        [MenuItem(CityBlocksWindowMenuPath, false, 2)]
        public static void ShowWindow()
        {
            PowerUpManagerWindow window = GetWindow<PowerUpManagerWindow>("Power-Up Manager");
            window.minSize = new Vector2(340, 360);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Power-Up Inventory Manager", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Manage power-up counts for testing & gameplay.", EditorStyles.miniLabel);
            EditorGUILayout.Space(5);

            LevelProgressData data = LevelProgressStore.Load();
            if (data == null) data = new LevelProgressData();
            if (data.powerUps == null) data.powerUps = new PowerUpInventory();

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Current Power-Up Inventory", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawPowerUpRow("Delete", GameController.PowerUpType.delete, data.powerUps.deleteUses);
            DrawPowerUpRow("Look Ahead", GameController.PowerUpType.lookAhead, data.powerUps.deleteRowUses);
            DrawPowerUpRow("Level Up", GameController.PowerUpType.levelUp, data.powerUps.levelUpUses);
            DrawPowerUpRow("Extra Turns", GameController.PowerUpType.extraTurns, data.powerUps.extraTurnsUses);

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
            if (GUILayout.Button("+10 of Every Power-Up", GUILayout.Height(36)))
            {
                PowerUpEditorMenu.AddPowerUpsToInventory(10, showDialog: false);
                Repaint();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 to All"))
            {
                PowerUpEditorMenu.AddPowerUpsToInventory(1, showDialog: false);
                Repaint();
            }
            if (GUILayout.Button("+5 to All"))
            {
                PowerUpEditorMenu.AddPowerUpsToInventory(5, showDialog: false);
                Repaint();
            }
            if (GUILayout.Button("+50 to All"))
            {
                PowerUpEditorMenu.AddPowerUpsToInventory(50, showDialog: false);
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("Reset / Clear All Power-Ups (0)", GUILayout.Height(24)))
            {
                if (EditorUtility.DisplayDialog("Clear Power-Ups?", "Are you sure you want to set all power-up counts to 0?", "Yes, Clear", "Cancel"))
                {
                    PowerUpEditorMenu.SetSpecificPowerUp(GameController.PowerUpType.delete, 0);
                    PowerUpEditorMenu.SetSpecificPowerUp(GameController.PowerUpType.lookAhead, 0);
                    PowerUpEditorMenu.SetSpecificPowerUp(GameController.PowerUpType.levelUp, 0);
                    PowerUpEditorMenu.SetSpecificPowerUp(GameController.PowerUpType.extraTurns, 0);
                    Repaint();
                }
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawPowerUpRow(string label, GameController.PowerUpType type, int currentCount)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(110));
            EditorGUILayout.LabelField(currentCount.ToString(), EditorStyles.boldLabel, GUILayout.Width(50));

            if (GUILayout.Button("+1", GUILayout.Width(35)))
            {
                PowerUpEditorMenu.SetSpecificPowerUp(type, currentCount + 1);
                Repaint();
            }
            if (GUILayout.Button("+10", GUILayout.Width(42)))
            {
                PowerUpEditorMenu.SetSpecificPowerUp(type, currentCount + 10);
                Repaint();
            }
            if (GUILayout.Button("0", GUILayout.Width(25)))
            {
                PowerUpEditorMenu.SetSpecificPowerUp(type, 0);
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
