# CityBlocks continuation prompt

Continue CityBlocks from Assets/Plans/cloud-accounts-economy-monetization-ui-export-rev2.md. Read this compact handoff first, then only relevant plan sections/dependencies/current source. Execute ONE small reviewable step per approval and stop for review. Same-file writes must be sequential; check file integrity after reconnects. Do not rerun completed deployments just because the connection dropped.

## Current checkpoint
Step 34 is implemented: six .ecr definitions in Assets/CloudServices/Economy were validated (78 deterministic JSON, mapping, reward, dependency, import and plan-integrity checks), deployed and published to DEVELOPMENT only. All six deployment items returned Deployed/Created remotely. A subsequent read-only admin resource read-back returned 12 resources: the six existing basic resources plus exactly six RMPs; Apple IDs and reward quantities match below. Production unchanged. No purchase/device/runtime verification is claimed.

| Economy ID | Apple ID | US price (user-confirmed) | Rewards |
|---|---|---|---|
| RMP_NOADS | cityblocks.noads | $4.99 | NO_ADS inventory x1 (non-consumable) |
| RMP_STARTER | cityblocks.bundle.starter | $4.99 | COINS 1000 + 2 each PU_* |
| RMP_SMALL | cityblocks.bundle.small | $9.99 | COINS 2500 + 3 each PU_* |
| RMP_MEDIUM | cityblocks.bundle.medium | $19.99 | COINS 6000 + 5 each PU_* |
| RMP_LARGE | cityblocks.bundle.large | $49.99 | COINS 16000 + 12 each PU_* |
| RMP_MEGA | cityblocks.bundle.mega | $99.99 | COINS 35000 + 25 each PU_* |

All bundles are consumables; Starter is intended once per account, but an RMP definition does NOT enforce that. Later server-side eligibility/race/replay protection is mandatory before enabling it. PU_* = PU_DELETE, PU_LOOKAHEAD, PU_LEVELUP, PU_EXTRATURNS. No Ads removes interstitials, not rewarded-ad features by implication.

Step 33: user confirmed six Apple drafts/localizations/prices saved, active Paid Apps Agreement/banking/tax, Sandbox tester ready. Do not demand screenshots for these confirmations. Review screenshots and submission remain pending; not claimed Ready to Submit. No native product fetching, Sandbox purchases or restoration tested.

## Approved scope changes
NO Rescue Pack, RMP_RESCUE, Rescue CTA or direct real-money extra-move grant. Any existing cityblocks.bundle.rescue Apple draft stays unsubmitted; do not delete automatically. Extra moves will be purchased with IN-GAME COINS via later BuyContinue flow. An IAP credits its wallet rewards only; never auto-spends coins or resumes a board. Old continue defaults (+5 moves; 900/1500/2500 coins) are provisional; confirm costs before implementation.

Legacy coin-only pack rework is DEFERRED by user. Live Approved consumables: CityBlocks400Gems, CityBlocks1000Gems, CityBlocks3000Gems, CityBlocks10000Gems. Preserve IDs, prices, metadata, availability and Apple purchase records. No new mappings/offers/migration for them now. Inspect live quantities/prices and old-client compatibility before separately approved coin-pack rework. The current checkout's empty offer IDs do not mean the released app lacks live IAP.

## Technical context and safety
Unity 6000.4.5f1, iOS, URP-HighFidelity, legacy Input Manager/uGUI. Live bundle ID MUST stay com.Unity-Technologies.com.unity.template.CityBlocks. UGS project CityBlocks 59df1639-a463-491f-8463-f5c8050ff1a0; development d2bd855b-8d2a-415c-85cc-ce00b5220dd9. Unity IAP 5.4.3, Economy 3.5.4. No package/settings/runtime/scene changes in Step 34. Existing three StoreOfferDefinition assets have empty productId/appleProductId; keep purchases disabled until safe server validation. Inspect PowerUpStore as well as UnityIapProvider: the provider retains FulfillPurchase/ConfirmPurchase calls but Step 26 disabled local consumable fulfillment, leaving pending transactions until Step 37.

Apple review: Economy request expects BASE64 APP RECEIPT. IAP exposes order.Info.Apple.AppReceipt and Apple.jwsRepresentation. order.Info.Receipt is Unity JSON wrapping AppReceipt; do not pass the entire JSON or JWS as the base64 receipt. StoreKit 2 alone is not proof of incompatibility; actual native app-receipt availability/redemption must be tested. Notification Integrations menus are NOT Apple store configuration. No verified API-key/shared-secret dashboard requirement found; do not generate credentials based on old plan wording. No forced legacy StoreKit, package-source changes or compatibility-guard bypass.

## Previous work/gates
Step 32 client close-out implemented; 22 deterministic checks passed. Existing 25c/25d server-UTC daily backend/catalog/migration retained, not rebuilt. Daily transport guards auth-session changes including A→B→A; popup clears stale account state, cached deployed schedule offline, Retry and 00:00 UTC copy. Legacy local Claim disabled without changing migration data.
Live/device gates for 25d/26/27/31/32 remain OPEN: actual daily/replay/offline/reconnect/migration, account/native flow, queue, wallet/debit recovery and second-device behavior. Backend tests were not rerun successfully at Step 32 or 34. No Step 32/34 iOS build, Play Mode/native/visual verification claimed. Pending uncertain server operations remain fail-closed; never erase receipts or blindly retry debits/grants. Existing reward journal/CAS and owner-bound queues must be preserved.

Step 45 account deletion is MANDATORY before any App Store submission including account features. Crash reporting is OUT OF SCOPE; historical Step 5b is not active work.

## NEXT (only after separate approval)
Step 35 device/Sandbox receipt-redemption spike using Starter and the published development config. Read Step 35, inspect actual provider/PowerUpStore/wallet/access policy before adding a temporary test path; do not enable unsafe local grants. Check mapping/environment/publish/receipt freshness/policy errors before choosing a server JWS fallback. Verify transaction/product identity, grants and duplicate/redelivery behavior; remove temporary harness afterward. Do not implement Steps 36–37, legacy coin packs or coin continues as part of the spike. Stop after the one approved step and report exactly what passed, failed and remains unverified.
