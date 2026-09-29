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
      in the Editor: `[Perf] logger on: gpu=... frameTiming=on tier=...` then one line per 5 s.
- [x] 6. DebugPrinter sleeps (disabled) while enableDebugPrinter is off and wakes on `PlayerDataManager.DebugSettingsSaved`:
      title GC 368 -> 16 B/frame (ES3 coroutine left), gameplay 736 -> 368 (MDK `Jazz.DebugFrameManager.OnGUI` left).
- [x] 7. Paw-pick analytics: `RogueView.TrackButton(button, index, input)`, RewardView sends "motion" / "remote" / "debug".
- [ ] 8. Full EditMode tests, gameplay scenario numbers, device steps, commit (the pass-3 code is committed at the
      milestone; the spawn agent works in parallel, so shared generated prefabs are left to its Build All).
