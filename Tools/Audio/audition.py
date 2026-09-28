"""Audition helper: fetch candidate files per slot from the shared repo and print/plot their analysis.

    Tools/.venv/bin/python Tools/Audio/audition.py --out <scratch dir> [--slot Title,Act1] [--sheet]

Not part of the build; it documents how the final picks in `picks.py` were compared (duration, loudness,
spectral centroid, attack, loop wrap). Writes `audition.json` (+ `audition_<slot>.png` with --sheet).
"""
import argparse
import json
import os

import audiolib as al
import shared_repo
import sheet

UGMC = "Ultimate Game Music Collection/"
U = "Universal Sound FX/"
C = "Cute UI _ Interact Sound Effects Pack/AUDIO/"
M = "Merge Games Sound Effects and Music Pack/AUDIO/"
CG = "Cute_Game_Sounds/WAV/"
NEX = "NEX/Sound effect/"

BGM_CANDIDATES = {
    "Title": [UGMC + "Fantasy Orchestral/Upbeat City LOOP.wav", UGMC + "Locations/Tavern LOOP LIVELY.wav",
              UGMC + "Platform/Soaring LOOP NO SOLO.wav", UGMC + "Titles/Casual/Casual Title GUITAR LOOP.wav",
              UGMC + "Platform/Forest LOOP.wav"],
    "Act1": [UGMC + "Combat/Skeletons LOOP.wav", UGMC + "Platform/Platform Action LOOP.wav",
             UGMC + "Platform/Jungle LOOP.wav", UGMC + "Combat/Tribal Chase LOOP.wav", UGMC + "Combat/Close Combat LOOP.wav",
             UGMC + "Combat/Enemy Territory LOOP.wav", UGMC + "Combat/Desperate Battle LOOP.wav",
             UGMC + "Locations/Remote Island - Main Loop.wav", UGMC + "Combat/Sea Battle LOOP.wav", UGMC + "Combat/Danger LOOP.wav"],
    "Act2": [UGMC + "Combat/Dark Dungeon ACTION LOOP.wav", UGMC + "Combat/Enemies LOOP.wav",
             UGMC + "Seasonal/Halloween/Undead LOOP WITHOUT INTRO.wav", UGMC + "Combat/Tense Combat LOOP.wav",
             UGMC + "Dungeons/Dangerous Dungeon LOOP.wav", UGMC + "Dungeons/Murky Dungeon LOOP.wav",
             UGMC + "Seasonal/Halloween/Halloween LOOP.wav", UGMC + "Combat/Wasteland Combat Loop.wav",
             UGMC + "Locations/Moonlit Forest - Main Loop.wav"],
    "Act3": [UGMC + "Combat/Epic Combat LOOP.wav", UGMC + "Combat/Frantic Battle LOOP.wav",
             UGMC + "Combat/Circle of Death LOOP.wav", UGMC + "Combat/Barren Boss LOOP.wav",
             UGMC + "Titles/Cinematic/Planet Title V2 Loop.wav"],
    "Boss": [UGMC + "Combat/Boss Battle 1 Loop.wav", UGMC + "Combat/Boss Battle 3 Loop.wav",
             UGMC + "Combat/Boss Battle 4 Loop.wav", UGMC + "Combat/Boss LOOP.wav", UGMC + "Combat/Boss Battle 2 Loop.wav",
             UGMC + "Combat/Boss Battle 5 Loop.wav"],
    "Reward": [UGMC + "Short Cues/Item Stores/Item Store 1/Item Store 1 LOOP.wav",
               UGMC + "Short Cues/Item Stores/Item Store 2/Item Store 2 LOOP.wav",
               UGMC + "Puzzles/Calm Bright LOOP.wav", UGMC + "Dungeons/Pretty Dungeon LOOP.wav",
               UGMC + "Puzzles/Harp and Pizz LOOP.wav", UGMC + "Puzzles/Laid Back LOOP.wav",
               UGMC + "Puzzles/Lighthearted LOOP SHORT.wav"],
}

SFX_CANDIDATES = {
    "Victory": [UGMC + "Short Cues/Winning/Triumphant Victory.wav", UGMC + "Short Cues/Winning/Fanfare Win.wav",
                UGMC + "Short Cues/Winning/Fanfare WORLD WIN.wav",
                U + "MUSIC_EFFECTS/Solo_Chip_Square/MUSIC_EFFECT_Solo_Chip_Square_Positive_01_stereo.wav"],
    "GameOver": [UGMC + "Short Cues/Losing/Dark Defeat 1.wav", UGMC + "Short Cues/Losing/Dark Defeat 2.wav",
                 UGMC + "Short Cues/Losing/Dramatic Defeat SHORT.wav", UGMC + "Short Cues/Losing/Casual Lose 2.wav",
                 U + "MUSIC_EFFECTS/Solo_Chip_Square/MUSIC_EFFECT_Solo_Chip_Square_Negative_01_stereo.wav"],
    "StageClear": [UGMC + "Short Cues/Quest SFX/Quest Finish DRUMS SMALL.wav", UGMC + "Short Cues/Winning/Casual Win 1.wav",
                   UGMC + "Short Cues/Winning/Marimba SHORT WIN.wav", UGMC + "Short Cues/Winning/Forest WIN.wav",
                   M + "STINGER_SUCCESS/Stinger/STGR_Success_1.wav", U + "RETRO_LOFI/RETRO_Bonus_01_mono.wav"],
    "BossAppear": [UGMC + "Combat/Danger LOOP PARTS/Danger HORN.wav", UGMC + "Short Cues/Scares/Scare DRUMS BIG.wav",
                   UGMC + "Short Cues/Scares/Deep Drum SCARE.wav", UGMC + "Short Cues/Scares/Drum and Cymbal SCARE.wav"],
    "LevelUp": [UGMC + "Short Cues/Level Up/Level Up ETHNIC DRUMS.wav", UGMC + "Short Cues/Level Up/Level Up ORCHESTRA.wav",
                U + "RETRO_LOFI/RETRO_Powerup_05_mono.wav", M + "SFX/UI/Claim_Purchase_Upgrade/SFX_UI_Upgrade_1.wav"],
    "LowHpWarning": [UGMC + "Short Cues/Sound Effects/Heartbeat SINGLE 1.wav", UGMC + "Short Cues/Sound Effects/Heartbeat SINGLE 2.wav",
                     UGMC + "Short Cues/Sound Effects/Heartbeat SINGLE 3.wav", U + "HUMAN/Heartbeat/Heartbeat_Pulse_01_mono.wav",
                     U + "HUMAN/Heartbeat/Heartbeat_Pulse_02_mono.wav"],
    "TurnStart": [U + "USER_INTERFACES/Beeps/UI_Beep_Double_Clean_Up_stereo.wav", UGMC + "Short Cues/Quest SFX/Snare Hits/Quest Snare 1.wav",
                  UGMC + "Short Cues/Quest SFX/Bongo Hits/Quest Bongos 1.wav", U + "RETRO_LOFI/RETRO_Bonus_03_mono.wav"],
    "CueStrike": [U + f"SPORTS/Pool/POOL_Ball_Hit_{i:02d}_mono.wav" for i in range(1, 12)] + [U + "RETRO_LOFI/RETRO_Punch_03_mono.wav"],
    "BallLaunch": [U + "RETRO_LOFI/RETRO_Pew_01_mono.wav", U + "8BIT/Jumping/8BIT_RETRO_Jump_Glide_Up_Bright_Quick_mono.wav",
                   U + "8BIT/Weapons/8BIT_RETRO_Fire_Blaster_Short_Glide_mono.wav", U + "WHOOSHES/Classic/WHOOSH_Bright_Short_mono.wav",
                   U + "MAGIC_SPELLS/MAGIC_SPELL_Short_Fast_Burst_Quick_Fade_mono.wav"],
    "BallWallBounce": [U + f"RETRO_LOFI/RETRO_Bump_0{i}_mono.wav" for i in range(1, 6)] +
                      [U + "8BIT/Hits_Bumps/8BIT_RETRO_Hit_Bump_Distorted_Tap_Bright_mono.wav", U + "8BIT/Hits_Bumps/8BIT_RETRO_Hit_Bump_Thud_mono.wav"],
    "BallHitSoft": [U + f"IMPACTS/Shoot_Em_Up/Short/IMPACT_ShootEmUp_Short_01_RR 0{i}_mono.wav" for i in range(1, 4)] +
                   [U + f"IMPACTS/Shoot_Em_Up/Short/IMPACT_ShootEmUp_Short_0{j}_RR 01_mono.wav" for j in range(2, 8)],
    "BallHitMid": [U + f"IMPACTS/Shoot_Em_Up/Medium/IMPACT_ShootEmUp_Medium_03_RR 0{i}_mono.wav" for i in range(1, 4)] +
                  [U + f"IMPACTS/Shoot_Em_Up/Medium/IMPACT_ShootEmUp_Medium_0{j}_RR 01_mono.wav" for j in (1, 2, 4, 5, 6)],
    "BallHitHard": [U + f"RETRO_LOFI/RETRO_Impact_0{i}_mono.wav" for i in range(1, 7)],
    "BossHit": [U + f"IMPACTS/Shoot_Em_Up/Medium/IMPACT_ShootEmUp_Medium_10_RR 0{i}_mono.wav" for i in range(1, 4)],
    "CritHit": [U + "RETRO_LOFI/RETRO_Melee_Attack_Kick_Punch_05_mono.wav", U + "8BIT/Hits_Bumps/8BIT_RETRO_Hit_Bump_Deep_Zap_mono.wav",
                UGMC + "Short Cues/Scales/Combo Hit 1.wav"],
    "EnemyDeath": [U + "RETRO_LOFI/RETRO_Destroy_01_mono.wav", U + "RETRO_LOFI/RETRO_Destroy_02_mono.wav",
                   U + "8BIT/Explosions/8BIT_RETRO_Explosion_Zap_Destroy_mono.wav", U + "RETRO_LOFI/LOFI_Destroy_01_mono.wav"],
    "BossDeath": [U + "RETRO_LOFI/RETRO_Explosion_Nuclear_01_mono.wav", U + "8BIT/Explosions/8BIT_RETRO_Explosion_Long_Sweep_Down_mono.wav"],
    "Explosion": [U + "8BIT/Explosions/8BIT_RETRO_Explosion_Short_Deep_mono.wav", U + "RETRO_LOFI/RETRO_Explode_02_mono.wav",
                  U + "8BIT/Explosions/8BIT_RETRO_Explosion_Short_Distorted_1_mono.wav"],
    "Freeze": [U + "ELEMENTS/Ice/ICE_Cracking_04_mono.wav", U + "SHATTER/SHATTER_Glass_Small_01_mono.wav",
               U + "ELEMENTS/Ice/ICE_Cracking_02_mono.wav", U + "BREAKS_SNAPS/BREAK_Ice_Crack_01_mono.wav"],
    "Burn": [U + "MAGIC_SPELLS/MAGIC_SPELL_Flame_01_mono.wav", U + "MAGIC_SPELLS/MAGIC_SPELL_Flame_02_mono.wav"],
    "Lightning": [U + f"ELECTRICITY/ELECTRICITY_Spark_0{i}_mono.wav" for i in range(1, 4)] + [U + "ZAPS/ZAP_Electric_01_mono.wav"],
    "Poison": [U + "MAGIC_SPELLS/MAGIC_SPELL_Deep_Tone_Bubbling_Zaps_Subtle_mono.wav", C + "Pop/Liquid/SFX_Pop_Liquid_1.wav",
               C + "Bubble/Natural/SFX_Pop_Bubble_Single_1.wav"],
    "Heal": [C + "Powerup/SFX_Powerup_Potion_1.wav", U + "RETRO_LOFI/RETRO_Powerup_03_mono.wav", NEX + "Unrealsfx - Magical Game - Magic Potion Pickup.wav"],
    "Blocked": [U + "FORCE_FIELDS/FORCE_FIELD_Scifi_Pulse_01_mono.wav", U + "MAGIC_SPELLS/MAGIC_SPELL_Shield_mono.wav",
                U + "IMPACTS/Metal/IMPACT_Metal_Cling_Bright_mono.wav"],
    "Split": [C + f"Pop/Mouth/SFX_Pop_Mouth_High_Sharp_{i}.wav" for i in range(1, 4)],
    "Portal": [U + "SPACE_WARPS/SPACE_WARP_Quick_Charge_Jump_Away_01_mono.wav", U + "MAGIC_SPELLS/MAGIC_SPELL_Teleport_mono.wav"],
    "PickupBall": [U + "8BIT/Powerups/8BIT_RETRO_Powerup_Spawn_Quick_Climbing_mono.wav", U + "RETRO_LOFI/RETRO_Powerup_01_mono.wav"],
    "PickupPower": [U + "8BIT/Powerups/8BIT_RETRO_Powerup_Spawn_Flash_Aggressive_mono.wav"],
    "PowerShot": [U + "8BIT/Weapons/8BIT_RETRO_Fire_Blaster_Deep_Glide_mono.wav"],
    "EnemyStep": [U + "THUDS_THUMPS/THUD_Dark_03_Short_mono.wav", U + "RETRO_LOFI/LOFI_Marching_01_mono.wav",
                  U + "THUDS_THUMPS/THUD_Squishy_01_mono.wav", U + "THUDS_THUMPS/THUD_Subtle_Tap_mono.wav"],
    "EnemyAttack": [U + "RETRO_LOFI/RETRO_Melee_Attack_Kick_Punch_02_mono.wav", U + "RETRO_LOFI/RETRO_Punch_01_mono.wav"],
    "EnemyCast": [U + "MAGIC_SPELLS/MAGIC_SPELL_Dark_Pulse_Echo_Subtle_mono.wav", U + "RETRO_LOFI/RETRO_Spell_01_mono.wav"],
    "CrateBreak": [U + "RETRO_LOFI/LOFI_Break_01_mono.wav", U + "BREAKS_SNAPS/BREAK_Crunch_01_mono.wav"],
    "PlayerHurt": [U + "8BIT/Hits_Bumps/8BIT_RETRO_Hit_Bump_Quick_Dark_Drop_mono.wav", U + "ANIMALS/ANIMAL_Cat_Meow_02_mono.wav"],
    "BallReturn": [U + "8BIT/Various/8BIT_RETRO_Effect_Reverse_Zoom_mono.wav", C + "Collect/Pop/SFX_Player_Collect_Pop_1.wav"],
    "RewardReveal": [U + f"CARDS/CARDS_Deal_01_RR{i}_mono.wav" for i in range(1, 4)],
    "RewardPick": [C + "UI/Bonus/SFX_UI_Bonus_Rich_1.wav", M + "SFX/UI/Claim_Purchase_Upgrade/SFX_UI_Claim_1.wav"],
    "UiMove": [U + "8BIT/Beeps/8BIT_RETRO_Beep_1_Very_Short_mono.wav", M + "SFX/UI/Click/SFX_UI_Click_Generic_1.wav"],
    "UiSelect": [U + "RETRO_LOFI/RETRO_Click_01_mono.wav", C + "UI/Click/Select/SFX_UI_Button_Click_Select_1.wav"],
    "UiBack": [U + "USER_INTERFACES/Beeps/UI_Beep_Double_Clean_Down_stereo.wav", M + "SFX/UI/Click/SFX_UI_Click_Close_1.wav"],
    "UiPause": [U + "TIME_WARPS/TIME_WARP_Stop_01_mono.wav", U + "TIME_WARPS/TIME_WARP_Start_01_mono.wav"],
    "CountdownTick": [U + "8BIT/Beeps/8BIT_RETRO_Beep_Short_Bright_mono.wav", C + "UI/Countdown/SFX_UI_Countdown_Blow_1.wav"],
    "Generic": [M + "SFX/UI/Click/SFX_UI_Click_Open_1.wav", M + "SFX/UI/Click/SFX_UI_Click_Close_1.wav"],
    # --- revision 2: short accents that layer on top of the music stingers (the music lives only in Stinger_*.ogg) ---
    "AccentStageClear": [U + f"RETRO_LOFI/RETRO_Bonus_0{i}_mono.wav" for i in range(1, 9)] +
                        [U + "8BIT/Coin_Collect/8BIT_RETRO_Coin_Collect_Two_Note_Bright_Twinkle_mono.wav",
                         U + "MUSIC_EFFECTS/Solo_Chip_Square/MUSIC_EFFECT_Solo_Chip_Square_Positive_01_stereo.wav"],
    "AccentBossAppear": [U + "IMPACTS/Metal/IMPACT_Metal_Cling_Deep_mono.wav", U + "IMPACTS/Stone/IMPACT_Stone_Deep_mono.wav",
                         CG + "Cute_Game_Musical_SFX_Percussion_Gong_Time_Up_01.wav",
                         CG + "Cute_Game_Musical_SFX_Percussion_Gong_Time_Up_02.wav",
                         UGMC + "Short Cues/Scares/Deep Drum SCARE.wav", U + "RETRO_LOFI/LOFI_Rumble_01_mono.wav",
                         U + "EXPLOSIONS/Long/EXPLOSION_Long_Distant_Impact_Rumble_mono.wav",
                         U + "MAGIC_SPELLS/MAGIC_SPELL_Slow_In_Muffled_Boom_mono.wav", U + "RETRO_LOFI/RETRO_Thump_01_mono.wav",
                         U + "RETRO_LOFI/RETRO_Thump_03_mono.wav"],
    "AccentGameOver": [U + f"MUSIC_EFFECTS/Solo_Chip_Square/MUSIC_EFFECT_Solo_Chip_Square_Negative_0{i}_stereo.wav" for i in range(1, 7)] +
                      [U + "RETRO_LOFI/RETRO_Death_01_mono.wav", U + "RETRO_LOFI/RETRO_Death_02_mono.wav"],
    "AccentVictory": [C + "Confetti/SFX_Confetti_Explosion_1.wav", C + "Confetti/SFX_Confetti_Explosion_2.wav",
                      C + "Confetti/SFX_Confetti_Explosion_Bright_1.wav", C + "Confetti/SFX_Confetti_Explosion_Bright_2.wav",
                      C + "Firework/SFX_Firework_Explosion_1.wav", "Epic Toon FX/Sound/etfx_explosion_sparkle2.wav",
                      "Epic Toon FX/Sound/etfx_explosion_sparkle3.wav"],
    "Burn2": [U + "MAGIC_SPELLS/MAGIC_SPELL_Flame_03_mono.wav", U + "MAGIC_SPELLS/MAGIC_SPELL_Flame_04_mono.wav",
              U + "MAGIC_SPELLS/MAGIC_SPELL_Flame_Mechanical_01_mono.wav",
              U + "EXPLOSIONS/Short/EXPLOSION_Short_Ignite_Kickback_Debris_mono.wav", "Epic Toon FX/Sound/etfx_explosion_fireball.wav",
              "Epic Toon FX/Sound/etfx_shoot_fireball.wav", U + "ELEMENTS/Fire/FIRE_Campfire_Active_01_loop_mono.wav"],
    "ExplosionTransient": [U + "EXPLOSIONS/Short/EXPLOSION_Short_Kickback_Crackle_mono.wav",
                           U + "EXPLOSIONS/Short/EXPLOSION_Short_Smooth_Crackle_mono.wav", U + "RETRO_LOFI/LOFI_Bang_01_mono.wav",
                           U + "RETRO_LOFI/LOFI_Bang_03_mono.wav", U + "RETRO_LOFI/RETRO_Bang_01_mono.wav"],
    "Round2": [U + f"IMPACTS/Shoot_Em_Up/Short/IMPACT_ShootEmUp_Short_05_RR 0{i}_mono.wav" for i in (2, 3)] +
              [U + f"IMPACTS/Shoot_Em_Up/Short/IMPACT_ShootEmUp_Short_04_RR 0{i}_mono.wav" for i in (2, 3)] +
              [U + f"IMPACTS/Shoot_Em_Up/Medium/IMPACT_ShootEmUp_Medium_06_RR 0{i}_mono.wav" for i in (2, 3)] +
              [U + f"IMPACTS/Shoot_Em_Up/Medium/IMPACT_ShootEmUp_Medium_01_RR 0{i}_mono.wav" for i in (2, 3)] +
              [U + f"ANIMALS/ANIMAL_Cat_Meow_0{a}_mono.wav" for a in ("1_RR01", "1_RR02", "3_RR01", "3_RR02", "4_RR01", "4_RR02")] +
              [C + f"Powerup/SFX_Powerup_{n}_1.wav" for n in ("Bright", "Crystal", "Rich")] +
              [C + f"Collect/Pop/SFX_Player_Collect_Pop_{i}.wav" for i in (2, 3)] +
              [M + f"SFX/UI/Click/SFX_UI_Click_Generic_{i}.wav" for i in (2, 3)] +
              [M + "SFX/UI/Click/SFX_UI_Click_Open_2.wav", M + "SFX/UI/Click/SFX_UI_Click_Close_2.wav",
               C + "UI/Click/Select/SFX_UI_Button_Click_Select_2.wav"] +
              [U + f"RETRO_LOFI/RETRO_Melee_Attack_Kick_Punch_{i:02d}_mono.wav" for i in (4, 6, 8)] +
              [U + "RETRO_LOFI/RETRO_Destroy_03_mono.wav", U + "RETRO_LOFI/LOFI_Destroy_02_mono.wav",
               U + "RETRO_LOFI/RETRO_Explode_05_mono.wav", U + "IMPACTS/Wood/IMPACT_Wood_01_mono.wav"],
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--cache", default=os.path.join(os.path.dirname(__file__), "..", "Staging", ".cache", "audio_src"))
    ap.add_argument("--slot", default="")
    ap.add_argument("--jobs", type=int, default=10)
    ap.add_argument("--sheet", action="store_true", help="also render waveform/spectrogram PNGs per slot")
    a = ap.parse_args()
    slots = {**{k: (v, True) for k, v in BGM_CANDIDATES.items()}, **{k: (v, False) for k, v in SFX_CANDIDATES.items()}}
    wanted = set(filter(None, a.slot.split(",")))
    if wanted:
        slots = {k: v for k, v in slots.items() if k in wanted}
    rels = [r for files, _ in slots.values() for r in files]
    paths = shared_repo.fetch_all(rels, os.path.abspath(a.cache), a.jobs)
    report = {}
    os.makedirs(a.out, exist_ok=True)
    for slot, (files, is_bgm) in slots.items():
        rows, images = [], []
        for rel in files:
            x = al.decode(paths[rel], 2 if is_bgm else 1)
            row = {"file": rel, **al.analyze(x), "probe": shared_repo.probe(paths[rel])}
            if is_bgm:
                row["loop"] = al.loop_report(x)
                row["music"] = al.music_features(x)
            rows.append(row)
            sub = f"{row['dur_s']}s pk {row['peak_db']} sL {row['short_lufs']} I {row.get('int_lufs', 0)} cen {row['centroid_hz']}"
            if is_bgm:
                sub += f" | {row['music']} | wrap x{row['loop']['wrap_step_ratio']}"
            images.append({"label": os.path.basename(rel), "sub": sub, "samples": x, "category": "impact"})
        report[slot] = rows
        if a.sheet and is_bgm:
            sheet.bgm_sheet(images, os.path.join(a.out, f"audition_{slot}.png"), f"BGM candidates: {slot}")
        elif a.sheet:
            sheet.sfx_sheet(images, os.path.join(a.out, f"audition_{slot}.png"), f"SFX candidates: {slot}", cols=4, max_s=2.0)
    with open(os.path.join(a.out, "audition.json"), "w") as fh:
        json.dump(report, fh, indent=1)
    for slot, rows in report.items():
        print(f"== {slot}")
        for r in rows:
            extra = r.get("loop", {})
            print(f"  {r['dur_s']:7.2f}s pk {r['peak_db']:6.1f} sL {r['short_lufs']:6.1f} I {r.get('int_lufs', 0):6.1f} "
                  f"cen {r['centroid_hz']:5d} att {r['attack_ms']:5.1f} {r['probe'].get('sample_rate')}/{r['probe'].get('channels')} "
                  f"{os.path.basename(r['file'])} {extra}")


if __name__ == "__main__":
    main()
