"""Writes Tools/Blender/environment/layouts.json: arena kit assembly + per-act diorama dressing, lights, fake
god-ray volumes and ambient particle emitters.

    python3 Tools/Blender/environment/make_layouts.py [--out PATH]

Pure Python (no Blender), deterministic (fixed seeds). Coordinates are the TDD §14.1 arena frame in Unity metres:
x right, y up, z north (up the arena); the arena spans x in [-3.5, 3.5], z in [0, 11.6] (launch zone z in [0, 1.6],
grid z in [1.6, 11.6], launch line z = 0.55). rotY is Unity degrees about +Y (clockwise seen from above; +Z turns
toward +X). Pieces face +Z, so props that should face the camera (south, looking north) use rotY ~ 180.

Entry kinds inside act["props"]:
  * FBX pieces      {"piece": "Env_*", x, y, z, rotY, scale[, light, tag]}
  * god-ray volume  {"piece": "LightShaft", "mesh": "Env_LightShaft", x, y, z (TOP of the shaft), direction,
                     euler, size [top width, length, top width], color, intensity}
  * particles       {"piece": "AmbientParticles", "type": <Particles sprite name>, x, y, z (box centre), size, ...}
"""
import argparse
import json
import math
import os
import random

HERE = os.path.dirname(os.path.abspath(__file__))

FLOOR_Y = 0.0
GROUND_Y = -0.2
ARENA = {"xMin": -3.5, "xMax": 3.5, "zLaunch": 0.0, "launchZoneHeight": 1.6, "zGridMin": 1.6, "zTop": 11.6,
         "columns": 7, "rows": 10, "cellSize": 1.0, "launchLineZ": 0.55, "floorY": FLOOR_Y, "groundY": GROUND_Y,
         "wallThickness": 0.4, "wallHeight": 0.52}
# ArenaConfig default pose: cameraPosition (0, 20.4, 12.7) relative to the arena centre (sim (3.5, 5.8)), pitch 58,
# FOV 28 (vertical). In this frame the camera sits at (0, 20.4, -6.9) and looks at the arena centre (0, 0, 5.8).
CAMERA = {"target": [0.0, 0.0, 5.8], "pitchDeg": 58.0, "fovDeg": 28.0, "distance": 24.05, "aspect": 16 / 9,
          "note": "ArenaConfig default pose expressed in this frame; the game camera pose lives in ArenaConfig"}
# keep scenery out of this rectangle (arena + walls + torches + a little air)
KEEP_OUT = (-4.3, 4.3, -1.05, 12.35)

# default real-time light per emissive piece (piece-local offset). Only props flagged "light": true get one.
PIECE_LIGHTS = {
    "Env_WallTorch": {"offset": [0.0, 0.52, 0.26], "color": [1.0, 0.62, 0.3], "intensity": 1.6, "range": 4.5},
    "Env_WallTorch_Arcane": {"offset": [0.0, 0.52, 0.26], "color": [0.35, 0.85, 1.0], "intensity": 1.6, "range": 4.5},
    "Env_StoneLantern": {"offset": [0.0, 0.93, 0.0], "color": [1.0, 0.72, 0.38], "intensity": 1.4, "range": 4.0},
    "Env_Brazier": {"offset": [0.0, 1.45, 0.0], "color": [1.0, 0.55, 0.25], "intensity": 2.4, "range": 6.5},
    "Env_Candles": {"offset": [0.0, 0.62, 0.0], "color": [1.0, 0.66, 0.35], "intensity": 0.9, "range": 2.6},
    "Env_Crystal_A": {"offset": [0.0, 1.2, 0.0], "color": [0.35, 0.9, 1.0], "intensity": 2.2, "range": 6.5},
    "Env_Crystal_B": {"offset": [0.0, 0.8, 0.0], "color": [0.78, 0.42, 1.0], "intensity": 1.8, "range": 5.0},
    "Env_Crystal_C": {"offset": [0.0, 0.4, 0.0], "color": [0.4, 0.9, 1.0], "intensity": 1.0, "range": 3.0},
    "Env_GlowMushroom": {"offset": [0.0, 0.62, 0.0], "color": [0.3, 1.0, 0.9], "intensity": 1.1, "range": 3.2},
    "Env_RuneStone": {"offset": [0.0, 0.95, 0.4], "color": [0.35, 0.9, 1.0], "intensity": 1.0, "range": 3.0},
}
MAX_LIGHTS = 8
# rough ground footprint radius per piece, for keeping scatter off big props
RADIUS = {"Env_Tree_A": 0.7, "Env_Tree_B": 0.7, "Env_Bush": 0.85, "Env_RuinColumn": 0.5, "Env_RuinColumn_Broken": 0.9,
          "Env_RuinArch": 1.7, "Env_MossyRock_A": 0.9, "Env_MossyRock_B": 0.6, "Env_StoneLantern": 0.45, "Env_Fence": 1.0,
          "Env_CryptWall": 1.0, "Env_Tombstone_A": 0.55, "Env_Tombstone_B": 0.55, "Env_Candles": 0.3, "Env_Brazier": 0.5,
          "Env_Coffin": 1.1, "Env_BonePile": 0.55, "Env_IronGate": 1.7, "Env_Crystal_A": 0.85, "Env_Crystal_B": 0.65,
          "Env_Crystal_C": 0.4, "Env_Stalagmite": 0.8, "Env_CaveRock_A": 1.2, "Env_CaveRock_B": 0.9,
          "Env_GlowMushroom": 0.5, "Env_RuneStone": 0.65, "Env_GroundMound": 0.0, "Env_Pebbles": 0.4,
          "Env_GrassTuft": 0.3, "Env_Flowers": 0.35, "Env_Banner": 0.0, "Env_GroundPatch": 0.0, "Env_FloorTile": 0.0,
          "Env_WaterPool": 1.3, "Env_WaterPool_Glow": 1.3, "Env_PavingPatch": 0.0, "Env_Stairs": 1.1, "Env_Log": 0.9, "Env_Stump": 0.45, "Env_Mushrooms": 0.25,
          "Env_Urn": 0.3, "Env_CryptPillar": 0.45, "Env_CaveFloorTile": 0.0, "LightShaft": 0.0, "AmbientParticles": 0.0}
# rough height per piece (m, scale 1) for the frame-visibility pruning; default 1
HEIGHT = {"Env_Tree_A": 4.3, "Env_Tree_B": 4.6, "Env_RuinArch": 4.2, "Env_RuinColumn": 2.8, "Env_RuinColumn_Broken": 1.7,
          "Env_CryptWall": 2.8, "Env_IronGate": 3.9, "Env_CryptPillar": 2.9, "Env_Stalagmite": 2.5, "Env_Crystal_A": 2.0,
          "Env_CaveRock_A": 1.4, "Env_StoneLantern": 1.55, "Env_Brazier": 1.7, "Env_RuneStone": 1.75, "Env_Banner": 0.2,
          "Env_Bush": 1.2}
# canopy / overhang radius when larger than the footprint radius
REACH = {"Env_Tree_A": 1.9, "Env_Tree_B": 1.5, "Env_RuinArch": 1.8, "Env_CryptWall": 1.1, "Env_CaveRock_A": 1.4,
         "Env_GroundMound": 2.5, "Env_GroundPatch": 1.6, "Env_PavingPatch": 1.9, "Env_WaterPool": 1.5}
FRAME_MARGIN = 1.06   # keep props that peek into an 6% overscan (camera shake / sub-texel snapping margin)
PARTICLE_SPRITES = "Assets/Sprites/BilliardRogue/Particles/<type>.png"


def r3(v):
    return [round(c, 3) for c in v]


def P(piece, x, z, rot=0.0, s=1.0, y=GROUND_Y, light=False, tag=None):
    e = {"piece": piece, "x": round(x, 3), "y": round(y, 3), "z": round(z, 3), "rotY": round(rot % 360, 1),
         "scale": round(s, 3)}
    if light:
        e["light"] = True
    if tag:
        e["tag"] = tag
    return e


# ---------------------------------------------------------------- effects
def norm(v):
    n = math.sqrt(sum(c * c for c in v))
    return [c / n for c in v]


def euler_for_down(d):
    """Unity Euler (x, y, 0) that turns local -Y onto direction d (Quaternion.Euler order: y after x)."""
    dx, dy, dz = norm(d)
    ex = math.degrees(math.acos(max(-1.0, min(1.0, -dy))))
    ey = math.degrees(math.atan2(-dx, -dz)) if abs(dy) < 0.9999 else 0.0
    return [round(ex, 2), round(ey % 360, 2), 0.0]


def shaft(land, direction, length, width, color, intensity, sink=0.6):
    """God-ray volume that lands at `land` (x, z on the ground) coming along `direction`. The bottom 35% of the
    volume fades out, so it is sunk `sink` m below the ground to put the brightest part just above the landing."""
    d = norm(direction)
    lx, lz = land
    bottom = (lx + d[0] * sink / -d[1], GROUND_Y - sink, lz + d[2] * sink / -d[1])
    top = [bottom[i] - d[i] * length for i in range(3)]
    return {"piece": "LightShaft", "mesh": "Env_LightShaft", "x": round(top[0], 3), "y": round(top[1], 3),
            "z": round(top[2], 3), "direction": r3(d), "euler": euler_for_down(d),
            "size": [round(width, 3), round(length, 3), round(width, 3)], "color": r3(color),
            "intensity": round(intensity, 3)}


def particles(ptype, centre, size, max_particles, rate, lifetime, velocity, start_size, colors, glow, noise=0.3,
              light_influence=None, tag=None):
    """Ambient emitter: box shape (centre, size), world-space simulation, loops forever, prewarmed."""
    e = {"piece": "AmbientParticles", "type": ptype, "sprite": PARTICLE_SPRITES.replace("<type>", ptype),
         "x": round(centre[0], 3), "y": round(centre[1], 3), "z": round(centre[2], 3), "size": r3(size),
         "maxParticles": max_particles, "rate": rate, "lifetime": list(lifetime), "velocity": r3(velocity),
         "noise": noise, "startSize": list(start_size), "colorA": r3(colors[0]), "colorB": r3(colors[-1]),
         "shader": "BilliardRogue/GlowParticle" if glow else "BilliardRogue/LitParticle", "prewarm": True}
    if light_influence is not None:
        e["lightInfluence"] = light_influence
    if tag:
        e["tag"] = tag
    return e


def lighting(sun_dir, sun_color, sun_intensity, sky, equator, ground_c, fog_color, fog_density, rim, add_tint,
             god_color, god_intensity, particle_tint, grade, mood):
    """Preview lighting + the same values in ActLightingPreset field names (Configs/ActLightingPreset.cs)."""
    d = norm(sun_dir)
    euler = [round(math.degrees(math.asin(-d[1])), 1), round(math.degrees(math.atan2(d[0], d[2])), 1), 0.0]
    return {
        "mood": mood,
        "sun": {"direction": r3(d), "euler": euler, "color": r3(sun_color), "intensity": sun_intensity},
        "ambient": {"mode": "Trilight", "sky": r3(sky), "equator": r3(equator), "ground": r3(ground_c)},
        "fog": {"color": r3(fog_color), "density": fog_density},
        "grade": grade,
        "preset": {"sunColor": r3(sun_color), "sunIntensity": sun_intensity, "sunEuler": euler,
                   "ambientSky": r3(sky), "ambientEquator": r3(equator), "ambientGround": r3(ground_c),
                   "fogColor": r3(fog_color), "fogDensity": fog_density, "rimColor": r3(rim),
                   "additionalLightTint": r3(add_tint), "godRayColor": r3(god_color), "godRayIntensity": god_intensity,
                   "particleTint": r3(particle_tint)},
    }


# ---------------------------------------------------------------- arena kit (shared by all acts)
def kit():
    out = []
    for row in range(10):
        z = ARENA["zGridMin"] + (9 - row) + 0.5
        for col in range(7):
            out.append(P("Env_FloorTile_Danger" if row == 9 else "Env_FloorTile", col - 3.0, z, 0, 1, FLOOR_Y,
                         tag=f"cell {col},{row}"))
    out.append(P("Env_LaunchPad", 0.0, 0.8, 0, 1, FLOOR_Y))
    for k in range(12):
        z = 11.1 - k
        out.append(P("Env_WallSegment", -3.5, z, 90, 1, FLOOR_Y, tag="wall left"))
        out.append(P("Env_WallSegment", 3.5, z, 270, 1, FLOOR_Y, tag="wall right"))
    for c in range(7):
        out.append(P("Env_WallSegment", c - 3.0, 11.6, 180, 1, FLOOR_Y, tag="wall top"))
    for sx in (-1, 1):
        out.append(P("Env_WallCorner", sx * 3.72, 11.82, 0, 1, FLOOR_Y, tag="corner top"))
        out.append(P("Env_WallCorner", sx * 3.72, -0.62, 0, 1, FLOOR_Y, tag="post bottom"))
    return out


def torches(piece, zs, light_zs=()):
    """Sconces on the OUTER faces of the side walls (flames peek over the rim; nothing enters the play field)."""
    out = []
    for z in zs:
        for sx in (-1, 1):
            out.append(P(piece, sx * 3.9, z, 270 if sx < 0 else 90, 1, 0.28, light=z in light_zs))
    return out


# ---------------------------------------------------------------- scatter
def blocked(x, z, r, props):
    if KEEP_OUT[0] - r < x < KEEP_OUT[1] + r and KEEP_OUT[2] - r < z < KEEP_OUT[3] + r:
        return True
    for p in props:
        rr = RADIUS.get(p["piece"], 0.5) * p.get("scale", 1.0) if isinstance(p.get("scale", 1.0), (int, float)) else 0
        if rr > 0 and (p["x"] - x) ** 2 + (p["z"] - z) ** 2 < (rr + r) ** 2:
            return True
    return False


def scatter(props, rng, piece, count, regions, r, s=(0.8, 1.2), rot=(0, 360), tries=600):
    """Rejection-sample `count` pieces inside regions [(x0, x1, z0, z1), ...] away from the arena and props."""
    out = []
    for _ in range(tries):
        if len(out) >= count:
            break
        x0, x1, z0, z1 = rng.choice(regions)
        x, z = rng.uniform(x0, x1), rng.uniform(z0, z1)
        if blocked(x, z, r, props + out):
            continue
        out.append(P(piece, x, z, rng.uniform(*rot), rng.uniform(*s)))
    return out


def cluster(props, rng, piece, cx, cz, count, spread, r, s=(0.8, 1.15)):
    out = []
    for _ in range(count * 30):
        if len(out) >= count:
            break
        a, d = rng.uniform(0, 2 * math.pi), spread * math.sqrt(rng.random())
        x, z = cx + math.cos(a) * d, cz + math.sin(a) * d
        if blocked(x, z, r, props + out):
            continue
        out.append(P(piece, x, z, rng.uniform(0, 360), rng.uniform(*s)))
    return out


def dress(props, rng, piece, centres, per, spread, r, s=(0.8, 1.15)):
    out = []
    for cx, cz in centres:
        out += cluster(props + out, rng, piece, cx, cz, per, spread, r, s)
    return out


def ground(surface, piece="Env_GroundTile"):
    """Ground tiles [x, z, rotY] covering the frame. surface=None -> a palette tile (rotated to hide repeats)."""
    tiles = [{"x": x, "z": z, "rotY": 0 if surface else (x * 7 + z * 3) // 4 % 4 * 90} for x in range(-22, 23, 4)
             for z in range(-10, 27, 4)]
    return {"piece": piece, "y": GROUND_Y, "tileSize": 4.0, "surface": surface or "", "tiles": tiles}


def paving(rng, x0, x1, z0, z1, keep=0.65, y=GROUND_Y + 0.03, hole=None):
    """Ruined temple floor: kit floor tiles on a 1 m grid with gaps, jitter and small twists."""
    out = []
    x = x0
    while x <= x1 + 1e-6:
        z = z0
        while z <= z1 + 1e-6:
            edge = min(x - x0, x1 - x, z - z0, z1 - z)
            k = keep * (0.55 if edge < 0.9 else 1.0)       # ragged border
            if rng.random() < k and not (hole and hole(x, z)):
                out.append(P("Env_FloorTile", x + rng.uniform(-0.05, 0.05), z + rng.uniform(-0.05, 0.05),
                             rng.uniform(-6, 6), 1.0, y + rng.uniform(-0.03, 0.0)))
            z += 1.03
        x += 1.03
    return out


def path(points, s=0.8, step=1.0):
    """Continuous chain of overlapping ground patches along a polyline (dirt paths, rubble trails)."""
    out, pts = [], []
    for (x0, z0), (x1, z1) in zip(points, points[1:]):
        n = max(1, int(math.hypot(x1 - x0, z1 - z0) / step))
        pts += [(x0 + (x1 - x0) * i / n, z0 + (z1 - z0) * i / n) for i in range(n)]
    pts.append(points[-1])
    for i, (x, z) in enumerate(pts):
        out.append(P("Env_GroundPatch", x, z, i * 47.0, s * (0.9 + 0.1 * ((i * 7) % 3))))
    return out


# ---------------------------------------------------------------- act 1: mossy ruins (golden hour)
SUN1 = (0.62, -0.72, -0.1)


def act1():
    rng = random.Random(101)
    props = torches("Env_WallTorch", (2.6, 8.6))
    # ---- left: forest edge (tall trees frame the edge) + lantern shrine clearing on a dirt path
    props += path([(-5.6, -0.6), (-5.9, 1.4), (-6.3, 3.3), (-6.9, 5.2), (-7.4, 6.9), (-7.5, 8.3)], 0.66, step=0.95)
    props += [P("Env_GroundPatch", -7.6, 9.1, 20, 1.2)]
    props += [
        P("Env_Tree_A", -11.4, 12.6, 30, 1.3), P("Env_Tree_B", -9.3, 14.2, 0, 1.1), P("Env_Tree_A", -12.4, 8.4, 200, 1.25),
        P("Env_Tree_B", -10.3, 5.9, 60, 1.15), P("Env_Tree_A", -12.1, 3.0, 120, 1.2), P("Env_Tree_B", -9.9, 0.3, 20, 1.0),
        P("Env_Tree_A", -8.0, -1.9, 80, 0.95), P("Env_Tree_B", -13.2, 11.0, 150, 1.2),
        P("Env_StoneLantern", -6.3, 9.3, 180, 1.0, light=True), P("Env_StoneLantern", -8.8, 9.5, 180, 1.0, light=True),
        P("Env_RuinColumn_Broken", -7.6, 11.1, 210, 0.85), P("Env_MossyRock_A", -9.6, 5.2, 60, 1.0),
        P("Env_MossyRock_B", -5.2, 4.3, 110, 0.75), P("Env_MossyRock_B", -8.9, 7.0, 20, 0.9),
        P("Env_Stump", -6.0, 6.2, 30, 1.0), P("Env_Log", -8.2, 0.9, 70, 1.0),
        P("Env_Bush", -4.95, 11.4, 20, 0.85), P("Env_Bush", -5.0, 7.4, 140, 0.7), P("Env_Bush", -5.05, 1.9, 80, 0.8),
        P("Env_Bush", -9.4, 10.2, 200, 0.9), P("Env_Bush", -6.8, -1.2, 10, 1.0), P("Env_Bush", -10.4, 7.6, 90, 0.95),
        P("Env_WaterPool", -8.5, 3.0, 15, 0.85),
        P("Env_GroundMound", -11.0, 7.0, 20, 1.3), P("Env_GroundMound", -10.8, 1.6, -30, 1.0),
        P("Env_Mushrooms", -8.2, 2.6, 0, 1.0), P("Env_Mushrooms", -6.9, 10.4, 90, 0.9),
        P("Env_Mushrooms", -10.1, 9.1, 40, 1.0),
    ]
    # ---- right: temple terrace - colonnade on ruined paving, stairs up to an arch, fence at the lower right
    props += [P("Env_PavingPatch", 6.3, z, rot, 1.0) for z, rot in ((1.3, 80), (3.6, 95), (6.0, 85), (8.4, 100),
                                                                      (10.6, 90))]
    props += [P("Env_PavingPatch", 8.9, 5.2, 10, 0.9), P("Env_PavingPatch", 8.9, 9.6, 170, 0.95),
              P("Env_FloorTile", 7.6, 2.4, 12, 1.0, GROUND_Y + 0.02), P("Env_FloorTile", 4.9, 11.9, -8, 1.0, GROUND_Y + 0.02)]
    props += [
        P("Env_RuinColumn_Broken", 5.4, 1.5, 40, 0.95), P("Env_RuinColumn", 5.4, 4.4, 0, 1.0),
        P("Env_RuinColumn", 5.4, 7.3, 10, 1.0), P("Env_RuinColumn_Broken", 5.4, 10.2, 160, 1.0),
        P("Env_RuinColumn_Broken", 8.4, 4.0, 250, 0.9),
        # stairs (risers toward the camera) up to a landing that carries the arch gateway
        P("Env_Stairs", 8.9, 7.3, 180, 1.0), P("Env_RuinArch", 8.9, 7.6, 180, 1.0, y=GROUND_Y + 0.42), P("Env_MossyRock_A", 10.7, 2.6, 250, 0.9),
        P("Env_MossyRock_B", 7.2, 0.4, 20, 0.8), P("Env_Log", 10.0, 5.0, 160, 0.9),
        P("Env_Tree_A", 11.4, 11.9, 300, 1.25), P("Env_Tree_B", 11.6, 8.3, 45, 1.2), P("Env_Tree_A", 11.3, 3.6, 80, 1.15),
        P("Env_Tree_A", 13.0, 6.2, 20, 1.3),
        P("Env_Tree_B", 10.4, -0.9, 20, 1.0), P("Env_Tree_B", 10.2, 14.2, 110, 1.05),
        P("Env_Bush", 10.2, 9.8, 70, 0.95), P("Env_Bush", 7.4, 12.5, 30, 0.9), P("Env_Bush", 5.2, -0.8, 200, 0.8),
        P("Env_Bush", 10.4, 5.9, 250, 0.8),
        P("Env_StoneLantern", 5.1, 12.6, 180, 1.0, light=True), P("Env_Stump", 9.5, 1.3, 0, 0.9),
        P("Env_Fence", 6.4, -0.35, 8, 1.0), P("Env_Fence", 8.4, -0.6, -5, 1.0),
        P("Env_GroundMound", 11.3, 9.4, 10, 1.1), P("Env_Mushrooms", 9.2, 8.6, 70, 0.8),
    ]
    # ---- behind the top rim: only low props show (the frame's top edge cuts at ~1 m there)
    props += [P("Env_Bush", -2.5, 13.2, 0, 0.9), P("Env_Bush", 1.9, 13.4, 90, 1.0), P("Env_MossyRock_B", -0.3, 12.9, 30, 0.9),
              P("Env_RuinColumn_Broken", 3.4, 13.0, 300, 0.8), P("Env_Fence", -4.9, 13.0, 95, 0.9),
              P("Env_Mushrooms", 0.8, 12.7, 0, 0.9)]
    props += dress(props, rng, "Env_Flowers", [(-5.5, 8.4), (-8.2, 8.0), (-6.1, 4.9), (-5.4, 0.3), (6.9, 0.2),
                                               (9.8, 0.4), (10.6, 8.0), (-1.2, 12.8), (0.9, 13.3), (-8.3, 5.9),
                                               (-10.6, 4.2), (-7.0, 12.4)], 2, 0.8, 0.35)
    props += dress(props, rng, "Env_GrassTuft", [(-5.2, 9.9), (-7.9, 2.3), (-6.3, 5.5), (-9.0, 11.2), (-4.8, 0.6),
                                                 (6.6, 2.4), (6.7, 5.8), (6.6, 10.8), (9.7, 3.5), (9.4, 11.4),
                                                 (4.8, 3.0), (4.8, 8.7), (-2.9, 12.8), (2.7, 12.9), (8.0, -1.0),
                                                 (-9.8, 3.9), (-8.1, 5.0)], 3, 0.9, 0.28)
    props += scatter(props, rng, "Env_GrassTuft", 22, [(-12, -4.5, -1.5, 14.5), (4.5, 12, -1.5, 14.5)], 0.3)
    props += scatter(props, rng, "Env_Pebbles", 5, [(-9, -4.6, -1.0, 12), (4.6, 9, -1.0, 12)], 0.4, s=(0.6, 0.9))
    # ---- light: warm low sun from the left through the canopy; motes and leaves ride the shafts
    gold = (1.0, 1.0, 1.0)      # relative tint: final colour = color * lighting.preset.godRayColor
    # shafts only on the sun side: a shaft landing on the right would have to cross over the arena
    for (lx, lz), L, w, k in (((-7.2, 9.7), 10.0, 0.85, 0.34), ((-6.3, 5.8), 10.0, 0.75, 0.3),
                              ((-8.6, 12.9), 9.5, 0.7, 0.26), ((-5.5, 1.9), 9.5, 0.65, 0.24),
                              ((-9.4, 7.6), 9.0, 0.6, 0.22)):
        props.append(shaft((lx, lz), SUN1, L, w, gold, k))
    props += [
        particles("Leaf", (0.0, 3.2, 6.8), (24.0, 5.5, 16.0), 46, 6.0, (6.0, 9.0), (0.35, -0.42, -0.08),
                  (0.1, 0.16), [(0.98, 0.62, 0.22), (0.92, 0.78, 0.3), (0.55, 0.72, 0.25)], False, noise=0.6,
                  light_influence=0.85, tag="falling leaves over the whole diorama (sparse over the arena)"),
        particles("Mote", (-7.4, 1.4, 6.5), (6.0, 2.4, 13.0), 40, 6.0, (4.0, 7.0), (0.05, 0.12, 0.0), (0.05, 0.09),
                  [(1.0, 0.86, 0.45), (1.0, 0.7, 0.35)], True, noise=0.35, tag="golden pollen / fireflies in the forest shafts"),
        particles("Mote", (7.8, 1.4, 6.5), (6.0, 2.4, 13.0), 26, 4.0, (4.0, 7.0), (0.05, 0.12, 0.0), (0.05, 0.08),
                  [(1.0, 0.86, 0.45)], True, noise=0.35, tag="pollen over the temple terrace"),
    ]
    return {
        "name": "Mossy Ruins",
        "surfaces": {"Env_FloorTile/Top_Surface": "StoneFloor", "Env_FloorTile_Danger/Top_Surface": "StoneFloor",
                     "Env_LaunchPad/Top_Surface": "StoneFloor", "Env_WallSegment/Side_Surface": "MossyBrick",
                     "Env_WallSegment/Top_Surface": "StoneFloor", "Env_WallCorner/Side_Surface": "MossyBrick",
                     "Env_GroundMound/Top_Surface": "Grass", "Env_GroundPatch/Top_Surface": "Dirt",
                     "Env_PavingPatch/Top_Surface": "MossyBrick"},
        "ground": ground("Grass"),
        "lighting": lighting(SUN1, (1.0, 0.78, 0.5), 1.35, (0.5, 0.52, 0.78), (0.42, 0.36, 0.5), (0.22, 0.2, 0.24),
                             (0.95, 0.78, 0.55), 0.008, (1.0, 0.85, 0.6), (1.0, 0.82, 0.55), (1.0, 0.8, 0.5), 1.0,
                             (1.0, 0.9, 0.75),
                             {"shadows": [0.42, 0.4, 0.66], "highlights": [1.0, 0.84, 0.58], "saturation": 1.08,
                              "contrast": 1.08, "vignette": 0.32},
                             "golden hour: low warm sun from the left through the canopy, long violet shade, "
                             "god rays + pollen + falling leaves"),
        "props": props,
    }


# ---------------------------------------------------------------- act 2: sunken crypt (night)
MOON2 = (-0.42, -0.86, -0.2)


def act2():
    rng = random.Random(202)
    props = torches("Env_WallTorch", (2.6, 8.6), light_zs=(8.6,))
    # far walls: behind the top rim only the plinths show; the side back walls at z 11.3 are fully in frame
    props += [P("Env_CryptWall", x, 13.4, 180, 1.0) for x in (-3.0, -1.0, 1.0, 3.0)]
    props += [P("Env_CryptWall", -5.3, 11.4, 180, 1.0), P("Env_IronGate", -7.85, 11.4, 180, 1.0),
              P("Env_CryptWall", -10.4, 11.4, 180, 1.0), P("Env_CryptWall", -12.4, 11.4, 180, 1.0),
              P("Env_CryptWall", 5.3, 11.4, 180, 1.0), P("Env_CryptWall", 7.3, 11.4, 180, 1.0),
              P("Env_CryptWall", 9.3, 11.4, 180, 1.0), P("Env_CryptWall", 11.3, 11.4, 180, 1.0)]
    props += [P("Env_CryptWall", sx * 11.2, z, 90 if sx < 0 else 270, 1.0) for sx in (-1, 1)
              for z in (0.4, 2.4, 4.4, 6.4, 8.4)]
    props += [P("Env_Banner", x, 11.1, 180, 1.0, y=2.25) for x in (-5.3, -10.4, 5.3, 9.3)]
    props += [P("Env_Banner", sx * 10.9, z, 90 if sx < 0 else 270, 0.9, y=2.25) for sx in (-1, 1) for z in (3.4, 7.4)]
    props += [
        P("Env_Brazier", -5.0, 10.2, 0, 1.0, light=True), P("Env_Brazier", 5.0, 10.2, 0, 1.0, light=True),
        P("Env_Brazier", -5.1, 0.3, 0, 0.95, light=True), P("Env_Brazier", 5.1, 0.3, 0, 0.95, light=True),
        # left: sunken graveyard - tombstone rows either side of a path to the gate
        P("Env_Tombstone_A", -6.2, 8.4, 180, 1.0), P("Env_Tombstone_B", -9.4, 8.6, 172, 1.0),
        P("Env_Tombstone_A", -6.3, 5.6, 185, 0.95), P("Env_Tombstone_B", -9.5, 5.5, 176, 1.05),
        P("Env_Tombstone_A", -6.2, 2.8, 190, 0.95), P("Env_Tombstone_A", -9.4, 2.6, 178, 1.0),
        P("Env_Tombstone_B", -10.2, 9.9, 186, 0.9), P("Env_Tombstone_A", -8.9, 0.4, 170, 0.9),
        P("Env_Candles", -7.0, 10.1, 0, 1.0, light=True), P("Env_Candles", -8.8, 7.0, 50, 0.85),
        P("Env_Candles", -5.3, 4.2, 0, 0.8), P("Env_BonePile", -10.0, 1.6, 100, 1.1), P("Env_BonePile", -5.1, 7.1, 20, 0.8),
        P("Env_Urn", -5.6, 9.6, 0, 1.0), P("Env_Urn", -10.2, 4.1, 40, 0.9),
        # right: flooded sarcophagus hall
        P("Env_WaterPool", 8.0, 6.6, 90, 1.6), P("Env_WaterPool", 6.3, 1.7, 30, 1.0),
        P("Env_Coffin", 6.3, 8.6, 90, 1.0), P("Env_Coffin", 6.3, 4.6, 90, 1.0), P("Env_Coffin", 9.7, 9.3, 90, 1.0),
        P("Env_CryptPillar", 5.4, 1.6, 0, 1.0), P("Env_CryptPillar", 5.4, 5.6, 0, 1.0),
        P("Env_CryptPillar", 5.4, 9.6, 0, 1.0), P("Env_CryptPillar", -5.4, 11.9, 0, 1.0),
        P("Env_Coffin", 9.6, 3.2, 90, 0.9),
        P("Env_Candles", 5.1, 9.9, 30, 0.9, light=True), P("Env_Candles", 9.6, 7.8, 0, 0.8), P("Env_Candles", 5.1, 3.1, 90, 0.8),
        P("Env_Tombstone_B", 9.6, 1.0, 190, 1.0), P("Env_BonePile", 10.1, 5.2, 300, 1.0), P("Env_BonePile", 5.0, 6.6, 200, 0.75),
        P("Env_Urn", 7.3, 10.3, 0, 1.0), P("Env_Urn", 10.0, 6.4, 120, 0.85),
        P("Env_Candles", -9.9, 5.4, 120, 0.7), P("Env_Candles", -6.8, 1.6, 200, 0.75), P("Env_Candles", 7.3, 3.4, 60, 0.7),
        P("Env_Candles", 7.0, 9.9, 10, 0.75), P("Env_Candles", 10.0, 2.0, 0, 0.7),
        # behind the top rim
        P("Env_Candles", -1.4, 12.6, 0, 0.9), P("Env_Candles", 2.1, 12.55, 40, 0.85), P("Env_BonePile", -2.9, 12.8, 150, 0.9),
        P("Env_BonePile", 0.4, 12.9, 20, 0.8), P("Env_Urn", 3.3, 12.7, 0, 0.9),
    ]
    props += [P("Env_PavingPatch", -7.85, z, 90 + 7 * i, 0.62) for i, z in enumerate((10.3, 8.3, 6.3, 4.3, 2.3, 0.3))]
    props += [P("Env_GroundPatch", x, z, 35 * i, 0.9) for i, (x, z) in
              enumerate(((-6.2, 8.9), (-6.3, 5.9), (-6.2, 3.1), (-9.5, 9.1), (-9.5, 6.0), (-9.4, 3.0), (-9.0, 0.6)))]
    props += [P("Env_PavingPatch", 7.9, z, 90, 1.0) for z in (2.4, 5.2, 8.0, 10.3)]
    props += scatter(props, rng, "Env_Pebbles", 6, [(-10.5, -4.5, -1.5, 12.8), (4.5, 10.5, -1.5, 12.8)], 0.4,
                     s=(0.6, 0.85))
    props += dress(props, rng, "Env_GrassTuft", [(-9.9, 9.4), (-4.9, 9.0), (10.2, 10.6), (4.8, 2.3), (-10.0, 3.2),
                                                 (-6.6, 1.1)], 2, 0.6, 0.25, s=(0.55, 0.8))
    # ---- light: cold moonbeams from high windows (upper right), embers above the braziers, dust in the beams
    moon = (1.0, 1.0, 1.0)      # relative tint of preset.godRayColor (cold moonlight)
    for (lx, lz), L, w, k in (((-7.8, 7.4), 10.0, 1.2, 0.5), ((-7.6, 3.2), 10.0, 1.0, 0.4),
                              ((8.0, 6.6), 10.5, 1.3, 0.5), ((7.5, 1.9), 9.5, 0.9, 0.35), ((-9.6, 10.6), 9.0, 0.9, 0.3)):
        props.append(shaft((lx, lz), MOON2, L, w, moon, k))
    for x, z in ((-5.0, 10.2), (5.0, 10.2), (-5.1, 0.3), (5.1, 0.3)):
        props.append(particles("Ember", (x, 2.2, z), (0.5, 0.4, 0.5), 14, 5.0, (1.2, 2.2), (0.0, 0.9, 0.05),
                               (0.04, 0.07), [(1.0, 0.62, 0.22), (1.0, 0.42, 0.14)], True, noise=0.5,
                               tag="embers rising from the brazier"))
    props += [
        particles("Dust", (0.0, 2.5, 6.4), (22.0, 4.0, 15.0), 50, 6.0, (6.0, 10.0), (0.06, -0.03, 0.04), (0.04, 0.07),
                  [(0.7, 0.78, 1.0)], False, noise=0.2, light_influence=1.0, tag="dust motes, visible in the moonbeams"),
        particles("Smoke", (-7.8, 0.15, 5.6), (4.5, 0.3, 11.0), 16, 2.0, (6.0, 9.0), (0.08, 0.02, 0.0), (0.9, 1.5),
                  [(0.35, 0.42, 0.7)], False, noise=0.1, light_influence=0.6,
                  tag="ground mist over the graveyard (low alpha, keep overdraw small)"),
    ]
    return {
        "name": "Sunken Crypt",
        "surfaces": {"Env_FloorTile/Top_Surface": "CryptFloor", "Env_FloorTile_Danger/Top_Surface": "CryptFloor",
                     "Env_LaunchPad/Top_Surface": "CryptFloor", "Env_WallSegment/Side_Surface": "CryptBrick",
                     "Env_WallSegment/Top_Surface": "CryptFloor", "Env_WallCorner/Side_Surface": "CryptBrick",
                     "Env_CryptWall/Side_Surface": "CryptBrick", "Env_GroundMound/Top_Surface": "Dirt",
                     "Env_GroundPatch/Top_Surface": "Dirt",
                     "Env_PavingPatch/Top_Surface": "CryptFloor"},
        "ground": ground("CryptBrick"),
        "lighting": lighting(MOON2, (0.55, 0.66, 1.0), 0.75, (0.2, 0.24, 0.48), (0.14, 0.15, 0.3), (0.06, 0.06, 0.1),
                             (0.08, 0.1, 0.2), 0.02, (0.5, 0.6, 1.0), (1.0, 0.7, 0.45), (0.55, 0.68, 1.0), 0.6,
                             (0.9, 0.92, 1.0),
                             {"shadows": [0.3, 0.36, 0.72], "highlights": [1.0, 0.84, 0.68], "saturation": 1.05,
                              "contrast": 1.1, "vignette": 0.38},
                             "deep blue moonlight from the upper right, warm brazier and candle pools, flooded hall"),
        "props": props,
    }


# ---------------------------------------------------------------- act 3: crystal hollow (magic)
CEIL3 = (0.18, -0.96, -0.12)


def act3():
    rng = random.Random(303)
    props = torches("Env_WallTorch_Arcane", (2.6, 8.6))
    props += [
        # cave rim: big rock masses and stalagmites at the frame edges
        P("Env_CaveRock_A", -11.4, 11.6, 30, 1.7), P("Env_CaveRock_A", -12.4, 6.0, 100, 1.6),
        P("Env_CaveRock_A", -10.6, 0.2, 250, 1.3), P("Env_CaveRock_A", 11.6, 11.0, 200, 1.7),
        P("Env_CaveRock_A", 12.4, 5.2, 300, 1.6), P("Env_CaveRock_A", 10.8, -0.6, 20, 1.3),
        P("Env_CaveRock_A", -7.8, 14.2, 150, 1.3), P("Env_CaveRock_A", 7.6, 14.6, 60, 1.3),
        P("Env_CaveRock_B", -5.9, 13.1, 0, 1.1), P("Env_CaveRock_B", 6.1, 13.2, 150, 1.0),
        P("Env_CaveRock_B", -5.4, -1.0, 60, 0.85), P("Env_CaveRock_B", 10.2, 7.9, 10, 0.9),
        P("Env_CaveRock_B", -9.9, 3.2, 200, 0.9),
        P("Env_Stalagmite", -9.1, 12.6, 0, 1.3), P("Env_Stalagmite", 9.4, 12.4, 90, 1.25),
        P("Env_Stalagmite", -10.1, 8.4, 40, 1.05), P("Env_Stalagmite", 10.1, 2.3, 200, 1.1),
        P("Env_Stalagmite", -7.2, 0.0, 130, 0.8), P("Env_Stalagmite", 7.8, -0.6, 20, 0.8),
        P("Env_Stalagmite", 11.2, 8.6, 300, 0.95), P("Env_Stalagmite", -12.0, 2.6, 80, 1.0),
        # crystal groves = the light sources
        P("Env_Crystal_A", -7.0, 9.7, 20, 1.25, light=True), P("Env_Crystal_B", -5.3, 11.1, 200, 0.9),
        P("Env_Crystal_C", -8.2, 10.8, 60, 1.0), P("Env_Crystal_C", -5.3, 8.5, 300, 0.7),
        P("Env_Crystal_A", 7.2, 5.0, 170, 1.15, light=True), P("Env_Crystal_B", 5.5, 3.3, 20, 1.0, light=True),
        P("Env_Crystal_C", 8.5, 6.3, 110, 1.1), P("Env_Crystal_C", 5.3, 5.9, 240, 0.7),
        P("Env_Crystal_B", -5.6, 2.0, 300, 0.9, light=True), P("Env_Crystal_C", -4.95, 3.5, 0, 0.8),
        P("Env_Crystal_B", -1.7, 12.9, 0, 0.9, light=True), P("Env_Crystal_C", -0.3, 12.7, 90, 0.85),
        P("Env_Crystal_C", 2.2, 12.8, 200, 0.95), P("Env_Crystal_B", 8.6, 9.8, 60, 1.05),
        P("Env_Crystal_C", 5.1, 11.4, 30, 0.8),
        # rune circle on the left around a glowing pool, mushroom patches
        P("Env_WaterPool_Glow", -8.2, 5.5, 0, 1.0), P("Env_RuneStone", -7.0, 4.3, 150, 1.0, light=True),
        P("Env_RuneStone", -9.5, 4.5, 205, 0.9), P("Env_RuneStone", -8.3, 6.9, 180, 0.95),
        P("Env_GlowMushroom", -5.3, 6.6, 0, 1.1, light=True), P("Env_GlowMushroom", 5.5, 9.9, 90, 1.0),
        P("Env_GlowMushroom", 8.8, 1.4, 40, 1.2), P("Env_GlowMushroom", -8.9, 1.8, 200, 1.0),
        P("Env_GlowMushroom", 4.9, -0.6, 300, 0.9), P("Env_GlowMushroom", -10.2, 9.5, 120, 0.9),
        P("Env_GroundMound", -9.6, 10.6, 30, 1.2), P("Env_GroundMound", 10.1, 4.9, 0, 1.1),
        P("Env_GroundPatch", -6.8, 9.9, 20, 0.8), P("Env_GroundPatch", 7.1, 4.8, 60, 0.85),
    ]
    props += scatter(props, rng, "Env_Pebbles", 12, [(-11, -4.5, -1.5, 13), (4.5, 11, -1.5, 13)], 0.4)
    props += scatter(props, rng, "Env_Crystal_C", 6, [(-10, -4.7, -1.0, 12), (4.7, 10, -1.0, 12)], 0.45, s=(0.45, 0.65))
    # ---- light: violet shafts from cracks in the cave ceiling, floating motes, sparkles around the groves
    for (lx, lz), L, w, col, k in (((-7.0, 9.4), 11.0, 1.1, (0.75, 0.55, 1.0), 0.55),
                                   ((7.2, 5.0), 11.0, 1.2, (0.55, 0.85, 1.0), 0.5),
                                   ((-8.3, 5.4), 11.5, 0.9, (0.8, 0.6, 1.0), 0.45),
                                   ((8.4, 10.6), 10.5, 0.8, (0.75, 0.55, 1.0), 0.35),
                                   ((-6.2, 1.3), 10.0, 0.8, (0.55, 0.85, 1.0), 0.3)):
        props.append(shaft((lx, lz), CEIL3, L, w, col, k))
    props += [
        particles("Mote", (0.0, 2.2, 6.4), (22.0, 3.6, 15.0), 70, 10.0, (4.0, 8.0), (0.0, 0.14, 0.0), (0.05, 0.09),
                  [(0.45, 0.95, 1.0), (0.8, 0.55, 1.0), (1.0, 0.6, 0.95)], True, noise=0.4,
                  tag="floating magic motes (sparse over the arena)"),
        particles("Star", (-6.6, 1.0, 9.8), (3.0, 1.6, 3.0), 10, 3.0, (0.6, 1.2), (0.0, 0.05, 0.0), (0.08, 0.14),
                  [(0.6, 1.0, 1.0)], True, noise=0.0, tag="sparkles on the big cyan grove"),
        particles("Star", (7.0, 1.0, 4.6), (3.0, 1.6, 3.0), 10, 3.0, (0.6, 1.2), (0.0, 0.05, 0.0), (0.08, 0.14),
                  [(0.6, 1.0, 1.0), (0.85, 0.6, 1.0)], True, noise=0.0, tag="sparkles on the right grove"),
    ]
    return {
        "name": "Crystal Hollow",
        "surfaces": {"Env_FloorTile/Top_Surface": "StoneFloor", "Env_FloorTile_Danger/Top_Surface": "StoneFloor",
                     "Env_LaunchPad/Top_Surface": "StoneFloor", "Env_WallSegment/Side_Surface": "CrystalRock",
                     "Env_WallSegment/Top_Surface": "CrystalRock", "Env_WallCorner/Side_Surface": "CrystalRock",
                     "Env_GroundMound/Top_Surface": "CrystalRock", "Env_GroundPatch/Top_Surface": "CrystalRock"},
        "ground": ground(None, "Env_CaveFloorTile"),
        "lighting": lighting(CEIL3, (0.72, 0.55, 1.0), 0.6, (0.3, 0.2, 0.52), (0.18, 0.14, 0.34), (0.08, 0.06, 0.14),
                             (0.2, 0.12, 0.34), 0.018, (0.5, 1.0, 1.0), (0.7, 0.9, 1.0), (1.0, 1.0, 1.0), 0.9,
                             (0.85, 0.9, 1.0),
                             {"shadows": [0.36, 0.26, 0.64], "highlights": [0.8, 0.95, 1.0], "saturation": 1.1,
                              "contrast": 1.1, "vignette": 0.38},
                             "violet cave gloom lit from ceiling cracks, cyan and magenta crystal glow, floating motes"),
        "props": props,
    }


# ---------------------------------------------------------------- checks + output
def camera_pose():
    p = math.radians(CAMERA["pitchDeg"])
    fwd = (0.0, -math.sin(p), math.cos(p))
    up = (0.0, math.cos(p), math.sin(p))
    tx, ty, tz = CAMERA["target"]
    d = CAMERA["distance"]
    return (tx - fwd[0] * d, ty - fwd[1] * d, tz - fwd[2] * d), fwd, up


def in_view(x, y, z, margin=1.0):
    """Is a point inside the preview camera frame (margin > 1 = overscan)?"""
    cam, fwd, up = camera_pose()
    v = (x - cam[0], y - cam[1], z - cam[2])
    depth = sum(a * b for a, b in zip(v, fwd))
    t = math.tan(math.radians(CAMERA["fovDeg"]) / 2)
    sy = sum(a * b for a, b in zip(v, up)) / (depth * t)
    sx = v[0] / (depth * t * CAMERA["aspect"])
    return abs(sx) <= margin and abs(sy) <= margin


def prop_visible(p, margin=FRAME_MARGIN):
    """Any corner of the prop's rough bounding box (footprint / canopy reach x height) inside the frame?"""
    if not p["piece"].startswith("Env_"):
        return True
    s = p["scale"]
    r = max(RADIUS.get(p["piece"], 0.5), REACH.get(p["piece"], 0.0)) * s
    h = HEIGHT.get(p["piece"], 1.0) * s
    return any(in_view(p["x"] + dx, p["y"] + dy, p["z"] + dz, margin)
               for dx in (-r, 0.0, r) for dz in (-r, 0.0, r) for dy in (0.0, h))


def prune(act):
    """Drop dressing that can never be seen from the game camera (saves draw calls on the TV)."""
    keep = [p for p in act["props"] if prop_visible(p)]
    act["pruned"] = len(act["props"]) - len(keep)
    act["props"] = keep
    return act


def resolve_lights(act):
    lights = []
    for i, p in enumerate(act["props"]):
        if not p.get("light"):
            continue
        d = PIECE_LIGHTS[p["piece"]]
        a = math.radians(p["rotY"])
        lx, ly, lz = (c * p["scale"] for c in d["offset"])
        pos = [p["x"] + lx * math.cos(a) + lz * math.sin(a), p["y"] + ly, p["z"] - lx * math.sin(a) + lz * math.cos(a)]
        lights.append({"prop": i, "piece": p["piece"], "position": r3(pos), "color": d["color"],
                       "intensity": d["intensity"], "range": d["range"]})
    if len(lights) > MAX_LIGHTS:
        raise SystemExit(f"{act['name']}: {len(lights)} real-time lights > {MAX_LIGHTS}")
    act["pointLights"] = lights
    return act


def surface_list(mapping):
    """{'Env_X/Part_Surface': 'Name'} -> [{piece, part, surface}] (JsonUtility has no dictionaries)."""
    out = []
    for key in sorted(mapping):
        piece, part = key.split("/")
        out.append({"piece": piece, "part": part, "surface": mapping[key]})
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(HERE, "layouts.json"))
    a = ap.parse_args()
    acts = {"1": act1(), "2": act2(), "3": act3()}
    for act in acts.values():
        prune(act)
        resolve_lights(act)
    doc = {
        "version": 2,
        "generator": "Tools/Blender/environment/make_layouts.py (edit there, not here)",
        "coordinates": "TDD §14.1 arena frame, Unity metres: x right, y up, z north/up the arena; origin = arena "
                       "centre-x on the open bottom edge of the launch zone (sim (3.5, 0)). rotY degrees about +Y "
                       "(clockwise from above). Props face +Z; rotY 180 faces the camera. y is the pivot height "
                       "(ground props sit on groundY -0.2, arena kit on floorY 0).",
        "frame": {
            "simToFrame": "frame = (simX - 3.5, height, simY)",
            "arenaLayoutNote": "Presentation/ArenaLayout.cs (Foundation) currently centres the arena on its "
                               "transform and maps sim y to local -Z (TDD §14.1 says +Z). Until that is aligned, "
                               "parent the whole dressing under a root at ArenaLayout-local (0, 0, 5.8) rotated "
                               "rotY 180: that rigid transform lines the arena kit up exactly and mirrors the "
                               "scenery left/right (harmless). Directions (sun euler Y, LightShaft euler Y, "
                               "particle velocity) then rotate with the root: add 180 to world yaw values.",
        },
        "models": "Assets/Models/BilliardRogue/Environment/<piece>.fbx",
        "arena": ARENA,
        "camera": CAMERA,
        "pivots": [{"piece": k, "pivot": v} for k, v in (
            ("Env_FloorTile*", "centre of the top face (top at y 0); tile 1x1x0.2"),
            ("Env_LaunchPad", "centre of the top face; 7 x 1.6 x 0.2; place at z 0.8; rail groove at world z 0.55"),
            ("Env_GroundTile", "centre of the top face; 4 x 4 (surface ground)"),
            ("Env_CaveFloorTile", "centre of the tile at ground level; 4 x 4 palette ground; flat border"),
            ("Env_WallSegment", "bottom of the inner face, centred on the 1 m length; inner face along local +Z; "
                                "0.4 thick toward -Z; top 0.52; sinks to y -0.3"),
            ("Env_WallCorner", "bottom centre of a 0.44 m post (floor level; post sinks to y -0.3)"),
            ("Env_WallTorch*/Env_Banner", "wall mounting point; the prop sticks out along local +Z"),
            ("Env_LightShaft", "TOP centre of the volume; 1 m long along local -Y, top diameter 1, flares to 1.7"),
            ("Env_WaterPool*", "bottom centre; water surface 3 cm above the pivot plane"),
            ("Env_Stairs", "bottom centre of the 3.6 x 2.1 m footprint; risers face local +Z (rotY 180 faces the "
                           "camera); landing top y 0.42 over local z -1.05..0.45"),
            ("Env_PavingPatch/Env_GroundPatch", "centre; flat decal 1.5-2.5 cm above the pivot plane"),
            ("default", "bottom centre, facing +Z"))],
        "pieceLights": [dict(piece=k, **v) for k, v in sorted(PIECE_LIGHTS.items())],
        "effects": [{"piece": k, "doc": v} for k, v in {
            "LightShaft": "Fake god-ray volume. Instance Env_LightShaft.fbx (or any cone) at (x, y, z) = TOP of the "
                          "shaft with Quaternion.Euler(euler) (turns local -Y onto `direction`) and localScale = "
                          "size [top width, length, top width]; material M_LightShaft (BilliardRogue/LightShaft, "
                          "additive, Cull Off, ZWrite Off, no shadows) with _Color = color (a relative tint; white "
                          "in Acts 1-2, violet / cyan per shaft in Act 3) * lighting.preset.godRayColor, "
                          "_Intensity = intensity * preset.godRayIntensity. UV0.v = 1 at the top "
                          "-> 0 at the bottom: fade the bottom ~35% and the top ~10%; soften edges with N.V. The "
                          "volume is sunk below the ground so the fade hides the ground intersection.",
            "AmbientParticles": "Box-shaped looping emitter (ParticleSystem, world space, prewarmed): centre (x, y, z), "
                                "size = box extents, maxParticles, rate (per second), lifetime [min, max] s, velocity "
                                "(start velocity, world frame) + noise strength, startSize [min, max] m, start colour "
                                "random between colorA and colorB (multiply with preset.particleTint), shader = particle "
                                "material family, sprite = flipbook sheet (12 fps). Built by the VFX module into "
                                "ActDefinition.ambientParticlesPrefab. Keep total alive particles per act <= 200.",
        }.items()],
        "unityNotes": [
            "Forward renderer, 4 per-object lights: each act flags at most 8 props with light=true (pointLights); "
            "every other emissive part just glows through the Palette_Emission map + bloom.",
            "Parts named *_Surface take M_Surface_<surface> from the act's 'surfaces' list (piece + part); "
            "every other part takes M_Palette (Env_LightShaft/Shaft takes M_LightShaft). The ground tiles take "
            "M_Surface_<ground.surface> (ground.surface empty = palette ground piece, e.g. Env_CaveFloorTile).",
            "*_Surface UVs are local box projections in metres (1 UV = 1 m) that start at each 1 m cell edge. "
            "Surfaces.json tiles are 2 m (_Tiling 0.5): ToonLit should sample *_Surface parts with world-space box "
            "mapping (what the previews do) so neighbouring pieces continue the pattern; with mesh UVs every 1 m "
            "cell repeats the same half tile.",
            "Env_FloorTile_Danger/DangerInlay_Emissive is the red danger-row inlay (emissive palette red): give it "
            "M_DangerTile to pulse it when the danger row is occupied.",
            "Env_Tree_*/Canopy, Env_Banner/Cloth, */Flame, Env_IronGate/Gate and Env_WaterPool/Water have their own "
            "pivots for DOTween sway / flicker / raise / bob.",
            "lighting.preset uses ActLightingPreset field names; sunEuler = lighting.sun.euler (x = elevation, "
            "y = yaw in this frame). lighting.grade is the preview grade, a starting point for the act Volume.",
        ],
        "kit": kit(),
        "acts": [dict(id=int(k), **{**v, "surfaces": surface_list(v["surfaces"])}) for k, v in sorted(acts.items())],
    }
    with open(a.out, "w") as f:
        json.dump(doc, f, indent=1)
        f.write("\n")
    for k, act in acts.items():
        print(f"act {k}: pruned {act.pop('pruned')} props that never enter the game frame")
    n = sum(len(x["props"]) for x in acts.values())
    fx = {k: (sum(p["piece"] == "LightShaft" for p in x["props"]), sum(p["piece"] == "AmbientParticles"
                                                                        for p in x["props"])) for k, x in acts.items()}
    print(f"layouts -> {a.out} (kit {len(doc['kit'])}, entries {n}, lights "
          f"{[len(x['pointLights']) for x in acts.values()]}, shafts/emitters {fx})")


if __name__ == "__main__":
    main()
