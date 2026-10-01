using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    public class LevelRulesUI : MonoBehaviour
    {
        public static LevelRulesUI Instance { get; private set; }

        [Header("Header / Score HUD")]
        [SerializeField] private GameObject scorePanel;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text recordScoreText;
        [SerializeField] private TMP_Text movesLabel;

        [Header("Play HUD")]
        [SerializeField] private GameObject playPanel;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button deletePowerUpButton;
        [SerializeField] private TMP_Text deletePowerUpLabel;
        [SerializeField] private Button levelUpPowerUpButton;
        [SerializeField] private TMP_Text levelUpPowerUpLabel;
        [SerializeField] private Button lookAheadPowerUpButton;
        [SerializeField] private TMP_Text lookAheadPowerUpLabel;
        [SerializeField] private Button previewButton;
        [SerializeField] private TMP_Text previewLabel;

        [Header("Start / Pause Menu")]
        [SerializeField] private GameObject startPanel;
        [SerializeField] private Button playButton;
        [SerializeField] private TMP_Text playButtonLabel;
        [SerializeField] private Button startBackButton;

        [Header("Power Up Prompts")]
        [SerializeField] private GameObject powerUpPanel_Delete;
        [SerializeField] private GameObject powerUpPanel_LookAhead;
        [SerializeField] private GameObject powerUpPanel_LevelUp;

        [Header("Endless Panels")]
        [SerializeField] private GameObject levelUpPanel;
        [SerializeField] private Button continueLevelUpButton;
        [SerializeField] private GameObject endlessLossPanel;
        [SerializeField] private Button endlessLossBackButton;

        [Header("Win / Loss Panels")]
        [SerializeField] private GameObject winPanel;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button winMenuButton;
        [SerializeField] private GameObject lossPanel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button lossMenuButton;
        [SerializeField] private Button buyExtraMovesButton;
        [SerializeField] private StoreOfferDefinition extraMovesOffer;

        [Header("Tutorial")]
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private TMP_Text tutorialLabel;
        [SerializeField] private Button tutorialNextButton;

        private LevelSession session;
        private GameController game;
        private int tutorialStep;
        private bool isBound;
        private bool listenersRegistered;

        public bool IsLevelMode => session != null || LevelFlow.IsLevelMode;
        public LevelSession Session => session;
        public GameController Game => game;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (game == null)
            {
                game = GameController.instance ?? FindAnyObjectByType<GameController>();
            }

            RegisterListeners();

            if (!LevelFlow.IsLevelMode)
            {
                InitializeEndlessMode();
            }
            else if (session == null && LevelFlow.ActiveSession != null)
            {
                Bind(LevelFlow.ActiveSession, game);
            }
        }

        public void InitializeEndlessMode()
        {
            session = null;
            if (game == null) game = GameController.instance ?? FindAnyObjectByType<GameController>();

            if (movesLabel != null) movesLabel.gameObject.SetActive(false);
            if (previewButton != null) previewButton.gameObject.SetActive(false);
            if (winPanel != null) winPanel.SetActive(false);
            if (lossPanel != null) lossPanel.SetActive(false);
            if (endlessLossPanel != null) endlessLossPanel.SetActive(false);
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
            if (levelUpPanel != null) levelUpPanel.SetActive(false);
            if (powerUpPanel_Delete != null) powerUpPanel_Delete.SetActive(false);
            if (powerUpPanel_LookAhead != null) powerUpPanel_LookAhead.SetActive(false);
            if (powerUpPanel_LevelUp != null) powerUpPanel_LevelUp.SetActive(false);

            if (scorePanel != null) scorePanel.SetActive(true);

            if (game != null && game.state == GameController.GameState.play)
            {
                if (startPanel != null) startPanel.SetActive(false);
                if (playPanel != null) playPanel.SetActive(true);
            }
            else
            {
                if (startPanel != null) startPanel.SetActive(true);
                if (playPanel != null) playPanel.SetActive(false);
            }

            if (playButtonLabel != null) playButtonLabel.text = "PLAY";

            isBound = true;
            Refresh();
        }

        public void Bind(LevelSession levelSession, GameController gameController)
        {
            session = levelSession;
            game = gameController;

            RegisterListeners();

            if (movesLabel != null) movesLabel.gameObject.SetActive(true);
            if (previewButton != null) previewButton.gameObject.SetActive(true);
            if (winPanel != null) winPanel.SetActive(false);
            if (lossPanel != null) lossPanel.SetActive(false);
            if (endlessLossPanel != null) endlessLossPanel.SetActive(false);
            if (levelUpPanel != null) levelUpPanel.SetActive(false);
            if (powerUpPanel_Delete != null) powerUpPanel_Delete.SetActive(false);
            if (powerUpPanel_LookAhead != null) powerUpPanel_LookAhead.SetActive(false);
            if (powerUpPanel_LevelUp != null) powerUpPanel_LevelUp.SetActive(false);

            if (buyExtraMovesButton != null)
                buyExtraMovesButton.gameObject.SetActive(extraMovesOffer != null &&
                                                         extraMovesOffer.extraMovesOnPurchase > 0 && !string.IsNullOrWhiteSpace(extraMovesOffer.productId));
            if (tutorialPanel != null) tutorialPanel.SetActive(false);

            if (scorePanel != null) scorePanel.SetActive(true);

            if (ShouldShowTutorial())
            {
                if (startPanel != null) startPanel.SetActive(false);
                if (playPanel != null) playPanel.SetActive(false);
                ShowTutorialStep();
            }
            else
            {
                if (game != null && game.state == GameController.GameState.play)
                {
                    if (startPanel != null) startPanel.SetActive(false);
                    if (playPanel != null) playPanel.SetActive(true);
                }
                else
                {
                    if (startPanel != null) startPanel.SetActive(true);
                    if (playPanel != null) playPanel.SetActive(false);
                }
            }

            if (playButtonLabel != null) playButtonLabel.text = "PLAY";

            isBound = true;
            Refresh();
        }

        private void RegisterListeners()
        {
            if (listenersRegistered) return;
            listenersRegistered = true;

            if (playButton != null) playButton.onClick.AddListener(StartGame);
            if (startBackButton != null) startBackButton.onClick.AddListener(ReturnToMenu);
            if (pauseButton != null) pauseButton.onClick.AddListener(PauseGame);

            if (deletePowerUpButton != null) deletePowerUpButton.onClick.AddListener(() => TryUsePowerUp(GameController.PowerUpType.delete));
            if (levelUpPowerUpButton != null) levelUpPowerUpButton.onClick.AddListener(() => TryUsePowerUp(GameController.PowerUpType.levelUp));
            if (lookAheadPowerUpButton != null) lookAheadPowerUpButton.onClick.AddListener(() => TryUsePowerUp(GameController.PowerUpType.lookAhead));
            if (previewButton != null) previewButton.onClick.AddListener(ActivatePreview);

            if (continueLevelUpButton != null) continueLevelUpButton.onClick.AddListener(ContinueFromLevelUp);

            if (nextLevelButton != null) nextLevelButton.onClick.AddListener(NextLevel);
            if (winMenuButton != null) winMenuButton.onClick.AddListener(ReturnToMenu);
            if (retryButton != null) retryButton.onClick.AddListener(Retry);
            if (lossMenuButton != null) lossMenuButton.onClick.AddListener(ReturnToMenu);
            if (buyExtraMovesButton != null) buyExtraMovesButton.onClick.AddListener(BuyExtraMoves);
            if (endlessLossBackButton != null) endlessLossBackButton.onClick.AddListener(ReturnToMenu);

            if (tutorialNextButton != null) tutorialNextButton.onClick.AddListener(AdvanceTutorial);
        }

        public void StartGame()
        {
            if (game != null) game.state = GameController.GameState.play;
            if (startPanel != null) startPanel.SetActive(false);
            if (playPanel != null) playPanel.SetActive(true);
            Refresh();
        }

        public void PauseGame()
        {
            if (game != null) game.state = GameController.GameState.start;
            if (playPanel != null) playPanel.SetActive(false);
            if (startPanel != null) startPanel.SetActive(true);
            if (playButtonLabel != null) playButtonLabel.text = "RESUME";
            Refresh();
        }

        public void ContinueFromLevelUp()
        {
            if (levelUpPanel != null) levelUpPanel.SetActive(false);
            if (playPanel != null) playPanel.SetActive(true);
            if (game != null) game.state = GameController.GameState.play;
            Refresh();
        }

        public void TryUsePowerUp(GameController.PowerUpType type)
        {
            if (game == null) return;
            if (game.TryBeginPowerUp(type))
            {
                if (playPanel != null) playPanel.SetActive(false);
                switch (type)
                {
                    case GameController.PowerUpType.delete:
                        if (powerUpPanel_Delete != null) powerUpPanel_Delete.SetActive(true);
                        break;
                    case GameController.PowerUpType.levelUp:
                        if (powerUpPanel_LevelUp != null) powerUpPanel_LevelUp.SetActive(true);
                        break;
                    case GameController.PowerUpType.lookAhead:
                        if (powerUpPanel_LookAhead != null) powerUpPanel_LookAhead.SetActive(true);
                        break;
                }
            }
        }

        public void ReturnToMenu()
        {
            if (session != null)
            {
                session.ReturnToMenu();
            }
            else
            {
                if (game != null) game.ResetValuesOnLoss();
                LevelFlow.GoToMenu();
            }
        }

        public void Retry()
        {
            if (session != null)
            {
                session.Retry();
            }
            else
            {
                if (game != null) game.ResetValuesOnLoss();
                SceneManager.LoadScene("EndlessMode");
            }
        }

        public void Refresh()
        {
            if (game == null) game = GameController.instance ?? FindAnyObjectByType<GameController>();
            if (game == null) return;

            if (scoreText != null) scoreText.text = game.score.ToString();
            if (recordScoreText != null) recordScoreText.text = game.recordScore.ToString();

            if (session != null && movesLabel != null)
            {
                movesLabel.text = session.Definition.moveLimit > 0
                    ? "MOVES: " + session.MovesRemaining
                    : "MOVES: ∞";
            }

            if (previewButton != null)
            {
                if (session != null)
                {
                    previewButton.gameObject.SetActive(true);
                    previewButton.interactable = !game.PreviewUsed &&
                                                 game.state == GameController.GameState.play;
                    if (previewLabel != null)
                    {
                        previewLabel.text = game.PreviewTurnsRemaining > 0
                            ? "SHOW 3: " + game.PreviewTurnsRemaining + " TURNS"
                            : game.PreviewUsed ? "SHOW 3: USED" : "SHOW 3: 3 TURNS";
                    }
                }
                else
                {
                    previewButton.gameObject.SetActive(false);
                }
            }

            RefreshPowerUps();
        }

        private void RefreshPowerUps()
        {
            if (game == null) return;
            bool canUse = game.state == GameController.GameState.play && !game.IsResolvingMove;

            int deleteUses = PowerUpStore.Uses(GameController.PowerUpType.delete);
            if (deletePowerUpButton != null) deletePowerUpButton.interactable = canUse && deleteUses > 0;
            if (deletePowerUpLabel != null) deletePowerUpLabel.text = "DELETE (" + deleteUses + ")";

            int levelUpUses = PowerUpStore.Uses(GameController.PowerUpType.levelUp);
            if (levelUpPowerUpButton != null) levelUpPowerUpButton.interactable = canUse && levelUpUses > 0;
            if (levelUpPowerUpLabel != null) levelUpPowerUpLabel.text = "LVL UP (" + levelUpUses + ")";

            int lookAheadUses = PowerUpStore.Uses(GameController.PowerUpType.lookAhead);
            if (lookAheadPowerUpButton != null) lookAheadPowerUpButton.interactable = canUse && lookAheadUses > 0;
            if (lookAheadPowerUpLabel != null) lookAheadPowerUpLabel.text = "LOOK AHEAD (" + lookAheadUses + ")";
        }

        private void Update()
        {
            RefreshPowerUps();
        }

        public void ShowWin()
        {
            if (winPanel != null) winPanel.SetActive(true);
            if (playPanel != null) playPanel.SetActive(false);
            if (nextLevelButton != null && session != null)
                nextLevelButton.interactable = LevelCatalog.Next(session.Definition.levelNumber) != null;
            Refresh();
        }

        public void ShowLoss()
        {
            if (playPanel != null) playPanel.SetActive(false);
            if (session != null)
            {
                if (lossPanel != null) lossPanel.SetActive(true);
                if (endlessLossPanel != null) endlessLossPanel.SetActive(false);
                if (buyExtraMovesButton != null)
                    buyExtraMovesButton.gameObject.SetActive(
                        session.PendingFailedObjective == "moves_limit" && extraMovesOffer != null &&
                        extraMovesOffer.extraMovesOnPurchase > 0 && !string.IsNullOrWhiteSpace(extraMovesOffer.productId));
            }
            else
            {
                if (endlessLossPanel != null) endlessLossPanel.SetActive(true);
                else if (lossPanel != null)
                {
                    lossPanel.SetActive(true);
                    if (buyExtraMovesButton != null) buyExtraMovesButton.gameObject.SetActive(false);
                }
            }
            Refresh();
        }

        public void HideLoss()
        {
            if (lossPanel != null) lossPanel.SetActive(false);
            if (endlessLossPanel != null) endlessLossPanel.SetActive(false);
            if (playPanel != null) playPanel.SetActive(true);
        }

        private void BuyExtraMoves()
        {
            if (session == null || !session.AwaitingFailureDecision || extraMovesOffer == null ||
                extraMovesOffer.extraMovesOnPurchase <= 0) return;
            LevelAnalytics.CheckoutStarted(extraMovesOffer.productId, extraMovesOffer.priceValue, extraMovesOffer.currencyCode);
            PowerUpStore.RequestPurchase(extraMovesOffer.productId);
        }

        public void ActivatePreview()
        {
            if (game != null && game.ActivatePreview()) Refresh();
        }

        public void NextLevel()
        {
            if (session == null) return;
            LevelDefinition next = LevelCatalog.Next(session.Definition.levelNumber);
            if (next != null) LevelFlow.StartLevel(next.levelNumber);
        }

        private bool ShouldShowTutorial()
        {
            return session != null &&
                   session.Definition.levelNumber == 1 &&
                   !session.Progress.firstLevelTutorialComplete &&
                   session.Definition.tutorialSteps != null && session.Definition.tutorialSteps.Count > 0 &&
                   tutorialPanel != null && tutorialLabel != null && tutorialNextButton != null;
        }

        private void ShowTutorialStep()
        {
            if (tutorialPanel == null || session == null) return;
            tutorialPanel.SetActive(true);
            if (playPanel != null) playPanel.SetActive(false);
            tutorialLabel.text = session.Definition.tutorialSteps[tutorialStep] +
                                 "\n\n" + (tutorialStep + 1) + " / " + session.Definition.tutorialSteps.Count;
            TMP_Text buttonLabel = tutorialNextButton.GetComponentInChildren<TMP_Text>();
            if (buttonLabel != null)
                buttonLabel.text = tutorialStep == session.Definition.tutorialSteps.Count - 1 ? "START" : "CONTINUE";
        }

        public void AdvanceTutorial()
        {
            if (session == null) return;
            tutorialStep++;
            if (tutorialStep < session.Definition.tutorialSteps.Count)
            {
                ShowTutorialStep();
                return;
            }

            if (tutorialPanel != null) tutorialPanel.SetActive(false);
            session.CompleteFirstLevelTutorial();
            if (game != null)
            {
                game.state = GameController.GameState.play;
            }
            if (playPanel != null) playPanel.SetActive(true);
            Refresh();
        }
    }
}

