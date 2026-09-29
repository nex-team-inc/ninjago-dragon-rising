# Final wave: performance — checkpoint

Budget (Mali-G52 class, 60 fps, world 640x360 + UI 1080p): ≤ 120 batches (SetPass + shadow casters count), ≤ 60 SetPass,
≤ 150k tris, ≤ 2 ms UI, zero per-frame GC during gameplay.

Measured in the Editor (unfocused, ~10 fps, so ms numbers are not device numbers; batches / SetPass / tris / shadow
casters / GC bytes are). Probe: scratchpad `perf/PerfProbe.cs` (UnityStats + `Recorder("GC.Alloc")` sampled every
editor tick, profiler frames walked for GC bytes per PlayerLoop path, scene dump of renderers / materials / particles /
canvases), driver `perf/scen.sh` (DebugHooks: StartNewRun → SkipCalibration → SetBot; GotoStage(5) until ≥ 12 enemies;
GotoStage(11) boss; AddEveryBall for the hectic case). Seed 29, 150 frames per scenario.

## Before (1P)

| Scenario | batches med / max | SetPass med / max | tris | shadow casters med / max | GC (game paths) B/frame avg |
|---|---|---|---|---|---|
| A act 1 stage 1 (bot, balls flying) | 161 / 179 | 55 / 64 | 53k | 54 / 60 | 3337 |
| B act 2 stage 2, 15 enemies | 253 / 265 | 74 / 86 | 63k | 99 / 104 | 2460 |
| C act 3 boss (Crystal Golem) | 113 / 153 | 53 / 72 | 43k | 31 / 50 | 2531 |
| E hectic: 12 ball types incl. split/bomb/lightning at the boss | 164 / 183 | 65 / 89 | 49k | 51 / 52 | 1632 |

2P before: not measurable — the EnumDictionary conversion (review #14) had already been picked up by the Editor while the
BoardPresenter prefab still carried the array fields, so 2P starts threw at BoardViews; 2P is measured after only.

Findings behind the numbers:
- Shadow casters: every enemy part cast (Skeleton 5, Bat 4 incl. eyes and wings, Slime 4); the act dioramas cast from
  every animated part (14 canopies, 7 flames in act 1; banners, braziers, candles in act 2); ShieldMarker cast.
- All materials in use are SRP-batcher compatible (ToonLit, LitParticle, GlowParticle, LightShaft, AimGuide); no GPU
  instancing needed (SRP batcher path). Shader keywords are already `shader_feature_local`; URP strips the rest.
- Particles: max particles = recipe budgets (≤ 38 alive for the act ambient, 1–20 per burst). Fine.
- UI: one nested `Hud` canvas (45 graphics) under the GameplayView canvas (70 graphics) that also carries the world
  labels, which move every LateUpdate → the whole view canvas rebuilt (Canvas.BuildBatch 1.3 calls/frame).
- GC: 1.4–2.2 KB/frame average under `GameSession.Update`, all on event frames (shot / hit / spawn), plus runtime
  `Instantiate` of TMP labels and damage numbers (pools had capacity but no prewarm), plus Editor-only `[Analytics]`
  `Debug.Log` strings. No per-frame allocation on quiet frames (median game GC 0 B).
- Import settings already match D9 (ASTC 4x4, point, no mips; SFX ADPCM decompress-on-load; BGM Vorbis streaming).

## After pass 1 (enemy/env shadows, canvases, prewarm) — same scenarios

| Scenario | batches med / max | SetPass med / max | tris | shadow casters med / max | GC (game paths) B/frame avg |
|---|---|---|---|---|---|
| A act 1 stage 1 | 151 / 165 | 57 / 64 | 52k | 39 / 41 | 2597 |
| B act 2 stage 2, 15 enemies | 194 / 203 | 62 / 73 | 57k | 35 / 37 | 1895 |
| C act 3 boss | 112 / 137 | 56 / 66 | 43k | 22 / 25 | 2533 |
| E hectic (12 ball types, boss) | 147 / 166 | 63 / 81 | 47k | 26 / 27 | 1634 |
| D 2P act 1 stage 1 | 170 / 184 | 57 / 63 | 53k | 48 / 51 | 2498 |

Remaining casters after pass 1 (A): 14 tree canopies + the combined act mesh, 4 arena kit groups, 8 cat parts + cue,
aim-guide ghost ball + markers, portal ring/swirl/runes, pickup parts → pass 2 keeps the cat body, the portal ring and
the largest pickup part only, and the guides never cast.

## GC attribution (deep profile, scenario A, 103 frames)

Deep-profiled (`ProfilerDriver.deepProfiling`) with the bot shooting: 2.8 KB/frame averaged over the window, all of it
on shot frames and all of it analytics — `RunAnalytics.ShotFired` → `AnalyticsManager.TrackEvent` (766 B/frame,
78 KB total) and `ShotResultTracker.Consume` → `ball_result` (618 B/frame, 63 KB) — i.e. ~1.4 KB per event inside
the platform's `GameAnalyticsProperties` / `GameAnalyticsValue` / `GameAnalytics.Track` (dictionary, boxed values,
StringBuilder), plus the Editor-only `[Analytics]` `Debug.Log` JSON. Everything else is noise: `ComboPresenter` 16 B on
6 frames, `BallQueueWidget.Set` 13 B, `PlayVfx` 4 B, UniTask `DelayPromise` continuation boxes ≤ 200 B on flow frames.
Quiet frames (balls flying, no event) allocate 0 B in game code. The two per-shot events are required by TDD §12 and
their cost lives in `Nex.Platform`; only the enum-name strings were ours (now cached: `BallTypeNames`,
`EnemyTypeNames`).

## Fixes

- Enemies cast shadows from the main body part only (`WorldPrefabModels.CastShadowsFrom`, `WorldPrefabsBuilder`);
  ShieldMarker never casts.
- Environment: `Flame` and `Cloth` parts cast no shadow (`EnvironmentActBuilder.NoShadowParts`).
- HUD: HpPanel, BossBar, BallsPanel, Chips, TrackingWarnings and both banners each get their own nested Canvas
  (`UiHudBuilder.IsolateCanvas`); the WorldLabelLayer prefab gets its own Canvas (`BoardPrefabBuilder`).
- Pools prewarm (`ObjectPooler.Prewarm`): balls 16, labels 16, damage numbers 16, enemies 2 per type.
- Leftovers: (a) `ObjectPooler.Release` ignores an instance a scene teardown already destroyed (the play-exit
  MissingReferenceException), and `RunAnalytics` resolves the AnalyticsManager per call (the play-exit NRE from
  `TurnController.Abandon`); (b) `ControlConfig.strikeExpirySeconds` 0.3 → 0.6 (> shotCooldown 0.35; the asset had no
  serialized value so the class default applies) + `GameplaySessionTests.StrikeExpiryOutlastsTheShotCooldown`;
  (c) `CherryIntegrationManager.SetIsCameraMutedForDebug` writes the fake property without a CameraGuardController;
  (d) `SessionAnalytics.TrackingLost` / `PlayerTurnLoop.TrackingLostSeconds` removed; (e) `BoardPresenter` pools and
  `WorldLabel.statusIcons` are `EnumDictionary`s written by `BoardPrefabBuilder.WriteEnumDictionary`; (f) see log.

## After pass 2 (one caster per cat/prop/pickup, guides never cast; commit 7f6da517, Build All 19/19)

| Scenario | batches med / max | SetPass med / max | tris | shadow casters med / max | GC (game paths) B/frame avg |
|---|---|---|---|---|---|
| A act 1 stage 1 | 137 / 150 | 53 / 61 | 50k | 25 / 27 | 2604 |
| B act 2 stage 2, 16 enemies | 185 / 192 | 64 / 70 | 55k | 25 / 26 | 1764 |
| C act 3 boss | 101 / 129 | 52 / 66 | 42k | 11 / 15 | 2338 |
| E hectic (12 ball types, boss) | 136 / 156 | 59 / 82 | 45k | 15 / 16 | 1958 |
| D 2P act 1 stage 1 | 148 / 162 | 54 / 60 | 51k | 26 / 28 | 1007 |

(GC on these rows = shot/hit event frames, all inside `Nex.Platform` analytics, see the attribution below.)

## Log

- [x] Before measurements (1P: A, B, C, E)
- [x] Code fixes above; compile_check green
- [x] Build All, after measurements (pass 1 and pass 2), deep-profile GC attribution; pass 2 committed (7f6da517)
- [ ] EditMode tests, audio check: folded into pass 3 below

# Pass 3 — GPU on Mali-G52 (device title ~40 fps, frames alternating 17/33 ms at vsync 60)

Device facts (v2 logcat): Mali-G52, OpenGL ES 3.2, EGL surface 1920x1080, Swappy on. Editor-side only this pass: the user
is testing the demo APK on the device (no build, no adb). Scratch: `<session scratchpad>/perf2/` (`GpuAudit.cs` cameras /
URP / volumes / overdraw in canvas space, `RenderTree.cs` render-loop sample tree, `menus.sh` title / aim / pause /
reward CPU+GC, `PerfProbe.cs` from pass 1 with full GC paths).

## Before (Editor, 1920x1080 Game view)

Per-frame 1080p work (the world itself is 642x362 + post, ~1/9 of a 1080p pass per full-screen pass):
- UI is a camera stack: `Main Camera` (Base, clears, draws nothing: every UI canvas is on RootCamera) + `RootCamera`
  (Overlay, culling Everything incl. World, shadows on, HDR/MSAA allowed). A stack always renders into a 1080p
  intermediate: Main Camera stores it, RootCamera loads it, draws, stores it, `BlitFinalToBackBuffer` copies it again.
  Render tree: 3 cameras (WorldCamera, Main Camera, RootCamera), 2 UI render graphs, `BlitFinalToBackBuffer`.
- Android Blit Type = Always (PlayerSettings): the player adds its own offscreen-to-surface 1080p copy every frame.
- Full-screen UI layers (screens of 1080p fill, canvas-space estimate): title 2.71 (world RawImage 1.0 + title
  Vignette 1.0 + UI 0.7), aiming 1.51 (world 1.0 + HUD 0.5), reward 4.36 (world 1.0 + Dim 1.0 + Vignette 1.0 +
  HUD 0.48 still drawn under the dim + balls 0.9).
- World post (642x362): Bloom Dual/Half/4 iterations/HQ off, TiltShift 2 half-res passes x 9 taps + composite, Uber
  (LUT 32 HDR, vignette, Neutral tonemap). Shadows 1024, 1 cascade, 42 m, hard; 6 point lights + sun, 4 per object.
- CPU/GC (Editor, 80 frames): 368 B/frame on the title and 736 B/frame in gameplay from IMGUI `GUIUtility.BeginGUI`:
  DebugPrinter.OnGUI runs every frame although the printer is off (plus the MDK `Jazz.DebugFrameManager` in gameplay);
  16-17 B/frame each from ES3GlobalManager, NexCamera and Jazz coroutines (vendor code); game code 0 B on quiet frames.
- PlayerSettings.enableFrameTimingStats = false.

## Plan (resume here)
- [x] 1. UI straight to the backbuffer: RootCamera Base (clear black, HDR/MSAA off, no shadows/post, culls World +
      WorldVolume) in the view-manager variant (FlowPrefabsBuilder); the Main Camera is gone, an `AudioListener` object
      keeps the listener (MainSceneBuilder); `RenderingPerfContractTests` (6 tests).
- [x] 2. PlayerSettings via RenderPipelineBuilder: Frame Timing Stats on, Android Blit Type Auto (ProjectSettings.asset).
- [x] 3. Overlay fill: `UiOverlaySpriteComposer` composes Dim + Vignette (UiTheme sprites and tints, Gamma blend) into
      `Overlay_DimVignette.png`, `UiPrefabKit.DimLayers` draws it as one layer (Pause, Reward, Tracking lost, Summary,
      Player mode, Settings); HUD columns + PiP leave while the reward view is up (`GameplayView.ChooseRewardAsync`), which
      also fixes the v2 label overlap. Dropped: hollow 9-slice title vignette (saves ~0.2 of a layer, needs a sprite border
      that the make_ui.py slice validator rejects for a gradient).
- [x] 4. Low-end GPU tier: `HD2DVisualConfig.DetectTier` (names Mali-G52/G51/G31, shader level <= 35), `Volume_LowTier`
      (bloom 3 iterations, HQ off, optional quarter res; tilt-shift 2 taps), `WorldCameraRig.lowTierVolume` (priority 50),
      `DebugSettings.renderTier` (0 auto / 1 full / 2 low). Verified in play mode: renderTier 2 -> tier Low, volume on,
      evaluated bloom it=3, tilt taps=2; auto on the Mac = Full.
- [x] 5. `FrameTimingLogger` on WorldCameraRig.prefab; `DebugSettings.logFrameTiming` (on in BR_CONTROL_DEMO). Verified
      in the Editor: `[Perf] logger on: gpu=Apple M4 Max api=Metal ... frameTiming=on` then one line per 5 s.
- [x] 6. DebugPrinter sleeps (disabled) while enableDebugPrinter is off and wakes on `PlayerDataManager.DebugSettingsSaved`:
      title GC 368 -> 16 B/frame (ES3 coroutine left), gameplay 736 -> 368 (MDK `Jazz.DebugFrameManager.OnGUI` left).
- [x] 7. Paw-pick analytics: `RogueView.TrackButton(button, index, input)`, RewardView sends "motion" / "remote" / "debug".
- [x] 8. EditMode tests (Simulation 76/76, InputCore 45/45, Assembly-CSharp-Editor 48/48 incl. the 6 new ones), gameplay
      scenario numbers, device steps below; committed 038da6d4 + this follow-up. Builders run this pass: RenderPipeline,
      VolumeProfiles, WorldCameraRig, UiViews, FlowPrefabs, MainScene (not a full Build All: the spawn agent's WIP was
      in the tree, and GameplayHud / ControlReadoutOverlay / CalibrationView / TitleView / StageIntroView /
      GameplayView prefabs regenerated from it are left uncommitted for its own Build All).
- [x] 9. Follow-ups: player builds log plain Log lines with ScriptOnly stack traces (`BilliardRogueInitializer`; the
      project logs Full traces, a native unwind per [Analytics] line on every shot); RewardView uses the base view
      canvas (CS0108); logger names "missing frames under budget" when the averages fit but frames still drop.

## After pass 3 (Editor, 1920x1080 Game view)

| | before | after |
|---|---|---|
| Cameras rendered per frame | 3 (WorldCamera, Main Camera, RootCamera) | 2 (WorldCamera, RootCamera) |
| UI render graph | 3 native render passes + `BlitFinalToBackBuffer` | 1 native render pass, no final blit |
| UI render loop CPU (M4 Max, one frame) | title 0.53 ms, game 0.57 ms | title 0.31 ms, game 0.49 ms |
| 1080p fill, title (screens) | 2.71 + intermediate copy + final blit + Android blit | 2.70 (world 1.0, vignette 1.0, UI 0.7) |
| 1080p fill, aiming | 1.51 + the same 3 copies | 1.54 (world 1.0, HUD 0.5) |
| 1080p fill, reward | 4.36 + the same 3 copies | 2.87 (world 1.0, dim 1.0, balls 0.9; HUD hidden) |
| Reward batches / SetPass (med) | 131 / 46 | 107 / 36 |
| GC per frame, title (whole player loop) | 384 B (368 DebugPrinter IMGUI + 16 ES3) | 16 B (ES3 coroutine) |
| GC per frame, aiming / pause / reward | 785 B (2 x 368 IMGUI + 3 x 16) | 417 B (368 MDK DebugFrameManager IMGUI + 3 x 16-17 vendor) |

Gameplay scenarios with the pass-3 changes (the spawn agent's batch spawning is in the tree: act 1 opens with 10
enemies, the boss brings escorts, so rows are not comparable with pass 2):

| Scenario | batches med / max | SetPass med / max | tris | shadow casters med / max | GC game B/frame avg |
|---|---|---|---|---|---|
| A act 1 stage 1 | 154 / 169 | 57 / 69 | 53k | 27 / 32 | 3288 |
| B act 2 stage 2 (11 enemies) | 163 / 181 | 62 / 88 | 53k | 20 / 22 | 1728 |
| C act 3 boss + escorts (11) | 182 / 199 | 70 / 87 | 52k | 22 / 23 | 3002 |
| E hectic (12 ball types) | 186 / 225 | 70 / 95 | 51k | 20 / 22 | ~2000 |

Game-code GC is still event-frame only: `GameSession.Update` on shot / hit / spawn frames (analytics in Nex.Platform,
see the attribution above), `[Analytics]` log strings, and first activations of pooled TMP labels / enemies when a batch
brings more enemies of one type than the prewarm (enemies 2 per type, labels 16, numbers 16).

CPU (Editor, per script, median / max ms): title EventSystem 0.06, DioramaAnimator 0.04, 2D Animation
DeformationManager 0.04, UGUI batches 0.08; gameplay GameSession.Update 0.17 / 5.4 (shot frames), WorldLabelLayer
0.02 / 1.1, UGUI 0.12 / 1.5; reward UGUI 0.16. Spikes of 40-50 ms in `UniTaskLoopRunnerUpdate` are flow steps
(stage clear, reward instantiate) and Editor `Debug.Log` stack traces, not steady frames. The debug `AutoAimBot` costs
1.1 ms every frame while it drives (off for players).

## Expected device impact (estimate, Mali-G52 MC2, LPDDR4 ~8 GB/s usable, shared with the camera + MDK)

Per frame at 1080p RGBA8 (8.3 MB per full-screen read or write):
- Before: Main Camera clears + stores the intermediate (8.3 MB), RootCamera loads it (8.3), draws, stores it (8.3), the
  final blit reads + writes (16.6), the Android blit reads + writes (16.6) = ~58 MB/frame, 3.5 GB/s at 60 fps, plus
  two extra full-screen fragment passes (~0.6-0.8 ms each). That is ~6-8 ms of the frame spent copying, which fits the
  17/33 ms alternation (a GPU frame just over 16.7 ms).
- After: the UI draws straight into the surface: one store (8.3 MB) and no copies, i.e. ~50 MB/frame and ~1.5 ms of fill
  less; overlays another ~0.7 ms less (one dim layer), the reward view also drops the HUD (~0.5 screen). Low tier:
  bloom 3 mips + tilt-shift 2 taps on the 642x362 world saves only ~0.1-0.2 ms; it is there as the switch for the next
  step if the logger says GPU-bound in gameplay.
- CPU: one camera less (culling + render graph record/compile/execute of Main Camera, and RootCamera no longer culls the
  World layer), no IMGUI pass on the title, no native stack unwinding per log line.
Expectation: title and menus at a steady 60; gameplay depends on the world pass (642x362 ToonLit with 4 per-pixel lights,
~3 ms estimated) and on the main thread with the MDK running; the [Perf] line answers which.

## Device verification (next install; nothing was built or installed this pass)

1. `ControlDemoBuild.Run()` (or menu Nex/Billiard Rogue/Build Control Demo APK), `adb install -r`, launch.
2. `adb logcat -s Unity | grep Perf`: the first line lists the device (expect `gpu=Mali-G52 api=OpenGLES3 ...
   frameTiming=on`), then one line every 5 s in this format (values are per 5 s window):
   `[Perf] fps <avg> missed <% frames over 20 ms> max <worst frame>ms | cpu main <avg>/<max> render <avg>/<max> gpu
   <avg>/<max> ms (avg/max) wait <present wait> | <GPU-bound / CPU-main-bound / CPU-render-bound / within budget / missing
   frames under budget> | gc <B/frame> on <frames> frames, <n> GCs | batches <n> setpass <n> tris <n>k | tier <Full/Low> |
   <top view or turn phase>`. Expect `tier Low` on the Playground. `gpu n/a` means the driver gave no timer queries;
   fps and CPU still count.
3. Sit 20 s each on: title, calibration, aiming (act 1), balls flying with Hype, reward pick (paws), pause, act 2 dense,
   act 3 boss. Note fps / missed / gpu / cpu main per screen and the bottleneck word.
4. A/B the tier: Debug Settings (Konami code) → Render: Quality Tier 1 (full) vs 2 (low), 20 s each on the same screen;
   Render: Disable Bloom / Disable Tilt-Shift for the post cost; compare the gpu ms.
5. Check the picture: title / reward dim look unchanged, no black frame or stretched image after the Blit Type change,
   the platform overlays (camera mute, pause) still draw.
6. If still GPU-bound in gameplay: next levers are per-object lights 4 → 2 and/or per-vertex additional lights for the
   low tier (a second URP asset or a keyword), shadow map 1024 → 512, LUT 32 → 16, tilt-shift off in the low tier
   (`HD2DVisualConfig.lowTierTiltShift`), bloom quarter resolution (`lowTierBloomQuarterResolution`). If CPU-main-bound:
   profile with the Unity Profiler over adb (development build) on the heaviest screen.

## Still at risk
- Android Blit Type Auto is untested on the Playground (surface is 1920x1080, so no blit is expected); if the screen is
  black or letterboxed, set it back to Always in RenderPipelineBuilder.ConfigurePlayer.
- Remaining per-frame GC outside game code: MDK `Jazz.DebugFrameManager.OnGUI` (368 B in the Editor, gameplay only),
  ES3 / NexCamera / Jazz coroutines 16-17 B each (vendor / platform code, not changed).
- Batch spawning (spawn agent) raises gameplay SetPass to 57-70 median, 87-95 max at the boss with escorts (budget 60),
  and new enemies beyond the pool prewarm instantiate TMP labels mid-stage. Request for the spawn agent: prewarm enemy
  pools to the largest batch per type and labels to the max enemies on the board.
- The title keeps its full-screen vignette (1 layer); a hollow 9-slice would save ~0.2 of a layer but needs a sprite
  border the make_ui.py slice validator rejects for a gradient.
- Editor frame times are not device times; every ms number above is Editor-side or an estimate until the logger runs on
  the Playground.
