"""Final audio assignments (single source of truth) for Billiard Rogue.

Every `SfxManager.SoundEffect` in TDD §5 (+ the framework's GenericEnter/GenericExit) and every
`BgmManager.BgmType` maps to one or more output files. An SFX variant is a list of layers:
  R(<path under the shared repo's Assets/>, ...)  -> materialized from ~/Documents/music-cell-shared-assets
  S(<synth preset>, ...)                          -> synthesized by synth_presets.py
Each layer is decoded to 44.1 kHz mono, optionally cut/filtered/re-pitched, peak-normalised to the ceiling and then
offset by `gain` dB (so gains are relative to the loudest layer). The mix is trimmed, faded and normalised to the
category loudness target (see TARGETS) under the SFX_PEAK_CEILING_DB peak ceiling. Rationale per pick: README.md.

Mix plan (revision 2, director review): every level leaves headroom for summing in Unity's float mixer.
SFX peak <= -3 dBFS, BGM -18 LUFS / -3 dBFS, and Foundation is asked for a -6 dB master trim (PLANNED_MASTER_TRIM_DB);
build_audio.py sums worst-case event chains over the loudest loops and fails if they would clip after that trim.
"""
from dataclasses import dataclass, field

UGMC = "Ultimate Game Music Collection/"
U = "Universal Sound FX/"
C = "Cute UI _ Interact Sound Effects Pack/AUDIO/"
M = "Merge Games Sound Effects and Music Pack/AUDIO/"
NEX = "NEX/Sound effect/"


@dataclass
class R:
    rel: str
    gain: float = 0.0  # dB relative to the layer's own -1 dBFS peak
    at: float = 0.0  # offset in the mix (s)
    start: float = 0.0  # cut the source (s)
    end: float = 0.0
    max_s: float = 0.0  # cap length with a fade
    lp: float = 0.0  # low-pass Hz
    hp: float = 0.0  # high-pass Hz
    semis: float = 0.0  # tape-style re-pitch
    from_peak_ms: float = -1.0  # >= 0: start this many ms before the loudest point (drops slow swells / pre-noise)


@dataclass
class S:
    preset: str
    gain: float = 0.0
    at: float = 0.0
    p: float = 1.0  # pitch multiplier
    seed: int = 0


@dataclass
class V:
    """One output file (= one variant)."""
    layers: list
    semis: float = 0.0  # re-pitch the whole mix
    max_s: float = 0.0
    fade_ms: float = 12.0
    drive_db: float = 0.0  # tanh saturation pre-gain: denser body at the same -1 dBFS peak


@dataclass
class Sfx:
    category: str
    variants: list
    target: float = None  # overrides the category target
    notes: str = ""
    extra: dict = field(default_factory=dict)


# Headroom plan. SFX peaks stop at -3 dBFS and the music at -18 LUFS / -3 dBFS (2 dB under revision 1 across the board, so
# the SFX/music balance is unchanged). Unity sums everything in float and Android clamps the result to 16-bit, so the
# build also sums worst-case event chains over the loudest loops (HEADROOM_SCENARIOS) and requires the result to stay
# <= 0 dBFS after the master trim that Foundation is asked to add (README "Requests to other owners").
SFX_PEAK_CEILING_DB = -3.0
PLANNED_MASTER_TRIM_DB = -6.0  # music then plays at -24 LUFS, the broadcast TV reference (ATSC A/85)
HEADROOM_MIN_MARGIN_DB = 0.3  # worst case must stay this far under 0 dBFS after the trim (ADPCM / resampler overshoot)

# Short-window (100 ms) peak loudness targets in LUFS for mono clips; the -3 dBFS ceiling binds first for the
# transient-heavy impacts. Per-effect `target=` overrides the category. The combat ladder is asserted by the build
# (LADDER): bounce < soft < mid < hard/boss < big with >= LADDER_MIN_STEP_LU between tiers; the big tier is measured
# on its onset (first 110 ms) because it lands on the same frame as the hit it confirms.
TARGETS = {
    "ui": -23.0,
    "ui_accent": -20.0,
    "bounce": -20.0,
    "ball": -17.0,
    "hit": -17.0,
    "magic": -16.0,
    "enemy": -17.0,
    "pickup": -16.0,
    "big": -14.0,
    "accent": -17.0,
}

SHOOT_SHORT = U + "IMPACTS/Shoot_Em_Up/Short/IMPACT_ShootEmUp_Short_05_RR 0{}_mono.wav"
SHOOT_MED = U + "IMPACTS/Shoot_Em_Up/Medium/IMPACT_ShootEmUp_Medium_{:02d}_RR 0{}_mono.wav"

SFX = {
    # --- framework (ViewManager Back / Debug Settings) ---------------------------------------------------------
    # UI clicks start 2 ms before their transient (from_peak_ms=2): the Merge clicks carry ~22 ms of pre-noise.
    "GenericEnter": Sfx("ui", [V([R(M + "SFX/UI/Click/SFX_UI_Click_Open_1.wav", from_peak_ms=2)])]),
    "GenericExit": Sfx("ui", [V([R(M + "SFX/UI/Click/SFX_UI_Click_Close_1.wav", from_peak_ms=2)])]),
    # --- UI (UiBack reuses GenericExit_1.wav, see SFX_ALIASES) -----------------------------------------------------
    "UiMove": Sfx("ui", [V([R(M + "SFX/UI/Click/SFX_UI_Click_Generic_1.wav", from_peak_ms=2)]),
                         V([R(M + "SFX/UI/Click/SFX_UI_Click_Generic_2.wav", from_peak_ms=2)])]),
    "UiSelect": Sfx("ui_accent", [V([R(C + "UI/Click/Select/SFX_UI_Button_Click_Select_1.wav", from_peak_ms=2)])], target=-21.0),
    "UiPause": Sfx("ui_accent", [V([S("ui_pause")])]),
    "UiResume": Sfx("ui_accent", [V([S("ui_resume")])]),
    # --- cue & ball ----------------------------------------------------------------------------------------------
    "CueStrike": Sfx("ball", [V([R(U + "SPORTS/Pool/POOL_Ball_Hit_03_mono.wav", max_s=0.3), S("cue_thump", gain=-4)], drive_db=6),
                              V([R(U + "SPORTS/Pool/POOL_Ball_Hit_05_mono.wav", max_s=0.3), S("cue_thump", gain=-4, p=0.93)],
                                drive_db=6)],
                     target=-16.0),
    "BallLaunch": Sfx("ball", [V([R(U + "8BIT/Jumping/8BIT_RETRO_Jump_Glide_Up_Bright_Quick_mono.wav"),
                                  R(U + "WHOOSHES/Classic/WHOOSH_Bright_Short_mono.wav", gain=-3)]),
                               V([R(U + "8BIT/Jumping/8BIT_RETRO_Jump_Glide_Up_Bright_Quick_mono.wav"),
                                  R(U + "WHOOSHES/Classic/WHOOSH_Bright_Short_mono.wav", gain=-3)], semis=-1.5)]),
    "BallWallBounce": Sfx("bounce", [V([R(U + f"RETRO_LOFI/RETRO_Bump_0{i}_mono.wav")]) for i in (1, 2, 4)]),
    "BallHitSoft": Sfx("hit", [V([R(SHOOT_SHORT.format(i)), S("hit_ping", gain=-7)], drive_db=3) for i in (1, 2, 3)], target=-18.5),
    "BallHitMid": Sfx("hit", [V([R(SHOOT_MED.format(6, i), max_s=0.3, semis=s), S("hit_ping", gain=-6), S("sub_thump", gain=-10)])
                              for i, s in ((2, 0.0), (3, 0.0), (3, -1.5))]),
    "BallHitHard": Sfx("hit", [V([R(U + f"RETRO_LOFI/RETRO_Impact_0{i}_mono.wav", max_s=0.3), S("hit_ping", gain=-6),
                                  S("sub_thump", gain=-6)], drive_db=3) for i in (2, 5, 4)], target=-15.5),
    # Big tier: tanh drive (as CueStrike) packs more body under the same peak so crits/kills sit >= 1.5 LU over hard hits.
    # Punch_05 has its own partial ~6 cents flat of C6 (1043 Hz): +0.06 st puts it on the ping instead of beating against it.
    "CritHit": Sfx("big", [V([R(U + "RETRO_LOFI/RETRO_Melee_Attack_Kick_Punch_05_mono.wav", max_s=0.4, semis=0.06),
                              S("crit_shing", gain=-4), S("hit_ping", gain=-6)], drive_db=5),
                           V([R(U + "RETRO_LOFI/RETRO_Melee_Attack_Kick_Punch_08_mono.wav", max_s=0.4),
                              S("crit_shing", gain=-4, seed=2), S("hit_ping", gain=-6)], drive_db=5)], target=-13.5),
    "Blocked": Sfx("hit", [V([R(U + "IMPACTS/Metal/IMPACT_Metal_Cling_Bright_mono.wav", max_s=0.45),
                              R(U + "RETRO_LOFI/RETRO_Bump_01_mono.wav", gain=-6)]),
                           V([R(U + "IMPACTS/Metal/IMPACT_Metal_Cling_Bright_mono.wav", max_s=0.45),
                              R(U + "RETRO_LOFI/RETRO_Bump_01_mono.wav", gain=-6)], semis=-2)], target=-17.5),
    "BallReturn": Sfx("ui", [V([R(C + "Collect/Pop/SFX_Player_Collect_Pop_1.wav", max_s=0.35)]),
                             V([R(C + "Collect/Pop/SFX_Player_Collect_Pop_2.wav", max_s=0.35)])], target=-22.0),
    # Frequent optional bonus: level with the bottom of the big tier, never the loudest sound.
    "PowerShot": Sfx("big", [V([R(U + "8BIT/Weapons/8BIT_RETRO_Fire_Blaster_Deep_Glide_mono.wav"), S("power_boom", gain=-2)])],
                     target=-14.5),
    # --- enemies -------------------------------------------------------------------------------------------------
    "EnemyDeath": Sfx("big", [V([R(U + "RETRO_LOFI/RETRO_Destroy_03_mono.wav", max_s=0.5), S("enemy_pop", gain=-5)], drive_db=6),
                              V([R(U + "RETRO_LOFI/RETRO_Destroy_03_mono.wav", max_s=0.5, semis=1.5),
                                 S("enemy_pop", gain=-5, p=1.12, seed=1)], drive_db=6)]),
    # Medium_10 carries a strong partial at 1031.6-1033.4 Hz (22 cents flat of the C6 combo ping): each RR is re-pitched so
    # that partial lands on 1046.5 Hz, and the ping is raised to -4 dB so it leads (checked by PING_FAMILIES).
    "BossHit": Sfx("hit", [V([R(SHOOT_MED.format(10, i), semis=s), S("boss_thud", gain=-4), S("hit_ping", gain=-4)], drive_db=2)
                           for i, s in ((1, 0.228), (2, 0.248), (3, 0.18))], target=-15.5),
    "BossDeath": Sfx("big", [V([R(U + "RETRO_LOFI/RETRO_Explosion_Nuclear_01_mono.wav")], max_s=3.0, fade_ms=800, drive_db=2)],
                     target=-13.5),
    "EnemyStep": Sfx("enemy", [V([S("enemy_step")]), V([S("enemy_step", p=0.9, seed=1)]), V([S("enemy_step", p=1.1, seed=2)])],
                     target=-18.0),
    "EnemyAttack": Sfx("enemy", [V([R(U + "RETRO_LOFI/RETRO_Melee_Attack_Kick_Punch_02_mono.wav")]),
                                 V([R(U + "RETRO_LOFI/RETRO_Melee_Attack_Kick_Punch_06_mono.wav")])], target=-15.0),
    "EnemyCast": Sfx("enemy", [V([R(U + "MAGIC_SPELLS/MAGIC_SPELL_Dark_Pulse_Echo_Subtle_mono.wav")])]),
    "CrateBreak": Sfx("hit", [V([R(U + "RETRO_LOFI/LOFI_Break_01_mono.wav"), R(U + "IMPACTS/Wood/IMPACT_Wood_01_mono.wav", gain=-4)])]),
    # --- ball abilities / status ---------------------------------------------------------------------------------
    # t=0 boom + noise crack under the gated 8-bit crackle, so the loudest part lands on the bomber flash (not ~170 ms later).
    "Explosion": Sfx("big", [V([S("power_boom"), S("blast_crack", gain=-2), R(U + "RETRO_LOFI/RETRO_Explode_05_mono.wav", gain=-4),
                                R(U + "8BIT/Explosions/8BIT_RETRO_Explosion_Short_Deep_mono.wav", gain=-8)], drive_db=6),
                             V([S("power_boom", p=0.9, seed=1), S("blast_crack", gain=-2, seed=1),
                                R(U + "RETRO_LOFI/RETRO_Explode_02_mono.wav", max_s=0.7, gain=-4),
                                R(U + "RETRO_LOFI/RETRO_Explode_05_mono.wav", gain=-7)], drive_db=6)]),
    "Freeze": Sfx("magic", [V([S("ice_chime"), R(U + "ELEMENTS/Ice/ICE_Cracking_02_mono.wav", gain=-3, max_s=0.4)]),
                            V([S("ice_chime", p=1.06, seed=1), R(U + "SHATTER/SHATTER_Glass_Small_01_mono.wav", gain=-8, max_s=0.5)])]),
    # Both variants: instant attack (<= 30 ms to peak) and similar brightness; 200 Hz high-pass (TV speakers).
    "Burn": Sfx("magic", [V([R(U + "MAGIC_SPELLS/MAGIC_SPELL_Flame_02_mono.wav", from_peak_ms=25, hp=200)], drive_db=3),
                          V([R(U + "EXPLOSIONS/Short/EXPLOSION_Short_Ignite_Kickback_Debris_mono.wav", max_s=0.55, hp=200),
                             R(U + "MAGIC_SPELLS/MAGIC_SPELL_Flame_02_mono.wav", from_peak_ms=25, hp=200, semis=-2, gain=-5)],
                            drive_db=3)]),
    "Lightning": Sfx("magic", [V([R(U + "ZAPS/ZAP_Electric_01_mono.wav", max_s=0.45),
                                  R(U + f"ELECTRICITY/ELECTRICITY_Spark_0{i}_mono.wav", gain=-2, from_peak_ms=5)]) for i in (1, 2, 3)]),
    "Poison": Sfx("magic", [V([S("bubbles"), R(C + "Pop/Liquid/SFX_Pop_Liquid_1.wav", gain=-6, lp=5000)]),
                            V([S("bubbles", p=0.9, seed=1), R(C + "Bubble/Natural/SFX_Pop_Bubble_Single_1.wav", gain=-4)])]),
    "Heal": Sfx("magic", [V([R(U + "RETRO_LOFI/RETRO_Powerup_03_mono.wav", max_s=0.6)])], target=-17.0),
    "Split": Sfx("magic", [V([R(C + f"Pop/Mouth/SFX_Pop_Mouth_High_Sharp_{a}.wav"),
                              R(C + f"Pop/Mouth/SFX_Pop_Mouth_High_Sharp_{b}.wav", at=0.045)]) for a, b in ((1, 2), (2, 3), (3, 1))]),
    "Portal": Sfx("magic", [V([R(U + "MAGIC_SPELLS/MAGIC_SPELL_Teleport_mono.wav", from_peak_ms=90, max_s=0.6)])]),
    # --- pickups / player ----------------------------------------------------------------------------------------
    "PickupBall": Sfx("pickup", [V([R(U + "8BIT/Powerups/8BIT_RETRO_Powerup_Spawn_Quick_Climbing_mono.wav")])]),
    "PickupHeal": Sfx("pickup", [V([R(NEX + "Unrealsfx - Magical Game - Magic Potion Pickup.wav")], max_s=1.2, fade_ms=400)]),
    "PickupPower": Sfx("pickup", [V([R(C + "Powerup/SFX_Powerup_Crystal_1.wav")], max_s=0.8, fade_ms=200)]),
    "PlayerHurt": Sfx("big", [V([R(U + "8BIT/Hits_Bumps/8BIT_RETRO_Hit_Bump_Quick_Dark_Drop_mono.wav"),
                                 S("player_hurt", gain=-3, at=0.02)])], target=-12.0),
    "LowHpWarning": Sfx("ui_accent", [V([S("heartbeat")])], target=-17.0),
    # --- flow / rewards ------------------------------------------------------------------------------------------
    "TurnStart": Sfx("ui_accent", [V([S("turn_start")])], target=-19.0),
    # Every card-deal variant starts 15 ms before its slap, so the "land" is at the same time on each staggered card.
    "RewardReveal": Sfx("ui", [V([R(U + f"CARDS/CARDS_Deal_01_RR{i}_mono.wav", from_peak_ms=15)], drive_db=4) for i in (1, 2, 3)],
                        target=-21.0),
    "RewardPick": Sfx("ui_accent", [V([R(C + "UI/Bonus/SFX_UI_Bonus_Rich_1.wav")], max_s=1.2, fade_ms=400)]),
    "LevelUp": Sfx("pickup", [V([R(M + "SFX/UI/Claim_Purchase_Upgrade/SFX_UI_Upgrade_1.wav")])]),
    "CountdownTick": Sfx("ui_accent", [V([R(C + "UI/Countdown/SFX_UI_Countdown_Blow_1.wav", from_peak_ms=6)]),
                                       V([R(C + "UI/Countdown/SFX_UI_Countdown_Blow_2.wav", from_peak_ms=6)])]),
    # --- flow accents (TDD §5 SoundEffects). Short SFX that layer ON TOP of the music stingers (STINGERS below, Music
    # group): flow code fires both together, e.g. PlaySoundEffect(StageClear) + BgmManager.PlayStinger(StageClear).
    # Tonal accents are transposed into the stinger's key (chroma match): Marimba SHORT WIN = A major, Dark Defeat 2 = E minor.
    "StageClear": Sfx("accent", [V([R(U + "RETRO_LOFI/RETRO_Bonus_03_mono.wav", semis=-5)], fade_ms=60)]),
    # Rumble under the 0.8 s boss push-in, then a deep impact + gong ring at 0.78 s (lands with the horn's swell peak).
    "BossAppear": Sfx("accent", [V([R(U + "RETRO_LOFI/LOFI_Rumble_01_mono.wav", max_s=0.85, gain=-6, lp=3000),
                                    S("boss_thud", at=0.78), R(U + "IMPACTS/Stone/IMPACT_Stone_Deep_mono.wav", gain=-3, at=0.78),
                                    R(U + "IMPACTS/Metal/IMPACT_Metal_Cling_Deep_mono.wav", semis=-5, max_s=0.5, gain=-9, at=0.78)],
                                   max_s=1.25, fade_ms=250)], target=-16.0),
    "GameOver": Sfx("accent", [V([R(U + "MUSIC_EFFECTS/Solo_Chip_Square/MUSIC_EFFECT_Solo_Chip_Square_Negative_03_stereo.wav",
                                    semis=4, lp=6000)], fade_ms=120)], target=-18.0),
    "Victory": Sfx("accent", [V([R(C + "Confetti/SFX_Confetti_Explosion_1.wav"),
                                 R("Epic Toon FX/Sound/etfx_explosion_sparkle2.wav", gain=-4, at=0.03)], max_s=1.0, fade_ms=250)]),
}
# Keys that reuse another key's clip asset. SfxManager dedups by AudioClip instance per frame, so ViewManager's Back
# (GenericExit) and a view's own UiBack on the same press collapse into one click instead of summing +6 dB in phase.
SFX_ALIASES = {"UiBack": "GenericExit"}

# Order of TDD §5 (+ UiResume, which the Foundation enum added); the Unity builder sorts by enum value anyway.
SFX_ORDER = ["GenericEnter", "GenericExit", "UiMove", "UiSelect", "UiBack", "UiPause", "UiResume", "CueStrike", "BallLaunch",
             "BallWallBounce", "BallHitSoft", "BallHitMid", "BallHitHard", "CritHit", "Blocked", "EnemyDeath", "BossHit",
             "BossDeath", "Explosion", "Freeze", "Burn", "Lightning", "Poison", "Heal", "Split", "Portal", "PickupBall",
             "PickupHeal", "PickupPower", "EnemyStep", "EnemyAttack", "EnemyCast", "PlayerHurt", "LowHpWarning",
             "BallReturn", "TurnStart", "RewardReveal", "RewardPick", "LevelUp", "CountdownTick", "StageClear",
             "BossAppear", "GameOver", "Victory", "CrateBreak", "PowerShot"]
assert sorted(SFX_ORDER) == sorted([*SFX, *SFX_ALIASES]), set(SFX_ORDER) ^ set(SFX) ^ set(SFX_ALIASES)
assert all(target in SFX for target in SFX_ALIASES.values())

# --- build checks (build_audio.check) -------------------------------------------------------------------------------
# Combat loudness ladder, quietest first; each tier's quietest file must sit >= LADDER_MIN_STEP_LU over the loudest file
# of the tier below. "big" is measured on its onset (the kill/crit confirm lands on the hit's frame).
LADDER = [("bounce", ["BallWallBounce"]), ("soft", ["BallHitSoft"]), ("mid", ["BallHitMid"]), ("hard", ["BallHitHard", "BossHit"]),
          ("big", ["CritHit", "EnemyDeath", "Explosion", "BossDeath"])]
LADDER_MIN_STEP_LU = 1.5
NOT_LOUDER_THAN_BIG = ["PowerShot"]  # frequent bonus: must not top the big tier
# SFX tied to an instant VFX: -3 dB of the peak within 30 ms, and the first 110 ms within 1 LU of the loudest window.
INSTANT = ["CueStrike", "BallWallBounce", "BallHitSoft", "BallHitMid", "BallHitHard", "CritHit", "Blocked", "EnemyDeath", "BossHit",
           "BossDeath", "CrateBreak", "Explosion", "Burn", "Lightning", "Freeze", "Poison", "Split", "PlayerHurt", "PowerShot"]
INSTANT_MAX_MS, INSTANT_ONSET_LU = 30.0, 1.0
UI_CLICKS = ["GenericEnter", "GenericExit", "UiMove", "UiSelect"]  # -3 dB of the peak within 5 ms
UI_CLICK_MAX_MS = 5.0
# Hit families that carry the C6 combo ping: the strongest partial in 1.0-1.1 kHz must be 1046.5 Hz +/- 5 cents.
PING_FAMILIES = ["BallHitSoft", "BallHitMid", "BallHitHard", "CritHit", "BossHit"]
PING_HZ, PING_TOLERANCE_CENTS = 1046.5, 5.0
# Worst-case sums (Unity 2D sources, unity gain, default sliders): (label, loops to sum over, [(sfx file, t s)], stinger
# ogg or None, music gain while it plays). SfxManager allows these: different effects, or the same effect >= 35 ms apart.
BOMBER_CHAIN = [("BallHitHard_2.wav", 0.0), ("Explosion_1.wav", 0.0), ("EnemyDeath_2.wav", 0.0), ("Explosion_2.wav", 0.05),
                ("EnemyDeath_1.wav", 0.05), ("CritHit_1.wav", 0.08)]
HEADROOM_SCENARIOS = [
    ("single hard hit", ["Boss", "Act3"], [("BallHitHard_2.wav", 0.0)], None, 1.0),
    ("bomber chain", ["Boss", "Act3"], BOMBER_CHAIN, None, 1.0),
    ("boss kill chain", ["Boss"], [("BossHit_2.wav", 0.0), ("BossDeath_1.wav", 0.0), ("CritHit_2.wav", 0.02),
                                   ("Explosion_1.wav", 0.04)], None, 1.0),
    ("victory accent + stinger", ["Boss"], [("Victory_1.wav", 0.0)], "Stinger_Victory.ogg", 0.3),
    ("boss appear accent + stinger", ["Act3"], [("BossAppear_1.wav", 0.0)], "Stinger_BossAppear.ogg", 0.3),
]


@dataclass
class Stinger:
    """BgmManager.StingerType clip: a stereo music cue (Music group, ducks the BGM). The music lives only here."""
    rel: str
    fade_ms: float = 300.0


STINGERS = {
    "StageClear": Stinger(UGMC + "Short Cues/Winning/Marimba SHORT WIN.wav"),
    "BossAppear": Stinger(UGMC + "Combat/Danger LOOP PARTS/Danger HORN.wav"),
    "Victory": Stinger(UGMC + "Short Cues/Winning/Fanfare WORLD WIN.wav", fade_ms=600),
    "Defeat": Stinger(UGMC + "Short Cues/Losing/Dark Defeat 2.wav", fade_ms=600),
}


@dataclass
class Bgm:
    rel: str
    notes: str = ""


BGM_TARGET_LUFS = -18.0
BGM_PEAK_CEILING_DB = -3.0
BGM = {
    "Title": Bgm(UGMC + "Fantasy Orchestral/Upbeat City LOOP.wav"),
    "Act1": Bgm(UGMC + "Combat/Sea Battle LOOP.wav"),
    "Act2": Bgm(UGMC + "Combat/Enemies LOOP.wav"),
    "Act3": Bgm(UGMC + "Combat/Frantic Battle LOOP.wav"),
    "Boss": Bgm(UGMC + "Combat/Boss Battle 1 Loop.wav"),
    "Reward": Bgm(UGMC + "Short Cues/Item Stores/Item Store 1/Item Store 1 LOOP.wav"),
}
# Existing starter key `BgmType.Main` has no own track; it reuses the Title loop so it never plays silence.
BGM_ALIASES = {"Main": "Title"}
