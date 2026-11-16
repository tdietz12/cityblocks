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
- **`StoreOffersWindow` & `DailyLoginRewards`**:
  - **Unified Visual Design (`StoreOffersWindow`)**:
    - Matches `MainMenu` (`StartPanel`) and `LevelRulesUI` (`StartPanel`, `LevelWinPanel`, `LevelLossPanel`, `LevelUpPanel`):
    - **CanvasScaler**: Configured to `ScaleWithScreenSize` (`referenceResolution = 393x852`, `matchWidthOrHeight = 0`) matching `MainMenu`'s `StartPanel` so card and button proportions match identically across all portrait mobile resolutions.
    - **Backdrop (`StoreOfferPanel`)**: Fullscreen dimmed overlay `RGBA(0.030, 0.050, 0.080, 0.780)` with `CanvasGroup` and `UIPanelAnimator`.
    - **Modal Card (`StoreOfferPanel/Card`)**: Centered `330x310` card using sliced `RoundedRect` (`Assets/UI/FigmaImport/RoundedRect.png`) in deep slate-navy `RGBA(0.059, 0.082, 0.110, 0.950)` (`#0F151C`).
    - **Header (`StreakLabel` / Title)**: Bold `24pt` (`#F5F5F5`, `RGBA(0.961, 0.961, 0.961, 1.000)`) centered at the top of `Card` (`"STORE"`), with an optional store icon badge matching the Main Menu Store card.
    - **Offer Details Container (`OfferBox` / `RewardSummary`)**: Inner rounded sub-panel (`RoundedRect`, `RGBA(0.118, 0.157, 0.204, 0.850)`) displaying clean, well-spaced offer text (`14pt`, `RGBA(0.941, 0.941, 0.941, 1.000)`).
    - **Buttons Inside `Card`**:
      - `ClaimButton` (`"DAILY SPECIAL"` / `"BUY DAILY SPECIAL"`): Primary gold button (`RoundedRect` sliced, `RGBA(0.831, 0.643, 0.141, 1.000)`, bold dark label `RGBA(0.059, 0.059, 0.059, 1.000)`).
      - `ReminderButton` (`"REMOVE ADS"`): Secondary slate button (`RoundedRect` sliced, `RGBA(0.118, 0.157, 0.204, 1.000)`, bold gold/light label `RGBA(0.831, 0.643, 0.141, 1.000)`).
      - `DailyRewardOpenButton` (`closeStoreButton`, `"CLOSE"`): Parented **inside `StoreOfferPanel/Card`** (instead of outside `StoreOfferPanel`), styled as a secondary slate button (`RoundedRect` sliced, `RGBA(0.118, 0.157, 0.204, 1.000)`, bold light label `RGBA(0.941, 0.941, 0.941, 1.000)`).
  - **Unified Entrance & Exit Animation (`StoreOffersWindow`)**:
    - Because `closeStoreButton` now lives inside `StoreOfferPanel/Card` and stays active as part of the card hierarchy, opening the Store via `UIPanelAnimator.SetAnimatedActive(dailySpecialWindow, true)` animates the entire `Card` (`0.82x -> 1.04x -> 1.0x`), fades `CanvasGroup` (`0 -> 1`), and staggers all 3 buttons (`ClaimButton`, `ReminderButton`, `closeStoreButton`) in sequence.
    - Closing the Store via `UIPanelAnimator.SetAnimatedActive(dailySpecialWindow, false)` smoothly shrinks and fades the `Card` and all 3 buttons together over `0.16s` (`PlayExit()`) instead of snapping `closeStoreButton` off prematurely.

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

3. `Assets/Scripts/UI/StoreOfferWindow.cs`:
   - Keep `closeStoreButton` (`DailyRewardOpenButton`) active inside `StoreOfferPanel/Card` (instead of toggling it outside `StoreOfferPanel`) so it participates in `UIPanelAnimator`'s card pop-in, `CanvasGroup` alpha fade, staggered button entrance, and smooth exit animation (`PlayExit()`).
   - Format the Store description cleanly with rich-text section headers (`<b>DAILY SPECIAL</b>`, `<b>REMOVE ADS</b>`) and readable booster summary lines.

### Prefabs & Scenes to Modify
1. `Assets/Prefabs/UI/LevelRulesUI.prefab` & `Assets/Scenes/EndlessMode.unity`:
   - Attach and configure `UIPanelAnimator` on `StartPanel`, `LevelUpPanel`, `LevelWinPanel`, `LevelLossPanel`, `LosePanel`, `PlayPanel`, `PowerUpPanel_Delete`, `PowerUpPanel_LevelUp`, and `PowerUpPanel_LookAhead`.
   - Attach and configure `UIButtonAnimator` on all interactive buttons (`PlayButton`, `BackButton`, `ContinueButton`, `PauseButton`, `DeletePowerUpButton`, `LookAheadPowerUpButton`, `LevelUpPowerUpButton`, `PrimaryButton`, `MenuButton`, `BuyExtraMovesButton`, `RetryButton`).
2. `Assets/Prefabs/UI/StoreOffersWindow.prefab` & `Assets/Scenes/MainMenu.unity`:
   - Update `StoreOffersWindow` `CanvasScaler` to `ScaleWithScreenSize` (`393x852`, `match = 0`) matching `MainMenu`'s `StartPanel`.
   - Style `StoreOfferPanel/Card` (`330x310`) with sliced `RoundedRect` (`Assets/UI/FigmaImport/RoundedRect.png`) and deep slate-navy `RGBA(0.059, 0.082, 0.110, 0.950)` matching `StartPanel`, `LevelWinPanel`, `LevelLossPanel`, and `LevelUpPanel`.
   - Style `StreakLabel` (`"STORE"`) with bold `24pt` `#F5F5F5` typography and `RewardSummary` with clean `14pt` light typography inside the card.
   - Reparent `DailyRewardOpenButton` (`closeStoreButton`, `"CLOSE"`) inside `StoreOfferPanel/Card`, style `ClaimButton` (`RoundedRect` gold `#D4A424` with bold dark text), `ReminderButton` (`RoundedRect` slate `#1E2834` with bold gold text), and `DailyRewardOpenButton` (`RoundedRect` slate `#1E2834` with bold light text), and include all 3 buttons in `UIPanelAnimator._staggeredButtons`.

### Editor Tests to Create / Update
1. `Assets/Editor/Tests/UIMotionConsistencyTests.cs`:
   - Automated Editor verification suite (`[MenuItem("Tools/City Blocks/Tests/UI Motion Consistency Checks")]`) verifying:
     - All buttons in `LevelRulesUI` and `StoreOffersWindow` have `UIButtonAnimator` configured with consistent squash/overshoot parameters.
     - Pointer down squashes button scale (`< 1.0`), pointer up/click overshoots (`> 1.0`) and settles to `1.0`.
     - Disabled buttons (`button.interactable = false`) do not squash or idle-pulse.
     - All modal panels (`StartPanel`, `LevelUpPanel`, `LevelWinPanel`, `LevelLossPanel`, and `StoreOfferPanel` in `StoreOffersWindow`) have `UIPanelAnimator`, use the unified `RoundedRect` card styling, animate their card scale and `CanvasGroup` alpha on `PlayEntrance()` and `PlayExit()`, and keep `closeStoreButton` inside `StoreOfferPanel/Card` so it animates smoothly on both open and close.
     - `movesLabel` and `ObjectiveCardUI` rows pulse when values change.
     - Existing test suites (`Combo Celebration Checks`, `Merge Multiplier Checks`, `Score Collection Deterministic Checks`) pass with zero regressions.

# Implementation Steps

### Step 1: Update `StoreOfferWindow.cs` for Unified Card & Close Button Lifecycle
- **Files**: `Assets/Scripts/UI/StoreOfferWindow.cs`
- **Details**:
  - Stop hiding/showing `closeStoreButton` independently outside `StoreOfferPanel`; instead, keep `closeStoreButton` active inside `StoreOfferPanel/Card` so `UIPanelAnimator.SetAnimatedActive(dailySpecialWindow, true/false)` animates the entire card and all 3 buttons (including `"CLOSE"`) together on both open and close.
  - Format the Store offer summary text cleanly for the unified card layout.
- **Dependencies**: None

### Step 2: Restyle & Re-layout `StoreOffersWindow.prefab` and `MainMenu.unity` to Match Game Menus
- **Files**: `Assets/Prefabs/UI/StoreOffersWindow.prefab`, `Assets/Scenes/MainMenu.unity`
- **Details**:
  - Configure `CanvasScaler` on `StoreOffersWindow` to `ScaleWithScreenSize` (`393x852`, `matchWidthOrHeight = 0`) matching `MainMenu`'s `StartPanel`.
  - Apply sliced `RoundedRect` (`Assets/UI/FigmaImport/RoundedRect.png`) and dark slate `RGBA(0.059, 0.082, 0.110, 0.950)` to `StoreOfferPanel/Card` (`330x310`).
  - Reparent `DailyRewardOpenButton` (`closeStoreButton`) under `StoreOfferPanel/Card`, apply `RoundedRect` gold (`RGBA(0.831, 0.643, 0.141, 1.000)`) and slate (`RGBA(0.118, 0.157, 0.204, 1.000)`) button styling with bold typography, and wire all 3 buttons into `UIPanelAnimator._staggeredButtons`.
- **Dependencies**: Step 1

### Step 3: Update & Run Automated Verification Suite (`UIMotionConsistencyTests.cs`)
- **Files**: `Assets/Editor/Tests/UIMotionConsistencyTests.cs`
- **Details**:
  - Add verification checks confirming `StoreOffersWindow` uses `ScaleWithScreenSize`, sliced `RoundedRect` card & button styling matching the other menus, `closeStoreButton` parented inside `StoreOfferPanel/Card`, staggered button entrance, and smooth `PlayEntrance()` / `PlayExit()` animations via `StoreOfferWindow.OpenDailySpecial()` and `CloseDailySpecial()`.
- **Dependencies**: Step 1, Step 2

# Verification & Testing
- Run `Tools/City Blocks/Tests/UI Motion Consistency Checks` to verify deterministic button squash/bounce, modal card entrance & exit animations on `StartPanel`, `LevelUpPanel`, `LevelWinPanel`, `LevelLossPanel`, and `StoreOffersWindow` (`StoreOfferPanel`), unified `RoundedRect` visual styling, and HUD counter pulses.
- Run `Tools/City Blocks/Tests/Combo Celebration Checks` and `Tools/City Blocks/Tests/Merge Multiplier Checks` to verify full compatibility.
