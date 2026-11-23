using System;
using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Daily login reward modal. Opens automatically on the main menu once per day: today's reward is claimed and its
    /// power-ups are added to the inventory as the modal opens, so COLLECT and closing both keep the reward.
    /// </summary>
    public class DailyLoginStreakUI : MonoBehaviour
    {
        [Serializable]
        public class DayTile
        {
            public Image background;
            public TMP_Text dayLabel;
            public Image rewardIcon;
            public TMP_Text amountLabel;
            public Image checkIcon;
            public Outline todayOutline;
            public Image coinIcon;
            public TMP_Text coinLabel;
        }

        [Header("Panel")]
        [SerializeField] private GameObject panel;
        [FormerlySerializedAs("claimButton")]
        [SerializeField] private Button collectButton;
        [SerializeField] private Button closeButton;

        [Header("Labels")]
        [SerializeField] private TMP_Text streakLabel;
        [SerializeField] private TMP_Text rewardLabel;
        [SerializeField] private TMP_Text feedbackLabel;

        [Header("7-day track")]
        [SerializeField] private DayTile[] dayTiles = new DayTile[DailyLoginStreakService.StreakLength];

        [Header("Reward icons")]
        [SerializeField] private Sprite deleteIcon;
        [SerializeField] private Sprite levelUpIcon;
        [SerializeField] private Sprite lookAheadIcon;
        [Tooltip("Shown when a day grants more than one kind of power-up.")]
        [SerializeField] private Sprite bundleIcon;

        [Header("Tile colors")]
        [SerializeField] private Color collectedColor = new Color(0.831f, 0.643f, 0.141f);
        [SerializeField] private Color upcomingColor = new Color(0.086f, 0.118f, 0.153f);
        [SerializeField] private Color collectedInk = new Color(0.059f, 0.059f, 0.059f);
        [SerializeField] private Color upcomingInk = new Color(0.42f, 0.467f, 0.522f);
        [Tooltip("Color of the coin amount on upcoming tiles.")]
        [SerializeField] private Color coinColor = new Color(0.98f, 0.78f, 0.2f);

        [Header("Login entrance")]
        [Tooltip("Seconds (real time) to wait after the main menu appears before the automatic popup animates in, so scene-load hitches don't skip the animation.")]
        [SerializeField, Min(0f)] private float autoOpenDelay = 0.45f;

        private bool popupOpen;
        private bool autoOpenPending;
        private int shownDay;
        private float openedAt;
        // True while the modal is showing an editor preview (nothing claimed), so closing it sends no analytics.
        private bool previewOnly;

        private void Start()
        {
            if (collectButton != null) collectButton.onClick.AddListener(() => Dismiss("collect"));
            if (closeButton != null) closeButton.onClick.AddListener(() => Dismiss("close"));
if (panel != null && !popupOpen) panel.SetActive(false);
StartCoroutine(AutoOpenAfterDelay());
}

private void OnApplicationPause(bool paused)
{
// Covers midnight passing while the player sits on the main menu with the app in the background.
if (!paused && isActiveAndEnabled) StartCoroutine(AutoOpenAfterDelay());
}

private System.Collections.IEnumerator AutoOpenAfterDelay()
{
if (autoOpenPending || popupOpen) yield break;
autoOpenPending = true;
if (autoOpenDelay > 0f) yield return new WaitForSecondsRealtime(autoOpenDelay);
autoOpenPending = false;
TryAutoOpen();
}

        private void Update()
        {
            if (popupOpen && Input.GetKeyDown(KeyCode.Escape)) Dismiss("close");
        }

        /// <summary>Claims today's reward and opens the modal, if a reward is available and the player isn't being redirected into the first tutorial.</summary>
        /// <summary>True while the popup is open or waiting to auto-open (other menu popups queue behind it).</summary>
        public bool IsShowingOrPending => popupOpen || autoOpenPending;

        public void TryAutoOpen() => Open(false);

#if UNITY_EDITOR
        /// <summary>
        /// Editor test hook: claims today's reward and (re)opens the modal even if it is already showing or the player
        /// is a brand-new tutorial player. Returns false if there was nothing to claim.
        /// </summary>
        public bool DebugForceOpen()
        {
            if (panel == null || !DailyLoginStreakService.CanClaim) return false;
            popupOpen = false;
            previewOnly = false;
            panel.SetActive(false); // so the entrance animation replays
            Open(true);
            return popupOpen;
        }
#endif

        private void Open(bool ignoreFirstLaunch)
        {
            if (popupOpen || panel == null || !DailyLoginStreakService.CanClaim) return;
            if (!ignoreFirstLaunch && IsFirstLaunchRedirect()) return;
            if (!DailyLoginStreakService.Claim(out int day, out PowerUpInventory reward)) return;

            shownDay = day;
            Populate(day, reward);
            popupOpen = true;
            openedAt = Time.realtimeSinceStartup;
            UIPanelAnimator.SetAnimatedActive(panel, true);
        }

        private static bool IsFirstLaunchRedirect()
        {
            // Matches LevelButton_MainMenu: a brand-new player is sent straight into the Level 1 tutorial.
            LevelProgressData progress = LevelProgressStore.Load();
            return !progress.levelTutorialComplete && progress.completedLevels.Count == 0;
        }

        private void Dismiss(string method)
        {
            if (!popupOpen) return;
            popupOpen = false;
            if (!previewOnly) LevelAnalytics.DailyRewardPopupShown(shownDay, method, Mathf.RoundToInt(Time.realtimeSinceStartup - openedAt));
            previewOnly = false;
            UIPanelAnimator.SetAnimatedActive(panel, false);
        }

        private void Populate(int day, PowerUpInventory reward)
        {
            if (streakLabel != null) streakLabel.text = "LOGIN STREAK  " + day + " / " + DailyLoginStreakService.StreakLength + " DAYS";
            if (rewardLabel != null) rewardLabel.text = "+" + Describe(reward, DailyLoginStreakService.CoinsForDay(day));
            if (feedbackLabel != null)
            {
                int tomorrow = Mathf.Min(DailyLoginStreakService.StreakLength, day + 1);
                feedbackLabel.text = "Come back tomorrow for " +
                    Describe(DailyLoginStreakService.RewardForDay(tomorrow), DailyLoginStreakService.CoinsForDay(tomorrow)).ToLowerInvariant();
            }
            for (int i = 0; i < dayTiles.Length; i++) PopulateTile(dayTiles[i], i + 1, day);
        }

        private void PopulateTile(DayTile tile, int tileDay, int today)
        {
            if (tile == null) return;
            bool collected = tileDay <= today;
            bool isToday = tileDay == today;
            PowerUpInventory reward = DailyLoginStreakService.RewardForDay(tileDay);
            Color ink = collected ? collectedInk : upcomingInk;

            if (tile.background != null) tile.background.color = collected ? collectedColor : upcomingColor;
            if (tile.todayOutline != null) tile.todayOutline.enabled = isToday;
            if (tile.dayLabel != null) { tile.dayLabel.text = isToday ? "TODAY" : "DAY " + tileDay; tile.dayLabel.color = ink; }
            if (tile.checkIcon != null) { tile.checkIcon.gameObject.SetActive(collected); tile.checkIcon.color = ink; }
            if (tile.rewardIcon != null)
            {
                tile.rewardIcon.gameObject.SetActive(!collected);
                tile.rewardIcon.sprite = IconFor(reward);
                tile.rewardIcon.color = Color.white;
            }
            if (tile.amountLabel != null)
            {
                tile.amountLabel.gameObject.SetActive(!collected);
                tile.amountLabel.text = "x" + DailyLoginStreakService.TotalUses(reward);
                tile.amountLabel.color = ink;
            }
            if (tile.coinIcon != null)
            {
                tile.coinIcon.gameObject.SetActive(!collected);
                tile.coinIcon.color = coinColor;
            }
            if (tile.coinLabel != null)
            {
                tile.coinLabel.gameObject.SetActive(!collected);
                tile.coinLabel.text = DailyLoginStreakService.CoinsForDay(tileDay).ToString();
                tile.coinLabel.color = coinColor;
            }
        }

        private Sprite IconFor(PowerUpInventory reward)
        {
            if (KindCount(reward) > 1) return bundleIcon;
            if (reward.deleteUses > 0) return deleteIcon;
            if (reward.levelUpUses > 0) return levelUpIcon;
            if (reward.deleteRowUses > 0) return lookAheadIcon;
            return bundleIcon;
        }

        private static int KindCount(PowerUpInventory reward)
        {
            int kinds = 0;
            if (reward.deleteUses > 0) kinds++;
            if (reward.levelUpUses > 0) kinds++;
            if (reward.deleteRowUses > 0) kinds++;
            if (reward.extraTurnsUses > 0) kinds++;
            return kinds;
        }

        /// <summary>Player-facing reward text, e.g. "2 DELETE", "1 DELETE + 1 LEVEL UP" or "2 OF EACH POWER-UP".</summary>
        public static string Describe(PowerUpInventory reward)
        {
            if (reward == null) return "";
            if (reward.deleteUses > 0 && reward.deleteUses == reward.levelUpUses && reward.levelUpUses == reward.deleteRowUses)
                return reward.deleteUses + " OF EACH POWER-UP";

            string text = "";
            if (reward.deleteUses > 0) text = Join(text, reward.deleteUses + " DELETE");
            if (reward.levelUpUses > 0) text = Join(text, reward.levelUpUses + " LEVEL UP");
            if (reward.deleteRowUses > 0) text = Join(text, reward.deleteRowUses + " LOOK AHEAD");
            if (reward.extraTurnsUses > 0) text = Join(text, reward.extraTurnsUses + " EXTRA TURNS");
            return text;
        }

        /// <summary>Reward text including coins, e.g. "150 COINS + 1 LEVEL UP".</summary>
        public static string Describe(PowerUpInventory reward, int coins)
        {
            string powerUps = Describe(reward);
            if (coins <= 0) return powerUps;
            return Join(coins + " COINS", powerUps);
        }

        private static string Join(string a, string b) =>
            string.IsNullOrEmpty(a) ? b : string.IsNullOrEmpty(b) ? a : a + " + " + b;
    }
}
