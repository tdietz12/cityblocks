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

---

# Objective Cards Extension — Current Requested Scope

This revision adds objective-card work to the existing plan. Implement only this extension for the current request; do not repeat or change the earlier tutorial rework.

# Project Overview
- Game Title: City Blocks.
- High-Level Concept: Single-player tower-merging puzzle game. Campaign players must satisfy every configured level objective while managing board space and move limits.
- Players: Single player.
- Inspiration / Reference Games: Existing plan references 2048 and Triple Town; no new design direction.
- Tone / Art Direction: Preserve the existing colorful city aesthetic and compact mobile cards.
- Target Platform: iOS, as currently configured.
- Screen Orientation / Resolution: Portrait; retain the existing CanvasScaler and layouts (prior plan reference: 1080x1920).
- Render Pipeline: URP-HighFidelity, Unity 6000.4.5f1.

# Game Mechanics
## Core Gameplay Loop
Place and merge towers, gain score, and meet every objective in `LevelSession.Definition.objectives`. Objective cards expose the actual win requirements without changing gameplay or completion checks.

## Controls and Input Methods
No new controls. Retain the Legacy Input Manager, current touch/click handling, and existing start/pause buttons. Objective icons and numbers are display-only and must not intercept gameplay raycasts.

# UI
## Confirmed Display Behavior
User selected: **required target on StartPanel; current and target on ScorePanel**.
- StartPanel: one icon and the required target per objective, e.g. tower icon `6`, score icon `1000`.
- ScorePanel: the matching icon with `current / target`, e.g. tower icon `2 / 6`, score icon `450 / 1000`.
- Tower progress comes from `game.level` (highest achieved tier), not a count of buildings or the tallest tower currently present. Score progress comes from `game.score`. These match `LevelSession.ObjectivesMet()`.
- Preserve actual current values, including values exceeding the target; do not substitute remaining amounts.
- Retain authored objective order. Include every objective, including completed ones; hide unused rows.
- Hide both objective cards in Endless mode or when there are no objectives. Do not invent obstacle-clearing goals or treat the move limit as an objective; moves already have a separate card.

## Wireframe
```
StartPanel / Panel / ObjectivesCard    ScorePanel / Panel / ObjectivesCard
+---------------------------+         +---------------------------+
| [Tower icon]       6      |         | [Tower icon]     2 / 6     |
| [Score icon]       1000   |         | [Score icon]   450 / 1000  |
+---------------------------+         +---------------------------+
```
Reuse existing icon/number styling. Use distinct, recognizable tower and score sprites, assigned explicitly in the Inspector. Inspect existing images visually before mapping generically named Vector assets; use a recognizable existing tower icon and a neutral points/score symbol, not currency or an unrelated power-up. If existing art cannot represent score clearly, obtain user confirmation before adding artwork.

# Key Asset & Context
- `Assets/Scripts/UI/LevelRulesUI.cs`: currently has no objective references or presentation logic. Add serialized references for the two cards and objective-type sprites, and centralize target/progress refresh.
- `Assets/Scripts/UI/ObjectiveCardUI.cs` (new): small reusable uGUI presenter for an ordered list of rows, each with a root, `UnityEngine.UI.Image`, and `TMP_Text`. Proposed API: `Bind(LevelDefinition definition, bool showProgress, Sprite towerIcon, Sprite scoreIcon)` and `Refresh(int highestTowerLevel, int score)`. Cache rows and bound values; do not recreate UI each frame.
- `Assets/Scenes/EndlessMode.unity`: the loaded `Game/LevelRulesUI` is a standalone scene hierarchy, not a connected prefab instance. Both cards already exist at `ScorePanel/Panel/ObjectivesCard` and `StartPanel/Panel/ObjectivesCard`, with Row1–Row3, Icon, and Text children. They currently contain static placeholders `5 / 6`, `40`, and `20`.
- `Assets/Prefabs/UI/LevelRulesUI.prefab`: separate reusable asset; content search found no ObjectivesCard rows. Add corresponding cards using the scene styling and bind the presenter references without replacing the whole scene UI or overwriting unrelated prefab content.
- `Assets/Scripts/Gameplay/LevelDefinition.cs`: read-only context; `LevelObjectiveType` currently has `ReachTowerLevel` and `ReachScore`; each objective has `type` and `target`. All 100 level assets were inspected; the current maximum is two objectives.
- `Assets/Scripts/Gameplay/LevelSession.cs`: read-only context; `Initialize()` binds UI, `ResolveMove()` refreshes it, and `ObjectivesMet()` checks all targets. No changes to objective semantics, saves, tutorials, or analytics.
- Existing `Assets/UI/` and `Assets/UI/FigmaImport/EndlessMode/` sprites and existing TMP font assets: reuse; no font or package changes.
- Impact inspected using AssetDatabase dependency queries: the prefab and scene reference LevelRulesUI; `Assets/_Recovery/0.unity` references the prefab. Do not directly edit recovery scenes. Recheck dependencies/dependents before implementing edits, using the dependency tool if available or read-only AssetDatabase queries otherwise.

# Implementation Steps
1. **Build the shared objective-card presenter**
   - Create `Assets/Scripts/UI/ObjectiveCardUI.cs` following project namespace and serialized-field conventions.
   - Bind each valid objective to its icon and target/progress format. Handle null definition, empty list, and missing UI references safely; report unsupported types rather than displaying the wrong icon.
   - Reuse Row1–Row3; disable unused rows. If future definitions exceed the available rows, instantiate additional rows from a configured template once at binding, with layout accommodating all rows; never silently truncate objectives.
   - Use vertical row layout and stable icon/text alignment, preserving the current compact card appearance. Disable raycastTarget on decorative icons/text.
   - Dependencies: inspected objective data model; no gameplay edits.
2. **Integrate with LevelRulesUI**
   - Modify `Assets/Scripts/UI/LevelRulesUI.cs` to reference both presenters and tower/score sprites.
   - In `Bind()`, bind StartPanel with targets and ScorePanel with progress from the same level definition.
   - In `Refresh()`, update both cards from `game.level` and `game.score`, including pause, resume, retry, next level, and restored runs.
   - In `InitializeEndlessMode()`, clear bindings and hide the cards to avoid stale campaign goals.
   - Preserve existing tutorial visibility and all menu/gameplay flow. Use existing refresh paths; add no polling or subscriptions unless verification proves a progress update is missed.
   - Dependencies: Step 1.
3. **Wire the standalone scene and reusable prefab**
   - Modify `Assets/Scenes/EndlessMode.unity`: add presenters to existing cards, assign row/icon/text references, remove visible placeholder defaults, and assign presenters/sprites on LevelRulesUI.
   - Modify `Assets/Prefabs/UI/LevelRulesUI.prefab`: add corresponding cards only where absent, maintain visual consistency, and serialize the same bindings.
   - Check compact progress text accommodates five-digit score targets and additional rows without colliding with the score, moves, or Play button.
   - Dependencies: Steps 1–2 and sprite inspection.
4. **Verify objective presentation and regressions**
   - Run compilation, presenter checks, campaign Play Mode checks, and Endless mode checks below.
   - Adjust only objective-card spacing or refresh integration if a test fails; no unrelated UI redesign.
   - Dependencies: Steps 1–3.

# Verification & Testing
- **Mapping/format tests:** ReachTowerLevel target 6 => Start `6`, HUD `2 / 6`; ReachScore target 1000 => Start `1000`, HUD `450 / 1000`. Verify mixed objectives retain order and type icons.
- **Progress semantics:** Initial starting towers and restored scores appear immediately. After merge/score resolution, HUD shows the updated highest achieved tier and score; removal of a tower does not reduce historical tower progress. Current values greater than target remain accurate.
- **Row lifecycle:** Check one, two, three, and more-than-three objectives using temporary in-memory definitions rather than editing production levels. All goals appear; unused rows are hidden; rebind to a smaller level removes stale extra rows.
- **No-objective cases:** Null/empty definitions and Endless mode show neither placeholder rows nor stale campaign objectives; regular score/record HUD stays visible.
- **Flow tests:** Start, pause, resume, tutorial overlay, retry, next level, and restored campaign runs show the correct level's targets and progress. StartPanel remains target-only even after pausing mid-level.
- **Asset wiring:** Verify both standalone scene objects and the prefab have complete serialized references and distinct correct sprites; no missing scripts or dangling references.
- **Mobile layout:** Check portrait Game View on small and tall iPhone aspect ratios, long score values, row/icon alignment, readable TMP text, and no overlap or accidental input blocking.
- **Regression:** Existing `ObjectivesMet()` still requires all configured objectives; moves, win/loss, power-ups, and tutorial interaction behave unchanged.
- **Compilation/build:** No new console errors; run existing tests if available and an iOS build validation where the installed toolchain permits it. Report unavailable device/build checks rather than claiming they passed.
