using CloudServices;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Main-menu panel offered once when the player has no linked account and Game Center didn't sign in
    /// (cancelled or signed out on the device). "Sign in with Apple" links the Apple ID to the current player;
    /// "Play as Guest" keeps the anonymous player. After either choice it never shows again.
    /// Waits behind the daily reward popup so the two never overlap.
    /// </summary>
    public class SignInPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Button appleButton;
        [SerializeField] private UnityEngine.UI.Button guestButton;
        [SerializeField] private TMP_Text feedbackLabel;
        [Tooltip("Daily reward popup on the same menu; this panel waits until it has closed.")]
        [SerializeField] private DailyLoginStreakUI dailyReward;

        [Header("Timing")]
        [Tooltip("Seconds between checks for whether the panel should open.")]
        [SerializeField, Min(0.1f)] private float checkInterval = 0.5f;
        [Tooltip("Seconds (real time) the menu must be visible before the panel animates in.")]
        [SerializeField, Min(0f)] private float openDelay = 0.6f;

        private bool open;
        private bool busy;
        private float nextCheckAt;
        private float eligibleSince = -1f;

        private void Awake()
        {
            if (appleButton != null) appleButton.onClick.AddListener(OnAppleClicked);
            if (guestButton != null) guestButton.onClick.AddListener(OnGuestClicked);
            if (panel != null) panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (appleButton != null) appleButton.onClick.RemoveListener(OnAppleClicked);
            if (guestButton != null) guestButton.onClick.RemoveListener(OnGuestClicked);
        }

        private void Update()
        {
            if (open || panel == null || Time.unscaledTime < nextCheckAt) return;
            nextCheckAt = Time.unscaledTime + checkInterval;

            bool eligible = AccountService.ShouldOfferSignIn && (dailyReward == null || !dailyReward.IsShowingOrPending);
            if (!eligible) { eligibleSince = -1f; return; }
            if (eligibleSince < 0f) eligibleSince = Time.unscaledTime;
            if (Time.unscaledTime - eligibleSince >= openDelay) Open();
        }

        private void Open()
        {
            open = true;
            SetFeedback("");
            SetButtonsInteractable(true);
            UIPanelAnimator.SetAnimatedActive(panel, true);
        }

        private void Close()
        {
            open = false;
            UIPanelAnimator.SetAnimatedActive(panel, false);
        }

        private async void OnAppleClicked()
        {
            if (busy) return;
            busy = true;
            SetButtonsInteractable(false);
            SetFeedback("Signing in...");

            AccountService.AppleSignInResult result = await AccountService.SignInWithAppleAsync();
            busy = false;
            if (this == null) return;

            switch (result)
            {
                case AccountService.AppleSignInResult.Success:
                    Close();
                    return;
                case AccountService.AppleSignInResult.Cancelled:
                    SetFeedback("");
                    break;
                case AccountService.AppleSignInResult.Unsupported:
                    SetFeedback("Sign in with Apple is only available on iPhone and iPad.");
                    break;
                default:
                    SetFeedback("Couldn't sign in. Check your connection and try again.");
                    break;
            }
            SetButtonsInteractable(true);
        }

        private void OnGuestClicked()
        {
            if (busy || !open) return;
            AccountService.ContinueAsGuest();
            Close();
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (appleButton != null) appleButton.interactable = interactable;
            if (guestButton != null) guestButton.interactable = interactable;
        }

        private void SetFeedback(string message)
        {
            if (feedbackLabel != null) feedbackLabel.text = message;
        }
    }
}
