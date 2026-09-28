# Billiard Rogue audio pipeline (`Tools/Audio`)

Owner: Audio module (TDD §15). Produces every `SfxManager.SoundEffect` and `BgmManager.BgmType` / `StingerType`
clip, the manifest `audio_manifest.json` (TDD §14.3) and the Editor builder that wires them into the singleton prefabs.

## Regenerate (one command, deterministic)

```bash
/Users/simonbut/project/VibeProject3/Tools/.venv/bin/python /Users/simonbut/project/VibeProject3/Tools/Audio/build_audio.py \
    [--preview <dir for review PNG/JSON/MD>]
```

- ~30 s warm. The first run on a machine downloads ~80 source files (~6 s latency each, 12 in parallel) through Git LFS.
- Sources come from the team repo `~/Documents/music-cell-shared-assets`, materialized **per file** with
  `git show HEAD:<path> | git lfs smudge -- <path>` (`shared_repo.py`). The repo's sparse checkout and work tree are
  never changed; `.meta` files are never copied; every fetched file is checked to be real audio (`ffprobe`, not an LFS
  pointer). The local copy lives in the gitignored `Tools/Staging/.cache/audio_src/` and is pruned to the current picks.
- Two consecutive runs produce byte-identical WAV/OGG/JSON (Ogg Vorbis is encoded bit-exact, synth noise is seeded).
- The build fails (exit 1) on: wrong format, a peak over the ceiling, a loop click, a length change after Vorbis
  encoding, a BGM more than 1 LU off target, and the mix rules below (loudness ladder, onset timing, C6 ping tuning,
  summing headroom). `--preview` also writes `audio_sfx_onsets.png` (first 250 ms of every timed SFX) and
  `audio_headroom.png` (worst-case sums).

## Outputs (staging mirror of `Starter/Assets`, copied in by integration)

| Path | Format |
|---|---|
| `Tools/Staging/Assets/Audio/Sfx/BilliardRogue/<SoundEffect>_<n>.wav` (76 files, 1-3 variants; `UiBack` reuses `GenericExit_1.wav`) | 44.1 kHz mono 16-bit PCM, peak <= -3 dBFS |
| `Tools/Staging/Assets/Audio/Bgm/BilliardRogue/<BgmType>.ogg` (Title, Act1-3, Boss, Reward) | 44.1 kHz stereo Ogg Vorbis q5, -18 LUFS |
| `Tools/Staging/Assets/Audio/Bgm/BilliardRogue/Stinger_<StingerType>.ogg` (StageClear, BossAppear, Victory, Defeat) | 44.1 kHz stereo Ogg Vorbis q5, -18 LUFS |
| `Tools/Audio/audio_manifest.json` | `sfx` (enum -> files), `bgm`, `stingers`, `sources` (file -> shared-repo path or `synth:<preset>`), `sfx_levels` (file -> built `peak_db`, `samples`: the builder's import check) |
| `Tools/Staging/EditorScripts/AudioRegistryBuilder.cs` | goes to `Starter/Assets/Scripts/BilliardRogue/Editor/` |

## Files

| File | Role |
|---|---|
| `build_audio.py` | main build: fetch -> render SFX / process BGM -> write -> verify -> manifest -> review sheets |
| `picks.py` | **single source of truth** for assignments: layers per variant, loudness category, trims |
| `synth_presets.py`, `sfxsynth.py` | sfxr-style numpy synth (template from `research/asset-toolchain-templates/Audio`) + our presets |
| `audiolib.py` | decode (ffmpeg/soxr), BS.1770 loudness, analysis (centroid, attack, loop wrap, tempo), trim/fade/filters, WAV/OGG writers |
| `shared_repo.py` | per-file LFS materialization + cache |
| `sheet.py` | review images (waveform + log spectrogram, loop-wrap close-ups, loudness bars) |
| `audition.py` | candidate comparison used to choose the picks (`--slot X --sheet`); not part of the build |

## Processing

**Mix plan (headroom).** Unity sums every source in float and Android clamps the result to 16-bit, so the content leaves
room for summing: SFX peaks stop at **-3 dBFS**, music at **-18 LUFS / -3 dBFS**, and Foundation is asked for a
**-6 dB master trim** (music then plays at -24 LUFS, the broadcast TV reference; see Requests). The build sums worst-case
event chains over whole loops, the way Unity plays them (mono clip on a 2D source = the same signal on both channels at
unity gain, default sliders, stinger over music ducked to 0.3 in 0.15 s), at every 0.25 s offset, and fails unless the
result stays at least 0.3 dB under 0 dBFS after that trim (`picks.HEADROOM_SCENARIOS`):

| Scenario (loop) | Raw max / median | After -6 dB trim |
|---|---|---|
| single BallHitHard_2 (Boss / Act3) | -1.2 / -3.3, +0.6 / -3.2 dBFS | -7.2 / -5.4 |
| bomber chain: hard hit + 2 Explosion + 2 EnemyDeath + CritHit in 80 ms (Boss / Act3) | +5.5 / +4.0, +5.5 / +4.0 | -0.5 / -0.5 |
| boss kill: BossHit + BossDeath + CritHit + Explosion (Boss) | +4.1 / +2.8 | -1.9 |
| Victory accent + Stinger_Victory over ducked Boss | +0.7 / -0.9 | -5.3 |
| BossAppear accent + Stinger_BossAppear over ducked Act3 | -0.8 / -1.7 | -6.8 |

Without the trim (or a limiter) every bomber chain clips by up to 5.5 dB. That is why the trim is a request, not an option.

**SFX.** Each variant is a mix of layers (shared-repo samples and/or synth presets). Each layer is decoded to 44.1 kHz
mono (anti-phase-safe downmix), onset-trimmed (-45 dB re peak), optionally cut (`from_peak_ms`: start N ms before the
loudest point), filtered, re-pitched, peak-normalised and offset by its layer gain. The mix gets optional tanh drive
(denser body under the same peak), a 25 Hz high-pass, silence trim (-50 dB head / -60 dB tail), a 0.5 ms fade-in and a
quadratic fade-out. Loudness is the peak of a 100 ms K-weighted window ("short LUFS"; EBU windows are longer than most
SFX); "onset" is the loudest window starting in the first 10 ms (what competes on the frame the sound fires). All
variants of one SoundEffect are set to the same level (the quietest reachable one), so round-robin never jumps in volume.

Reachable levels (short LUFS, mono), quietest to loudest:

| Tier | Level | Effects |
|---|---|---|
| UI | -23 (BallReturn -22, RewardReveal -21.3, UiSelect -21) | UiMove, UiBack/GenericExit, GenericEnter |
| flow | -20 (TurnStart -19, LowHpWarning -17) | UiPause/Resume, CountdownTick, RewardPick |
| **bounce** | **-20.0** | BallWallBounce (most frequent) |
| **soft hit** | **-18.5** | BallHitSoft |
| **mid hit** | **-17.0** (Blocked -17.5, CrateBreak -17.0) | BallHitMid |
| **hard hit** | **-15.5** | BallHitHard, BossHit |
| **big (onset)** | **-14.0 .. -13.5** | EnemyDeath -14.0, Explosion -14.0, CritHit -13.5, BossDeath -13.5 (onset -14.0) |
| other | ball -17 (CueStrike -16.1), magic -16 (Heal -17, Split -17.6), pickup -16 (LevelUp -18.4), enemy step -18 / attack -15 / cast -17, PowerShot -14.5, PlayerHurt -12 | |
| accents | StageClear -17, BossAppear -16, GameOver -18, Victory -17 | flow accents over the music stingers |

The combat ladder is asserted by the build (`picks.LADDER`): each tier's quietest file is >= 1.5 LU over the loudest file
of the tier below, the big tier measured on its onset (a kill confirm lands on the same frame as the hit, so its onset
must not be masked), and PowerShot (a frequent bonus) may not top the big tier. The big tier reaches its level through
tanh drive (CritHit 5 dB, EnemyDeath/Explosion 6 dB, BossDeath 2 dB) rather than peak, which is what keeps the bomber
chain under the headroom limit. Other build checks: SFX tied to an instant VFX (`picks.INSTANT`) reach -3 dB of their
peak within 30 ms and their first 110 ms is within 1 LU of their loudest window; UI clicks reach -3 dB of peak within
5 ms; every hit family that carries the combo ping (Soft/Mid/Hard/Crit/BossHit) has its strongest 1.0-1.1 kHz partial at
C6 = 1046.5 Hz +/- 5 cents (all are within 2 cents).

**BGM.** Source loops are the UGMC "LOOP" edits (musical-length, designed to wrap). Processing is loop-safe only:
decode stereo 44.1 kHz, one **static** gain to -18 LUFS integrated (no dynamic `loudnorm`, which would change the level
across the wrap), -3 dBFS sample-peak ceiling (Title is peak-capped at -18.15 LUFS rather than limited; a memoryless soft
knee would be used only if a track were > 1 LU short), Ogg Vorbis q5 bit-exact. Verification decodes the OGG: sample count
identical to the source, loudness, peak, and the wrap step (|last-first| sample) compared with the track's own
99.9th-percentile sample step: all six loops are 0.02-0.25 (below 1 = no click). The music stingers get the same
treatment as stereo one-shots at -18 LUFS for `BgmManager.PlayStinger` (which ducks the music to 0.3).

## Final assignments and rationale

Paths are under `music-cell-shared-assets/Assets/`. U = `Universal Sound FX/`, C = `Cute UI _ Interact Sound Effects Pack/AUDIO/`,
M = `Merge Games Sound Effects and Music Pack/AUDIO/`, UGMC = `Ultimate Game Music Collection/`.
All candidates were compared by duration, peak, short/integrated loudness, spectral centroid, attack time,
waveform/spectrogram and (BGM) loop wrap, tempo, loudness range and loop length (`audition.py`).

### BGM (`BgmType`)

| Key | Source loop | Length | Why |
|---|---|---|---|
| Title (+ Main) | UGMC `Fantasy Orchestral/Upbeat City LOOP` | 87.7 s | Research pick kept: warm orchestral adventure, 115 BPM, dynamic (LRA 7.6) with a breakdown, perfectly continuous wrap. `Main` (starter key) aliases it so it never plays silence. |
| Act1 Mossy Ruins | UGMC `Combat/Sea Battle LOOP` | 99.5 s | Replaces the research pick `Skeletons LOOP`: Skeletons is only 42.7 s (would repeat ~15x per act) and is the darkest/bassiest Act 1 candidate (86 % energy < 200 Hz). Sea Battle is a bright, steady swashbuckling adventure (112 BPM, centroid 514 Hz, LRA 4.3), 2.3x longer, clean wrap. |
| Act2 Sunken Crypt | UGMC `Combat/Enemies LOOP` | 128 s | Replaces `Dark Dungeon ACTION LOOP` (51 s, very dense/bright). Enemies is darker (centroid 369 Hz), 75 BPM, builds and relaxes (LRA 8.6) = less fatigue over ~10 min, and is 2.5x longer; wrap step 0.04. Dark Dungeon ACTION is the alternate. |
| Act3 Crystal Hollow | UGMC `Combat/Frantic Battle LOOP` | 92.3 s | Replaces `Epic Combat LOOP` (48 s): same epic register but faster (129 BPM, 5.5 onsets/s) for the final act's escalation and ~2x longer; `Circle of Death` (36 s, choppy) and `Barren Boss` (desert-flavoured, reads as a boss cue) rejected. |
| Boss | UGMC `Combat/Boss Battle 1 Loop` | 86.7 s | Research pick kept: fastest candidate (157 BPM), relentless (LRA 2.0), clearly more intense than Act 3; wrap 0.03. |
| Reward | UGMC `Short Cues/Item Stores/Item Store 1/Item Store 1 LOOP` | 16 s | Research pick kept: calm shop loop (≈60 BPM plucks, LRA 1.2) for a < 10 s card choice; `Calm Bright`/`Pretty Dungeon` rejected for 0.2-1.5 s of silence before their wrap. |

### Music stingers (`StingerType`, stereo OGG, Music group) and flow accents (`SoundEffect`, mono WAV)

The music exists **only** in `Stinger_*.ogg`. The four flow SoundEffects are short accents designed to layer on top
of it (revision 1 shipped the same recording twice, which stacked +6 dB in phase or flammed when both were played, and
kept playing music on the SFX group when the Music slider was at 0). Tonal accents are transposed into the stinger's key
(chroma match): Marimba SHORT WIN is A major, Dark Defeat 2 is E minor.

| Event | `StingerType` music (OGG) | `SoundEffect` accent (WAV) |
|---|---|---|
| Stage clear | StageClear: UGMC `Short Cues/Winning/Marimba SHORT WIN` (2.1 s, 22 ms attack, cute marimba for 12 plays per run) | StageClear_1: U `RETRO_LOFI/RETRO_Bonus_03` at -5 st (D major arpeggio -> A major), 0.56 s chip bonus jingle |
| Boss appear | BossAppear: UGMC `Combat/Danger LOOP PARTS/Danger HORN` (3.0 s, swell peaks ~0.9 s) | BossAppear_1: U `LOFI_Rumble_01` under the 0.8 s push-in, then at **0.78 s** a synth `boss_thud` (C2) + U `IMPACTS/Stone/IMPACT_Stone_Deep` + U `IMPACT_Metal_Cling_Deep` at -5 st (gong ring); 1.25 s. Fire it when the push-in starts. |
| Defeat | Defeat: UGMC `Short Cues/Losing/Dark Defeat 2` (5.2 s, 57 ms attack) | GameOver_1: U `MUSIC_EFFECTS/Solo_Chip_Square/..._Negative_03` at +4 st (falling C minor arpeggio -> E minor), low-passed 6 kHz, 0.68 s |
| Victory | Victory: UGMC `Short Cues/Winning/Fanfare WORLD WIN` (6.4 s, instant attack) | Victory_1: C `Confetti/SFX_Confetti_Explosion_1` + `Epic Toon FX/etfx_explosion_sparkle2` (atonal burst, cannot clash), 0.69 s |

### SFX (`SoundEffect`)

| Key (variants) | Source(s) | Why |
|---|---|---|
| GenericEnter (1), GenericExit (1) | M `SFX/UI/Click/SFX_UI_Click_Open_1` / `_Close_1` | Framework keys (Debug Settings open / Back) were silent; same click family as the starter's Generic clicks. |
| UiMove (2) | M `SFX_UI_Click_Generic_1/2` | Soft 2-3 kHz ticks for remote navigation, quietest category. |
| UiSelect (1) | C `UI/Click/Select/SFX_UI_Button_Click_Select_1` | Brighter "confirm" click that reads over move ticks (variant 2 is a double click, dropped). |
| UiBack (= GenericExit_1.wav) | M `SFX_UI_Click_Close_1` | **Same asset as GenericExit** (manifest points both keys at one file): ViewManager's Back already plays GenericExit, and SfxManager dedups one AudioClip per frame, so a view that also plays UiBack on the same press gets one click, not two summed in phase (+6 dB). |
| (all UI clicks) | | Cut to start 2 ms before the transient (`from_peak_ms=2`): the Merge clicks carried ~22 ms of pre-noise; every click now reaches -3 dB of its peak in 1-3 ms. |
| UiPause (1) / UiResume (1) | synth `ui_pause` / `ui_resume` | Falling / rising B-E square blip; `TIME_WARP_*` were 1.5 s of sub-bass (89 Hz centroid) — inaudible on TV speakers and too slow. UiResume exists in the Foundation enum (not in TDD §5). |
| CountdownTick (2) | C `UI/Countdown/SFX_UI_Countdown_Blow_1/2` | Cut to start 6 ms before the peak (the source's 115 ms swell made the tick late). |
| CueStrike (2) | U `SPORTS/Pool/POOL_Ball_Hit_03/05` + synth `cue_thump` | Real pool clack = billiards identity; a 180 Hz body and 6 dB drive make the main player action land (+5 LU over the raw clack at the same peak). |
| BallLaunch (2) | U `8BIT/Jumping/8BIT_RETRO_Jump_Glide_Up_Bright_Quick` + U `WHOOSHES/Classic/WHOOSH_Bright_Short` | Rising 0.15 s chip glide = ball rocketing up the arena, air layer for the magic; `RETRO_Pew_01` falls in pitch and rings 0.5 s. V2 is -1.5 st. |
| BallWallBounce (3) | U `RETRO_LOFI/RETRO_Bump_01/02/04` | 0.09 s low retro bumps (centroid 670-1200 Hz) for the most frequent event; `Bump_03` had a 32 ms attack, `Bump_05` rings 0.44 s. |
| BallHitSoft (3) | U `IMPACTS/Shoot_Em_Up/Short/IMPACT_ShootEmUp_Short_05_RR 01-03` + synth `hit_ping` | The designed round-robin set with the warmest spectrum (~2 kHz; the research's `Short_01` RR is 5-6 kHz and harsh for the most common hit). **Combo:** every hit carries a C6 tonal ping at the same pitch in every variant, so `PlaySoundEffect(effect, pitch: 2^(combo/12))` reads as a rising scale. |
| BallHitMid (3) | U `.../Medium/IMPACT_ShootEmUp_Medium_06_RR 02/03` (+ RR 03 at -1.5 st) + `hit_ping` + `sub_thump` | Heavier body; the research's `Medium_03` RR has 41-61 ms attacks and RR 01 of `Medium_06` ~30-40 ms — replaced. Capped at 0.3 s. |
| BallHitHard (3) | U `RETRO_LOFI/RETRO_Impact_02/05/04` + `hit_ping` + `sub_thump` | Sub-ms attacks, crunchy retro body, extra sub weight; same C6 ping. |
| CritHit (2) | U `RETRO_LOFI/RETRO_Melee_Attack_Kick_Punch_05/08` + synth `crit_shing` + `hit_ping`, 5 dB drive | Punch + crushed arpeggio sparkle = unmistakably special; the arpeggio starts on a just fifth over C6 (1569.75 Hz) so the drive's 2G-2C product lands on the ping, and Punch_05 is re-pitched +0.06 st to put its own partial on C6. |
| Blocked (2) | U `IMPACTS/Metal/IMPACT_Metal_Cling_Bright` + U `RETRO_Bump_01` | Shield Knight "BLOCK" = metallic clang with instant attack (2.5 ms); force-field/shield spells had 130-200 ms swells. |
| BallReturn (2) | C `Collect/Pop/SFX_Player_Collect_Pop_1/2` | Every ball exit plays this — soft quiet pop; `8BIT_RETRO_Effect_Reverse_Zoom` swells for 0.6 s at 8 kHz. |
| PowerShot (1) | U `8BIT/Weapons/8BIT_RETRO_Fire_Blaster_Deep_Glide` + synth `power_boom` | Chip blast plus sub boom for "POWER!". |
| EnemyDeath (2) | U `RETRO_LOFI/RETRO_Destroy_03` (+1.5 st) + synth `enemy_pop`, 6 dB drive | `Destroy_03` is the low, crunchy member (665 Hz centroid); research's `Destroy_01` sits at 8.9 kHz (harsh for every kill). Pop layer keeps it cute; the drive lifts the kill confirm 1.5 LU over the hard hit it fires with. |
| BossHit (3) | U `.../Medium/IMPACT_ShootEmUp_Medium_10_RR 01-03` (+0.23/+0.25/+0.18 st) + synth `boss_thud` + `hit_ping` (-4 dB) | Research RR set kept; added 65 Hz thud for the boss's mass. Medium_10 has a strong partial at 1031.6-1033.4 Hz (22 cents flat of the C6 ping, 12-15 Hz beating); each RR is re-pitched so it lands on 1046.5 Hz, and the ping is raised from -8 to -4 dB. |
| BossDeath (1) | U `RETRO_LOFI/RETRO_Explosion_Nuclear_01`, 2 dB drive | Big 3 s retro blast, faded at 3.0 s. |
| EnemyStep (3) | synth `enemy_step` | No clean single-hop thud existed (`THUD_Dark_03` quiet/long, `LOFI_Marching` 2.3 s loop). 110 ms squishy thud at three pitches; SfxManager's 35 ms repeat limit collapses a whole row hop into one step. |
| EnemyAttack (2) | U `RETRO_Melee_Attack_Kick_Punch_02/06` | Whoosh-into-punch matches the lunge animation. |
| EnemyCast (1) | U `MAGIC_SPELLS/MAGIC_SPELL_Dark_Pulse_Echo_Subtle` | Dark, readable telegraph. |
| CrateBreak (1) | U `RETRO_LOFI/LOFI_Break_01` + U `IMPACTS/Wood/IMPACT_Wood_01` | Retro break with a wooden body. |
| Explosion (2) | synth `power_boom` + synth `blast_crack` at t=0, over U `RETRO_LOFI/RETRO_Explode_05` + U `8BIT_RETRO_Explosion_Short_Deep` / U `RETRO_Explode_02` + `Explode_05`, 6 dB drive | Short (0.45-0.7 s) so Bomber chain reactions don't smear. The gated 8-bit crackle peaked 160-190 ms late; the boom + noise crack puts the loudest part on the bomber flash (-3 dB of peak at 0.5-4.5 ms). |
| Freeze (2) | synth `ice_chime` + U `ELEMENTS/Ice/ICE_Cracking_02` / U `SHATTER/SHATTER_Glass_Small_01` | C7-E7-G7 crystal chime + crackle; `ICE_Cracking_04` (research) starts 240 ms late. |
| Burn (2) | U `MAGIC_SPELLS/MAGIC_SPELL_Flame_02` / U `EXPLOSIONS/Short/EXPLOSION_Short_Ignite_Kickback_Debris` (0.55 s) + `Flame_02` at -2 st, all high-passed 200 Hz, 3 dB drive | Flame_02 cut to 25 ms before its peak. `Flame_01` (a pulsing swell peaking at 188 ms, 305 Hz centroid that TV speakers barely play) is replaced by an instant ignite; both variants now hit within 2 ms at 1.6 / 2.6 kHz centroid and reach the magic target. |
| Lightning (3) | U `ZAPS/ZAP_Electric_01` + U `ELECTRICITY/ELECTRICITY_Spark_01/02/03` (cut 5 ms before their peak) | Sparks alone are 10 kHz, 0.12 s and thin; the zap gives body, the spark varies per RR (Spark_03 used to land 50 ms late). |
| Poison (2) | synth `bubbles` + C `Pop/Liquid/SFX_Pop_Liquid_1` / C `Bubble/Natural/SFX_Pop_Bubble_Single_1` | Bubbling "blorp" in 0.2 s; the research's bubbling-zaps spell has a 360 ms attack and lasts 1.6 s. |
| Heal (1) | U `RETRO_LOFI/RETRO_Powerup_03` | Short (0.6 s) — Fang heals per hit, so it must be brief. |
| Split (3) | C `Pop/Mouth/SFX_Pop_Mouth_High_Sharp_1/2/3` (pairs, 45 ms apart) | A double pop reads as "splits in two". |
| Portal (1) | U `MAGIC_SPELLS/MAGIC_SPELL_Teleport` | Cut to 90 ms before its peak, capped 0.6 s; `SPACE_WARP_Quick_Charge_Jump_Away_01` peaks after 3 s. |
| PickupBall (1) | U `8BIT/Powerups/8BIT_RETRO_Powerup_Spawn_Quick_Climbing` | Research pick; quick climbing chip = +1 ball. |
| PickupHeal (1) | NEX `Sound effect/Unrealsfx - Magical Game - Magic Potion Pickup` | Distinct from the per-hit Heal; 96 kHz source resampled, faded at 1.2 s. |
| PickupPower (1) | C `Powerup/SFX_Powerup_Crystal_1` | Sparkling crystal arpeggio; research's `Powerup_Spawn_Flash_Aggressive` was 1.2 s, -1.4 short LUFS and slow. |
| PlayerHurt (1) | U `8BIT/Hits_Bumps/8BIT_RETRO_Hit_Bump_Quick_Dark_Drop` + synth `player_hurt` | Instant thump + falling square. Cat meows in the pack all swell for 0.5-1.4 s, rejected. |
| LowHpWarning (1) | synth `heartbeat` | 0.35 s lub-dub (75/86 Hz fundamentals + saturation harmonics so TV speakers reproduce it); UGMC heartbeats are 1 s with a 173 Hz centroid. Play it on a ~0.8 s cadence while HP is low. |
| TurnStart (1) | synth `turn_start` | Rising C-E-G chime, 0.42 s; `UI_Beep_Double_Clean_Up` is a 695 Hz beep, `Quest Snare` 4 s. |
| RewardReveal (3) | U `CARDS/CARDS_Deal_01_RR1/2/3`, cut 15 ms before the slap, 4 dB drive | Card-deal whooshes, one per card. The slap used to land at 53/136/96 ms (80 ms jitter against the staggered card animation); every variant now lands at 14-16 ms. |
| RewardPick (1) | C `UI/Bonus/SFX_UI_Bonus_Rich_1` | Rich bonus shimmer, faded at 1.2 s. |
| LevelUp (1) | M `SFX/UI/Claim_Purchase_Upgrade/SFX_UI_Upgrade_1` | Musical upgrade cue, clearly different from the chip pickups; UGMC Level Up cues are 6-21 s. |

## Runtime notes for Gameplay / Presentation / UI

- Combo pitch: `SfxManager.Instance.PlaySoundEffect(SoundEffect.BallHitSoft, Mathf.Pow(2f, Mathf.Min(combo, 12) / 12f))`
  (semitone steps). All hit families (Soft/Mid/Hard/Crit/BossHit) share the C6 ping, tuned within 2 cents, so mixed hits in
  one combo stay in tune.
- **Flow events fire both paths together, at the same moment:** `SfxManager.PlaySoundEffect(StageClear | BossAppear |
  GameOver | Victory)` (the short accent, SFX group) **and** `BgmManager.PlayStinger(StageClear | BossAppear | Defeat |
  Victory)` (the music, Music group, ducks the BGM). They are designed to layer; neither duplicates the other. Fire
  BossAppear when the 0.8 s camera push-in starts: its impact is built in at 0.78 s.
- `UiBack` and `GenericExit` are one clip. A view does not need to avoid playing UiBack on a Back press that ViewManager
  already answers with GenericExit; SfxManager plays one click.
- BGM loops are exact-length; set `AudioSource.loop = true` (BgmManager does) and don't trim in Unity.

## Unity integration

1. Copy `Tools/Staging/Assets/Audio/**` into `Starter/Assets/Audio/**` (no `.meta`; Unity mints them) and run the
   import-settings builder (D9).
2. Copy `Tools/Staging/EditorScripts/AudioRegistryBuilder.cs` to `Starter/Assets/Scripts/BilliardRogue/Editor/`.
3. `unity command eval 'return Nex.BilliardRogue.Editor.AudioRegistryBuilder.Run();' --project-path /Users/simonbut/project/VibeProject3/Starter`
   (also `Nex/Billiard Rogue/Audio Registry`, and `Build All` picks it up). It reads `../Tools/Audio/audio_manifest.json`,
   loads the clips (importing any that are on disk but not imported yet), makes `SfxManager.soundEffectDict`,
   `BgmManager.bgmDict` and `BgmManager.stingerDict` contain every enum key in ascending order (via
   `EnumDictionaryRepair.EnsureAllKeys`), overwrites the manifest keys, keeps other keys' values, saves only when
   something changed, and returns a summary (missing clips, unknown manifest keys, keys without audio).
4. The same run **verifies the SFX imports** against `sfx_levels`: it reports an `IMPORT-SETTINGS ERROR` for any clip
   whose importer has Force To Mono with Normalize on, whose sample count changed, or whose peak (read with
   `AudioClip.GetData`, Decompress On Load clips only) differs from the built peak by more than 0.5 dB. Run it after the
   ImportSettingsBuilder; an error there means the loudness matching was undone on import.

## Requests to other owners

- **Foundation (VolumeManager / AudioMixerUtils owner): add master headroom. Required, or the output clips.**
  `Starter/Assets/Audio/AudioMixer/Main.mixer` has attenuation only, and a Bomber chain reaction peaks at +5.5 dBFS at
  default sliders (table above). Apply a constant trim to the master: in `VolumeManager.SetMasterVolume`,
  `audioMixer.SetFloat(masterVolumeParameterName, AudioMixerUtils.GetMixerVolumeValueFrom01Value(value) + masterHeadroomDb)`
  with `const float masterHeadroomDb = -6f` (this brings the worst case to -0.5 dBFS, and the music to -24 LUFS, the
  broadcast TV reference). Better still, also add a Compressor/limiter effect on the Master group (threshold about -3 dB,
  fast attack, ratio 10:1 or more). `.mixer` files can't be hand-edited, so do it from an editor builder or once in the
  Editor. If the trim value changes, update `picks.PLANNED_MASTER_TRIM_DB` so the build's headroom check follows it.
- **ImportSettingsBuilder (2D art & font owner): don't let Unity re-normalize the SFX.** For
  `Assets/Audio/Sfx/BilliardRogue/**` set `forceToMono = false`. The files are already mono, so Force To Mono gains
  nothing, and with the importer's default `normalize: 1` Unity normalizes each clip during the downmix. Peaks range
  from -3 to -13 dBFS by design, so every file would rise by 3-13 dB (61 of 76 by more than 4 dB). That undoes both the
  loudness matching and the headroom plan. If Force To
  Mono stays on, set `m_Normalize` to 0 through `new SerializedObject(importer)`. The D9 table and research §10.1 say
  "forceToMono=true ... our SFX are already peak-normalised"; that is no longer true. AudioRegistryBuilder reports the
  problem if it happens. Keep ADPCM + Decompress On Load for every SFX: after this revision the longest SFX is BossDeath_1
  (3.0 s) and the rest are 1.25 s or shorter. It also keeps the builder's GetData check working. Make
  `Bgm/BilliardRogue/Stinger_*.ogg` Compressed In Memory (not Streaming) so the stinger starts without stream latency.
- **Integrator (TDD §5 / §8):** add a note that the four flow SoundEffects (StageClear, BossAppear, GameOver, Victory)
  are accents fired **together with** `BgmManager.PlayStinger` (StageClear, BossAppear, Defeat, Victory), and that
  `UiBack` shares the GenericExit clip. Flow and Presentation code that follows the TDD should call both paths.

## Known limits

- Picks were judged by measurement and waveform/spectrogram review, not by listening; an ear pass on TV speakers is
  still recommended (especially Act1 = `Sea Battle`, which replaced the research pick).
- Loudness targets are calibrated relative to each other and to -18 LUFS music. Final mix balance (mixer group levels)
  should still be checked in game. The headroom check assumes unity gain on the SFX sources and pitch 1; combo pitches
  above 1 shorten a clip but don't raise its peak.
- The flow accents were chosen by chroma and waveform analysis to layer over their stinger; judge by ear whether
  BossAppear's 0.78 s impact sits well with the horn swell.
- Act3 (loudness range 1.2) and Boss (2.0) are both flat-intensity combat loops told apart mostly by tempo (129 vs
  157 BPM). An ear pass should confirm the boss stage feels like an escalation.
