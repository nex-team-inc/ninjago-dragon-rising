# Billiard Rogue audio pipeline (`Tools/Audio`)

Owner: Audio module (TDD §15). Produces every `SfxManager.SoundEffect` and `BgmManager.BgmType` / `StingerType`
clip, the manifest `audio_manifest.json` (TDD §14.3) and the Editor builder that wires them into the singleton prefabs.

## Regenerate (one command, deterministic)

```bash
/Users/simonbut/project/VibeProject3/Tools/.venv/bin/python /Users/simonbut/project/VibeProject3/Tools/Audio/build_audio.py \
    [--preview <dir for review PNG/JSON/MD>]
```

- ~25 s warm. The first run on a machine downloads ~75 source files (~6 s latency each, 12 in parallel) through Git LFS.
- Sources come from the team repo `~/Documents/music-cell-shared-assets`, materialized **per file** with
  `git show HEAD:<path> | git lfs smudge -- <path>` (`shared_repo.py`). The repo's sparse checkout and work tree are
  never changed; `.meta` files are never copied; every fetched file is checked to be real audio (`ffprobe`, not an LFS
  pointer). The local copy lives in the gitignored `Tools/Staging/.cache/audio_src/` and is pruned to the current picks.
- Two consecutive runs produce byte-identical WAV/OGG/JSON (Ogg Vorbis is encoded bit-exact, synth noise is seeded).
- The build fails (exit 1) if a file has the wrong format, a peak over the ceiling, a loop click, a length change after
  Vorbis encoding, or a BGM more than 1 LU off target.

## Outputs (staging mirror of `Starter/Assets`, copied in by integration)

| Path | Format |
|---|---|
| `Tools/Staging/Assets/Audio/Sfx/BilliardRogue/<SoundEffect>_<n>.wav` (77 files, 1-3 variants) | 44.1 kHz mono 16-bit PCM |
| `Tools/Staging/Assets/Audio/Bgm/BilliardRogue/<BgmType>.ogg` (Title, Act1-3, Boss, Reward) | 44.1 kHz stereo Ogg Vorbis q5 |
| `Tools/Staging/Assets/Audio/Bgm/BilliardRogue/Stinger_<StingerType>.ogg` (StageClear, BossAppear, Victory, Defeat) | 44.1 kHz stereo Ogg Vorbis q5 |
| `Tools/Audio/audio_manifest.json` | `sfx` (enum -> files), `bgm`, `stingers`, `sources` (file -> shared-repo path or `synth:<preset>`) |
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

**SFX.** Each variant is a mix of layers (shared-repo samples and/or synth presets). Each layer is decoded to
44.1 kHz mono (anti-phase-safe downmix), onset-trimmed (-45 dB re peak), optionally cut/filtered/re-pitched,
peak-normalised and offset by its layer gain. The mix gets a 25 Hz high-pass, silence trim (-50 dB head / -60 dB tail),
0.5 ms fade-in, a quadratic fade-out (12 ms default, longer for capped tails) and optional tanh drive. Loudness is the
peak of a 100 ms K-weighted window ("short LUFS"; EBU windows are longer than most SFX). Targets per category, all under
a **-1 dBFS peak ceiling**; all variants of one SoundEffect are set to the same level (the quietest reachable one) so
random round-robin never jumps in volume:

| Category | Target (short LUFS) | Used by |
|---|---|---|
| ui | -21 (BallReturn -20, RewardReveal -19) | UiMove, UiBack, Generic*, BallReturn, RewardReveal |
| ui_accent | -18 (UiSelect -19, TurnStart -17, LowHp -15) | UiSelect, UiPause/Resume, TurnStart, RewardPick, CountdownTick, LowHpWarning |
| bounce | -17 | BallWallBounce (most frequent sound) |
| ball | -15 (CueStrike -13) | CueStrike, BallLaunch |
| magic / pickup / enemy | -14 / -14 / -15 (step -16, attack -13) | status effects, pickups, LevelUp, enemy step/attack/cast |
| hit | -12 (Hard -10, Boss -11, Blocked -13) | ball hits, BossHit, Blocked, CrateBreak — ceiling binds first = peaks at -1 dBFS |
| big | -9 (Crit, deaths, explosions, PlayerHurt -10, PowerShot -10) | punchiest events |
| stinger | -17 LUFS integrated (mono-counted ~ -14 stereo) | StageClear, BossAppear, GameOver, Victory |

Resulting ladder (reachable levels): bounce -17 < soft hit -13.9 < mid hit -12.2 < hard/boss hit -11.9, UI clicks -21.

**BGM.** Source loops are the UGMC "LOOP" edits (musical-length, designed to wrap). Processing is loop-safe only:
decode stereo 44.1 kHz, one **static** gain to -16 LUFS integrated (no dynamic `loudnorm`, which would change the level
across the wrap), -1.5 dBFS sample-peak ceiling (Title is peak-capped at -16.6 LUFS rather than limited; a memoryless
soft knee would be used only if a track were >1 LU short), Ogg Vorbis q5 bit-exact. Verification decodes the OGG:
sample count identical to the source, loudness, peak, and the wrap step (|last-first| sample) compared with the track's
own 99.9th-percentile sample step — all six loops are 0.02-0.40 (below 1 = no click). Stingers get the same treatment as
stereo one-shots at -16 LUFS for `BgmManager.PlayStinger` (which ducks the music to 0.3).

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

### Stingers (SoundEffect WAV mono + `Stinger_<StingerType>.ogg` stereo)

| SoundEffect / StingerType | Source | Length | Why |
|---|---|---|---|
| StageClear / StageClear | UGMC `Short Cues/Winning/Marimba SHORT WIN` | 2.1 s | GDD banner is 1.2 s: shortest bright, immediate (22 ms attack) win cue; cute marimba fits 12 plays per run. Research pick `Quest Finish DRUMS SMALL` is a 6 s drum-only cue. |
| BossAppear / BossAppear | UGMC `Combat/Danger LOOP PARTS/Danger HORN` | 3.0 s | Research pick kept: horn swell peaks at ~0.9 s, i.e. right as the 0.8 s boss push-in ends. |
| GameOver / Defeat | UGMC `Short Cues/Losing/Dark Defeat 2` | 5.2 s | Immediate orchestral hit (57 ms attack) instead of `Dark Defeat 1`, whose main hit only arrives after ~0.8 s of near silence. |
| Victory / Victory | UGMC `Short Cues/Winning/Fanfare WORLD WIN` | 6.4 s | Full fanfare with an instant attack for the run win; `Triumphant Victory` (research) spends 1.3 s building. |

### SFX (`SoundEffect`)

| Key (variants) | Source(s) | Why |
|---|---|---|
| GenericEnter (1), GenericExit (1) | M `SFX/UI/Click/SFX_UI_Click_Open_1` / `_Close_1` | Framework keys (Debug Settings open / Back) were silent; same click family as the starter's Generic clicks. |
| UiMove (2) | M `SFX_UI_Click_Generic_1/2` | Soft 2-3 kHz ticks for remote navigation, quietest category. |
| UiSelect (1) | C `UI/Click/Select/SFX_UI_Button_Click_Select_1` | Brighter "confirm" click that reads over move ticks (variant 2 is a double click, dropped). |
| UiBack (1) | M `SFX_UI_Click_Close_1` | Matches GenericExit (ViewManager Back); `Close_2` is much brighter, dropped for consistency. |
| UiPause (1) / UiResume (1) | synth `ui_pause` / `ui_resume` | Falling / rising B-E square blip; `TIME_WARP_*` were 1.5 s of sub-bass (89 Hz centroid) — inaudible on TV speakers and too slow. UiResume exists in the Foundation enum (not in TDD §5). |
| CountdownTick (2) | C `UI/Countdown/SFX_UI_Countdown_Blow_1/2` | Cut to start 6 ms before the peak (the source's 115 ms swell made the tick late). |
| CueStrike (2) | U `SPORTS/Pool/POOL_Ball_Hit_03/05` + synth `cue_thump` | Real pool clack = billiards identity; a 180 Hz body and 6 dB drive make the main player action land (-14 short LUFS vs -19 raw). |
| BallLaunch (2) | U `8BIT/Jumping/8BIT_RETRO_Jump_Glide_Up_Bright_Quick` + U `WHOOSHES/Classic/WHOOSH_Bright_Short` | Rising 0.15 s chip glide = ball rocketing up the arena, air layer for the magic; `RETRO_Pew_01` falls in pitch and rings 0.5 s. V2 is -1.5 st. |
| BallWallBounce (3) | U `RETRO_LOFI/RETRO_Bump_01/02/04` | 0.09 s low retro bumps (centroid 670-1200 Hz) for the most frequent event; `Bump_03` had a 32 ms attack, `Bump_05` rings 0.44 s. |
| BallHitSoft (3) | U `IMPACTS/Shoot_Em_Up/Short/IMPACT_ShootEmUp_Short_05_RR 01-03` + synth `hit_ping` | The designed round-robin set with the warmest spectrum (~2 kHz; the research's `Short_01` RR is 5-6 kHz and harsh for the most common hit). **Combo:** every hit carries a C6 tonal ping at the same pitch in every variant, so `PlaySoundEffect(effect, pitch: 2^(combo/12))` reads as a rising scale. |
| BallHitMid (3) | U `.../Medium/IMPACT_ShootEmUp_Medium_06_RR 02/03` (+ RR 03 at -1.5 st) + `hit_ping` + `sub_thump` | Heavier body; the research's `Medium_03` RR has 41-61 ms attacks and RR 01 of `Medium_06` ~30-40 ms — replaced. Capped at 0.3 s. |
| BallHitHard (3) | U `RETRO_LOFI/RETRO_Impact_02/05/04` + `hit_ping` + `sub_thump` | Sub-ms attacks, crunchy retro body, extra sub weight; same C6 ping. |
| CritHit (2) | U `RETRO_LOFI/RETRO_Melee_Attack_Kick_Punch_05/08` + synth `crit_shing` + `hit_ping` | Punch + crushed G6 arpeggio sparkle = unmistakably special. |
| Blocked (2) | U `IMPACTS/Metal/IMPACT_Metal_Cling_Bright` + U `RETRO_Bump_01` | Shield Knight "BLOCK" = metallic clang with instant attack (2.5 ms); force-field/shield spells had 130-200 ms swells. |
| BallReturn (2) | C `Collect/Pop/SFX_Player_Collect_Pop_1/2` | Every ball exit plays this — soft quiet pop; `8BIT_RETRO_Effect_Reverse_Zoom` swells for 0.6 s at 8 kHz. |
| PowerShot (1) | U `8BIT/Weapons/8BIT_RETRO_Fire_Blaster_Deep_Glide` + synth `power_boom` | Chip blast plus sub boom for "POWER!". |
| EnemyDeath (2) | U `RETRO_LOFI/RETRO_Destroy_03` (+1.5 st) + synth `enemy_pop` | `Destroy_03` is the low, crunchy member (665 Hz centroid); research's `Destroy_01` sits at 8.9 kHz (harsh for every kill). Pop layer keeps it cute. |
| BossHit (3) | U `.../Medium/IMPACT_ShootEmUp_Medium_10_RR 01-03` + synth `boss_thud` + `hit_ping` | Research RR set kept; added 65 Hz thud for the boss's mass. |
| BossDeath (1) | U `RETRO_LOFI/RETRO_Explosion_Nuclear_01` | Big 3 s retro blast, faded at 3.0 s. |
| EnemyStep (3) | synth `enemy_step` | No clean single-hop thud existed (`THUD_Dark_03` quiet/long, `LOFI_Marching` 2.3 s loop). 110 ms squishy thud at three pitches; SfxManager's 35 ms repeat limit collapses a whole row hop into one step. |
| EnemyAttack (2) | U `RETRO_Melee_Attack_Kick_Punch_02/06` | Whoosh-into-punch matches the lunge animation. |
| EnemyCast (1) | U `MAGIC_SPELLS/MAGIC_SPELL_Dark_Pulse_Echo_Subtle` | Dark, readable telegraph. |
| CrateBreak (1) | U `RETRO_LOFI/LOFI_Break_01` + U `IMPACTS/Wood/IMPACT_Wood_01` | Retro break with a wooden body. |
| Explosion (2) | U `RETRO_LOFI/RETRO_Explode_05` (250 Hz, punchy) + U `8BIT_RETRO_Explosion_Short_Deep` / U `RETRO_Explode_02` | Short (0.45-0.7 s) so Bomber chain reactions don't smear. |
| Freeze (2) | synth `ice_chime` + U `ELEMENTS/Ice/ICE_Cracking_02` / U `SHATTER/SHATTER_Glass_Small_01` | C7-E7-G7 crystal chime + crackle; `ICE_Cracking_04` (research) starts 240 ms late. |
| Burn (2) | U `MAGIC_SPELLS/MAGIC_SPELL_Flame_01/02` | Cut to 70 ms before the peak (sources swell 170-270 ms). |
| Lightning (3) | U `ZAPS/ZAP_Electric_01` + U `ELECTRICITY/ELECTRICITY_Spark_01/02/03` | Sparks alone are 10 kHz, 0.12 s and thin; the zap gives body, the spark varies per RR. |
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
| RewardReveal (3) | U `CARDS/CARDS_Deal_01_RR1/2/3` | Card-deal whooshes, one per card (two sources peak at only -15/-18 dBFS; normalised). |
| RewardPick (1) | C `UI/Bonus/SFX_UI_Bonus_Rich_1` | Rich bonus shimmer, faded at 1.2 s. |
| LevelUp (1) | M `SFX/UI/Claim_Purchase_Upgrade/SFX_UI_Upgrade_1` | Musical upgrade cue, clearly different from the chip pickups; UGMC Level Up cues are 6-21 s. |

## Runtime notes for Gameplay / Presentation / UI

- Combo pitch: `SfxManager.Instance.PlaySoundEffect(SoundEffect.BallHitSoft, Mathf.Pow(2f, Mathf.Min(combo, 12) / 12f))`
  (semitone steps). All hit families share the C6 ping, so mixed Soft/Mid/Hard hits in one combo stay in tune.
- Stingers exist twice: the four stinger `SoundEffect`s (mono WAV, SFX mixer group) and `BgmManager.StingerType`
  clips (stereo OGG, Music group, ducks the BGM). Use **one** path per event — `BgmManager.PlayStinger` is preferred
  (`GameOver` ↔ `Defeat`).
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

## Requests to other owners

- ImportSettingsBuilder (2D art & font): the `Sfx/BilliardRogue` rule (ADPCM, Decompress On Load, mono) is right for
  short clips, but `Victory_1` (6.4 s), `GameOver_1` (5.2 s), `BossDeath_1`, `BossAppear_1` (3.0 s) and `StageClear_1`
  (2.1 s) would be better as Vorbis 0.6 Compressed In Memory; `Bgm/BilliardRogue/Stinger_*.ogg` should be
  Compressed In Memory (not Streaming) so the stinger starts without stream latency.

## Known limits

- Picks were judged by measurement and waveform/spectrogram review, not by listening; an ear pass on TV speakers is
  still recommended (especially Act1 = `Sea Battle`, which replaced the research pick).
- Loudness targets are calibrated relative to each other and to -16 LUFS music; final mix balance (mixer group
  levels) should be checked in game.
