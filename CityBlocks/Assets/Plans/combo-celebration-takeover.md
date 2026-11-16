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
|          .  ((  ((===============))  ))  .        |
|       <>     ((   +-----------+   ))       <>     |
|     []     ((     | AWESOME!  |     ))       []   |
|       <>     ((   +-----------+   ))       <>     |
|          .  ((  ((===============))  ))  .        |
|                                                   |
|   [Concentric Seismic Shockwave Rings +           |
|    Heavy Angular Concrete/Brick Rubble Shards]    |
+---------------------------------------------------+
```

### Visual Elements & Animation Choreography (0.20s Start Delay + 2.25s Celebration Duration)
1. **Start Delay (`_startDelay = 0.2f`)**:
   - After `grid.IsSettled()`, waits `0.2s` before displaying the takeover visuals and firing the camera punch, while keeping `IsCelebrating` (and `HasPendingComboCelebration`) true so no premature moves or win/loss modals interrupt.
2. **Fullscreen Dim & Input Blocker (`CanvasGroup` + `Image`)**:
   - Stretches across the entire screen (`AnchorMin (0,0)`, `AnchorMax (1,1)`).
   - Blocks raycasts while the celebration is active (`blocksRaycasts = true`), preventing unintended column taps or booster presses.
   - Smoothly fades backdrop alpha from `0 -> 0.58` over `0.18s`, holds through the extended `2.25s` celebration window, and fades out over `0.30s`.
3. **Congratulatory Headline Only (`TMP_Text`) — Tiered by Chain Length, Guaranteed Screen Fit**:
   - **Combo length text (`"xN COMBO!"`) is removed** — only the congratulatory word is shown.
   - **3 Merges**: `"AWESOME!"` / `"GREAT!"` / `"SUPER!"`
   - **4 Merges**: `"AMAZING!"` / `"SPECTACULAR!"` / `"FANTASTIC!"`
   - **5+ Merges**: `"UNBELIEVABLE!"` / `"LEGENDARY!"` / `"INCREDIBLE!"`
   - Uses responsive single-line sizing and dynamic scale clamping so even the longest words (`"UNBELIEVABLE!"`, `"SPECTACULAR!"`) span nearly edge-to-edge while remaining strictly within the screen width on all mobile aspect ratios with zero clipping or wrapping.
   - Adds a short **seismic jackhammer tremor** (`0.22s` high-frequency vertical/horizontal impact thud on `_bannerRoot.anchoredPosition`) when the celebration erupts.
4. **Seismic Shockwaves & Rubble VFX (Replaces Confetti Burst)**:
   - **Concentric Seismic Impact Rings (`ShockwaveRing`)**:
     - Spawns `2–3` staggered concentric shockwave rings (`2` rings for 3 merges, `3` rings for 4+ merges) behind the headline using a procedurally generated crisp ring `Sprite`.
     - Rings expand rapidly from `~140px` to `1150px+` with cubic ease-out (`1 - (1 - t)^3`), tinted in Hazard Amber (`#FFCC1A`), Jackhammer Orange (`#FF7A1F`), and Seismic Concrete Dust (`#E6E0D6`), fading out as they ripple across the screen.
   - **Heavy Angular Rubble Shards & Hazard Chips (`RubbleShard`)**:
     - Replaces paper-like fluttering confetti with `24–40` chiseled polygonal concrete/stone rubble chunks, terracotta brick fragments, and hazard-beacon chips using a procedurally generated faceted rock/shard `Sprite`.
     - Material palette: Concrete Gray (`#8C9196`), Dark Asphalt (`#383B40`), Limestone Mortar (`#C7C2B8`), Terracotta Brick (`#D96B27`), and Hazard Beacon Amber (`#FFBF1A`).
     - Heavy ballistic physics: explosive radial blast velocity (`520–920 px/s`) with upward launch kick (`260–420 px/s`), heavy gravitational acceleration (`-1350 px/s²`), and rigid angular tumbling (no paper-flip `Cos` distortion) so every piece reads as solid construction rubble.
   - **Camera Punch Synchronization**:
     - Fires `BoardCameraFeedback.PunchMerge()` right as the first seismic shockwave ring and rubble blast erupt at the end of the `0.2s` start delay.

# Key Asset & Context

### Scripts to Modify
1. `Assets/Scripts/UI/ComboCelebrationController.cs`:
   - Replace `ConfettiPalette`, `ConfettiPiece`, `SpawnConfettiBurst`, `UpdateConfetti`, and `EnsureConfettiPool` with the **Seismic Shockwaves & Rubble** system:
     - `ShockwaveRing` struct and pool (`_shockwaves`) for expanding concentric jackhammer shockwave rings.
     - `RubbleShard` struct and pool (`_rubble`) for heavy, rigid-tumbling faceted concrete/stone/brick shards.
     - Cached procedural sprites (`GetOrCreateRingSprite()` and `GetOrCreateRubbleShardSprite()`) so rings and angular shards render crisply without external texture dependencies.
     - Seismic tremor offset on `_bannerRoot.anchoredPosition` during the initial `0.22s` impact window.
     - Expose `ActiveShockwaveCount` and `ActiveRubbleCount` (plus `ActiveVfxCount`) for deterministic test inspection while preserving `[FormerlySerializedAs("_confettiContainer")]` on `_vfxContainer` for prefab/scene compatibility.
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
             public int ActiveShockwaveCount { get; }
             public int ActiveRubbleCount { get; }
             public int ActiveVfxCount { get; }
             public void Bind(GameController game);
             public void TriggerCelebration(int mergeCount);
             public void Advance(float delta);
             public void ClearImmediate();
             public static string SelectCongratulatoryWord(int mergeCount, int variantIndex = 0);
         }
     }
     ```

### Prefabs & Scenes to Modify
1. `Assets/Prefabs/UI/ComboCelebrationOverlay.prefab`:
   - Rename `ConfettiContainer` to `SeismicVfxContainer` (wired to `_vfxContainer` on `ComboCelebrationController`) and tune default rubble burst count (`_rubbleBurstCount = 28`) and shockwave ring parameters.
2. `Assets/Prefabs/UI/LevelRulesUI.prefab` & `Assets/Scenes/EndlessMode.unity`:
   - Ensure prefab instances in `LevelRulesUI.prefab` and `EndlessMode.unity` stay synchronized with `ComboCelebrationOverlay.prefab`.

### Editor Tests to Modify
1. `Assets/Editor/Tests/ComboCelebrationTests.cs`:
   - Update `Tools/City Blocks/Tests/Combo Celebration Checks` to verify:
     - During the `0.2s` start delay, `ActiveShockwaveCount == 0` and `ActiveRubbleCount == 0`.
     - Once the `0.2s` start delay completes, concentric seismic shockwave rings (`ActiveShockwaveCount >= 2`) and heavy rubble shards (`ActiveRubbleCount >= 16`) spawn and animate outward/downward.
     - Shockwave rings expand in size over time, and rubble shards follow heavy ballistic trajectories without paper-flip distortion.
     - No legacy `Confetti_` GameObjects exist under the overlay.
     - All shockwaves and rubble shards deactivate cleanly when the celebration ends or when `ClearAndSynchronize()` is called.

# Implementation Steps

### Step 1: Replace Confetti with Seismic Shockwaves & Rubble in `ComboCelebrationController`
- **Files**: `Assets/Scripts/UI/ComboCelebrationController.cs`
- **Details**:
  - Replace `ConfettiPalette` and `ConfettiPiece` with `RubblePalette`, `ShockwavePalette`, `ShockwaveRing`, and `RubbleShard`.
  - Add procedural sprite generators (`GetOrCreateRingSprite()` for hollow concentric shockwave rings and `GetOrCreateRubbleShardSprite()` for faceted polygonal stone/concrete chunks).
  - Implement `SpawnSeismicBurst(int mergeCount)` and `UpdateSeismicBurst(float time)`:
    - Spawn `2` staggered shockwave rings for 3-merge combos and `3` rings for 4+ merge combos, expanding from `140px` to `1150px+` with ease-out scaling and alpha decay.
    - Spawn `24–40` angular rubble shards with radial blast velocity, upward kick, heavy gravity (`-1350 px/s²`), and rigid angular rotation.
    - Add a `0.22s` damped jackhammer impact tremor to `_bannerRoot.anchoredPosition` on burst start.
  - Expose `ActiveShockwaveCount`, `ActiveRubbleCount`, and `ActiveVfxCount`.
- **Dependencies**: None

### Step 2: Update `ComboCelebrationOverlay.prefab`, `LevelRulesUI.prefab`, and `EndlessMode.unity`
- **Files**: `Assets/Prefabs/UI/ComboCelebrationOverlay.prefab`, `Assets/Prefabs/UI/LevelRulesUI.prefab`, `Assets/Scenes/EndlessMode.unity`
- **Details**:
  - Rename `ConfettiContainer` to `SeismicVfxContainer` and ensure `_vfxContainer` is bound on `ComboCelebrationController`.
- **Dependencies**: Step 1

### Step 3: Update Automated Verification Suite (`ComboCelebrationTests.cs`)
- **Files**: `Assets/Editor/Tests/ComboCelebrationTests.cs`
- **Details**:
  - Replace `ActiveConfettiCount` assertions with checks for `ActiveShockwaveCount` (`>= 2`) and `ActiveRubbleCount` (`>= 16`), shockwave ring expansion, absence of `Confetti_` objects, and full cleanup on dismissal and `ClearAndSynchronize()`.
- **Dependencies**: Step 1, Step 2

# Verification & Testing
- Run `Tools/City Blocks/Tests/Combo Celebration Checks` via `EditorApplication.ExecuteMenuItem` to verify:
  1. Placing a tower that results in 1 or 2 merges does **not** activate the fullscreen takeover.
  2. Placing a tower that results in 3, 4, or 5+ merges activates the celebration after `grid.IsSettled()`, waiting `0.2s` (`_startDelay`) before visuals erupt, and running for `2.25s` (`_totalDuration`).
  3. During the `0.2s` start delay, `ActiveShockwaveCount == 0` and `ActiveRubbleCount == 0`.
  4. After the `0.2s` start delay, concentric seismic shockwave rings (`ActiveShockwaveCount >= 2`) expand outward and heavy angular rubble shards (`ActiveRubbleCount >= 16`) launch and tumble with heavy gravity, with zero legacy `Confetti_` objects.
  5. Congratulatory headline text escalates by tier (3 merges vs 4 merges vs 5+ merges), displays **no** combo length text, and fits strictly inside the screen bounds at peak overshoot scale.
  6. Input (`blocksRaycasts` / move resolution) is blocked during the celebration and automatically restored when complete.
  7. Existing test suites (`Tools/City Blocks/Tests/Merge Multiplier Checks`) continue to pass without regression.
