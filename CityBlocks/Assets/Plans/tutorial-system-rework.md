# Project Overview
- Game Title: City Blocks
- High-Level Concept: A puzzle strategy city-building game where players place, stack, and merge structures on a grid, managing limited moves and board space to reach target objectives.
- Players: Single player
- Inspiration / Reference Games: CityBlocks, 2048, Triple Town
- Tone / Art Direction: Casual, vibrant low-poly 3D aesthetics with clear, high-contrast mobile UI.
- Target Platform: iOS and Android mobile devices
- Screen Orientation / Resolution: Portrait 1080x1920
- Render Pipeline: Universal Render Pipeline (URP-HighFidelity)

# Game Mechanics
## Core Gameplay Loop
Players receive buildings from a queue and place them onto available grid columns. Adjacent buildings of matching tier merge into higher-level architectural structures, freeing board space, awarding score, and damaging adjacent obstacles. The objective is to achieve required tower levels or scores before exhausting moves or letting the grid fill completely.

## Controls and Input Methods
- Direct touch/click on 3D grid columns to place the queued building.
- HUD buttons for activating power-ups (Delete, Level Up, Look Ahead).
- Modal and overlay UI controls during menus, level-up milestones, and tutorials.

# UI
## Tutorial Architecture Wireframe & Layout

### Non-Intrusive UI Overlay (Zero-Config Prefab)
```
+---------------------------------------------------+
|  [Instructional Banner: "TAP COLUMN TO PLACE"]    |
|  (Non-blocking UI Image + Text, raycastTarget=0)  |
|                                                   |
|             |                                     |
|             v  [Animated Arrow / Finger Asset]    |
|                                                   |
|      Col 0             Col 1             Col 2    |
|   +----------+      +----------+      +----------+|
|   | 3D Board |      | 3D Board |      | 3D Board ||
|   | (Active) |      | (Active) |      | (Active) ||
|   +----------+      +----------+      +----------+|
|                                                   |
|      [Optional Button: "CONTINUE"] (If present)   |
+---------------------------------------------------+
```
- **Transparent & Non-Blocking**: The container and background images do not intercept raycasts, allowing direct 3D Physics touches to reach `Cell.cs`.
- **Dual-Completion**:
  - The player can place a tower / make a move directly on the board to advance/complete the tutorial step.
  - OR, if the prefab contains a button (like "CONTINUE"), tapping it also advances/completes the step. No button press is required.
- **Zero Inspector Configuration**: Any prefab can be placed in `LevelDefinition.tutorialSteps`. The system auto-detects buttons, listens for gameplay moves, and handles everything with no manual setup.

# Key Asset & Context

### Scripts to Update / Simplify
1. `Assets/Scripts/UI/TutorialStep.cs`:
   - Ultra-simple, optional component on step prefabs:
     ```csharp
     public class TutorialStep : MonoBehaviour
     {
         [Tooltip("Optional continue button. Auto-detected in children if not assigned.")]
         public Button continueButton;

         [Tooltip("If true, placing a tower or making a move in the game advances/completes this step.")]
         public bool advanceOnGameMove = true;
     }
     ```
   - No complex enums, no column lockout math, no required parameters. If `TutorialStep` is not even present on the prefab, the controller still works with sensible defaults (`advanceOnGameMove = true`, auto-wires any child button).

2. `Assets/Scripts/UI/LevelTutorialController.cs`:
   - Instantiates step prefabs into `tutorialPanel` / `StepContainer`.
   - Ensures `game.state = GameState.play` so cells receive clicks/touches immediately.
   - Ensures `playPanel` remains active so the HUD and game are interactive.
   - Auto-finds any `Button` in the prefab and binds `onClick` to `AdvanceStep()`.
   - Listens to `game.onTowerPlaced` and automatically calls `AdvanceStep()` when a tower is placed.
   - Cleans up current step when advancing or completing.

3. `Assets/Scripts/UI/LevelRulesUI.cs`:
   - When tutorial starts, ensures `playPanel` is active and `game.state` is set to `GameState.play`.
   - Keeps HUD and board fully interactive.

### Assets & Prefabs to Update
1. `Assets/Resources/Level001_Tutorial1.prefab`:
   - Set root `Image.raycastTarget = false` (or remove blocking background image) so 3D cell clicks pass through.
   - Position instruction title and pointing asset (e.g. arrow / prompt) indicating where to tap.
   - Keep "CONTINUE" button optionally available, but player can advance simply by tapping any column to place a tower.
2. `Assets/Resources/Levels/Level001.asset`:
   - Fix reference to `Level001_Tutorial1.prefab` so `tutorialSteps[0]` is properly linked.

# Implementation Steps

### Step 1: Simplify TutorialStep Component
- **Description**: Simplify `Assets/Scripts/UI/TutorialStep.cs` to a minimal script with only optional `continueButton` and `advanceOnGameMove = true`. Remove all complex enums, column gating, and mask parameters.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

### Step 2: Update LevelTutorialController for Non-Blocking Free Interaction
- **Description**: Update `Assets/Scripts/UI/LevelTutorialController.cs` to ensure `game.state` remains `GameState.play`, keep HUD active, auto-bind any child button to advance the step, and listen to `game.onTowerPlaced` to advance when the player makes a move on the board. Remove the restrictive masking layer.
- **Assigned role**: developer
- **Dependencies**: Step 1
- **Parallelizable**: No

### Step 3: Update LevelRulesUI to Enable Immediate Board Play
- **Description**: Update `Assets/Scripts/UI/LevelRulesUI.cs` to ensure `playPanel` is kept visible and `game.state` is set to `GameState.play` when the tutorial overlay is shown so cell touches work immediately.
- **Assigned role**: developer
- **Dependencies**: Step 2
- **Parallelizable**: No

### Step 4: Reconfigure Level001_Tutorial1 Prefab as a Non-Blocking Overlay
- **Description**: Update `Assets/Resources/Level001_Tutorial1.prefab` to set root `Image.raycastTarget = false` so board clicks pass through, position the title banner and arrow pointing at the board, and keep the button optional so the player can either tap the button or directly tap a column to place a tower.
- **Assigned role**: developer
- **Dependencies**: Step 3
- **Parallelizable**: No

### Step 5: Fix Level001.asset Tutorial Step Reference
- **Description**: Re-link `Level001_Tutorial1.prefab` to `tutorialSteps[0]` in `Assets/Resources/Levels/Level001.asset`.
- **Assigned role**: developer
- **Dependencies**: Step 4
- **Parallelizable**: No

### Step 6: Verification and Play Mode Testing
- **Description**: Run Play Mode integration test. Verify Level 1 loads with `Level001_Tutorial1` overlay active, the player can immediately tap a column to place a tower without having to click "CONTINUE", placing a tower advances and completes the tutorial, and the game transitions into regular gameplay.
- **Assigned role**: developer
- **Dependencies**: Step 5
- **Parallelizable**: No

# Verification & Testing
1. **Compilation & Assembly Verification**:
   - Verify zero compilation errors across all project scripts.
2. **Immediate Board Interaction Test**:
   - Launch Level 1. Verify `Level001_Tutorial1` overlay is visible.
   - Verify `game.state` is `GameState.play`.
   - Tap column 1 directly on the 3D board: verify the tower is placed immediately without touching any UI button.
3. **Tutorial Completion via Move**:
   - Verify placing the tower immediately triggers tutorial advancement/completion and cleans up the tutorial overlay.
4. **Button Interaction (Optional)**:
   - If the player taps the "CONTINUE" button instead of the board, verify it also advances/completes the tutorial.
5. **No Parameter Configuration Required**:
   - Verify any prefab with visual elements (images, text) can simply be dropped into `LevelDefinition.tutorialSteps` and works out-of-the-box.
   - Open Endless Mode: Verify game starts normally, HUD displays correctly, and tutorial container remains inactive.
