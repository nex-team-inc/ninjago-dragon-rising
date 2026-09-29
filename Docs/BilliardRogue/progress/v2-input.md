# v2-B Input — checkpoint

Owner: v2 input agent (editor lock key `v2-input`). Spec: `Docs/BilliardRogue/GDD-v2-Changes.md` §2, §3 (energy only),
§4 (pointer only). Resume: read this file + `git log --oneline -- Starter/Assets/Scripts/BilliardRogue/Input
Starter/Assets/Scripts/BilliardRogue/Configs/ControlConfig.cs Starter/Assets/Scripts/BilliardRogue/Editor/InputPrefabsBuilder.cs`.

## Plan / status

- [x] 1. Easier strike (§2): `StrikeDetector` start = cue speed toward the ball paw AND relative approach speed
      ≥ strikeSpeed, cue heading within `angleToleranceDeg` (60) of the ball paw; the approach axis is fixed at the
      start; land on swept contact (≤ contact) OR on crossing the line through the ball paw perpendicular to the axis
      with lateral offset ≤ `lineCrossMaxOffset` (12 in). Types moved to `Input/Core/StrikeTypes.cs` (class < 400).
      New defaults: strike 19 in/s, contact 9, rearm 0.25, arm 14, power ×3.5, full power 110.
      Tests: StrikeDetectorTests updated to v2 settings + new cases (slower thrust, 8 in stop, line crossing, 50° off,
      70° off never starts, tangential aim sweep / arm raise / arm drop / far wave never fire).
- [x] 2. `Input/Core/MotionEnergyFilter` (pure) + `Input/MotionEnergyMeter : IMotionEnergy` (raw pose per camera
      frame: wrists, elbows, shoulders, hips, knees, nose; aspect-normalized / engine.RawPpi = inches; per-node
      deadzone, clamp, mean → /range → 0..1; attack 0.1 s / release 0.5 s on unscaled time). Tests.
- [x] 3. `Input/PawPointer : IPawPointer` (smoothed Chest/LeftHand/RightHand, chest-relative inches → centre +
      half range → 0..1, OneEuro; tracking from raw frames on realtime like PawShotInput).
- [x] 4. Debug energy (hold H / mouse speed) + mouse pointer (both paws = mouse ± offset) in `DebugShotInput`; bot
      energy (Perlin) in `AutoAimBot`; `ShotInputRouter` routes via `RoutedBodyInput` (energy = max(body, active
      source's simulated), pointer = body, else the mouse for Debug).
- [x] 5. `ControlConfig` new fields; `Editor/ControlConfigV2Upgrade` (SerializedObject) applied to the asset.
- [x] 6. `InputPrefabsBuilder` adds MotionEnergyMeter + PawPointer and wires them; prefab rebuilt.
- [x] 7a. compile_check green; Editor recompile clean; EditMode `Nex.BilliardRogue.InputCore.Tests` 45/45
      (StrikeDetector 29, MotionEnergyFilter 9, PawPointerMath 3, AimHistory 4). ControlConfigV2Upgrade ran
      (35→19, 5→9, arm 10→14, rearm 0.35→0.25, power ×2→×3.5, full 120→110); PlayerShotInput.prefab rebuilt
      (meter + pointer wired).
- [x] 7b. Play mode (scratchpad `v2input/verify.sh` + `V2InputVerify.cs`; gameplay itself hit another agent's
      in-progress GameplayHud NRE, so the check ran at the calibration test strike, where the routers exist):
      Debug source → MotionEnergy / PawPointer non-null, v2 strike settings live (19 / 9 / 60° / 12 / 14 / 0.25);
      debug energy from a synthetic mouse wiggle 0.15 → 0.86 in 0.2 s, 0.03 after 1.5 s release; Bot source →
      energy wandered 0.05..1.00 over 12 s (all tiers, some time under 0.2); meter untracked / 0 and body pointer
      untracked without camera frames; paws "none" with the mouse outside the Game view. Console: only the Editor's
      camera timeout / socket errors. EditMode 45/45 after the readout flag.

## API

```csharp
ShotInputRouter.MotionEnergy : IMotionEnergy   // stable RoutedBodyInput; cache it
ShotInputRouter.PawPointer   : IPawPointer     // same object
ShotInputRouter.BodyMotion   : MotionEnergyMeter?  // readout: Energy01, IsTracked, Intensity01, MeanExcessSpeed, DetectedNodes
// Routing: Paw → body meter / body pointer; Debug → max(body, H key or mouse speed) / body pointer, else the mouse
// (both paws = mouse ± debugPawOffset01, only inside the Game view); Bot → max(body, Perlin 0.05..1) / body pointer.
MotionEnergyMeter.Initialize(int playerIndex, OnePlayerDetectionEngine engine, ControlConfig config)
PawPointer.Initialize(int playerIndex, OnePlayerDetectionEngine engine, ControlConfig config)
// Both are initialized by ShotInputRouter.Initialize(playerIndex, engine, ctx): no coordinator change needed.
MotionEnergyFilter(int nodeCount, MotionEnergySettings) .AddSample/.MarkNoBody/.Tick/.Smooth   (Input/Core)
PawPointerMath.ToScreen01(Vector2 handInches, Vector2 center, Vector2 halfRange)               (Input/Core)
StrikeSettings.angleToleranceDeg / .lineCrossMaxOffset; StrikeResult.byLineCross; StrikeReadout.lastStrikeByLineCross
ControlConfig: StrikeAngleToleranceDeg, LineCrossMaxOffsetInches, MotionEnergySettings, MotionAttack/ReleaseSeconds,
  PawPointerCenterInches, PawPointerHalfRangeInches, PawPointerMinCutoff/Beta, DebugMotionFullScreensPerSec,
  DebugPawOffset01, BotMotionFrequencyHz/Min/Max
Editor: ControlConfigV2Upgrade.Run() (menu "Nex/Billiard Rogue/Upgrade Control Config (v2)"), idempotent
```

## Requests (for the integrator)

1. Hype (gameplay owner): cache `router.MotionEnergy` per player; Hype source = max over players of `Energy01`
   (co-op, GDD v2 §3) while ≥ 1 ball is in flight, else 0. Energy already has the 0.1 s attack / 0.5 s release.
2. Reward pick (GameplayView / RewardView owner): `rewardView.SetPawPointer(routers[chooser].PawPointer, chooser,
   numPlayers)`. In the Editor (Debug source) both paws follow the mouse while it is inside the Game view; with the
   bot and no body the pointer reports no paws, so automated runs keep using `DebugHooks.ChooseReward`.
3. Control readout (`UI/Debug/ControlReadoutPanel` owner): add a Hype line, e.g.
   `ENERGY {router.MotionEnergy.Energy01:0.00}  BODY {meter.Energy01:0.00} {meter.DetectedNodes}/11 {meter.MeanExcessSpeed:0} in/s`
   (meter = `router.BodyMotion`), and show `(line)` after STRIKE when `StrikeReadout.lastStrikeByLineCross`.
   The contact tuning row now also scales the line-cross offset.
4. `Docs/BilliardRogue/ControlDemo.md` (doc owner): Editor keys — hold **H** (or move the mouse fast) to simulate
   Hype; strikes are easier (19 in/s, 9 in contact, passing beside the left paw counts).
