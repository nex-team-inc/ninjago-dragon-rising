# Input module — progress checkpoint

Owner: Input agent (TDD §6, §0a D6/D7). Resume from here after a usage-limit kill:
read this file + `git log -- Starter/Assets/Scripts/BilliardRogue/Input Starter/Assets/Scripts/BilliardRogue/Configs/ControlConfig.cs Starter/Assets/Scripts/BilliardRogue/Editor/DetectionPrefabsBuilder.cs Starter/Assets/Scripts/BilliardRogue/Editor/InputPrefabsBuilder.cs Starter/Assets/Prefabs/BilliardRogue/Detection Starter/Assets/Prefabs/BilliardRogue/Input`.

## Status

- [x] `Input/Core` asmdef `Nex.BilliardRogue.InputCore` (pure C#): `StrikeDetector` (+ `StrikeSettings`, `StrikeResult`, `StrikeState`), `AimHistory`
- [x] `Input/Core/Tests` asmdef `Nex.BilliardRogue.InputCore.Tests` (EditMode NUnit): StrikeDetectorTests (12), AimHistoryTests (4)
- [x] `ControlConfig` extended (launch filter, strike travel/timeouts/expiry, tracking hysteresis, debug, bot search + scoring)
- [x] Runtime: `PawShotInput`, `DebugShotInput`, `AutoAimBot` + `BotShotPlanner`, `ShotInputRouter`, `ShotInputContext`, `ShotInputSource`, `ShotInputDebugCommands` (DebugHooks.SetBot)
- [x] Editor: `DetectionPrefabsBuilder`, `InputPrefabsBuilder`; both ran in the Editor, prefabs + metas committed (8a2ca557)
- [x] Paw tracking judged from the raw camera pose (chest + elbows + wrists detected), not from node auto-hide
      (engine auto-hide runs on `Time.fixedTime` and freezes while gameplay is paused → tracking could never
      drop/return during the tracking-lost pause); timestamps on `Time.realtimeSinceStartup`
- [x] `ShotInputContext.forceDebugInput` provider (debug builds: mouse/keyboard even while tracked)
- [x] Style pass: braces on every non-guard / multi-condition `if` (code-editing.mdc)
- [x] Verified: compile_check green; Editor recompile clean; EditMode `Nex.BilliardRogue.InputCore.Tests` 16/16 passed;
      no scene left dirty
- [ ] Integration (not mine): coordinator wiring (Requests below), playtest in play mode

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
- Tracking (paw): raw pose has chest + both elbows/wrists in the newest camera frame, a smoothed pose within
  trackingDropSeconds AND a camera frame within staleFrameSeconds, acquired after trackingAcquireSeconds; Update
  never touches the engine (CameraSession may destroy it first). Regaining tracking resets the strike.
- Router: Bot when DebugSettings.autoAimBot (debug builds, read live); Debug when ctx.forceDebugInput() (debug
  builds); Editor only: Debug when the paw is not tracked; else Paw. Inactive debug/bot components are disabled (no
  bot search cost when unused). TrackingLost / TrackingRestored after ControlConfig.trackingLostSeconds (gameplay
  keeps its own timer for the overlay).
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
ShotInputRouter : IShotInput; ActiveSource; IsTrackingLost; PlayerIndex; event Action<int> TrackingLost, TrackingRestored
ShotInputContext { ControlConfig control; GameRules rules; Func<bool> leftHanded; Func<bool> forceDebugInput;
                   Func<RunState?> run; Camera? worldCamera; ArenaLayout? layout; }
PawShotInput.Initialize(int playerIndex, OnePlayerDetectionEngine engine, ControlConfig config, ArenaRules arena, bool leftHanded); SetLeftHanded(bool); StrikeState
DebugShotInput.Initialize(int playerIndex, ControlConfig config, ArenaRules arena, Camera? worldCamera, ArenaLayout? layout)
AutoAimBot.Initialize(int playerIndex, GameRules rules, Func<RunState?> run, ControlConfig config)
```

## Requests (for the integrator)

1. `Flow/BilliardRogueCoordinator.Inputs.cs`: `CreateShotInput` → `instance.GetComponent<ShotInputRouter>()` +
   `router.Initialize(playerIndex, engine, shotInputContext)`; one shared `ShotInputContext { control = config.Control,
   rules = <cached RulesFactory.Build(config)>, leftHanded = () => PlayerDataManager.Instance.PlayerPreference.leftHandedCue,
   forceDebugInput = () => PlayerDataManager.Instance.DebugSettings.forceDebugInput, run = () => runFlow.ActiveGameplay != null
   ? runFlow.ActiveGameplay.Run : null, worldCamera = worldDisplay.WorldCamera, layout = arenaLayout }`.
2. `PlayerData/DebugSettings.cs`: `[DebugOrder(23), Description("Input: Force Mouse/Keyboard")] public bool forceDebugInput;`
