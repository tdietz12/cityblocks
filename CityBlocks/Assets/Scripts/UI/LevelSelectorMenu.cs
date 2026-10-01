using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class LevelSelectorMenu : MonoBehaviour
    {
        private const int LevelsPerPage = 12;
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject overlay;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button[] levelButtons;
        [SerializeField] private TMP_Text pageLabel;
        private int page;

        private void Start()
        {
            if (openButton == null || overlay == null || closeButton == null ||
                previousButton == null || nextButton == null || pageLabel == null ||
                levelButtons == null || levelButtons.Length != LevelsPerPage)
            {
                Debug.LogError("Level selector prefab references are incomplete.", this);
                return;
            }
            openButton.onClick.AddListener(Open);
            closeButton.onClick.AddListener(Close);
            previousButton.onClick.AddListener(() => ChangePage(-1));
            nextButton.onClick.AddListener(() => ChangePage(1));

            LevelProgressData progress = LevelProgressStore.Load();
            if (!progress.firstLevelTutorialComplete && progress.completedLevels.Count == 0)
            {
                int firstLevel = progress.activeRun != null ? progress.activeRun.levelNumber : 1;
                LevelFlow.StartLevel(firstLevel);
            }
            else if (!progress.firstLevelTutorialComplete && progress.completedLevels.Contains(1))
            {
                progress.firstLevelTutorialComplete = true;
                LevelProgressStore.Save(progress);
            }
        }

        private void Open()
        {
            page = 0;
            RenderPage();
            overlay.SetActive(true);
        }

        private void Close() { overlay.SetActive(false); }

        private void ChangePage(int direction)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(LevelCatalog.Levels.Count / (float)LevelsPerPage));
            page = Mathf.Clamp(page + direction, 0, count - 1);
            RenderPage();
        }

        private void RenderPage()
        {
            LevelProgressData progress = LevelProgressStore.Load();
            int start = page * LevelsPerPage;
            int end = Mathf.Min(start + LevelsPerPage, LevelCatalog.Levels.Count);
            for (int slot = 0; slot < levelButtons.Length; slot++)
            {
                Button button = levelButtons[slot];
                button.onClick.RemoveAllListeners();
                int i = start + slot;
                button.gameObject.SetActive(i < end);
                if (i >= end) continue;

                LevelDefinition definition = LevelCatalog.Levels[i];
                int number = definition.levelNumber;
                bool unlocked = progress.IsUnlocked(number);
                bool completed = progress.completedLevels.Contains(number);
                bool active = progress.activeRun != null && progress.activeRun.levelNumber == number;
                string label = number.ToString();
                if (active) label += "\nCONTINUE";
                else if (completed) label += "\nDONE";
                else if (!unlocked) label += "\nLOCKED";
                else if (definition.difficulty == LevelDifficulty.Hard) label += "\nHARD";
                else if (definition.difficulty == LevelDifficulty.VeryHard) label += "\nVERY HARD";
                button.GetComponentInChildren<TMP_Text>().text = label;
                button.interactable = unlocked;
                button.onClick.AddListener(() => LevelFlow.StartLevel(number));
            }
            int pages = Mathf.Max(1, Mathf.CeilToInt(LevelCatalog.Levels.Count / (float)LevelsPerPage));
            pageLabel.text = (page + 1) + " / " + pages;
            previousButton.interactable = page > 0;
            nextButton.interactable = page < pages - 1;
        }
    }
}
