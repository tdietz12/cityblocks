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
        [SerializeField, Min(0.05f)] private float _rowPulseDuration = 0.16f;
        [SerializeField, Range(1.05f, 1.6f)] private float _rowPulseScale = 1.25f;

        private readonly List<LevelObjective> objectives = new List<LevelObjective>();
        private readonly List<int> _lastValues = new List<int>();
        private readonly List<float> _rowPulseAges = new List<float>();
        private bool showProgress;
        private bool _hasBaseline;

        public void Bind(LevelDefinition definition, bool displayProgress, Sprite towerIcon, Sprite scoreIcon)
        {
            objectives.Clear();
            _lastValues.Clear();
            _rowPulseAges.Clear();
            _hasBaseline = false;
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
                    _lastValues.Add(-1);
                    _rowPulseAges.Add(-1f);
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
                if (row.root != null)
                {
                    row.root.SetActive(active);
                    row.root.transform.localScale = Vector3.one;
                }
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

                while (_lastValues.Count <= i) _lastValues.Add(-1);
                while (_rowPulseAges.Count <= i) _rowPulseAges.Add(-1f);

                if (showProgress && _hasBaseline && _lastValues[i] >= 0 && current > _lastValues[i])
                {
                    _rowPulseAges[i] = 0f;
                    EvaluateRowPulse(i);
                }
                _lastValues[i] = current;
            }
            _hasBaseline = true;
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        public void Advance(float delta)
        {
            delta = Mathf.Max(0f, delta);
            for (int i = 0; i < _rowPulseAges.Count && i < rows.Count; i++)
            {
                if (_rowPulseAges[i] < 0f) continue;
                _rowPulseAges[i] += delta;
                if (_rowPulseAges[i] >= _rowPulseDuration)
                {
                    _rowPulseAges[i] = -1f;
                    if (rows[i].root != null) rows[i].root.transform.localScale = Vector3.one;
                }
                else
                {
                    EvaluateRowPulse(i);
                }
            }
        }

        private void EvaluateRowPulse(int index)
        {
            if (index < 0 || index >= rows.Count || rows[index].root == null) return;
            float t = _rowPulseDuration <= 0f ? 1f : Mathf.Clamp01(_rowPulseAges[index] / _rowPulseDuration);
            float scale = ScoreFeedbackController.EvaluateCounterPulse(t, 1f, _rowPulseScale);
            rows[index].root.transform.localScale = Vector3.one * scale;
        }
    }
}
