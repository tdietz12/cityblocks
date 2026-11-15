using System;
using System.Collections.Generic;
using Gameplay;
using UnityEngine;

namespace UI
{
    public class ObjectiveCardUI : MonoBehaviour
    {
        [Serializable]
        private class ObjectiveRow
        {
            [SerializeField] internal GameObject root;
            [SerializeField] internal UnityEngine.UI.Image icon;
            [SerializeField] internal TMPro.TMP_Text label;
        }

        [Header("Objective Rows")]
        [SerializeField] private List<ObjectiveRow> rows = new List<ObjectiveRow>();
        [SerializeField] private RectTransform cardRect;
        [SerializeField] private float minimumHeight = 72f;
        [SerializeField] private float rowHeight = 18f;
        [SerializeField] private float verticalPadding = 18f;

        private readonly List<LevelObjective> objectives = new List<LevelObjective>();
        private bool showProgress;

        public void Bind(LevelDefinition definition, bool displayProgress, Sprite towerIcon, Sprite scoreIcon)
        {
            objectives.Clear();
            showProgress = displayProgress;
            if (definition != null && definition.objectives != null)
            {
                foreach (LevelObjective objective in definition.objectives)
                {
                    if (objective == null) continue;
                    if (objective.type != LevelObjectiveType.ReachTowerLevel && objective.type != LevelObjectiveType.ReachScore)
                    {
                        Debug.LogWarning("Unsupported objective type: " + objective.type, this);
                        continue;
                    }
                    objectives.Add(objective);
                }
            }

            while (rows.Count < objectives.Count)
            {
                if (rows.Count == 0 || rows[0].root == null)
                {
                    Debug.LogError("Objective card requires a configured row template.", this);
                    break;
                }
                GameObject clone = Instantiate(rows[0].root, rows[0].root.transform.parent);
                clone.name = "Row" + (rows.Count + 1);
                rows.Add(new ObjectiveRow
                {
                    root = clone,
                    icon = clone.GetComponentInChildren<UnityEngine.UI.Image>(true),
                    label = clone.GetComponentInChildren<TMPro.TMP_Text>(true)
                });
            }

            for (int i = 0; i < rows.Count; i++)
            {
                ObjectiveRow row = rows[i];
                bool active = i < objectives.Count;
                if (row.root != null) row.root.SetActive(active);
                if (!active) continue;
                if (row.icon != null)
                {
                    row.icon.sprite = objectives[i].type == LevelObjectiveType.ReachTowerLevel ? towerIcon : scoreIcon;
                    row.icon.preserveAspect = true;
                    row.icon.raycastTarget = false;
                }
                if (row.label != null) row.label.raycastTarget = false;
            }
            if (cardRect != null)
                cardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    Mathf.Max(minimumHeight, objectives.Count * rowHeight + verticalPadding));
            gameObject.SetActive(objectives.Count > 0);
            Refresh(0, 0);
        }

        public void Refresh(int highestTowerLevel, int score)
        {
            for (int i = 0; i < objectives.Count && i < rows.Count; i++)
            {
                if (rows[i].label == null) continue;
                LevelObjective objective = objectives[i];
                int current = objective.type == LevelObjectiveType.ReachTowerLevel ? highestTowerLevel : score;
                rows[i].label.text = showProgress
                    ? current.ToString() + " / " + objective.target.ToString()
                    : objective.target.ToString();
            }
        }
    }
}
