# Billiard Rogue — v2 design changes (from the first on-device playtest, 2026-09-29)

These override GDD.md where they conflict. Every number is a default in a config asset.

## 1. Rewards are balls only
- The reward view always offers **3 balls** (distinct types). No Heal / Max HP / abstract "upgrade" cards.
- Picking a ball **adds one ball of that type** to the bag. If the bag is at `bagCap`, only owned types are offered and picking one **levels up** the lowest-level ball of that type (shown as "Lv 1 → Lv 2").
- Rarity weights per act and unlock tiers still apply; Basic is never offered while any ability ball is available.
- To replace the missing Heal card: after every stage clear the player heals `BalanceRules.stageClearHeal` (default 4 HP); after a boss the existing `bossHealFraction` still applies.
- Ball presentation must be understandable **from its look alone**: a big, distinct ball visual (colour + emblem), its name, and a **1–3 word effect** (`br.ball.<type>.short`):
  | Ball | Short |
  |---|---|
  | Basic | Plain shot |
  | Flame | Burns |
  | Frost | Freezes |
  | Thunder | Chain zap |
  | Bomb | Explodes |
  | Splitter | Splits in 2 (Lv3: 3) |
  | Piercer | Pierces |
  | Iron | Heavy hit |
  | Venom | Poisons |
  | Vampire | Heals you |
  | Rubber | Wall bounce power |
  | Lucky | Lucky crits |
  Long per-level descriptions stay in the table for a future codex but are no longer shown on cards.

## 2. Shooting is easier to trigger
- Lower the strike speed threshold (≈ 55% of the v1 value), increase the contact distance (≈ 1.8×), shorten re-arm (≈ 0.25 s), and accept a strike when the right paw moves **toward** the left paw fast enough and either gets within the (larger) contact distance **or passes the left paw's line**. Angle tolerance toward the left paw ≈ 60°.
- Aim still comes from the right → left paw vector sampled just before the strike starts.
- All values in `ControlConfig`; the ControlConfig asset is updated to the new defaults.

## 3. Body movement powers flying balls ("Hype")
- While **at least one ball is in flight**, the player's **whole-body motion** charges **Hype** (0..1):
  - Motion intensity = smoothed speed of tracked nodes (wrists/hands, elbows, shoulders, hips, knees, head), body-normalized in inches/s via `DistancePerInch`, minus a jitter deadzone, over a range → 0..1. Fast attack (~0.1 s), slower release (~0.5 s). In 2P the **maximum** of the tracked players counts (co-op: anyone dancing powers the ball).
  - Hype drives, for every ball in flight, continuously: **speed** × lerp(1.0, 1.8), **damage** × lerp(1.0, 2.5) (rounded, at least +1 at Hype ≥ tier 2), and **juice**: hit-stop duration × lerp(1, 3) (capped per second so pacing never stalls), camera shake × lerp(1, 2.5), hit VFX scale × lerp(1, 2), damage numbers larger, ball glow / trail / size up, screen-edge aura at the top tier.
  - Tiers for UX: 0.25 / 0.55 / 0.85 → HUD Hype meter ("POWER ×1.4!", "×1.9!!", "MAX!!!") with SFX stingers when a tier is reached.
  - Encourage movement: when balls are flying and Hype < 0.2 for 1 s, show a big "MOVE!" prompt with a dancing-cat icon near the arena bottom; the cat avatar dances with the player's motion.
  - Hype resets to 0 when no ball is in flight; it never affects aiming (the next shot still uses the paw aim/strike).
- Keyboard/debug: holding a debug key (and the bot) simulates Hype so it can be tested without a body.
- Analytics: per shot, average and max Hype and the damage gained from Hype.

## 4. Choosing a ball with your hands
- The reward view becomes a **motion view**: the 3 ball options float across the upper-middle of the screen. **Two cartoon cat arms** (the character's arms: fur sleeves + paws, P1 orange / P2 charcoal) rise from the bottom-left and bottom-right edges; each paw follows the player's corresponding hand (body-relative hand position mapped generously to screen space so small movements cover the screen).
- When **both paws are on the same ball**, a radial fill runs for `rewardHoldSeconds` (≈ 0.8 s); completing it **picks** that ball (grab animation, SFX, VFX). Moving a paw away drains the fill. The hovered ball enlarges and shows its name + short effect.
- Remote arrows + Enter still work (accessibility / no tracking), and `DebugHooks.ChooseReward` still works. In the Editor without a body, the mouse drives both paws.
- 2P: choosers alternate per reward (P1, then P2, …) with a "P2 picks!" banner; only the chooser's arms show.

## Contracts (added in code before the build agents start)
- `Nex.BilliardRogue.IMotionEnergy { float Energy01 { get; } bool IsTracked { get; } }` and `IPawPointer { bool TryGetPaws(out Vector2 left01, out Vector2 right01); }` (Input). `ShotInputRouter.MotionEnergy` / `.PawPointer` expose them (null until implemented).
- `IGameplayHud.SetHype(float hype01, int tier)` and `IGameplayHud.ShowMovePrompt(bool visible)`.
- `BoardPresenter.SetHype(float hype01)` (juice scaling).
- `BallSimulator.SetHype(float speedMultiplier, float damageMultiplier)` (applied every step to all balls in flight).
- `RewardView.SetPawPointer(IPawPointer? pointer, int chooserIndex, int numPlayers)` (called by GameplayView before `ChooseAsync`).
