# v2-D Presentation / juice (Hype) — checkpoint

Owner: Claude agent `v2-presentation` (editor_lock key `v2-presentation`). Spec: `GDD-v2-Changes.md` §3.

## Plan
1. JuiceConfig: `HypeSettings` (`juice.Hype`): tier thresholds, smoothing, ball glow/size/trail, tier colours, hit VFX scale, extra sparks tier, shake multiplier + cap, number size/pop per tier, aura, cat dance/tier pose.
2. `HypeJuice` (new, Presentation): smooths the target from `BoardPresenter.SetHype`, detects tier rises, pushes a `HypeLook` to balls, cats, `BoardEventPlayer`, `CameraShaker`, `WorldLabelLayer`, `HypeAuraView`. Ticked from `BoardPresenter.Update` with unscaled time (hit-stop does not freeze the fade).
3. BallView.ApplyHype (emission strength, subtle size, trail time/width/tier tint). BoardEventPlayer hit VFX scale + extra sparks at tier ≥ 2. CameraShaker hype multiplier (capped). WorldLabelLayer / DamageNumber size + colour + pop by tier.
4. `HypeAuraView` (new): LineRenderer loop around the arena rim (M_BallTrail), pulses at tier 3. Built by BoardPrefabBuilder, wired to `BoardPresenter.hypeAura`.
5. CatView: dance with Hype (bounce, sway, squash, faster tail) and a big tier pose (jump + spin + punch).
6. Rebuild prefabs (WorldPrefabsBuilder.Run), verify in play mode with forced Hype, commit.

## Done
- Code for steps 1–5 written (uncommitted until the tree compiles in the Editor): JuiceConfig.HypeSettings,
  Presentation/HypeJuice.cs (+ HypeLook), Presentation/HypeAuraView.cs, BallView.ApplyHype, BoardEventPlayer.SetHype,
  CameraShaker.SetHypeMultiplier, WorldLabelLayer.SetHype + DamageNumber pop, CatView.SetHype/PlayTierPose,
  BoardPresenter (hypeAura field, Update tick, SetHype, SetDebugHype, HypeShown/HypeTier), BoardPrefabBuilder.BuildHypeAura.
  compile_check (skipping the v2-B in-progress `Input/Core/StrikeTypes.cs`) OK.

## Next
- Wait for the tree to compile (v2-B mid-edit), recompile in the Editor (metas for HypeJuice.cs / HypeAuraView.cs),
  run `WorldPrefabsBuilder.Run()`, verify in play mode (`BoardPresenter.SetDebugHype(0.95f)` via eval), commit.

## API
- `BoardPresenter.SetHype(float hype01)` (contract, unchanged).

## Requests
- (none yet)
