# Project Overview
- Game Title: City Blocks
- High-Level Concept: A 3D mobile puzzle city-building game where players drop and merge tiered buildings on a grid, chaining combos while managing board space and move limits.
- Players: Single player
- Inspiration / Reference Games: CityBlocks, 2048, Triple Town, Royal Match
- Tone / Art Direction: Vibrant low-poly 3D city aesthetics with high-contrast, juicy mobile UI and tactile, responsive interface feedback.
- Target Platform: iOS and Android
- Screen Orientation / Resolution: Portrait 1080x1920
- Render Pipeline: Universal Render Pipeline (URP-HighFidelity)

# Game Mechanics
## Core Gameplay Loop
Players navigate from the Main Menu (`MainMenu.unity`) or Start/Pause Menu into either Campaign Level Mode or Endless Mode (`EndlessMode.unity`), place queued towers onto the 3D board, use strategic boosters (Delete, Level-Up, Look-Ahead), and resolve milestones (`LevelUpPanel`) or end-of-run outcomes (`LevelWinPanel`, `LevelLossPanel`).
Adding a **Unified UI Motion & Micro-Interaction System** across all menus, HUD bars, modal cards, and buttons provides immediate tactile feedback on every touch and smooth visual continuity across every state transition without delaying player input.

## Controls and Input Methods
- **Button Press / Release (`IPointerDownHandler`, `IPointerUpHandler`, `IPointerClickHandler`)**:
  - Pressing any interactive button immediately squashes it to `0.92x` (`0.07s` ease-out).
  - Releasing or clicking springs it back with an elastic overshoot (`0.92x -> 1.08x -> 1.0x` over `0.18s`).
- **Primary CTA Idle Attention Pulse**:
  - Primary action buttons (`PlayButton`, `ContinueButton`, `NextLevelButton`, `RetryButton`, and Main Menu's `LevelsMenuButton` / `EndlessButton`) gently breathe (`1.0x <-> 1.035x` sine wave at `1.8 Hz`) with a subtle arrow nudge to draw player focus.
- **Modal Panel & HUD Transitions**:
  - Panels (`StartPanel`, `LevelUpPanel`, `LevelWinPanel`, `LevelLossPanel`, `LosePanel`, `StoreOffersWindow`, `DailyLoginRewards`) pop in with an elastic card scale (`0.82x -> 1.04x -> 1.0x` over `0.22s`) and `CanvasGroup` alpha fade (`0 -> 1` over `0.16s`), with staggered child button entrance (`+0.04s` per button).

# UI
## Comprehensive List of Menu & Button Animation Opportunities in Main Game Screens

### 1. All Interactive Buttons (Universal Tactile Squash & Release Bounce)
- **Main Menu (`MainMenu.unity`)**: `LevelsMenuButton` (Campaign play), `EndlessButton`, `StoreButton`, `DailyLoginRewards` open/claim/close/reminder buttons, `StoreOffersWindow` purchase/close buttons, and `TopBar` settings button.
- **Gameplay HUD (`PlayPanel`)**: `PauseButton`, `DeletePowerUpButton`, `LookAheadPowerUpButton`, `LevelUpPowerUpButton`.
- **Modal Overlay Cards (`LevelRulesUI`)**:
  - `StartPanel`: `PlayButton` ("PLAY" / "RESUME"), `BackButton` ("BACK").
  - `LevelUpPanel`: `ContinueButton` ("CONTINUE").
  - `LevelWinPanel`: `PrimaryButton` ("NEXT LEVEL"), `MenuButton` ("MAIN MENU").
  - `LevelLossPanel` & `LosePanel`: `PrimaryButton` / `RetryButton` ("TRY AGAIN"), `MenuButton` / `BackButton` ("MAIN MENU"), `BuyExtraMovesButton` ("BUY EXTRA MOVES").

### 2. Modal Card Entrance & Staggered Content Choreography
- **`StartPanel` (Start / Pause Card)**: Card pops in from `0.82x -> 1.04x -> 1.0x` with alpha fade; `ObjectivesCard`, `BackButton`, and `PlayButton` stagger in sequentially.
- **`LevelUpPanel` (Endless Milestone Card)**: Card pops in with elastic scale; `"LEVEL UP!"` title punches from `1.25x -> 1.0x`; `ContinueButton` pops up `0.06s` later.
- **`LevelWinPanel` (Campaign Victory Card)**: Card elastic pop-in, `"YOU WIN!"` title bounce, and staggered `"MAIN MENU"` / `"NEXT LEVEL"` buttons.
- **`LevelLossPanel` / `LosePanel` (Defeat Card)**: Card entrance with a subtle horizontal shake (`±8px` damped sine over `0.22s`) on `"LEVEL FAILED"` / `"GAME OVER"` to reinforce impact, followed by `"TRY AGAIN"`, `"MAIN MENU"`, and `"BUY EXTRA MOVES"`.
- **`StoreOffersWindow` & `DailyLoginRewards`**: Smooth card pop-in and fade when opened from Main Menu or gameplay.

### 3. In-Game HUD & Counter Micro-Animations (`ScorePanel` & `PlayPanel`)
- **Top HUD (`ScorePanel`) & Bottom Dock (`PlayPanel`) Entrance**: When transitioning from `StartPanel` into active gameplay (`StartGame()`), `PlayPanel` buttons pop in with a quick staggered wave (`0.03s` apart) and `ScorePanel` slides/fades cleanly.
- **Power-Up Targeting Banner (`PowerUpPanel_Delete`, `PowerUpPanel_LevelUp`, `PowerUpPanel_LookAhead`)**: Prompt banner slides/scales in when entering targeting mode and pulses gently while awaiting cell selection.
- **Power-Up Badge Count & Moves Remaining Tick Pulse**:
  - When `MovesRemaining` decrements (or increases via Extra Moves) or when a booster count changes, the label pulses (`1.0x -> 1.35x -> 1.0x` over `0.15s`).
  - When `MovesRemaining <= 3` in Campaign mode, `movesLabel` tints warm amber/red and pulses on each move to signal urgency.
- **Objective Progress Row Pulse (`ObjectiveCardUI`)**:
  - When a player's highest tower level or score advances toward an objective, the corresponding row icon/text pulses (`1.0x -> 1.22x -> 1.0x`), and plays a completion pop when `current >= target`.

## Motion Architecture Wireframe
```
+-------------------------------------------------------------------+
|                     UIAnimationProfile (Shared Constants)         |
|  - ButtonPressScale: 0.92 (0.07s) | ReleaseOvershoot: 1.08 (0.18s)|
|  - IdlePulseScale:   1.035 (1.8Hz)| CounterTickPeak:  1.35 (0.15s)|
|  - ModalStartScale:  0.82 -> 1.04 -> 1.0 (0.22s) + Alpha 0 -> 1   |
+-------------------------------------------------------------------+
         |                                  |
         v                                  v
+--------------------------------+  +-------------------------------+
|       UIButtonAnimator         |  |        UIPanelAnimator        |
| - Attached to all UI Buttons   |  | - Attached to Modal & HUD     |
| - PointerDown / Up / Click     |  |   Panels (Start, LevelUp,     |
| - Optional Primary CTA Pulse   |  |   Win, Loss, Play, PowerUp,   |
| - Deterministic Advance(delta) |  |   Store, DailyLogin)          |
+--------------------------------+  +-------------------------------+
```

# Key Asset & Context

### New Scripts to Create
1. `Assets/Scripts/UI/UIButtonAnimator.cs`:
   - Lightweight, allocation-free component implementing `IPointerDownHandler`, `IPointerUpHandler`, `IPointerExitHandler`, and `IPointerClickHandler`.
   - Animates button `localScale` using `Time.unscaledDeltaTime` (and exposes `Advance(float delta)` for deterministic Editor testing).
   - Supports an optional `_enableIdlePulse` flag for primary CTA buttons (`PlayButton`, `ContinueButton`, `NextLevelButton`, `RetryButton`, `LevelsMenuButton`) and optional child `Arrow` icon bobbing.
   - Respects `Button.interactable` (disabled buttons do not pulse or squash).

2. `Assets/Scripts/UI/UIPanelAnimator.cs`:
   - Attached to modal panels (`StartPanel`, `LevelUpPanel`, `LevelWinPanel`, `LevelLossPanel`, `LosePanel`, `PowerUpPanel_Delete`, `PowerUpPanel_LevelUp`, `PowerUpPanel_LookAhead`, `StoreOffersWindow`, `DailyLoginRewards`) and HUD containers (`PlayPanel`).
   - Automatically triggers entrance choreography in `OnEnable()` (using `CanvasGroup` alpha fade + card `RectTransform` elastic pop-in `0.82 -> 1.04 -> 1.0` + optional title punch/shake + staggered child button pop-in).
   - Exposes `PlayEntrance()`, `Advance(float delta)`, and `SnapToEnd()` so existing synchronous tests that check `panel.activeSelf` or button clickability continue to work without any breaking changes (raycasts remain enabled immediately on activation).

### Existing Scripts to Modify
1. `Assets/Scripts/UI/LevelRulesUI.cs`:
   - Track previous `MovesRemaining` and power-up counts in `Refresh()`; when a value changes during active play, trigger a scale tick pulse on `movesLabel` or the respective power-up badge label (`deletePowerUpLabel`, `levelUpPowerUpLabel`, `lookAheadPowerUpLabel`).
   - Add low-moves warning pulse/tint when `session != null && session.MovesRemaining > 0 && session.MovesRemaining <= 3`.
   - Ensure `UIPanelAnimator` and `UIButtonAnimator` components are bound/attached on `LevelRulesUI` panels and buttons if not already present.

2. `Assets/Scripts/UI/ObjectiveCardUI.cs`:
   - Track previous progress per objective row; when `current` increases or reaches `objective.target`, trigger a brief scale pulse (`1.0 -> 1.25 -> 1.0` over `0.15s`) on the row's icon/label.

### Prefabs & Scenes to Modify
1. `Assets/Prefabs/UI/LevelRulesUI.prefab` & `Assets/Scenes/EndlessMode.unity`:
   - Attach and configure `UIPanelAnimator` on `StartPanel`, `LevelUpPanel`, `LevelWinPanel`, `LevelLossPanel`, `LosePanel`, `PlayPanel`, `PowerUpPanel_Delete`, `PowerUpPanel_LevelUp`, and `PowerUpPanel_LookAhead`.
   - Attach and configure `UIButtonAnimator` on all interactive buttons (`PlayButton`, `BackButton`, `ContinueButton`, `PauseButton`, `DeletePowerUpButton`, `LookAheadPowerUpButton`, `LevelUpPowerUpButton`, `PrimaryButton`, `MenuButton`, `BuyExtraMovesButton`, `RetryButton`), enabling `_enableIdlePulse` on primary CTAs.
2. `Assets/Scenes/MainMenu.unity`, `Assets/Prefabs/UI/LevelsMenuButton.prefab`, `Assets/Prefabs/UI/StoreOffersWindow.prefab`, `Assets/Prefabs/UI/DailyLoginRewards.prefab`:
   - Attach `UIButtonAnimator` to Main Menu buttons (`LevelsMenuButton`, `EndlessButton`, `StoreButton`, `Settings`, Store & Daily Login buttons) and `UIPanelAnimator` to `StartPanel`, `StoreOffersWindow`, and `DailyLoginRewards` modal panels.

### Editor Tests to Create
1. `Assets/Editor/Tests/UIMotionConsistencyTests.cs`:
   - Automated Editor verification suite (`[MenuItem("Tools/City Blocks/Tests/UI Motion Consistency Checks")]`) verifying:
     - All buttons in `LevelRulesUI` have `UIButtonAnimator` configured with consistent squash/overshoot parameters.
     - Pointer down squashes button scale (`< 1.0`), pointer up/click overshoots (`> 1.0`) and settles to `1.0`.
     - Disabled buttons (`button.interactable = false`) do not squash or idle-pulse.
     - All modal panels (`StartPanel`, `LevelUpPanel`, `LevelWinPanel`, `LevelLossPanel`) have `UIPanelAnimator`, animate their card scale and `CanvasGroup` alpha on `OnEnable()`, and settle cleanly at `scale == Vector3.one` and `alpha == 1f`.
     - `movesLabel` and `ObjectiveCardUI` rows pulse when values change.
     - Existing test suites (`Combo Celebration Checks`, `Merge Multiplier Checks`, `Score Collection Deterministic Checks`) pass with zero regressions.

# Implementation Steps

### Step 1: Create `UIButtonAnimator` and `UIPanelAnimator` Core Components
- **Files**: `Assets/Scripts/UI/UIButtonAnimator.cs`, `Assets/Scripts/UI/UIPanelAnimator.cs`
- **Details**:
  - Implement `UIButtonAnimator` with pointer press squash (`0.92x`), release elastic bounce (`1.08x -> 1.0x`), optional primary CTA breathing pulse (`1.035x`), optional arrow icon nudge, and deterministic `Advance(float delta)`.
  - Implement `UIPanelAnimator` with modal card elastic pop-in (`0.82x -> 1.04x -> 1.0x`), `CanvasGroup` fade-in, optional defeat card shake, staggered child button scale-in, and deterministic `Advance(float delta)`.
- **Dependencies**: None

### Step 2: Add HUD Counter & Objective Progress Micro-Pulses in `LevelRulesUI` and `ObjectiveCardUI`
- **Files**: `Assets/Scripts/UI/LevelRulesUI.cs`, `Assets/Scripts/UI/ObjectiveCardUI.cs`
- **Details**:
  - Add reusable label/rect pulse helper to pulse `movesLabel` (with low-moves urgency tint when `<= 3`) and power-up count labels when their values change.
  - Pulse `ObjectiveCardUI` rows when tower level or score progress advances or completes an objective.
- **Dependencies**: Step 1

### Step 3: Wire `UIButtonAnimator` and `UIPanelAnimator` Across Prefabs and Scenes
- **Files**: `Assets/Prefabs/UI/LevelRulesUI.prefab`, `Assets/Prefabs/UI/LevelsMenuButton.prefab`, `Assets/Prefabs/UI/StoreOffersWindow.prefab`, `Assets/Prefabs/UI/DailyLoginRewards.prefab`, `Assets/Scenes/EndlessMode.unity`, `Assets/Scenes/MainMenu.unity`
- **Details**:
  - Attach `UIPanelAnimator` and `UIButtonAnimator` with consistent timing settings across all game screens and prefabs.
- **Dependencies**: Step 1, Step 2

### Step 4: Create & Run Automated Verification Suite (`UIMotionConsistencyTests.cs`)
- **Files**: `Assets/Editor/Tests/UIMotionConsistencyTests.cs`
- **Details**:
  - Verify button press/release/idle animations, modal entrance choreography, HUD counter pulses, and run all existing test suites to confirm zero regressions.
- **Dependencies**: Step 1, Step 2, Step 3

# Verification & Testing
- Run `Tools/City Blocks/Tests/UI Motion Consistency Checks` to verify deterministic button squash/bounce, CTA idle pulse, modal card entrance animations on `StartPanel`, `LevelUpPanel`, `LevelWinPanel`, and `LevelLossPanel`, and HUD counter pulses.
- Run `Tools/City Blocks/Tests/Combo Celebration Checks`, `Tools/City Blocks/Tests/Merge Multiplier Checks`, and `Tools/City Blocks/Tests/Score Collection Deterministic Checks` to verify full compatibility.
