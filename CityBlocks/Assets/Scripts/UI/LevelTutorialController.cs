using Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Coordinates the multi-step tutorial lifecycle using custom prefabs per step.
    /// Prefabs can contain whatever UI elements are needed (instruction banners, pointing arrows, text, etc.).
    /// The player can advance by clicking any continue button found on the prefab OR by placing a tower / making a move in the game.
    /// </summary>
    public class LevelTutorialController : MonoBehaviour
    {
        [Header("Container")]
        [SerializeField] private Transform stepContainer;

        private LevelSession session;
        private GameController game;
        private LevelRulesUI rulesUI;

        private int currentStepIndex = 0;
        private GameObject currentStepInstance;
        private TutorialStep currentStepComponent;
        private bool isTutorialActive = false;

        public bool IsTutorialActive => isTutorialActive;
        public int CurrentStepIndex => currentStepIndex;

        public void Initialize(LevelRulesUI ui)
        {
            rulesUI = ui;
        }

        /// <summary>
        /// Begins the tutorial sequence defined in the active level definition.
        /// </summary>
        public void StartTutorial(LevelSession levelSession, GameController gameController)
        {
            session = levelSession;
            game = gameController;

            if (session == null || session.Definition == null ||
                session.Definition.tutorialSteps == null || session.Definition.tutorialSteps.Count == 0)
            {
                CompleteTutorial();
                return;
            }

            isTutorialActive = true;
            currentStepIndex = 0;

            if (game != null)
            {
                // Ensure game is actively accepting moves
                game.state = GameController.GameState.play;
                game.PlacementFilter = null; // No column gating; allow free placement
                game.onTowerPlaced -= HandleTowerPlaced;
                game.onTowerPlaced += HandleTowerPlaced;
            }

            if (rulesUI != null)
            {
                rulesUI.SetPlayPanelVisible(true);
            }

            ShowStep(currentStepIndex);
        }

        public void ShowStep(int index)
        {
            currentStepIndex = index;

            // Destroy previous step instance
            if (currentStepInstance != null)
            {
                Destroy(currentStepInstance);
                currentStepInstance = null;
                currentStepComponent = null;
            }

            if (session == null || session.Definition.tutorialSteps == null ||
                index < 0 || index >= session.Definition.tutorialSteps.Count)
            {
                CompleteTutorial();
                return;
            }

            GameObject stepPrefab = session.Definition.tutorialSteps[index];
            if (stepPrefab == null)
            {
                Debug.LogWarning($"[LevelTutorialController] Tutorial step {index} prefab is missing, skipping.");
                AdvanceStep();
                return;
            }

            Transform parent = stepContainer != null ? stepContainer : transform;
            currentStepInstance = Instantiate(stepPrefab, parent);
            currentStepInstance.SetActive(true);

            // Match parent RectTransform if UI element
            RectTransform rt = currentStepInstance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localScale = Vector3.one;
                rt.localPosition = Vector3.zero;
            }

            currentStepComponent = currentStepInstance.GetComponent<TutorialStep>();
            if (currentStepComponent != null)
            {
                currentStepComponent.ValidateAndApply();
            }

            // Ensure the game is in play state and play HUD is active
            if (game != null)
            {
                game.state = GameController.GameState.play;
            }
            if (rulesUI != null)
            {
                rulesUI.SetPlayPanelVisible(true);
            }

            // Auto-wire any Button on the prefab to advance the tutorial
            Button btn = currentStepComponent != null && currentStepComponent.continueButton != null
                ? currentStepComponent.continueButton
                : currentStepInstance.GetComponentInChildren<Button>(true);

            if (btn != null)
            {
                btn.onClick.RemoveListener(AdvanceStep);
                btn.onClick.AddListener(AdvanceStep);
            }
        }

        public void AdvanceStep()
        {
            currentStepIndex++;
            if (session != null && session.Definition.tutorialSteps != null &&
                currentStepIndex < session.Definition.tutorialSteps.Count)
            {
                ShowStep(currentStepIndex);
            }
            else
            {
                CompleteTutorial();
            }
        }

        public void CompleteTutorial()
        {
            isTutorialActive = false;

            if (currentStepInstance != null)
            {
                Destroy(currentStepInstance);
                currentStepInstance = null;
                currentStepComponent = null;
            }

            if (game != null)
            {
                game.PlacementFilter = null;
                game.onTowerPlaced -= HandleTowerPlaced;
                game.state = GameController.GameState.play;
            }

            if (session != null)
            {
                session.CompleteLevelTutorial();
            }

            if (rulesUI != null)
            {
                rulesUI.OnTutorialCompleted();
            }
        }

        private void HandleTowerPlaced(int col)
        {
            if (!isTutorialActive) return;

            // If advanceOnGameMove is true (default), placing a tower advances/completes the step
            bool advance = currentStepComponent == null || currentStepComponent.advanceOnGameMove;
            if (advance)
            {
                AdvanceStep();
            }
        }

        private void Update()
        {
            if (isTutorialActive && game != null && game.state != GameController.GameState.play)
            {
                game.state = GameController.GameState.play;
            }
        }

        private void OnDestroy()
        {
            if (game != null)
            {
                game.PlacementFilter = null;
                game.onTowerPlaced -= HandleTowerPlaced;
            }
        }
    }
}

