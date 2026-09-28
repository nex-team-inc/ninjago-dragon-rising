# Input module — progress checkpoint

Owner: Input agent (TDD §6, §0a D6/D7). Resume from here after a usage-limit kill:
read this file + `git log -- Starter/Assets/Scripts/BilliardRogue/Input Starter/Assets/Scripts/BilliardRogue/Configs/ControlConfig.cs Starter/Assets/Scripts/BilliardRogue/Editor/DetectionPrefabsBuilder.cs Starter/Assets/Scripts/BilliardRogue/Editor/InputPrefabsBuilder.cs Starter/Assets/Prefabs/BilliardRogue/Detection Starter/Assets/Prefabs/BilliardRogue/Input`.

## Status

- [x] `Input/Core` asmdef `Nex.BilliardRogue.InputCore` (pure C#): `StrikeDetector` (+ `StrikeSettings`, `StrikeResult`, `StrikeState`), `AimHistory`
- [x] `Input/Core/Tests` asmdef `Nex.BilliardRogue.InputCore.Tests` (EditMode NUnit): StrikeDetectorTests (12), AimHistoryTests (4)
- [x] `ControlConfig` extended (launch filter, strike travel/timeouts/expiry, tracking hysteresis, debug, bot search + scoring)
- [x] Runtime: `PawShotInput`, `DebugShotInput`, `AutoAimBot` + `BotShotPlanner`, `ShotInputRouter`, `ShotInputContext`, `ShotInputSource`, `ShotInputDebugCommands` (DebugHooks.SetBot)
- [x] Editor: `DetectionPrefabsBuilder`, `InputPrefabsBuilder` (compile_check green)
- [ ] Editor (inside lock): recompile, run EditMode tests, run both builders, commit generated prefabs + metas
- [ ] Final report with Requests

## Decisions

- Tests: StrikeDetector/AimHistory live in their own asmdef with no Assembly-CSharp deps, so the tests sit in a real
  test asmdef (rule: "NUnit tests live in a test assembly"), mirroring the Simulation pattern (D8). Namespace stays
  `Nex.BilliardRogue` (a `...Input` namespace would shadow `UnityEngine.Input` for every `Nex.BilliardRogue` file).
- Strike = min(cue-paw speed toward the ball paw, paw-distance shrink rate) ≥ threshold, contact on the swept
  relative segment (a 30 Hz jump past the ball paw still counts), min travel 4 in, stall (< ½ threshold) or 0.4 s
  timeout drops the approach, re-arm needs rearmSeconds AND paws apart ≥ armDistance. Power = InverseLerp(threshold,
  fullPower, peak), power shot at peak ≥ 2× threshold.
- Strike aim = AimHistory (smoothed aim stamped with camera frameTime) at strikeStart − aimSampleDelaySeconds; the
  displayed aim is held at that value while an approach is in progress.
- Pending strikes expire after strikeExpirySeconds (0.3 s) so strikes made during pause / enemy phase / the other
  player's turn never fire later. The bot's strike does not expire (it waits to be consumed).
- Tracking (paw): chest + both paws valid within trackingDropSeconds AND a camera frame within staleFrameSeconds,
  acquired after trackingAcquireSeconds; Update never touches the engine (CameraSession may destroy it first).
  Regaining tracking resets the strike (paws must separate again).
- Router: Bot when DebugSettings.autoAimBot (debug builds, read live); Editor only: Debug when the paw is not
  tracked; else Paw. Inactive debug/bot components are disabled (no bot search cost when unused). TrackingLost /
  TrackingRestored events after ControlConfig.trackingLostSeconds (gameplay keeps its own timer for the overlay).
- AutoAimBot owns a private read-only BallSimulator (PredictPath does not mutate) instead of the session's, because
  inputs are created by the coordinator before the GameSession exists; it reads the run through `Func<RunState?>`.
- DebugHooks.Shoot is already registered by Gameplay (`PlayerTurnLoop.ForceShoot`), so Input does not override it;
  Input registers `DebugHooks.SetBotHandler` at startup (session-only toggle of `DebugSettings.autoAimBot`).
- No player-facing strings in Input (tracking hints live in UI/Flow keys), so no `LocKeys.Input.cs`.

## API

```csharp
// Root of PlayerShotInput.prefab; one per player.
ShotInputRouter.Initialize(int playerIndex, OnePlayerDetectionEngine engine, ShotInputContext ctx)
ShotInputRouter.Initialize(int playerIndex, IShotInput paw, IShotInput debug, IShotInput bot, ControlConfig config)
ShotInputRouter : IShotInput; ActiveSource; IsTrackingLost; PlayerIndex; event Action<int> TrackingLost, TrackingRestored; SetLeftHanded(bool)
ShotInputContext { ControlConfig control; GameRules rules; bool leftHanded; Func<RunState?> run; Camera? worldCamera; ArenaLayout? layout; }
PawShotInput.Initialize(int playerIndex, OnePlayerDetectionEngine engine, ControlConfig config, ArenaRules arena, bool leftHanded); SetLeftHanded(bool); StrikeState
DebugShotInput.Initialize(int playerIndex, ControlConfig config, ArenaRules arena, Camera? worldCamera, ArenaLayout? layout)
AutoAimBot.Initialize(int playerIndex, GameRules rules, Func<RunState?> run, ControlConfig config)
```

## Requests (for the integrator)

See the final report of the Input agent (coordinator Inputs.cs replacement, DebugSettings flag).
