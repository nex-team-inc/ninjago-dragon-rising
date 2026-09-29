# v2-A — Simulation + Gameplay (balls-only rewards, stage-clear heal, Hype, analytics)

Owner key: `sim-gameplay` (editor lock label). Spec: `GDD-v2-Changes.md` §1, §3.
Owns: `Simulation/**` (+tests), `Gameplay/**`, `Persistence/**`, `Analytics/**`, `Configs/BalanceConfig.cs`,
`Configs/PacingConfig.cs`, new `Configs/HypeConfig.cs` (+ field on `BilliardRogueConfig`, creation/wiring in
`Editor/ConfigAssetsBuilder.cs`). Brief also names `DebugHooks.SetHype` + `DebugSettings.forceHype` (small additive edits).

## Status
- [x] 1. Balls-only RewardGenerator + stageClearHeal + tests
- [x] 2. BallSimulator.SetHype (speed w/ re-derived substeps, damage mult + min bonus) + tests
- [x] 3. HypeConfig asset + HypeController (Gameplay) + HUD/board/time-scale pushes + debug override
- [x] 4. Analytics per shot (avg/max Hype, Hype damage)
- [x] 5. Verify: EditMode Simulation tests 67/67; GameplaySmokeRunner 1234/1P + 777/2P OK; headless Hype smoke
      (scratch `HypeSmoke.cs`, see Verification)

## Next
Done. Only the Requests below remain for the integrator / other agents.

## API
- `BallSimulator.SetHype(speed, damage)` (contract) + overload `SetHype(speed, damage, minBonusDamage)`; read-back
  `HypeSpeedMultiplier`, `HypeDamageMultiplier`. Speed clamped 1..`BalanceRules.maxHypeSpeedMultiplier` (3); every
  base substep is split ⌈speed⌉ ways (no tunnelling). Damage ≥ 1 scales direct / chain / explosion / crate hits,
  rounded half up, floor `minBonusDamage`. `Clear()` resets Hype. Aim guide (`PredictPath`) ignores Hype.
- `SimEvent.hypeBonus` (EnemyHit: part of `value` added by Hype, overkill excluded); `DamageSource.hypeBonus`.
- `BalanceRules.stageClearHeal` (4): `RunFactory.CompleteStage` heals it every clear, + `bossHealFraction` on a boss
  (one PlayerHealed). `BalanceRules.maxHypeSpeedMultiplier` (3).
- `RewardGenerator`: up to 3 distinct ball types; below bagCap `NewBall`; at bagCap `UpgradeBall` of owned types only
  (lowest-level copy below levelCap, `amount` = new level → "Lv amount-1 → Lv amount"); Basic only as last resort
  (unless `offerBasicBall`); fewer than 3 only at bagCap (0 when all maxed → `RewardFlow` skips the view). Static
  helpers `TryMakeBallOption(rules, run, type, out option)`, `LowestLevelIndex(rules, run, type)`, const `OptionCount`.
  Heal/MaxHp enum values kept (append-only); a v1 save's pending Heal/MaxHp cards are re-rolled by `RewardFlow`.
- `HypeConfig` (Assets/Configs/BilliardRogue/HypeConfig.asset, `BilliardRogueConfig.Hype`, nullable → defaults):
  energy curve, attack 0.1 / release 0.5 s, tiers 0.25/0.55/0.85 (+ hysteresis 0.04), speed ×1.8, damage ×2.5,
  min bonus +1 from tier 2, hit-stop ×3 with ≤ 0.12 s extra per second, MOVE prompt below 0.2 for 1 s.
- `HypeController` (Gameplay, owned by `PlayerTurnLoop.Hype`): `Hype`, `Tier`, `MovePromptVisible`, `DebugOverride`,
  `Tick(unscaled, scaled)`, `Reset()`. Max `ShotInputRouter.MotionEnergy.Energy01` across players (null → 0), only
  while balls fly; pushes `BallSimulator.SetHype`, `BoardDriver.SetHype` → `BoardPresenter.SetHype`,
  `HudBinder.SetHype/ShowMovePrompt` (change-detected) → `IGameplayHud`, `TimeScaleController.SetHitStopScale`.
  The MOVE prompt needs a tracked motion source (or a debug override): keyboard play without energy never prompts.
- `TimeScaleController.SetHitStopScale(multiplier, extraCapPerSecond)`; `ResetEffects` resets it.
- Debug: `DebugHooks.SetHype(float)` (negative = off) → `HypeController.DebugOverride`; `DebugSettings.forceHype`
  (-1 off); `DebugHooks.State()` shows `hype=… tier=…`.
- Analytics: `ball_result` gains `hype_avg`, `hype_max` (0..1, flight-time weighted) and `hype_damage`.
  `SessionAnalytics.ShotResult` / `RunAnalytics.ShotResult` take `(…, hypeAverage, hypeMax, hypeDamage)`.

## Verification
- `python3 Tools/compile_check.py` green.
- EditMode `run_tests editor Nex.BilliardRogue.Simulation.Tests assembly`: 67/67 (new `HypeTests` 7, reward tests
  rewritten for v2, `EveryStageClearHealsAndABossClearAddsItsFraction`).
- `GameplaySmokeRunner.Run(1234,1,30000)` and `(777,2,30000)`: OK.
- Headless Hype smoke (16–18 shots, godMode): off → ball speed 13.0, dmg mult 1.00, dmg/hit 1.02, hit-stop ticks 21;
  `forceHype=1` → speed 23.4 (×1.8), dmg ×2.5, dmg/hit 2.25 (overkill clipped), tier 3, hit-stop ticks 74 (capped);
  `DebugHooks.SetHype(0.6)` → speed ×1.48, dmg ×1.9, tier 2.

## Requests
- v2-C UI: `RewardCard`/reward view — options are balls only; show "Lv amount-1 → Lv amount" for `UpgradeBall`;
  there may be 1–2 options at bagCap (0 never reaches the view). `IGameplayHud.SetHype(hype01, tier)` is pushed only
  on change (≥ 0.01 or tier change); tier stinger SFX belong to the HUD (tier rises arrive via SetHype).
- v2-D Presentation: `SimEvent.hypeBonus` on EnemyHit can size damage numbers; tiers are defined in `HypeConfig`
  (`config.Hype.TierThreshold(t)`) — prefer them over duplicating thresholds in JuiceConfig.
- v2-B Input: `HypeController` reads `ShotInputRouter.MotionEnergy` (Energy01 + IsTracked) for every player; the
  debug key / bot simulated energy should report `IsTracked = true` so the MOVE prompt logic applies.
- Integrator: `DebugSettings.forceHype` (PlayerData/DebugSettings.cs) and `DebugHooks.SetHype` (Flow/DebugHooks.cs)
  were added by this item (additive only).
