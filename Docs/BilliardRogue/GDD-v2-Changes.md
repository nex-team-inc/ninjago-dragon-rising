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

## 5. Enemy spawning (playtest round 2, 2026-09-29)
- Enemies **appear on the field from nowhere** (pop-in with a spawn VFX + SFX, small stagger between them) at **random free cells** anywhere in the grid **except the 3 rows nearest the player** (with 10 rows: rows 0..6; never rows 7, 8, 9 — the danger row is 9). They no longer march in as rows from the top.
- Spawning is in **batches**: a batch of **at least 10 enemies** (`ActRules.minEnemiesPerBatch`, default 10; composition from the act's weighted pool and difficulty budget, HP scaling unchanged) every **3 turns** (`ActRules.spawnEveryNTurns`, default 3), starting with a batch on the stage's first turn. A normal stage has `ActRules.batchesPerStage` batches (default 3). If fewer free cells exist than the batch size, as many as fit spawn. Pickups (a few per batch) and field objects follow the same free-cell rule.
- **Skip empty turns**: if after the enemy phase the field has no enemies (ignoring Bone Walls) and batches remain, the next batch spawns immediately (the turn counter jumps to the next spawn turn) — the player never gets a turn with nothing to shoot. With no batches left and no enemies, the stage is cleared.
- Boss stages: the boss appears at start (2×2, rows 0–1, centre) together with the first escort batch; escort batches follow every `spawnEveryNTurns` while the boss lives (same ≥10 rule, fewer if cells run out).
- Enemies still advance one row per enemy phase and attack from the danger row as before.

## 6. Debug overlay hidden by default
- The control readout (debug canvas) is hidden by default in every build, including the control demo; it stays available as the Debug Settings toggle "Show Control Readout". The DebugPrinter stays off by default.

## 7. Turns are 3 shots, and the enemies advance every turn (playtest 3, 2026-09-29)
- A turn fires `BalanceRules.shotsPerTurn` balls (default 3; 0 = the whole bag, the v1 rule), taken from the bag in order and continuing across turns (`RunState.nextBagIndex`), so reward balls enter the rotation. +1 Ball pickups add a bonus Basic shot to the current turn (it does not carry over and does not move the rotation).
- After the turn's last ball the enemy phase runs: every enemy steps forward and attacks from the danger row. Practice mode no longer means infinite balls or holding the enemies above the danger row (both made the enemies stop).

## 8. Damage numbers (playtest 3)
- Every ball hits in its own colour (`JuiceConfig.numbers.ballColors`) as a light-to-deep gradient over a dark outline dropped one font pixel; numbers land white-hot, arc away, grow at mid / hard damage, pop harder with damage and combo; killing blows shine, crits cycle the rainbow, COMBO text heats from gold to pink and turns rainbow from 12.

## 9. No cat on the board, the cue stays (playtest 3)
- The cat knight is hidden (`JuiceConfig.cat.visible` off); its cue lies behind the waiting ball along the aim and thrusts on the strike, only while a shot waits. The title's cat portraits stay.

## 10. 3 HP, every attack takes 1 (playtest 4)
- `playerMaxHp` 3 and `BalanceRules.damagePerAttack` 1: every melee or ranged attack takes exactly 1 HP whatever the enemy. Heals fit 3 HP: stage clear +1, heal pickup +1, a boss clear refills, Vampire heals at most 1 / 1 / 2 per shot.
- Practice mode (god) is off by default in every build, the control demo included.

## 11. 2 players shoot together (playtest 4)
- In 2P each player has `shotsPerTurn` shots per turn and both shoot at the same time from their own launch position, each with their own cue and aim guide; whoever fires takes the next bag ball, bonus shots are shared. The turn ends when both are out of shots and the balls are back. A player with shots left who leaves the frame pauses the run.
- Enemies (bosses included) spawn with `BalanceRules.coopEnemyHpScale` × HP in 2P (default 2).

## 12. Balance with 3-shot turns and 3 HP (`Tools/sim_smoke_eval.cs`, 6 seeds)
- Tuned: 2 batches per normal stage, `hpScalePerStage` 0.08, bosses at 65 % HP (King Slime 46, Bone Lich 85, Crystal Golem 130).
- 1P aimed bot: 0/6 without Hype (dies in act 2), 3/6 with Hype 0.5; 1P casual (30 % random shots) with Hype 0.5: 0/6. 2P co-op with Hype 0.5: 2/6 aimed, 2/6 casual. Hype and ball power are being redesigned next (playtest 4 items B and C); rebalance after them.
