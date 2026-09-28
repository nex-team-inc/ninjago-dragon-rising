#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds Environment/Env_Act{n}.prefab from one act of layouts.json (TDD §17): ground tiles pruned to the world
    /// camera's view, every dressing prop (palette + act surfaces), at most MaxLightsPerAct realtime point lights
    /// (the strongest flagged props, flickering / pulsing through DioramaAnimator), the god-ray shafts and the
    /// AmbientParticles anchor. The root sits at the arena origin and holds ActEnvironment.
    /// </summary>
    public static class EnvironmentActBuilder
    {
        public const string PathFormat = "Assets/Prefabs/BilliardRogue/Environment/Env_Act{0}.prefab";
        public const string CombinedMeshFolder = "Assets/Prefabs/BilliardRogue/Environment/Combined";
        /// <summary>Forward path, 4 lights per object (TDD D2): a handful of dramatic lights, emissive + bloom for the rest.</summary>
        public const int MaxLightsPerAct = 6;
        const string ShaftModel = "Env_LightShaft";
        const float FrustumMarginDeg = 8f;

        static readonly string[] FirePieces = { "Env_WallTorch", "Env_WallTorch_Arcane", "Env_Brazier", "Env_Candles", "Env_StoneLantern" };
        static readonly string[] NoShadowPieces = { "Env_GrassTuft", "Env_Flowers", "Env_Pebbles", "Env_GroundPatch", "Env_PavingPatch", "Env_Mushrooms", "Env_GroundMound", "Env_WaterPool", "Env_WaterPool_Glow" };
        static readonly string[] SwayParts = { "Canopy", "Cloth" };
        static readonly string[] BobParts = { "Water", "Water_Emissive", "Glints_Emissive" };

        sealed class Context
        {
            public LayoutAct act = null!;
            public int layer;
            public readonly Dictionary<string, GameObject?> models = new();
            public readonly List<GameObject?> props = new();
            public readonly List<(Light light, DioramaAnimator.LightKind kind, float intensity, Color color)> lights = new();
            public readonly List<Transform> flames = new();
            public readonly List<Transform> swaying = new();
            public readonly List<Transform> bobbing = new();
            public readonly List<(Renderer renderer, Color color, float intensity)> shafts = new();
            public int groundTiles;
            public int groundPruned;
        }

        #region Build

        /// <summary>Saves Env_Act{act.id}.prefab; returns a one-line summary.</summary>
        public static string Build(LayoutAct act, ArenaConfig arena)
        {
            var ctx = new Context { act = act, layer = WorldLayers.Resolve() };
            var root = WorldPrefabModels.NewRoot($"Env_Act{act.id}", typeof(ActEnvironment), typeof(DioramaAnimator));
            root.GetComponent<DioramaAnimator>().enabled = false;
            root.transform.localScale = Vector3.one * arena.WorldScale;

            BuildGround(ctx, root.transform, ViewPlanes(arena));
            BuildProps(ctx, Group(root.transform, "Props"));
            BuildLights(ctx, Group(root.transform, "Lights"));
            BuildShafts(ctx, Group(root.transform, "LightShafts"));
            var anchor = Group(root.transform, "AmbientParticles");
            // Static dressing (ground tiles, prop bodies) merges into one renderer per material; shafts (per-renderer
            // property blocks) and the animated parts (never static) stay separate.
            var shaftRenderers = new HashSet<Renderer>();
            foreach (var shaft in ctx.shafts) shaftRenderers.Add(shaft.renderer);
            var combined = StaticMeshCombiner.Combine(root, CombinedMeshFolder, $"Env_Act{act.id}", r => shaftRenderers.Contains(r) ? null : "Env");

            WorldLayers.Apply(root, ctx.layer);
            Wire(root, ctx, anchor);
            WorldPrefabModels.SavePrefab(root, string.Format(PathFormat, act.id));
            return $"act {act.id} ({act.name}): {ctx.props.Count} props, {ctx.groundTiles} ground tiles ({ctx.groundPruned} culled), {ctx.lights.Count}/{act.pointLights.Length} lights, {ctx.shafts.Count} shafts, {ctx.flames.Count} flames, {ctx.swaying.Count} sway, {ctx.bobbing.Count} water, combined {combined}";
        }

        static void BuildGround(Context ctx, Transform root, Plane[] view)
        {
            var ground = ctx.act.ground;
            if (ground.tiles.Length == 0 || string.IsNullOrEmpty(ground.piece)) return;
            var parent = Group(root, "Ground");
            var model = ModelFor(ctx, ground.piece);
            var material = EnvironmentPieces.Surface(ground.surface);
            var extents = new Vector3(ground.tileSize, 1f, ground.tileSize);
            foreach (var tile in ground.tiles)
            {
                var position = new Vector3(tile.x, ground.y, tile.z);
                if (!GeometryUtility.TestPlanesAABB(view, new Bounds(position, extents)))
                {
                    ctx.groundPruned++;
                    continue;
                }

                var instance = EnvironmentPieces.Instantiate(model, $"{ground.piece}_{tile.x:0}_{tile.z:0}", parent, position, Quaternion.Euler(0f, tile.rotY, 0f), Vector3.one);
                EnvironmentPieces.SetAllMaterials(instance, material);
                EnvironmentPieces.SetShadows(instance, cast: false);
                EnvironmentPieces.SetStatic(instance);
                ctx.groundTiles++;
            }
        }

        static void BuildProps(Context ctx, Transform parent)
        {
            for (var i = 0; i < ctx.act.props.Length; i++)
            {
                var prop = ctx.act.props[i];
                if (prop.piece == LayoutProp.LightShaftPiece || prop.piece == LayoutProp.AmbientParticlesPiece)
                {
                    ctx.props.Add(null);
                    continue;
                }

                var instance = EnvironmentPieces.Instantiate(ModelFor(ctx, prop.piece), $"{prop.piece}_{i:000}", parent, prop.Position,
                    Quaternion.Euler(0f, prop.rotY, 0f), Vector3.one * prop.UniformScale);
                EnvironmentPieces.SetAllMaterials(instance, EnvironmentPieces.Palette());
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var partName = renderer.gameObject == instance ? "" : renderer.gameObject.name;
                    var surface = ctx.act.SurfaceFor(prop.piece, partName);
                    if (surface.Length == 0 && renderer.gameObject == instance) surface = RootSurface(ctx.act, prop.piece);
                    if (surface.Length > 0) EnvironmentPieces.SetMaterial(renderer, EnvironmentPieces.Surface(surface));
                }

                EnvironmentPieces.SetShadows(instance, System.Array.IndexOf(NoShadowPieces, prop.piece) < 0);
                EnvironmentPieces.SetStatic(instance);
                CollectAnimatedParts(ctx, instance);
                ctx.props.Add(instance);
            }
        }

        static void BuildLights(Context ctx, Transform parent)
        {
            var picked = new List<LayoutPointLight>(ctx.act.pointLights);
            picked.Sort((a, b) => b.Weight.CompareTo(a.Weight));
            if (picked.Count > MaxLightsPerAct) picked.RemoveRange(MaxLightsPerAct, picked.Count - MaxLightsPerAct);
            foreach (var entry in picked)
            {
                var go = new GameObject($"Light_{entry.piece}_{entry.prop:000}");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = EnvironmentLayout.Vec(entry.position, Vector3.zero);
                var light = go.AddComponent<Light>();
                var color = EnvironmentLayout.Col(entry.color, Color.white);
                light.type = LightType.Point;
                light.color = color;
                light.intensity = entry.intensity;
                light.range = entry.range;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.Auto;
                light.lightmapBakeType = LightmapBakeType.Realtime;
                light.bounceIntensity = 0f;
                if (ctx.layer >= 0) light.cullingMask = 1 << ctx.layer;
                var kind = System.Array.IndexOf(FirePieces, entry.piece) >= 0 ? DioramaAnimator.LightKind.Fire : DioramaAnimator.LightKind.Crystal;
                ctx.lights.Add((light, kind, entry.intensity, color));
                if (entry.prop < 0 || entry.prop >= ctx.act.props.Length || ctx.act.props[entry.prop].piece != entry.piece)
                {
                    EnvironmentPieces.Warn($"act {ctx.act.id}: point light prop index {entry.prop} does not point at a {entry.piece}; light kept at its position.");
                }
            }
        }

        static void BuildShafts(Context ctx, Transform parent)
        {
            var model = ModelFor(ctx, ShaftModel);
            var material = EnvironmentPieces.LightShaft();
            for (var i = 0; i < ctx.act.props.Length; i++)
            {
                var prop = ctx.act.props[i];
                if (prop.piece != LayoutProp.LightShaftPiece) continue;
                var mesh = string.IsNullOrEmpty(prop.mesh) ? ShaftModel : prop.mesh;
                var instance = EnvironmentPieces.Instantiate(mesh == ShaftModel ? model : ModelFor(ctx, mesh), $"Shaft_{i:000}", parent, prop.Position,
                    Quaternion.Euler(EnvironmentLayout.Vec(prop.euler, Vector3.zero)), EnvironmentLayout.Vec(prop.size, Vector3.one));
                EnvironmentPieces.SetAllMaterials(instance, material);
                EnvironmentPieces.SetShadows(instance, cast: false, receive: false);
                var renderer = instance.GetComponentInChildren<Renderer>();
                ctx.shafts.Add((renderer, EnvironmentLayout.Col(prop.color, Color.white), prop.intensity > 0f ? prop.intensity : 1f));
            }
        }

        #endregion

        #region Helpers

        static GameObject? ModelFor(Context ctx, string piece)
        {
            if (!ctx.models.TryGetValue(piece, out var model)) ctx.models[piece] = model = EnvironmentPieces.LoadModel(piece);
            return model;
        }

        static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // A lone *_Surface part is merged into the FBX root by the default import: take the act surface listed for it.
        static string RootSurface(LayoutAct act, string piece)
        {
            foreach (var surface in act.surfaces)
            {
                if (surface.piece == piece) return surface.surface;
            }

            return "";
        }

        static void CollectAnimatedParts(Context ctx, GameObject instance)
        {
            var flame = EnvironmentPieces.Part(instance, "Flame");
            // Only flames authored with a pivot at their base scale cleanly (candle flames share the prop origin).
            if (flame != null && flame != instance.transform && flame.localPosition.sqrMagnitude > 1e-6f) ctx.flames.Add(flame);
            foreach (var name in SwayParts)
            {
                var part = EnvironmentPieces.Part(instance, name);
                if (part != null && part != instance.transform) ctx.swaying.Add(part);
            }

            foreach (var name in BobParts)
            {
                var part = EnvironmentPieces.Part(instance, name);
                if (part != null && part != instance.transform) ctx.bobbing.Add(part);
            }
        }

        /// <summary>World camera frustum from the ArenaConfig pose (widened for shake and pixel margin), in the arena frame.</summary>
        static Plane[] ViewPlanes(ArenaConfig arena)
        {
            var rotation = Quaternion.Euler(arena.CameraPitchDeg, 0f, 0f);
            var view = Matrix4x4.TRS(arena.CameraPosition, rotation, new Vector3(1f, 1f, -1f)).inverse;
            var projection = Matrix4x4.Perspective(arena.CameraFov + FrustumMarginDeg, 16f / 9f * 1.08f, 0.3f, 200f);
            return GeometryUtility.CalculateFrustumPlanes(projection * view);
        }

        static void Wire(GameObject root, Context ctx, Transform anchor)
        {
            var animatorSo = new SerializedObject(root.GetComponent<DioramaAnimator>());
            var lights = animatorSo.FindProperty("lights");
            lights.arraySize = ctx.lights.Count;
            for (var i = 0; i < ctx.lights.Count; i++)
            {
                var element = lights.GetArrayElementAtIndex(i);
                var (light, kind, intensity, color) = ctx.lights[i];
                element.FindPropertyRelative("light").objectReferenceValue = light;
                element.FindPropertyRelative("kind").intValue = (int)kind;
                element.FindPropertyRelative("baseIntensity").floatValue = intensity;
                element.FindPropertyRelative("baseColor").colorValue = color;
                element.FindPropertyRelative("phase").floatValue = i * 7.31f;
            }

            WriteParts(animatorSo.FindProperty("flames"), ctx.flames, 3.7f);
            WriteParts(animatorSo.FindProperty("swaying"), ctx.swaying, 1.9f);
            WriteParts(animatorSo.FindProperty("bobbing"), ctx.bobbing, 2.3f);
            animatorSo.ApplyModifiedPropertiesWithoutUndo();

            var environmentSo = new SerializedObject(root.GetComponent<ActEnvironment>());
            environmentSo.FindProperty("actIndex").intValue = ctx.act.id - 1;
            environmentSo.FindProperty("animator").objectReferenceValue = root.GetComponent<DioramaAnimator>();
            environmentSo.FindProperty("ambientAnchor").objectReferenceValue = anchor;
            var shafts = environmentSo.FindProperty("lightShafts");
            shafts.arraySize = ctx.shafts.Count;
            for (var i = 0; i < ctx.shafts.Count; i++)
            {
                var element = shafts.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("renderer").objectReferenceValue = ctx.shafts[i].renderer;
                element.FindPropertyRelative("color").colorValue = ctx.shafts[i].color;
                element.FindPropertyRelative("intensity").floatValue = ctx.shafts[i].intensity;
            }

            WriteSurfaces(environmentSo.FindProperty("arenaSurfaces"), ctx.act);
            environmentSo.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WriteParts(SerializedProperty array, List<Transform> parts, float phaseStep)
        {
            array.arraySize = parts.Count;
            for (var i = 0; i < parts.Count; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("part").objectReferenceValue = parts[i];
                element.FindPropertyRelative("phase").floatValue = i * phaseStep;
            }
        }

        // EnumDictionary<ArenaSurface, Material>: every key in ascending order (TDD D11); slots without a surface stay empty.
        static void WriteSurfaces(SerializedProperty dictionary, LayoutAct act)
        {
            var pairs = dictionary.FindPropertyRelative("pairs");
            var keys = (ArenaSurface[])System.Enum.GetValues(typeof(ArenaSurface));
            System.Array.Sort(keys);
            pairs.arraySize = keys.Length;
            for (var i = 0; i < keys.Length; i++)
            {
                var pair = pairs.GetArrayElementAtIndex(i);
                pair.FindPropertyRelative("key").intValue = (int)keys[i];
                var surface = "";
                foreach (var (piece, part, slot) in EnvironmentArenaBuilder.SurfaceParts)
                {
                    if (slot == keys[i]) surface = act.SurfaceFor(piece, part);
                }

                pair.FindPropertyRelative("value").objectReferenceValue = surface.Length > 0 ? EnvironmentPieces.Surface(surface) : null;
            }
        }

        #endregion
    }
}
