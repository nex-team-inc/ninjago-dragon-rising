# Billiard Rogue — Technical Design Document (contracts for all build agents)

Read with: `GDD.md` (rules/numbers), `research/*.md` (verified starter/engine APIs), `.cursor/rules/**` (project rules — binding).
This file is the **single source of truth for names, folders, public APIs and ownership**. If you must deviate, keep the public surface compatible and note it in your final report.

---

## 0a. Binding decisions from research (Docs/BilliardRogue/research/*.md — read the note for your area)

| # | Decision |
|---|---|
| D1 | **One scene** (`Main.unity`), every step a View (scene loads destroy the ViewManager). Only exception: if the detection player count must change and re-instantiating `DetectionManager` proves unreliable, reload `Main.unity` and resume via `PlayerDataManager.appViewState` (pending flow: Calibration + numPlayers + isContinue). `CameraSession` first tries destroy + re-instantiate of the DetectionManager prefab. |
| D2 | Rendering rig = `research/urp-hd2d-rendering.md` §3.2: `WorldCamera` (Base, perspective, renders **only World layers** into a 640×360 point-filtered RT, post-processing ON, HDR on, own Volume layer, TiltShift RG feature on its renderer) — **never in `CameraChainItem.baseCameras`**. The view manager's `RootCamera` is the only screen camera: Base, straight into the backbuffer (clear black, HDR / MSAA / post / shadows off, World layers culled); no Main Camera and no camera stack (a stack renders the 1080p UI through an intermediate plus a final blit, see progress/final-perf.md pass 3). RT shown by a full-stretch RawImage on its own Screen-Space-Camera canvas on `RootCamera` at planeDistance 295. Layers `World`, `WorldVolume` created by `RenderPipelineBuilder` via SerializedObject on TagManager. Forward path, 4 per-object lights, 1 cascade, hard shadows, 1024 map. Render Graph only (`RecordRenderGraph`), Forward+ keyword is `_CLUSTER_LIGHT_LOOP`. |
| D3 | No background blur (team.nex.dual-blur is a no-op under RG). Overlays (Pause/Reward/TrackingLost) use a dim full-screen image. `RequiresAdditionalBackgroundBlur` stays false so the Gameplay HUD doesn't run its background animation. |
| D4 | Gameplay owns pause & time: `TimeScaleController` is the only writer of `Time.timeScale`. All overlay/UI animations and BGM fades use **unscaled** time (DOTween `SetUpdate(true)`, MMF unscaled mode). |
| D5 | PiP camera preview during gameplay = `AreaPreviewFrame` + `PlayerIndicatorsManager` on a **Screen Space – Overlay** canvas in a corner (keeps camera feed out of bloom/pixelation). Active player highlight via new `PlayerIndicatorsManager.SetActivePlayer(int)` + `PreviewFramePlayerIndicator.SetHighlighted(bool)` (Foundation adds them). |
| D6 | Detection input: one `OnePlayerDetectionEngine` per player built from a **hidden-node prefab variant** (no debug circles). Paw coordinates in inches: `(node − Chest) / DistancePerInch`. Smoothed stream (every frame) for launch X and aim; raw nodes sampled when `original.frameTime` changes (~30 Hz) for strike speed. Avoid engine Wrist/Ear/Ankle properties (throw). Use `using Jazz;` inside `namespace Nex.*` (name clashes). |
| D7 | Editor/CLI driving: keyboard nav reads legacy `Input` and `simulate_key` is unavailable, so a runtime static `Nex.BilliardRogue.DebugHooks` (guarded `ENABLE_DEBUG_SETTINGS \|\| DEVELOPMENT_BUILD \|\| UNITY_EDITOR`) exposes flow/gameplay commands for `unity command eval`: `StartNewRun(players, seed)`, `ContinueRun()`, `SkipCalibration()`, `SetBot(bool)`, `ChooseReward(i)`, `Shoot(angleDeg)`, `GotoStage(n)`, `KillAll()`, `State()` (returns a summary string). |
| D8 | Tests: simulation in its own asmdef + EditMode test asmdef (rule-compliant). Views/gameplay stay in Assembly-CSharp (`View.Manager`/`IsActive` are internal). ES3 must list assembly `Nex.BilliardRogue.Simulation` in `ES3Defaults.assemblyNames` (Foundation verifies with an Editor round-trip; add it via SerializedObject if missing). |
| D9 | Import settings: **no global AssetPostprocessor** (adding one reimports the whole project). `ImportSettingsBuilder` sets importer settings explicitly for files under our BilliardRogue folders only (models: bakeAxisConversion false, materialImportMode None, preserveHierarchy true, normals Calculate angle 0 or Import; textures: point, no mips, palette uncompressed; sprites: PPU, point; audio: SFX ADPCM mono decompress-on-load, BGM Vorbis 0.6 streaming). |
| D10 | Starter fixes owned by Foundation: VolumeManager subscribes to PlayerDataManager volume properties and saves back; `SetupStateManager.ClearTrackers` clears `playerStates`; SetupStateManager S-key gated to debug builds via `Nex.Dev.DebugInput`; `SetupWarningMessage` uses localized strings; `SfxManager` gains pitched/volume playback through a small AudioSource pool; `BgmManager` gains crossfade (optional second source), unscaled fades, stingers; `VfxManager.PlayVisualEffect` returns the ParticleSystem and accepts rotation/scale. |
| D11 | EnumDictionary assets must contain **every** enum key in ascending order (builders write all keys; VFX max pool > 0). Enum values are persisted as ints — append only, never reorder. |
| D12 | Fonts: original Latin pixel TTF (fontTools from bitmap glyphs) + CJK static 1-bit RASTER_HINTED TMP atlas from `GlowSansJ-Normal-Bold.otf` at 16 px; one fallback chain for all locales; font sizes multiples of 16 (16/32/48/64); font assets **Static** (DynamicFontSanitizer wipes dynamic `*SDF.asset`). |
| D13 | VFX are built from scratch (Epic Toon FX is not URP-ready and unsafe to pool): ParticleSystems with `BilliardRogue/LitParticle` + pixel flipbooks at 12 fps, collision module in Planes mode (floor) for bounce, no AudioSources/Lights inside VFX prefabs. |
| D14 | Audio from the team repo `~/Documents/music-cell-shared-assets`, materialized per file (`git show HEAD:<path> \| git lfs smudge`), never copying its `.meta`; picks in `research/audio-vfx-analytics.md`; gaps synthesized (`Tools/Audio`). |
| D15 | Builders live in `Assets/Scripts/BilliardRogue/Editor/` (compiled, menu items `Nex/Billiard Rogue/...`) and are run through the CLI with `unity command eval 'Nex.BilliardRogue.Editor.X.Run();'` or `run_script`. Always `--project-path /Users/simonbut/project/VibeProject3/Starter`. Never pass `save_path` to capture commands. |

## 0. Hard rules (from project rules + user requirements)

1. Never hand-write/hand-edit `.meta`, `.unity`, `.prefab`, `.asset`. All Unity assets are produced by **Editor C# builder scripts** run through the Unity CLI (`unity command eval` / `eval_file`, always with `--project-path /Users/simonbut/project/VibeProject3/Starter`). `.asmdef`, `.cs`, `.shader`, `.hlsl`, `.py`, `.md`, `.json` text files are fine to write.
2. Reuse starter infrastructure (see `research/*` + `.cursor/rules/unity/starter-infrastructure.mdc`): `SingletonSpawner` bootstrap, `ViewManager` views for **every** flow step, `DetectionManager` + `PreviewsManager` + `SetupStateManager` for setup, PiP preview + `PlayerIndicatorsManager` during gameplay, `PlayerDataManager` (Easy Save) for all persistence, `DebugSettings`/`DebugSettingsView` for dev tuning, Unity Localization + `NexLocalizedString` for all text, `AnalyticsManager` for every UI action/result/turn, `VfxManager`/`SfxManager`/`BgmManager`, ScriptableObject configs with `EnumDictionary`, keyboard navigation on every view, `SecretCodeSequenceDetector` = Up Up Down Down Left Right Left Right.
3. C# style per `.cursor/rules/unity/code-editing.mdc`: `#nullable enable`, `= null!` for required serialized refs, no defensive null checks on required refs, camelCase private fields without `private`, `var`, `#region` in classes ≥100 lines, class < 400 lines, **no LINQ in runtime code**, no `GameObject.Find`, UniTask not coroutines, `destroyCancellationToken`, `Initialize(...)` dependency injection from the parent, `[Header]`/`[Tooltip]`/`[Range]` on serialized fields.
4. Performance: 60 fps on low-end Android (Mali-G52 class). Zero per-frame GC in gameplay (pool everything, reuse lists, no string concat per frame, no LINQ, no closures in hot paths).
5. Verify your C# with `python3 /Users/simonbut/project/VibeProject3/Tools/compile_check.py` (compiles Assembly-CSharp + Editor with Unity's Roslyn, ~2 s, does not touch the Editor). Do **not** trigger Editor recompiles/refreshes unless your task says you own the Editor.
6. Commit only when your task says so; commit messages explain intent, end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

---

## 1. Folder & namespace layout

| What | Path | Namespace |
|---|---|---|
| Pure simulation (rules, state, physics) — own asmdef | `Starter/Assets/Scripts/BilliardRogue/Simulation/` (`Nex.BilliardRogue.Simulation.asmdef`) | `Nex.BilliardRogue.Simulation` |
| Simulation EditMode tests | `Starter/Assets/Scripts/BilliardRogue/Simulation/Tests/` (`Nex.BilliardRogue.Simulation.Tests.asmdef`, editor-only, test assembly) | `Nex.BilliardRogue.Simulation.Tests` |
| Configs (ScriptableObject classes) | `Starter/Assets/Scripts/BilliardRogue/Configs/` | `Nex.BilliardRogue` |
| Gameplay orchestration (turns, session) | `Starter/Assets/Scripts/BilliardRogue/Gameplay/` | `Nex.BilliardRogue` |
| Input (paws, debug, bot) | `Starter/Assets/Scripts/BilliardRogue/Input/` | `Nex.BilliardRogue` |
| Presentation (3D views, juice) | `Starter/Assets/Scripts/BilliardRogue/Presentation/` | `Nex.BilliardRogue` |
| UI views + widgets | `Starter/Assets/Scripts/BilliardRogue/UI/Views/`, `.../UI/Widgets/` | `Nex.BilliardRogue` |
| Flow (initializer, coordinator, camera session) | `Starter/Assets/Scripts/BilliardRogue/Flow/` | `Nex.BilliardRogue` |
| Persistence glue | `Starter/Assets/Scripts/BilliardRogue/Persistence/` + edits to `Assets/Scripts/PlayerData/*` | `Nex.BilliardRogue` / `Nex` |
| Analytics glue | `Starter/Assets/Scripts/BilliardRogue/Analytics/` | `Nex.BilliardRogue` |
| Rendering runtime (pixel camera, renderer features) | `Starter/Assets/Scripts/BilliardRogue/Rendering/` | `Nex.BilliardRogue` |
| Localization keys | `Starter/Assets/Scripts/BilliardRogue/Localization/LocKeys.cs` | `Nex.BilliardRogue` |
| Editor builders/tools | `Starter/Assets/Scripts/BilliardRogue/Editor/` | `Nex.BilliardRogue.Editor` |
| Shaders | `Starter/Assets/Shaders/BilliardRogue/` | shader names `BilliardRogue/...` |
| Config assets | `Starter/Assets/Configs/BilliardRogue/` | |
| Prefabs | `Starter/Assets/Prefabs/BilliardRogue/{Views,Board,Enemies,Balls,Environment,Player,Vfx,UI}/` | |
| Models (FBX from Blender) | `Starter/Assets/Models/BilliardRogue/{Enemies,Bosses,Environment,Player,Props,Balls}/` | |
| Materials | `Starter/Assets/Materials/BilliardRogue/` | |
| Textures | `Starter/Assets/Textures/BilliardRogue/` | |
| Sprites (icons, UI frames, particles) | `Starter/Assets/Sprites/BilliardRogue/{Icons,UI,Particles}/` | |
| Fonts | `Starter/Assets/Fonts/BilliardRogue/` | |
| Audio | `Starter/Assets/Audio/Sfx/BilliardRogue/`, `Starter/Assets/Audio/Bgm/BilliardRogue/` | |
| Scene | `Starter/Assets/Scenes/BilliardRogue/Main.unity` (build index 0) | |
| Offline generators | `Tools/Blender/*.py`, `Tools/Textures/*.py`, `Tools/Audio/*.py`, `Tools/Fonts/*.py` (python: `Tools/.venv/bin/python`) | |

---

## 2. Architecture overview

```
Main.unity
 ├─ SingletonSpawner (StartUp + Common singletons)  ──activates──►  BilliardRogueInitializer
 ├─ BilliardRogueInitializer  → loads/Initializes BilliardRogueCoordinator → StartMain()
 ├─ BilliardRogueCoordinator (Flow)  owns ViewManager flow + CameraSession + GameSessionFactory
 ├─ MainViewManager (starter prefab instance, with SecretCodeSequenceDetector)
 ├─ World
 │   ├─ WorldCameraRig (PixelWorldCamera → low-res RT, TiltShift renderer)
 │   ├─ Lighting (sun + act lights)            ◄── ActEnvironmentController
 │   ├─ Environment (act diorama prefabs)
 │   └─ Arena (BoardPresenter root, cats, walls)
 └─ WorldDisplay (full-screen point-filtered RT display behind all UI)

Gameplay (inside GameplayView lifetime):
  GameSession ── TurnController ──► Simulation.RunState / BallSimulator / EnemyPhaseResolver / StageGenerator / RewardGenerator
      │                │ drains SimEvent list every frame
      │                ▼
      │         BoardPresenter (EnemyView, BallView, FieldObjectView, PickupView, CatView, AimGuide, DamageNumbers, HpLabels, Juice)
      ├── ShotInputRouter[player] ──► PawAimInput (DetectionManager engines) | DebugAimInput | AutoAimBot
      ├── Hud (GameplayView widgets)
      └── RunPersistence (PlayerDataManager) + RunAnalytics
```

Simulation is **pure C#, deterministic, allocation-free per step, unit tested**. MonoBehaviours only orchestrate and present.

---

## 3. Simulation (`Nex.BilliardRogue.Simulation`, asmdef, references nothing but UnityEngine core types: `Vector2`, `Mathf`, `[Serializable]`)

### 3.1 Enums (`SimEnums.cs`)
```csharp
public enum BallType { Basic, Flame, Frost, Thunder, Bomb, Splitter, Piercer, Iron, Venom, Vampire, Rubber, Lucky }
public enum BallRarity { Common, Uncommon, Rare }
public enum EnemyType { Slime, Bat, Skeleton, ShieldKnight, Mage, Healer, Bomber, Totem, KingSlime, BoneLich, CrystalGolem, BoneWall }
public enum FieldObjectType { Pillar, Crate, Portal, Mud }
public enum PickupType { ExtraBall, Heal, Power }
public enum StatusType { Burn, Poison, Freeze }
public enum Face { None, Bottom, Top, Left, Right }       // shield faces
public enum RewardKind { NewBall, UpgradeBall, Heal, MaxHp }
public enum RunOutcome { None, Victory, Defeat, Abandoned }
public enum SimEventKind { /* see 3.5 */ }
```
`BoneWall` is a boss-summoned destructible obstacle modelled as an enemy with moveRows 0, attack 0.

### 3.2 Rules data (plain `[Serializable]` classes, filled from ScriptableObjects by `RulesFactory` in Assembly-CSharp)
```csharp
[Serializable] public class BallLevelStats { public int damage = 1; public float speedMultiplier = 1f; public int maxBounces = 40;
  public int statusStacks; public float procChance; public int chainCount; public float chainRange = 2.2f; public int chainDamage = 1;
  public int areaDamage; public int areaRadius; public bool areaCross; public int splitCount; public int splitDamage = 1;
  public int pierceCount; public int healPerHit; public int healCapPerShot; public int bonusPerWallBounce; public int bonusCap;
  public float critChance; public int critMultiplier = 3; public int frozenBonusDamage; public bool spreadBurnOnDeath; }
[Serializable] public class BallRules { public BallType type; public BallRarity rarity; public BallLevelStats[] levels = new BallLevelStats[3]; public int unlockTier; }
[Serializable] public class EnemyRules { public EnemyType type; public int hp; public int attack; public int moveRows = 1; public int moveEveryNTurns = 1;
  public int width = 1; public int height = 1; public bool isBoss; public bool ranged; public int abilityEveryNTurns; public int abilityValue;
  public Face shieldFace = Face.None; public bool rotatingShield; public int deathExplosionDamage; public int healAmount; public int spawnCount; public EnemyType spawnType; }
[Serializable] public class ArenaRules { public int columns = 7; public int rows = 10; public float launchZoneHeight = 1.6f; public float launchY = 0.55f;
  public float ballRadius = 0.2f; public float ballSpeed = 13f; public float enemyInset = 0.08f; public float bossInset = 0.1f;
  public float minAimAngleDeg = 12f; public float maxFlightSeconds = 7f; public int maxIdleWallBounces = 10; public float antiStallAccel = 6f; public int substepsPerSecond = 240; }
[Serializable] public class BalanceRules { public int playerMaxHp = 30; public int bagCap = 12; public BallType[] startingBag; public int levelCap = 3;
  public float hpScalePerStage = 0.16f; public int attackBonusPerAct = 1; public int healRewardAmount = 12; public int maxHpRewardAmount = 5;
  public float bossHealFraction = 0.5f; public int pickupHealAmount = 4; public float pickupChancePerRow = 0.2f; public int poisonMax = 5; }
[Serializable] public class WaveEntryWeight { public EnemyType type; public float weight; public int cost; }
[Serializable] public class ActRules { public int actIndex; public int normalStages = 3; public int minWaves = 5; public int maxWaves = 8;
  public WaveEntryWeight[] enemyPool; public EnemyType bossType; public int baseBudgetPerRow = 3; public int budgetGrowthPerStage = 1;
  public int maxFieldObjects = 3; public float[] rarityWeights = { 0.6f, 0.3f, 0.1f }; }
public sealed class GameRules { public ArenaRules arena; public BalanceRules balance; public BallRules[] balls /*index = (int)BallType*/; public EnemyRules[] enemies /*index=(int)EnemyType*/; public ActRules[] acts; }
```
Additive fields since (defaults in the class; see HANDOFF.md §3–§4): `ArenaRules.maxStallSeconds = 1f` (ghost drop after the pull, bounding a flight to `maxFlightSeconds + maxStallSeconds + TopWallY / ballSpeed`), `BalanceRules.offerBasicBall = false`, `ActRules.minOpenColumnsPerRow = 1`, `EnemyRules.spawnEveryNTurns / spawnCountBelowHalf / halfHpSummonType / halfHpSummonCount`, `EnemyState.halfHpSummonPending`, `StatusStacks.burnSpreads`.

### 3.3 State (all `[Serializable]`, public fields, ES3-friendly; lists not arrays where they grow)
```csharp
[Serializable] public struct GridPos { public int col, row; }
[Serializable] public class StatusStacks { public int burn; public int poison; public int frozenTurns; }
[Serializable] public class EnemyState { public int id; public EnemyType type; public int col, row; public int width = 1, height = 1; public int hp, maxHp, attack;
  public StatusStacks status = new(); public int turnCounter; public Face shieldFace; public bool bossHalfTriggered; }
[Serializable] public class FieldObjectState { public int id; public FieldObjectType type; public int col, row; public int hp; public int pairId = -1; }
[Serializable] public class PickupState { public int id; public PickupType type; public int col, row; }
[Serializable] public class BallInstance { public BallType type; public int level = 1; }
[Serializable] public class WaveCell { public int col; public bool isPickup; public EnemyType enemy; public PickupType pickup; }
[Serializable] public class WaveRow { public List<WaveCell> cells = new(); }
[Serializable] public class StagePlan { public int actIndex, stageInAct; public bool isBoss; public List<WaveRow> waves = new(); public List<FieldObjectState> fieldObjects = new(); }
[Serializable] public class BoardState { public List<EnemyState> enemies = new(); public List<FieldObjectState> fieldObjects = new(); public List<PickupState> pickups = new(); public int nextId = 1; }
[Serializable] public class RunStats { public int turns, shots, hits, kills, damageDealt, damageTaken, bestCombo, bossesDefeated; public float playSeconds; }
[Serializable] public class RunState { public string runId; public int seed; public ulong rngState; public int numPlayers = 1; public int actIndex; public int stageInAct; public int stageNumber /*0..11*/;
  public int turnInStage; public int playerHp, playerMaxHp; public List<BallInstance> bag = new(); public BoardState board = new(); public StagePlan stage = new(); public int nextWaveIndex;
  public int activePlayerIndex; public RunStats stats = new(); public RunOutcome outcome; public bool awaitingReward; public List<RewardOption> pendingRewards = new(); }
[Serializable] public class RewardOption { public RewardKind kind; public BallType ballType; public int bagIndex = -1; public int amount; }
[Serializable] public class MetaProgressData { public int runsStarted, runsWon, bestStageNumber = -1; public int highestUnlockTier; public int totalKills; public bool tutorialSeen; }
```
`RunState` is the save file for a run (saved via PlayerDataManager). Saves only happen at turn boundaries (no in-flight balls).

### 3.4 Services (public API — implementations inside the asmdef)
```csharp
public sealed class SimRandom { public SimRandom(ulong state); public ulong State { get; } public int Range(int minInclusive, int maxExclusive); public float Value01(); public static ulong SeedToState(int seed); }
public static class ArenaGeometry {   // single owner of sim-space math. Sim space: x right [0,cols], y up; launch line y=0 is the bottom exit; row r cell spans y∈[launchZoneHeight+rows-1-r, launchZoneHeight+rows-r]
  public static Rect CellRect(ArenaRules a, int col, int row); public static Rect FootprintRect(ArenaRules a, int col, int row, int w, int h, float inset);
  public static float TopWallY(ArenaRules a); public static Vector2 CellCenter(ArenaRules a, int col, int row); public static int DangerRow(ArenaRules a) /* rows-1 */; }
public sealed class StageGenerator { public StagePlan Generate(GameRules rules, int actIndex, int stageInAct, int stageNumber, SimRandom rng); }
public sealed class RewardGenerator { public void Roll(GameRules rules, RunState run, int highestUnlockTier, SimRandom rng, List<RewardOption> output /*3*/); public void Apply(GameRules rules, RunState run, RewardOption option); }
public sealed class BoardOps { // spawning, occupancy, damage & status application shared by BallSimulator and EnemyPhaseResolver
  public BoardOps(GameRules rules); public bool IsCellFree(BoardState b, int col, int row); public EnemyState SpawnEnemy(RunState run, EnemyType t, int col, int row, List<SimEvent> events);
  public int DamageEnemy(RunState run, EnemyState e, int amount, DamageSource src, List<SimEvent> events); /* returns dealt; handles poison bonus, death, bomber explosion, burn spread */ }
public sealed class BallSimulator {
  public BallSimulator(GameRules rules, BoardOps ops);
  public int ActiveCount { get; }
  public void Launch(RunState run, BallInstance ball, Vector2 origin, Vector2 direction, bool powerShot, int shooterIndex, List<SimEvent> events); // origin on launch line
  public void Step(RunState run, float dt, List<SimEvent> events);   // fixed substeps internally; anti-stall; abilities; pickups; portals; mud
  public void ForEachBall(BallVisitor visitor);                       // for presentation: id, type, level, position, velocity, isMini
  public int PredictPath(RunState run, Vector2 origin, Vector2 dir, float maxLength, int maxBounces, Vector2[] pointsOut); // aim guide (walls+enemies+objects)
  public void Clear(); }
public delegate void BallVisitor(in BallSnapshot ball);   // readonly struct BallSnapshot { int id; BallType type; int level; Vector2 position; Vector2 velocity; bool isMini; float radius; } — not "BallView", which is the Presentation MonoBehaviour (§8)
public sealed class EnemyPhaseResolver { public EnemyPhaseResolver(GameRules rules, BoardOps ops); public void Resolve(RunState run, SimRandom rng, List<SimEvent> events); } // status ticks → abilities → advance → danger attacks → spawn next wave; events carry `step` (0..4) for staged animation
public sealed class RunFactory { public RunState NewRun(GameRules rules, int seed, int numPlayers); public void BeginStage(GameRules rules, RunState run, SimRandom rng, List<SimEvent> events); public bool IsStageCleared(RunState run); public bool AdvanceToNextStage(GameRules rules, RunState run) /* false when run complete = victory */; }
```
`DamageSource` struct: `{ BallType ballType; int ballId; bool isStatusTick; bool isExplosion; bool isChain; bool isCrit; }`.
**Implemented (2026-09-28, `109f1fca`).** `BoardOps` also exposes `IsFootprintFree`, `EnemyAt`, `SpawnWaveRow`, `ApplyStatus`, `HealEnemy`, `DamageCrate`, `CollectPickup`, `DamagePlayer`, `HealPlayer`; `RunFactory` adds `CompleteStage`; `StatusStacks.burnSpreads` added. Binding rule decisions (footprints, cadence, freeze, escorts, pickups, splitter/bomb/piercer semantics) are in `HANDOFF.md` §3–§4.

### 3.5 Events (`SimEvent` struct, appended to a caller-owned `List<SimEvent>`; no allocations)
```csharp
public struct SimEvent { public SimEventKind kind; public int step; public int ballId; public int targetId; public int sourceId; public int value; public int value2;
  public BallType ballType; public EnemyType enemyType; public StatusType status; public PickupType pickup; public Vector2 position; public Vector2 position2; public bool flag; }
public enum SimEventKind { BallLaunched, BallWallBounce, BallExited, BallSplit, BallTeleported, BallSlowed,
  EnemyHit /*value=dmg, flag=crit*/, EnemyBlocked, EnemyKilled, EnemyHealed, EnemySpawned, EnemyMoved /*position=from, position2=to*/, EnemyAttack /*value=dmg, flag=ranged*/, EnemyAbilityTelegraph, EnemyShieldRotated,
  StatusApplied /*status,value=stacks*/, StatusTick /*status,value=dmg*/, FreezeExpired, ChainLightning /*position→position2*/, Explosion /*position, value=radius*/,
  CrateHit, CrateBroken, PickupCollected, PickupExpired, PlayerDamaged /*value*/, PlayerHealed /*value*/, PlayerDied, PowerShotConsumed,
  ComboChanged /*ballId, value=combo*/, BossPhaseChanged, WaveSpawned, StageCleared }
```
Positions are in **sim space**; presentation converts with `ArenaLayout` (3D) — never re-derive geometry.

### 3.6 Tests (`Simulation/Tests`, NUnit EditMode)
Reflection off walls/enemy faces, shield block from below, pierce, split, bomb area, chain targets, burn/poison ticks, freeze skip, enemy advance with blocking + diagonal slide, danger row attack, wave spawn, stage clear, reward roll determinism for a seed, anti-stall guarantees exit within N seconds, PredictPath matches Step trajectory, RunState ES3-style round-trip via `JsonUtility` (smoke).

---

## 4. Configs (ScriptableObjects in Assembly-CSharp, `Nex.BilliardRogue`, assets in `Assets/Configs/BilliardRogue/`)

| Class | Asset | Contents |
|---|---|---|
| `BilliardRogueConfig` | `BilliardRogueConfig.asset` | root: refs to every config below (the only object passed from the coordinator) |
| `BallDefinition` | `Balls/Ball_<Type>.asset` ×12 | `BallRules rules`; `Sprite icon`; `Color color`, `Color glowColor`; `Material material`; `GameObject? trailPrefab`; `SfxManager.SoundEffect hitSfx, launchSfx`; `VfxManager.VisualEffect hitVfx`; `string nameKey, descKeyPrefix` (loc) |
| `BallCatalog` | `BallCatalog.asset` | `EnumDictionary<BallType, BallDefinition>` |
| `EnemyDefinition` | `Enemies/Enemy_<Type>.asset` ×12 | `EnemyRules rules`; `EnemyView prefab`; `Sprite icon`; `float hopHeight`; `SfxManager.SoundEffect hitSfx, deathSfx, attackSfx`; `VfxManager.VisualEffect deathVfx`; `string nameKey` |
| `EnemyCatalog` | `EnemyCatalog.asset` | `EnumDictionary<EnemyType, EnemyDefinition>` |
| `FieldObjectCatalog` | `FieldObjectCatalog.asset` | `EnumDictionary<FieldObjectType, FieldObjectView>` prefabs, `EnumDictionary<PickupType, PickupView>` prefabs (crate HP is `BalanceRules.crateHp`, single owner) |
| `ActDefinition` | `Acts/Act_1..3.asset` | `ActRules rules`; `string nameKey`; `GameObject environmentPrefab`; `ActLightingPreset lighting`; `BgmManager.BgmType battleBgm`; `VolumeProfile volumeProfile`; `VfxManager.VisualEffect ambientEffect` (AmbientAct{n}, played through VfxManager) |
| `ArenaConfig` | `ArenaConfig.asset` | `ArenaRules rules`; world scale; wall/floor prefabs; camera pose (position, pitch, FOV) |
| `BalanceConfig` | `BalanceConfig.asset` | `BalanceRules rules` |
| `ControlConfig` | `ControlConfig.asset` | paw mapping & strike detection tunables (see §6), debug/bot params |
| `PacingConfig` | `PacingConfig.asset` | shot cooldown, fast-forward delay/scale, enemy hop/attack durations, stagger, banners, hit-stop, slow-mo, reward reveal timing |
| `JuiceConfig` | `JuiceConfig.asset` | shake curves per damage, damage number colors/sizes, combo pitch step, flash colors |
| `HD2DVisualConfig` | `HD2DVisualConfig.asset` | world RT resolution (default 640×360), pixel snapping, tilt-shift band (center, width, falloff, max blur), bloom/vignette defaults, per-quality overrides |

`RulesFactory.Build(BilliardRogueConfig) → GameRules` is the only bridge from configs to simulation (single owner).
Designers tune everything in these assets; builders **create assets only if missing** and never overwrite existing values.

---

## 5. Starter extensions (single owners — edit these starter files, add entries only)

- `View.ViewIdentifier` += `Title, PlayerMode, Calibration, Gameplay, StageIntro, Reward, Pause, TrackingLost, Summary, Settings`.
- `SfxManager.SoundEffect` += (keep existing values; append with explicit ints ≥ 100):
  `UiMove, UiSelect, UiBack, UiPause, CueStrike, BallLaunch, BallWallBounce, BallHitSoft, BallHitMid, BallHitHard, CritHit, Blocked, EnemyDeath, BossHit, BossDeath, Explosion, Freeze, Burn, Lightning, Poison, Heal, Split, Portal, PickupBall, PickupHeal, PickupPower, EnemyStep, EnemyAttack, EnemyCast, PlayerHurt, LowHpWarning, BallReturn, TurnStart, RewardReveal, RewardPick, LevelUp, CountdownTick, StageClear, BossAppear, GameOver, Victory, CrateBreak, PowerShot`.
- `BgmManager.BgmType` += `Title, Act1, Act2, Act3, Boss, Reward` (keep `Main`).
- `VfxManager.VisualEffect` += `HitSpark, CritSpark, EnemyPoof, BossPoof, Explosion, FreezeBurst, BurnBurst, PoisonBurst, LightningHit, HealSparkle, SplitPop, PortalFlash, PickupSparkle, DustPuff, PlayerHurtFlash, WallSpark, CratePieces, LevelUpBurst`.
- `PlayerPreference` += `string localeCode = ""; int numPlayers = 1; bool leftHandedCue; int aimGuideLength = 1 /*0 short,1 normal,2 long*/; bool screenShake = true;`
- `PlayerDataManager` += run & meta persistence (ES3 stays inside PlayerDataManager):
  `RunState? LoadRun(); void SaveRun(RunState run); void ClearRun(); bool HasRunSave(); MetaProgressData MetaProgress { get; } void SaveMetaProgress();` (keys `billiardRogueRun`, `billiardRogueMeta`; respect `DISABLE_PERSISTENCE`).
- `DebugSettings` += `godMode, infiniteBalls, autoAimBot, skipCalibration, forceStartStage (-1), forceRewardBall (enum or -1), showSimDebug, disablePixelation, disableTiltShift, disableBloom, fastEnemyPhase, unlockAllBalls` + methods `KillAllEnemies()`, `ClearStage()`, `AddEveryBall()`, `ResetMetaProgress()` (guarded as the existing file is).

---

## 6. Input (`Input/`)

```csharp
public struct StrikeInfo { public Vector2 direction; /*sim space, normalized, up*/ public float power01; public bool isPowerShot; }
public interface IShotInput { bool IsTracking { get; } float LaunchX01 { get; } Vector2 AimDirection { get; } bool TryConsumeStrike(out StrikeInfo strike); void ResetStrike(); }
public sealed class PawShotInput : MonoBehaviour, IShotInput   // Initialize(int playerIndex, OnePlayerDetectionEngine engine, ControlConfig config, bool leftHanded)
public sealed class DebugShotInput : MonoBehaviour, IShotInput // mouse/arrows + Space via Nex.Dev.DebugInput; editor & debug builds
public sealed class AutoAimBot : MonoBehaviour, IShotInput      // Initialize(BallSimulator sim, Func<RunState> run, ArenaRules a, ControlConfig c); picks the best of N sampled angles via PredictPath scoring
public sealed class ShotInputRouter : MonoBehaviour, IShotInput // per player; selects Paw vs Debug vs Bot from DebugSettings and tracking state; exposes TrackingLost event
```
`ControlConfig` fields: `launchXRangeInches` (left paw offset from chest mapped to full launch width), `aimSmoothing` (OneEuro minCutoff/beta), `strikeSpeedInchesPerSec`, `contactDistanceInches`, `rearmSeconds`, `aimSampleDelaySeconds` (0.12), `powerShotSpeedMultiplier` (2), `trackingLostSeconds` (1.2), bot sampling count, bot think delay. The aim clamp is `ArenaRules.minAimAngleDeg` only: input clamps through `ArenaGeometry.ClampAim(arenaRules, dir)`.

---

## 7. Gameplay orchestration (`Gameplay/`)

```csharp
public sealed class GameSession : MonoBehaviour        // lives under GameplayView; Initialize(GameSessionContext ctx)
public sealed class GameSessionContext { BilliardRogueConfig config; GameRules rules; RunState run; BoardPresenter board; GameplayHud hud; IShotInput[] inputs; ViewManager viewManager; RunPersistence persistence; RunAnalytics analytics; ... }
public sealed class TurnController : MonoBehaviour     // state machine: StageIntro → PlayerTurn → EnemyPhase → (StageClear → Reward) | Defeat | Victory
  public event Action<RunOutcome>? RunEnded; public event Action? RewardRequested; public UniTask RunAsync(CancellationToken ct);
public sealed class ShotSequencer                       // bag order, alternating shooters (P1,P2,...), extra-ball pickups, cooldown
public sealed class TimeScaleController : MonoBehaviour // single owner of Time.timeScale for gameplay: hit-stop, slow-mo, fast-forward, pause
public sealed class RunPersistence                      // wraps PlayerDataManager run/meta save; bool HasSave (== Load() != null); RunState? Load(); BeginRun(RunState); SaveTurnBoundary(RunState); bool CompleteRun(RunState) → newRecord (a victory ranks as SimConstants.StageCount); Abandon() drops the save — Save & Quit never writes Abandoned into RunState (see GameSession.RunAsync)
```
Main loop per frame during PlayerTurn: read input → maybe `BallSimulator.Launch` → `Step(dt)` → drain `events` into `BoardPresenter.Consume(events)` + analytics aggregation → clear list.

---

## 8. Presentation (`Presentation/`)

```csharp
public sealed class ArenaLayout : MonoBehaviour   // single owner of sim→world: Vector3 ToWorld(Vector2 sim, float height=0); Vector2 ToSim(Vector3); float CellSize
public sealed class BoardPresenter : MonoBehaviour // Initialize(config, rules, ArenaLayout, camera for labels, overlay RectTransform); Rebuild(RunState); Consume(List<SimEvent>); UpdateBalls(BallSimulator); UniTask PlayEnemyPhase(List<SimEvent>, PacingConfig) ; AimGuide, cats
public sealed class EnemyView : MonoBehaviour, IPoolableObject   // model root, squash/hop/flash/death, status overlays, telegraph icon, shield indicator
public sealed class BallView : MonoBehaviour, IPoolableObject    // mesh + emissive + trail
public sealed class FieldObjectView, PickupView, CatView, AimGuideView
public sealed class WorldLabelLayer : MonoBehaviour  // crisp UI HP labels + damage numbers positioned from world camera viewport coords (pooled TMP)
public sealed class CameraShaker, ComboPresenter, ActEnvironmentController (applies ActDefinition: environment prefab, lighting preset, volume profile, ambient effect via VfxManager)
```
Everything visual goes through `VfxManager.PlayVisualEffect`, audio through `SfxManager.PlaySoundEffect` / `BgmManager`.

---

## 9. UI views (`UI/Views/`, prefabs `Assets/Prefabs/BilliardRogue/Views/`)

All subclass `SimpleCanvasView`, override `Identifier`, `Controls`, `AnalyticsScreenName`, implement keyboard navigation (KeyResponder), log every button via `RunAnalytics.UiAction(view, button)`, and use `NexLocalizedString` for all text. Start/stop logic in `ViewDidBecomeTopView`/`ViewDidLoseTopView`; `if (!IsActive) return;` in handlers.

| View | Identifier | Controls | Screen name | Key API |
|---|---|---|---|---|
| `TitleView` | Title | Exit | `title` | events `ContinueRequested, NewRunRequested, SettingsRequested`; `SetContinueInfo(RunState?)`, `SetBest(MetaProgressData)` |
| `PlayerModeView` | PlayerMode | Back | `player_mode` | `event Action<int> PlayersChosen` |
| `CalibrationView` | Calibration | Back | `calibration` | `Initialize(CameraSession, int numPlayers, ControlConfig)`; `UniTask<bool> RunAsync(ct)` (move in → raise hand → pose tutorial → test strike); uses PreviewsManager + SetupStateManager |
| `GameplayView` | Gameplay | Pause (via Back→Pause) | `gameplay` | hosts HUD widgets, PiP preview frame + PlayerIndicatorsManager; `Initialize(GameSessionContext)`; raises `RunEnded` |
| `StageIntroView` | StageIntro | None | `stage_intro` | `Show(act, stage, isBoss)`; auto pops after PacingConfig time |
| `RewardView` | Reward | None | `reward` | `UniTask<int> ChooseAsync(IReadOnlyList<RewardOption>, BallCatalog, RunState)` |
| `PauseView` | Pause | Back (= Resume) | `pause` | Resume / Settings / Save & Quit, uses ViewManager pause announcements |
| `TrackingLostView` | TrackingLost | None | `tracking_lost` | auto-dismiss when tracked |
| `SummaryView` | Summary | None | `summary` | `Show(RunState, MetaProgressData, bool newRecord)`; PlayAgain / Title |
| `SettingsView` | Settings | Back | `settings` | language (Locales), volumes (PlayerDataManager properties), aim guide, left-handed, screen shake |

Flow (`Flow/BilliardRogueCoordinator`): Title → (NewRun → PlayerMode) → Calibration → `ReplaceView(Gameplay)` inside a transaction that leaves Title as the root → Summary (`ReplaceView`) → Title. Continue skips PlayerMode but **not** Calibration. `CameraSession` (Flow) owns the `DetectionManager` instance lifetime (created before Calibration, disposed when returning to Title).

---

## 10. Rendering (`Rendering/` + `Assets/Shaders/BilliardRogue/`)
Details and chosen approach: `research/urp-hd2d-rendering.md`. Components: `PixelWorldCamera` (low-res RT + point upscale + texel snapping), `TiltShiftFeature` + `TiltShiftVolume` (Render Graph renderer feature), shaders `BilliardRogue/ToonLit`, `BilliardRogue/ToonLitTransparent`, `BilliardRogue/LitParticle` (billboard pixel sprites, main+additional lights, flipbook), `BilliardRogue/GodRay`, `BilliardRogue/Emissive`, `BilliardRogue/Outline` (optional inverted hull). Post: Bloom (HQ off, downscale), Vignette, Color Adjustments, Tonemapping (Neutral/ACES), per-act Volume profiles. Hard shadows: 1 cascade, 1024, short distance.

---

## 11. Localization
String table collection: existing `LocalizationTable`. Keys prefixed `br.` and declared once in `LocKeys.cs` (constants). `Editor/LocalizationSeeder.cs` holds all translations (en, fr-CA, zh-Hans, zh-Hant, ja) and upserts them. Names: `br.ui.<view>.<item>`, `br.ball.<type>.name`, `br.ball.<type>.desc.<level>`, `br.enemy.<type>.name`, `br.act.<n>.name`, `br.hud.*`, `br.setup.*`, `br.summary.*`, `br.reward.*`, `br.float.*` (BLOCK, CRIT, POWER, COMBO...).

---

## 12. Analytics (`Analytics/RunAnalytics.cs`, thin wrapper over `AnalyticsManager.Instance`)
`UiAction(screen, button)`, `SessionStart(numPlayers, isContinue, runId = RunState.runId, seed)`/`SessionStop(outcome)` (→ TrackGameStart/Stop), `Pause()/Resume()`, `TurnStart(stage, turn, balls, hp)`, `ShotFired(ballType, level, angleDeg, power, shooter)`, `ShotResult(ballType, hits, damage, bounces, kills, combo)`, `TurnEnd(stage, turn, enemiesAdvanced, damageTaken, hp)`, `StageStart(act, stage, isBoss)`, `StageClear(act, stage, turns, hp)`, `RewardOffered(options)`, `RewardChosen(option, index)`, `BossSpawn(type)`, `BossDefeated(type, turns)`, `RunEnd(outcome, stageNumber, turns, seconds, kills)`, `Setup(step, seconds)`, `TrackingLost(player, seconds)`, `SettingChanged(name, value)`. Every call also `Debug.Log`s through AnalyticsManager (`[Analytics] EVENT: …` in Editor/development/ENABLE_DEBUG_SETTINGS builds).

---

## 13. Editor builders (`Editor/`, namespace `Nex.BilliardRogue.Editor`)
Static entry points, each idempotent, callable via `unity command eval 'Nex.BilliardRogue.Editor.<Class>.Run();' --project-path ...` and via menu `Nex/Billiard Rogue/...`:
`ImportSettingsBuilder` (FBX/texture/audio import rules via AssetPostprocessor + reimport), `ConfigAssetsBuilder`, `MaterialsBuilder`, `WorldPrefabsBuilder`, `VfxPrefabsBuilder`, `UiViewsBuilder`, `LocalizationSeeder`, `RenderPipelineBuilder` (URP asset/renderer/quality settings), `AudioRegistryBuilder` (fills SfxManager/BgmManager/VfxManager prefab EnumDictionaries), `MainSceneBuilder`, `BuildAll`.
Rules: prefabs/scenes are regenerated from code (don't hand-tweak generated prefabs — tune configs instead); config assets are created only if missing; never delete user assets outside our folders.

---

## 14. Asset naming contracts (builders load by path; missing asset → builder uses a primitive placeholder and logs a warning)

### 14.1 Models (`Assets/Models/BilliardRogue/...`, FBX, 1 unit = 1 m = 1 cell, pivot at bottom-center, Y up, **model faces Unity +Z** — standard forward)
World convention: `ArenaLayout` maps sim x → local **+X** and sim y (up the arena) → local **+Z**, height → +Y; the camera sits south (low Z, high Y) looking north/down with yaw 0. **Origin:** the `ArenaLayout` local origin is the centre of the launch line, sim `(columns/2, 0)`, so the arena spans x ∈ [-columns/2, columns/2] (±3.5) and z ∈ [0, launchZoneHeight + rows] (0..11.6) with the grid centre at z = 6.6. `Arena.prefab` (root = `ArenaLayout`), the environment layouts (`Tools/Blender/environment/make_layouts.py`) and `ArenaConfig.cameraPosition` (relative to that origin; rig rotation `Euler(cameraPitchDeg, 0, 0)`) all share this frame. Enemies therefore face the camera by being rotated 180° around Y by `EnemyView`; the cat faces +Z (toward enemies) when striking and turns 3/4 toward the camera when idle.
Readability: the world renders at 640×360, so one cell is only ~25–30 px on screen — bold silhouettes, big eyes, 2–3 strong colour blocks per model, no thin details. Budgets: enemy ≤ 600 tris, boss ≤ 1500, hero ≤ 900, prop ≤ 300, environment piece ≤ 800.
Pipeline: offline outputs are generated into the gitignored staging mirror `Tools/Staging/Assets/...` (same sub-paths as `Starter/Assets/...`); the integration step copies them into `Starter/Assets/` so Unity imports everything once with the right import settings.
Each FBX root has **named child parts** so presentation can animate rigid parts by code (no skeletal rigs):
| File | Parts (child names) | Footprint |
|---|---|---|
| `Enemies/Enemy_Slime.fbx` | Body, Eyes | 1×1, ~0.7 tall |
| `Enemies/Enemy_Bat.fbx` | Body, WingL, WingR, Eyes | 1×1, hovering (Body pivot at 0.4) |
| `Enemies/Enemy_Skeleton.fbx` | Body, Head, ArmL, ArmR, Weapon | 1×1 |
| `Enemies/Enemy_ShieldKnight.fbx` | Body, Head, Shield (on -Z/bottom face… i.e. facing +Z toward player), Weapon | 1×1 |
| `Enemies/Enemy_Mage.fbx` | Body, Head, Hat, Staff, StaffGem (emissive) | 1×1 |
| `Enemies/Enemy_Healer.fbx` | Stem, Cap, Eyes, Spores | 1×1 |
| `Enemies/Enemy_Bomber.fbx` | Body, Shell, Fuse (emissive tip), LegsL, LegsR | 1×1 |
| `Enemies/Enemy_Totem.fbx` | Base, Face, Eyes (emissive) | 1×1, tall |
| `Enemies/Enemy_BoneWall.fbx` | Wall | 1×1 |
| `Bosses/Boss_KingSlime.fbx` | Body, Crown, Eyes | 2×2 |
| `Bosses/Boss_BoneLich.fbx` | Robe, Skull, HandL, HandR, Staff, Orb (emissive) | 2×2 |
| `Bosses/Boss_CrystalGolem.fbx` | Body, Head, ArmL, ArmR, CoreCrystal (emissive), ShieldCrystal (emissive) | 2×2 |
| `Player/Cat_Hero.fbx` | Body, Head, EarL, EarR, Tail, PawL, PawR, Cape | ~0.9 tall |
| `Player/Cue_Stick.fbx` | Stick, Tip | 1.2 long along +Z |
| `Balls/Ball.fbx` | Ball (low-poly sphere, ~80 tris, radius 0.5 → scaled in Unity) | |
| `Props/Prop_Pillar.fbx`, `Prop_Crate.fbx`, `Prop_Portal.fbx` (Ring, Swirl), `Prop_Mud.fbx` (flat), `Pickup_ExtraBall.fbx`, `Pickup_Heal.fbx`, `Pickup_Power.fbx` | | 1×1 |
| `Environment/Env_*.fbx` | floor tiles, arena wall segments, corner posts, torches/braziers (Flame part emissive), trees, bushes, grass, rocks, ruins (columns, arches, broken walls), crypt props (tombstones, candles, coffins), crystal clusters, banners, fences, lanterns, stairs | free |

Shared palette: all character/prop models UV-map into `Assets/Textures/BilliardRogue/Palette/Palette_Main.png` (32×32 swatch atlas, point filter). P2 cat uses `Palette_CatP2.png` (same layout, recolored fur swatches). Emissive parts use swatches from the palette's emissive row; the `Emissive` material flag is set by the builder for parts whose name contains `Emissive`, `Gem`, `Orb`, `Flame`, `Crystal`, `Fuse`, `Eyes` (per-model overrides in WorldPrefabsBuilder).

### 14.2 Textures & sprites
- Environment detail sets: `Assets/Textures/BilliardRogue/Surfaces/<Name>_Albedo.png`, `_Normal.png`, `_Cavity.png` (stone floor, mossy brick, crypt brick, wood planks, crystal rock, dirt, grass) — 64×64, point filter, tileable.
- Particles: `Assets/Sprites/BilliardRogue/Particles/<Name>.png` flipbook sheets (horizontal strip, N frames, frame size multiple of 4): `Spark`, `Dust`, `Leaf`, `Ember`, `Snow`, `Mote`, `Smoke`, `Star`, `Ring`, `Bolt`, `IceShard`, `Bubble`, `Heart`, `Debris`, `Flash`.
- UI: `Assets/Sprites/BilliardRogue/UI/` 9-slice pixel frames (`Frame_Panel`, `Frame_Card`, `Frame_Button`, `Frame_ButtonFocused`, `Frame_Banner`, `Bar_Bg`, `Bar_Fill_Hp`, `Bar_Fill_Boss`, `Icon_Heart`, `Icon_Ball`, `Icon_Skull`, `Icon_Turn`, `Arrow`, `Cursor`, `Chip`), logo `Logo_BilliardRogue`.
- Icons: `Assets/Sprites/BilliardRogue/Icons/Ball_<Type>.png` (32×32), `Enemy_<Type>.png` (48×48 renders), `Status_<Burn|Poison|Freeze>.png`, `Telegraph_<Spawn|Cast|Heal|Quake>.png`, `Reward_<Heal|MaxHp>.png`.
- Font: `Assets/Fonts/BilliardRogue/BilliardPixel.ttf` (original Latin pixel font) + CJK pixel fallback font(s); TMP font assets built by `FontAssetsBuilder` into the same folder.

### 14.3 Audio
`Assets/Audio/Sfx/BilliardRogue/<SoundEffect>_<n>.wav` and `Assets/Audio/Bgm/BilliardRogue/<BgmType>.wav|ogg`, mapping recorded in `Tools/Audio/audio_manifest.json` (enum name → files). `AudioRegistryBuilder` reads the manifest.

---

## 15. Module ownership (parallel build wave) — edit only files you own

| Module | Owns (create/edit) | Must not edit |
|---|---|---|
| Foundation (runs first) | Simulation data types/enums/events/SimRandom/ArenaGeometry, asmdefs, all `Configs/*.cs` initial versions, `RulesFactory`, `LocKeys.cs`, `Analytics/RunAnalytics.cs`, `Input/IShotInput.cs`, `Presentation/ArenaLayout.cs`, starter extensions (§5), `Editor/ConfigAssetsBuilder.cs`, `Editor/BilliardRogueMenu.cs` | — |
| Simulation | `Simulation/**` services + `Simulation/Tests/**` | data type shapes (additive fields OK) |
| Input | `Input/**`, `ControlConfig.cs` | |
| Rendering | `Rendering/**`, `Assets/Shaders/BilliardRogue/**`, `HD2DVisualConfig.cs`, `Editor/RenderPipelineBuilder.cs`, `Editor/MaterialsBuilder.cs`, `Editor/WorldCameraRigBuilder.cs` | |
| Gameplay | `Gameplay/**`, `Persistence/**`, `PacingConfig.cs`, `BalanceConfig.cs` | |
| Presentation-Core | `Presentation/**` except `ArenaLayout.cs`, `ActEnvironmentController.cs`, `Environment/**`; `JuiceConfig.cs`, `FieldObjectCatalog.cs`, `Editor/WorldPrefabsBuilder.cs`, prefabs Enemies/Balls/Board/Player + `World/BoardPresenter.prefab` | |
| Presentation-World | `Presentation/ActEnvironmentController.cs`, `Presentation/Environment/**`, `ArenaConfig.cs`, `ActLightingPreset.cs`, `Editor/EnvironmentBuilder.cs`, `World/Arena.prefab`, `Environment/**` prefabs | |
| UI-Views | `UI/**` (Title, PlayerMode, Settings, StageIntro, Reward, Pause, TrackingLost, Summary views, widgets, `GameplayHud`), `LocKeys.UI.cs`, `Editor/UiViewsBuilder.cs` | |
| Flow | `Flow/**` (initializer, coordinator, `CameraSession`, `CalibrationView`, `GameplayView`, DebugHooks registrations), `LocKeys.Flow.cs`, `Editor/FlowPrefabsBuilder.cs`, `Editor/MainSceneBuilder.cs`, `Main.unity` | |
| VFX | `Editor/VfxPrefabsBuilder.cs`, `Presentation/Vfx/**` (if any runtime helper) | |
| Models | `Tools/Blender/**`, `Assets/Models/BilliardRogue/**`, `Assets/Textures/BilliardRogue/Palette/**`, `Assets/Sprites/BilliardRogue/Icons/Enemy_*.png` | |
| 2D art & font | `Tools/Textures/**`, `Tools/Fonts/**`, `Assets/Textures/BilliardRogue/Surfaces/**`, `Assets/Sprites/BilliardRogue/{UI,Particles}/**`, `Assets/Sprites/BilliardRogue/Icons/{Ball_,Status_,Telegraph_,Reward_}*`, `Assets/Fonts/BilliardRogue/**`, `Editor/FontAssetsBuilder.cs`, `Editor/ImportSettingsBuilder.cs` | |
| Audio | `Tools/Audio/**`, `Assets/Audio/Sfx/BilliardRogue/**`, `Assets/Audio/Bgm/BilliardRogue/**`, `Editor/AudioRegistryBuilder.cs` | |

Cross-module needs (a field in someone else's file, a new enum entry): don't edit — write it under "Requests" in your final report; the integrator applies it.
Localization strings: add keys you need as constants in a module partial `public static partial class LocKeys` file inside your folder (e.g. `UI/LocKeys.UI.cs`) with the English text in a `// en: ...` trailing comment; the localization pass translates them.

---

## 16. Shader & material contract (Rendering owns shaders/materials; VFX, Presentation and builders only reference these names)

| Shader | Use | Properties (exact names) | Keywords |
|---|---|---|---|
| `BilliardRogue/ToonLit` | all opaque models (palette or surface textures), balls | `_BaseMap`, `_BaseColor`, `_EmissionMap`, `_EmissionColor` (HDR), `_EmissionStrength`, `_BumpMap`, `_BumpScale` (default 2 — exaggerated), `_CavityMap`, `_CavityStrength`, `_Bands` (3–5, default 4), `_ShadowTint`, `_RimColor`, `_RimPower`, `_FlashColor`, `_FlashAmount` (hit flash 0..1), `_StatusTint` (rgb + a=amount; freeze/poison/burn tint), `_Tiling` (world-UV tiling for *_Surface meshes) | `_NORMALMAP`, `_CAVITYMAP`, `_EMISSION` |
| `BilliardRogue/ToonLitTransparent` | ghosts, telegraph decals, ice overlay | same as ToonLit + `_Alpha` | |
| `BilliardRogue/LitParticle` | pixel-sprite particles (alpha-clip, lit by main + additional lights, billboards via ParticleSystemRenderer), multiplies **vertex colour** | `_BaseMap`, `_BaseColor`, `_Cutoff`, `_LightInfluence` (0 unlit … 1 fully lit), `_EmissionStrength` | |
| `BilliardRogue/GlowParticle` | additive glows, flashes, trails, light motes (bloom sources); vertex colour | `_BaseMap`, `_BaseColor` (HDR), `_Intensity` | |
| `BilliardRogue/LightShaft` | fake volumetric god rays (additive quads/cones, scrolling noise, depth/edge fade) | `_Color` (HDR), `_Intensity`, `_NoiseTex`, `_NoiseScroll` (vec), `_EdgeSoftness`, `_FadeDistance` | |
| `BilliardRogue/AimGuide` | dotted aim line on a LineRenderer (unlit, scrolling dashes, fades with distance) | `_DashTex`, `_Color` (HDR), `_ScrollSpeed`, `_FadeStart`, `_FadeEnd` | |
| `BilliardRogue/TiltShiftBlur` | full-screen pass used only by `TiltShiftFeature` | internal | |

Materials (built by `MaterialsBuilder` into `Assets/Materials/BilliardRogue/`): `M_Palette` (ToonLit, Palette_Main + Palette_Emission), `M_Palette_CatP2`, `M_Surface_<SurfaceName>` (StoneFloor, MossyBrick, CryptBrick, CryptFloor, WoodPlank, CrystalRock, Dirt, Grass — albedo/normal/cavity, `_Tiling` 1), `M_Ball_<BallType>` ×12 (ToonLit, neutral base, emission = ball glow colour), `M_LightShaft`, `M_AimGuide`, `M_GlowParticle_Default`, `M_LitParticle_Default`, `M_DangerTile` (ToonLit with emission driven by script). VFX materials live in `Assets/Materials/BilliardRogue/Vfx/` (VfxPrefabsBuilder).
Per-renderer runtime changes (flash, status tint) use `MaterialPropertyBlock` with the property names above (cache `Shader.PropertyToID`).
Layers (created by `RenderPipelineBuilder`): `World` (every 3D object the WorldCamera renders), `WorldVolume` (the world post-process Volume). UI stays on `UI`.

---

## 17. Prefab / asset path contracts between builders (MainSceneBuilder instantiates these by path)

| Asset | Built by (module) | Contents |
|---|---|---|
| `Assets/Prefabs/BilliardRogue/World/WorldCameraRig.prefab` | Rendering (`WorldCameraRigBuilder`) | WorldCamera (layer World, low-res RT via `PixelWorldDisplay`), WorldVolume (layer WorldVolume) with default profile, `WorldDisplayCanvas` (Screen Space-Camera on RootCamera at planeDistance 295, full-stretch RawImage) |
| `Assets/Settings/BilliardRogue/Volumes/Volume_Act{1,2,3}.asset`, `Volume_Title.asset` | Rendering | per-act grading/bloom/vignette/tilt-shift |
| `Assets/Prefabs/BilliardRogue/World/Arena.prefab` | Presentation-World (`EnvironmentBuilder`) | `ArenaLayout` at root, floor tiles, danger row tiles, launch pad, walls; layer World |
| `Assets/Prefabs/BilliardRogue/Environment/Env_Act{1,2,3}.prefab` | Presentation-World | act diorama dressing from `Tools/Blender/environment/layouts.json`, act lights, light shafts, ambient particle emitters; applied by `ActEnvironmentController` |
| `Assets/Prefabs/BilliardRogue/World/BoardPresenter.prefab` | Presentation-Core (`WorldPrefabsBuilder`) | `BoardPresenter`, pools roots, `AimGuideView`, two `CatView`s + cues, `CameraShaker` hook |
| `Assets/Prefabs/BilliardRogue/{Enemies,Balls,Board,Player}/*.prefab` | Presentation-Core | EnemyView per EnemyType, BallView, FieldObjectView/PickupView per type, CatView; also fills `EnemyDefinition.prefab`, `FieldObjectCatalog`, `BallDefinition.material` refs if null |
| `Assets/Prefabs/BilliardRogue/Vfx/Vfx_<VisualEffect>.prefab`, `Vfx_Ambient_Act{1,2,3}.prefab` | VFX (`VfxPrefabsBuilder`) | registered in the VfxManager prefab EnumDictionary |
| `Assets/Prefabs/BilliardRogue/Detection/OnePlayerDetectionEngine_Hidden.prefab` | Input (`DetectionPrefabsBuilder`) | engine variant with HiddenPoseNode nodes |
| `Assets/Prefabs/BilliardRogue/Input/PlayerShotInput.prefab` | Input | `PawShotInput` + `DebugShotInput` + `AutoAimBot` + `ShotInputRouter` (one instance per player at runtime) |
| `Assets/Prefabs/BilliardRogue/UI/GameplayHud.prefab` | UI-Views (`UiViewsBuilder`) | `GameplayHud : IGameplayHud` widgets |
| `Assets/Prefabs/BilliardRogue/Views/{Title,PlayerMode,Settings,StageIntro,Reward,Pause,TrackingLost,Summary}View.prefab` | UI-Views | views |
| `Assets/Prefabs/BilliardRogue/Views/{Calibration,Gameplay}View.prefab` | Flow (`FlowPrefabsBuilder`) | CalibrationView (PreviewsManager setup UI), GameplayView (HUD instance + PiP overlay canvas + label layer + session host) |
| `Assets/Prefabs/BilliardRogue/Flow/BilliardRogueCoordinator.prefab`, `BilliardRogueViewManager.prefab` (variant of starter `MainViewManager.prefab` + `SecretCodeSequenceDetector` Up Up Down Down Left Right Left Right) | Flow | |
| `Assets/Scenes/BilliardRogue/Main.unity` | Flow (`MainSceneBuilder`) | SingletonSpawner (as GameUIExample), initializer, coordinator, view manager, WorldCameraRig, Arena, Env_Act1..3 (inactive), EventSystem; build index 0 |
| TMP fonts `Assets/Fonts/BilliardRogue/BilliardPixel_TMP.asset`, `BilliardPixelBold_TMP.asset`, `BilliardPixel_CJK_TMP.asset` | 2D art (`FontAssetsBuilder`) | UI code loads these by path in builders; fallback chain Latin → CJK |

Builders reference assets of other modules **by these paths**, and degrade gracefully (placeholder + warning) when an asset is not built yet. `BilliardRogueMenu` Build All order: ImportSettings → ConfigAssets → RenderPipeline (layers, World renderer) → Materials → VolumeProfiles → WorldCameraRig → LocalizationSeeder → FontAssets (the CJK atlas covers the seeded strings) → DetectionPrefabs → InputPrefabs → WorldPrefabs → Environment → VfxPrefabs → AudioRegistry → UiViews → FlowPrefabs → MainScene.
