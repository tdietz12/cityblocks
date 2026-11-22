# Cloud Accounts, Economy, Retention Analytics, Monetization & UI Export — rev. 2

> **This file replaces `cloud-accounts-economy-monetization-ui-export.md`.** That file's first 16 KB were corrupted (zeroed) during editing and it should be deleted once this plan is approved. Everything below is the full plan with the rev. 2 changes applied.
>
> Scope: (A) Player tracking on Unity Gaming Services (UGS), (B) Monetization rework (coins, bundles, no-ads, fail-screen continues), (C) Editor tool that exports every UI screen/modal/button to the Desktop as PNG + Figma-ready SVG package.
>
> **Rev. 2:** reordered so **player tracking comes first** (Milestones A–H, Steps 1–39); monetization UI, compliance, UI export and launch follow (Milestones I–L).
>
> **Decisions confirmed by user**
> | Topic | Decision |
> |---|---|
> | Wallet authority | **UGS Economy + Cloud Code** (server-authoritative grants, Apple receipt redemption, Access Control) |
> | Sign-in | **Silent Game Center** on launch → fallback one-time screen (Sign in with Apple / Continue as Guest) → Sign in with Apple linkable from Settings |
> | Analytics | **Migrate to UGS Analytics**; **remove Firebase entirely (Analytics + Crashlytics)** — rev. 2, user decision. No crash reporter for now |
> | Coins | **Earned + purchased** |
> | Offline | Levels & Endless **playable offline**; power-ups, coins, continues, store **locked offline** |
> | UI export | **PNG screenshots + Figma-ready package** (SVG per screen + component PNGs + manifest) |
> | **Order (rev. 2)** | **Player tracking first.** Analytics → retention events → accounts → cloud inventory + heartbeat/offline lock → cloud progress → cloud-validated purchases. Store UI / coin continues and the UI export come afterwards |
> | **First-launch sign-in (rev. 2)** | Confirmed: silent Game Center → one-time Sign in with Apple / Continue as Guest screen → link later in Settings. The account (Game Center or Apple) carries progress, coins and power-ups across devices |
> | **Retention event scope (rev. 2)** | Level lifecycle (start / win / lose / restart / quit / resume) + power-up use + extra-move continues, **plus** all four groups: Session & funnel, Economy flow, Store & ads, Daily & re-engagement |

---

# Project Overview
- **Game Title:** CityBlocks
- **High-Level Concept:** A merge-and-stack city tower puzzle. Players drop towers into columns, merge equal levels into taller buildings, and complete objective-driven levels (Leveling mode) or chase high scores (Endless mode).
- **Players:** Single player (cloud-synced account across devices)
- **Inspiration / Reference Games:** Royal Match, Candy Crush Saga (continue/booster economy), 2048 / Merge Towers (core merge loop)
- **Tone / Art Direction:** Clean stylized low-poly city (Environment_4 / City packs), gold accent (#D4A424) on slate UI (#B8C4D0), TMP typography
- **Target Platform:** iOS (Apple App Store). Code stays platform-guarded so Android can be added later.
- **Screen Orientation / Resolution:** Portrait. Canvas reference 393x852 (iPhone logical points); export target 1290x2796 (iPhone 15/16 Pro Max @3x)
- **Render Pipeline:** URP (URP-HighFidelity)

---

# Game Mechanics

## Core Gameplay Loop
1. **Play** – Start a level (or Endless run), drop/merge towers within a move limit to meet objectives.
2. **Outcome** – Win → coins reward + next level unlocked. Lose → **Continue decision** (spend coins for +5 moves, buy the Rescue Bundle, or Retry/Quit).
3. **Progress & Reward** – Coins accumulate from wins, first-try clears, daily login streak, and Endless milestones. Power-ups make hard levels possible.
4. **Return** – Daily login streak (existing `DailyLoginStreakService`), local notifications (existing `DailyLoginNotification`), and cross-device cloud progress bring players back.

**Why it works (MDA / SDT):**
- *Competence* – power-ups and continues turn a near-miss loss into a recoverable moment ("so close" effect) instead of churn.
- *Autonomy* – silent sign-in, no forced login wall, guest option; players choose when to spend.
- *Relatedness/Ownership* – cloud account carries progress across devices, raising switching cost and long-term retention.
- Coin sink (continues/boosters) ↔ coin faucet (wins/daily) creates an economy where purchases accelerate, not gate, progression.
- *Measurement* – every step of this loop (start → outcome → continue decision → return) emits a retention event, so D1/D7 retention, per-level difficulty and continue conversion can be read directly from UGS Analytics.

### Economy design (defaults — all tunable via Remote Config)
| Faucet | Coins |
|---|---|
| First clear of a level | 50 (+25 if attempt #1) |
| Replay win | 10 |
| Daily login streak (existing table) | 100 / 150 / 200 / 250 / 300 / 400 / 500 |
| Endless milestone (every 5 tower levels reached, new record only) | 25 |
| New-account welcome grant | 500 coins + 1 of each power-up |

| Sink | Cost |
|---|---|
| Continue #1 (+5 moves) in same attempt | 900 coins |
| Continue #2 | 1,500 coins |
| Continue #3+ | 2,500 coins |
| (Optional later) Buy single power-up with coins | 400–600 coins |

### IAP catalog (Apple price tiers; Economy Real-Money Purchases)
| Product ID | Type | Price | Contents |
|---|---|---|---|
| `cityblocks.noads` | Non-consumable | $4.99 | Remove interstitial ads forever (existing `NoAdsOffer`) |
| `cityblocks.bundle.starter` | Consumable, once per account | $4.99 | 1,000 coins + 2× each power-up ("Best Value" badge) |
| `cityblocks.bundle.small` | Consumable | $9.99 | 2,500 coins + 3× each power-up |
| `cityblocks.bundle.medium` | Consumable | $19.99 | 6,000 coins + 5× each power-up ("Popular") |
| `cityblocks.bundle.large` | Consumable | $49.99 | 16,000 coins + 12× each power-up |
| `cityblocks.bundle.mega` | Consumable | $99.99 | 35,000 coins + 25× each power-up |
| `cityblocks.bundle.rescue` | Consumable | $4.99 | **+5 moves (applied to current failed level)** + 1,000 coins + 1× each power-up — shown only on the fail screen (replaces existing `ExtraMovesOffer`) |

Power-ups (existing `GameController.PowerUpType`): `delete`, `lookAhead` (stored as `deleteRowUses`), `levelUp`, `extraTurns`.

> Current state (verified): all three `Assets/Resources/StoreOffers/*.asset` have an **empty `productId`**, so no real purchases are live yet. That's why Step 26 can safely switch power-ups to the cloud wallet before purchases move to the cloud in Milestone H.

## Controls and Input Methods
- Touch only (existing tap-to-drop on `Cell`, legacy Input Manager / uGUI `EventSystem`). **No input changes.**
- New UI uses standard uGUI `Button`s with existing `UIButtonAnimator` press feedback and `UIPanelAnimator` open/close.
- Buttons ≥ 44pt tap target, primary CTA in the thumb zone (bottom 40% of screen).
- Purchase / coin-spend buttons show a busy spinner and are non-interactable while a server call is in flight (prevents double-taps).

---

# UI

All new UI is **uGUI** under the existing `LevelRulesUI` canvas (EndlessMode scene) or MainMenu canvas, follows `UIPanelAnimator` patterns, and reuses existing fonts/colors.

### 1. Sign-in fallback (only if Game Center unavailable/cancelled, first launch only)
```
┌──────────────────────────────┐
│          CITYBLOCKS logo         │
│                                  │
│   Save your city across devices  │
│                                  │
│  [  Sign in with Apple  ]   │ ← Apple HIG black button
│                                  │
│      Continue as Guest           │ ← text button
│  (You can link later in Settings)│
└──────────────────────────────┘
```

### 2. Main Menu additions
```
┌──────────────────────────────┐
│ [⚙]          [🪙 2,450 +]  [☁︎✓] │ ← settings, coin chip (opens store), sync/offline icon
│                                  │
│          LEVEL 23  [PLAY]        │
│          [ENDLESS]               │
│   [STORE]   [DAILY]   [NO ADS]   │
└──────────────────────────────┘
```

### 3. Store screen (replaces combined `StoreOffersWindow` content)
```
┌──────────────────────────────┐
│  STORE             🪙 2,450  [X] │
│ ┌──────────────────────────┐ │
│ │ REMOVE ADS          $4.99    │ │ (hidden when owned)
│ └──────────────────────────┘ │
│ ┌──────────────────────────┐ │
│ │ ★ STARTER PACK  BEST VALUE   │ │
│ │ 🪙1,000  🗑2 ⬆2 👁2 +3 2    │ │
│ │                    [$4.99]   │ │
│ └──────────────────────────┘ │
│  … Small / Medium(POPULAR) /     │
│    Large / Mega  (ScrollRect)    │
│  [Restore Purchases]             │ ← required by App Review
└──────────────────────────────┘
```
Prices use `Product.metadata.localizedPriceString` from Unity IAP (never hard-coded).

### 4. Level fail / Continue modal (rework of `LevelLossPanel`)
```
┌──────────────────────────────┐
│        OUT OF MOVES!             │
│   Objective: Reach Tower 8 (7/8) │
│                                  │
│ [ +5 MOVES   🪙 900 ]            │ ← primary (green), disabled + "Not enough coins → Store" if short
│ [ RESCUE BUNDLE  $4.99 ]         │ ← +5 moves, 1,000 coins, 1× each power-up
│                                  │
│   Retry          Main Menu       │
│ ⚠ Connect to the internet to    │ ← offline banner (replaces both CTAs)
│   continue                       │
└──────────────────────────────┘
```

### 5. Play HUD additions
- Coin chip (top-right) and offline icon.
- Power-up buttons show lock badge + toast "Power-ups need an internet connection" when offline.

### 6. Settings → Account panel (new)
- Signed in as: Game Center alias / Apple / Guest.
- [Link Sign in with Apple] (if not linked), Game Center status.
- [Restore Purchases].
- [Delete Account] → confirmation modal (App Store guideline 5.1.1(v) requirement).
- Privacy/analytics consent toggle (UGS Analytics data collection).

### 7. Global toasts / modals
- "Purchase complete" reward reveal (coins + power-up icons fly to HUD chip).
- "Purchase failed / pending" modal.
- "You're offline" toast; "Syncing…" indicator.

---

# Key Asset & Context

## Existing code that will be modified (verified in project)
| File | Current role | Change |
|---|---|---|
| `Assets/Scripts/Gameplay/PowerUpStore.cs` | Static inventory backed by local `level-progress.json`; `FulfillPurchase`, `TryUse`, `AdsRemoved` | Step 9: retention events. Step 26: becomes a façade over new `PlayerWallet` (server-backed cache); `TryUse` → async server-confirmed `UsePowerUpAsync`. Step 37: local fulfilment removed (Economy grants). |
| `Assets/Scripts/Gameplay/UnityIapProvider.cs` | Unity IAP 5 `StoreController`; grants locally then `ConfirmPurchase` | On `OnPurchasePending` → `EconomyService.Instance.Purchases.RedeemAppleAppStorePurchaseAsync(...)` → only on success refresh wallet + `ConfirmPurchase`. `RestoreEntitlements` → redeem no-ads via Economy. Add public `RestorePurchases()` + a pending-transaction check for the heartbeat. |
| `Assets/Scripts/Gameplay/StoreOfferDefinition.cs` | ScriptableObject offer | Add `economyPurchaseId`, `coins`, `oneTimePerAccount`, `badge`, `sortOrder`, `showInStore`, `showOnFailScreen`; remove/obsolete `priceInPowerUpUses`. |
| `Assets/Scripts/Gameplay/LevelAnalytics.cs` | Firebase event façade (`Record`, typed helpers, `EventRecorded` hook) | Keep public API (all call sites keep working); swap backend to UGS Analytics via new `AnalyticsSink`; central common params; new typed events (Milestone B). |
| `Assets/Scripts/Gameplay/FirebaseBootstrap.cs` | Firebase App + Analytics + Crashlytics | **Deleted** (Step 5, done). |
| `Assets/Scripts/Gameplay/LevelSession.cs` | Level lifecycle: `Initialize`, `Retry`, `ContinueAfterExtraMoves`, `Complete`, `Fail`/`FinalizeFailure`, `ExitToMenu` | Step 7: `attemptId` (GUID), `continuesUsed`, `level_restart`, `level_resume`, richer complete/fail/quit params. Step 29: queue results to `CloudProgressService`. |
| `Assets/Scripts/Gameplay/GameController.cs` | Power-up use via `PowerUpStore.TryUse` (lines ~389–553), Endless start/fail (~185, ~922), `BoosterUsed` (~945) | Step 8: `endless_new_record`. Step 9: `booster_used` params + `insufficient_funds`. Step 26: power-ups gated by `ConnectivityService.IsOnline`; async confirm. Step 30: Endless results → `CloudProgressService`. |
| `Assets/Scripts/Gameplay/LevelProgressData.cs` / `LevelProgressStore.cs` | Local JSON progress | Remains the **offline cache**; add `LevelRunData.attemptId/continuesUsed` (Step 7), `pendingResults`, `lastSyncUtc` (Step 27). Wallet fields become cache-only (never authoritative). |
| `Assets/Scripts/Gameplay/DailyLoginStreakService.cs` | Local clock streak, raw `LevelAnalytics.Record` call | Step 10: typed `daily_reward_claimed` + `daily_streak_broken`. Step 32: claims move server-side (`ClaimDailyReward` Cloud Code, server time) and grant **coins**. |
| `Assets/Scripts/Gameplay/DailyLoginNotification.cs` | Schedules the daily local notification | Step 10: `notification_opened` on launch. |
| `Assets/Scripts/UI/LevelTutorialController.cs`, `TutorialStep.cs` | Tutorial flow | Step 8: `tutorial_step`, `tutorial_complete`. |
| `Assets/Scripts/UI/PlayButton_MainMenu.cs`, `LevelButton_MainMenu.cs` | Menu level entry | Step 8: `level_select`. |
| `Assets/Scripts/Data Persistence/ProgressionData.cs` | `recordScore`, `recordLevel`, `softCurrency`, `hardCurrency` | Endless records synced to cloud; `softCurrency/hardCurrency` deprecated (wallet lives in Economy). |
| `Assets/Scripts/UI/LevelRulesUI.cs` | HUD, `ShowLoss()`, `BuyExtraMoves()` | Step 9: `continue_offered/declined`. Later: continue modal logic (coins + Rescue Bundle + offline state); coin chip; power-up lock states. |
| `Assets/Scripts/UI/StoreOfferWindow.cs` | Combined daily special + no-ads panel | Step 11 audit; later replaced by `StoreScreen` (list of `StoreOfferCard`s). |
| `Assets/Scripts/Gameplay/UnityAdsProvider.cs` | Interstitials, checks `PowerUpStore.AdsRemoved` | Reads entitlement from `PlayerWallet.AdsRemoved` (server-backed, cached). |
| `Assets/Resources/StoreOffers/*.asset` | `DailySpecialOffer`, `NoAdsOffer`, `ExtraMovesOffer` (all with empty `productId`) | Replace with the 7 catalog assets above. |
| `Packages/manifest.json` | Has `com.unity.purchasing` 5.4.3, `com.unity.ads` 4.19.0, `com.unity.mobile.notifications` 2.4.2; **no UGS packages yet** | Add UGS packages (below). |

## New runtime scripts (`Assets/Scripts/Services/`)
```csharp
// Boot order: UnityServices → Auth → Remote Config → Economy config sync → Bootstrap snapshot
public sealed class CloudBootstrap : MonoBehaviour            // RuntimeInitializeOnLoad, DontDestroyOnLoad
{
    public static Task Ready { get; }                          // completes when online-ready or offline fallback decided
    public static event Action<CloudState> StateChanged;       // Offline, SigningIn, Online, Syncing
}

public sealed class SessionTracker : MonoBehaviour             // rev. 2 — session_start / session_end, session_number (UGS sessionID covers ids)
{
    public static int SessionNumber { get; }
    public static bool IsFirstLaunch { get; }
}

public static class AccountService
{
    Task<bool> TrySilentGameCenterAsync();                     // Apple.GameKit → GKLocalPlayer.Authenticate + FetchItems
    Task SignInWithAppleAsync(bool link);                      // native Sign in with Apple → idToken → Sign/Link
    Task ContinueAsGuestAsync();                               // SignInAnonymouslyAsync
    AccountKind Current { get; }                               // GameCenter | Apple | Guest
    Task DeleteAccountAsync();                                 // CloudCode DeleteMyData → AuthenticationService.DeleteAccountAsync
}

public static class ConnectivityService                        // heartbeat owner
{
    bool IsOnline { get; }                                     // authorized && last heartbeat OK && reachability != NotReachable
    event Action<bool> OnlineChanged;
    // 60 s while foregrounded, immediately on OnApplicationPause(false), on network change
}

public static class PlayerWallet                               // server snapshot cache
{
    int Coins { get; } int PowerUps(GameController.PowerUpType t); bool AdsRemoved { get; }
    event Action Changed;
    Task<bool> UsePowerUpAsync(GameController.PowerUpType t, string attemptId);
    Task<ContinueResult> BuyContinueAsync(int level, string attemptId, int continueIndex);
    void ApplySnapshot(PlayerSnapshot s);
}

public static class CloudProgressService
{
    void QueueLevelResult(LevelResult r);                      // persisted in LevelProgressData.pendingResults; flushed when online
    void QueueEndlessResult(EndlessResult r);
    Task FlushAsync();
}

public static class AnalyticsSink                              // UGS Analytics wrapper used by LevelAnalytics.Record
{
    void Record(string name, IDictionary<string, object> p);   // AnalyticsService.Instance.RecordEvent(new CustomEvent(name){...})
}
```

## Cloud Code C# module (project root, per UGS module layout)
```
CityBlocksCCM/
  .gitignore                (!*.sln !*.csproj !Properties/PublishProfiles/*.pubxml)
  CityBlocks.sln
  CityBlocks/CityBlocks.csproj (net9.0, Com.Unity.Services.CloudCode.Apis, .Core)
  CityBlocks/ModuleSetup.cs
  CityBlocks/PlayerModule.cs
  CityBlocks/Properties/PublishProfiles/FolderProfile.pubxml (linux-x64)
  CityBlocks.Tests/…        (IsPublishable=false)
Assets/CloudCode/CityBlocks.ccmr   → { "modulePath": "../../CityBlocksCCM/CityBlocks.sln" }
```
Endpoints (`[CloudCodeFunction]`):
| Endpoint | Purpose | Writes |
|---|---|---|
| `Bootstrap(clientVersion, legacyPayload?)` | First call after sign-in. Creates welcome grant once, one-time capped legacy migration, returns `PlayerSnapshot` | Protected Cloud Save `profile`, Economy grants |
| `Heartbeat(sessionId)` | Returns slim snapshot (coins, power-ups, adsRemoved, serverTimeUtc, configVersion); updates `last_seen` | Protected `profile.lastSeen` |
| `SubmitLevelResult(level, attemptId, outcome, score, movesRemaining, continuesUsed)` | Idempotent by `attemptId`; max-merges progress; grants win coins | Protected `level_progress`, Economy COINS |
| `SubmitEndlessResult(runId, score, levelReached)` | Max-merges records; milestone coins | Protected `endless_progress` |
| `UsePowerUp(type, attemptId, useId)` | Idempotent decrement | Economy PU_* |
| `BuyContinue(level, attemptId, continueIndex)` | Debit escalating coin cost (Remote Config), returns `movesGranted` | Economy COINS |
| `ClaimDailyReward()` | Server-time streak, grants coins | Protected `daily_streak`, Economy COINS |
| `DeleteMyData()` | Wipes Cloud Save + Economy balances before auth deletion | all |

## Cloud resources (`Assets/CloudServices/`, deployed via Services > Deployment)
- `Economy`: currencies `COINS`, `PU_DELETE`, `PU_LOOKAHEAD`, `PU_LEVELUP`, `PU_EXTRATURNS`; inventory item `NO_ADS`; Real-Money Purchases `RMP_NOADS`, `RMP_STARTER`, `RMP_SMALL`, `RMP_MEDIUM`, `RMP_LARGE`, `RMP_MEGA`, `RMP_RESCUE` mapped to Apple product IDs.
- `CityBlocks.rc` Remote Config: `coin_rewards` (JSON), `continue_costs` `[900,1500,2500]`, `continue_moves` 5, `heartbeat_seconds` 60, `welcome_grant` (JSON), `store_layout` (JSON order/badges).
- `CityBlocks.ac` Access Control: deny-all-then-allow template; **players can only**: sign in/link, read Economy config/currencies/inventory, `purchases/appleappstore` (receipt redemption), call Cloud Code modules, read own Protected Cloud Save. **Denied:** direct currency/inventory writes, virtual purchases (not used), Cloud Save writes, unlink, player-auth users write (deletion goes through our flow — allow `users**` Write only if `DeleteAccountAsync` requires it; verify in Step 21).

## Packages to add
| Package | Version (min) |
|---|---|
| `com.unity.services.core` | 1.16.0 |
| `com.unity.services.authentication` | 3.6.1 |
| `com.unity.services.cloudcode` | 2.10.3 |
| `com.unity.services.cloudsave` | 3.4.0 |
| `com.unity.services.economy` | latest verified for 6000.4 (check Package Manager) |
| `com.unity.services.analytics` | latest verified for 6000.4 |
| `com.unity.remote-config` | 4.2.5 |
| `com.unity.services.deployment` | 1.7.2 |
| `com.unity.services.tooling` | 1.4.1 |
| Apple Unity Plugins: `com.apple.unityplugin.core` + `com.apple.unityplugin.gamekit` | built from github.com/apple/unityplugins (tarballs in `Packages/Apple/`) |
| Sign in with Apple native bridge | e.g. `com.lupidan.apple-signin-unity` (returns identityToken) |

Removed (Step 5, done): the whole Firebase SDK (`Assets/Firebase`, `Assets/Plugins/iOS|tvOS/Firebase`, `GoogleService-Info.plist`, `google-services-desktop.json`, editor icons) and Firebase's EDM4U copy (`Assets/ExternalDependencyManager`). Unity IAP re-installs its own resolver at `Assets/MobileDependencyResolver` — keep it.

## Editor tooling (new)
- `Assets/Editor/UIExport/UIExportManifest.cs` (ScriptableObject listing every screen state: scene, root object path, objects to enable/disable, setup hook name, mock data).
- `Assets/Editor/UIExport/UIExportRunner.cs` (menu **Tools ▸ CityBlocks ▸ Export UI to Desktop**).
- `Assets/Editor/UIExport/UguiSvgWriter.cs` (RectTransform/Image/TMP → SVG).
- `Assets/Editor/UIExport/UIExportMockState.cs` (fake wallet/progress so screens render populated without network).

---

## Analytics event taxonomy (target state)
All events include `player_level_max`, `session_number`, `is_online`, `account_kind` — plus UGS's own standard `sessionID`/`userID`/`playerID`, which the SDK stamps automatically, so there is no custom `session_id` (added centrallyin `LevelAnalytics.Record`, so call sites stay one-liners; `is_online` is reachability-only until the heartbeat lands in Step 24, and `account_kind` is `anonymous` until Step 15). All level events (`level_start/complete/fail/restart/quit/resume`, `continue_*`, `booster_used`) also carry `attempt_id`, so one attempt's whole story can be joined in the Data Explorer. UGS rejects unregistered events, so each step that adds events also lists what to register in Dashboard ▸ Analytics ▸ Event Manager.

| Event | Params | Fired from | Step |
|---|---|---|---|
| `session_start` **(new)** | `session_number`, `is_first_launch`, `days_since_install`, `days_since_last_session` | SessionTracker (cold start + resume after > 5 min in background) | 6 |
| `session_end` **(new)** | `duration_s`, `levels_played`, `screen`(menu/level/endless) | SessionTracker (`OnApplicationPause(true)`) | 6 |
| `level_start` | `level_number`, `attempt_number`, `attempt_id` | LevelSession.Initialize (exists) | 7 |
| `level_resume` **(new)** | `level_number`, `attempt_number`, `attempt_id`, `moves_remaining` | LevelSession.Initialize (saved run resumed) | 7 |
| `level_complete` | + `moves_remaining`, `score`, `continues_used` | LevelSession.Complete (exists) | 7 |
| `level_fail` | + `failed_objective`, `score`, `continues_used` | FinalizeFailure (exists) | 7 |
| `level_restart` **(new)** | `level_number`, `attempt_number`, `attempt_id`, `from_state`(playing/fail) | LevelSession.Retry | 7 |
| `level_quit` | existing + `attempt_id`, `moves_remaining` | ExitToMenu | 7 |
| `level_select` **(new)** | `level_number`, `highest_unlocked`, `source`(play_button/level_list) | PlayButton_MainMenu / LevelButton_MainMenu | 8 |
| `tutorial_step` **(new)** | `step_id` | LevelTutorialController | 8 |
| `tutorial_complete` **(new)** | `duration_s` | LevelTutorialController / `LevelSession.CompleteLevelTutorial` | 8 |
| `endless_level_start/fail/quit` | existing | GameController/LevelFlow | — |
| `endless_new_record` **(new)** | `score`, `level_reached` | Endless fail | 8 |
| `booster_used` | existing + `attempt_id`, `balance_after`, `is_online` | GameController.RecordBoosterUse | 9 |
| `insufficient_funds` | existing (now also fired when a power-up has 0 uses) | GameController / LevelRulesUI | 9 |
| `continue_offered` **(new)** | `level_number`, `continue_index` (+ `coin_cost`, `coins_balance` from Step 43) | ShowLoss | 9 |
| `continue_purchased` **(new)** | + `currency`(iap → coins from Step 43), `moves_granted` | Extra Moves offer → BuyContinue / Rescue | 9 |
| `continue_declined` **(new)** | `level_number`, `choice`(retry/menu) | Loss buttons | 9 |
| `powerup_granted` **(new)** | `booster_id`, `amount`, `source`(welcome/purchase/daily/migration), `balance_after` | PowerUpStore → later PlayerWallet | 9 |
| `daily_reward_claimed` | existing + `coins` | DailyLoginStreakService | 10 |
| `daily_streak_broken` **(new)** | `previous_streak`, `days_missed` | DailyLoginStreakService | 10 |
| `notification_opened` **(new)** | `notification_id`, `hours_since_scheduled` | DailyLoginNotification (on launch) | 10 |
| `store_open`, `view_item_list`, `view_item`, `begin_checkout` | existing | StoreOfferWindow → StoreScreen | 11 |
| `adImpression` (UGS built-in, replaces `ad_impression`; no common params, nothing to register), `ad_load_failed` (`error_code` prefixed `load:`/`show:`) | existing | UnityAdsProvider | 11 ✅ |
| `sign_in` | `method`(gamecenter/apple/guest/session), `is_new_player` | AccountService | 15–16 |
| `account_linked` | `provider` | AccountService | 17 |
| `coins_earned` / `coins_spent` **(new)** | `amount`, `source`/`sink`, `balance_after` | PlayerWallet (from server response) | 22+ |
| `offline_mode_changed` **(new)** | `is_online`, `duration_s` | ConnectivityService | 24 |
| `booster_blocked_offline` **(new)** | `booster_id` | GameController | 26 |
| `purchase` → UGS `transaction` standard event | `productID`, `transactionID`, `realCurrency`, items | UnityIapProvider after Economy redemption | 37 |
| `purchase_failed` | existing + `reason` | UnityIapProvider | 37 |
| `purchase_restored` **(new)** | `product_id`, `trigger`(heartbeat/resume/restore_button) | ConnectivityService purchase refresh | 38 |

---

# Implementation Steps

## How we'll work through these
- Every step is **small and self-contained** and ends at a **🔍 Review checkpoint**. I execute **one step per request**, then stop and report: files changed, how to verify, and any open questions. You review and leave feedback, we adjust the plan if needed, and then you tell me to run the next step.
- **🧑 You** marks manual work I can't do from the Editor (Unity Dashboard, App Store Connect, device/TestFlight testing, building Apple plugin tarballs). For those I give you exact click-by-click instructions.
- Order is chosen so the **game stays shippable after every step**: new systems are added next to the existing ones first, and a separate **switch-over** step moves gameplay onto them.
- Size: **S** ≈ 1–3 files, about one sitting · **M** ≈ 4–8 files.
- **Player-tracking scope = Milestones A–H (Steps 1–39).** Milestones I–L are the monetization UI, compliance, UI export and launch.
- **Step 0 (housekeeping, first thing after approval):** delete the corrupted `Assets/Plans/cloud-accounts-economy-monetization-ui-export.md` (+ `.meta`).

---

## Milestone A — Foundations (no gameplay change)

#### Step 1 — Link UGS project & environments · 🧑 You · S
- **Do:** Project Settings ▸ Services → link to a Unity Cloud project; in the Dashboard create `development` and `production` environments; enable Analytics.
- **🔍 Checkpoint:** Project ID shows in Project Settings ▸ Services.
- **Depends on:** —

#### Step 2 — Add UGS packages · S
- **Files:** `Packages/manifest.json` (core, authentication, cloudcode, cloudsave, economy, analytics, remote-config, deployment, tooling).
- **🔍 Checkpoint:** Project compiles, console clean, existing EditMode tests in `Assets/Editor/Tests/` still pass.
- **Depends on:** 1

#### Step 3 — `CloudBootstrap` (init + temporary anonymous session) · S
- **Files:** new `Assets/Scripts/Services/CloudBootstrap.cs`.
- **Behavior:** `UnityServices.InitializeAsync()` with environment set by build type → restores the cached session or signs in anonymously → logs `PlayerId`. Replaced by real sign-in in Step 15.
- **🔍 Checkpoint:** Entering Play Mode twice logs the **same** PlayerId.
- **Depends on:** 2

#### Step 4 — Send analytics to UGS (alongside Firebase for now) · S
- **Files:** new `Assets/Scripts/Services/AnalyticsSink.cs`; `LevelAnalytics.cs` (`Record` sends to both backends; `EventRecorded` hook stays; common params `session_number`, `player_level_max`, `is_online`, `account_kind` added centrally).
- **Note:** UGS Analytics doesn't collect anything until data collection is started explicitly after consent. Use the consent API of the installed Analytics version (`StartDataCollection()` or the newer end-user-consent API); the in-app toggle comes in Step 46. Events recorded before init/sign-in are buffered by `AnalyticsSink` and flushed once `CloudBootstrap.Ready` completes (offline events are cached by the SDK and uploaded later).
- **🧑 You:** register the existing events in Event Manager (I'll give you the list).
- **🔍 Checkpoint:** `level_start` / `level_complete` / `booster_used` show up in the UGS Event Browser (dev).
- **Depends on:** 3

#### Step 5 — Remove Firebase entirely (Analytics + Crashlytics) · S · ✅ done (rev. 2: user chose to drop Crashlytics too, and to run this before finishing Step 4's dashboard checkpoint)
- **Files:** `FirebaseBootstrap.cs` deleted; `LevelAnalytics.cs` (UGS only); deleted the Firebase SDK, native plugins, config files and Firebase's EDM4U copy.
- **🔍 Checkpoint:** Compiles with no errors/warnings; no `Firebase` references anywhere in `Assets`; an iOS build no longer pulls Firebase pods (verify on the next device build).
- **Depends on:** 4 + your confirmation that UGS events are arriving

#### Step 5b — Unity Cloud crash & exception reporting · S (🧑 partly you) · *added in rev. 2 (replaces Crashlytics)* · ✅ done (test exception with player metadata confirmed in Cloud Diagnostics; device symbol check pending first TestFlight build)
- **🧑 You:** Window ▸ General ▸ Services ▸ **Cloud Diagnostics** ▸ turn on **Crash and Exception Reporting** (and enable Cloud Diagnostics for CityBlocks in the Dashboard if asked).
- **Files:** `CloudBootstrap.cs` (after sign-in: `UnityEngine.CloudDiagnostics.CrashReportHandler.SetUserMetadata` with `player_id`, `environment`, `account_kind` so crashes can be matched to analytics players); temporary debug menu item to throw a test exception (removed afterwards).
- **Notes:** check that iOS symbol upload is enabled so native crash stacks are readable; check which free-tier limits apply on the UGS pricing page before launch.
- **🔍 Checkpoint:** A test exception from the Editor/development build appears under Dashboard ▸ Cloud Diagnostics with the player metadata; a forced crash on a TestFlight device shows a readable stack trace.
- **Depends on:** 3

---

## Milestone B — Retention tracking(client-only, works with today's game) · *added in rev. 2*
> These steps need only Steps 1–5. They instrument the game as it is today, so you start getting retention data (D1/D7 funnels, level difficulty, restart loops, power-up use, continue conversion) **before** the accounts and cloud-inventory work. Later steps only add or adjust parameters (for example `currency=coins` on continues once coin continues exist).

#### Step 6 — Session & first-launch funnel · S · ✅ code done (also: `account_kind` is now resolved when an event is sent, so events buffered before sign-in report the real account; register session_start/session_end in Event Manager)
- **Files:** new `Assets/Scripts/Services/SessionTracker.cs` (`RuntimeInitializeOnLoad`, DontDestroyOnLoad; install date, session counter and last-session time kept in PlayerPrefs); `LevelAnalytics.cs` (`SessionStarted`, `SessionEnded`).
- **Behavior:** `session_start` on cold start and on resume after more than 5 min in the background; `session_end` on `OnApplicationPause(true)` with duration and levels played. `session_number` feeds the common params from Step 4 (UGS's standard `sessionID` already covers session grouping).
- **🔍 Checkpoint:** Background the app for more than 5 min and come back → a second `session_start` with `session_number` + 1; the first ever launch has `is_first_launch=1`.
- **Depends on:** 4

#### Step 7 — Level lifecycle completeness (start / win / lose / restart / quit / resume) · M · ✅ code done (update level_* schemas + register level_resume/level_restart in Event Manager)
- **Files:** `LevelProgressData.cs` (`LevelRunData.attemptId`, `LevelRunData.continuesUsed`), `LevelSession.cs`, `LevelAnalytics.cs`.
- **Behavior:**
  - `Initialize`: a new run gets a GUID `attemptId`; a resumed saved run fires `level_resume` instead of a second `level_start` (today a resume double-counts starts).
  - `Retry()`: fires `level_restart` with `from_state` = `fail` (when `awaitingFailureDecision`) or `playing`, **before** the new `level_start`.
  - `Complete` / `FinalizeFailure`: add `score`, `continues_used`, `attempt_id`; `ExitToMenu`: add `attempt_id` and `moves_remaining` to `level_quit`.
  - `ContinueAfterExtraMoves`: increments `continuesUsed` (stored on the run so it survives relaunch).
- **🔍 Checkpoint:** Play → restart mid-level → fail → retry → win: the event log (EventRecorded hook / Console) shows `start, restart(playing), start, fail, restart(fail), start, complete`, all with the right `attempt_id`s; killing and relaunching mid-level shows `level_resume`.
- **Depends on:** 4

#### Step 8 — Menu & tutorial funnel + Endless records · M · ✅ code done (register level_select, tutorial_step, tutorial_complete, endless_new_record)
- **Files:** `PlayButton_MainMenu.cs`, `LevelButton_MainMenu.cs` (`level_select`), `LevelTutorialController.cs` / `TutorialStep.cs` (`tutorial_step`, `tutorial_complete`), `GameController.cs` (Endless fail at ~line 922 → `endless_new_record` when `ProgressionData.recordScore`/`recordLevel` is beaten), `LevelAnalytics.cs`.
- **🔍 Checkpoint:** On a fresh install, finishing the tutorial logs every step in order followed by `tutorial_complete`; beating the Endless record logs `endless_new_record` once per run.
- **Depends on:** 4

#### Step 9 — Power-up & continue flow (today's inventory and Extra Moves offer) · M · ✅ code done (`insufficient_funds` moved to Step 43: power-up buttons are disabled at 0 uses, so a 0-use tap can't happen until the 'not enough → Store' UI exists)
- **Files:** `GameController.cs` (`booster_used` + `attempt_id`, `balance_after`; `insufficient_funds` when a power-up has 0 uses), `LevelRulesUI.cs` (`continue_offered` in `ShowLoss`, `continue_declined` on Retry / Main Menu with `choice`), `PowerUpStore.cs` (`continue_purchased` with `currency=iap` after the Extra Moves offer is fulfilled; `powerup_granted` in `ConfirmBundlePurchase`), `LevelAnalytics.cs`.
- **🔍 Checkpoint:** Fail a level → `continue_offered`; choose Retry → `continue_declined(choice=retry)`; use each power-up → `booster_used` with the right `balance_after`; tapping a power-up with 0 uses → `insufficient_funds`.
- **Depends on:** 7

#### Step 10 — Daily & re-engagement · S · ✅ code done (notification_opened counts cold starts from a reminder tap; register daily_streak_broken, notification_opened, add reward_amount to daily_reward_claimed)
- **Files:** `DailyLoginStreakService.cs` (move its raw `Record` call to a typed `DailyRewardClaimed`; add `daily_streak_broken` with previous streak and days missed), `DailyLoginNotification.cs` (`notification_opened` via `iOSNotificationCenter.GetLastRespondedNotification()` on launch), `LevelAnalytics.cs`.
- **🔍 Checkpoint:** Opening the app from the daily notification logs `notification_opened`; skipping a day (simulated in the Editor by moving the stored last-claim date) logs `daily_streak_broken`.
- **Depends on:** 4

#### Step 11 — Store & ads audit, event tests, Event Manager registration · S (🧑 partly you) · ✅ code done (audit fixes: combined store panel also logs view_item for no_ads; ad_load_failed error_code prefixed load:/show:; booster_id null guard. ad_impression → UGS built-in `adImpression` via `AnalyticsSink.RecordStandard`, sent once per ad when it closes, with completion/click/watch time and placement `return_to_menu`/`after_win`. StoreProviderBridge deleted (unused, would double-send purchase). Tools ▸ City Blocks ▸ Tests ▸ Level Analytics Checks passes 1177 checks. Left: 🧑 final Event Manager registration check + 10-min device session)
- **Files:** `StoreOfferWindow.cs`, `UnityAdsProvider.cs`, `StoreProviderBridge.cs` (check that every existing store/ad event goes through `LevelAnalytics` with consistent params; nothing calls a backend directly); new `Assets/Editor/Tests/LevelAnalyticsTests.cs` (every public `LevelAnalytics` method → expected name + params via `EventRecorded`).
- **🧑 You:** register all Milestone B events + parameters in Dashboard ▸ Analytics ▸ Event Manager (I'll give you a copy-paste table of event names, parameter names and types).
- **🔍 Checkpoint:** Tests pass; after a 10-minute play session on device, every event from Steps 6–10 appears in the UGS Event Browser with no "invalid event" entries.
- **Depends on:** 6, 7, 8, 9, 10

---

## Milestone C — Accounts

#### Step 12 — Apple account setup · 🧑 You · S
- **Do:** App Store Connect: enable the Game Center and Sign in with Apple capabilities for the bundle ID. Unity Dashboard ▸ Authentication ▸ Identity Providers: add **Apple** and **Apple Game Center**.
- **🔍 Checkpoint:** Both providers show as enabled in the Dashboard.
- **Depends on:** 1

#### Step 13 — Import Apple plugins · S (🧑 partly you)
- **Files:** `Packages/manifest.json`, `Packages/Apple/*.tgz` (Apple.Core + Apple.GameKit, built on your Mac with Apple's build script; I'll provide the commands), Sign in with Apple plugin.
- **🔍 Checkpoint:** Compiles in the Editor and an iOS build succeeds.
- **Depends on:** 2

#### Step 14 — iOS capabilities post-process · S
- **Files:** new `Assets/Editor/iOSCapabilitiesPostProcess.cs` (Game Center, Sign in with Apple and In-App Purchase entitlements via `ProjectCapabilityManager`).
- **🔍 Checkpoint:** The generated Xcode project lists all 3 capabilities.
- **Depends on:** 13

#### Step 15 — Silent Game Center sign-in · M
- **Files:** new `Assets/Scripts/Services/AccountService.cs`; `CloudBootstrap.cs` (cached session → Game Center → anonymous fallback for now); `LevelAnalytics.cs` (`sign_in` with `method`, `is_new_player`; `account_kind` common param becomes real).
- **🔍 Checkpoint (device):** Fresh install with Game Center signed in → only the system banner appears; `PlayerInfo` shows a Game Center identity; reinstalling keeps the same PlayerId; `sign_in(method=gamecenter)` logged.
- **Depends on:** 3, 12, 14

#### Step 16 — Sign in with Apple + Guest fallback screen · M
- **Files:** `AccountService.cs`; new `Assets/Scripts/UI/SignInPanel.cs`, `Assets/Prefabs/UI/SignInPanel.prefab`; `MainMenu.unity`.
- **🔍 Checkpoint (device):** With Game Center signed out the panel appears once; Apple and Guest both work; the panel never shows again after a choice is made; `sign_in(method=apple|guest)` logged.
- **⚠ Release gate:** Apple requires in-app account deletion once the app lets players create an account, so **no App Store submission may include this step until Step 45 (Delete Account) is done.** TestFlight is fine.
- **Depends on:** 15

#### Step 17 — Account Settings panel (status + linking) · M
- **Files:** new `AccountSettingsPanel.cs` + prefab; settings button on MainMenu; `LevelAnalytics.cs` (`account_linked`).
- **Behavior:** Shows the current account type; links Sign in with Apple / Game Center; on `AccountAlreadyLinked` shows a "Switch to that account?" dialog (never force-links).
- **🔍 Checkpoint:** A guest who links Apple keeps the same PlayerId and progress; linking an Apple ID that belongs to another player shows the dialog.
- **Depends on:** 16

---

## Milestone D — Cloud backend skeleton

#### Step 18 — Cloud Code module scaffold + `Heartbeat` · M
- **Files:** `CityBlocksCCM/**` (sln, csproj, ModuleSetup, PlayerModule with `Heartbeat` returning server time, FolderProfile.pubxml, .gitignore, tests), `Assets/CloudCode/CityBlocks.ccmr`, generated bindings.
- **🧑 You:** install the .NET 9 SDK if it's missing; press Deploy in Services ▸ Deployment.
- **🔍 Checkpoint:** Deployment goes green; a Play Mode call logs the server time.
- **Depends on:** 3

#### Step 19 — Economy currencies & entitlement · S
- **Files:** `Assets/CloudServices/Economy/` → `COINS`, `PU_DELETE`, `PU_LOOKAHEAD`, `PU_LEVELUP`, `PU_EXTRATURNS`, inventory item `NO_ADS`.
- **🔍 Checkpoint:** Deployed; visible in the Dashboard ▸ Economy.
- **Depends on:** 18

#### Step 20 — Remote Config tuning values · S
- **Files:** `Assets/CloudServices/CityBlocks.rc` (`coin_rewards`, `continue_costs`, `continue_moves`, `heartbeat_seconds`, `welcome_grant`, `store_layout`).
- **🔍 Checkpoint:** Deployed; a Play Mode fetch logs `continue_costs = [900,1500,2500]`.
- **Depends on:** 18

#### Step 21 — Access Control policy · S
- **Files:** `Assets/CloudServices/CityBlocks.ac` (deny-all-then-allow).
- **🔍 Checkpoint:** A debug call where the client tries to increment a balance itself is **refused**; sign-in and Cloud Code calls still work.
- **Depends on:** 19

#### Step 22 — `Bootstrap` endpoint (welcome grant + snapshot) · M
- **Files:** `PlayerModule.cs` + tests; client `PlayerSnapshot` DTO.
- **🔍 Checkpoint:** A new player receives 500 coins + 1 of each power-up exactly once (unit test + Dashboard player view); `powerup_granted(source=welcome)` / `coins_earned(source=welcome)` logged from the snapshot response.
- **Depends on:** 19, 20, 21

---

## Milestone E — Cloud inventory & heartbeat (display only)

#### Step 23 — `PlayerWallet` cache + coin chip · M
- **Files:** new `PlayerWallet.cs`, `CoinChipUI.cs`; coin chip added to the MainMenu and `LevelRulesUI.prefab` PlayPanel. Power-ups still use the local inventory at this point.
- **🔍 Checkpoint:** The chip shows the cloud balance after launch and the last cached value when offline.
- **Depends on:** 22

#### Step 24 — Heartbeat + offline indicator · M
- **Files:** new `ConnectivityService.cs`, `OfflineIndicatorUI.cs`; `LevelAnalytics.cs` (`offline_mode_changed`; `is_online` common param now comes from the heartbeat).
- **Behavior:** Each successful beat applies the inventory snapshot (coins, power-ups, no-ads) to `PlayerWallet`. Purchase refresh joins the same loop in Step 38, once purchases are validated by the cloud.
- **🔍 Checkpoint:** In airplane mode the offline icon appears within 2 heartbeats and clears when the connection returns; the event is logged; changing a balance in the Dashboard shows up in the game within one heartbeat.
- **Depends on:** 18, 23

---

## Milestone F — Power-ups move to the server + offline lock (switch-over)

#### Step 25 — `UsePowerUp` endpoint · S
- **Files:** `PlayerModule.cs` + tests (idempotent by `useId`).
- **🔍 Checkpoint:** Tests pass; deployed.
- **Depends on:** 22

#### Step 26 — Switch power-ups to the cloud wallet + offline lock · M
- **Files:** `PowerUpStore.cs` (becomes a façade over `PlayerWallet`; the local bundle-grant path is disabled — safe now because every `StoreOffers/*.asset` still has an empty `productId`, so no real purchases exist yet), `GameController.cs` (`TryBeginPowerUp`, `ActivateLookAhead`, `Apply*PowerUp` wait for server confirmation), `LevelRulesUI.cs` (lock badge + toast), power-up button scripts, `LevelAnalytics.cs`.
- **🔍 Checkpoint:** Using a booster lowers the cloud balance; offline → buttons locked, toast shown, `booster_blocked_offline` logged; a rapid double-tap uses only one; `booster_used.balance_after` matches the server value.
- **Depends on:** 9, 24, 25

---

## Milestone G — Progress tracking (Leveling + Endless in the cloud)

#### Step 27 — Pending results queue (local only) · S
- **Files:** `LevelProgressData.cs` (`pendingResults`, `lastSyncUtc`), `LevelProgressStore.cs`, new `CloudProgressService.cs`, new `PendingResultsQueueTests.cs`.
- **🔍 Checkpoint:** Tests pass; nothing calls the cloud yet.
- **Depends on:** —

#### Step 28 — `SubmitLevelResult` endpoint · S
- **Files:** `PlayerModule.cs` + tests (idempotent by `attemptId`, max-merge, first-clear coins).
- **🔍 Checkpoint:** Tests pass; deployed.
- **Depends on:** 22

#### Step 29 — Wire `LevelSession` to cloud progress · M
- **Files:** `LevelSession.cs` (queue a result on Complete / FinalizeFailure using the `attemptId` + `continuesUsed` added in Step 7), `LevelAnalytics.cs` (`coins_earned(source=level_win)` from the server response), `ConnectivityService.cs` (flushes the queue on heartbeat).
- **🔍 Checkpoint:** Win a level offline → coins appear after reconnecting; the Dashboard shows a Protected `level_progress` key.
- **Depends on:** 7, 24, 27, 28

#### Step 30 — Endless results to the cloud · M
- **Files:** `PlayerModule.cs` (`SubmitEndlessResult`), `GameController.cs`, `DataPersistenceController.cs` (the `endless_new_record` event already exists from Step 8).
- **🔍 Checkpoint:** The Endless record shows in the cloud and is restored on a second device.
- **Depends on:** 8, 29

#### Step 31 — Merge on sign-in + legacy migration · M
- **Files:** `CloudProgressService.cs`, `PlayerModule.cs` (`Bootstrap` legacy payload with caps), new `ProgressMergeTests.cs`.
- **🔍 Checkpoint:** An existing install at local level 20 updates → the cloud shows level 20; a second device restores it; progress never goes down.
- **Depends on:** 29, 30

#### Step 32 — Server-side daily reward pays coins · M
- **Files:** `PlayerModule.cs` (`ClaimDailyReward`), `DailyLoginStreakService.cs`, `DailyLoginStreakUI.cs`, `DailyLoginNotification.cs` (streak events from Step 10 now use server time).
- **🔍 Checkpoint:** Claiming grants coins; changing the device clock doesn't allow a second claim; offline shows "Connect to claim".
- **Depends on:** 10, 24

---

## Milestone H — Purchases through the cloud (validated grants + heartbeat refresh)
> Needed so that purchased coins / power-ups / no-ads live in the cloud inventory like everything else, and so the heartbeat can refresh purchases. This is the end of the **player tracking** scope.

#### Step 33 — IAP products & Apple integration · 🧑 You · S
- **Do:** Create the 7 IAP products in App Store Connect; add the Apple App Store integration (API key / shared secret) in Dashboard ▸ Economy; set up a Sandbox tester.
- **🔍 Checkpoint:** Products are in "Ready to Submit"; the integration is saved.
- **Depends on:** 12

#### Step 34 — Economy Real-Money Purchases · S
- **Files:** Economy config → `RMP_NOADS`, `RMP_STARTER`, `RMP_SMALL`, `RMP_MEDIUM`, `RMP_LARGE`, `RMP_MEGA`, `RMP_RESCUE` mapped to the Apple product IDs with their rewards.
- **🔍 Checkpoint:** Deployed; contents match the catalog table.
- **Depends on:** 19, 33

#### Step 35 — Receipt-redemption spike · S · ⚠ decision point
- **Files:** temporary debug script (removed afterwards).
- **Do:** Buy the Starter Pack in Sandbox on a device → redeem the Unity IAP 5.4.3 receipt with `RedeemAppleAppStorePurchaseAsync`.
- **🔍 Checkpoint:** **Works** → continue to Step 36. **Fails** (StoreKit 2 format) → insert Step 35b: a Cloud Code `RedeemAppleTransaction` endpoint that verifies the signed transaction with Apple's App Store Server API.
- **Depends on:** 34

#### Step 36 — Store offer data · S
- **Files:** `StoreOfferDefinition.cs` (new fields), the 7 assets in `Assets/Resources/StoreOffers/` (replacing `DailySpecialOffer` / `ExtraMovesOffer`), updated references in `LevelRulesUI.prefab` / `StoreOffersWindow.prefab`.
- **🔍 Checkpoint:** Compiles; the 7 offers appear in the Inspector; existing buttons are still bound.
- **Depends on:** 34

#### Step 37 — IAP switch-over to server-validated grants · M
- **Files:** `UnityIapProvider.cs` (redeem with Economy before `ConfirmPurchase`, `RestorePurchases()`, purchases blocked offline), `PowerUpStore.cs` (local grant path removed), `LevelAnalytics.cs` (UGS `transaction` event; `powerup_granted` / `coins_earned` with `source=purchase`).
- **🔍 Checkpoint:** In Sandbox, items arrive only after server validation; killing the app mid-purchase → granted exactly once on relaunch.
- **Depends on:** 26, 35, 36

#### Step 38 — Heartbeat purchase refresh · S · *added in rev. 2*
- **Files:** `ConnectivityService.cs` (on resume, on every 5th beat and when coming back online: `FetchPurchases()` → redeem any unconfirmed transactions with Economy → apply the returned inventory), `UnityIapProvider.cs` (expose a pending-transactions check), `LevelAnalytics.cs` (`purchase_restored` with `trigger`).
- **🔍 Checkpoint:** Buy in Sandbox, go to airplane mode before the grant arrives, come back online → the grant arrives through the heartbeat exactly once and `purchase_restored(trigger=heartbeat)` is logged.
- **Depends on:** 24, 37

#### Step 39 — No-ads from the cloud entitlement · S
- **Files:** `PowerUpStore.cs` / `PlayerWallet.cs` (`AdsRemoved`), `UnityAdsProvider.cs`, NO ADS menu button.
- **🔍 Checkpoint:** Buying no-ads stops interstitials; after reinstall + Restore ads stay off; ads stay off offline.
- **Depends on:** 37

---

## Milestone I — Monetization UI (after tracking is complete)

#### Step 40 — Store screen UI · M
- **Files:** new `StoreScreen.cs`, `StoreOfferCard.cs`; rework of `StoreOffersWindow.prefab`; `StoreMenuButton.cs`; replaces `StoreOfferWindow.cs`.
- **🔍 Checkpoint:** You review a screenshot; all 6 store items can be bought in Sandbox; localized prices shown; Restore works; store events logged.
- **Depends on:** 37

#### Step 41 — Purchase result modals + reward reveal · S
- **Files:** new `PurchaseResultModal.cs` + prefab.
- **🔍 Checkpoint:** Success shows the rewards flying to the coin chip; failure and pending show clear messages.
- **Depends on:** 40

#### Step 42 — `BuyContinue` endpoint · S
- **Files:** `PlayerModule.cs` + tests (escalating cost from Remote Config, idempotent by `attemptId` + `continueIndex`).
- **🔍 Checkpoint:** Tests pass; deployed.
- **Depends on:** 22

#### Step 43 — Coin continue on the fail screen · M
- **Files:** `LevelRulesUI.cs` (`ShowLoss`, continue button, "not enough coins → Store", offline banner; also wire `insufficient_funds` here and for 0-use power-up taps, moved from Step 9), `LevelLossPanel` in `LevelRulesUI.prefab`, `LevelSession.cs`, `LevelAnalytics.cs` (the `continue_*` events from Step 9 gain `coin_cost`, `coins_balance`, `currency=coins`; `coins_spent(sink=continue)`).
- **🔍 Checkpoint:** Costs go 900 → 1,500 → 2,500 within one attempt; the same board continues; events logged; offline shows the banner.
- **Depends on:** 26, 42

#### Step 44 — Rescue Bundle on the fail screen · S
- **Files:** `LevelRulesUI.cs`, `LevelRulesUI.prefab`.
- **🔍 Checkpoint:** A Sandbox purchase gives +5 moves, coins and power-ups; if the app is killed mid-purchase the moves are applied to the saved run on relaunch.
- **Depends on:** 37, 43

*(The old "Remaining retention events" step was removed in rev. 2 — its events now ship in Milestone B and in the steps that create them.)*

---

## Milestone J — Compliance

#### Step 45 — Delete Account · M
- **Files:** `PlayerModule.cs` (`DeleteMyData`), `AccountService.cs`, `AccountSettingsPanel.cs` + confirmation modal.
- **🔍 Checkpoint:** After deleting, the next launch is a brand-new player; the Dashboard shows the data removed.
- **⚠ Must ship before** any App Store submission that includes Step 16 (it can be pulled forward to right after Step 31 if you want to release the account features early).
- **Depends on:** 17, 31

#### Step 46 — Analytics consent toggle · S
- **Files:** `AnalyticsSink.cs`, `AccountSettingsPanel.cs`.
- **Note (found in Step 10):** iOS player builds define `ENABLE_UNITY_CONSENT`, where `StartDataCollection()` is obsolete and **throws if anything has set `EndUserConsent`** (nothing does today, so it works). Switch `AnalyticsSink` to `EndUserConsent.SetConsentState` (AnalyticsIntent only, preserving AdsIntent) under `#if ENABLE_UNITY_CONSENT`, keeping `StartDataCollection` for the Editor.
- **🔍 Checkpoint:** Turning it off stops data collection; the setting persists across launches.
- **Depends on:** 11, 17

---

## Milestone K — UI export to Desktop (after all screens exist)

#### Step 47 — Export mock state · S
- **Files:** new `Assets/Editor/UIExport/UIExportMockState.cs` (+ an Editor-only snapshot injection hook in `PlayerWallet`).
- **🔍 Checkpoint:** Screens render populated (2,450 coins, level 23) with no network.
- **Depends on:** 40–44

#### Step 48 — Screen-state list · S
- **Files:** new `UIExportManifest.cs` + `UIExportManifest.asset` (about 30 states: menus, store, sign-in, settings, HUD online/offline, power-up panels, win, loss variants, Endless lose, combo overlay, modals, toasts).
- **🔍 Checkpoint:** **You review the list** and add or remove states before anything is captured.
- **Depends on:** 47

#### Step 49 — Full-screen PNG export · M
- **Files:** new `UIExportRunner.cs` (menu **Tools ▸ CityBlocks ▸ Export UI to Desktop**; Play Mode runner; 1290x2796 render target; never saves scenes).
- **🔍 Checkpoint:** `~/Desktop/CityBlocks_UI_Export_<date>/Screens/PNG/` contains every state; you review the images.
- **Depends on:** 48

#### Step 50 — Component PNG export · M
- **Files:** `UIExportRunner.cs`, new `UIComponentCapture.cs` (each button in normal/disabled state, plus icons, on transparent backgrounds).
- **🔍 Checkpoint:** You review the `Components/Buttons` and `Components/Icons` folders.
- **Depends on:** 49

#### Step 51 — SVG + Figma package · M
- **Files:** new `UguiSvgWriter.cs`; `UIExportRunner.cs` (`Screens/SVG/`, `manifest.json`, `README_Figma.md`); new `UIExportSmokeTest.cs`.
- **🔍 Checkpoint:** Dragging an SVG into Figma gives named, editable layers; the smoke test passes.
- **Depends on:** 49

---

## Milestone L — Cleanup & launch

#### Step 52 — Remove dead code · S
- **Files:** ~~`StoreProviderBridge.cs`~~ (already deleted in Step 11), leftoverlocal grant code in `PowerUpStore.cs`, deprecated `softCurrency/hardCurrency`, the spike script.
- **🔍 Checkpoint:** Compiles; all tests pass; nothing in the scenes is missing a reference.
- **Depends on:** 37–44

#### Step 53 — Production deploy & release checklist · S (🧑 + me)
- **Do:** Deploy the `.ccmr`, `.rc`, `.ac` and Economy files to `production`; switch the build environment; run the full device checklist below on TestFlight.
- **🔍 Checkpoint:** Every item under Verification & Testing passes.
- **Depends on:** all

## Design notes referenced by the steps
- **Sign-in order (Steps 15–17):** cached session token → `SignInAnonymouslyAsync()` restores it silently (works for any linked account) · otherwise Game Center: `GKLocalPlayer.Authenticate()` → `FetchItems()` → `SignInWithAppleGameCenterAsync(signature, teamPlayerId, publicKeyUrl, salt, timestamp)` · otherwise the fallback panel: `SignInWithAppleAsync(idToken)` or `SignInAnonymouslyAsync()`. On a new device, signing in with the same Game Center/Apple ID returns the same `PlayerId`, so the cloud snapshot restores everything.
- **Heartbeat (Steps 24, 29, 38):** `Heartbeat` every `heartbeat_seconds` (60) while in the foreground, immediately on resume and when reachability changes. Two consecutive failures or `NotReachable` → offline (power-ups, coins, continues and store lock instantly). Each successful beat applies the inventory snapshot (Step 24) and flushes pending level/Endless results (Step 29); on resume, every 5th beat and when coming back online it also runs `FetchPurchases()` to finish any interrupted transactions (Step 38).
- **Analytics while offline (Steps 4–11):** gameplay events are still recorded offline; the UGS SDK caches them and uploads on reconnect, with `is_online=0` so offline play can be analysed separately.
- **Server-confirmed spends (Steps 26, 43):** the button shows busy and input is locked during the call, with a 3 s timeout. Balances only change on the server, so a timeout never double-charges; idempotency keys (`useId`, `attemptId`+`continueIndex`) are stored in Protected `processed_ops` (ring buffer of the last 500).
- **Merge rules (Step 31):** `highestUnlockedLevel = max`, `completedLevels = union`, records = max; progress never goes down. Legacy wallet migration is capped server-side (for example ≤ 50 of each power-up).
- **No-ads offline (Step 39):** a PlayerPrefs flag is kept as an offline cache so ads stay off without a connection.
- **UI export rendering (Steps 49–51):** the root Canvas temporarily switches to `ScreenSpaceCamera` with an export camera → 1290x2796 `RenderTexture` → `ReadPixels`/`EncodeToPNG`. Components are cropped using `RectTransformUtility` on transparent backgrounds. SVG: a `<g id=GameObjectName>` per node; `Image` → embedded base64 `<image>` (9-slice baked at size) or `<rect>`; `TMP_Text` → `<text>`; masks → `<clipPath>`. Output layout:
  ```
  ~/Desktop/CityBlocks_UI_Export_<yyyyMMdd_HHmm>/
    Screens/PNG/<Scene>__<State>.png
    Screens/SVG/<Scene>__<State>.svg
    Components/Buttons/<Name>__normal.png, __disabled.png
    Components/Icons/<SpriteName>.png
    manifest.json      (screens, element tree, rects in px & pt, colors, font refs)
    README_Figma.md    (drag Screens/SVG into a Figma page; frames are 393x852pt @3x; fonts to install)
  ```

---

# Verification & Testing

## Automated (EditMode, `Assets/Editor/Tests/`)
- `LevelAnalyticsTests`: every public `LevelAnalytics` method emits the expected name/params through `EventRecorded` (guards analytics taxonomy).
- `LevelSessionAnalyticsTests` (rev. 2): drives `LevelSession` through start → restart → fail → continue → win and asserts the exact event order, shared `attempt_id`, `continues_used`, and that a resumed run fires `level_resume` (not a second `level_start`).
- `SessionTrackerTests` (rev. 2): first launch → `is_first_launch=1`; background < 5 min → no new session; > 5 min → `session_number` + 1.
- `ProgressMergeTests`: local vs cloud merge (max unlocked, union completed, record max, never downgrades).
- `ContinueCostTests`: escalating cost by `continueIndex`, insufficient-coins path.
- `OfflineGatingTests`: with `ConnectivityService` forced offline, `PowerUpStore.TryUse`/`BuyContinue`/`RequestPurchase` all refuse and emit `booster_blocked_offline`.
- `PendingResultsQueueTests`: results persist across reload of `LevelProgressStore` and flush exactly once (idempotent `attemptId`).
- Cloud Code `CityBlocks.Tests` (NUnit, .NET 9): idempotency of `SubmitLevelResult`/`UsePowerUp`/`BuyContinue`, reward amounts, migration caps.
- `UIExportSmokeTest`: runs exporter on 2 states into a temp folder; asserts PNG dimensions 1290x2796 and SVG parses as XML with named `<g>` nodes.

## Manual device checks (TestFlight, Sandbox Apple ID)
1. Fresh install with Game Center signed in → no sign-in UI, banner only, `sign_in(method=gamecenter,is_new_player=1)`, welcome grant visible.
2. Fresh install with Game Center signed out → fallback screen; Sign in with Apple works; Guest works; later link from Settings.
3. Second device, same Game Center/Apple ID → identical level progress, Endless record, coins, power-ups, no-ads.
4. Purchase each bundle in Sandbox → balance updates only after Economy redemption; kill app mid-purchase → relaunch → heartbeat/`FetchPurchases` completes grant exactly once.
5. No-ads: buy, reinstall, Restore Purchases → ads stay off; offline after owning → ads still off.
6. Fail a level with ≥ 900 coins → +5 moves continues same board; 2nd continue costs 1,500; insufficient coins → Store opens; Rescue Bundle → moves + rewards.
7. Airplane mode: levels & Endless playable; power-up buttons locked with toast; continue/store show offline banner; win offline → coins appear after reconnect (queued result).
8. Delete Account → data wiped, next launch is a brand-new player.
9. UGS Analytics Event Browser shows all events from checks 1–8 with correct params; a forced test crash on device appears in Cloud Diagnostics with a readable stack and player metadata.
10. Retention funnel check (after Step 11, again at launch): in the Data Explorer, build `session_start → level_select → level_start → level_complete` and `level_fail → continue_offered → continue_purchased` funnels and confirm numbers are plausible for a 1-day TestFlight group; level 1–10 win rate and restart rate can be charted per level from `attempt_id`.
11. Heartbeat refresh: change a player's coin balance in the Dashboard → the game shows it within one heartbeat; airplane mode during a purchase → grant arrives once after reconnect.
12. Run **Tools ▸ CityBlocks ▸ Export UI to Desktop** → folder appears on Desktop with all ≈30 screen PNGs/SVGs, component PNGs, manifest; drag SVGs into Figma → editable named layers.

## Edge cases
- Token expiry mid-session (`Expired` → auto refresh; `SignInFailed` → offline mode, not crash).
- `AccountAlreadyLinked` when linking Apple ID used by another player → clear choice dialog.
- Clock manipulation on daily reward (server time wins).
- Duplicate transaction redelivery (Economy + `processed_ops` dedupe).
- Continue tapped twice quickly (button busy lock + server idempotency by `continueIndex`).
- Cold-start latency of Cloud Code (~first call after 15 min idle) → spinner, 3 s timeout → treat as temporarily offline for that action, never charge twice.
- Resumed level run after relaunch must not double-count `level_start` (Step 7 `level_resume`).
- Events fired before `CloudBootstrap.Ready` (e.g. `session_start` on cold start) are buffered, not dropped.

## Build checks
- iOS build succeeds; Xcode project contains Game Center, Sign in with Apple, In-App Purchase capabilities.
- No Firebase references remain (`rg -i "firebase" Assets` → empty).
- Deployment Window: `.ccmr`, `.rc`, `.ac`, Economy files deploy with no errors to `development`.

## Risks / open items to confirm during implementation
- **StoreKit 2 receipt ↔ Economy Apple redemption compatibility** (Step 35 spike; Cloud Code JWS fallback ready).
- **UGS Analytics consent API:** Editor uses `StartDataCollection()`; iOS builds define `ENABLE_UNITY_CONSENT` (obsolete warning, works while no SDK sets `EndUserConsent`). Migrate in Step 46.
- Exact Economy/Analytics package versions compatible with Unity 6000.4.5f1.
- Apple plugin build (Apple's unityplugins require building tarballs on macOS with Xcode).
- UGS Analytics consent requirements per region (GDPR/PIPL) — consent prompt behavior to be confirmed with legal.
- Fonts in SVG export: Figma substitutes missing fonts; README lists the TMP source fonts to install.
