using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class LevelRulesUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text movesLabel;
        [SerializeField] private TMP_Text previewLabel;
        [SerializeField] private Button previewButton;
        [SerializeField] private GameObject winPanel;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button winMenuButton;
        [SerializeField] private GameObject lossPanel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button lossMenuButton;
        [SerializeField] private Button buyExtraMovesButton;
        [SerializeField] private StoreOfferDefinition extraMovesOffer;
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private TMP_Text tutorialLabel;
        [SerializeField] private Button tutorialNextButton;

        private LevelSession session;
        private GameController game;
        private int tutorialStep;

        private void Awake()
        {
            if (!LevelFlow.IsLevelMode) gameObject.SetActive(false);
        }

        public void Bind(LevelSession levelSession, GameController gameController)
        {
            session = levelSession;
            game = gameController;
            previewButton.onClick.AddListener(ActivatePreview);
            nextLevelButton.onClick.AddListener(NextLevel);
            winMenuButton.onClick.AddListener(session.ReturnToMenu);
            retryButton.onClick.AddListener(session.Retry);
            lossMenuButton.onClick.AddListener(session.ReturnToMenu);
            if (buyExtraMovesButton != null) buyExtraMovesButton.onClick.AddListener(BuyExtraMoves);
            if (tutorialNextButton != null) tutorialNextButton.onClick.AddListener(AdvanceTutorial);
            winPanel.SetActive(false);
            lossPanel.SetActive(false);
            if (buyExtraMovesButton != null)
                buyExtraMovesButton.gameObject.SetActive(extraMovesOffer != null &&
                                                         extraMovesOffer.extraMovesOnPurchase > 0 && !string.IsNullOrWhiteSpace(extraMovesOffer.productId));
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
            Refresh();
            if (ShouldShowTutorial()) ShowTutorialStep();
        }

        public void Refresh()
        {
            if (session == null || game == null) return;
            movesLabel.text = session.Definition.moveLimit > 0
                ? "MOVES: " + session.MovesRemaining
                : "MOVES: ∞";
            previewButton.interactable = !game.PreviewUsed &&
                                         game.state == GameController.GameState.play;
            previewLabel.text = game.PreviewTurnsRemaining > 0
                ? "SHOW 3: " + game.PreviewTurnsRemaining + " TURNS"
                : game.PreviewUsed ? "SHOW 3: USED" : "SHOW 3: 3 TURNS";
        }

        public void ShowWin()
        {
            winPanel.SetActive(true);
            game.playPanel.SetActive(false);
            nextLevelButton.interactable = LevelCatalog.Next(session.Definition.levelNumber) != null;
            Refresh();
        }

        public void ShowLoss()
        {
            lossPanel.SetActive(true);
            game.playPanel.SetActive(false);
            if (buyExtraMovesButton != null)
                buyExtraMovesButton.gameObject.SetActive(
                    session.PendingFailedObjective == "moves_limit" && extraMovesOffer != null &&
                    extraMovesOffer.extraMovesOnPurchase > 0 && !string.IsNullOrWhiteSpace(extraMovesOffer.productId));
            Refresh();
        }

        public void HideLoss()
        {
            lossPanel.SetActive(false);
            game.playPanel.SetActive(true);
        }

        private void BuyExtraMoves()
        {
            if (session == null || !session.AwaitingFailureDecision || extraMovesOffer == null ||
                extraMovesOffer.extraMovesOnPurchase <= 0) return;
            LevelAnalytics.CheckoutStarted(extraMovesOffer.productId, extraMovesOffer.priceValue, extraMovesOffer.currencyCode);
            PowerUpStore.RequestPurchase(extraMovesOffer.productId);
        }

        private void ActivatePreview()
        {
            if (game.ActivatePreview()) Refresh();
        }

        private void NextLevel()
        {
            LevelDefinition next = LevelCatalog.Next(session.Definition.levelNumber);
            if (next != null) LevelFlow.StartLevel(next.levelNumber);
        }

        private bool ShouldShowTutorial()
        {
            return session.Definition.levelNumber == 1 &&
                   !session.Progress.firstLevelTutorialComplete &&
                   session.Definition.tutorialSteps != null && session.Definition.tutorialSteps.Count > 0 &&
                   tutorialPanel != null && tutorialLabel != null && tutorialNextButton != null;
        }

        private void ShowTutorialStep()
        {
            tutorialPanel.SetActive(true);
            game.playPanel.SetActive(false);
            tutorialLabel.text = session.Definition.tutorialSteps[tutorialStep] +
                                 "\n\n" + (tutorialStep + 1) + " / " + session.Definition.tutorialSteps.Count;
            TMP_Text buttonLabel = tutorialNextButton.GetComponentInChildren<TMP_Text>();
            if (buttonLabel != null)
                buttonLabel.text = tutorialStep == session.Definition.tutorialSteps.Count - 1 ? "START" : "CONTINUE";
        }

        private void AdvanceTutorial()
        {
            tutorialStep++;
            if (tutorialStep < session.Definition.tutorialSteps.Count)
            {
                ShowTutorialStep();
                return;
            }

            tutorialPanel.SetActive(false);
            session.CompleteFirstLevelTutorial();
            game.state = GameController.GameState.play;
            game.playPanel.SetActive(true);
            Refresh();
        }
    }
}
