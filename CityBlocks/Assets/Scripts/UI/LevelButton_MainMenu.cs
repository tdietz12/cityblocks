using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class LevelButton_MainMenu : MonoBehaviour
    {
        private Button button;
        private TMP_Text label;

        private void Awake()
        {
            button = GetComponent<Button>();
            label = GetComponentInChildren<TMP_Text>();
        }

        private void Start()
        {
            if (button == null) button = GetComponent<Button>();
            if (label == null) label = GetComponentInChildren<TMP_Text>();
            if (button != null) button.onClick.AddListener(ButtonPress);

            LevelProgressData progress = LevelProgressStore.Load();
            if (!progress.levelTutorialComplete && progress.completedLevels.Count == 0)
            {
                int firstLevel = progress.activeRun != null ? progress.activeRun.levelNumber : 1;
                LevelFlow.StartLevel(firstLevel);
                return;
            }
            else if (!progress.levelTutorialComplete && progress.completedLevels.Contains(1))
            {
                progress.levelTutorialComplete = true;
                LevelProgressStore.Save(progress);
            }

            RefreshButton();
        }

        private void OnEnable()
        {
            if (button == null) button = GetComponent<Button>();
            if (label == null) label = GetComponentInChildren<TMP_Text>();
            RefreshButton();
        }

        public void RefreshButton()
        {
            LevelProgressData progress = LevelProgressStore.Load();
            int target = GetLatestUnlockedLevel(progress);

            bool active = progress.activeRun != null && progress.activeRun.levelNumber == target;
            bool completed = progress.completedLevels.Contains(target);
            LevelDefinition def = LevelCatalog.Get(target);

            string text = "LEVEL " + target;
            if (active)
            {
                text += "\nCONTINUE";
            }
            else if (completed)
            {
                text += "\nDONE";
            }
            else if (def != null && def.difficulty == LevelDifficulty.Hard)
            {
                text += "\nHARD";
            }
            else if (def != null && def.difficulty == LevelDifficulty.VeryHard)
            {
                text += "\nVERY HARD";
            }

            if (label != null)
                label.text = text;

            if (button != null)
                button.interactable = progress.IsUnlocked(target);
        }

        public static int GetLatestUnlockedLevel(LevelProgressData progress)
        {
            if (progress == null) return 1;
            int target = progress.highestUnlockedLevel;
            if (progress.activeRun != null && progress.activeRun.levelNumber > 0)
            {
                target = progress.activeRun.levelNumber;
            }

            if (LevelCatalog.Levels != null && LevelCatalog.Levels.Count > 0)
            {
                int minLevel = LevelCatalog.Levels[0].levelNumber;
                int maxLevel = LevelCatalog.Levels[LevelCatalog.Levels.Count - 1].levelNumber;
                target = Mathf.Clamp(target, minLevel, maxLevel);
            }
            return Mathf.Max(1, target);
        }

        public void ButtonPress()
        {
            LevelProgressData progress = LevelProgressStore.Load();
            int target = GetLatestUnlockedLevel(progress);
            LevelFlow.StartLevel(target);
        }
    }
}
