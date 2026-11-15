# Project Overview
- Game Title: City Blocks
- High-Level Concept: A 3D mobile puzzle city-building game where players drop and merge tiered buildings on a grid, chaining combos while managing board space and move limits.
- Players: Single player
- Inspiration / Reference Games: CityBlocks, 2048, Triple Town, Royal Match
- Tone / Art Direction: Vibrant low-poly 3D city aesthetics with high-contrast, juicy mobile UI and celebratory visual feedback.
- Target Platform: iOS and Android
- Screen Orientation / Resolution: Portrait 1080x1920
- Render Pipeline: Universal Render Pipeline (URP-HighFidelity)

# Game Mechanics
## Core Gameplay Loop
Players place queued towers onto a grid column. When matching towers meet vertically or horizontally, they absorb into a higher-level tower and trigger gravity drops that can chain into subsequent merges within the same turn.
When a player places a tower from the queue and that single placement triggers **3 or more merges in a row** (`mergeCount >= 3`), the game waits until all merging and gravity drops on the board have completely settled (`grid.IsSettled()`), and then triggers a **Fullscreen Combo Celebration Takeover** before resuming normal play or displaying end-of-turn modals (Level Up / Win / Loss).

## Controls and Input Methods
- **Normal Play**: Tap/click on 3D grid columns to place the next tower from the queue.
- **Fullscreen Celebration Takeover (Auto-Dismissing Spectacle)**:
  - Activates automatically once the 3+ merge chain finishes settling, starting with a **0.20s start delay** (`_startDelay = 0.2f`) before the visual spectacle erupts.
  - Temporarily blocks board/power-up input across the delay and the extended celebration duration (`_totalDuration = 2.25f`, extended by `0.8s` from `1.45s`) so the player can enjoy the reward moment without accidental taps.
  - Automatically fades out and restores full board and HUD interactivity once complete.

# UI
## Fullscreen Celebration Takeover Wireframe & Visual Hierarchy
```
+---------------------------------------------------+
| [Full-Screen Dimmed Backdrop (Alpha 0 -> 0.58)]   |
|                                                   |
|  ///////////////////////////////////////////////  |
|  [Scrolling Yellow/Black Caution Ticker Tape Top] |
|  ///////////////////////////////////////////////  |
|                                                   |
|     *   +---------------------------+    .        |
|         |        AWESOME!           |  *          |
|         |  (Auto-Fit Screen Bounds, |             |
|         |   Gold Gradient + Shadow) |             |
|         +---------------------------+             |
|                                                   |
|  \\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\  |
|  [Scrolling Yellow/Black Caution Ticker Tape Bot] |
|  \\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\  |
|       [UI Confetti / Sparkle Burst VFX]           |
+---------------------------------------------------+
```

### Visual Elements & Animation Choreography (0.20s Start Delay + 2.25s Celebration Duration)
1. **Start Delay (`_startDelay = 0.2f`)**:
   - After `grid.IsSettled()`, waits `0.2s` before displaying the takeover visuals and firing the camera punch, while keeping `IsCelebrating` (and `HasPendingComboCelebration`) true so no premature moves or win/loss modals interrupt.
2. **Fullscreen Dim & Input Blocker (`CanvasGroup` + `Image`)**:
   - Stretches across the entire screen (`AnchorMin (0,0)`, `AnchorMax (1,1)`).
   - Blocks raycasts while the celebration is active (`blocksRaycasts = true`), preventing unintended column taps or booster presses.
   - Smoothly fades backdrop alpha from `0 -> 0.58` over `0.18s`, holds through the extended `2.25s` celebration window, and fades out over `0.30s`.
3. **Construction Caution Tape / Ticker Tape Aesthetic (Replaces Spinning Sunburst)**:
   - Removes the spinning radial sunburst completely.
   - Replaces it with top and bottom angled construction caution / ticker-tape ribbons (bold hazard yellow `#FFCC1A` bands with dark asphalt/black `#1A1712` borders and scrolling diagonal hazard stripes / construction ticker blocks) that slide and unfurl across the screen in opposite directions above and below the headline.
4. **Congratulatory Headline Only (`TMP_Text`) — Tiered by Chain Length, Guaranteed Screen Fit**:
   - **Combo length text (`"xN COMBO!"`) is removed** — only the congratulatory word is shown.
   - **3 Merges**: `"AWESOME!"` / `"GREAT!"` / `"SUPER!"`
   - **4 Merges**: `"AMAZING!"` / `"SPECTACULAR!"` / `"FANTASTIC!"`
   - **5+ Merges**: `"UNBELIEVABLE!"` / `"LEGENDARY!"` / `"INCREDIBLE!"`
   - Uses responsive auto-sizing and dynamic horizontal margin/scale clamping so even the longest words (`"UNBELIEVABLE!"`, `"SPECTACULAR!"`) combined with the elastic overshoot (`1.22x`) are strictly clamped within the screen width on all mobile aspect ratios (including narrow `9:19.5` and `9:16` portrait screens) with zero clipping or wrapping.
5. **Screen-Space Celebration VFX (UI Confetti & Sparkle Burst)**:
   - Procedural/UI particle burst of 28–36 colorful celebratory shards/sparkles (`RectTransform` + `Image` pool inside the overlay layer) that erupt outward from center with initial velocity, gravity, spin, and alpha fade, plus camera punch via `BoardCameraFeedback.PunchMerge()` when the delay completes.

# Key Asset & Context

### New Scripts to Create
1. `Assets/Scripts/UI/ComboCelebrationController.cs`:
   - Manages the fullscreen combo celebration lifecycle, `0.2s` start delay, `2.25s` celebration duration (extended by `0.8s`), tiered word selection (without combo count text), screen-bounds headline fitting, scrolling construction caution/ticker-tape ribbons, UI particle burst pool, and `IsCelebrating` state.
   - Key API:
     ```csharp
     namespace UI
     {
         public class ComboCelebrationController : MonoBehaviour
         {
             public bool IsCelebrating { get; }
             public bool IsInStartDelay { get; }
             public float StartDelay { get; }
             public float TotalDuration { get; }
             public string CurrentHeadline { get; }
             public int LastCelebratedMergeCount { get; }
             public void Bind(GameController game);
             public void TriggerCelebration(int mergeCount);
             public void Advance(float delta);
             public void ClearImmediate();
             public static string SelectCongratulatoryWord(int mergeCount, int variantIndex = 0);
         }
     }
     ```

### Existing Scripts to Modify
1. `Assets/Scripts/Gameplay/GameController.cs`:
   - Track whether the current combo turn originated from a normal tower placement (`_isPlacementComboTurn`).
   - In `PlayTower(...)`, start the placement combo turn (`BeginComboTurn(isTowerPlacement: true)`).
   - In `ApplyDeletePowerUp(...)` and `ApplyLevelUpPowerUp(...)`, call `BeginComboTurn(isTowerPlacement: false)`.
   - In `SaveLevelAfterMove()`:
     - After `yield return new WaitUntil(grid.IsSettled);`, capture `int completedMerges = _turnMergeCount;` and `bool wasPlacementTurn = _isPlacementComboTurn;` before calling `EndComboTurn()`.
     - If `wasPlacementTurn && completedMerges >= 3`, invoke `onComboCelebrationRequested?.Invoke(completedMerges)` (or trigger bound `ComboCelebrationController`) and wait while `HasPendingComboCelebration` is true before completing `moveResolving = false` and evaluating `HandleLevelUpCheck()` / `HandleLossCheck()` / `LevelSession.ResolveMove()`.
     - Alternatively, expose `public bool HasPendingComboCelebration` and include it in `HasPendingScoreFeedback` / move resolution gating so win/loss/level-up panels wait until the celebration completes.
2. `Assets/Scripts/UI/ScoreFeedbackController.cs` (or `LevelRulesUI.cs`):
   - Reference and bind `ComboCelebrationController` alongside score feedback so `HasPendingFeedback` (or `GameController.HasPendingScoreFeedback`) remains true while `ComboCelebrationController.IsCelebrating` is active, and `ClearAndSynchronize()` resets any active celebration on retry/menu/load.
3. `Assets/Scripts/Gameplay/BoardCameraFeedback.cs`:
   - Trigger a celebratory camera pulse when the combo celebration erupts.

### Prefabs & Scenes to Create / Modify
1. `Assets/Prefabs/UI/ComboCelebrationOverlay.prefab`:
   - Prefab containing the `ComboCelebrationController`, fullscreen dimmed `Image`, `CanvasGroup`, top and bottom construction caution/ticker-tape banners (`TopCautionTape`, `BottomCautionTape` with hazard stripes), `CongratulatoryHeadline` (`TextMeshProUGUI` with screen-fit sizing and no combo length subtitle), and confetti/sparkle particle container.
2. `Assets/Prefabs/UI/LevelRulesUI.prefab` & `Assets/Scenes/EndlessMode.unity`:
   - Instantiate and wire `ComboCelebrationOverlay` under `LevelRulesUI` (above `PlayPanel` and `ScoreFeedbackLayer`, below pause/win/loss modals) and bind references on `ScoreFeedbackController` / `GameController`.

### Editor Tests to Create / Update
1. `Assets/Editor/Tests/ComboCelebrationTests.cs`:
   - Automated Editor test suite (`[MenuItem("Tools/City Blocks/Tests/Combo Celebration Checks")]`) verifying:
     - 1 or 2 merges on a tower placement do **not** trigger celebration.
     - 3, 4, and 5+ merges on a single tower placement trigger celebration only **after** `grid.IsSettled()`.
     - The `0.2s` start delay holds visuals hidden while blocking premature turn transitions, then erupts into the `2.25s` celebration (extended by `0.8s`).
     - Power-up merges (Delete / Level-Up) with 3+ merges do **not** trigger the tower-placement celebration.
     - Tiered congratulatory words match the merge count, and no combo length text is shown.
     - All congratulatory words (including `"UNBELIEVABLE!"` and `"SPECTACULAR!"`) fit strictly within screen bounds even at peak overshoot scale.
     - Spinning sunburst is absent and replaced by construction caution/ticker-tape banners that animate horizontally.
     - Fullscreen takeover blocks input during celebration and auto-dismisses after `_startDelay + _totalDuration`.
     - Reset / Retry / ClearAndSynchronize immediately clears any active celebration state.

# Implementation Steps

### Step 1: Track Placement Merge Chains and Celebration Hook in `GameController`
- **Files**: `Assets/Scripts/Gameplay/GameController.cs`
- **Details**:
  - Add `_isPlacementComboTurn` flag to distinguish `PlayTower` turns from power-up turns (`ApplyDeletePowerUp`, `ApplyLevelUpPowerUp`).
  - Add `public event System.Action<int> onPlacementComboCompleted;` fired in `SaveLevelAfterMove()` once `grid.IsSettled()` is true and prior to `EndComboTurn()`, when `_isPlacementComboTurn && _turnMergeCount >= 3`.
  - Wait for any active combo celebration to finish (via `HasPendingScoreFeedback` and `HasPendingComboCelebration`) before transitioning to `levelUp`, `win`, or `lose` panels.
- **Dependencies**: None

### Step 2: Update `ComboCelebrationController` with 0.2s Start Delay, +0.8s Extended Duration, Screen-Fit Headline Only, and Caution/Ticker Tape Aesthetic
- **Files**: `Assets/Scripts/UI/ComboCelebrationController.cs`
- **Details**:
  - Add `_startDelay = 0.2f` so the visual takeover waits `0.2s` after settlement before appearing, and extend `_totalDuration` by `0.8s` (from `1.45s` to `2.25s`).
  - Remove the combo length subtitle (`"xN COMBO!"`) so only the congratulatory word is displayed.
  - Ensure all congratulatory words fit strictly inside the screen width at peak scale by clamping banner width and dynamically scaling/fitting long headlines (`"UNBELIEVABLE!"`, `"SPECTACULAR!"`) to the overlay rect with safe horizontal padding.
  - Remove the spinning sunburst effect and replace it with top and bottom angled construction caution / ticker-tape banners (`_topTapeBand`, `_bottomTapeBand`, `_topTapeStripeTrack`, `_bottomTapeStripeTrack`) that slide in and scroll hazard stripes across the banner.
- **Dependencies**: Step 1

### Step 3: Wire `ComboCelebrationController` into `ScoreFeedbackController` and `LevelRulesUI`
- **Files**: `Assets/Scripts/UI/ScoreFeedbackController.cs`, `Assets/Scripts/UI/LevelRulesUI.cs`
- **Details**:
  - Add `[SerializeField] private ComboCelebrationController _comboCelebration;` to `ScoreFeedbackController`.
  - Include `(_comboCelebration != null && _comboCelebration.IsCelebrating)` in `ScoreFeedbackController.HasPendingFeedback`.
  - Forward `Advance(delta)` and `ClearAndSynchronize()` to `_comboCelebration`.
  - Subscribe to `_game.onPlacementComboCompleted` to trigger `_comboCelebration.TriggerCelebration(mergeCount)` once merging is settled.
  - In `LevelRulesUI.RefreshPowerUps()`, ensure power-up buttons are non-interactable while celebration is active.
- **Dependencies**: Step 1, Step 2

### Step 4: Rebuild `ComboCelebrationOverlay.prefab` with Caution/Ticker Tape Aesthetic and Wire into `LevelRulesUI.prefab` and `EndlessMode.unity`
- **Files**: `Assets/Prefabs/UI/ComboCelebrationOverlay.prefab`, `Assets/Prefabs/UI/LevelRulesUI.prefab`, `Assets/Scenes/EndlessMode.unity`
- **Details**:
  - Rebuild `ComboCelebrationOverlay.prefab` removing `SunburstRays` and `SubtitleBadge`, adding `TopCautionTape` and `BottomCautionTape` (hazard yellow bands with dark trim and repeating diagonal black hazard stripes), and configuring `HeadlineLabel` with single-line auto-sizing and screen-fit margins.
  - Update the prefab instance in `LevelRulesUI.prefab` and `EndlessMode.unity`.
- **Dependencies**: Step 2, Step 3

### Step 5: Update Automated Verification Suite (`ComboCelebrationTests.cs`)
- **Files**: `Assets/Editor/Tests/ComboCelebrationTests.cs`
- **Details**:
  - Update editor tests to verify the `0.2s` start delay, the extended `2.25s` celebration duration, absence of combo length text, absence of spinning sunburst, presence and motion of construction caution/ticker tape, and guaranteed screen-bounds fit for every congratulatory word across narrow aspect ratios.
- **Dependencies**: Step 1, Step 2, Step 3, Step 4

# Verification & Testing
- Run `Tools/City Blocks/Tests/Combo Celebration Checks` via `EditorApplication.ExecuteMenuItem` to verify:
  1. Placing a tower that results in 1 or 2 merges does **not** activate the fullscreen takeover.
  2. Placing a tower that results in 3, 4, or 5+ merges activates the celebration after `grid.IsSettled()`, waiting `0.2s` (`_startDelay`) before visuals erupt, and running for the extended `2.25s` (`_totalDuration`).
  3. Power-up turns with 3+ merges do **not** trigger the celebration.
  4. Congratulatory headline text escalates by tier (3 merges vs 4 merges vs 5+ merges), displays **no** combo length text, and fits completely inside the screen bounds at peak overshoot scale.
  5. Construction caution / ticker-tape banners replace the spinning sunburst and scroll across the screen along with confetti VFX.
  6. Input (`blocksRaycasts` / move resolution) is blocked during the celebration and automatically restored when complete.
  7. Existing test suites (`Tools/City Blocks/Tests/Merge Multiplier Checks`) continue to pass without regression.
