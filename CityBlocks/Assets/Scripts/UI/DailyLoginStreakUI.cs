using System;
using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Daily login reward modal. Checks server eligibility on the main menu and displays confirmed cloud rewards.
    /// COLLECT and closing both keep an already granted reward. Offline/pending states never grant locally.
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
        [SerializeField] private DailyRewardSchedule rewardSchedule;
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

        private bool cloudRequestRunning;
        private bool claimConfirmed;
        private RewardAmounts[] serverSchedule;
        private TMP_Text collectLabel;
        private int requestGeneration;

        private void Start()
        {
            if (collectButton != null)
            {
                collectLabel = collectButton.GetComponentInChildren<TMP_Text>(true);
                collectButton.onClick.AddListener(OnCollect);
            }
            if (closeButton != null) closeButton.onClick.AddListener(OnClose);
            if (panel != null && !popupOpen) panel.SetActive(false);
            StartCoroutine(AutoOpenAfterDelay());
        }

        private void OnEnable()
        {
            CloudServices.ConnectivityService.OnlineChanged += OnOnlineChanged;
            CloudServices.AccountService.SessionChanged += OnSessionChanged;
        }
        private void OnDisable()
        {
            CloudServices.ConnectivityService.OnlineChanged -= OnOnlineChanged;
            CloudServices.AccountService.SessionChanged -= OnSessionChanged;
            requestGeneration++;
            autoOpenPending = false;
        }
        private void OnSessionChanged(string playerId)
        {
            requestGeneration++;
            popupOpen = false;
            claimConfirmed = false;
            previewOnly = false;
            serverSchedule = null;
            if (panel != null) UIPanelAnimator.SetAnimatedActive(panel, false);
            if (!string.IsNullOrEmpty(playerId)) StartCoroutine(CheckAfterSessionChange());
        }
        private System.Collections.IEnumerator CheckAfterSessionChange()
        {
            while (cloudRequestRunning) yield return null;
            if (isActiveAndEnabled && !string.IsNullOrEmpty(CloudServices.AccountService.PlayerId))
                yield return AutoOpenAfterDelay();
        }
private void OnDestroy()
        {
            if (collectButton != null) collectButton.onClick.RemoveListener(OnCollect);
            if (closeButton != null) closeButton.onClick.RemoveListener(OnClose);
        }
        private void OnOnlineChanged(bool online)
        {
            if (online && isActiveAndEnabled && !claimConfirmed) TryAutoOpen();
        }
        private void OnApplicationPause(bool paused)
        {
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
        public bool IsShowingOrPending => popupOpen || autoOpenPending || cloudRequestRunning;
        public void TryAutoOpen() => Open(false);
        private void OnCollect()
        {
            if (claimConfirmed || previewOnly) Dismiss("collect");
            else Open(true);
        }
        private void OnClose() => Dismiss("close");

#if UNITY_EDITOR
        /// <summary>Visual preview only. It never bypasses server eligibility or grants inventory.</summary>
        public bool DebugForceOpen()
        {
            if (panel == null || cloudRequestRunning) return false;
            panel.SetActive(false);
            previewOnly = true;
            claimConfirmed = true;
            shownDay = DailyLoginStreakService.CurrentStreakDay;
            Populate(shownDay, RewardForDisplay(shownDay).ToPowerUps(), RewardForDisplay(shownDay).coins);
            if (feedbackLabel != null) feedbackLabel.text = "EDITOR PREVIEW — NO REWARD GRANTED";
            if (collectLabel != null) collectLabel.text = "CLOSE PREVIEW";
            popupOpen = true;
            openedAt = Time.realtimeSinceStartup;
            UIPanelAnimator.SetAnimatedActive(panel, true);
            return true;
        }
#endif

        private async void Open(bool ignoreFirstLaunch)
        {
            if (cloudRequestRunning || panel == null || (popupOpen && claimConfirmed)) return;
            if (!ignoreFirstLaunch && IsFirstLaunchRedirect()) return;
            cloudRequestRunning = true;
            int generation = requestGeneration;
            long revision = CloudServices.AccountService.SessionRevision;
            previewOnly = false;
            if (collectButton != null) collectButton.interactable = false;
            try
            {
                var response = await CloudServices.CloudDailyRewards.CheckAndClaimAsync();
                if (this == null || !isActiveAndEnabled || generation != requestGeneration ||
                    revision != CloudServices.AccountService.SessionRevision) return;
                var cached = response ?? CloudServices.CloudDailyRewards.Cached;
                if (cached != null && cached.Schedule != null && cached.Schedule.Length == 7)
                    serverSchedule = cached.Schedule;
                if (response != null && response.Status == "already_claimed")
                {
                    // Another request/device may have claimed it. Never claim or add inventory locally.
                    if (popupOpen) Dismiss("close");
                    return;
                }
                shownDay = Mathf.Clamp(cached != null ? cached.StreakDay : 1, 1, 7);
                claimConfirmed = response != null && response.Status == "applied";
                var reward = response != null && response.Reward != null ? response.Reward : RewardForDisplay(shownDay);
                Populate(shownDay, reward.ToPowerUps(), reward.coins);
                if (!claimConfirmed)
                {
                    if (rewardLabel != null) rewardLabel.text = response != null && response.Status == "pending" ? "REWARD PROCESSING" : "CONNECT TO CLAIM";
                    if (feedbackLabel != null) feedbackLabel.text = response != null && response.Status == "pending"
                        ? "Your reward is pending. No extra claim will be made." : "Connect to the internet, then retry.";
                }
                if (collectLabel != null) collectLabel.text = claimConfirmed ? "COLLECT" : "RETRY";
                if (!popupOpen)
                {
                    popupOpen = true;
                    openedAt = Time.realtimeSinceStartup;
                    UIPanelAnimator.SetAnimatedActive(panel, true);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[DailyRewards] Popup check failed: " + exception.Message);
                if (this != null && isActiveAndEnabled && generation == requestGeneration &&
                    revision == CloudServices.AccountService.SessionRevision)
                {
                    claimConfirmed = false;
                    shownDay = 1;
                    var reward = RewardForDisplay(shownDay);
                    Populate(shownDay, reward.ToPowerUps(), reward.coins);
                    if (rewardLabel != null) rewardLabel.text = "CONNECT TO CLAIM";
                    if (feedbackLabel != null) feedbackLabel.text = "Couldn't claim. Check your connection and retry.";
                    if (collectLabel != null) collectLabel.text = "RETRY";
                    popupOpen = true;
                    openedAt = Time.realtimeSinceStartup;
                    UIPanelAnimator.SetAnimatedActive(panel, true);
                }
            }
            finally
            {
                cloudRequestRunning = false;
                if (this != null && collectButton != null) collectButton.interactable = true;
            }
        }

        private RewardAmounts RewardForDisplay(int day)
        {
            int index = Mathf.Clamp(day, 1, 7) - 1;
            if (serverSchedule != null && serverSchedule.Length == 7 && serverSchedule[index] != null) return serverSchedule[index];
            if (rewardSchedule != null && rewardSchedule.IsValid) return rewardSchedule.ForDay(day);
            var old = DailyLoginStreakService.RewardForDay(day);
            return new RewardAmounts { coins = DailyLoginStreakService.CoinsForDay(day), deleteUses = old.deleteUses,
                levelUpUses = old.levelUpUses, lookAheadUses = old.deleteRowUses, extraTurnsUses = old.extraTurnsUses };
        }

        private static bool IsFirstLaunchRedirect()
        {
            // Matches LevelButton_MainMenu: a brand-new player is sent straight into the Level 1 tutorial.
            LevelProgressData progress = LevelProgressStore.Load();
            return !progress.levelTutorialComplete && progress.completedLevels.Count == 0;
        }

        private void Dismiss(string method)
        {
            requestGeneration++;
            if (!popupOpen) return;
            popupOpen = false;
            if (!previewOnly) LevelAnalytics.DailyRewardPopupShown(shownDay, method, Mathf.RoundToInt(Time.realtimeSinceStartup - openedAt));
            previewOnly = false;
            claimConfirmed = false;
            UIPanelAnimator.SetAnimatedActive(panel, false);
        }

        private void Populate(int day, PowerUpInventory reward, int coins)
        {
            if (streakLabel != null) streakLabel.text = "LOGIN STREAK  " + day + " / " + DailyLoginStreakService.StreakLength + " DAYS";
            if (rewardLabel != null) rewardLabel.text = "+" + Describe(reward, coins);
            if (feedbackLabel != null)
            {
                int tomorrow = Mathf.Min(DailyLoginStreakService.StreakLength, day + 1);
                var tomorrowReward = RewardForDisplay(tomorrow);
                feedbackLabel.text = "Resets at 00:00 UTC. Next: " + Describe(tomorrowReward.ToPowerUps(), tomorrowReward.coins).ToLowerInvariant();
            }
            for (int i = 0; i < dayTiles.Length; i++) PopulateTile(dayTiles[i], i + 1, day);
        }

        private void PopulateTile(DayTile tile, int tileDay, int today)
        {
            if (tile == null) return;
            bool collected = tileDay < today || (tileDay == today && (claimConfirmed || previewOnly));
            bool isToday = tileDay == today;
            RewardAmounts configured = RewardForDisplay(tileDay);
            PowerUpInventory reward = configured.ToPowerUps();
            bool hasPowerUps = DailyLoginStreakService.TotalUses(reward) > 0;
            Color ink = collected ? collectedInk : upcomingInk;

            if (tile.background != null) tile.background.color = collected ? collectedColor : upcomingColor;
            if (tile.todayOutline != null) tile.todayOutline.enabled = isToday;
            if (tile.dayLabel != null) { tile.dayLabel.text = isToday ? "TODAY" : "DAY " + tileDay; tile.dayLabel.color = ink; }
            if (tile.checkIcon != null) { tile.checkIcon.gameObject.SetActive(collected); tile.checkIcon.color = ink; }
            if (tile.rewardIcon != null)
            {
                tile.rewardIcon.gameObject.SetActive(!collected && hasPowerUps);
                tile.rewardIcon.sprite = IconFor(reward);
                tile.rewardIcon.color = Color.white;
            }
            if (tile.amountLabel != null)
            {
                tile.amountLabel.gameObject.SetActive(!collected && hasPowerUps);
                tile.amountLabel.text = "x" + DailyLoginStreakService.TotalUses(reward);
                tile.amountLabel.color = ink;
            }
            if (tile.coinIcon != null)
            {
                tile.coinIcon.gameObject.SetActive(!collected && configured.coins > 0);
                tile.coinIcon.color = coinColor;
            }
            if (tile.coinLabel != null)
            {
                tile.coinLabel.gameObject.SetActive(!collected && configured.coins > 0);
                tile.coinLabel.text = configured.coins.ToString();
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
            if (reward.extraTurnsUses == 0 && reward.deleteUses > 0 && reward.deleteUses == reward.levelUpUses && reward.levelUpUses == reward.deleteRowUses)
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
