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
- [ ] 7b. Play-mode check with the debug source (H / mouse energy, mouse paws, bot energy).

## Requests (for the integrator)

- (pending)
