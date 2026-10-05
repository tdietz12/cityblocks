using System;
using Gameplay;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(LevelDefinition))]
    public class LevelDefinitionEditor : UnityEditor.Editor
    {
        private LevelDefinition levelDef;
        private SerializedProperty levelNumberProp;
        private SerializedProperty difficultyProp;
        private SerializedProperty columnsProp;
        private SerializedProperty rowsProp;
        private SerializedProperty queueProp;
        private SerializedProperty randomMaximumLevelProp;
        private SerializedProperty moveLimitProp;
        private SerializedProperty objectivesProp;
        private SerializedProperty tutorialStepsProp;
        private SerializedProperty showAdAfterWinProp;

        private bool showGeneratedDataFold;

        private void OnEnable()
        {
            levelDef = (LevelDefinition)target;
            levelNumberProp = serializedObject.FindProperty("levelNumber");
            difficultyProp = serializedObject.FindProperty("difficulty");
            columnsProp = serializedObject.FindProperty("columns");
            rowsProp = serializedObject.FindProperty("rows");
            queueProp = serializedObject.FindProperty("queue");
            randomMaximumLevelProp = serializedObject.FindProperty("randomMaximumLevel");
            moveLimitProp = serializedObject.FindProperty("moveLimit");
            objectivesProp = serializedObject.FindProperty("objectives");
            tutorialStepsProp = serializedObject.FindProperty("tutorialSteps");
            showAdAfterWinProp = serializedObject.FindProperty("showAdAfterWin");

            if (levelDef != null)
            {
                if (!levelDef.HasPopulatedGrid() &&
                    ((levelDef.startingBlocks != null && levelDef.startingBlocks.Count > 0) ||
                     (levelDef.obstacles != null && levelDef.obstacles.Count > 0) ||
                     (levelDef.unavailableCells != null && levelDef.unavailableCells.Count > 0)))
                {
                    Undo.RecordObject(levelDef, "Sync Grid from Legacy");
                    levelDef.SyncToGrid();
                    EditorUtility.SetDirty(levelDef);
                }
                else
                {
                    levelDef.ResizeGrid(levelDef.columns, levelDef.rows);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Level Information", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(levelNumberProp);
            EditorGUILayout.PropertyField(difficultyProp);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Board Dimensions", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            int newCols = EditorGUILayout.IntSlider(new GUIContent("Columns (Width)"), columnsProp.intValue, 3, 7);
            int newRows = EditorGUILayout.IntSlider(new GUIContent("Rows (Height)"), rowsProp.intValue, 3, 7);
            if (EditorGUI.EndChangeCheck())
            {
                columnsProp.intValue = newCols;
                rowsProp.intValue = newRows;
                serializedObject.ApplyModifiedProperties();

                Undo.RecordObject(levelDef, "Resize Level Grid");
                levelDef.columns = newCols;
                levelDef.rows = newRows;
                levelDef.ResizeGrid(newCols, newRows);
                levelDef.SyncFromGrid();
                EditorUtility.SetDirty(levelDef);
            }

            EditorGUILayout.Space(8);
            DrawGridMatrix();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Gameplay Rules", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(queueProp, true);
            EditorGUILayout.PropertyField(randomMaximumLevelProp);
            EditorGUILayout.PropertyField(moveLimitProp);
            EditorGUILayout.PropertyField(objectivesProp, true);
            EditorGUILayout.PropertyField(tutorialStepsProp, true);
            EditorGUILayout.PropertyField(showAdAfterWinProp);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            DrawValidationSection();

            DrawGeneratedDataSection();
        }

        private void DrawGridMatrix()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Editable Board Grid", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Each cell accepts:\n" +
                "• Number (e.g. 1, 2, 3) = Starting tower of that level\n" +
                "• 'Fixed Breakable' (or 'Fixed Breakable 2') = Breakable obstacle with durability\n" +
                "• 'Fixed Unbreakable' = Immovable steel obstacle\n" +
                "• 'Falling Unbreakable' = Gravity-falling steel obstacle\n" +
                "• 'Hole' or 'X' = Pass-through hole (unavailable cell)\n" +
                "• Empty = Normal open playable cell",
                MessageType.Info);

            EditorGUILayout.Space(4);

            int cols = levelDef.columns;
            int rows = levelDef.rows;

            levelDef.ResizeGrid(cols, rows);

            // Column Headers
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(95)); // Row label spacing
            for (int c = 0; c < cols; c++)
            {
                GUILayout.Label($"Col {c}", EditorStyles.miniBoldLabel, GUILayout.Width(75));
            }
            EditorGUILayout.EndHorizontal();

            // Draw Rows from Top (rows - 1) down to Bottom (0)
            bool gridChanged = false;
            for (int r = rows - 1; r >= 0; r--)
            {
                EditorGUILayout.BeginHorizontal();

                string rowLabel = r == rows - 1 ? $"Row {r} (Top)" : (r == 0 ? $"Row {r} (Bottom)" : $"Row {r}");
                GUILayout.Label(rowLabel, EditorStyles.label, GUILayout.Width(95));

                for (int c = 0; c < cols; c++)
                {
                    string currentVal = levelDef.grid[r].cells[c] ?? string.Empty;
                    Color prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = GetCellColor(currentVal);

                    string newVal = EditorGUILayout.TextField(currentVal, GUILayout.Width(75));

                    GUI.backgroundColor = prevBg;

                    if (newVal != currentVal)
                    {
                        Undo.RecordObject(levelDef, "Edit Level Cell");
                        levelDef.grid[r].cells[c] = newVal;
                        gridChanged = true;
                    }

                    // Context Menu for quick selection on cell
                    Rect fieldRect = GUILayoutUtility.GetLastRect();
                    if (Event.current.type == EventType.ContextClick && fieldRect.Contains(Event.current.mousePosition))
                    {
                        ShowCellContextMenu(r, c);
                        Event.current.Use();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            if (gridChanged)
            {
                levelDef.SyncFromGrid();
                EditorUtility.SetDirty(levelDef);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear All Cells", GUILayout.Height(22)))
            {
                if (EditorUtility.DisplayDialog("Clear All Cells?", "Are you sure you want to clear all cells on this board?", "Yes, Clear", "Cancel"))
                {
                    Undo.RecordObject(levelDef, "Clear Board Cells");
                    for (int r = 0; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                            levelDef.grid[r].cells[c] = string.Empty;
                    levelDef.SyncFromGrid();
                    EditorUtility.SetDirty(levelDef);
                }
            }

            if (GUILayout.Button("Reset from Legacy Lists", GUILayout.Height(22)))
            {
                if (EditorUtility.DisplayDialog("Reset from Legacy Lists?", "Populate the grid from existing startingBlocks, obstacles, and holes?", "Yes, Reset", "Cancel"))
                {
                    Undo.RecordObject(levelDef, "Reset Grid from Legacy Data");
                    levelDef.SyncToGrid();
                    EditorUtility.SetDirty(levelDef);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void ShowCellContextMenu(int row, int col)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Empty"), false, () => SetCellValue(row, col, string.Empty));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Towers/Tower Level 1"), false, () => SetCellValue(row, col, "1"));
            menu.AddItem(new GUIContent("Towers/Tower Level 2"), false, () => SetCellValue(row, col, "2"));
            menu.AddItem(new GUIContent("Towers/Tower Level 3"), false, () => SetCellValue(row, col, "3"));
            menu.AddItem(new GUIContent("Towers/Tower Level 4"), false, () => SetCellValue(row, col, "4"));
            menu.AddItem(new GUIContent("Towers/Tower Level 5"), false, () => SetCellValue(row, col, "5"));
            menu.AddItem(new GUIContent("Towers/Tower Level 6"), false, () => SetCellValue(row, col, "6"));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Obstacles/Fixed Breakable (1 Hit)"), false, () => SetCellValue(row, col, "Fixed Breakable"));
            menu.AddItem(new GUIContent("Obstacles/Fixed Breakable 2 (2 Hits)"), false, () => SetCellValue(row, col, "Fixed Breakable 2"));
            menu.AddItem(new GUIContent("Obstacles/Fixed Breakable 3 (3 Hits)"), false, () => SetCellValue(row, col, "Fixed Breakable 3"));
            menu.AddItem(new GUIContent("Obstacles/Fixed Breakable 4 (4 Hits)"), false, () => SetCellValue(row, col, "Fixed Breakable 4"));
            menu.AddItem(new GUIContent("Obstacles/Fixed Unbreakable"), false, () => SetCellValue(row, col, "Fixed Unbreakable"));
            menu.AddItem(new GUIContent("Obstacles/Falling Unbreakable"), false, () => SetCellValue(row, col, "Falling Unbreakable"));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Hole (Unavailable Cell)"), false, () => SetCellValue(row, col, "Hole"));
            menu.ShowAsContext();
        }

        private void SetCellValue(int row, int col, string value)
        {
            Undo.RecordObject(levelDef, "Set Cell Value");
            levelDef.grid[row].cells[col] = value;
            levelDef.SyncFromGrid();
            EditorUtility.SetDirty(levelDef);
        }

        private Color GetCellColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Color.white;
            string trimmed = value.Trim();

            if (int.TryParse(trimmed, out int level))
            {
                return new Color(0.6f, 0.9f, 0.65f); // Soft green for starting towers
            }
            if (trimmed.StartsWith("Fixed Breakable", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Breakable", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(1.0f, 0.82f, 0.5f); // Orange for breakables
            }
            if (string.Equals(trimmed, "Fixed Unbreakable", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Fixed", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(0.75f, 0.85f, 0.95f); // Soft slate blue for fixed unbreakable
            }
            if (string.Equals(trimmed, "Falling Unbreakable", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Falling", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(0.9f, 0.7f, 0.95f); // Lavender/purple for falling unbreakable
            }
            if (string.Equals(trimmed, "Hole", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "X", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "Unavailable", StringComparison.OrdinalIgnoreCase))
            {
                return new Color(0.55f, 0.55f, 0.55f); // Dark gray for hole
            }

            return new Color(1f, 0.6f, 0.6f); // Red tint for unrecognized format
        }

        private void DrawValidationSection()
        {
            if (levelDef.IsValid(out string error))
            {
                int startingTowers = levelDef.startingBlocks != null ? levelDef.startingBlocks.Count : 0;
                int obstacles = levelDef.obstacles != null ? levelDef.obstacles.Count : 0;
                int holes = levelDef.unavailableCells != null ? levelDef.unavailableCells.Count : 0;
                int playable = (levelDef.columns * levelDef.rows) - holes;

                EditorGUILayout.HelpBox(
                    $"Level is Valid!\nPlayable Cells: {playable} | Starting Towers: {startingTowers} | Obstacles: {obstacles} | Holes: {holes}",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox($"Level Error: {error}", MessageType.Error);
            }
        }

        private void DrawGeneratedDataSection()
        {
            EditorGUILayout.Space(6);
            showGeneratedDataFold = EditorGUILayout.Foldout(showGeneratedDataFold, "Generated Board Objects (Auto-synced)", true);
            if (showGeneratedDataFold)
            {
                EditorGUI.indentLevel++;
                EditorGUI.BeginDisabledGroup(true);

                int blockCount = levelDef.startingBlocks != null ? levelDef.startingBlocks.Count : 0;
                EditorGUILayout.LabelField($"Starting Blocks ({blockCount})", EditorStyles.boldLabel);
                if (levelDef.startingBlocks != null)
                {
                    foreach (var block in levelDef.startingBlocks)
                    {
                        EditorGUILayout.LabelField($"• Col {block.column}, Row {block.row} -> Level {block.towerLevel} Tower");
                    }
                }

                int obstacleCount = levelDef.obstacles != null ? levelDef.obstacles.Count : 0;
                EditorGUILayout.LabelField($"Obstacles ({obstacleCount})", EditorStyles.boldLabel);
                if (levelDef.obstacles != null)
                {
                    foreach (var obs in levelDef.obstacles)
                    {
                        string extra = obs.type == BoardObstacleType.Breakable ? $" (Durability: {obs.durability})" : "";
                        EditorGUILayout.LabelField($"• Col {obs.column}, Row {obs.row} -> {obs.type}{extra}");
                    }
                }

                int holeCount = levelDef.unavailableCells != null ? levelDef.unavailableCells.Count : 0;
                EditorGUILayout.LabelField($"Unavailable Hole Cells ({holeCount})", EditorStyles.boldLabel);
                if (levelDef.unavailableCells != null)
                {
                    foreach (var hole in levelDef.unavailableCells)
                    {
                        EditorGUILayout.LabelField($"• Col {hole.x}, Row {hole.y}");
                    }
                }

                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
            }
        }
    }
}
