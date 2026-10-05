# Project Overview
- Game Title: City Blocks
- High-Level Concept: A puzzle city-building game where players place and merge tiered buildings on a grid, managing limited moves and board space.
- Players: Single player
- Inspiration / Reference Games: CityBlocks, 2048, Triple Town
- Tone / Art Direction: Casual, vibrant low-poly 3D aesthetics with clean mobile UI.
- Target Platform: iOS and Android
- Screen Orientation / Resolution: Portrait 1080x1920
- Render Pipeline: Universal Render Pipeline (URP-HighFidelity)

# Game Mechanics
## Core Gameplay Loop
Players drop towers onto a grid from a randomized queue. Matching adjacent towers merge into higher tiers. The game ends in a loss when either the grid fills completely (cannot place another building) or the player runs out of moves in level mode. Upon losing, the loss screen is displayed giving the player two distinct choices: Retry (Try Again) or Back (Main Menu).

## Controls and Input Methods
- Touch/click on 3D grid columns during gameplay to place structures.
- Modal UI buttons on the Loss Panel:
  - **TRY AGAIN (Retry)**: Restarts the current game mode immediately with a fresh, reset board.
  - **MAIN MENU (Back)**: Returns the player to the main menu with any active run/loss data discarded, ensuring the next time that level or endless mode is opened, it starts with a clean board.

# UI
## Loss Panel Wireframe & Flow (Endless Mode & Level Mode)
```
+---------------------------------------------------+
|  [Dark Translucent Modal Backdrop]                |
|                                                   |
|       +-----------------------------------+       |
|       |             Card Panel            |       |
|       |                                   |       |
|       |           [ GAME OVER ]           |       |
|       |                                   |       |
|       |     [Optional Buy Moves Button]   |       |
|       |   (Level Mode with move_limit only) |     |
|       |                                   |       |
|       |       [ TRY AGAIN (Retry) ]       |       |
|       |                                   |       |
|       |       [ MAIN MENU (Back) ]        |       |
|       |                                   |       |
|       +-----------------------------------+       |
|                                                   |
+---------------------------------------------------+
```
- **Unified UI Options**: Both Endless Mode and Level Mode present the player with both **TRY AGAIN** (Retry) and **MAIN MENU** (Back).
- **Extra Moves Option**: Buy Extra Moves button is only visible in Campaign Level Mode when the failed objective was `moves_limit` and a store offer exists; hidden in Endless Mode or on full-grid losses.

# Key Asset & Context

### Scripts to Modify
1. `Assets/Scripts/Gameplay/GridController.cs`:
   - Add `public void ClearBoard()`:
     - Destroys all tower game objects in `grid[,]`.
     - Resets `grid` array references to null.
     - Clears `lastIntactBoardState` snapshot.
     - Clears `towersInPlay` tracking list.
   - Update `Save(ref GameSaveData, ref ProgressionData)`:
     - Do not save board pieces if `game != null && !game.HasActiveRun`.

2. `Assets/Scripts/Gameplay/GameController.cs`:
   - Update `ResetValuesOnLoss()`:
     - Reset `hasActiveRun = false`, `score = 0`, `level = 0`, queue values, look-ahead values.
     - If `grid != null`, call `grid.ClearBoard()`.
     - Set `state = GameState.start`.
   - Update `Save(ref GameSaveData, ref ProgressionData)`:
     - When `!hasActiveRun`, ensure `gameSaveData.boardState` is emptied, `gameSaveData.hasActiveRun = false`, and `gameSaveData.isLost = false`.

3. `Assets/Scripts/Data Persistence/DataPersistenceController.cs`:
   - Add `public void ClearSavedGame()`:
     - Resets `gameSaveData.hasActiveRun = false`, `gameSaveData.score = 0`, `gameSaveData.level = 0`, `gameSaveData.isLost = false`, and `gameSaveData.boardState = new List<Vector3>()`.
     - Immediately persists the clean save file to disk via `fileHandler.Save(gameSaveData, progressionData)`.

4. `Assets/Scripts/UI/LevelRulesUI.cs`:
   - Update `ShowLoss()`:
     - Display the complete loss card with both **TRY AGAIN** and **MAIN MENU** for both Endless Mode and Level Mode.
     - Only show `buyExtraMovesButton` if in Level Mode with moves_limit.
   - Update `Retry()`:
     - **Level Mode**: Call `session.Retry()`, which finalizes failure, sets `Progress.activeRun = null`, saves progress, and restarts the level with its default starting blocks and obstacles.
     - **Endless Mode**: Call `game.ResetValuesOnLoss()`, `game.grid.ClearBoard()`, `DataPersistenceController.instance.ClearSavedGame()`, and reload `EndlessMode.unity`.
   - Update `ReturnToMenu()`:
     - **Level Mode**: Call `session.ReturnToMenu()`, which finalizes failure, clears `Progress.activeRun = null`, saves progress, and goes to `MainMenu`.
     - **Endless Mode**: When in loss state, call `game.ResetValuesOnLoss()`, `game.grid.ClearBoard()`, `DataPersistenceController.instance.ClearSavedGame()`, and go to `MainMenu`.

5. `Assets/Scripts/Gameplay/LevelSession.cs`:
   - Ensure `session.Retry()` and `session.ReturnToMenu()` unconditionally clear `Progress.activeRun = null` and save `Progress` on failure so restarting or re-entering the level initializes from default `startingBlocks`.

### Prefabs to Modify
1. `Assets/Prefabs/UI/LevelRulesUI.prefab`:
   - Ensure `lossPanel` is properly wired and styled to serve as the unified loss card for both Level and Endless modes.
   - Also add a Retry button to `endlessLossPanel` (`LosePanel`) and wire it to `LevelRulesUI.Retry`, providing fallback support if `endlessLossPanel` is ever activated.

# Implementation Steps

### Step 1: Add ClearBoard to GridController and Fix Save Guard
- **Description**: Add `public void ClearBoard()` to `Assets/Scripts/Gameplay/GridController.cs` to destroy all tower instances, clear `lastIntactBoardState`, and clear `towersInPlay`. In `GridController.Save()`, ensure board pieces are not written when `game.HasActiveRun` is false.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

### Step 2: Add ClearSavedGame to DataPersistenceController
- **Description**: Add `public void ClearSavedGame()` to `Assets/Scripts/Data Persistence/DataPersistenceController.cs` to clear `hasActiveRun`, `boardState`, `score`, `level`, and `isLost`, immediately saving the empty run to disk.
- **Assigned role**: developer
- **Dependencies**: None
- **Parallelizable**: Yes

### Step 3: Update GameController ResetValuesOnLoss and Save
- **Description**: Update `Assets/Scripts/Gameplay/GameController.cs` in `ResetValuesOnLoss()` to clear the board via `grid.ClearBoard()` and reset `state`. In `GameController.Save()`, do not save a run when `!hasActiveRun` or when `state == GameState.lose` after reset.
- **Assigned role**: developer
- **Dependencies**: Step 1, Step 2
- **Parallelizable**: No

### Step 4: Update LevelRulesUI Loss Display and Reset Handling
- **Description**: In `Assets/Scripts/UI/LevelRulesUI.cs`, update `ShowLoss()` to show the loss panel with both Retry and Back options for both Endless Mode and Level Mode. Update `Retry()` and `ReturnToMenu()` to clear board state, reset saved data, and reload/exit cleanly.
- **Assigned role**: developer
- **Dependencies**: Step 3
- **Parallelizable**: No

### Step 5: Update LevelRulesUI Prefab Loss Panels
- **Description**: Update `Assets/Prefabs/UI/LevelRulesUI.prefab` and the `EndlessMode.unity` scene instance to ensure both Retry and Back buttons are configured and hooked up to `LevelRulesUI.Retry` and `LevelRulesUI.ReturnToMenu`.
- **Assigned role**: developer
- **Dependencies**: Step 4
- **Parallelizable**: No

### Step 6: Verification and Automated Testing
- **Description**: Perform automated tests for both Level Mode and Endless Mode covering:
  1. Endless Mode loss -> Click Retry -> Verify saved run and board are completely reset and game starts fresh.
  2. Endless Mode loss -> Click Back -> Verify main menu is loaded, and relaunching Endless Mode starts with an empty board.
  3. Level Mode loss -> Click Retry -> Verify level restarts with its original `startingBlocks` and default move count.
  4. Level Mode loss -> Click Back -> Verify main menu is loaded, and starting the level again starts fresh with original `startingBlocks`.
- **Assigned role**: developer
- **Dependencies**: Step 5
- **Parallelizable**: No

# Verification & Testing
1. **Compilation Check**:
   - Ensure zero compilation errors or warnings across all modified scripts.
2. **Endless Mode Loss + Retry Test**:
   - Simulate grid filling to trigger loss in Endless Mode.
   - Verify loss panel displays with both "TRY AGAIN" and "MAIN MENU" buttons.
   - Click "TRY AGAIN": Verify saved board state in JSON is cleared, scene reloads, and board has 0 towers.
3. **Endless Mode Loss + Back Test**:
   - Simulate grid filling to trigger loss in Endless Mode.
   - Click "MAIN MENU": Verify main menu loads and saved endless JSON file has `hasActiveRun = false` and empty `boardState`.
   - Re-enter Endless Mode: Verify it starts fresh with 0 towers and score 0.
4. **Level Mode Loss + Retry Test**:
   - Launch Level 1, exhaust moves or fill grid.
   - Click "TRY AGAIN": Verify Level 1 reloads with its original starting block(s) and full move limit.
5. **Level Mode Loss + Back Test**:
   - Launch Level 1, exhaust moves or fill grid.
   - Click "MAIN MENU": Verify `Progress.activeRun` is null.
   - Re-enter Level 1: Verify it starts with original starting blocks and full move limit.
