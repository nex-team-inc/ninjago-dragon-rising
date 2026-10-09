#nullable enable

using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>The 3D prefabs of the hand cursor games, all from primitives: the Stone Kick lane and the Earth Seal wall.</summary>
    public static class CursorGamesWorldBuilder
    {
        public const string StoneKickLanePath = PrefabsRoot + "/StoneKick/StoneKickLane.prefab";
        public const string EarthSealWorldPath = PrefabsRoot + "/EarthSeal/EarthSealWorld.prefab";
        const string RockPiecePath = PrefabsRoot + "/StoneKick/RockPiece.prefab";
        const string TilePath = PrefabsRoot + "/EarthSeal/EarthSealTile.prefab";
        static readonly Color warmStone = new(0.68f, 0.63f, 0.57f);
        static readonly Color warmStoneDark = new(0.5f, 0.46f, 0.42f);

        #region Entry Point

        public static void Build()
        {
            BuildStoneKickLane(BuildRockPiece());
            BuildEarthSealWorld(BuildTile());
        }

        #endregion

        #region Stone Kick

        static Transform BuildRockPiece()
        {
            var root = new GameObject("RockPiece");
            Block(PrimitiveType.Cube, "Chunk", root.transform, Vector3.zero, new Vector3(0.52f, 0.44f, 0.48f), Lit("Rock Warm Grey", warmStone),
                new Vector3(12f, 25f, 8f));
            Block(PrimitiveType.Cube, "Edge", root.transform, new Vector3(0.13f, 0.17f, -0.1f), new Vector3(0.27f, 0.23f, 0.25f), Lit("Rock Warm Dark", warmStoneDark),
                new Vector3(-20f, 40f, 15f));
            return SavePrefab(root, RockPiecePath).transform;
        }

        static void BuildStoneKickLane(Transform piecePrefab)
        {
            var root = new GameObject("StoneKickLane");
            var lane = root.AddComponent<StoneKickLane>();
            BuildArena(root.transform);
            var boss = BuildBoss(root.transform);
            var rock = BuildRock(root.transform);
            var hang = Empty("HangPoint", root.transform, new Vector3(0f, 1.55f, 2.5f));
            var impact = Empty("ImpactPoint", root.transform, new Vector3(0f, 1.7f, -1.05f));
            var pieces = Empty("Pieces", root.transform, Vector3.zero);
            // Low behind the player's spot: the rock hangs mid-screen and the boss stands above it on the ledge.
            var camera = NinjagoWorldBuilder.NewCamera(root.transform, new Vector3(0f, 1.8f, -1.5f), new Vector3(4f, 0f, 0f), 62f);
            Set(lane, "boss", boss);
            Set(lane, "rock", rock);
            Set(lane, "piecePrefab", piecePrefab);
            Set(lane, "piecesRoot", pieces);
            Set(lane, "hangPoint", hang);
            Set(lane, "impactPoint", impact);
            Set(lane, "laneCamera", camera);
            SavePrefab(root, StoneKickLanePath);
        }

        static void BuildArena(Transform root)
        {
            var scenery = Empty("Scenery", root, Vector3.zero);
            Block(PrimitiveType.Cube, "Floor", scenery, new Vector3(0f, -0.1f, 8f), new Vector3(20f, 0.2f, 26f), Lit("Arena Sand", new Color(0.96f, 0.83f, 0.52f)));
            var cliff = Lit("Arena Cliff", new Color(0.88f, 0.5f, 0.27f));
            var cliffDark = Lit("Arena Cliff Dark", new Color(0.7f, 0.36f, 0.2f));
            Block(PrimitiveType.Cube, "Ledge", scenery, new Vector3(0f, 1.5f, 14f), new Vector3(7f, 3f, 4f), cliff);
            Block(PrimitiveType.Cube, "LedgeStep", scenery, new Vector3(0f, 0.6f, 11.5f), new Vector3(5f, 1.2f, 1f), cliffDark);
            Block(PrimitiveType.Cube, "BackCliff", scenery, new Vector3(0f, 4f, 18f), new Vector3(26f, 8f, 2f), cliffDark);
            Block(PrimitiveType.Cube, "LeftCliff", scenery, new Vector3(-8.5f, 2.5f, 9f), new Vector3(3f, 5f, 20f), cliff);
            Block(PrimitiveType.Cube, "RightCliff", scenery, new Vector3(8.5f, 2.5f, 9f), new Vector3(3f, 5f, 20f), cliff);
            var boulder = Lit("Arena Boulder", new Color(0.62f, 0.56f, 0.5f));
            Block(PrimitiveType.Cube, "BoulderL", scenery, new Vector3(-4.5f, 0.5f, 6f), new Vector3(1.4f, 1f, 1.2f), boulder, new Vector3(0f, 20f, 0f));
            Block(PrimitiveType.Cube, "BoulderR", scenery, new Vector3(4.8f, 0.4f, 8f), new Vector3(1.1f, 0.8f, 1.3f), boulder, new Vector3(0f, -15f, 0f));
            var grass = Lit("Arena Grass", new Color(0.35f, 0.8f, 0.3f));
            Block(PrimitiveType.Cube, "GrassL", scenery, new Vector3(-5.5f, 0.01f, 2f), new Vector3(3f, 0.04f, 6f), grass);
            Block(PrimitiveType.Cube, "GrassR", scenery, new Vector3(5.5f, 0.01f, 3f), new Vector3(3f, 0.04f, 6f), grass);
        }

        static BossRig BuildBoss(Transform root)
        {
            var bossRoot = Empty("Boss", root, new Vector3(0f, 3f, 14f));
            var rig = bossRoot.gameObject.AddComponent<BossRig>();
            var body = Empty("Body", bossRoot, Vector3.zero);
            var grey = Lit("Boss Grey", new Color(0.56f, 0.59f, 0.65f));
            var dark = Lit("Boss Dark", new Color(0.35f, 0.37f, 0.43f));
            var eye = Lit("Boss Eye", new Color(1f, 0.45f, 0.1f), 0.3f, new Color(2.2f, 0.8f, 0.1f));
            Block(PrimitiveType.Cube, "LegL", body, new Vector3(-0.45f, 0.45f, 0f), new Vector3(0.6f, 0.9f, 0.6f), dark);
            Block(PrimitiveType.Cube, "LegR", body, new Vector3(0.45f, 0.45f, 0f), new Vector3(0.6f, 0.9f, 0.6f), dark);
            Block(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 1.6f, 0f), new Vector3(1.9f, 1.5f, 1f), grey);
            Block(PrimitiveType.Cube, "Belt", body, new Vector3(0f, 0.92f, 0f), new Vector3(1.8f, 0.2f, 1.05f), dark);
            Block(PrimitiveType.Cube, "Head", body, new Vector3(0f, 2.75f, 0f), new Vector3(1f, 0.85f, 0.9f), grey);
            Block(PrimitiveType.Cube, "Brow", body, new Vector3(0f, 2.98f, -0.46f), new Vector3(0.8f, 0.12f, 0.06f), dark);
            Block(PrimitiveType.Cube, "EyeL", body, new Vector3(-0.22f, 2.8f, -0.46f), new Vector3(0.2f, 0.1f, 0.04f), eye);
            Block(PrimitiveType.Cube, "EyeR", body, new Vector3(0.22f, 2.8f, -0.46f), new Vector3(0.2f, 0.1f, 0.04f), eye);
            var idleArm = Empty("IdleArmPivot", body, new Vector3(-1.2f, 2.1f, 0f));
            idleArm.localRotation = Quaternion.Euler(0f, 0f, -10f);
            Block(PrimitiveType.Cube, "Arm", idleArm, new Vector3(0f, -0.55f, 0f), new Vector3(0.45f, 1.1f, 0.5f), grey);
            Block(PrimitiveType.Cube, "Fist", idleArm, new Vector3(0f, -1.2f, 0f), new Vector3(0.6f, 0.5f, 0.6f), dark);
            // Underhand lob: the arm hangs along the pivot's -Y and swings about X toward the camera.
            var throwArm = Empty("ThrowArmPivot", body, new Vector3(1.2f, 2.1f, 0f));
            Block(PrimitiveType.Cube, "Arm", throwArm, new Vector3(0f, -0.55f, 0f), new Vector3(0.45f, 1.1f, 0.5f), grey);
            Block(PrimitiveType.Cube, "Fist", throwArm, new Vector3(0f, -1.2f, 0f), new Vector3(0.6f, 0.5f, 0.6f), dark);
            var hand = Empty("HandPoint", throwArm, new Vector3(0f, -1.35f, 0f));
            var chest = Empty("ChestPoint", body, new Vector3(0f, 1.6f, -0.55f));
            var marker = NinjagoWorldBuilder.BuildPlayerTagCanvas(bossRoot, "Marker", new Vector3(0f, 3.8f, 0f), 0.016f, 64f, true);
            Set(rig, "bodyRoot", body);
            Set(rig, "armPivot", throwArm);
            Set(rig, "handPoint", hand);
            Set(rig, "chestPoint", chest);
            Set(rig, "marker", marker);
            return rig;
        }

        static Transform BuildRock(Transform root)
        {
            var rock = Empty("Rock", root, new Vector3(0f, 1.55f, 2.5f));
            var warm = Lit("Rock Warm Grey", warmStone);
            var dark = Lit("Rock Warm Dark", warmStoneDark);
            Block(PrimitiveType.Sphere, "Core", rock, Vector3.zero, new Vector3(1.5f, 1.3f, 1.4f), warm);
            Block(PrimitiveType.Sphere, "LobeA", rock, new Vector3(0.32f, 0.3f, -0.18f), new Vector3(0.8f, 0.7f, 0.75f), warm);
            Block(PrimitiveType.Sphere, "LobeB", rock, new Vector3(-0.36f, -0.22f, -0.12f), new Vector3(0.75f, 0.65f, 0.7f), dark);
            Block(PrimitiveType.Cube, "Chip", rock, new Vector3(0.05f, -0.42f, -0.42f), new Vector3(0.3f, 0.22f, 0.22f), dark, new Vector3(5f, -30f, 15f));
            return rock;
        }

        #endregion

        #region Earth Seal

        // Authored as a unit square facing -Z; EarthSealTile stretches it to the grid's tile size.
        static EarthSealTile BuildTile()
        {
            var root = new GameObject("EarthSealTile");
            var tile = root.AddComponent<EarthSealTile>();
            var shake = Empty("Shake", root.transform, Vector3.zero);
            var stone = Block(PrimitiveType.Cube, "Stone", shake, Vector3.zero, new Vector3(0.97f, 0.97f, 0.5f), Lit("Seal Stone", Color.white, 0.1f));
            var seamMaterial = Lit("Seal Seam", new Color(1f, 0.85f, 0.3f), 0.5f, new Color(3f, 2f, 0.4f));
            var seam = Empty("Seam", shake, new Vector3(0f, 0f, -0.26f));
            Block(PrimitiveType.Cube, "SeamA", seam, new Vector3(-0.27f, 0.12f, 0f), new Vector3(0.36f, 0.05f, 0.02f), seamMaterial, new Vector3(0f, 0f, -35f));
            Block(PrimitiveType.Cube, "SeamB", seam, Vector3.zero, new Vector3(0.3f, 0.05f, 0.02f), seamMaterial, new Vector3(0f, 0f, 40f));
            Block(PrimitiveType.Cube, "SeamC", seam, new Vector3(0.26f, -0.1f, 0f), new Vector3(0.34f, 0.05f, 0.02f), seamMaterial, new Vector3(0f, 0f, -30f));
            var edgeMaterial = Lit("Seal Edge Glow", new Color(1f, 0.6f, 0.15f), 0.5f, new Color(2.5f, 1.1f, 0.2f));
            var edges = Empty("EdgeGlow", shake, new Vector3(0f, 0f, -0.27f));
            var top = Block(PrimitiveType.Cube, "Top", edges, new Vector3(0f, 0.47f, 0f), new Vector3(0.97f, 0.06f, 0.03f), edgeMaterial);
            var bottom = Block(PrimitiveType.Cube, "Bottom", edges, new Vector3(0f, -0.47f, 0f), new Vector3(0.97f, 0.06f, 0.03f), edgeMaterial);
            var left = Block(PrimitiveType.Cube, "Left", edges, new Vector3(-0.47f, 0f, 0f), new Vector3(0.06f, 0.97f, 0.03f), edgeMaterial);
            var right = Block(PrimitiveType.Cube, "Right", edges, new Vector3(0.47f, 0f, 0f), new Vector3(0.06f, 0.97f, 0.03f), edgeMaterial);
            var crust = Block(PrimitiveType.Cube, "Crust", shake, new Vector3(0f, 0f, -0.29f), new Vector3(0.9f, 0.9f, 0.04f), Lit("Seal Crust", Color.white, 0.2f));
            var monster = BuildMonster(shake);
            var hole = Block(PrimitiveType.Cube, "Hole", shake, new Vector3(0f, 0f, -0.255f), new Vector3(0.86f, 0.86f, 0.02f),
                Lit("Seal Hole", new Color(0.05f, 0.04f, 0.05f)));

            Set(tile, "shakeRoot", shake);
            Set(tile, "stone", stone.GetComponent<MeshRenderer>());
            Set(tile, "seam", seam.gameObject);
            Set(tile, "edgeGlow", edges.gameObject);
            Set(tile, "crust", crust.transform);
            Set(tile, "crustRenderer", crust.GetComponent<MeshRenderer>());
            Set(tile, "monster", monster);
            Set(tile, "hole", hole);
            SetArray(tile, "widthStretched", new Object[] { stone.transform, seam, top.transform, bottom.transform, hole.transform });
            SetArray(tile, "widthShifted", new Object[] { left.transform, right.transform });
            return SavePrefab(root, TilePath).GetComponent<EarthSealTile>();
        }

        static Transform BuildMonster(Transform parent)
        {
            var monster = Empty("Monster", parent, new Vector3(0f, 0f, -0.3f));
            Block(PrimitiveType.Sphere, "Body", monster, Vector3.zero, new Vector3(0.42f, 0.36f, 0.3f), Lit("Seal Monster", new Color(0.48f, 0.2f, 0.62f)));
            var white = Lit("Seal Monster Eye", Color.white, 0.4f);
            var pupil = Lit("Seal Monster Pupil", new Color(0.05f, 0.05f, 0.05f));
            foreach (var x in new[] { -0.08f, 0.08f })
            {
                Block(PrimitiveType.Cube, "Eye", monster, new Vector3(x, 0.05f, -0.15f), new Vector3(0.08f, 0.08f, 0.02f), white);
                Block(PrimitiveType.Cube, "Pupil", monster, new Vector3(x, 0.04f, -0.165f), new Vector3(0.04f, 0.04f, 0.02f), pupil);
            }

            return monster;
        }

        static void BuildEarthSealWorld(EarthSealTile tilePrefab)
        {
            var root = new GameObject("EarthSealWorld");
            var world = root.AddComponent<EarthSealWorld>();
            // Flat to the wall; the top fifth of the frame stays clear for the HUD and the camera preview.
            var camera = NinjagoWorldBuilder.NewCamera(root.transform, new Vector3(0f, 0f, -14f), Vector3.zero, 40f);
            camera.farClipPlane = 40f;
            var gridArea = new Vector2(16.6f, 7.7f);
            var gridCenterY = -0.85f;
            Block(PrimitiveType.Cube, "BackWall", root.transform, new Vector3(0f, 0f, 0.9f), new Vector3(34f, 18f, 1f), Lit("Seal Wall Back", new Color(0.44f, 0.35f, 0.28f)));
            var frame = Lit("Seal Wall Frame", new Color(0.3f, 0.23f, 0.19f));
            var halfWidth = gridArea.x * 0.5f + 0.25f;
            var halfHeight = gridArea.y * 0.5f + 0.25f;
            Block(PrimitiveType.Cube, "FrameTop", root.transform, new Vector3(0f, gridCenterY + halfHeight, 0.2f), new Vector3(halfWidth * 2f + 0.5f, 0.5f, 0.8f), frame);
            Block(PrimitiveType.Cube, "FrameBottom", root.transform, new Vector3(0f, gridCenterY - halfHeight, 0.2f), new Vector3(halfWidth * 2f + 0.5f, 0.5f, 0.8f), frame);
            Block(PrimitiveType.Cube, "FrameLeft", root.transform, new Vector3(-halfWidth, gridCenterY, 0.2f), new Vector3(0.5f, halfHeight * 2f, 0.8f), frame);
            Block(PrimitiveType.Cube, "FrameRight", root.transform, new Vector3(halfWidth, gridCenterY, 0.2f), new Vector3(0.5f, halfHeight * 2f, 0.8f), frame);
            var tiles = Empty("Tiles", root.transform, new Vector3(0f, gridCenterY, 0f));
            Set(world, "worldCamera", camera);
            Set(world, "tilePrefab", tilePrefab);
            Set(world, "tilesRoot", tiles);
            Set(world, "gridArea", property => property.vector2Value = gridArea);
            SavePrefab(root, EarthSealWorldPath);
        }

        #endregion
    }
}
