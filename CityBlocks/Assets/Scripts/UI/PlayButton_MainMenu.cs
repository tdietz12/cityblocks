using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    public class PlayButton_MainMenu : MonoBehaviour
    {
        [Min(1)] public int endlessUnlockLevel = 3;

        private Button button;
        private TMP_Text label;

        private void Start()
        {
            button = GetComponent<Button>();
            label = GetComponentInChildren<TMP_Text>();
            if (button != null) button.onClick.AddListener(ButtonPress);
            RefreshUnlockState();
        }

        private void OnEnable()
        {
            if (button == null) button = GetComponent<Button>();
            if (label == null) label = GetComponentInChildren<TMP_Text>();
            RefreshUnlockState();
        }

        private void RefreshUnlockState()
        {
            if (button == null) return;
            bool unlocked = LevelProgressStore.Load().completedLevels.Contains(endlessUnlockLevel);
            button.interactable = unlocked;
            if (label != null)
                label.text = unlocked ? "ENDLESS" : "ENDLESS\nCLEAR LEVEL " + endlessUnlockLevel;
        }

        public void ButtonPress()
        {
            if (!LevelProgressStore.Load().completedLevels.Contains(endlessUnlockLevel)) return;
            SceneManager.LoadScene("EndlessMode");
        }
    }
}
