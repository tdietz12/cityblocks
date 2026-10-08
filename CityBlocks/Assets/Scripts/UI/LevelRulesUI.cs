using Data_Persistence;
using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Manages the in-game UI overlay for both Campaign (LevelSession) and Endless modes.
    /// Controls HUD labels, power-up buttons, menu flow, win/loss popups, and the first-level tutorial.
    /// </summary>
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
        [SerializeField] private Button endlessLossRetryButton;

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
        [SerializeField] private LevelTutorialController tutorialController;
        [SerializeField] private TMP_Text tutorialLabel;
        [SerializeField] private Button tutorialNextButton;

        private LevelSession session;
        private GameController game;
        private int tutorialStep;
        private bool isBound;
        private bool listenersRegistered;

        public bool IsBound => isBound;
        public bool IsLevelMode => session != null || LevelFlow.IsLevelMode;
        public LevelSession Session => session;
        public GameController Game => game;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            EnsureGameControllerReference();
            RegisterListeners();

            if (!LevelFlow.IsLevelMode)
            {
                // Ensure endless save data has been loaded before setting up UI
                if (DataPersistenceController.instance != null && !DataPersistenceController.instance.HasLoaded)
                {
                    DataPersistenceController.instance.LoadGame();
                }
                InitializeEndlessMode();
            }
            else if (session == null && LevelFlow.ActiveSession != null)
            {
                Bind(LevelFlow.ActiveSession, game);
            }
        }

        private void Update()
        {
            // Continuously update button interactability and remaining booster count labels
            RefreshPowerUps();
        }

        /// <summary>
        /// Sets up the UI for endless mode gameplay (hiding level-specific elements like move limits).
        /// </summary>
        public void InitializeEndlessMode()
        {
            session = null;
            EnsureGameControllerReference();

            if (DataPersistenceController.instance != null && !DataPersistenceController.instance.HasLoaded)
            {
                DataPersistenceController.instance.LoadGame();
            }

            HideAllOverlayPanels();

            if (movesLabel != null) movesLabel.gameObject.SetActive(false);
            if (scorePanel != null) scorePanel.SetActive(true);

            if (game != null && game.state == GameController.GameState.lose)
            {
                ShowLoss();
            }
            else
            {
                UpdatePlayOrStartPanels();

                if (playButtonLabel != null)
                {
                    playButtonLabel.text = (game != null && game.HasActiveRun) ? "RESUME" : "PLAY";
                }
            }

            isBound = true;
            Refresh();
        }

        /// <summary>
        /// Binds an active Campaign level session and game controller to this UI.
        /// </summary>
        public void Bind(LevelSession levelSession, GameController gameController)
        {
            session = levelSession;
            game = gameController;

            RegisterListeners();
            HideAllOverlayPanels();

            if (movesLabel != null) movesLabel.gameObject.SetActive(true);
            if (scorePanel != null) scorePanel.SetActive(true);

            // Configure optional IAP button for purchasing additional moves
            if (buyExtraMovesButton != null)
            {
                bool hasOffer = extraMovesOffer != null &&
                                extraMovesOffer.extraMovesOnPurchase > 0 &&
                                !string.IsNullOrWhiteSpace(extraMovesOffer.productId);
                buyExtraMovesButton.gameObject.SetActive(hasOffer);
            }

            if (ShouldShowTutorial())
            {
                SetPanelActive(startPanel, false);
                SetPanelActive(playPanel, true);
                if (game != null) game.state = GameController.GameState.play;
                ShowTutorialStep();
            }
            else
            {
                UpdatePlayOrStartPanels();
            }

            if (playButtonLabel != null) playButtonLabel.text = "PLAY";

            isBound = true;
            Refresh();
        }

        /// <summary>
        /// Registers all button click event listeners once.
        /// </summary>
        private void RegisterListeners()
        {
            if (listenersRegistered) return;
            listenersRegistered = true;

            // Header & Menu buttons
            if (playButton != null) playButton.onClick.AddListener(StartGame);
            if (startBackButton != null) startBackButton.onClick.AddListener(ReturnToMenu);
            if (pauseButton != null) pauseButton.onClick.AddListener(PauseGame);

            // Power-up activation buttons
            if (deletePowerUpButton != null) deletePowerUpButton.onClick.AddListener(() => TryUsePowerUp(GameController.PowerUpType.delete));
            if (levelUpPowerUpButton != null) levelUpPowerUpButton.onClick.AddListener(() => TryUsePowerUp(GameController.PowerUpType.levelUp));
            if (lookAheadPowerUpButton != null) lookAheadPowerUpButton.onClick.AddListener(ActivateLookAhead);

            // Endless mode level up continue
            if (continueLevelUpButton != null) continueLevelUpButton.onClick.AddListener(ContinueFromLevelUp);

            // Win / Loss buttons
            if (nextLevelButton != null) nextLevelButton.onClick.AddListener(NextLevel);
            if (winMenuButton != null) winMenuButton.onClick.AddListener(ReturnToMenu);
            if (retryButton != null) retryButton.onClick.AddListener(Retry);
            if (lossMenuButton != null) lossMenuButton.onClick.AddListener(ReturnToMenu);
            if (buyExtraMovesButton != null) buyExtraMovesButton.onClick.AddListener(BuyExtraMoves);
            if (endlessLossBackButton != null) endlessLossBackButton.onClick.AddListener(ReturnToMenu);
            if (endlessLossRetryButton != null) endlessLossRetryButton.onClick.AddListener(Retry);

            // Tutorial
            if (tutorialNextButton != null) tutorialNextButton.onClick.AddListener(AdvanceTutorial);
        }

        /// <summary>
        /// Starts or resumes the game from the pause/start overlay.
        /// </summary>
        public void StartGame()
        {
            if (game != null) game.state = GameController.GameState.play;
            SetPanelActive(startPanel, false);
            SetPanelActive(playPanel, true);
            Refresh();
        }

        /// <summary>
        /// Pauses the game and reveals the start/pause menu with a RESUME label.
        /// </summary>
        public void PauseGame()
        {
            if (game != null) game.state = GameController.GameState.start;
            SetPanelActive(playPanel, false);
            SetPanelActive(startPanel, true);
            if (playButtonLabel != null) playButtonLabel.text = "RESUME";

            // In endless mode, save when entering the pause menu
            if (!IsLevelMode && DataPersistenceController.instance != null)
            {
                DataPersistenceController.instance.SaveGame();
            }

            Refresh();
        }

        /// <summary>
        /// Dismisses the endless level up milestone screen and returns to active gameplay.
        /// </summary>
        public void ContinueFromLevelUp()
        {
            SetPanelActive(levelUpPanel, false);
            SetPanelActive(playPanel, true);
            if (game != null) game.state = GameController.GameState.play;
            Refresh();
        }

        /// <summary>
        /// Initiates the selected power-up mode or look-ahead display.
        /// </summary>
        public void TryUsePowerUp(GameController.PowerUpType type)
        {
            if (game == null) return;

            if (type == GameController.PowerUpType.lookAhead)
            {
                ActivateLookAhead();
                return;
            }

            if (game.TryBeginPowerUp(type))
            {
                SetPanelActive(playPanel, false);
                switch (type)
                {
                    case GameController.PowerUpType.delete:
                        SetPanelActive(powerUpPanel_Delete, true);
                        break;
                    case GameController.PowerUpType.levelUp:
                        SetPanelActive(powerUpPanel_LevelUp, true);
                        break;
                }
            }
        }

        /// <summary>
        /// Returns to the main menu scene, notifying the active session or saving/resetting endless values.
        /// </summary>
        public void ReturnToMenu()
        {
            if (session != null)
            {
                session.ReturnToMenu();
            }
            else
            {
                if (game != null && game.state == GameController.GameState.lose)
                {
                    game.ResetValuesOnLoss();
                    if (game.grid != null)
                    {
                        game.grid.ClearBoard();
                    }
                    if (DataPersistenceController.instance != null)
                    {
                        DataPersistenceController.instance.ClearSavedGame();
                    }
                }
                else
                {
                    if (DataPersistenceController.instance != null)
                    {
                        DataPersistenceController.instance.SaveGame();
                    }
                }
                LevelFlow.GoToMenu();
            }
        }

        /// <summary>
        /// Retries the current level or restarts Endless mode.
        /// </summary>
        public void Retry()
        {
            if (session != null)
            {
                session.Retry();
            }
            else
            {
                if (game != null)
                {
                    game.ResetValuesOnLoss();
                    if (game.grid != null)
                    {
                        game.grid.ClearBoard();
                    }
                }
                if (DataPersistenceController.instance != null)
                {
                    DataPersistenceController.instance.ClearSavedGame();
                }
                SceneManager.LoadScene("EndlessMode");
            }
        }

        /// <summary>
        /// Refreshes all HUD texts: scores, moves remaining, and power-up buttons.
        /// </summary>
        public void Refresh()
        {
            EnsureGameControllerReference();
            if (game == null) return;

            if (scoreText != null) scoreText.text = game.score.ToString();
            if (recordScoreText != null) recordScoreText.text = game.recordScore.ToString();

            if (session != null && movesLabel != null)
            {
                movesLabel.text = session.Definition.moveLimit > 0
                    ? "MOVES: " + session.MovesRemaining
                    : "MOVES: ∞";
            }

            if (lookAheadPowerUpButton != null)
            {
                lookAheadPowerUpButton.gameObject.SetActive(true);
            }

            if (playButtonLabel != null && !IsLevelMode)
            {
                playButtonLabel.text = (game != null && game.HasActiveRun) ? "RESUME" : "PLAY";
            }

            RefreshPowerUps();
        }

        /// <summary>
        /// Updates the state and badge counts on all power-up buttons based on player inventory and current game state.
        /// </summary>
        private void RefreshPowerUps()
        {
            if (game == null) return;
            bool canUse = game.state == GameController.GameState.play && !game.IsResolvingMove;

            // Delete Power-up
            int deleteUses = PowerUpStore.Uses(GameController.PowerUpType.delete);
            UpdateButtonState(deletePowerUpButton, deletePowerUpLabel, canUse && deleteUses > 0, deleteUses.ToString());

            // Level Up Power-up
            int levelUpUses = PowerUpStore.Uses(GameController.PowerUpType.levelUp);
            UpdateButtonState(levelUpPowerUpButton, levelUpPowerUpLabel, canUse && levelUpUses > 0, levelUpUses.ToString());

            // Look Ahead Power-up
            int lookAheadUses = PowerUpStore.Uses(GameController.PowerUpType.lookAhead);
            bool canUseLookAhead = canUse && lookAheadUses > 0 && game.LookAheadTurnsRemaining <= 0;
            string lookAheadText = game.LookAheadTurnsRemaining > 0
                ? game.LookAheadTurnsRemaining.ToString()
                : lookAheadUses.ToString();
            UpdateButtonState(lookAheadPowerUpButton, lookAheadPowerUpLabel, canUseLookAhead, lookAheadText);
        }

        /// <summary>
        /// Displays the level win popup panel.
        /// </summary>
        public void ShowWin()
        {
            SetPanelActive(winPanel, true);
            SetPanelActive(playPanel, false);

            if (nextLevelButton != null && session != null)
            {
                nextLevelButton.interactable = LevelCatalog.Next(session.Definition.levelNumber) != null;
            }

            Refresh();
        }

        /// <summary>
        /// Displays the level or endless loss popup panel.
        /// </summary>
        public void ShowLoss()
        {
            SetPanelActive(playPanel, false);
            SetPanelActive(endlessLossPanel, false);
            SetPanelActive(lossPanel, true);

            if (session != null)
            {
                if (buyExtraMovesButton != null)
                {
                    bool canBuyMoves = session.PendingFailedObjective == "moves_limit" &&
                                       extraMovesOffer != null &&
                                       extraMovesOffer.extraMovesOnPurchase > 0 &&
                                       !string.IsNullOrWhiteSpace(extraMovesOffer.productId);
                    buyExtraMovesButton.gameObject.SetActive(canBuyMoves);
                }
            }
            else
            {
                if (buyExtraMovesButton != null)
                {
                    buyExtraMovesButton.gameObject.SetActive(false);
                }
            }

            Refresh();
        }

        /// <summary>
        /// Hides loss panels and restores the active play panel (e.g. after purchasing extra moves).
        /// </summary>
        public void HideLoss()
        {
            SetPanelActive(lossPanel, false);
            SetPanelActive(endlessLossPanel, false);
            SetPanelActive(playPanel, true);
        }

        /// <summary>
        /// Initiates the IAP purchase flow for extra moves during a moves-exhausted loss state.
        /// </summary>
        private void BuyExtraMoves()
        {
            if (session == null || !session.AwaitingFailureDecision || extraMovesOffer == null ||
                extraMovesOffer.extraMovesOnPurchase <= 0) return;

            LevelAnalytics.CheckoutStarted(extraMovesOffer.productId, extraMovesOffer.priceValue, extraMovesOffer.currencyCode);
            PowerUpStore.RequestPurchase(extraMovesOffer.productId);
        }

        /// <summary>
        /// Activates the Look Ahead preview booster.
        /// </summary>
        public void ActivateLookAhead()
        {
            if (game != null && game.ActivateLookAhead())
            {
                Refresh();
            }
        }

        /// <summary>
        /// Alias for ActivateLookAhead.
        /// </summary>
        public void ActivatePreview() => ActivateLookAhead();

        /// <summary>
        /// Loads the next campaign level.
        /// </summary>
        public void NextLevel()
        {
            if (session == null) return;
            LevelDefinition next = LevelCatalog.Next(session.Definition.levelNumber);
            if (next != null)
            {
                LevelFlow.StartLevel(next.levelNumber);
            }
        }

        /// <summary>
        /// Advances through the step-by-step tutorial cards or finishes it and begins play.
        /// </summary>
        public void AdvanceTutorial()
        {
            if (tutorialController != null && tutorialController.IsTutorialActive)
            {
                tutorialController.AdvanceStep();
                return;
            }

            if (session == null) return;
            tutorialStep++;

            if (tutorialStep < session.Definition.tutorialSteps.Count)
            {
                ShowTutorialStep();
                return;
            }

            SetPanelActive(tutorialPanel, false);
            session.CompleteLevelTutorial();

            if (game != null)
            {
                game.state = GameController.GameState.play;
            }

            SetPanelActive(playPanel, true);
            Refresh();
        }

        public void SetPlayPanelVisible(bool visible)
        {
            SetPanelActive(playPanel, visible);
        }

        public void OnTutorialCompleted()
        {
            SetPanelActive(tutorialPanel, false);
            if (game != null)
            {
                game.state = GameController.GameState.play;
            }
            SetPanelActive(playPanel, true);
            Refresh();
        }

        private bool ShouldShowTutorial()
        {
            return session != null &&
                   session.Definition != null &&
                   session.Definition.tutorialSteps != null &&
                   session.Definition.tutorialSteps.Count > 0 &&
                   session.Definition.tutorialSteps.Exists(step => step != null);
        }

        private void ShowTutorialStep()
        {
            if (session == null) return;

            if (tutorialController == null)
            {
                tutorialController = GetComponentInChildren<LevelTutorialController>(true);
            }

            if (tutorialPanel != null)
            {
                tutorialPanel.SetActive(true);
            }

            if (game != null)
            {
                game.state = GameController.GameState.play;
            }
            SetPanelActive(playPanel, true);

            if (tutorialController != null)
            {
                tutorialController.Initialize(this);
                tutorialController.StartTutorial(session, game);
                return;
            }

            // Fallback if tutorialController is not assigned
            if (tutorialPanel == null) return;
            GameObject currentTutorialPanel = Instantiate(session.Definition.tutorialSteps[tutorialStep], tutorialPanel.transform);
            currentTutorialPanel.transform.SetParent(tutorialPanel.transform);
            SetPanelActive(playPanel, true);
        }

        private void EnsureGameControllerReference()
        {
            if (game == null)
            {
                game = GameController.instance ?? FindAnyObjectByType<GameController>();
            }
        }

        private void HideAllOverlayPanels()
        {
            SetPanelActive(winPanel, false);
            SetPanelActive(lossPanel, false);
            SetPanelActive(endlessLossPanel, false);
            SetPanelActive(tutorialPanel, false);
            SetPanelActive(levelUpPanel, false);
            SetPanelActive(powerUpPanel_Delete, false);
            SetPanelActive(powerUpPanel_LookAhead, false);
            SetPanelActive(powerUpPanel_LevelUp, false);
        }

        private void UpdatePlayOrStartPanels()
        {
            bool isPlaying = game != null && game.state == GameController.GameState.play;
            SetPanelActive(startPanel, !isPlaying);
            SetPanelActive(playPanel, isPlaying);
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }

        private static void UpdateButtonState(Button button, TMP_Text label, bool interactable, string labelText)
        {
            if (button != null) button.interactable = interactable;
            if (label != null) label.text = labelText;
        }
    }
}

