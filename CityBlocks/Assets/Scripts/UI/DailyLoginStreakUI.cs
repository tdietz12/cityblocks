using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class DailyLoginStreakUI : MonoBehaviour
    {
        [SerializeField] private Button openButton;
        [SerializeField] private GameObject panel;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button claimButton;
        [SerializeField] private Button reminderButton;
        [SerializeField] private TMP_Text streakLabel;
        [SerializeField] private TMP_Text rewardLabel;
        [SerializeField] private TMP_Text feedbackLabel;

        private void Start()
        {
            if (openButton != null) openButton.onClick.AddListener(Toggle);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (claimButton != null) claimButton.onClick.AddListener(Claim);
            if (reminderButton != null) reminderButton.onClick.AddListener(ToggleReminder);
            if (panel != null) panel.SetActive(false);
            Refresh();
        }

        private void Toggle()
        {
            panel.SetActive(!panel.activeSelf);
            if (panel.activeSelf) Refresh();
        }

        private void Close()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void Claim()
        {
            if (DailyLoginStreakService.Claim(out int day, out int credits))
            {
                if (feedbackLabel != null) feedbackLabel.text = "DAY " + day + " REWARD CLAIMED  +" + credits;
            }
            else if (feedbackLabel != null)
            {
                feedbackLabel.text = "TODAY'S REWARD IS ALREADY CLAIMED";
            }
            Refresh();
        }

        private void ToggleReminder()
        {
            if (DailyLoginNotification.IsEnabled)
            {
                DailyLoginNotification.Disable();
                if (feedbackLabel != null) feedbackLabel.text = "DAILY REMINDER OFF";
            }
            else
            {
                StartCoroutine(DailyLoginNotification.RequestPermissionAndEnable());
                if (feedbackLabel != null) feedbackLabel.text = "DAILY REMINDER REQUESTED";
            }
            if (panel != null) panel.SetActive(false);
            Refresh();
        }

        private void Update()
        {
            if (panel != null && panel.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void Refresh()
        {
            int day = DailyLoginStreakService.CurrentStreakDay;
            if (streakLabel != null) streakLabel.text = "LOGIN STREAK  " + day + " / 7 DAYS";
            if (rewardLabel != null)
            {
                rewardLabel.fontSize = Mathf.Min(rewardLabel.fontSize, 42);
                rewardLabel.text = DailyLoginStreakService.CanClaim
                    ? "TODAY'S REWARD  +" + DailyLoginStreakService.RewardForDay(day) + " CREDITS"
                    : "TODAY'S REWARD CLAIMED  |  BALANCE " + DailyLoginNotification.RewardCreditBalance;
            }
            if (claimButton != null) claimButton.interactable = DailyLoginStreakService.CanClaim;
            if (reminderButton != null)
            {
                TMP_Text label = reminderButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = DailyLoginNotification.IsEnabled ? "TURN OFF REMINDER" : "ENABLE DAILY REMINDER";
            }
        }
    }
}
