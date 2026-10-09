#nullable enable

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Nex.Ninjago.Editor.NinjagoAssetsBuilder;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>The 3D prefabs, all built from primitives: detection engine variant, courtyard, runner world.</summary>
    public static class NinjagoWorldBuilder
    {
        public const string EnginePath = PrefabsRoot + "/Detection/NinjaDetectionEngine.prefab";
        public const string CourtyardPath = PrefabsRoot + "/Fight/Courtyard.prefab";
        public const string RunnerWorldPath = PrefabsRoot + "/Chase/RunnerWorld.prefab";
        const string BarrierPath = PrefabsRoot + "/Chase/BrickBarrier.prefab";
        const string FloatingBlockPath = PrefabsRoot + "/Chase/FloatingBlock.prefab";
        static readonly Color skyColor = new(0.5f, 0.78f, 1f);

        #region Entry Point

        public static void Build()
        {
            BuildEngineVariant();
            BuildCourtyard();
            BuildRunnerWorld();
        }

        #endregion

        #region Detection

        static void BuildEngineVariant()
        {
            var basePrefab = Load<GameObject>("Assets/Prefabs/Detection/DetectionEngine/OnePlayerDetectionEngine.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            Set(instance.GetComponent<OnePlayerDetectionEngine>(), "smoothedNodePrefab",
                Load<GameObject>("Assets/Prefabs/Detection/DetectionEngine/HiddenPoseNode.prefab"));
            SavePrefab(instance, EnginePath);
        }

        #endregion

        #region Courtyard

        static void BuildCourtyard()
        {
            var root = new GameObject("Courtyard");
            var lane = root.AddComponent<FightLane>();
            BuildCourtyardScenery(root.transform);
            var ninja = BuildNinja(root.transform);
            var brute = BuildBrute(root.transform, out var bruteChest);
            // Behind and above the ninja so the brute's head and the cocked staff read over the ninja's shoulders.
            var camera = NewCamera(root.transform, new Vector3(0f, 2.8f, -3.9f), new Vector3(14f, 0f, 0f), 60f);
            Set(lane, "ninja", ninja);
            Set(lane, "brute", brute);
            Set(lane, "bruteChest", bruteChest);
            Set(lane, "laneCamera", camera);
            SavePrefab(root, CourtyardPath);
        }

        static void BuildCourtyardScenery(Transform root)
        {
            var scenery = Empty("Scenery", root, Vector3.zero);
            Block(PrimitiveType.Cube, "Floor", scenery, new Vector3(0f, -0.1f, 3f), new Vector3(13f, 0.2f, 15f), Lit("Courtyard Stone", new Color(0.86f, 0.78f, 0.62f)));
            Block(PrimitiveType.Cube, "Path", scenery, new Vector3(0f, -0.09f, 3f), new Vector3(3.2f, 0.2f, 15f), Lit("Courtyard Path", new Color(0.93f, 0.88f, 0.74f)));
            var wall = Lit("Courtyard Wall", new Color(0.86f, 0.32f, 0.22f));
            var roof = Lit("Courtyard Roof", new Color(0.12f, 0.62f, 0.5f));
            Block(PrimitiveType.Cube, "BackWall", scenery, new Vector3(0f, 1.4f, 10f), new Vector3(13f, 2.8f, 0.6f), wall);
            Block(PrimitiveType.Cube, "BackRoof", scenery, new Vector3(0f, 2.95f, 9.8f), new Vector3(14f, 0.35f, 1.6f), roof);
            Block(PrimitiveType.Cube, "LeftWall", scenery, new Vector3(-6.4f, 0.7f, 3f), new Vector3(0.5f, 1.4f, 14f), wall);
            Block(PrimitiveType.Cube, "RightWall", scenery, new Vector3(6.4f, 0.7f, 3f), new Vector3(0.5f, 1.4f, 14f), wall);
            var post = Lit("Courtyard Post", new Color(0.35f, 0.2f, 0.12f));
            var lantern = Lit("Courtyard Lantern", new Color(1f, 0.8f, 0.3f), 0.3f, new Color(1f, 0.6f, 0.1f));
            foreach (var x in new[] { -4.5f, 4.5f })
            {
                Block(PrimitiveType.Cube, "Post", scenery, new Vector3(x, 1f, 8.4f), new Vector3(0.3f, 2f, 0.3f), post);
                Block(PrimitiveType.Cube, "Lantern", scenery, new Vector3(x, 2.15f, 8.4f), new Vector3(0.55f, 0.55f, 0.55f), lantern);
            }
        }

        static NinjaRig BuildNinja(Transform root)
        {
            var ninjaRoot = Empty("Ninja", root, Vector3.zero);
            var rig = ninjaRoot.gameObject.AddComponent<NinjaRig>();
            var body = Empty("Body", ninjaRoot, Vector3.zero);
            var tint = Lit("Ninja Tint", Color.white, 0.25f);
            var dark = Lit("Ninja Dark", new Color(0.12f, 0.12f, 0.15f));
            var gold = Lit("Ninja Gold", new Color(1f, 0.78f, 0.15f), 0.5f);
            var skin = Lit("Ninja Skin", new Color(1f, 0.84f, 0.25f));
            var steel = Lit("Ninja Steel", new Color(0.85f, 0.88f, 0.92f), 0.7f);
            var tinted = new[]
            {
                Block(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 0.88f, 0f), new Vector3(0.56f, 0.55f, 0.32f), tint),
                Block(PrimitiveType.Cube, "ArmL", body, new Vector3(-0.38f, 0.86f, 0f), new Vector3(0.16f, 0.5f, 0.2f), tint, new Vector3(0f, 0f, -8f)),
                Block(PrimitiveType.Cube, "ArmR", body, new Vector3(0.38f, 0.86f, 0f), new Vector3(0.16f, 0.5f, 0.2f), tint, new Vector3(0f, 0f, 8f)),
                Block(PrimitiveType.Cube, "Hood", body, new Vector3(0f, 1.38f, 0f), new Vector3(0.44f, 0.42f, 0.42f), tint),
                Block(PrimitiveType.Cube, "HoodKnot", body, new Vector3(0f, 1.44f, -0.24f), new Vector3(0.14f, 0.14f, 0.1f), tint),
            };
            Block(PrimitiveType.Cube, "LegL", body, new Vector3(-0.13f, 0.25f, 0f), new Vector3(0.22f, 0.5f, 0.26f), dark);
            Block(PrimitiveType.Cube, "LegR", body, new Vector3(0.13f, 0.25f, 0f), new Vector3(0.22f, 0.5f, 0.26f), dark);
            Block(PrimitiveType.Cube, "Hips", body, new Vector3(0f, 0.55f, 0f), new Vector3(0.5f, 0.15f, 0.3f), dark);
            Block(PrimitiveType.Cube, "Belt", body, new Vector3(0f, 0.65f, 0f), new Vector3(0.58f, 0.08f, 0.34f), gold);
            Block(PrimitiveType.Cube, "HandL", body, new Vector3(-0.41f, 0.57f, 0f), new Vector3(0.14f, 0.14f, 0.14f), skin);
            Block(PrimitiveType.Cube, "HandR", body, new Vector3(0.41f, 0.57f, 0f), new Vector3(0.14f, 0.14f, 0.14f), skin);
            Block(PrimitiveType.Cube, "FaceSlit", body, new Vector3(0f, 1.4f, 0.21f), new Vector3(0.34f, 0.1f, 0.02f), skin);
            Block(PrimitiveType.Cube, "Blade", body, new Vector3(0.05f, 1.05f, -0.22f), new Vector3(0.06f, 0.85f, 0.04f), steel, new Vector3(0f, 0f, -35f));
            Block(PrimitiveType.Cube, "Hilt", body, new Vector3(0.3f, 1.42f, -0.22f), new Vector3(0.08f, 0.25f, 0.06f), gold, new Vector3(0f, 0f, -35f));

            var marker = BuildPlayerTagCanvas(ninjaRoot, "Marker", new Vector3(0f, 1.95f, 0f), 0.008f, 64f, true);
            var ringCanvas = WorldCanvas(ninjaRoot, "SwirlRing", new Vector3(0f, 0.03f, 0f), 0.01f, new Vector2(300f, 300f));
            ringCanvas.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var ring = NewImage(ringCanvas.transform, "Fill", GetSprite("Ring"), Color.white);
            Stretch(ring.rectTransform);
            ring.type = UnityEngine.UI.Image.Type.Filled;
            ring.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            ring.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;

            Set(rig, "bodyRoot", body);
            SetArray(rig, "colorRenderers", System.Array.ConvertAll(tinted, go => (Object)go.GetComponent<MeshRenderer>()));
            Set(rig, "marker", marker);
            Set(rig, "swirlRingRoot", ringCanvas.gameObject);
            Set(rig, "swirlRing", ring);
            return rig;
        }

        static BruteRig BuildBrute(Transform root, out Transform chest)
        {
            var bruteRoot = Empty("Brute", root, new Vector3(0f, 0f, 4.4f));
            var rig = bruteRoot.gameObject.AddComponent<BruteRig>();
            var body = Empty("Body", bruteRoot, Vector3.zero);
            var grey = Lit("Brute Grey", new Color(0.56f, 0.58f, 0.62f));
            var dark = Lit("Brute Dark", new Color(0.36f, 0.38f, 0.42f));
            var eye = Lit("Brute Eye", new Color(1f, 0.25f, 0.1f), 0.3f, new Color(1.5f, 0.3f, 0.1f));
            Block(PrimitiveType.Cube, "LegL", body, new Vector3(-0.34f, 0.4f, 0f), new Vector3(0.46f, 0.8f, 0.5f), dark);
            Block(PrimitiveType.Cube, "LegR", body, new Vector3(0.34f, 0.4f, 0f), new Vector3(0.46f, 0.8f, 0.5f), dark);
            Block(PrimitiveType.Cube, "Torso", body, new Vector3(0f, 1.35f, 0f), new Vector3(1.4f, 1.1f, 0.8f), grey);
            Block(PrimitiveType.Cube, "Belt", body, new Vector3(0f, 0.86f, 0f), new Vector3(1.3f, 0.18f, 0.82f), dark);
            Block(PrimitiveType.Cube, "ShoulderL", body, new Vector3(-0.86f, 1.78f, 0f), new Vector3(0.5f, 0.42f, 0.62f), dark);
            Block(PrimitiveType.Cube, "ShoulderR", body, new Vector3(0.86f, 1.78f, 0f), new Vector3(0.5f, 0.42f, 0.62f), dark);
            Block(PrimitiveType.Cube, "ArmL", body, new Vector3(-0.8f, 1.3f, -0.25f), new Vector3(0.34f, 0.85f, 0.38f), grey, new Vector3(-35f, 0f, 0f));
            Block(PrimitiveType.Cube, "ArmR", body, new Vector3(0.8f, 1.3f, -0.25f), new Vector3(0.34f, 0.85f, 0.38f), grey, new Vector3(-35f, 0f, 0f));
            Block(PrimitiveType.Cube, "Head", body, new Vector3(0f, 2.22f, 0f), new Vector3(0.72f, 0.62f, 0.66f), grey);
            Block(PrimitiveType.Cube, "EyeL", body, new Vector3(-0.16f, 2.28f, -0.34f), new Vector3(0.14f, 0.08f, 0.04f), eye);
            Block(PrimitiveType.Cube, "EyeR", body, new Vector3(0.16f, 2.28f, -0.34f), new Vector3(0.14f, 0.08f, 0.04f), eye);
            chest = Empty("Chest", body, new Vector3(0f, 1.4f, -0.5f));

            // The staff lies along the pivot's local -Z, reaching past the ninja at yaw 0.
            var pivot = Empty("StaffPivot", body, new Vector3(0f, 1.25f, -0.6f));
            pivot.localRotation = Quaternion.Euler(50f, 0f, 0f);
            var wood = Lit("Staff Wood", new Color(0.45f, 0.27f, 0.13f));
            var edge = Lit("Staff Edge", new Color(1f, 0.9f, 0.2f), 0.5f, new Color(2.2f, 1.6f, 0.2f));
            Block(PrimitiveType.Cylinder, "Shaft", pivot, new Vector3(0f, 0f, -2.2f), new Vector3(0.13f, 2.2f, 0.13f), wood, new Vector3(90f, 0f, 0f));
            Block(PrimitiveType.Cube, "Edge", pivot, new Vector3(0f, 0.07f, -2.25f), new Vector3(0.17f, 0.05f, 4.2f), edge);
            Block(PrimitiveType.Cube, "CapFar", pivot, new Vector3(0f, 0f, -4.4f), new Vector3(0.26f, 0.26f, 0.26f), edge);
            Block(PrimitiveType.Cube, "CapNear", pivot, new Vector3(0f, 0f, 0.05f), new Vector3(0.24f, 0.24f, 0.24f), edge);
            var tip = Empty("Tip", pivot, new Vector3(0f, 0f, -4.4f));
            var trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.widthMultiplier = 0.35f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.sharedMaterial = Particle("Staff Trail", true);
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.emitting = false;

            Set(rig, "bodyRoot", body);
            Set(rig, "staffPivot", pivot);
            Set(rig, "staffTrail", trail);
            Set(rig, "staffTip", tip);
            return rig;
        }

        #endregion

        #region Runner World

        static void BuildRunnerWorld()
        {
            var barrier = BuildObstacleBlock(BarrierPath, Lit("Brick Barrier", new Color(0.88f, 0.32f, 0.2f)),
                Lit("Brick Mortar", new Color(0.95f, 0.9f, 0.78f)));
            var floating = BuildObstacleBlock(FloatingBlockPath, Lit("Floating Block", new Color(0.25f, 0.72f, 0.85f)),
                Lit("Floating Block Edge", new Color(0.12f, 0.4f, 0.55f)));

            var root = new GameObject("RunnerWorld");
            var world = root.AddComponent<RunnerWorld>();
            var camera = NewCamera(root.transform, Vector3.zero, Vector3.zero, 60f);
            camera.farClipPlane = 140f;
            var carPose = Empty("CarCameraPose", root.transform, new Vector3(0f, 3f, -6.5f));
            carPose.localRotation = Quaternion.Euler(8.8f, 0f, 0f);
            // Close behind the craft and centered on its vertical band, so climbs and dives read clearly.
            var skyPose = Empty("SkyCameraPose", root.transform, new Vector3(0f, 3.9f, -5f));
            skyPose.localRotation = Quaternion.Euler(1f, 0f, 0f);
            var road = BuildRoad(root.transform, out var roadStrip);
            var sky = BuildSky(root.transform, out var skyStrip);
            var car = BuildCar(root.transform);
            var skycraft = BuildSkycraft(root.transform);
            var barrierPool = Empty("BarrierPool", root.transform, Vector3.zero).gameObject.AddComponent<ObstacleBlockPool>();
            var floatingPool = Empty("FloatingBlockPool", root.transform, Vector3.zero).gameObject.AddComponent<ObstacleBlockPool>();

            Set(world, "worldCamera", camera);
            Set(world, "carCameraPose", carPose);
            Set(world, "skyCameraPose", skyPose);
            Set(world, "car", car);
            Set(world, "skycraft", skycraft);
            Set(world, "roadScenery", road);
            Set(world, "skyScenery", sky);
            Set(world, "roadStrip", roadStrip);
            Set(world, "skyStrip", skyStrip);
            Set(world, "barrierPool", barrierPool);
            Set(world, "barrierPrefab", barrier);
            Set(world, "floatingBlockPool", floatingPool);
            Set(world, "floatingBlockPrefab", floating);
            SavePrefab(root, RunnerWorldPath);
        }

        static ObstacleBlock BuildObstacleBlock(string path, Material main, Material lines)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = System.IO.Path.GetFileNameWithoutExtension(path);
            Object.DestroyImmediate(root.GetComponent<Collider>());
            root.GetComponent<MeshRenderer>().sharedMaterial = main;
            // Unit cube scaled per cell at spawn; children are thin bands that read as mortar lines and a cap.
            foreach (var y in new[] { -0.18f, 0.16f })
            {
                Block(PrimitiveType.Cube, "Line", root.transform, new Vector3(0f, y, 0f), new Vector3(1.01f, 0.05f, 1.01f), lines);
            }

            Block(PrimitiveType.Cube, "Cap", root.transform, new Vector3(0f, 0.47f, 0f), new Vector3(1.02f, 0.08f, 1.02f), lines);
            root.AddComponent<ObstacleBlock>();
            return SavePrefab(root, path).GetComponent<ObstacleBlock>();
        }

        static GameObject BuildRoad(Transform root, out ScrollingStrip strip)
        {
            var road = Empty("RoadScenery", root, Vector3.zero);
            Block(PrimitiveType.Cube, "Asphalt", road, new Vector3(0f, -0.05f, 55f), new Vector3(6.4f, 0.1f, 140f), Lit("Road", new Color(0.27f, 0.27f, 0.3f)));
            var grass = Lit("Grass", new Color(0.36f, 0.78f, 0.32f));
            Block(PrimitiveType.Cube, "GrassL", road, new Vector3(-23.2f, -0.06f, 55f), new Vector3(40f, 0.1f, 140f), grass);
            Block(PrimitiveType.Cube, "GrassR", road, new Vector3(23.2f, -0.06f, 55f), new Vector3(40f, 0.1f, 140f), grass);
            var curb = Lit("Curb", new Color(0.95f, 0.95f, 0.92f));
            Block(PrimitiveType.Cube, "CurbL", road, new Vector3(-3.3f, 0.03f, 55f), new Vector3(0.25f, 0.16f, 140f), curb);
            Block(PrimitiveType.Cube, "CurbR", road, new Vector3(3.3f, 0.03f, 55f), new Vector3(0.25f, 0.16f, 140f), curb);
            strip = road.gameObject.AddComponent<ScrollingStrip>();
            var items = new System.Collections.Generic.List<Object>();
            var dash = Lit("Lane Line", new Color(0.98f, 0.95f, 0.85f));
            var trunk = Lit("Tree Trunk", new Color(0.45f, 0.28f, 0.15f));
            var leaves = Lit("Tree Leaves", new Color(0.15f, 0.6f, 0.25f));
            for (var z = 0; z < 120; z += 6)
            {
                foreach (var x in new[] { -1f, 1f })
                {
                    items.Add(Block(PrimitiveType.Cube, "Dash", road, new Vector3(x, 0.01f, z), new Vector3(0.12f, 0.02f, 2.2f), dash).transform);
                }
            }

            for (var z = 0; z < 120; z += 10)
            {
                var side = z % 20 == 0 ? -1f : 1f;
                var tree = Empty("Tree", road, new Vector3(side * 5.6f, 0f, z));
                Block(PrimitiveType.Cube, "Trunk", tree, new Vector3(0f, 0.45f, 0f), new Vector3(0.35f, 0.9f, 0.35f), trunk);
                Block(PrimitiveType.Cube, "Leaves", tree, new Vector3(0f, 1.4f, 0f), new Vector3(1.4f, 1.3f, 1.4f), leaves);
                items.Add(tree);
            }

            SetArray(strip, "items", items);
            Set(strip, "wrapLength", property => property.floatValue = 120f);
            Set(strip, "minZ", property => property.floatValue = -10f);
            return road.gameObject;
        }

        static GameObject BuildSky(Transform root, out ScrollingStrip strip)
        {
            var sky = Empty("SkyScenery", root, Vector3.zero);
            Block(PrimitiveType.Cube, "GroundFar", sky, new Vector3(0f, -14f, 60f), new Vector3(240f, 0.2f, 240f), Lit("Ground Far", new Color(0.42f, 0.74f, 0.36f)));
            strip = sky.gameObject.AddComponent<ScrollingStrip>();
            var cloud = Lit("Cloud", Color.white, 0.05f);
            var items = new System.Collections.Generic.List<Object>();
            var placements = new[]
            {
                new Vector3(-8f, 2f, 5f), new Vector3(9f, 5f, 15f), new Vector3(-11f, 6f, 28f), new Vector3(7f, 0.5f, 38f),
                new Vector3(-6f, -1.5f, 50f), new Vector3(12f, 3f, 62f), new Vector3(-9f, 4.5f, 75f), new Vector3(6f, -2f, 88f),
                new Vector3(-13f, 1f, 98f), new Vector3(10f, 7f, 110f),
            };
            for (var i = 0; i < placements.Length; i++)
            {
                var puff = Empty("Cloud", sky, placements[i]);
                Block(PrimitiveType.Sphere, "A", puff, Vector3.zero, new Vector3(3.2f, 1.6f, 2.2f), cloud);
                Block(PrimitiveType.Sphere, "B", puff, new Vector3(1.4f, 0.3f, 0.2f), new Vector3(2.2f, 1.4f, 1.8f), cloud);
                Block(PrimitiveType.Sphere, "C", puff, new Vector3(-1.3f, 0.2f, -0.2f), new Vector3(2f, 1.2f, 1.6f), cloud);
                items.Add(puff);
            }

            SetArray(strip, "items", items);
            Set(strip, "wrapLength", property => property.floatValue = 120f);
            Set(strip, "minZ", property => property.floatValue = -12f);
            return sky.gameObject;
        }

        static VehicleRig BuildCar(Transform root)
        {
            var car = Empty("Car", root, Vector3.zero);
            var rig = car.gameObject.AddComponent<VehicleRig>();
            var body = Empty("Body", car, Vector3.zero);
            var paint = Lit("Car Paint", new Color(1f, 0.72f, 0.1f), 0.45f);
            var dark = Lit("Car Dark", new Color(0.14f, 0.14f, 0.17f));
            var glass = Lit("Car Glass", new Color(0.55f, 0.85f, 1f), 0.8f);
            var light = Lit("Car Light", Color.white, 0.5f, new Color(1.6f, 1.5f, 1.2f));
            Block(PrimitiveType.Cube, "Chassis", body, new Vector3(0f, 0.36f, 0f), new Vector3(1.55f, 0.36f, 2.4f), paint);
            Block(PrimitiveType.Cube, "Cabin", body, new Vector3(0f, 0.7f, -0.15f), new Vector3(1.1f, 0.34f, 1.05f), glass);
            Block(PrimitiveType.Cube, "Roof", body, new Vector3(0f, 0.89f, -0.15f), new Vector3(1.12f, 0.06f, 1.0f), paint);
            Block(PrimitiveType.Cube, "Spoiler", body, new Vector3(0f, 0.8f, -1.1f), new Vector3(1.55f, 0.07f, 0.3f), dark);
            Block(PrimitiveType.Cube, "Bumper", body, new Vector3(0f, 0.24f, 1.22f), new Vector3(1.6f, 0.14f, 0.12f), dark);
            Block(PrimitiveType.Cube, "LightL", body, new Vector3(-0.5f, 0.42f, 1.21f), new Vector3(0.28f, 0.12f, 0.04f), light);
            Block(PrimitiveType.Cube, "LightR", body, new Vector3(0.5f, 0.42f, 1.21f), new Vector3(0.28f, 0.12f, 0.04f), light);
            foreach (var x in new[] { -0.82f, 0.82f })
            {
                foreach (var z in new[] { -0.78f, 0.78f })
                {
                    Block(PrimitiveType.Cylinder, "Wheel", body, new Vector3(x, 0.26f, z), new Vector3(0.52f, 0.15f, 0.52f), dark, new Vector3(0f, 0f, 90f));
                }
            }

            BuildSteerMarkers(rig, car, 1.75f);
            Set(rig, "body", body);
            Set(rig, "bankPerSpeed", property => property.floatValue = 2f);
            return rig;
        }

        static VehicleRig BuildSkycraft(Transform root)
        {
            var craft = Empty("Skycraft", root, Vector3.zero);
            var rig = craft.gameObject.AddComponent<VehicleRig>();
            var body = Empty("Body", craft, Vector3.zero);
            var hull = Lit("Skycraft Hull", new Color(0.96f, 0.96f, 0.98f), 0.5f);
            var trim = Lit("Skycraft Trim", new Color(0.15f, 0.7f, 0.6f), 0.4f);
            var glow = Lit("Skycraft Glow", new Color(0.4f, 0.9f, 1f), 0.5f, new Color(0.6f, 1.6f, 2f));
            Block(PrimitiveType.Cube, "Fuselage", body, Vector3.zero, new Vector3(0.5f, 0.18f, 2.1f), hull);
            Block(PrimitiveType.Cube, "Nose", body, new Vector3(0f, -0.01f, 1.2f), new Vector3(0.34f, 0.14f, 0.35f), trim);
            Block(PrimitiveType.Cube, "Canopy", body, new Vector3(0f, 0.13f, 0.4f), new Vector3(0.3f, 0.1f, 0.6f), glow);
            Block(PrimitiveType.Cube, "Wings", body, new Vector3(0f, -0.02f, 0.05f), new Vector3(1.8f, 0.05f, 0.5f), trim);
            Block(PrimitiveType.Cube, "TailWings", body, new Vector3(0f, 0.02f, -0.92f), new Vector3(0.8f, 0.04f, 0.26f), trim);
            Block(PrimitiveType.Cube, "TailFin", body, new Vector3(0f, 0.2f, -0.92f), new Vector3(0.05f, 0.36f, 0.32f), trim);
            Block(PrimitiveType.Cube, "Engine", body, new Vector3(0f, 0f, -1.08f), new Vector3(0.3f, 0.12f, 0.08f), glow);
            BuildSteerMarkers(rig, craft, 0.75f);
            Set(rig, "body", body);
            return rig;
        }

        static void BuildSteerMarkers(VehicleRig rig, Transform vehicle, float height)
        {
            var markers = new Object[SimulatedBody.MaxPlayers];
            for (var i = 0; i < markers.Length; i++)
            {
                var canvas = WorldCanvas(vehicle, $"SteerMarkerP{i + 1}", new Vector3(i == 0 ? -0.65f : 0.65f, height, 0f), 0.01f, new Vector2(140f, 140f));
                var marker = canvas.gameObject.AddComponent<PlayerSteerMarker>();
                var tag = BuildPlayerTag(canvas.transform, new Vector2(0f, 30f), 56f);
                var arrow = NewImage(canvas.transform, "Arrow", GetSprite("Arrow"), Color.white);
                Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -35f), new Vector2(80f, 50f));
                Set(marker, "playerTag", tag);
                Set(marker, "arrow", arrow.rectTransform);
                Set(marker, "arrowImage", arrow);
                markers[i] = marker;
            }

            SetArray(rig, "markers", markers);
        }

        #endregion

        #region Shared

        static Camera NewCamera(Transform parent, Vector3 position, Vector3 euler, float fieldOfView)
        {
            var go = new GameObject("Camera");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = skyColor;
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            camera.allowMSAA = false;
            camera.allowHDR = false;
            return camera;
        }

        static Canvas WorldCanvas(Transform parent, string name, Vector3 position, float scale, Vector2 size)
        {
            var rect = NewRect(name, parent);
            var canvas = rect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            rect.sizeDelta = size;
            rect.localPosition = position;
            rect.localScale = Vector3.one * scale;
            return canvas;
        }

        static PlayerTagLabel BuildPlayerTagCanvas(Transform parent, string name, Vector3 position, float scale, float fontSize, bool withPointer)
        {
            var canvas = WorldCanvas(parent, name, position, scale, new Vector2(200f, 140f));
            var tag = BuildPlayerTag(canvas.transform, new Vector2(0f, 25f), fontSize);
            if (withPointer)
            {
                var pointer = NewImage(canvas.transform, "Pointer", GetSprite("Arrow"), Color.white);
                Place(pointer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -38f), new Vector2(46f, 40f));
                pointer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            }

            return tag;
        }

        public static PlayerTagLabel BuildPlayerTag(Transform parent, Vector2 position, float fontSize)
        {
            var label = Label(parent, "PlayerTag", null, "P1", fontSize, Color.white, OutlineTextMaterial());
            var rect = (RectTransform)label.transform;
            Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(200f, fontSize * 1.2f));
            var tag = label.gameObject.AddComponent<PlayerTagLabel>();
            Set(tag, "label", label);
            Set(tag, "text", label.GetComponent<TMPro.TextMeshProUGUI>());
            SetLocalized(tag, "tagText", "ninjago.player.tag");
            return tag;
        }

        #endregion
    }
}
