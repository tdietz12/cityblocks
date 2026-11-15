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

---

# Tower Animation & Camera Feedback Extension — Current Requested Scope

Implement only this extension for the current request. Preserve the previous tutorial, objective-card, level-authoring, and loss/reset work; do not repeat it.

# Project Overview
- Game Title: City Blocks.
- High-Level Concept: A single-player city-themed tower-merging puzzle game. Improve the readability and tactile satisfaction of placement and merges through ordered motion and restrained camera feedback, without changing puzzle rules.
- Players: Single player.
- Inspiration / Reference Games: Existing project references 2048 and Triple Town; no new art direction.
- Tone / Art Direction: Preserve the colorful low-poly city aesthetic. Smooth, quick slides and near-imperceptible stabilized-handheld camera movement, not violent shake.
- Target Platform: iOS, as currently configured.
- Screen Orientation / Resolution: Portrait; retain existing layout and validate small/tall iPhone aspect ratios (previous reference resolution: 1080x1920).
- Render Pipeline: URP-HighFidelity; Unity 6000.4.5f1, URP 17.4.0.

# Game Mechanics
## Core Gameplay Loop
Select a valid column, place the queued tower, absorb matching neighbors, settle any affected towers, repeat any chain merges, then resolve the move and evaluate objectives/loss. Improve cause-and-effect clarity (MDA) and player competence/readability (SDT), while preserving existing scoring, tier increases, moves, obstacles, holes, queues, and save coordinates.

### Confirmed User Decisions
- The incoming tower appears **in the space below the lowest playable cell of the chosen column**, not at the current top-entry position or in an occupied playable cell.
- It slides upward/inward from that visual entrance to its initial landing/contact position.
- Every absorbed neighbor visibly slides into the stationary surviving tower and disappears on arrival. Only after all absorptions finish may the survivor and affected columns settle, followed by another merge check.
- Keep existing board rules. The user explicitly accepted that post-merge settling may slide downward when support below is removed; do not reverse gravity or force all final motion upward.
- Use a **small lens zoom**, not a physical camera dolly, for each merge; return to the default fitted lens afterward.
- At rest, add extremely subtle, smooth stabilized-handheld drift around the default camera pose.

### Animation Contract
1. Validate the move using the existing rules before consuming the queue or showing placement motion.
2. Snap only the incoming tower to the off-board visual entrance; restore its normal scale immediately when leaving the queue.
3. Slide to the rules-defined landing cell, or the selected full-column top tower's contact position for the existing special top merge.
4. Reserve a merge batch, hold the survivor still, and slide all matched neighbors in simultaneously. Arrival, not an arbitrary destruction timer, controls disappearance.
5. Once the batch has arrived, apply the existing tier/score/damage semantics exactly once, refresh the survivor's visual, play the existing effect, and trigger one camera pulse for that merge batch. Each subsequent chain-merge batch gets its own pulse; a simultaneous multi-neighbor merge remains one existing level-up batch.
6. Resolve affected-column gravity only after absorption completes. Wait for required movement/obstacle settling before checking the next batch and finally reporting the move settled.
7. Suggested starting values, serialized for tuning: placement/settling slides about 0.18–0.25 seconds (distance-aware with sensible limits), absorption about 0.16–0.20 seconds, camera punch about 0.07–0.09 seconds in and 0.18–0.24 seconds out, approximately 2.5% lens magnification. Use elapsed-time easing and exact endpoints, not per-frame Lerp coefficients. These are tuning defaults rather than gameplay rule changes.

## Controls and Input Methods
- Actual Player Settings were inspected: `activeInputHandler = 0` (Legacy Input Manager). `Cell.cs` uses touch/mouse and `Camera.main.ScreenPointToRay` with physics cell colliders.
- Preserve those controls; no input backend migration or new controls.
- Keep placement/power-ups blocked until the complete board animation sequence settles; camera recovery must not unnecessarily delay a legal next move.

# UI
No new panels, buttons, text, or artwork. Preserve screen-space HUD positions; world-space tower labels must stay attached/aligned throughout slides and zoom.

## Motion Storyboard
```
[Existing HUD — unchanged]

       Selected column
          [target]       2. slide upward to landing/contact
             ↑
       [lowest playable cell]
          [incoming]     1. appears in off-board space below it

   [match] → [survivor] ← [match]
      3. slide in together, disappear on arrival
      4. upgrade + small lens punch
      5. settle if necessary; repeat chain merge

   Default lens + barely perceptible smooth pose drift
```
The camera's child overlay renders layer-5 content including world-space tower labels. Synchronize its projection with the base camera during zoom and restoration so numbers do not separate from buildings. Preserve existing URP camera stack and screen-space HUD layout.

# Key Asset & Context
## Inspected State
- `Assets/Scripts/Gameplay/GameController.cs`: `PlayTower()` currently immediately destroys a full-column matching top tower, increments its incoming replacement, and starts the replacement at temporary row `totalRows - 1`. `SaveLevelAfterMove()` waits for `grid.IsSettled`. This special merge must receive the same visible absorption and camera feedback instead of disappearing instantly.
- `Assets/Scripts/Gameplay/TowerController.cs`: `Update()` currently moves toward logical row using frame-dependent Lerp; `Drop()` changes logical coordinates then waits 0.25 seconds; `Merge()` immediately removes neighbors, changes tier, then waits 0.25 seconds. `level += matches.Count` and one level-up event per batch are existing semantics to preserve.
- `Assets/Scripts/Gameplay/FallingTowerController.cs`: existing visual proxy self-destructs after 0.15 seconds regardless of whether it reaches its target and shrinks to half scale. Replace timer-driven disappearance with explicit slide completion; keep tower silhouettes legible until arrival.
- `Assets/Scripts/Gameplay/GridController.cs`: `WorldPosition(column,row)` maps row 0 to the near/screen-bottom board edge; `FindLandingRow`, `ResolveGravity`, `IsAvailable`, and `TopPlayableRow` define current logical rules. `towersInPlay` and `IsSettled()` are the existing flow contracts. Add a visual-entrance helper and reliable animation/batch tracking without changing save row coordinates.
- `Assets/Scripts/Gameplay/BoardCameraFitter.cs`: campaign fitting adjusts position once, retaining camera angle/FOV, and currently bounds board plus queue. It does not yet account explicitly for the new entrance below the near edge or recapture an animation baseline.
- `Assets/Scripts/Gameplay/LevelSession.cs`: calls `BoardCameraFitter.Fit` after loading board and obstacles. Read-only integration context; change only if the fitter cannot notify the camera feedback component directly.
- `Assets/Scenes/EndlessMode.unity`: shared gameplay scene, perspective `MainCamera`, FOV 45, rotation 60 degrees X, position (1.5, 9.5, -4.5). It has a child overlay camera at FOV 45 rendering layer 5. Preserve all lighting, Volume profiles, and camera-stack settings.
- `Assets/Prefabs/Tower.prefab`: wired to level visual resources, world-space number Canvas, level-up effect, and FallingTower prefab. Serialize motion tuning here while retaining visuals and references.
- `Assets/Prefabs/FallingTower.prefab`: reuse as an absorption visual proxy if needed; complete motion must be awaited and its cleanup tracked by the owning sequence.
- Proposed new `Assets/Scripts/Gameplay/BoardCameraFeedback.cs`: cached camera/game references; APIs such as `CaptureDefaultState()`, `PunchMerge()`, and `ResetFeedback()`. Single owner for additive drift and lens pulse.
- Proposed new `Assets/Editor/Tests/TowerAnimationFeedbackTests.cs`: editor-hosted deterministic checks/coroutine integration harness using the existing test package (1.6.0), without moving production scripts into new assembly definitions. Confirm discovery for predefined editor assemblies before finalizing harness setup.

## Dependency / Impact Analysis
Read-only AssetDatabase dependency and dependent queries were used because a dedicated dependency tool is not available in this session. Game/Grid scripts affect EndlessMode and recovery scene references; Tower script affects Tower prefab; FallingTower script affects FallingTower and Tower prefabs plus gameplay scene. Tower/FallingTower prefabs propagate to the gameplay scene. Do not directly edit `Assets/_Recovery/0.unity`. Recheck dependency/dependent impact for every edited asset before implementation. No packages, shaders, lighting, fonts, level assets, or save schema changes.

## Implementation Approach
Use guarded coroutine/elapsed-time motion integrated with the existing controllers, plus a lightweight camera feedback component. Reuse the existing visual proxy rather than introducing a tween package. Prevent overlapping tower routines/competing transform writers; reserve matched neighbors and defer their own drop/merge requests while absorbed. A broader board-model/animation-command rewrite would isolate logic more completely but is unnecessarily invasive for this animation-only request. Camera lens zoom was selected over dolly because it preserves perspective and fitted pose; smooth bounded noise supplies drift without random per-frame jitter.

# Implementation Steps
1. **Establish explicit movement and merge ownership**
   - Files: `GridController.cs`, `TowerController.cs`.
   - Add a per-tower active-sequence guard/phase and reserved-for-absorption flag. Replace the unconditional grid-mode transform Lerp with a single elapsed-time slide implementation; stationary towers snap only when no motion owns their transform.
   - Retain compatibility with existing `drop`/`merge` requests from gravity, power-ups, and saved spawn rows. Consume/defer requests safely instead of launching overlapping coroutines. Serialize conflicting merge batches with grid-level ownership/reservations so two survivors cannot absorb the same tower; do not reserve idle towers forever or create mutual-wait deadlocks.
   - Extend `IsSettled()` to cover active movement, absorption proxies/batches, and existing moving obstacles. Remove active tracking only when the sequence truly finishes. Include cancellation/teardown cleanup for board clearing/retry, proxy destruction, and interrupted sequences.
   - Add `VisualEntryPosition(column)` by finding that column's lowest available playable row and offsetting one cell toward the near edge, e.g. extrapolated `WorldPosition(column, lowestAvailableRow - 1)`. Keep it purely visual and outside logical occupancy/save data.
   - Dependencies: inspected grid coordinate mapping and existing flow contracts.
2. **Implement ordered placement, absorption, and settling**
   - Files: `GameController.cs`, `TowerController.cs`, `FallingTowerController.cs`; tuning on Tower/FallingTower prefabs.
   - In valid `PlayTower()`, restore scale and move the incoming tower to its visual entrance before starting its initial slide. Keep queue consumption, replenishment, `RecordMove`, look-ahead decrement, and `onTowerPlaced` exactly once per accepted move.
   - Route the special full-column top merge through an explicit forced-match absorption instead of immediate destruction. Keep the temporary entry reservation until the top cell becomes free, animate to the top tower's contact point, absorb it, then safely remap the survivor into the vacated playable cell and continue normal gravity/chain checks. Preserve its original +1 tier, score increment, level tracking, and top-cell obstacle-damage footprint, including boards with holes at their top edge.
   - For standard batches, capture neighbor coordinates/visuals, reserve all participants, clear consumed logical cells at the established commit point, and await all visual slides while the survivor remains stationary. Transfer the complete presentation (including the number label where appropriate) or animate the actual reserved tower; never leave floating labels behind.
   - Remove the fixed proxy `Invoke` lifetime; explicitly initialize target/duration and report completion. Destroy each consumed visual on arrival. Handle absent proxy prefab with the same visible behavior using the reserved tower, not instant deletion.
   - Apply batch tier/score events once after arrival; retain `level += matches.Count`, existing damage deduplication, and level-up effect. Only then request and await gravity in affected columns/obstacles before another merge check. Keep higher towers from moving into unfinished absorption paths.
   - Dependencies: Step 1.
3. **Add bounded camera zoom and stabilized drift**
   - Files: new `BoardCameraFeedback.cs`, `BoardCameraFitter.cs`; bind merge feedback from the common batch completion path in Tower/Game integration.
   - Cache the authored default pose/lens for Endless mode and recapture the campaign default immediately after fitting, never from an already drifted/zoomed frame. Fitting must use baseline camera axes/FOV if feedback has already started.
   - For lens magnification `m`, derive the pulse FOV from `2 * atan(tan(defaultFov/2) / m)` so 2.5% magnification means a small controlled frame punch, not an arbitrary degree jump.
   - Trigger once per completed batch, including the special top merge and actual chain merges following boosters. Do not pulse for an empty merge check or a booster tier increment with no absorption. New pulses restart smoothly from the current pulse value with a bounded peak, never accumulate permanent zoom.
   - Compose camera pose from immutable baseline plus low-frequency, tiny bounded smooth offsets (starting translation about 0.01–0.02 world units, angular drift about 0.05–0.10 degrees, negligible/no roll). Reduce drift during punch then smoothly resume; no per-frame random jitter, no accumulating pose drift.
   - Apply effects in one camera update owner, synchronize overlay lens projection, restore all captured values on disable/reset/refit. Suspend and restore feedback outside active gameplay so start/pause/win/loss screens remain stable; do not change current pause semantics or time scale. A pulse may finish independently of board settlement.
   - Expand campaign fitting bounds only enough to include the new entrance and small feedback margin, preserving queue and existing camera angle. Check authored Endless framing separately and adjust only if entrance is actually clipped.
   - Dependencies: Steps 1–2; inspected base/overlay camera setup.
4. **Wire and tune the shared gameplay scene**
   - Files: `Assets/Scenes/EndlessMode.unity`, `Assets/Prefabs/Tower.prefab`, `Assets/Prefabs/FallingTower.prefab` as required.
   - Add camera feedback to MainCamera, assign GameController/base/overlay cameras, and persist conservative motion values in the existing prefabs.
   - Preserve the current URP stack, world-space labels, layer masks, lighting, post-processing profiles, HUD, and unrelated prefab content. No changes to MainMenu camera.
   - Verify near-edge entrance positions on columns with unavailable bottom cells, different board sizes, high-level tower silhouettes, and left/right extremes. Tune timing and drift by visually reviewing placement, simultaneous merges, and chains.
   - Dependencies: Steps 2–3, dependency recheck before each asset edit.
5. **Verify timing, flow, and gameplay regressions**
   - Files: proposed editor-hosted tests/harness under `Assets/Editor/Tests/`; modify production integration only to correct demonstrated animation/settlement issues.
   - Run the checks below in both campaign and Endless mode. Use temporary test fixtures/in-memory definitions; do not alter production levels, user progress, or save data to create tests.
   - Dependencies: Steps 1–4.

# Verification & Testing
- **Placement:** valid taps show full-scale incoming tower one cell below that column's lowest available cell, then a smooth upward slide to the correct target. Empty/partially full/blocked columns and bottom holes behave correctly; invalid taps consume no tower or move and trigger no animation/pulse.
- **Absorption ordering:** one/two/multiple matching neighbors all reach the stationary survivor and disappear on arrival; no early timer deletion, orphaned labels/proxies, or survivor settling before all arrivals. Tier and score updates fire once using existing formulas.
- **Special full-column merge:** matching top tower visibly slides in and disappears; incoming tier increases once, existing top-cell obstacle damage is preserved, temporary occupancy is removed, and subsequent landing/chains behave correctly. Mismatched top/blocked obstacle remains invalid.
- **Chain and gravity:** test merges removing support, horizontal/vertical matches, two potentially competing survivors, falling/fixed/breakable obstacles, deduplicated obstacle damage, holes, and 3–7 row/column boards. Final logical boards/tiers/scores match the existing rule results and all movement ownership releases without deadlocks.
- **Frame-rate independence:** compare slides at 30, 60, and 120 FPS; elapsed durations are comparable and all endpoints exact. Verify long-frame/zero-duration tuning paths finish cleanly.
- **Flow safety:** `IsSettled` stays false through tower/proxy/obstacle activity; `onMoveResolved`, save, objectives, win/loss, and milestone screens occur only after board resolution. Rapid taps/power-ups cannot launch competing sequences. Camera return does not lock gameplay.
- **Camera:** every completed merge batch, including full-column/chain merges, causes a restrained punch. Non-merges do not. Repeated pulses remain capped and return to the captured FOV; baseline pose never accumulates drift. Long idle observation feels barely handheld, not jittery.
- **Projection/UI:** base and overlay projections stay aligned throughout zoom/drift; world-space numbers remain on towers, screen-space HUD stays fixed, and physics raycasts still select the displayed columns.
- **Lifecycle:** campaign fitting establishes a clean new baseline; retry, next level, resume, pause, win/loss, ClearBoard, component disable, and scene unload remove proxies/coroutines and restore lens/pose without missing references or stale reservations.
- **Mobile framing/performance:** portrait small/tall iPhone views keep the entrance, board, queue, and tall towers visible at punch peak/drift extrema. Cache references; no per-frame scene searches, recurring allocations, or expensive new rendering effects.
- **Compilation/test/build:** zero new console errors; run available deterministic checks and Play Mode scenarios, then iOS build/device validation when toolchain/device access permits. No new runtime assembly definitions or package installations merely for tests. Report unavailable checks explicitly; do not claim unperformed tests passed.

---

# Merge Score Popups & HUD Collection — Current Requested Scope

Implement only this extension for the current request. Prior tutorial/objective/animation work is context, not work to repeat. Preserve the latest actual animation tuning: top-entry spawn via `WorldPosition(column, totalRows - 1)`, slides 0.14s, absorption 0.12s, camera magnification 4%, punch-in 0.055s, return 0.16s, and existing subtle drift. These supersede the earlier animation extension's old entrance/timing descriptions.

# Project Overview
- Game Title: City Blocks.
- High-Level Concept: A single-player tower-merging city puzzle game. Make earned points travel visibly from each completed merge to the top score counter, creating clear cause-and-effect and satisfying collection feedback without changing scoring rules.
- Players: Single player.
- Inspiration / Reference Games: Existing 2048 / Triple Town references; no new game direction.
- Tone / Art Direction: Preserve colorful low-poly city art and compact gold/white HUD styling. Readable floating numbers, restrained motion, and one snappy counter pulse.
- Target Platform: iOS.
- Screen Orientation / Resolution: Portrait; retain existing CanvasScaler/layout, prior reference 1080x1920; verify small/tall iPhone aspect ratios.
- Render Pipeline: URP-HighFidelity, Unity 6000.4.5f1; no rendering-pipeline or Volume changes.

# Game Mechanics
## Core Gameplay Loop
Place a tower, resolve merges and gravity, accumulate points, and meet level objectives. Each completed merge batch awards `resultingTowerLevel * 10` internally, while the corresponding HUD collection animation delays only the displayed total.

### Confirmed Decision and Alternatives
User selected **Delay visible score**: keep the authoritative `game.score` and record updates immediate; saves and objective checks continue using those values. Delay the score's visual presentation until the popup reaches the HUD. This preserves reliable progression while making feedback legible (MDA) and supporting player competence (SDT).

Rejected alternative: delay actual scoring until arrival. It matches literal collection timing, but would require pending-point recovery/serialization and additional changes to objective and move resolution. Do not implement that alternative or add a save schema for cosmetic flights.

### Collection Contract
- One `+N` popup per **completed merge batch**, including full-column top merges and every successive chain batch. Simultaneous absorption of several matching neighbors remains one batch, with the existing final-tier award; do not change the formula or award points per absorbed neighbor.
- Spawn at the merge's captured world location, projected into screen-space UI using the explicit gameplay camera, with a small visual height offset so it is perceivable above the building.
- Brief pop-out scale animation (approximately 0.10–0.12s) at spawn; during the first **1.0 second total**, float upward slowly in screen space (initial rise distance about 70–100 reference pixels). Pop-out overlaps that one-second rise, rather than adding another full phase.
- After the rise, fly smoothly/quickly to the actual top HUD score number over about 0.25–0.30s. Recompute the destination from the target RectTransform as needed so safe-area/layout changes do not cause misses.
- On arrival, hide/recycle the popup, release exactly that award from the pending visual total, update the displayed score, and play **one fast pulse** on the score number (about 1.00 -> 1.12 -> 1.00 over 0.15–0.18s, no looping/bounce).
- Multiple popups may coexist and retain independent award amounts. Same-frame arrivals may update the counter together with one pulse; later arrivals retrigger smoothly from the current pulse scale without additive growth or lost awards.
- Popup flights are cosmetic: they do not extend board settling or block normal next placements after gravity/merges settle. Terminal gameplay states still block input normally; win/loss/milestone overlays must not cover unfinished collection feedback.
- No popup or pulse for a non-merge placement, obstacle damage, or the initial tier increase from a Level Up booster. Actual merges caused by boosters still award/pop normally.

## Controls and Input Methods
Keep Legacy Input Manager touch/click controls and all existing buttons. Popups are display-only: no GraphicRaycaster on the feedback Canvas, text/image raycastTarget disabled, CanvasGroup blocksRaycasts/interactable disabled. Existing physics cell clicks and tutorial/power-up targeting must pass through the floating numbers.

# UI
## Storyboard
```
+-----------------------------------------+
|         SCORE 780  <- popup arrives      |
|                   -> 810, one pulse     |
|                      ^                  |
|                     /  quick flight     |
|              +30                        |
|               ^  slow rise for 1 second |
|          [completed merge]              |
|                                         |
|      existing HUD/buttons unchanged     |
+-----------------------------------------+
```
- Actual scene score target: `Game/LevelRulesUI/ScorePanel/Panel/Score/ScoreNumberText`.
- Both GameController and LevelRulesUI currently reference this same TMP label; both must use a shared displayed-score value instead of overwriting it with `game.score` during a flight.
- Existing root `Game/LevelRulesUI` is ScreenSpaceOverlay, sorting order 30. Add a small dedicated feedback child Canvas/layer outside `PlayPanel`, with suitable sorting just above the HUD; do not place popups inside a layout group or a panel that hides when gameplay pauses.
- Reuse `LiberationSans SDF.asset` and existing gold score styling. Popup text uses fixed readable point size and a sufficiently wide rect for large `+N` values; no font generation or runtime AutoSize on popups. Preserve existing score-label layout/AutoSize behavior unless a demonstrated clipping issue requires a narrowly scoped adjustment.
- Pulse the score number's visual RectTransform or a centered visual wrapper without scaling the whole header or triggering layout-size changes. Preserve baseline scale/pivot/layout.
- The record label and campaign score-objective progress should present the same collected/displayed score timing, while their underlying record/objective calculations stay immediate. Tower-level objective progress remains immediate. StartPanel targets remain target-only.

# Key Asset & Context
## Files to Modify
- `Assets/Scripts/Gameplay/TowerController.cs`: both `PlaceAtFullColumn()` and `Merge()` invoke `onTowerLevelUpEvent(level)` after absorption. Route score awards through a position-aware method at that exact completion point; retain tier-tracking event behavior and existing damage/gravity/camera effects.
- `Assets/Scripts/Gameplay/GameController.cs`: `IncrementScore(int)` currently awards `towerLevel * 10` and writes score/record text immediately. Add a position-aware merge-award entry point (e.g. `AwardMergeScore(int resultingLevel, Vector3 worldPosition)`), a score-award notification/presenter integration, and public read-only displayed-score accessors with immediate fallback. Keep model values, records, save paths, and formula unchanged. Remove old score subscription on newly queued towers so the explicit award does not double count.
- `Assets/Scripts/Gameplay/GridController.cs`: remove the corresponding old scoring subscription when restoring grid towers; preserve highest-tier subscription and the existing compatibility score forwarding method where needed. Integrate board-clear cancellation/synchronization using the existing `onBoardCleared` signal.
- `Assets/Scripts/UI/LevelRulesUI.cs`: `Refresh()` currently writes `game.score`, `game.recordScore`, and score-objective progress directly. Bind the shared score presenter, refresh from displayed values, and react to collected arrivals. Add cancellable/generation-checked deferred presentation for `ShowWin()` / `ShowLoss()` when flights remain; game.state is already set by existing logical win/loss flow. Keep ScorePanel and feedback layer visible while waiting, disable irrelevant play buttons normally, and do not display stale panels after retry/rebind/menu exit.
- `GameController.HandleLevelUpCheck()`: defer only the endless milestone panel while feedback is pending, retaining the milestone flag until presentation can occur. Do not delay ordinary saves, `onMoveResolved`, or board input just for cosmetic flights.
- `Assets/Scenes/EndlessMode.unity`: wire the presenter, explicit MainCamera, score target/pulse target, and popup layer/template in the existing standalone hierarchy. Preserve camera stack, all gameplay timing, HUD layout, fonts, lights, and Volume profiles.
- `Assets/Prefabs/UI/LevelRulesUI.prefab`: add equivalent reusable feedback hierarchy/bindings; bind the gameplay camera/game controller through existing UI initialization because asset prefabs cannot hold scene-object references.

## Files to Create
- `Assets/Scripts/UI/ScoreFeedbackController.cs`: one owner of visual pending awards and popup pooling. Suggested API: `Bind(GameController game, Camera gameplayCamera)`, `Enqueue(int points, Vector3 mergeWorldPosition)`, `DisplayedScore`, `DisplayedRecordScore`, `HasPendingFeedback`, `onDisplayedScoreChanged`, and `ClearAndSynchronize()`.
- `Assets/Scripts/UI/MergeScorePopup.cs`: reusable UI motion element with cached TMP label/RectTransform/CanvasGroup, elapsed-time pop/rise/fly phases, and exactly-once completion or cancellation. Completion is owned by the presenter, never by a tower coroutine or a delayed Destroy timer.
- `Assets/Prefabs/UI/MergeScorePopup.prefab`: pooled `+N` template reusing the existing font/material/color, without input interception or a world-space Canvas.
- `Assets/Editor/Tests/ScoreFeedbackTests.cs`: deterministic UI/model checks and an isolated runtime bootstrap for multi-frame verification; no production assembly-definition migration or package changes.

## Inspected Connections and Dependencies
- All current scoring event subscriptions were searched: only GameController queue spawning and GridController saved-board restoration attach IncrementScore; remove/replace both to prevent double awards. Preserve existing highest-tier event subscriptions.
- AssetDatabase dependency/dependent queries inspected GameController, TowerController, GridController, LevelRulesUI, the gameplay scene, and the UI prefab. The scene is standalone; the UI prefab is a separate reusable asset. Recovery scenes reference those assets but must not be edited directly.
- `LevelSession.ObjectivesMet()` reads `game.score`; `Complete()` / `Fail()` set `game.state` before calling UI. Keep this authoritative behavior and defer only panels. `SaveLevelAfterMove()` currently saves at board settlement; leave it independent of flights.
- Explicit MainCamera projection is required: the existing child UICamera is also tagged MainCamera, so do not assume `Camera.main` selects the correct world-to-screen camera.
- Before implementation edits, recheck dependency/dependent impact using the dedicated dependency tool if available, or read-only AssetDatabase queries otherwise. Read current files again before editing; the earlier animation plan has stale descriptions.

# Implementation Steps
1. **Add reusable score popup presentation**
   - Create `MergeScorePopup.cs`, `ScoreFeedbackController.cs`, and `MergeScorePopup.prefab`.
   - Cache UI references and reuse pooled elements, avoiding per-frame allocations/scene searches, redundant text assignments, or recurring font/material creation. Isolate moving UI in the feedback Canvas to avoid rebuilding the whole HUD.
   - Use screen-space anchored coordinates and elapsed-time easing. Capture the merge origin at award time; do not make the popup follow the moving survivor or camera after spawn. Keep rise screen-up, then target the actual score RectTransform center.
   - Track pending award tokens/amounts and current run/bind generation. Each arrival releases one token exactly once; cancellation removes presentation only, never changes game.score.
   - Derive displayed score from authoritative total minus current pending visual awards (or an equivalent synchronized ledger), preserving correct totals even for simultaneous/out-of-order arrivals. Capture the starting record baseline so the record HUD does not reveal in-flight points prematurely.
   - Dependencies: inspected Canvas and score label; existing font/styling.
2. **Connect completed merges without changing scoring**
   - Modify TowerController, GameController, and GridController as above.
   - Award points immediately at both merge completion paths; enqueue visual feedback before either HUD writer refreshes. Maintain exactly one model increment per batch, including restored towers, full columns, and booster-triggered chains.
   - Expose displayed-value fallbacks when no active/configured presenter is available. Direct compatibility calls without a valid merge origin must still update immediately and never strand points.
   - Keep non-merge actions, model records, highest-tier tracking, objective checks, analytics payloads, and saves unchanged.
   - Dependencies: Step 1.
3. **Coordinate HUD writers, pulses, and overlays**
   - Modify LevelRulesUI and its bindings; both GameController.UpdateScoreUI and LevelRulesUI.Refresh consume the same displayed values. Use collection events to refresh promptly instead of additional per-frame score polling.
   - Keep score-objective progress presentation aligned with collected points, with targets unchanged. Pulse only on arrival, preserving/recovering the baseline scale during overlapping arrivals and panel transitions.
   - Delay win/loss panel visibility until HasPendingFeedback is false, while preserving the already-decided terminal state and immediate persistence. Guard against a stale delayed panel after a new bind/reset. Defer endless milestone presentation similarly without losing its flag.
   - Normal next moves remain available after board settlement. Use unscaled elapsed time for cosmetic flights/pulses so ordinary pause does not strand points or modal waits; keep actual gameplay time/pause rules unchanged.
   - On initialization/restored save/rebind, synchronize visual totals without replaying old popups. On clear/retry/unbind/disable/scene exit, cancel tokens and pulses and synchronize to the current logical total, with no duplicate awards or point loss. On app suspension, either pause naturally or flush presentation safely; persistence always uses the model score.
   - Dependencies: Steps 1–2.
4. **Wire standalone scene and reusable UI prefab**
   - Update only the specified UI hierarchy and serialized references in EndlessMode and LevelRulesUI.prefab.
   - Assign the explicit gameplay projection camera in the scene; perform equivalent binding through UI initialization for prefab use. Use the existing score target for both writers and a feedback layer that stays available during modal deferral.
   - Verify popup rect/font readability, clipping, top HUD target center, and baseline pulse scale on portrait sizes. Maintain tutorial masks, input passthrough, objective rows, and all unrelated panels.
   - Dependencies: Steps 1–3, impact recheck before each asset edit.
5. **Verify accounting, motion, and lifecycle behavior**
   - Add/use ScoreFeedbackTests without overwriting the existing `Assets/Editor/PlayModeTestRunner.cs` or prior TowerAnimationFeedbackTests.
   - Use a distinct SessionState key prefix for this test bootstrap to avoid the previously observed objective-runner interference; capture completion before result retrieval so read commands do not interrupt Play Mode. Restore original scenes and remove temporary fixtures/screenshots after analysis.
   - First run deterministic accounting/projection/pulse checks; then isolated multi-frame UI/merge tests with runtime screenshots. No production save files, progress, level definitions, or monetization services should be used as test fixtures.
   - Dependencies: Steps 1–4.

# Verification & Testing
- **Core timing:** starting HUD 780, tier-3 merge award +30 => model immediately 810 while HUD stays 780. Popup pops/rises for approximately 1.0s, flies to score in about 0.25–0.30s, disappears on arrival, HUD becomes 810, and pulse completes once at baseline scale.
- **Scoring fidelity:** tier-1 single-neighbor => +20; tier-3 two-neighbor batch => +50 once; successive tier-2/tier-3 chain => separate +20/+30 popups and total +50. Include forced full-column merges and saved-board towers. No duplicate legacy subscriptions.
- **Concurrent accounting:** multiple outstanding flights, same-frame arrivals, overlapping chain/next-move flights, reordered completion, and long frames all produce model total equal to collected display plus pending points. No omissions, negative totals, repeated callbacks, or permanently lagging record display.
- **Gameplay/persistence:** legal next move is not delayed by cosmetic flights. Save/pause/app exit during flight records full authoritative points; restore shows that saved total immediately without replay. Score objectives evaluate using the actual total and terminal state blocks input, while result panels wait until flights finish. Milestone screens also wait without losing their trigger.
- **HUD consistency:** both score writers, record label, and campaign score-progress rows use consistent displayed values; unrelated refresh calls do not jump ahead. Start targets and tower-level progress remain unchanged.
- **Motion/projection:** rise is screen-up regardless of board orientation/drift; initial popup originates from the correct merge and flight reaches the actual top number. Test edge columns, different tower heights, tall/small portrait aspect ratios, safe-area/layout changes, and camera punch/drift during spawn. Popups remain readable and inside reasonable screen bounds.
- **Pulse:** fast single pulse per arrival burst, exact baseline recovery, no cumulative scale, no layout shift, and no pulse on plain placement or load. Test arrival during an existing pulse and large score values.
- **Lifecycle:** pause/resume, disabled/missing presenter or target, zero durations, reset/ClearBoard, retry/next level, menu exit, scene teardown, and rebind all clean up flights, pooled objects, subscriptions, and deferred panels. Missing presentation falls back to immediate display instead of waiting forever.
- **Input/performance:** no popup raycast interception, no added font/atlas bloat, no per-frame scene lookup/material creation, bounded reusable pool growth, and no unnecessary full-HUD Canvas rebuilds. Preserve existing mobile UI and tutorial interaction.
- **Validation:** compilation with no new errors; deterministic and multi-frame checks with explicit reported results; inspect runtime screenshots for readable popups, target alignment, and no HUD/modal occlusion. Device/iOS-build verification only when available; report unavailable or interrupted checks rather than claiming success.

---

# Turn Merge Multiplier & Flying Multiplier Tokens — Current Requested Scope

Implement only this extension for the current request; do not repeat the older tutorial/score-popup work. Read actual files before implementation. Preserve the latest saved camera/score tuning, not the outdated values in prior sections: instant 6% merge zoom with 0.20s return; stabilized X/Z drift at translation 0.03, rotation 0.4 degrees, frequency 0.35; 0.15s stationary horizontal-chain pause; score popup rise 0.30s over 45 UI units, flight 0.15s, text 44pt with shared shadow material; counter pulse 1.20x over 0.15s. Preserve SingleEventSystemGuard.

# Project Overview
- Game Title: City Blocks.
- High-Level Concept: A single-player city-themed tower-merging puzzle. Reward successive merge batches within one turn with escalating score multipliers, made readable by a pulsing board-corner badge and a miniature multiplier flying into each affected score popup before score collection.
- Players: Single player.
- Inspiration / Reference Games: Existing 2048 / Triple Town references; escalating chain/combo feedback rather than new board rules.
- Tone / Art Direction: Existing colorful low-poly city and gold, shadowed score feedback. Compact, legible multiplier box; no broad HUD redesign.
- Target Platform: iOS.
- Screen Orientation / Resolution: Portrait; retain actual CanvasScaler reference/layout and validate small/tall iPhone aspects. No hard-coded screen-resolution positioning.
- Render Pipeline: URP-HighFidelity, Unity 6000.4.5f1. No pipeline, lighting, shader-source, or Volume changes.

# Game Mechanics
## Core Gameplay Loop
Make a legal placement, resolve all resulting merge batches and gravity, collect points, then begin the next turn. A turn-local combo rewards extended chains without carrying a multiplier to another placement.

### Confirmed User Decisions
1. **One completed merge batch counts as one merge.** Simultaneously absorbing multiple neighbors increments the multiplier once, consistent with current merge/score/camera feedback. Each subsequent chain batch increments it again.
2. **Only that merge receives its multiplier.** Earlier awards are not recalculated or multiplied retroactively.
3. Preserve the previously selected internal-immediate/visible-delayed score architecture: calculate and credit the multiplied award internally at merge completion; visually apply it to the popup when the miniature multiplier arrives, then collect it into the top score HUD.

### Scoring and Turn Contract
- Valid turn begins before the accepted tower starts resolving; merge count starts at zero.
- First completed batch: multiplier 1, no visible x1 box, ordinary base-score popup.
- Second batch: show **x2** in the badge, pulse it, and launch a smaller x2 toward that batch's score popup.
- Third and later batches: x3, x4, etc.; pulse and launch a matching token on each increment. No cap unless the existing numeric storage requires safe handling.
- Award formula: `basePoints = resultingTowerLevel * 10`; `finalPoints = basePoints * mergeIndexWithinTurn` (first batch uses 1).
- Example: resulting levels 2, 3, 4 across three batches yield +20, +60, +120, totaling **200**; not a retroactive multiplier on all turn points.
- Reset logical combo state when the entire board resolution finishes (`grid.IsSettled` / `onMoveResolved`), not after a short time gap. Existing horizontal-chain waits and falling obstacles must not reset it prematurely.
- A board-changing booster that triggers resolution gets its own resolution-scoped combo (same semantics as a turn); its initial non-merge tier increase does not increment the combo. Look-ahead, extra turns, rejected input, and obstacle damage do not create or increment a merge combo.
- After turn end, finish any already-started badge pulse, then hide/reset the badge without extending gameplay settling. Already-launched multiplier tokens and score popups retain immutable turn/award snapshots and finish independently. A new turn resets the badge immediately; old tokens cannot change the new turn's counter.
- Fresh run, loaded run, retry, and scene exit start/reset with no combo badge or replayed tokens. Save the fully credited logical score including already-earned multipliers using existing save paths; no cosmetic token/combination-state save schema. Do not redesign the project's existing handling of interrupted, unfinished board cascades.
- Score objectives use the multiplied logical total and may therefore be achieved sooner. Do not alter level targets or rebalance authored levels without a separate request.

## Controls and Input Methods
Keep Legacy Input Manager controls, legal-placement validation, existing move limits, and power-up targeting. The new badge/tokens are non-interactive; disable graphic raycasts and CanvasGroup input on them. Keep exactly one working EventSystem through the existing guard; no additional EventSystem, GraphicRaycaster, or input package.

# UI
## Initial Layout / Motion Storyboard
```
+----------------------------------------+
| SCORE total           existing HUD      |
|                                        |
|             grid upper-right [ x2 ]     |
|                              pulse      |
|                              |          |
|                  smaller x2  |          |
|                      \       |          |
|                 +30  <- x2 arrives      |
|                  -> +60                 |
|                    ^ short rise         |
|                    \ -> top SCORE       |
|                         score pulse      |
|           existing power-up buttons     |
+----------------------------------------+
```
- Add a compact badge initially about **64x40 UI reference units**, 28pt gold text, dark rounded background reusing existing UI art. Position it over the **player-visible upper-right corner area of the grid**, with a small configurable inset. This is initial tuning, not a fixed-resolution absolute coordinate.
- Project the actual board's corner bounds using GameController.GameplayCamera and current GridController dimensions/WorldOffset. Determine top/right in screen space, not from logical row names: this camera projects the logical top-entry row below the board. Reposition after board fit/layout changes and follow subtle camera drift without per-frame scene searches.
- Use the existing screen-space feedback Canvas/layer. Keep the badge outside PlayPanel and layout groups that would override its projected positioning. Clamp within the Canvas/safe display area without overlapping the top score or power-up HUD.
- Badge increments use the **same pulse equation, amplitude, and duration as the score counter** (current 1.20x, 0.15s); restart from current scale without additive growth. Share the existing pulse calculation/tuning rather than introducing a tween package.
- Miniature tokens: about **24pt**, same font/gold shadowed style, visibly smaller than the 44pt score popup. Text is x2/x3/etc., not a separate point award.
- Token starts at the badge center, flies to its own moving/rising score popup (initial duration about 0.15s), disappears on contact, and changes popup text from the base amount to the final multiplied amount. A subtle popup pop on application may be used only if it does not change collection timing/readability.
- Token motion overlaps the 0.30s rise. A multiplied popup must not begin its final flight to the score HUD before its token has applied; if timing/layout makes that necessary, hold only that popup at its rise endpoint. Never block the board or launch the token toward the main HUD instead.
- The score HUD still increases only when the final score popup arrives, with its existing fast pulse. Pending visual accounting always reserves **final** multiplied points, even while the popup temporarily displays the base amount.

# Key Asset & Context
## Existing Assets to Modify
- `Assets/Scripts/Gameplay/GameController.cs`: add resolution-local merge count/turn identity and combo lifecycle notifications. Begin only after validating/accepting placement or board-changing booster use. `AwardMergeScore(int, Vector3)` increments the batch count when a combo turn is active, computes final points once, immediately updates score/record, and passes base/multiplier/final information to presentation. End/reset in SaveLevelAfterMove after board settling and before the next turn can begin; reset on existing load/run-reset paths.
- `Assets/Scripts/UI/ScoreFeedbackController.cs`: extend Enqueue and pooling/accounting for score awards carrying base, final points, multiplier, and turn/award identity. Track multiplier tokens as pending feedback; coordinate token arrivals, popup gates, badge updates, and reset. Keep all normal score writers/records/objective presentation on the existing delayed-display values.
- `Assets/Scripts/UI/MergeScorePopup.cs`: separate immutable collected Points from initially displayed base points. Add an exactly-once multiplier-application operation and a flight gate, reset all state on pool reuse/cancel. Preserve normal x1 popup timing and shadow/font styling.
- `Assets/Scripts/UI/LevelRulesUI.cs`: bind the new badge through the existing GameController/presenter initialization if required; keep existing deferred terminal overlays waiting for all relevant feedback and the final score pulse.
- `Assets/Scenes/EndlessMode.unity`: wire the badge, miniature token template, explicit gameplay camera, and presenter references into the current standalone HUD; preserve camera, input, lighting, and unrelated UI.
- `Assets/Prefabs/UI/LevelRulesUI.prefab`: equivalent reusable badge/presenter hierarchy and bindings; no scene-object references serialized into prefab assets.
- `Assets/Editor/Tests/ScoreFeedbackTests.cs` and/or proposed combo tests: update accepted-turn chain totals (the old +20/+30 = 50 chain becomes +20/+60 = 80). Keep ordinary direct-award/no-active-turn checks at x1. Do not overwrite existing objective-card test scripts.

## Assets to Create
- `Assets/Scripts/UI/MergeMultiplierCounter.cs`: projected badge positioning, show/increment/reset, and score-equivalent pulse presentation, with cached Game/Grid/camera/RectTransform references.
- `Assets/Scripts/UI/MergeMultiplierToken.cs`: pooled small multiplier flight element, tied to a unique score-popup/award handle, with completion/cancel semantics and moving target tracking.
- `Assets/Prefabs/UI/MergeMultiplierCounter.prefab` and `Assets/Prefabs/UI/MergeMultiplierToken.prefab`: existing font/shadow material and rounded-rect art, fixed readable sizes; no font atlas duplication or input interception.
- `Assets/Editor/Tests/MergeMultiplierTests.cs` if a separate harness is cleaner: deterministic lifecycle/accounting/UI geometry tests and optional isolated runtime visual verification with a distinct state prefix.

## Inspected Context / Impact
- GameController awards score centrally after both standard and forced top merges; saved towers now use that same position-aware path. One new combo increment belongs there, not in per-neighbor removal or the camera's start event.
- SaveLevelAfterMove waits for grid.IsSettled before unlocking input and firing onMoveResolved. Existing chain delays/obstacle motion are part of this contract; visual flights are not.
- Existing ScoreFeedbackController pending ledger subtracts pending final points from game.score; extend this accounting rather than creating an unrelated score accumulator.
- Existing popup is a pooled screen-space TMP element with independent timed rise/flight. Never let a token mutate a pooled popup belonging to another award after reset/reuse.
- Read-only AssetDatabase dependency queries identified scene/prefab dependents for GameController, GridController, LevelRulesUI, ScoreFeedbackController, and MergeScorePopup, including recovery scenes. Do not edit recovery scenes directly.
- Recheck dependencies before each implementation edit, including new prefab references. No package, backend, font-source, EventSystem, rendering-pipeline, or authored-level changes.

# Implementation Steps
1. **Implement turn-local multiplier accounting**
   - Modify GameController's accepted-action, AwardMergeScore, settling, and reset/load paths.
   - Keep combo counting independent of cosmetic flight duration; first batch x1, next batch x2, etc. Turn identity must be stable through gravity and horizontal pauses.
   - Make ordinary compatibility/direct awards outside an accepted resolution remain x1, avoiding stale multipliers during initialization or restoration.
   - Compute base/final points once with safe numeric intermediates; prevent overflow/wraparound in new multiplier math while retaining current storage types. Do not retroactively modify earlier awards.
   - Dependencies: inspected existing scoring/turn flow and user decisions.
2. **Build badge and token presentation**
   - Create the counter/token scripts and prefabs; add the small projected grid-corner badge to the existing feedback layer.
   - Reuse score pulse math/current tuning for the badge and reuse existing static font/shadow material. Pool tokens; no per-frame font/material creation or scene-object lookup.
   - Project current grid bounds into screen-space Canvas coordinates, using explicit cached camera references and configurable inset; preserve player input through the badge/tokens.
   - Dependencies: Step 1 and existing HUD assets.
3. **Apply multiplier visually before score collection**
   - Modify MergeScorePopup and ScoreFeedbackController to spawn a base-text popup while reserving final points, then launch the appropriate token for multipliers >=2.
   - Track independent award/turn handles and update the token's target as the popup rises. On exactly-once contact, show final points, hide/recycle the token, and allow score flight after the rise phase.
   - Preserve unscaled visual timing and existing score collection/pulse behavior. Missing/disabled token presentation must apply the multiplier visually immediately rather than strand points or change model scoring.
   - Dependencies: Steps 1–2.
4. **Complete lifecycle and prefab/scene wiring**
   - Bind the badge and templates in standalone scene and reusable UI prefab, including initial upper-right grid positioning.
   - Reset logical counter at board completion and hide/reset the badge after any short pulse finishes; do not cancel valid old score/token flights. New turn/rebind/retry/load/unload clears or isolates old handles appropriately.
   - Extend HasPendingFeedback for multiplier flights/pulses where needed so terminal panels do not occlude collection. Missing targets, app suspension, disabled presenter, and cancellation all synchronize displayed totals without a second model award.
   - Dependencies: Steps 1–3; impact recheck before asset edits.
5. **Verify gameplay rewards, presentation, and regression safety**
   - Add deterministic tests first, then isolated multi-frame tests if the editor supports reliable entry. Use temporary/in-memory fixtures, independent state keys, completion detection, and safe scene restoration.
   - Update old accepted-turn score expectations intentionally for the new scoring rule; do not weaken assertions to hide failures. Keep unrelated camera/single-EventSystem checks.
   - Inspect multiplier positioning/pulses and token-to-popup application visually, including portrait sizes and overlapping turns. Report unavailable/interrupted runtime/device checks explicitly rather than repeating unsafe Play Mode entry attempts.
   - Dependencies: Steps 1–4.

# Verification & Testing
- **Counting:** no badge/x1 on first batch; one x2 increment for the second batch; x3/x4 thereafter. Simultaneous two/three-neighbor absorptions count once. Include full-column top merges and cascades across different survivors/columns.
- **Scoring:** three batches with base points 20/30/40 produce 20/60/120 (total 200). Two-batch chain produces 20+60=80. HUD collection amounts, internal score, record, saved total, and score-objective evaluation agree; earlier awards never change retroactively.
- **Token ordering:** x2 badge pulses; small x2 travels from badge to its own +30 popup, popup becomes +60 before flying to main score. No mini token for x1. Gate collection until multiplier application, including zero/long durations and large frame steps.
- **Concurrent feedback:** multiple popups/tokens, out-of-order arrivals, new turns while old flights finish, hidden badge after resolution, and pooled element reuse retain correct identities/amounts. No duplicate multiplication, wrong-popup application, missing points, or old-token changes to a new badge.
- **Turn reset:** horizontal waits and gravity gaps do not reset combo. Settled board ends it. Next placement starts at x1. Invalid clicks and non-merging utility boosters do not increase it. Board-changing boosters start their own combo; initial booster tier change alone does not count.
- **Lifecycle/save:** pause during an active resolution does not accidentally restart the combo; already-earned multiplied totals persist via normal score saves. New/loaded/retried runs show no stale counter or token replay. Preserve existing unfinished-cascade interruption behavior rather than claiming a redesigned resume system.
- **Layout/readability:** badge remains over the visible upper-right grid area for 3–7 row/column boards, holes/obstacles, camera fit/drift/punch, and small/tall portrait layouts. Token is clearly smaller than score popup; neither intercepts touches, overlaps important HUD elements, or clips on reset/pulse.
- **Performance:** bounded pooled token reuse, shared font/materials, fixed popup font sizes, cached references, no new EventSystem or per-frame scene searches, and no needless whole-HUD rebuilds.
- **Validation:** zero new compile errors; explicit deterministic/runtime results and visual analysis where available; no changes to user saves/progress or real monetization services as fixtures. State the limits of any blocked Play Mode or device verification.

---

# Endless Level-Up Board Cleanup — Current Requested Scope

Implement only this extension. Preserve previous work, including rejection/reroll of queue levels below 1. Do not repeat older implementation steps.

# Project Overview
- Game Title: City Blocks.
- High-Level Concept: A city-themed tower-merging puzzle. In Endless mode, advancing the highest achieved tower tier clears obsolete board towers below the current valid spawn minimum, freeing space for continued progression.
- Players: Single player.
- Inspiration / Reference Games: Existing project references 2048 and Triple Town; no new design direction.
- Tone / Art Direction: Preserve existing colorful low-poly towers and motion.
- Target Platform: iOS.
- Screen Orientation / Resolution: Portrait; preserve existing layouts (prior reference 1080x1920).
- Render Pipeline: URP-HighFidelity; Unity 6000.4.5f1.

# Game Mechanics
## Core Gameplay Loop
Finish a placement or board-changing booster cascade. If that action increases the player's highest Endless tower level, remove board towers strictly below the valid spawn minimum, settle affected columns, and finish any resulting merges before resolving/saving the action.

### Confirmed Behavior
- User selected **After the current cascade** rather than immediate removal at each merge boundary. This preserves existing cascade rewards and readable cause-and-effect (MDA).
- Valid minimum at highest achieved level L: `L <= 10 ? 1 : Mathf.Max(1, L - 5 - Mathf.RoundToInt(L / 2f))`. The clamp reflects the existing reroll rejection of nonpositive towers; do not use raw 0 at L=11.
- Examples: L=12 minimum 1; L=13 minimum 2 (remove level-1 towers); L=17 minimum 4 (remove levels 1–3); L=20 minimum 5 (remove levels 1–4). Towers equal to the minimum survive.
- Trigger on an actual increase of `game.level`, including booster-only increases that produce no merge. Upgrading a tower without exceeding the historical highest level does not trigger cleanup.
- Campaign mode never performs this cleanup. Queue entries are not removed/rerolled for falling below the new spawn minimum; the existing queue floor-of-1 guard remains unchanged.
- Removal itself grants no score, combo increment, obstacle damage, camera merge pulse, or additional move/booster consumption. Actual gravity-triggered merge batches retain normal scoring and the same resolution-scoped combo.
- If post-cleanup gravity/merges raise the highest level again, repeat cleanup at the next settled boundary using the latest minimum until no cleanup remains pending.

## Controls and Input Methods
Keep Legacy Input Manager touch/click handling and current boosters. Input remains blocked by `moveResolving` through cleanup and its gravity/merge consequences. No new controls or input backend changes.

# UI
No new UI assets, panels, or effects. Preserve current score flights, multiplier badge, and deferred milestone/loss panels.

```
Existing cascade completes
          ↓
Remove board towers with tier < current minimum
          ↓
Existing gravity slides and any resulting merges
          ↓
Repeat if highest tier increased again
          ↓
One move-resolved notification → loss/milestone checks → save
```

# Key Asset & Context
- Modify `Assets/Scripts/Gameplay/GameController.cs`: `IncrementLevel(int)` currently tracks historical highest tier; `ApplyLevelUpPowerUp()` directly increments a tower but does NOT currently update historical level unless a subsequent merge event occurs. Fix that missing notification. `SaveLevelAfterMove()` waits for settlement before ending combo/unlocking input; insert cleanup processing before that finalization. `Update()` checks transitions only when `!moveResolving && grid.IsSettled()`; preserve this guard.
- Modify `Assets/Scripts/Gameplay/GridController.cs`: logical occupancy is `grid[,]`; obstacles occupy a separate array. `ResolveGravity(int, bool)` handles falling obstacles and marks unsupported towers for dropping. `IsSettled()` includes merge ownership, moving towers and obstacles. `CaptureBoardState()` maintains `lastIntactBoardState` for scene teardown. Add targeted low-tier removal, not `ClearBoard()`.
- Read-only `Assets/Scripts/Gameplay/TowerController.cs`: merge events occur after absorption, while the surviving tower still owns its coroutine. Do not delete board pieces directly from those events. `OnDisable()` stops routines and removes tracking; clear occupancy before disabling/destroying removed towers. No TowerController changes expected.
- Create `Assets/Editor/Tests/EndlessLevelCleanupTests.cs`: follow existing editor-hosted harness conventions; use isolated fixtures and a distinct SessionState prefix. Do not overwrite `PlayModeTestRunner.cs` or existing animation/score tests.
- Inspected loaded scene connections: `Game` references `Grid`, `Queue`, and `Tower` prefab. AssetDatabase dependency/dependent queries identify GameController/GridController dependents as EndlessMode and recovery scenes, and TowerController dependents as Tower prefab, EndlessMode, and recovery scene. No scene/prefab/level asset edits expected; never directly edit recovery scenes.
- Recheck dependencies/dependents before implementation edits, with the dependency tool if available or read-only AssetDatabase queries otherwise. No package, rendering, save schema, backend, monetization, or UI changes.

# Implementation Steps
1. **Expose deterministic Endless spawn bounds**
   - File: GameController.cs. Extract the existing raw min and exclusive max arithmetic into a deterministic helper used by `GetTowerSpawnLevelRange()`. Expose/derive the valid minimum as max(1, raw min), keeping the existing random distribution and reroll loop intact. Do not change upper-bound semantics or rounding.
   - Dependencies: inspected current spawn generation and queue guard.
2. **Track actual highest-level increases, including boosters**
   - File: GameController.cs. On an actual Endless increase in `IncrementLevel`, mark cleanup pending; do not delete during merge callbacks. In `ApplyLevelUpPowerUp`, call `IncrementLevel(towerCtrl.level)` after the direct increment so booster-only upgrades track historical level/record/milestone correctly in both modes; only Endless sets cleanup pending.
   - Clear pending state on load, reset/retry and campaign configuration to avoid stale work. Do not retroactively prune a loaded board merely because it contains old low towers; this request triggers on new level increases.
   - Dependencies: Step 1.
3. **Add safe targeted board pruning**
   - File: GridController.cs. Add a method such as `RemoveTowersBelowLevel(int minimumLevel)` that scans logical board occupancy, collects affected columns, clears each matching cell before disabling/destroying its tower, and removes stale tracking. Preserve all towers at/above the minimum, obstacles, unavailable cells, board dimensions and queue.
   - Only invoke when the board is settled. Include safe handling of null/uninitialized boards and temporary entry occupancy. Resolve gravity once per affected column after all deletions. Refresh the intact board snapshot after occupancy changes so teardown fallback cannot resurrect removed towers; final normal capture/save records settled coordinates.
   - Do not fire `onBoardCleared`, which cancels unrelated score feedback. Use runtime Destroy/editor DestroyImmediate appropriately for fixtures.
   - Dependencies: Steps 1–2; existing settlement and disable contracts.
4. **Integrate repeated cleanup into action resolution**
   - File: GameController.cs. In SaveLevelAfterMove, wait for the natural cascade to settle, then process pending Endless cleanup before EndComboTurn, unlocking input, onMoveResolved, loss/milestone checks and persistence.
   - Consume the pending flag before pruning so further level increases during gravity can mark a new pass. Yield through gravity/merges and recheck settlement/pending work until complete. A pass with zero removals must finish without an unnecessary loop or stuck lock.
   - Keep the existing combo active throughout actual resulting merges. Ensure single finalization and no duplicate save/notification. Preserve score-flight deferral and all campaign flow.
   - Dependencies: Steps 2–3.
5. **Verify isolated boundaries, cascades and regressions**
   - File: new EndlessLevelCleanupTests.cs; production changes only to correct evidenced failures. Use temporary/in-memory fixtures without modifying player saves, real booster balances, or authored levels. Restore editor scene setup and clean test fixtures.
   - Dependencies: Steps 1–4.

# Verification & Testing
- Compile with no new errors. Confirm extracted bounds match current raw spawn arithmetic at L=0–50 and valid minimum never drops below 1; retain queue reroll tests at L=11.
- At 12→13, let the initial cascade finish, then remove every remaining level-1 board tower while preserving level-2+. At 16→17 remove levels 1–3; at 19→20 remove levels 1–4. Test a jump across multiple thresholds and ties-to-even rounding at odd L.
- A level increase with unchanged minimum still safely processes the current floor. No increase in highest tier means no cleanup. Level 11 uses minimum 1, not raw 0.
- Booster-only highest-tier increase without matching neighbors updates game.level/record and triggers cleanup. Booster on a tower below the historical maximum does not. Verify actual booster pathway in isolated fixtures without spending the user's inventory.
- Verify pruning happens AFTER the original cascade, preserving its original merges/awards; all low towers are removed together, then gravity settles using current motion. Equal-minimum/high towers survive, and upper towers drop to correct logical/world coordinates.
- Cleanup alone adds no points, combo count, merge pulse, damage, moves or booster consumption. Resulting real merges score normally and keep the action's combo; an additional highest-level increase causes a fresh cleanup pass.
- No missing references, stale towersInPlay entries, merge ownership deadlocks, orphaned labels/proxies, or terminal checks before cleanup. onMoveResolved and final save occur once after final settlement. A previously full board can become playable before loss evaluation.
- Save capture and teardown fallback omit removed towers; load retains the resulting board. Reset/retry/configure clears pending state. Test empty board/no matches/null grid and repeated calls.
- Campaign starting towers, objectives, obstacle damage, queues and all boosters retain existing behavior except the corrected direct booster highest-tier notification. Old queued towers below the new Endless minimum remain queued as explicitly scoped.
- Run existing animation/score/combo checks where available. Report unavailable Play Mode/device/iOS build validation honestly; no testing has been performed for this new feature during planning.
