# Project Overview
- Game Title: City Blocks
- High-Level Concept: A 2.5D match-and-merge puzzle game where players drop numbered towers and merge them to reach higher tower levels, manage obstacles, and satisfy level objectives within move limits.
- Players: Single player
- Inspiration / Reference Games: 2048, Drop The Number, City Bloxx
- Tone / Art Direction: Casual, clean, colorful urban-puzzle aesthetic
- Target Platform: iOS (Mobile)
- Screen Orientation / Resolution: Portrait 1080x1920
- Render Pipeline: URP-HighFidelity

# Game Mechanics
## Core Gameplay Loop
Players drop towers into grid columns. Matching adjacent towers merge into higher-level buildings. Level definitions configure starting obstacles (Breakable, Fixed Unbreakable, Falling Unbreakable), initial towers, holes, move limits, queues, and objectives. This rework transforms level authoring from manually managing detached index lists (`startingBlocks`, `obstacles`) into directly painting and editing a visual 2D grid in the Unity Inspector.

## Controls and Input Methods
- **Unity Editor Level Authoring**:
  - Setting board dimensions (`columns` and `rows`) immediately generates a 2D grid of editable text fields matching that exact size.
  - Designers type into each cell:
    - A number (e.g. `1`, `2`, `3`...) to spawn a starting tower with that level.
    - `"Fixed Breakable"` (or `"Breakable"`, with optional durability e.g. `"Fixed Breakable 2"`) to place a breakable obstacle.
    - `"Fixed Unbreakable"` to place an immovable steel obstacle.
    - `"Falling Unbreakable"` to place a gravity-affected steel obstacle.
    - Empty or whitespace to indicate a clear, playable cell.
    - `"Hole"` or `"X"` to mark an unavailable hole cell.
  - The custom editor also provides quick dropdown/preset actions and color coding per cell type to speed up iteration.
- **In-Game Play**: Tap column to drop the active tower from the queue into the lowest available row. Unchanged.

# UI
- **Custom LevelDefinition Inspector**:
  - **General Settings**: Level Number, Difficulty.
  - **Grid Dimensions**: Column count (3–7) and Row count (3–7). Modifying either value immediately resizes the visual grid while preserving overlapping cell values.
  - **2D Board Layout Matrix**:
    - Displayed in intuitive orientation: Column 0 on left, Row 0 at bottom (matching board physics floor), Top Row at top.
    - Clear column headers (`Col 0`, `Col 1`, ...) and row labels (`Row [N] (Top)`, ..., `Row 0 (Bottom)`).
    - Each cell rendered with distinct background tint (e.g., green/blue for towers, orange for breakable, slate for fixed unbreakable, magenta for falling unbreakable, dark gray for holes).
    - Text field for direct string input plus a right-click / small picker popup for fast assignment.
  - **Level Rules & Objectives**: Queue entries, random maximum level, move limit, objectives, tutorial steps, and ad trigger toggle.
  - **Status & Validation Box**: Real-time validation reporting board status or any errors.

# Key Asset & Context
- `Assets/Scripts/Gameplay/LevelDefinition.cs`:
  - Add `[Serializable] public class LevelGridRow { public string[] cells; }`.
  - Add `public List<LevelGridRow> grid = new List<LevelGridRow>();`.
  - Implement bidirectional sync:
    - `SyncFromGrid()` parses the cell strings into `startingBlocks`, `obstacles`, and `unavailableCells`.
    - `SyncToGrid()` populates `grid` from existing `startingBlocks`, `obstacles`, and `unavailableCells` if `grid` is empty.
    - `ResizeGrid(int newCols, int newRows)` dynamically reallocates rows and cells while preserving existing cell values.
  - Implement `OnValidate()` and `ISerializationCallbackReceiver` to ensure `startingBlocks`, `obstacles`, and `grid` remain synchronized across asset reloads and builds.
- `Assets/Editor/LevelDefinitionEditor.cs`:
  - Custom editor extending `UnityEditor.Editor` with `[CustomEditor(typeof(LevelDefinition))]`.
  - Draws interactive 2D grid matrix with column/row headers and text fields.
  - Handles size changes, color highlights, and helper context menus.
- `Assets/Editor/LevelGridMigration.cs`:
  - Utility menu item (`Tools/City Blocks/Sync Grids for All Levels`) to migrate all 100 existing level assets from legacy lists to the new 2D grid layout.

# Implementation Steps
- **Step 1: Update LevelDefinition Data Structure & Serialization**
  - **Description**: Add `LevelGridRow`, `grid` field, resizing logic, and string parsing/sync logic to `Assets/Scripts/Gameplay/LevelDefinition.cs`. Ensure backward compatibility by populating `startingBlocks`, `obstacles`, and `unavailableCells` from `grid` and vice-versa.
  - **Assigned role**: developer
  - **Dependencies**: None
  - **Parallelizable**: No

- **Step 2: Create Custom Inspector for LevelDefinition**
  - **Description**: Create `Assets/Editor/LevelDefinitionEditor.cs`. Implement GUI layout for `columns` and `rows` that automatically resizes the grid. Render the 2D grid with row/column headers, text fields, color tints, quick preset options, and remaining level fields (queue, move limit, objectives, tutorial steps).
  - **Assigned role**: developer
  - **Dependencies**: Step 1
  - **Parallelizable**: No

- **Step 3: Create Migration & Batch Sync Utility**
  - **Description**: Create `Assets/Editor/LevelGridMigration.cs` with menu items to sync all existing 100 level assets in `Assets/Resources/Levels/` to the new grid format and verify asset validity.
  - **Assigned role**: developer
  - **Dependencies**: Step 1, Step 2
  - **Parallelizable**: No

- **Step 4: Execute Batch Sync on Existing Levels and Validate**
  - **Description**: Run the batch migration on all existing levels to populate their `grid` rows, ensuring no existing level data is lost or altered.
  - **Assigned role**: developer
  - **Dependencies**: Step 3
  - **Parallelizable**: No

- **Step 5: Run Automated Verification & Regression Testing**
  - **Description**: Run `LevelCatalog.Load()` verification across all 100 levels via test script and verify that game loading, obstacle initialization, and starting blocks operate identically.
  - **Assigned role**: developer
  - **Dependencies**: Step 4
  - **Parallelizable**: No

# Verification & Testing
- **Editor Grid Resizing Test**:
  - Open a `LevelDefinition` in the Inspector. Change columns from 3 to 5 and rows from 4 to 6; verify the 2D matrix resizes immediately to 5x6.
  - Enter tower numbers (`1`, `2`, `3`), `"Fixed Breakable"`, `"Fixed Unbreakable"`, and `"Falling Unbreakable"`. Verify that `startingBlocks` and `obstacles` populate with matching coordinates and types.
- **String Parsing Tests**:
  - Test case-insensitivity and whitespace tolerance: `"fixed breakable"`, `"Fixed Breakable"`, `"  Falling Unbreakable  "`, `"Fixed Unbreakable"`.
  - Test number parsing: `"1"`, `"4"`, `"6"`.
  - Test empty cells: `""` or whitespace.
- **Catalog Load Test**:
  - Execute a test script to invoke `LevelCatalog.Levels` across all 100 levels in `Assets/Resources/Levels/` to ensure all levels pass `IsValid()` without errors.
- **Gameplay Regression Test**:
  - Enter Play Mode on a level with starting towers and obstacles (e.g. Level 1 or Level 28); verify towers and obstacles spawn at the exact expected board positions.
