"""Writes Tools/Blender/environment/layouts.json: arena kit assembly + suggested diorama dressing per act.

    python3 Tools/Blender/environment/make_layouts.py [--out PATH]

Pure Python (no Blender), deterministic (fixed seeds). Coordinates are Unity world metres: x right, y up, z north
(up the arena); the arena spans x in [-3.5, 3.5], z in [0, 11.6] (launch line z = 0, grid z in [1.6, 11.6]).
rotY is Unity degrees about +Y (clockwise seen from above; +Z turns toward +X). Pieces face +Z, so props that
should face the camera (which sits south, looking north) use rotY ~ 180.
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
CAMERA = {"target": [0.0, 0.0, 5.6], "pitchDeg": 58.0, "fovDeg": 28.0, "distance": 26.0, "aspect": 16 / 9,
          "note": "preview camera used for the renders; the game camera pose lives in ArenaConfig"}
# keep scenery out of this rectangle (arena + walls + a little air)
KEEP_OUT = (-4.25, 4.25, -1.05, 12.35)

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
# rough ground footprint radius per piece, for keeping scatter off big props
RADIUS = {"Env_Tree_A": 0.7, "Env_Tree_B": 0.7, "Env_Bush": 0.85, "Env_RuinColumn": 0.5, "Env_RuinColumn_Broken": 0.9,
          "Env_RuinArch": 1.7, "Env_MossyRock_A": 0.9, "Env_MossyRock_B": 0.6, "Env_StoneLantern": 0.45, "Env_Fence": 1.0,
          "Env_CryptWall": 1.0, "Env_Tombstone_A": 0.6, "Env_Tombstone_B": 0.6, "Env_Candles": 0.3, "Env_Brazier": 0.5,
          "Env_Coffin": 1.1, "Env_BonePile": 0.55, "Env_IronGate": 1.7, "Env_Crystal_A": 0.85, "Env_Crystal_B": 0.65,
          "Env_Crystal_C": 0.4, "Env_Stalagmite": 0.8, "Env_CaveRock_A": 1.2, "Env_CaveRock_B": 0.9,
          "Env_GlowMushroom": 0.5, "Env_RuneStone": 0.65, "Env_GroundMound": 0.0, "Env_Pebbles": 0.4,
          "Env_GrassTuft": 0.3, "Env_Flowers": 0.35, "Env_Banner": 0.0}


def P(piece, x, z, rot=0.0, s=1.0, y=GROUND_Y, light=False, tag=None):
    e = {"piece": piece, "x": round(x, 3), "y": round(y, 3), "z": round(z, 3), "rotY": round(rot % 360, 1),
         "scale": round(s, 3)}
    if light:
        e["light"] = True
    if tag:
        e["tag"] = tag
    return e


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
        rr = RADIUS.get(p["piece"], 0.5) * p["scale"]
        if rr > 0 and (p["x"] - x) ** 2 + (p["z"] - z) ** 2 < (rr + r) ** 2:
            return True
    return False


def scatter(props, rng, piece, count, regions, r, s=(0.8, 1.2), rot=(0, 360), tries=400):
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


def ground(surface):
    tiles = [[x, z] for x in range(-22, 23, 4) for z in range(-10, 27, 4)]
    return {"piece": "Env_GroundTile", "y": GROUND_Y, "tileSize": 4.0, "surface": surface, "tiles": tiles}


# ---------------------------------------------------------------- composition helpers
def paving(rng, x0, x1, z0, z1, keep=0.65, y=GROUND_Y + 0.03):
    """Ruined temple floor: kit floor tiles on a 1 m grid with gaps, jitter and small twists."""
    out = []
    x = x0
    while x <= x1 + 1e-6:
        z = z0
        while z <= z1 + 1e-6:
            if rng.random() < keep:
                out.append(P("Env_FloorTile", x + rng.uniform(-0.06, 0.06), z + rng.uniform(-0.06, 0.06),
                             rng.uniform(-7, 7), 1.0, y + rng.uniform(-0.03, 0.0)))
            z += 1.02
        x += 1.02
    return out


def path(points, s=0.8, rot0=0.0):
    """Chain of ground patches along a polyline (paths, rubble trails)."""
    out = []
    for i, (x, z) in enumerate(points):
        out.append(P("Env_GroundPatch", x, z, rot0 + i * 47.0, s * (0.9 + 0.2 * ((i * 7) % 3) / 2)))
    return out


def dress(props, rng, piece, centres, per, spread, r, s=(0.8, 1.15)):
    out = []
    for cx, cz in centres:
        out += cluster(props + out, rng, piece, cx, cz, per, spread, r, s)
    return out


# ---------------------------------------------------------------- act 1: mossy ruins (golden hour)
def act1():
    rng = random.Random(101)
    props = torches("Env_WallTorch", (2.6, 8.6))
    # left: forest glade, a dirt path up to a lantern-lit clearing
    props += path([(-6.1, -0.7), (-6.6, 1.1), (-7.1, 2.9), (-7.2, 4.8), (-7.3, 6.6)], 0.62)
    props += [P("Env_GroundPatch", -7.3, 8.2, 20, 1.15)]
    props += [
        P("Env_Tree_A", -11.1, 10.4, 30, 1.25), P("Env_Tree_B", -8.2, 12.9, 0, 1.05), P("Env_Tree_A", -11.5, 4.2, 200, 1.1),
        P("Env_Tree_B", -12.6, 7.4, 60, 1.0), P("Env_Tree_A", -9.0, -1.4, 120, 0.95),
        P("Env_StoneLantern", -6.2, 8.1, 180, 1.0, light=True), P("Env_StoneLantern", -8.5, 8.3, 180, 1.0, light=True),
        P("Env_RuinColumn_Broken", -7.4, 9.7, 210, 0.9), P("Env_MossyRock_A", -9.3, 2.2, 60, 1.0),
        P("Env_MossyRock_B", -5.4, 3.6, 110, 0.8), P("Env_MossyRock_B", -10.1, 6.1, 20, 1.0),
        P("Env_Bush", -5.0, 10.9, 20, 0.9), P("Env_Bush", -4.95, 6.1, 140, 0.75), P("Env_Bush", -5.1, 1.1, 80, 0.85),
        P("Env_Bush", -8.6, 5.3, 200, 0.9), P("Env_Bush", -6.9, -1.4, 10, 1.0),
        P("Env_GroundMound", -10.4, 6.8, 20, 1.3), P("Env_GroundMound", -10.0, 0.6, -30, 1.0),
    ]
    # right: temple colonnade on ruined paving, arch gateway, fence at the lower right
    props += paving(rng, 5.1, 8.2, 1.6, 10.8, keep=0.62)
    props += [
        P("Env_RuinColumn", 5.9, 2.9, 0, 1.0), P("Env_RuinColumn_Broken", 5.9, 6.3, 40, 1.0),
        P("Env_RuinColumn", 5.9, 9.7, 10, 1.0), P("Env_RuinColumn_Broken", 8.6, 4.4, 160, 0.95),
        P("Env_RuinArch", 8.9, 8.6, 180, 1.0), P("Env_MossyRock_A", 10.6, 3.0, 250, 0.9),
        P("Env_MossyRock_B", 7.3, 1.1, 20, 0.8),
        P("Env_Tree_A", 12.0, 5.8, 300, 1.15), P("Env_Tree_B", 11.9, 11.9, 45, 1.05), P("Env_Tree_A", 10.3, -0.6, 80, 0.9),
        P("Env_Bush", 10.7, 8.3, 70, 0.95), P("Env_Bush", 7.3, 12.1, 30, 0.9), P("Env_Bush", 5.0, -0.8, 200, 0.8),
        P("Env_StoneLantern", 5.1, 11.2, 180, 1.0, light=True),
        P("Env_Fence", 6.6, -0.35, 8, 1.0), P("Env_Fence", 8.6, -0.6, -5, 1.0),
        P("Env_GroundMound", 10.8, 9.8, 10, 1.1),
    ]
    # behind the top wall: only low props are visible there (the frame's top edge cuts at ~1.5 m)
    props += [P("Env_Bush", -2.4, 13.0, 0, 0.9), P("Env_Bush", 1.9, 13.3, 90, 1.0), P("Env_MossyRock_B", -0.3, 12.8, 30, 0.9),
              P("Env_RuinColumn_Broken", 3.4, 12.9, 300, 0.85), P("Env_Fence", -4.9, 12.9, 95, 0.9)]
    props += dress(props, rng, "Env_Flowers", [(-5.6, 7.4), (-8.0, 6.9), (-6.0, 4.2), (-5.4, 0.0), (6.9, 0.4),
                                               (9.8, 1.2), (7.2, 7.3), (-1.2, 12.6), (0.9, 12.6), (10.0, 6.9)], 2, 0.8, 0.35)
    props += dress(props, rng, "Env_GrassTuft", [(-5.2, 8.9), (-7.9, 1.8), (-6.2, 5.6), (-9.0, 9.1), (-4.8, -0.2),
                                                 (6.8, 2.2), (7.0, 5.2), (6.8, 10.8), (9.6, 5.3), (9.2, 11.2),
                                                 (4.8, 4.6), (4.7, 8.0), (-2.9, 12.6), (2.7, 12.6), (8.0, -1.0)],
                   3, 0.9, 0.28)
    props += scatter(props, rng, "Env_GrassTuft", 14, [(-12, -4.4, -1.5, 14), (4.4, 12, -1.5, 14)], 0.3)
    return {
        "name": "Mossy Ruins", "mood": "golden hour: low warm sun from the front-left, long shadows, cool violet shade",
        "surfaces": {"Env_FloorTile/Top_Surface": "StoneFloor", "Env_FloorTile_Danger/Top_Surface": "StoneFloor",
                     "Env_LaunchPad/Top_Surface": "StoneFloor", "Env_WallSegment/Side_Surface": "MossyBrick",
                     "Env_WallSegment/Top_Surface": "StoneFloor", "Env_WallCorner/Side_Surface": "MossyBrick",
                     "Env_GroundMound/Top_Surface": "Grass", "Env_GroundPatch/Top_Surface": "Dirt"},
        "ground": ground("Grass"),
        "lighting": {"sun": {"direction": [0.6, -0.55, 0.58], "color": [1.0, 0.74, 0.46], "intensity": 1.35},
                     "ambient": [0.36, 0.34, 0.6], "ambientIntensity": 0.5,
                     "grade": {"shadows": [0.42, 0.4, 0.66], "highlights": [1.0, 0.84, 0.58], "saturation": 1.08,
                               "contrast": 1.08, "vignette": 0.3}},
        "props": props,
    }


# ---------------------------------------------------------------- act 2: sunken crypt (night)
def act2():
    rng = random.Random(202)
    props = torches("Env_WallTorch", (2.6, 8.6), light_zs=(8.6,))
    # far walls: behind the top rim only the plinths show; the side back walls at z 11.3 are fully in frame
    props += [P("Env_CryptWall", x, 13.3, 180, 1.0) for x in (-3.0, -1.0, 1.0, 3.0)]
    props += [P("Env_CryptWall", -5.2, 11.3, 180, 1.0), P("Env_IronGate", -7.75, 11.3, 180, 1.0),
              P("Env_CryptWall", -10.3, 11.3, 180, 1.0),
              P("Env_CryptWall", 5.2, 11.3, 180, 1.0), P("Env_CryptWall", 7.2, 11.3, 180, 1.0),
              P("Env_CryptWall", 9.2, 11.3, 180, 1.0), P("Env_CryptWall", 11.2, 11.3, 180, 1.0)]
    props += [P("Env_CryptWall", sx * 10.9, z, 90 if sx < 0 else 270, 1.0) for sx in (-1, 1) for z in (0.2, 2.2, 4.2, 6.2, 8.2)]
    props += [P("Env_Banner", x, 11.0, 180, 1.0, y=2.25) for x in (-5.2, -10.3, 5.2, 7.2, 9.2)]
    props += [
        P("Env_Brazier", -4.95, 10.1, 0, 1.0, light=True), P("Env_Brazier", 4.95, 10.1, 0, 1.0, light=True),
        P("Env_Brazier", -5.1, 0.2, 0, 0.95, light=True), P("Env_Brazier", 5.1, 0.2, 0, 0.95, light=True),
        # left: graveyard rows leading to the gate
        P("Env_Tombstone_A", -6.0, 8.1, 180, 1.0), P("Env_Tombstone_B", -7.8, 8.3, 172, 1.0),
        P("Env_Tombstone_A", -9.5, 8.0, 188, 0.95), P("Env_Tombstone_B", -6.1, 5.2, 185, 0.95),
        P("Env_Tombstone_A", -7.9, 5.4, 176, 1.05), P("Env_Tombstone_A", -9.6, 5.1, 182, 0.9),
        P("Env_Tombstone_A", -6.2, 2.4, 190, 0.95), P("Env_Tombstone_B", -8.0, 2.2, 178, 1.0),
        P("Env_Candles", -6.9, 9.9, 0, 1.0, light=True), P("Env_Candles", -8.6, 6.5, 50, 0.85),
        P("Env_Candles", -5.3, 3.9, 0, 0.8), P("Env_BonePile", -9.3, 1.1, 100, 1.1), P("Env_BonePile", -4.8, 6.9, 20, 0.8),
        # right: sarcophagus hall
        P("Env_Coffin", 6.5, 8.3, 90, 1.0), P("Env_Coffin", 6.5, 5.3, 90, 1.0), P("Env_Coffin", 9.3, 6.8, 90, 1.0),
        P("Env_RuinColumn", 8.1, 9.9, 20, 0.95), P("Env_RuinColumn_Broken", 8.1, 3.6, 120, 0.9),
        P("Env_Candles", 5.3, 9.5, 30, 0.9, light=True), P("Env_Candles", 9.4, 8.6, 0, 0.8), P("Env_Candles", 5.2, 4.0, 90, 0.8),
        P("Env_Tombstone_B", 6.2, 1.9, 190, 1.0), P("Env_Tombstone_A", 9.2, 2.4, 175, 1.0),
        P("Env_BonePile", 9.6, 4.6, 300, 1.0), P("Env_BonePile", 5.0, 7.0, 200, 0.8),
        # behind the top rim
        P("Env_Candles", -1.4, 12.55, 0, 0.9), P("Env_Candles", 2.1, 12.5, 40, 0.85), P("Env_BonePile", -2.9, 12.7, 150, 0.9),
        P("Env_BonePile", 0.4, 12.8, 20, 0.8),
    ]
    props += path([(-7.75, 10.3), (-7.2, 8.9), (-7.0, 7.0), (-7.1, 4.9), (-7.0, 2.9), (-7.3, 0.9), (-7.6, -1.0)], 0.5)
    props += [P("Env_GroundPatch", 7.9, 6.8, 30, 1.3), P("Env_GroundPatch", 4.9, -1.0, 80, 0.9),
              P("Env_GroundMound", 11.0, 1.0, 180, 0.7)]
    props += scatter(props, rng, "Env_Pebbles", 12, [(-10, -4.4, -1.5, 12.5), (4.4, 10, -1.5, 12.5)], 0.4)
    props += dress(props, rng, "Env_GrassTuft", [(-9.6, 9.6), (-4.9, 9.0), (9.9, 10.0), (4.8, 2.9), (-9.8, 3.4)], 2, 0.6,
                   0.25, s=(0.55, 0.8))
    return {
        "name": "Sunken Crypt", "mood": "deep blue moonlight from above-right, warm brazier and candle pools",
        "surfaces": {"Env_FloorTile/Top_Surface": "CryptFloor", "Env_FloorTile_Danger/Top_Surface": "CryptFloor",
                     "Env_LaunchPad/Top_Surface": "CryptFloor", "Env_WallSegment/Side_Surface": "CryptBrick",
                     "Env_WallSegment/Top_Surface": "CryptFloor", "Env_WallCorner/Side_Surface": "CryptBrick",
                     "Env_CryptWall/Side_Surface": "CryptBrick", "Env_GroundMound/Top_Surface": "Dirt",
                     "Env_GroundPatch/Top_Surface": "Dirt"},
        "ground": ground("CryptFloor"),
        "lighting": {"sun": {"direction": [-0.42, -0.8, 0.42], "color": [0.55, 0.68, 1.0], "intensity": 0.7},
                     "ambient": [0.13, 0.15, 0.32], "ambientIntensity": 0.85,
                     "grade": {"shadows": [0.3, 0.36, 0.72], "highlights": [1.0, 0.84, 0.68], "saturation": 1.05,
                               "contrast": 1.1, "vignette": 0.36}},
        "props": props,
    }


# ---------------------------------------------------------------- act 3: crystal hollow (magic)
def act3():
    rng = random.Random(303)
    props = torches("Env_WallTorch_Arcane", (2.6, 8.6))
    props += [
        # cave rim: big rock masses and stalagmites at the frame edges
        P("Env_CaveRock_A", -11.2, 11.0, 30, 1.6), P("Env_CaveRock_A", -12.0, 5.2, 100, 1.5),
        P("Env_CaveRock_A", -10.2, -0.9, 250, 1.2), P("Env_CaveRock_A", 11.4, 10.2, 200, 1.6),
        P("Env_CaveRock_A", 12.1, 4.4, 300, 1.5), P("Env_CaveRock_A", 10.4, -1.0, 20, 1.2),
        P("Env_CaveRock_B", -6.2, 13.1, 0, 1.2), P("Env_CaveRock_B", 6.4, 13.2, 150, 1.1),
        P("Env_CaveRock_B", -5.3, -1.0, 60, 0.85), P("Env_CaveRock_B", 10.0, 7.3, 10, 0.9),
        P("Env_Stalagmite", -8.9, 12.3, 0, 1.3), P("Env_Stalagmite", 9.1, 12.4, 90, 1.2),
        P("Env_Stalagmite", -9.7, 8.0, 40, 1.0), P("Env_Stalagmite", 9.9, 1.9, 200, 1.05),
        P("Env_Stalagmite", -7.0, 0.3, 130, 0.8), P("Env_Stalagmite", 7.5, -0.4, 20, 0.8),
        # crystal groves = the light sources
        P("Env_Crystal_A", -6.9, 9.4, 20, 1.2, light=True), P("Env_Crystal_B", -5.3, 10.9, 200, 0.9),
        P("Env_Crystal_C", -8.1, 10.4, 60, 1.0), P("Env_Crystal_C", -5.4, 8.2, 300, 0.7),
        P("Env_Crystal_A", 7.0, 4.9, 170, 1.1, light=True), P("Env_Crystal_B", 5.5, 3.3, 20, 1.0, light=True),
        P("Env_Crystal_C", 8.3, 6.1, 110, 1.1), P("Env_Crystal_C", 5.3, 5.7, 240, 0.7),
        P("Env_Crystal_B", -5.6, 2.0, 300, 0.9, light=True), P("Env_Crystal_C", -4.9, 3.4, 0, 0.8),
        P("Env_Crystal_B", -1.7, 12.8, 0, 0.9, light=True), P("Env_Crystal_C", -0.3, 12.6, 90, 0.85),
        P("Env_Crystal_C", 2.2, 12.7, 200, 0.95), P("Env_Crystal_B", 8.4, 9.6, 60, 1.05),
        P("Env_Crystal_C", 5.1, 11.6, 30, 0.8),
        # rune circle on the left, mushroom patches
        P("Env_RuneStone", -7.6, 4.0, 150, 1.0, light=True), P("Env_RuneStone", -9.4, 4.4, 205, 0.9),
        P("Env_RuneStone", -8.5, 5.9, 180, 0.95),
        P("Env_GlowMushroom", -5.3, 6.5, 0, 1.1, light=True), P("Env_GlowMushroom", 5.5, 9.9, 90, 1.0),
        P("Env_GlowMushroom", 8.6, 1.5, 40, 1.2), P("Env_GlowMushroom", -8.9, 1.9, 200, 1.0),
        P("Env_GlowMushroom", 4.9, -0.6, 300, 0.9), P("Env_GlowMushroom", -10.1, 9.2, 120, 0.9),
        P("Env_GroundMound", -9.3, 10.2, 30, 1.2), P("Env_GroundMound", 9.8, 4.6, 0, 1.1),
        P("Env_GroundPatch", -8.5, 4.9, 0, 1.2), P("Env_GroundPatch", 6.8, 4.4, 60, 1.1),
        P("Env_GroundPatch", -6.6, 9.8, 20, 1.0), P("Env_GroundPatch", 0.3, 12.9, 0, 1.1),
    ]
    props += scatter(props, rng, "Env_Pebbles", 10, [(-11, -4.4, -1.5, 13), (4.4, 11, -1.5, 13)], 0.4)
    props += scatter(props, rng, "Env_Crystal_C", 5, [(-10, -4.6, -1.0, 12), (4.6, 10, -1.0, 12)], 0.45, s=(0.45, 0.65))
    return {
        "name": "Crystal Hollow", "mood": "violet cave gloom, cyan and magenta crystal glow",
        "surfaces": {"Env_FloorTile/Top_Surface": "StoneFloor", "Env_FloorTile_Danger/Top_Surface": "StoneFloor",
                     "Env_LaunchPad/Top_Surface": "StoneFloor", "Env_WallSegment/Side_Surface": "CrystalRock",
                     "Env_WallSegment/Top_Surface": "CrystalRock", "Env_WallCorner/Side_Surface": "CrystalRock",
                     "Env_GroundMound/Top_Surface": "CrystalRock", "Env_GroundPatch/Top_Surface": "CrystalRock"},
        "ground": ground("Dirt"),
        "lighting": {"sun": {"direction": [0.25, -0.93, 0.28], "color": [0.72, 0.55, 1.0], "intensity": 0.55},
                     "ambient": [0.15, 0.1, 0.28], "ambientIntensity": 0.85,
                     "grade": {"shadows": [0.36, 0.26, 0.64], "highlights": [0.8, 0.95, 1.0], "saturation": 1.1,
                               "contrast": 1.1, "vignette": 0.36}},
        "props": props,
    }


def in_view(x, y, z):
    """Is a Unity point inside the preview camera frame?"""
    p = math.radians(CAMERA["pitchDeg"])
    fwd = (0.0, -math.sin(p), math.cos(p))
    up = (0.0, math.cos(p), math.sin(p))
    tx, ty, tz = CAMERA["target"]
    d = CAMERA["distance"]
    cam = (tx - fwd[0] * d, ty - fwd[1] * d, tz - fwd[2] * d)
    v = (x - cam[0], y - cam[1], z - cam[2])
    depth = sum(a * b for a, b in zip(v, fwd))
    t = math.tan(math.radians(CAMERA["fovDeg"]) / 2)
    sy = sum(a * b for a, b in zip(v, up)) / (depth * t)
    sx = v[0] / (depth * t * CAMERA["aspect"])
    return abs(sx) <= 1.0 and abs(sy) <= 1.0


def resolve_lights(act):
    lights = []
    for i, p in enumerate(act["props"]):
        if not p.get("light"):
            continue
        d = PIECE_LIGHTS[p["piece"]]
        a = math.radians(p["rotY"])
        lx, ly, lz = (c * p["scale"] for c in d["offset"])
        pos = [p["x"] + lx * math.cos(a) + lz * math.sin(a), p["y"] + ly, p["z"] - lx * math.sin(a) + lz * math.cos(a)]
        lights.append({"prop": i, "piece": p["piece"], "position": [round(c, 3) for c in pos], "color": d["color"],
                       "intensity": d["intensity"], "range": d["range"]})
    act["pointLights"] = lights
    return act


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(HERE, "layouts.json"))
    a = ap.parse_args()
    acts = {"1": act1(), "2": act2(), "3": act3()}
    for act in acts.values():
        resolve_lights(act)
    doc = {
        "version": 1,
        "generator": "Tools/Blender/environment/make_layouts.py (edit there, not here)",
        "coordinates": "Unity world metres. x right, y up, z north/up the arena; rotY degrees about +Y (clockwise from "
                       "above). Props face +Z; rotY 180 faces the camera. y is the pivot height (ground props sit on "
                       "groundY -0.2, arena kit on floorY 0).",
        "models": "Assets/Models/BilliardRogue/Environment/<piece>.fbx",
        "arena": ARENA,
        "camera": CAMERA,
        "pivots": {
            "Env_FloorTile*": "centre of the top face (top at y 0); tile 1x1x0.2",
            "Env_LaunchPad": "centre of the top face; 7 x 1.6 x 0.2; place at z 0.8; rail groove at world z 0.55",
            "Env_GroundTile": "centre of the top face; 4 x 4",
            "Env_WallSegment": "bottom of the inner face, centred on the 1 m length; inner face along local +Z; "
                               "0.4 thick toward -Z; top 0.52; sinks to y -0.3",
            "Env_WallCorner": "bottom centre of a 0.44 m post (floor level; post sinks to y -0.3)",
            "Env_WallTorch*/Env_Banner": "wall mounting point; the prop sticks out along local +Z",
            "default": "bottom centre, facing +Z",
        },
        "pieceLights": PIECE_LIGHTS,
        "unityNotes": [
            "Forward renderer, 4 per-object lights: each act flags at most 8 props with light=true (pointLights); "
            "every other emissive part just glows through the Palette_Emission map + bloom.",
            "Parts named *_Surface take M_Surface_<name> from the act's 'surfaces' map (key '<piece>/<part>'); "
            "all other parts take M_Palette.",
            "Env_FloorTile_Danger/DangerInlay_Emissive is the red danger-row inlay (emissive palette red): give it "
            "M_DangerTile to pulse it when the danger row is occupied.",
            "Env_Tree_*/Canopy, Env_Banner/Cloth, */Flame and Env_IronGate/Gate have their own pivots for DOTween "
            "sway / flicker / raise.",
        ],
        "kit": kit(),
        "acts": acts,
    }
    with open(a.out, "w") as f:
        json.dump(doc, f, indent=1)
        f.write("\n")
    for k, act in acts.items():
        hidden = [p["piece"] for p in act["props"] if not (in_view(p["x"], p["y"], p["z"])
                                                            or in_view(p["x"], p["y"] + 1.5, p["z"]))]
        if hidden:
            print(f"act {k}: {len(hidden)} props outside the preview frame: {sorted(set(hidden))}")
    n = sum(len(x["props"]) for x in acts.values())
    print(f"layouts -> {a.out} (kit {len(doc['kit'])}, props {n}, lights "
          f"{[len(x['pointLights']) for x in acts.values()]})")


if __name__ == "__main__":
    main()
