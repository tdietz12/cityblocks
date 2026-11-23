using System;
using CloudServices;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Settings screen (opened by StartPanel ▸ TopBar ▸ Settings) with the Account section: shows how the player is
    /// signed in, links Sign in with Apple, and offers "Switch to that account?" when the Apple ID or this device's
    /// Game Center account already belongs to a different player. Never force-links.
    /// </summary>
    public class AccountSettingsPanel : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Button closeButton;
        [Tooltip("Card that resizes to fit the visible account rows.")]
        [SerializeField] private RectTransform card;
        [Tooltip("Vertical Layout Group holding the account rows (anchored to the card's top).")]
        [SerializeField] private RectTransform content;
        [Tooltip("Space below the last visible row.")]
        [SerializeField, Min(0f)] private float bottomPadding = 24f;

        [Header("Account section")]
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text detailLabel;
        [SerializeField] private UnityEngine.UI.Button appleButton;
        [Tooltip("Shown only when this device's Game Center account belongs to a different player.")]
        [SerializeField] private UnityEngine.UI.Button gameCenterButton;
        [SerializeField] private TMP_Text feedbackLabel;
        [SerializeField] private TMP_Text playerIdLabel;

        [Header("Switch account dialog")]
        [SerializeField] private GameObject confirmDialog;
        [SerializeField] private TMP_Text confirmMessage;
        [SerializeField] private UnityEngine.UI.Button confirmButton;
        [SerializeField] private UnityEngine.UI.Button cancelButton;

        private bool open;
        private bool busy;
        private Func<System.Threading.Tasks.Task<bool>> pendingSwitch;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (appleButton != null) appleButton.onClick.AddListener(OnAppleClicked);
            if (gameCenterButton != null) gameCenterButton.onClick.AddListener(OnGameCenterClicked);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmSwitch);
            if (cancelButton != null) cancelButton.onClick.AddListener(HideConfirm);
            if (panel != null) panel.SetActive(false);
            if (confirmDialog != null) confirmDialog.SetActive(false);
            AccountService.KindChanged += OnKindChanged;
        }

        private void OnDestroy()
        {
            AccountService.KindChanged -= OnKindChanged;
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (appleButton != null) appleButton.onClick.RemoveListener(OnAppleClicked);
            if (gameCenterButton != null) gameCenterButton.onClick.RemoveListener(OnGameCenterClicked);
            if (confirmButton != null) confirmButton.onClick.RemoveListener(OnConfirmSwitch);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(HideConfirm);
        }

        private void Update()
        {
            if (!open || busy || !Input.GetKeyDown(KeyCode.Escape)) return;
            if (confirmDialog != null && confirmDialog.activeSelf) HideConfirm();
            else Close();
        }

        /// <summary>Wired to the Settings button.</summary>
        public void Open()
        {
            if (panel == null || open) return;
            open = true;
            SetFeedback("");
            HideConfirm();
            // Activate first: layout (FitCard) reports zero height for inactive objects.
            UIPanelAnimator.SetAnimatedActive(panel, true);
            Refresh();
        }

        public void Close()
        {
            if (!open || busy) return;
            open = false;
            UIPanelAnimator.SetAnimatedActive(panel, false);
        }

        private void OnKindChanged(string kind)
        {
            if (open) Refresh();
        }

        private void Refresh()
        {
            bool signedIn = CloudBootstrap.IsSignedIn;
            string kind = AccountService.Kind;
            string status;
            string detail;

            if (!signedIn)
            {
                status = "Offline";
                detail = "Connect to the internet to manage your account.";
            }
            else if (kind == AccountService.KindGameCenter)
            {
                string alias = AccountService.GameCenterAlias;
                status = string.IsNullOrEmpty(alias) ? "Signed in with Game Center" : "Signed in with Game Center as " + alias;
                detail = "Your progress is linked to your Game Center account.";
            }
            else if (kind == AccountService.KindApple)
            {
                status = "Signed in with Apple";
                detail = "Your progress is linked to your Apple ID.";
            }
            else
            {
                status = "Playing as Guest";
                detail = "Progress is only saved on this device. Sign in with Apple or turn on Game Center in your iPhone's Settings to keep it safe.";
            }

            if (statusLabel != null) statusLabel.text = status;
            if (detailLabel != null) detailLabel.text = detail;
            if (appleButton != null) appleButton.gameObject.SetActive(signedIn && !AccountService.IsAppleLinked);
            if (gameCenterButton != null) gameCenterButton.gameObject.SetActive(signedIn && AccountService.HasGameCenterConflict);
            if (playerIdLabel != null) playerIdLabel.text = signedIn ? "Player ID: " + AccountService.PlayerId : "";
            SetButtonsInteractable(!busy);
            FitCard();
        }

        /// <summary>Resizes the card so hidden rows (e.g. the Game Center switch button) don't leave a gap.</summary>
        private void FitCard()
        {
            if (card == null || content == null) return;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float rows = UnityEngine.UI.LayoutUtility.GetPreferredHeight(content);
            float top = -content.anchoredPosition.y;
            card.sizeDelta = new Vector2(card.sizeDelta.x, top + rows + bottomPadding);
        }

        private async void OnAppleClicked()
        {
            if (busy) return;
            SetBusy(true, "Signing in...");
            AccountService.LinkResult result = await AccountService.LinkAppleAsync();
            SetBusy(false, "");
            if (this == null) return;

            switch (result)
            {
                case AccountService.LinkResult.Linked:
                    SetFeedback("Apple ID linked.");
                    break;
                case AccountService.LinkResult.AlreadyLinked:
                    ShowConfirm("This Apple ID already has a City Blocks player. Switch to that player?", AccountService.SwitchToAppleAccountAsync);
                    break;
                case AccountService.LinkResult.Unsupported:
                    SetFeedback("Sign in with Apple is only available on iPhone and iPad.");
                    break;
                case AccountService.LinkResult.Failed:
                    SetFeedback("Couldn't sign in. Check your connection and try again.");
                    break;
            }
            Refresh();
        }

        private void OnGameCenterClicked()
        {
            if (busy) return;
            ShowConfirm("Your Game Center account already has a City Blocks player. Switch to that player?", AccountService.SwitchToGameCenterAccountAsync);
        }

        private void ShowConfirm(string question, Func<System.Threading.Tasks.Task<bool>> switchAction)
        {
            pendingSwitch = switchAction;
            bool recoverable = AccountService.IsAppleLinked || AccountService.IsGameCenterLinked;
            string warning = recoverable
                ? "You'll be signed out of your current account."
                : "Your current guest account can't be recovered after switching.";
            if (confirmMessage != null) confirmMessage.text = question + "\n\n" + warning + " Levels and power-ups on this device stay as they are.";
            if (confirmDialog != null) UIPanelAnimator.SetAnimatedActive(confirmDialog, true);
        }

        private void HideConfirm()
        {
            if (busy) return;
            pendingSwitch = null;
            if (confirmDialog != null && confirmDialog.activeSelf) UIPanelAnimator.SetAnimatedActive(confirmDialog, false);
        }

        private async void OnConfirmSwitch()
        {
            if (busy || pendingSwitch == null) return;
            Func<System.Threading.Tasks.Task<bool>> action = pendingSwitch;
            pendingSwitch = null;
            if (confirmDialog != null) UIPanelAnimator.SetAnimatedActive(confirmDialog, false);

            SetBusy(true, "Switching account...");
            bool switched = await action();
            SetBusy(false, switched ? "Switched account." : "Couldn't switch account. Try again later.");
            if (this == null) return;
            Refresh();
        }

        private void SetBusy(bool value, string message)
        {
            busy = value;
            SetFeedback(message);
            SetButtonsInteractable(!value);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (appleButton != null) appleButton.interactable = interactable;
            if (gameCenterButton != null) gameCenterButton.interactable = interactable;
            if (closeButton != null) closeButton.interactable = interactable;
            if (confirmButton != null) confirmButton.interactable = interactable;
            if (cancelButton != null) cancelButton.interactable = interactable;
        }

        private void SetFeedback(string message)
        {
            if (feedbackLabel != null) feedbackLabel.text = message;
        }
    }
}
