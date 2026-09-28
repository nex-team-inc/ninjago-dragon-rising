"""Final audio assignments (single source of truth) for Billiard Rogue.

Every `SfxManager.SoundEffect` in TDD §5 (+ the framework's GenericEnter/GenericExit) and every
`BgmManager.BgmType` maps to one or more output files. An SFX variant is a list of layers:
  R(<path under the shared repo's Assets/>, ...)  -> materialized from ~/Documents/music-cell-shared-assets
  S(<synth preset>, ...)                          -> synthesized by synth_presets.py
Each layer is decoded to 44.1 kHz mono, optionally cut/filtered/re-pitched, peak-normalised to -1 dBFS and then
offset by `gain` dB (so gains are relative to the loudest layer). The mix is trimmed, faded and normalised to the
category loudness target (see TARGETS) under a -1 dBFS peak ceiling. Rationale per pick: README.md.
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
    from_peak_ms: float = -1.0  # >= 0: start this many ms before the loudest point (drops slow swells)


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


# Short-window (100 ms) peak loudness targets in LUFS for mono clips; every clip is also capped at -1 dBFS peak,
# so for transient-heavy impacts the ceiling binds first (= "punchier": peak at -1 dBFS).
# Stingers use integrated loudness instead (mono-counted -17 LUFS ~ -14 LUFS stereo, ~2 LU over the -16 LUFS BGM).
TARGETS = {
    "ui": -21.0,
    "ui_accent": -18.0,
    "bounce": -17.0,
    "ball": -15.0,
    "hit": -12.0,
    "magic": -14.0,
    "enemy": -15.0,
    "pickup": -14.0,
    "big": -9.0,
    "stinger": -17.0,
}

SHOOT_SHORT = U + "IMPACTS/Shoot_Em_Up/Short/IMPACT_ShootEmUp_Short_05_RR 0{}_mono.wav"
SHOOT_MED = U + "IMPACTS/Shoot_Em_Up/Medium/IMPACT_ShootEmUp_Medium_{:02d}_RR 0{}_mono.wav"

SFX = {
    # --- framework (ViewManager Back / Debug Settings) ---------------------------------------------------------
    "GenericEnter": Sfx("ui", [V([R(M + "SFX/UI/Click/SFX_UI_Click_Open_1.wav")])]),
    "GenericExit": Sfx("ui", [V([R(M + "SFX/UI/Click/SFX_UI_Click_Close_1.wav")])]),
    # --- UI ------------------------------------------------------------------------------------------------------
    "UiMove": Sfx("ui", [V([R(M + "SFX/UI/Click/SFX_UI_Click_Generic_1.wav")]),
                         V([R(M + "SFX/UI/Click/SFX_UI_Click_Generic_2.wav")])]),
    "UiSelect": Sfx("ui_accent", [V([R(C + "UI/Click/Select/SFX_UI_Button_Click_Select_1.wav")])], target=-19.0),
    "UiBack": Sfx("ui", [V([R(M + "SFX/UI/Click/SFX_UI_Click_Close_1.wav")])]),
    "UiPause": Sfx("ui_accent", [V([S("ui_pause")])]),
    "UiResume": Sfx("ui_accent", [V([S("ui_resume")])]),
    # --- cue & ball ----------------------------------------------------------------------------------------------
    "CueStrike": Sfx("ball", [V([R(U + "SPORTS/Pool/POOL_Ball_Hit_03_mono.wav", max_s=0.3), S("cue_thump", gain=-4)], drive_db=6),
                              V([R(U + "SPORTS/Pool/POOL_Ball_Hit_05_mono.wav", max_s=0.3), S("cue_thump", gain=-4, p=0.93)],
                                drive_db=6)],
                     target=-13.0),
    "BallLaunch": Sfx("ball", [V([R(U + "8BIT/Jumping/8BIT_RETRO_Jump_Glide_Up_Bright_Quick_mono.wav"),
                                  R(U + "WHOOSHES/Classic/WHOOSH_Bright_Short_mono.wav", gain=-3)]),
                               V([R(U + "8BIT/Jumping/8BIT_RETRO_Jump_Glide_Up_Bright_Quick_mono.wav"),
                                  R(U + "WHOOSHES/Classic/WHOOSH_Bright_Short_mono.wav", gain=-3)], semis=-1.5)]),
    "BallWallBounce": Sfx("bounce", [V([R(U + f"RETRO_LOFI/RETRO_Bump_0{i}_mono.wav")]) for i in (1, 2, 4)]),
    "BallHitSoft": Sfx("hit", [V([R(SHOOT_SHORT.format(i)), S("hit_ping", gain=-7)], drive_db=3) for i in (1, 2, 3)]),
    "BallHitMid": Sfx("hit", [V([R(SHOOT_MED.format(6, i), max_s=0.3, semis=s), S("hit_ping", gain=-6), S("sub_thump", gain=-10)])
                              for i, s in ((2, 0.0), (3, 0.0), (3, -1.5))]),
    "BallHitHard": Sfx("hit", [V([R(U + f"RETRO_LOFI/RETRO_Impact_0{i}_mono.wav", max_s=0.3), S("hit_ping", gain=-6),
                                  S("sub_thump", gain=-6)], drive_db=3) for i in (2, 5, 4)], target=-10.0),
    "CritHit": Sfx("big", [V([R(U + "RETRO_LOFI/RETRO_Melee_Attack_Kick_Punch_05_mono.wav", max_s=0.4),
                              S("crit_shing", gain=-4), S("hit_ping", gain=-6)]),
                           V([R(U + "RETRO_LOFI/RETRO_Melee_Attack_Kick_Punch_08_mono.wav", max_s=0.4),
                              S("crit_shing", gain=-4, seed=2), S("hit_ping", gain=-6)])]),
    "Blocked": Sfx("hit", [V([R(U + "IMPACTS/Metal/IMPACT_Metal_Cling_Bright_mono.wav", max_s=0.45),
                              R(U + "RETRO_LOFI/RETRO_Bump_01_mono.wav", gain=-6)]),
                           V([R(U + "IMPACTS/Metal/IMPACT_Metal_Cling_Bright_mono.wav", max_s=0.45),
                              R(U + "RETRO_LOFI/RETRO_Bump_01_mono.wav", gain=-6)], semis=-2)], target=-13.0),
    "BallReturn": Sfx("ui", [V([R(C + "Collect/Pop/SFX_Player_Collect_Pop_1.wav", max_s=0.35)]),
                             V([R(C + "Collect/Pop/SFX_Player_Collect_Pop_2.wav", max_s=0.35)])], target=-20.0),
    "PowerShot": Sfx("big", [V([R(U + "8BIT/Weapons/8BIT_RETRO_Fire_Blaster_Deep_Glide_mono.wav"), S("power_boom", gain=-2)])],
                     target=-10.0),
    # --- enemies -------------------------------------------------------------------------------------------------
    "EnemyDeath": Sfx("big", [V([R(U + "RETRO_LOFI/RETRO_Destroy_03_mono.wav", max_s=0.5), S("enemy_pop", gain=-5)]),
                              V([R(U + "RETRO_LOFI/RETRO_Destroy_03_mono.wav", max_s=0.5, semis=1.5),
                                 S("enemy_pop", gain=-5, p=1.12, seed=1)])], target=-11.0),
    "BossHit": Sfx("hit", [V([R(SHOOT_MED.format(10, i)), S("boss_thud", gain=-4), S("hit_ping", gain=-8)], drive_db=2)
                           for i in (1, 2, 3)], target=-11.0),
    "BossDeath": Sfx("big", [V([R(U + "RETRO_LOFI/RETRO_Explosion_Nuclear_01_mono.wav")], max_s=3.0, fade_ms=800)]),
    "EnemyStep": Sfx("enemy", [V([S("enemy_step")]), V([S("enemy_step", p=0.9, seed=1)]), V([S("enemy_step", p=1.1, seed=2)])],
                     target=-16.0),
    "EnemyAttack": Sfx("enemy", [V([R(U + "RETRO_LOFI/RETRO_Melee_Attack_Kick_Punch_02_mono.wav")]),
                                 V([R(U + "RETRO_LOFI/RETRO_Melee_Attack_Kick_Punch_06_mono.wav")])], target=-13.0),
    "EnemyCast": Sfx("enemy", [V([R(U + "MAGIC_SPELLS/MAGIC_SPELL_Dark_Pulse_Echo_Subtle_mono.wav")])]),
    "CrateBreak": Sfx("hit", [V([R(U + "RETRO_LOFI/LOFI_Break_01_mono.wav"), R(U + "IMPACTS/Wood/IMPACT_Wood_01_mono.wav", gain=-4)])]),
    # --- ball abilities / status ---------------------------------------------------------------------------------
    "Explosion": Sfx("big", [V([R(U + "RETRO_LOFI/RETRO_Explode_05_mono.wav"),
                                R(U + "8BIT/Explosions/8BIT_RETRO_Explosion_Short_Deep_mono.wav", gain=-5)]),
                             V([R(U + "RETRO_LOFI/RETRO_Explode_02_mono.wav", max_s=0.7),
                                R(U + "RETRO_LOFI/RETRO_Explode_05_mono.wav", gain=-3)])]),
    "Freeze": Sfx("magic", [V([S("ice_chime"), R(U + "ELEMENTS/Ice/ICE_Cracking_02_mono.wav", gain=-3, max_s=0.4)]),
                            V([S("ice_chime", p=1.06, seed=1), R(U + "SHATTER/SHATTER_Glass_Small_01_mono.wav", gain=-8, max_s=0.5)])]),
    "Burn": Sfx("magic", [V([R(U + "MAGIC_SPELLS/MAGIC_SPELL_Flame_02_mono.wav", from_peak_ms=70)]),
                          V([R(U + "MAGIC_SPELLS/MAGIC_SPELL_Flame_01_mono.wav", from_peak_ms=70)])]),
    "Lightning": Sfx("magic", [V([R(U + "ZAPS/ZAP_Electric_01_mono.wav", max_s=0.45),
                                  R(U + f"ELECTRICITY/ELECTRICITY_Spark_0{i}_mono.wav", gain=-2)]) for i in (1, 2, 3)]),
    "Poison": Sfx("magic", [V([S("bubbles"), R(C + "Pop/Liquid/SFX_Pop_Liquid_1.wav", gain=-6, lp=5000)]),
                            V([S("bubbles", p=0.9, seed=1), R(C + "Bubble/Natural/SFX_Pop_Bubble_Single_1.wav", gain=-4)])]),
    "Heal": Sfx("magic", [V([R(U + "RETRO_LOFI/RETRO_Powerup_03_mono.wav", max_s=0.6)])], target=-15.0),
    "Split": Sfx("magic", [V([R(C + f"Pop/Mouth/SFX_Pop_Mouth_High_Sharp_{a}.wav"),
                              R(C + f"Pop/Mouth/SFX_Pop_Mouth_High_Sharp_{b}.wav", at=0.045)]) for a, b in ((1, 2), (2, 3), (3, 1))]),
    "Portal": Sfx("magic", [V([R(U + "MAGIC_SPELLS/MAGIC_SPELL_Teleport_mono.wav", from_peak_ms=90, max_s=0.6)])]),
    # --- pickups / player ----------------------------------------------------------------------------------------
    "PickupBall": Sfx("pickup", [V([R(U + "8BIT/Powerups/8BIT_RETRO_Powerup_Spawn_Quick_Climbing_mono.wav")])]),
    "PickupHeal": Sfx("pickup", [V([R(NEX + "Unrealsfx - Magical Game - Magic Potion Pickup.wav")], max_s=1.2, fade_ms=400)]),
    "PickupPower": Sfx("pickup", [V([R(C + "Powerup/SFX_Powerup_Crystal_1.wav")], max_s=0.8, fade_ms=200)]),
    "PlayerHurt": Sfx("big", [V([R(U + "8BIT/Hits_Bumps/8BIT_RETRO_Hit_Bump_Quick_Dark_Drop_mono.wav"),
                                 S("player_hurt", gain=-3, at=0.02)])], target=-10.0),
    "LowHpWarning": Sfx("ui_accent", [V([S("heartbeat")])], target=-15.0),
    # --- flow / rewards ------------------------------------------------------------------------------------------
    "TurnStart": Sfx("ui_accent", [V([S("turn_start")])], target=-17.0),
    "RewardReveal": Sfx("ui", [V([R(U + f"CARDS/CARDS_Deal_01_RR{i}_mono.wav")]) for i in (1, 2, 3)], target=-19.0),
    "RewardPick": Sfx("ui_accent", [V([R(C + "UI/Bonus/SFX_UI_Bonus_Rich_1.wav")], max_s=1.2, fade_ms=400)]),
    "LevelUp": Sfx("pickup", [V([R(M + "SFX/UI/Claim_Purchase_Upgrade/SFX_UI_Upgrade_1.wav")])]),
    "CountdownTick": Sfx("ui_accent", [V([R(C + "UI/Countdown/SFX_UI_Countdown_Blow_1.wav", from_peak_ms=6)]),
                                       V([R(C + "UI/Countdown/SFX_UI_Countdown_Blow_2.wav", from_peak_ms=6)])]),
    # --- stingers (SoundEffects per TDD §5; also exported as stereo OGG next to the BGM) ----------------------------
    "StageClear": Sfx("stinger", [V([R(UGMC + "Short Cues/Winning/Marimba SHORT WIN.wav")], fade_ms=300)]),
    "BossAppear": Sfx("stinger", [V([R(UGMC + "Combat/Danger LOOP PARTS/Danger HORN.wav")], fade_ms=300)]),
    "GameOver": Sfx("stinger", [V([R(UGMC + "Short Cues/Losing/Dark Defeat 2.wav")], fade_ms=600)]),
    "Victory": Sfx("stinger", [V([R(UGMC + "Short Cues/Winning/Fanfare WORLD WIN.wav")], fade_ms=600)]),
}

# Order of TDD §5 (+ UiResume, which the Foundation enum added); the Unity builder sorts by enum value anyway.
SFX_ORDER = ["GenericEnter", "GenericExit", "UiMove", "UiSelect", "UiBack", "UiPause", "UiResume", "CueStrike", "BallLaunch",
             "BallWallBounce", "BallHitSoft", "BallHitMid", "BallHitHard", "CritHit", "Blocked", "EnemyDeath", "BossHit",
             "BossDeath", "Explosion", "Freeze", "Burn", "Lightning", "Poison", "Heal", "Split", "Portal", "PickupBall",
             "PickupHeal", "PickupPower", "EnemyStep", "EnemyAttack", "EnemyCast", "PlayerHurt", "LowHpWarning",
             "BallReturn", "TurnStart", "RewardReveal", "RewardPick", "LevelUp", "CountdownTick", "StageClear",
             "BossAppear", "GameOver", "Victory", "CrateBreak", "PowerShot"]
assert sorted(SFX_ORDER) == sorted(SFX), set(SFX_ORDER) ^ set(SFX)

# BgmManager.StingerType -> SoundEffect whose source is re-rendered as a stereo OGG for BgmManager.PlayStinger.
STINGERS = {"StageClear": "StageClear", "BossAppear": "BossAppear", "Victory": "Victory", "Defeat": "GameOver"}


@dataclass
class Bgm:
    rel: str
    notes: str = ""


BGM_TARGET_LUFS = -16.0
BGM_PEAK_CEILING_DB = -1.5
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
