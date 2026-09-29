# v2-D Presentation / juice (Hype) — checkpoint

Owner: Claude agent `v2-presentation` (editor_lock key `v2-presentation`). Spec: `GDD-v2-Changes.md` §3.
Status: **done** (code, prefabs, play-mode verification). Only the requests below remain for the integrator.

## Done
1. `JuiceConfig.HypeSettings` (`juice.Hype`): tier thresholds 0.25/0.55/0.85, smoothing (rise 0.08 s / fall 0.35 s,
   unscaled), tier colours (yellow / red-orange / hot pink, saturated so the additive HDR trail and aura keep colour),
   ball glow ×2.2 / size ×1.15 / trail time ×2 / width ×1.35 / tint 0.85, hit VFX ×2, shake ×2.5 (boosted shakes capped
   at 16 px), extra sparks from tier 2, damage numbers +16 px and +22 % pop per tier (tier-coloured), rim aura, cat dance
   and tier pose. Values are code defaults (the JuiceConfig asset picks them up; save the asset to freeze them).
2. `Presentation/HypeJuice.cs` (+ `HypeLook` struct): smooths the target from `BoardPresenter.SetHype`, detects tier
   rises (hysteresis 0.05 on the way down), pushes the look to balls (every frame; BallView skips unchanged looks),
   and on change to BoardEventPlayer, CameraShaker, WorldLabelLayer, HypeAuraView and the cats. Zero GC.
3. `BallView.ApplyHype`, `BoardEventPlayer.SetHype` (hit + wall-spark VFX scale, extra HitSpark at tier ≥ 2, + CritSpark
   at tier 3), `CameraShaker.SetHypeMultiplier`, `WorldLabelLayer.SetHype` + `DamageNumber` pop overshoot.
4. `Presentation/HypeAuraView.cs`: one looped LineRenderer (M_BallTrail) on the arena rim, fades in and pulses at tier 3.
   Built by `BoardPrefabBuilder.BuildHypeAura`, wired to `BoardPresenter.hypeAura`.
5. `CatView.SetHype` (hop per beat, sway yaw/roll, squash, head nod, faster tail; 1.8→3.6 beats/s) and
   `CatView.PlayTierPose` (jump + spin + scale punch, stronger per tier) + a LevelUpBurst at the cat on each tier rise.
6. `WorldPrefabsBuilder.Run()` rebuilt the world prefabs (BoardPresenter gets HypeAura; the other prefab diffs are the
   current builder's cast-shadow settings that had not been rebuilt yet).
7. Verified in play mode (StartNewRun → SkipCalibration → AddEveryBall → `BoardPresenter.SetDebugHype` → Shoot):
   hype 0 = v1 look; 0.6 = tier 2, warm trail, no aura; 0.95 = tier 3, pink trail, pink rim aura, big pink damage
   number with extra sparks, cat dancing. No console errors from Presentation.

## API
- `BoardPresenter.SetHype(float hype01)` (contract, unchanged): call every frame or on change; visuals follow smoothly.
- `BoardPresenter.SetDebugHype(float hype01)` (negative releases), `HypeShown`, `HypeTier` (debug / verification).
- `JuiceConfig.Hype` (`JuiceConfig.HypeSettings`).

## Requests
- v2-A / integrator: `HypeController` already calls `BoardDriver.SetHype` → `BoardPresenter.SetHype` every tick — keep
  it. Keep `HypeConfig` tier thresholds equal to `JuiceConfig.Hype.tier1..3` (0.25/0.55/0.85), or, once HypeConfig is
  committed, let `HypeJuice.TierOf` read `config.Hype.TierThreshold(t)` / `TierHysteresis` (small change in HypeJuice).
- Optional: `DebugHooks.SetHype` handler could also call `BoardPresenter.SetDebugHype` so the look can be checked while
  aiming (today the board only shows Hype while balls fly, as specified).
