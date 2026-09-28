# Billiard Rogue — Game Design Document

Status: v1 (production target). Platform: Nex Playground (Android, TV, landscape 1920x1080, camera body tracking, TV-remote UI navigation). Engine: Unity 6000.3.9f1, URP 17.3.

All numbers below are **defaults**; every one of them lives in a ScriptableObject config so a designer can tune without code (see TDD.md → Configs).

---

## 1. Pitch

A cozy-but-tense roguelike where a cat knight plays billiards against a dungeon. Hold up your **left paw** — that's the ball. Your **right paw** is the cue. Snap the right paw into the left paw and the ball rockets up the arena, ricocheting off walls and monsters, dealing damage on every hit. When you've shot every ball in your bag, the monsters march one row closer. Clear the stage, pick a new ball with a strange power, and push deeper — three acts, three bosses, one life.

Pillars: **Readable** (every hit, number and threat is clear), **Snappy** (a turn never drags), **Juicy** (hits feel great: hit-stop, sparks, bloom, crunchy SFX), **Diorama** (HD-2D miniature look: pixelated 3D, tilt-shift, warm light shafts).

---

## 2. Core loop

```
Run start ─► Stage intro ─► [ Player turn ─► Enemy phase ]* ─► Stage clear ─► Reward (1 of 3) ─► next stage
                                   │                                  │
                                   └── HP 0 ─► Defeat summary         └── after Act 3 boss ─► Victory summary
```

### 2.1 Player turn
- The HUD shows the **ball queue** (icons in firing order) and the counter `Balls 6/8`.
- Each **action** fires exactly one ball: the next ball in the queue appears at the cue.
- Shots can be fired while earlier balls are still bouncing, after a short **shot cooldown** (0.35 s). Skilled players chain shots rapidly.
- A ball's action ends when it **exits through the bottom boundary** (the launch line).
- The turn ends when **all balls have been fired and all balls in flight have exited**.
- Anti-stall: a ball in flight > 7 s, or with 10 consecutive wall bounces without touching an enemy/object, gets a growing downward pull until it exits. When no balls remain to shoot and only stragglers fly for > 2.5 s, time scale ramps to 2.0x ("Fast-forward »" chip on the HUD).

### 2.2 Enemy phase (~1.5–2.5 s total)
1. Status ticks: burn/poison damage (with numbers), freeze decrements.
2. Special abilities (spawn, heal, cast) — each shows a telegraph icon one turn before.
3. **Advance**: every non-frozen enemy moves down by its `moveRows` (hop animation 0.4 s, all at once). Blocked cells: try diagonal down toward the center column, else stay.
4. **Danger row attacks**: every enemy standing in the danger row (bottom grid row) attacks the player for its `attack` damage (lunge animation + hurt flash + screen shake). Ranged enemies attack from any row on their cooldown.
5. New wave row spawns at the top (if the stage has waves left).
6. If player HP ≤ 0 → defeat.

### 2.3 Stage clear
All scheduled waves spawned and no enemies left → "STAGE CLEAR" stinger (1.2 s) → Reward view. Leftover pickups vanish.

---

## 3. Controls

### 3.1 Body (gameplay)
| Body | Game |
|---|---|
| Left paw horizontal position (relative to the chest, body-normalized) | Cat + ball position along the launch line |
| Vector from right paw → left paw | Cue/aim direction (clamped to 12°–168° from horizontal, always upward) |
| Right paw moving fast toward the left paw and touching it | **Strike** (fire) |

- Natural pose: left paw raised in front of the chest (the ball), right paw below/right of it (the cue), then thrust the right paw into the left paw.
- Strike detection: right-paw closing speed toward the left paw ≥ `strikeSpeedThreshold` (body-normalized, ≈ 35 in/s) AND paw distance ≤ `contactDistance` (≈ 5 in), with a 0.35 s re-arm. Aim direction is taken from the smoothed vector sampled ~120 ms **before** the strike started (so the thrust itself does not wobble the aim).
- **Power strike**: closing speed ≥ 2x threshold → first enemy hit deals +1 damage and shows "POWER!" (reward, not requirement).
- Aim guide: dotted line to the first bounce plus a short segment after (length tunable; "Long aim guide" setting).
- Handedness setting: "Left-handed cue" swaps the paw roles.
- Tracking lost > 1.2 s during the player turn → pause overlay "Step back into view" (auto-resumes when tracked again).

### 3.2 Remote / keyboard (UI)
All menus: arrows + Enter (TV remote), Back via the top-level control panel. Secret code **Up Up Down Down Left Right Left Right** opens Debug Settings (debug builds).

### 3.3 Editor/debug fallback (not player-facing)
Mouse/arrow keys aim, Space fires, via `Nex.Dev.DebugInput`; plus an **auto-aim bot** debug toggle for automated playtests.

---

## 4. Arena

- Grid: **7 columns × 10 rows** of 1-unit cells. Row 0 = top. Row 9 = **danger row** (tinted red floor tiles, pulsing when occupied).
- Launch zone below the grid: 1.6 units tall; the cat walks along the launch line.
- Walls: left, right, top reflect balls. Bottom is open (exit).
- Ball: radius 0.2, speed 13 units/s (per-ball multiplier), perfectly elastic reflections, no gravity (except anti-stall pull).
- Enemy hitbox: cell inset 0.08 (boss: its full footprint inset 0.1). Balls reflect off enemies (unless piercing).
- Camera: perspective, FOV ~28°, pitched ~58° down at the arena center: a miniature diorama; the whole grid stays inside the sharp focus band.

### 4.1 Field objects
| Object | Behaviour |
|---|---|
| **Pillar** | Static, indestructible, reflects balls, blocks enemies. |
| **Crate** | Static, 3 HP (scaled), reflects balls; breaking it may drop a pickup. |
| **Portal pair** | Static pair (A↔B). A ball entering one exits the other with the same velocity (0.25 s re-entry lock). Enemies cannot enter. |
| **Mud** | Static; balls passing through are slowed to 60% for 0.8 s. |
| **Pickup: +1 Ball** | Moves with waves; collected by any ball touch → one extra Basic shot this turn. |
| **Pickup: Heal** | +4 HP. |
| **Pickup: Power** | Next fired ball deals 2x on its first hit. |

Pickups that reach the danger row are removed.

---

## 5. Balls

Start bag: **4 × Basic**. Bag cap: 12 balls. Balls fire in bag order (new balls are appended). Level cap 3.

| Id | Name | Rarity | Lv1 | Lv2 | Lv3 |
|---|---|---|---|---|---|
| Basic | Cue Ball | Common | 1 dmg | 2 dmg | 3 dmg |
| Flame | Ember | Common | 1 dmg + Burn 2 | Burn 3 | Burn 4, burn spreads to 1 neighbour on death |
| Frost | Frost | Uncommon | 1 dmg, 35% Freeze (1 turn) | 50% | 65%, frozen targets take +2 |
| Thunder | Volt | Uncommon | hit chains to 2 enemies ≤2.2 cells, 1 dmg | 3 chains | 4 chains, 2 dmg |
| Bomb | Boom | Uncommon | first enemy hit: 3 dmg in 3×3 | 4 dmg | 5 dmg, 5×5 cross |
| Splitter | Twin | Rare | first enemy hit: splits into 2 minis (1 dmg) | 3 minis | 3 minis, minis 2 dmg |
| Piercer | Lance | Rare | passes through enemies (max 4), 2 dmg each | max 6 | max 8, 3 dmg |
| Iron | Iron | Common | 3 dmg, speed 0.75x, max 25 bounces | 4 dmg | 5 dmg |
| Venom | Venom | Uncommon | 1 dmg + Poison 1 | Poison 2 | Poison 3 |
| Vampire | Fang | Rare | 1 dmg, heal 1 per hit (max 2/shot) | max 3/shot | max 4/shot |
| Rubber | Bouncy | Common | 1 dmg, +1 dmg per wall bounce (max +4) | max +6 | max +8 |
| Lucky | Lucky | Rare | 1 dmg, 25% crit x3 | 35% | 45%, crit x4 |

Status effects:
- **Burn N**: at enemy phase take N damage, then N−1.
- **Poison N**: every hit on this enemy deals +1 dmg; at enemy phase take N damage (does not decay; max 5).
- **Freeze**: skips the next advance and attack; ice overlay.

Unlocks (meta): Splitter & Vampire unlock after first King Slime kill; Piercer & Lucky after first Bone Lich kill. Others available from the start.

---

## 6. Enemies

Base stats at stage 1; HP scales `hp × (1 + 0.16 × stageIndex)` (rounded), attack scales `+1 per act`.

| Id | Name | HP | Move | Attack | Special |
|---|---|---|---|---|---|
| Slime | Slime | 4 | 1 | 2 | — |
| Bat | Bat | 2 | 2 | 1 | Fast |
| Skeleton | Skeleton | 7 | 1 | 3 | — |
| ShieldKnight | Shield Knight | 6 | 1 | 3 | Shield on its bottom face: hits from below deal 0 ("BLOCK") — bank it off the walls |
| Mage | Imp Mage | 5 | 1 (every 2nd turn) | 2 ranged | Casts a bolt at the player every 2 turns from any row (telegraphed) |
| Healer | Shroom | 5 | 1 | 1 | Heals adjacent enemies 2 HP each enemy phase |
| Bomber | Beetle | 4 | 1 | 5 | On death explodes: 3 dmg to adjacent enemies (chain reactions!) |
| Totem | Totem | 10 | 0 | 0 | Every 2 turns spawns a Slime in a free adjacent cell |

### Bosses (2×2 cells, boss HP bar at top of arena)
| Id | Act | HP | Move | Pattern |
|---|---|---|---|---|
| KingSlime | 1 | 70 | every 2nd turn | Spawns 2 Slimes every 2 turns; below 50% HP spawns 3 |
| BoneLich | 2 | 130 | every 3rd turn | Casts 3-dmg bolt every 2 turns; raises 3 Bone Wall crates every 3 turns; summons 2 Skeletons at 50% |
| CrystalGolem | 3 | 200 | every 2nd turn | One face shielded (rotates each turn, shown by a glowing crystal); Quake every 3 turns: all enemies advance 1 extra row |

Boss intro: camera push-in 0.8 s, name banner, boss BGM.

---

## 7. Run structure

- **3 acts × 4 stages** (stage 4 = boss) = 12 stages.
- Act themes (lighting presets): **Act 1 Mossy Ruins** (golden hour, warm god rays, falling leaves), **Act 2 Sunken Crypt** (deep blue night, torches, drifting embers), **Act 3 Crystal Hollow** (violet/cyan magical glow, floating motes).
- Each normal stage: 5–8 **waves**. Wave 1 spawns at stage start (rows 0–1), then one new row each enemy phase until the schedule is exhausted. Waves are generated from weighted enemy pools per act with a difficulty budget that grows with stage index. 20% of rows contain a pickup; stage templates place 0–3 static field objects.
- Boss stage: boss at rows 0–1 center + 3 escort waves, then escorts every 2 turns while the boss lives.
- Between stages: **Reward view**: 3 cards (new ball / level-up an owned ball / Heal 12 / +5 Max HP), rarity weights by act. After a boss: full heal 50% of max HP first.
- Player: 30 max HP.
- Seeded RNG per run (seed saved) → a continued run is deterministic.

### 7.1 Pacing targets (flow)
| Moment | Target |
|---|---|
| Title → first shot | < 40 s (setup included) |
| Stage intro banner | 1.4 s |
| One shot (aim + strike) | 1–2 s |
| Typical player turn | 15–30 s |
| Enemy phase | 1.5–2.5 s |
| Stage | 2–4 min |
| Full run | 30–45 min |
| Reward choice | < 10 s |

Juice: hit-stop 40 ms on kills/crits (60 ms on boss hits), camera shake scaled by damage, damage numbers (white normal, yellow crit, blue block, green heal, orange burn, purple poison), combo counter per ball ("x7 COMBO") with rising pitch on successive hits, slow-mo 0.3 s on the last enemy of a stage.

---

## 8. Multiplayer (1–2 local players)

- Chosen in the Player Mode view; setup calibrates each player (DetectionManager multiplayer flow; players stand side by side).
- Co-op, shared HP and ball bag. Shots alternate P1, P2, P1… across the turn. Each player has their own cat (P1 orange tabby, P2 grey tuxedo); only the active shooter's cat holds the ball.
- The PiP camera preview shows both players with a head indicator; the **active** player's indicator is large, colored and bouncing, the other is small and dim. On-field: "P1/P2" pennant over the active cat, and the HUD banner "P2's shot!" when control switches.

---

## 9. UI flow (every step is a ViewManager view)

| # | View | Transition | Notes |
|---|---|---|---|
| 1 | **TitleView** | root push | Continue (if save) · New Run · Settings. Best record. Diorama idle behind. |
| 2 | **PlayerModeView** | push | 1 Player / 2 Players (remembered). |
| 3 | **CalibrationView** | push | Camera setup: move into frame → raise hand → pose tutorial → test strike. Uses PreviewsManager + SetupStateManager. Required before gameplay (also on Continue). |
| 4 | **GameplayView** | replace (stack reset to Title below) | HUD, PiP preview, player indicators, arena. |
| 4a | StageIntroView | push overlay (auto-pops) | "Act 1 · Stage 2 — Mossy Ruins". |
| 4b | RewardView | push overlay | 3 cards, left/right + Enter. |
| 4c | PauseView | push overlay (blur) | Resume · Settings · Save & Quit. |
| 4d | TrackingLostView | push overlay | Auto-resume. |
| 5 | **SummaryView** | replace | Victory/Defeat, stats, new record, Play again / Title. |
| — | SettingsView | push | Language, Master/Music/SFX volume, Aim guide length, Left-handed cue. |
| — | DebugSettingsView | push | Secret code / debug builds. |

Save points: after each enemy phase, after each reward pick, on Save & Quit, and on app pause. Continue resumes at the start of the player turn.

---

## 10. Audio & visual direction

- BGM: Title (warm adventure), Act battle loops ×3 (one per act), Boss loop, Reward/calm loop; stingers: stage clear, boss appear, victory, defeat.
- SFX: retro-leaning but punchy; every ball hit pitch-rises with the combo.
- HD-2D: low-res pixelated 3D, 4-band cel shading, crisp hard shadows, heavy bloom on emissives (torches, crystals, magic balls), tilt-shift blur outside the arena band, vignette, per-act color grading, volumetric-looking light shafts, lit pixel-sprite particles animated at 12 fps.
- UI: pixel-style font, chunky 9-slice pixel frames, high-contrast HP numbers above every enemy (rendered crisp at native resolution).

---

## 11. Analytics (summary — full taxonomy in TDD.md)
Every UI action, every turn start/end, every shot and its result, stage start/clear, reward offered/chosen, boss events, run end, setup steps, pause/resume, tracking lost, settings changes.
