# Daily Login Reward – Editor Testability (Player Reset Tool update)

# Project Overview
- **Game Title:** City Blocks
- **High-Level Concept:** Merge/tower puzzle game with campaign levels, Endless mode, power-ups, and a 7-day daily login reward streak.
- **Players:** Single player
- **Inspiration / Reference Games:** Casual mobile merge/puzzle games with daily streak retention loops
- **Tone / Art Direction:** Clean dark-slate UI with gold accents (existing style)
- **Target Platform:** iOS
- **Screen Orientation / Resolution:** Portrait (existing UI, Scale With Screen Size)
- **Render Pipeline:** URP

**Scope of this plan:** Editor-only tooling. No change to how the daily reward works in shipped builds. All new runtime hooks are inside `#if UNITY_EDITOR`.

# Game Mechanics
## Core Gameplay Loop
Not changed. Retention loop being tested: open the app → main menu → `DailyLoginStreakUI.TryAutoOpen()` claims today's reward (Day 1–7 power-ups) → modal shows the streak track → come back tomorrow. Missing a day resets to Day 1 and sends `daily_streak_broken`.

## Controls and Input Methods
Not changed. Testing happens through the **Tools → Player Reset Tool** window (IMGUI EditorWindow), before and during Play Mode.

# Current State (findings)
1. **Compile error blocks Play Mode:** `DailyLoginStreakUI.cs` lines 103–104 use a `previewOnly` field that doesn't exist (a preview feature that was only partly added). This has to be fixed first.
2. `SaveDataManagerWindow.cs` already has *Make Claimable as Day N*, *Simulate Missed Day*, and *Reset Daily Streak*, but it **reads and writes `daily-login-streak.json` itself**. `DailyLoginStreakService` has matching editor helpers (`DebugSetNextClaimDay`, `DebugBreakStreak`, `DebugReset`, `DebugLastClaimDate`, `DebugStoredStreakDay`, `DebugNextDayAfterToday`) that nothing calls, so the same logic exists in two places.
3. `DailyLoginStreakService.DebugEveryMenuVisitIsNewDay` (EditorPrefs toggle) exists, but **nothing reads it**, and there's no checkbox for it in the window.
4. `TryAutoOpen()` does nothing silently for a brand-new player (`IsFirstLaunchRedirect`). After a Full Reset the modal won't appear, and the tool doesn't say why.
5. The window's "Claimable Today" status doesn't update while you play, because the window only repaints on GUI events.
6. There's no way to see the modal for a given day without claiming it, which grants power-ups and sends analytics.

# UI
Updated **Daily Login Streak** box in the Player Reset Tool (wireframe):
```
┌ Daily Login Streak ─────────────────────────────┐
│ Current Streak Day:   3                          │
│ Last Claimed:         2026-05-12 (yesterday)     │
│ Claimable Today:      Yes (Day 4: 2 DELETE)      │
│ ⚠ HelpBox: "New player – modal is suppressed     │
│   until the Level 1 tutorial is done." [Fix: Set │
│   to Level 2]          (only when it applies)    │
│                                                  │
│ Make Ready to Claim                              │
│ Next Claim Day  [====o======] 4                  │
│ [ Make Claimable as Day 4 (+2 DELETE) ]  (green) │
│ [ Advance One Day ]  [ Simulate Missed Day ]     │
│ [ Reset Daily Streak ]                           │
│                                                  │
│ Fast Iteration                                   │
│ [x] Every main-menu visit is a new day           │
│                                                  │
│ Play Mode only                                   │
│ [ Open Modal Now ] [ Preview Day N (no grant) ]  │
└──────────────────────────────────────────────────┘
```
- Play-Mode-only buttons are disabled (greyed out) in Edit Mode, with a tooltip explaining why.
- Status messages keep using the window's existing `HelpBox`.

# Key Asset & Context
| File | Change |
|---|---|
| `Assets/Scripts/UI/DailyLoginStreakUI.cs` | Add `private bool previewOnly;` to fix the compile error. Add `#if UNITY_EDITOR` hooks: `DebugPreviewDay(int day)` (fills in and opens the modal with `previewOnly = true`, with no `Claim()`, no grant, and no analytics), plus a check for the "every visit is a new day" toggle in `TryAutoOpen()`. Add `public static bool WouldSuppressForFirstLaunch` (wraps `IsFirstLaunchRedirect`) so the tool can show a warning. |
| `Assets/Scripts/Gameplay/DailyLoginStreakService.cs` | Add editor helper `DebugAdvanceOneDay()`, which moves the last claim back one day and keeps the streak. Fix indentation inside the `#if UNITY_EDITOR` block. No runtime logic changes. |
| `Assets/Editor/SaveDataManagerWindow.cs` | Swap the file I/O in `ResetDailyStreak` / `MakeDailyRewardClaimable` / `SimulateMissedDailyLogin` / `DrawDailyStreakBox` for calls to the service's `Debug*` helpers. Remove `WriteDailyStreak`. Add the new UI from the wireframe. Repaint while playing (`OnInspectorUpdate`). |

Key signatures:
```csharp
// DailyLoginStreakUI (editor-only)
#if UNITY_EDITOR
public void DebugPreviewDay(int day);            // opens modal, previewOnly = true
public static bool WouldSuppressForFirstLaunch { get; }
#endif

// TryAutoOpen addition (editor-only, before the CanClaim check):
#if UNITY_EDITOR
if (DailyLoginStreakService.DebugEveryMenuVisitIsNewDay && !DailyLoginStreakService.CanClaim)
    DailyLoginStreakService.DebugSetNextClaimDay(DailyLoginStreakService.DebugNextDayAfterToday);
#endif

// DailyLoginStreakService (editor-only)
public static void DebugAdvanceOneDay();         // lastClaim -= 1 day, keep streakDay
```
Note: when the streak is already on Day 7, "every visit is a new day" stays on Day 7. This matches the live `Mathf.Min(StreakLength, …)` behavior.

# Implementation Steps
Each step is small, done one at a time, and ends with a review stop.

**Step 1 – Fix compile error (S)**
- `DailyLoginStreakUI.cs`: declare `private bool previewOnly;`.
- Dependency: none.
- 🔍 Review checkpoint: Console shows 0 compile errors and you can enter Play Mode on MainMenu.

**Step 2 – Tool uses the service's helpers (S)**
- `SaveDataManagerWindow.cs`: `ResetDailyStreak` → `DailyLoginStreakService.DebugReset()`, `MakeDailyRewardClaimable` → `DebugSetNextClaimDay`, `SimulateMissedDailyLogin` → `DebugBreakStreak`. `DrawDailyStreakBox` reads `DebugLastClaimDate` / `DebugStoredStreakDay`. Delete `WriteDailyStreak` and the direct JSON parsing. Keep the public method names so the menu items still work.
- Dependency: Step 1.
- 🔍 Review checkpoint: the three existing buttons behave as before. The JSON file in persistentDataPath shows the expected dates.

**Step 3 – "Advance One Day" + live status + first-launch warning (S)**
- `DailyLoginStreakService.cs`: add `DebugAdvanceOneDay()`.
- `DailyLoginStreakUI.cs`: add `WouldSuppressForFirstLaunch`.
- `SaveDataManagerWindow.cs`: add the *Advance One Day* button (it also reopens the modal in Play Mode). Show "(today/yesterday/N days ago)" after Last Claimed. Show a warning HelpBox with a **Set to Level 2** fix button when the first-launch check applies. Add `OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }`.
- Dependency: Step 2.
- 🔍 Review checkpoint: after Full Reset the warning appears, and the fix button makes the modal show on the next Play.

**Step 4 – "Every menu visit is a new day" toggle (S)**
- `DailyLoginStreakUI.TryAutoOpen()`: add the editor-only check shown above.
- `SaveDataManagerWindow.cs`: add a checkbox bound to `DebugEveryMenuVisitIsNewDay`, with a red "ON" hint so you don't forget it's enabled.
- Dependency: Step 2.
- 🔍 Review checkpoint: with the toggle on, going Menu → level → Menu moves through Days 1→7, granting the matching power-ups each time.

**Step 5 – Play Mode buttons: Open Now / Preview Day N (S)**
- `DailyLoginStreakUI.cs`: `DebugPreviewDay(int day)` → `Populate(day, RewardForDay(day))`, sets `previewOnly = true`, opens the panel. `Dismiss` already skips analytics when `previewOnly` is set.
- `SaveDataManagerWindow.cs`: **Open Modal Now** (calls `TryAutoOpen` on all `DailyLoginStreakUI`) and **Preview Day N** (uses the slider value). Both are disabled outside Play Mode.
- Dependency: Step 1.
- 🔍 Review checkpoint: Preview Day 7 shows the bundle tile and doesn't change the power-up counts or the streak file.

# Verification & Testing
- **Compile:** 0 errors after each step (Console).
- **Manual matrix (Play Mode on MainMenu, tutorial complete):**
  - Reset → Day 1 modal, +1 DELETE.
  - Make Claimable Day 4 → modal "4 / 7", tiles 1–4 checked, +2 DELETE.
  - Advance One Day after a Day 4 claim → Day 5 modal.
  - Simulate Missed Day → Day 1 modal, and `daily_streak_broken` is logged by LevelAnalytics.
  - Already claimed today → "Open Modal Now" does nothing, and the status shows "No (already claimed)".
  - Preview Day N → inventory and streak file unchanged, and no `daily_reward_popup_shown` analytics on close.
  - Full Reset (new player) → no modal, and the window shows the first-launch warning.
- **Build safety:** check that all new runtime members are inside `#if UNITY_EDITOR` (an iOS build or `BuildPipeline` script-only compile succeeds).
- **Existing tests:** run the EditMode tests (`UIMotionConsistencyTests` references the DailyLoginRewards panel) to confirm nothing regressed.
