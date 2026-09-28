#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds World/Arena.prefab (TDD §17) from ArenaConfig: ArenaLayout + ArenaView at the root (origin = launch-line
    /// centre, TDD §14.1), columns × rows floor cells, the danger row with its DangerRowPulse, the launch pad, rim
    /// walls and corner posts. The kit's *_Surface parts start with Act 1's surfaces and are swapped per act.
    /// </summary>
    public static class EnvironmentArenaBuilder
    {
        public const string ArenaPath = "Assets/Prefabs/BilliardRogue/World/Arena.prefab";
        public const string FloorModel = "Env_FloorTile";
        public const string DangerModel = "Env_FloorTile_Danger";
        public const string LaunchPadModel = "Env_LaunchPad";
        public const string WallModel = "Env_WallSegment";
        public const string CornerModel = "Env_WallCorner";
        const string TopSurface = "Top_Surface";
        const string SideSurface = "Side_Surface";
        const string DangerInlay = "DangerInlay_Emissive";
        const float LaunchPadAuthoredWidth = 7f;
        const float LaunchPadAuthoredDepth = 1.6f;
        const float CornerInset = 0.22f;

        /// <summary>Arena kit part → surface slot (layouts.json "surfaces" piece/part pairs).</summary>
        public static readonly (string piece, string part, ArenaSurface slot)[] SurfaceParts =
        {
            (FloorModel, TopSurface, ArenaSurface.FloorTop),
            (DangerModel, TopSurface, ArenaSurface.DangerTop),
            (LaunchPadModel, TopSurface, ArenaSurface.LaunchPadTop),
            (WallModel, SideSurface, ArenaSurface.WallSide),
            (WallModel, TopSurface, ArenaSurface.WallTop),
            (CornerModel, SideSurface, ArenaSurface.CornerSide),
        };

        sealed class Context
        {
            public float scale;
            public int layer;
            public LayoutAct? firstAct;
            public readonly Dictionary<ArenaSurface, List<Renderer>> surfaces = new();
            public readonly List<Renderer> inlays = new();
            public int pieces;
        }

        #region Build

        /// <summary>Saves Arena.prefab; returns a one-line summary.</summary>
        public static string Build(ArenaConfig config, LayoutAct? firstAct)
        {
            var rules = config.Rules;
            var ctx = new Context { scale = config.WorldScale, layer = WorldLayers.Resolve(), firstAct = firstAct };
            var root = WorldPrefabModels.NewRoot("Arena", typeof(ArenaLayout), typeof(ArenaView));

            var floor = Group(root.transform, "Floor");
            var dangerRoot = Group(root.transform, "DangerRow");
            var danger = dangerRoot.gameObject.AddComponent<DangerRowPulse>();
            danger.enabled = false;
            var zTop = rules.launchZoneHeight + rules.rows;
            var half = rules.columns * 0.5f;
            var floorModel = Model(config.FloorTilePrefab, FloorModel);
            var dangerModel = Model(config.DangerTilePrefab, DangerModel);
            for (var row = 0; row < rules.rows; row++)
            {
                var isDanger = row == rules.rows - 1;
                var z = zTop - row - 0.5f;
                for (var col = 0; col < rules.columns; col++)
                {
                    var x = col - half + 0.5f;
                    var parent = isDanger ? dangerRoot : floor;
                    var piece = Place(ctx, isDanger ? dangerModel : floorModel, isDanger ? DangerModel : FloorModel, $"Cell_{col}_{row}", parent, x, z, 0f, Vector3.one, castShadows: false);
                    if (!isDanger) continue;
                    var inlay = EnvironmentPieces.PartRenderer(piece, DangerInlay);
                    if (inlay == null)
                    {
                        EnvironmentPieces.Warn($"{DangerModel} has no {DangerInlay} part; the danger pulse drives the whole tile.");
                        inlay = piece.GetComponentInChildren<Renderer>();
                    }

                    EnvironmentPieces.SetMaterial(inlay, EnvironmentPieces.DangerTile());
                    ctx.inlays.Add(inlay);
                }
            }

            var padScale = new Vector3(rules.columns / LaunchPadAuthoredWidth, 1f, rules.launchZoneHeight / LaunchPadAuthoredDepth);
            Place(ctx, Model(config.LaunchPadPrefab, LaunchPadModel), LaunchPadModel, "LaunchPad", root.transform, 0f, rules.launchZoneHeight * 0.5f, 0f, padScale, castShadows: false);
            BuildWalls(ctx, config, root.transform, half, zTop);

            WireArena(root, danger, ctx);
            WorldLayers.Apply(root, ctx.layer);
            WorldPrefabModels.SavePrefab(root, ArenaPath);
            return $"arena {ctx.pieces} pieces ({rules.columns}x{rules.rows}, danger row {ctx.inlays.Count} inlays)";
        }

        static void BuildWalls(Context ctx, ArenaConfig config, Transform root, float half, float zTop)
        {
            var walls = Group(root, "Walls");
            var wallModel = Model(config.WallPrefab, WallModel);
            var lowest = zTop - 0.5f;
            for (var z = zTop - 0.5f; z > -0.5f; z -= 1f)
            {
                Place(ctx, wallModel, WallModel, $"Wall_L_{z:0.0}", walls, -half, z, 90f, Vector3.one, castShadows: true);
                Place(ctx, wallModel, WallModel, $"Wall_R_{z:0.0}", walls, half, z, 270f, Vector3.one, castShadows: true);
                lowest = z;
            }

            for (var col = 0; col < config.Rules.columns; col++)
            {
                Place(ctx, wallModel, WallModel, $"Wall_T_{col}", walls, col - half + 0.5f, zTop, 180f, Vector3.one, castShadows: true);
            }

            var corners = Group(root, "Corners");
            var cornerModel = Model(config.CornerPostPrefab, CornerModel);
            var bottom = lowest - 0.5f - CornerInset;
            Place(ctx, cornerModel, CornerModel, "Corner_TopLeft", corners, -half - CornerInset, zTop + CornerInset, 0f, Vector3.one, castShadows: true);
            Place(ctx, cornerModel, CornerModel, "Corner_TopRight", corners, half + CornerInset, zTop + CornerInset, 0f, Vector3.one, castShadows: true);
            Place(ctx, cornerModel, CornerModel, "Post_BottomLeft", corners, -half - CornerInset, bottom, 0f, Vector3.one, castShadows: true);
            Place(ctx, cornerModel, CornerModel, "Post_BottomRight", corners, half + CornerInset, bottom, 0f, Vector3.one, castShadows: true);
        }

        #endregion

        #region Helpers

        static GameObject? Model(GameObject? configured, string piece)
        {
            return configured != null ? configured : EnvironmentPieces.LoadModel(piece);
        }

        static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // Positions are in cells (arena frame); ArenaLayout maps one cell to worldScale units, so pieces scale with it.
        static GameObject Place(Context ctx, GameObject? model, string piece, string name, Transform parent, float x, float z, float rotY, Vector3 scale, bool castShadows)
        {
            var s = ctx.scale;
            var instance = EnvironmentPieces.Instantiate(model, name, parent, new Vector3(x * s, 0f, z * s), Quaternion.Euler(0f, rotY, 0f), scale * s);
            EnvironmentPieces.SetAllMaterials(instance, EnvironmentPieces.Palette());
            foreach (var (surfacePiece, part, slot) in SurfaceParts)
            {
                if (surfacePiece != piece) continue;
                var renderer = EnvironmentPieces.PartRenderer(instance, part);
                if (renderer == null) continue;
                var surface = ctx.firstAct?.SurfaceFor(piece, part) ?? "";
                if (surface.Length > 0) EnvironmentPieces.SetMaterial(renderer, EnvironmentPieces.Surface(surface));
                if (!ctx.surfaces.TryGetValue(slot, out var list)) ctx.surfaces[slot] = list = new List<Renderer>();
                list.Add(renderer);
            }

            EnvironmentPieces.SetShadows(instance, castShadows);
            EnvironmentPieces.SetStatic(instance);
            ctx.pieces++;
            return instance;
        }

        static void WireArena(GameObject root, DangerRowPulse danger, Context ctx)
        {
            var dangerSo = new SerializedObject(danger);
            var inlays = dangerSo.FindProperty("inlays");
            inlays.arraySize = ctx.inlays.Count;
            for (var i = 0; i < ctx.inlays.Count; i++)
            {
                inlays.GetArrayElementAtIndex(i).objectReferenceValue = ctx.inlays[i];
            }

            dangerSo.ApplyModifiedPropertiesWithoutUndo();

            var viewSo = new SerializedObject(root.GetComponent<ArenaView>());
            viewSo.FindProperty("layout").objectReferenceValue = root.GetComponent<ArenaLayout>();
            viewSo.FindProperty("dangerRow").objectReferenceValue = danger;
            var surfaces = viewSo.FindProperty("surfaces");
            surfaces.arraySize = SurfaceParts.Length;
            for (var i = 0; i < SurfaceParts.Length; i++)
            {
                var slot = SurfaceParts[i].slot;
                var element = surfaces.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("surface").intValue = (int)slot;
                var renderers = element.FindPropertyRelative("renderers");
                var list = ctx.surfaces.TryGetValue(slot, out var found) ? found : new List<Renderer>();
                renderers.arraySize = list.Count;
                for (var r = 0; r < list.Count; r++)
                {
                    renderers.GetArrayElementAtIndex(r).objectReferenceValue = list[r];
                }
            }

            viewSo.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
    }
}
