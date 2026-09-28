#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/BilliardRogue/Tests/RenderTest.unity: the WorldCameraRig prefab, a sun with hard shadows,
    /// two coloured point lights, a world-UV surface floor, staged palette models (their *_Surface parts take the
    /// surface materials like the arena kit), balls, a 10 m light shaft, an aim guide and an emissive probe —
    /// everything needed to judge the HD-2D look. Not in Build Settings. Refuses to run while the open scene has
    /// unsaved changes and reopens it afterwards.
    /// </summary>
    public static class RenderTestSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/BilliardRogue/Tests/RenderTest.unity";
        const string ModelRoot = "Assets/Models/BilliardRogue";
        const string MaterialRoot = MaterialsBuilder.MaterialRoot;
        const string SurfaceSuffix = "_Surface";
        const string SideSurfacePrefix = "Side";

        sealed class Placement
        {
            public string model = "";
            public string name = "";
            public Vector3 position;
            public float yaw;
            public Vector3 placeholderScale = Vector3.one;
        }

        sealed class MaterialSet
        {
            public Material palette = null!;
            public Material topSurface = null!;
            public Material sideSurface = null!;
        }

        static readonly Placement[] Placements =
        {
            new() { model = "Player/Cat_Hero.fbx", name = "Cat", position = new Vector3(0f, 0f, 0.6f), yaw = 0f, placeholderScale = new Vector3(0.6f, 0.9f, 0.6f) },
            new() { model = "Enemies/Enemy_Slime.fbx", name = "Slime", position = new Vector3(-2f, 0f, 4.5f), yaw = 180f, placeholderScale = new Vector3(0.8f, 0.7f, 0.8f) },
            new() { model = "Enemies/Enemy_Skeleton.fbx", name = "Skeleton", position = new Vector3(1f, 0f, 3.5f), yaw = 180f, placeholderScale = new Vector3(0.6f, 1f, 0.6f) },
            new() { model = "Enemies/Enemy_Mage.fbx", name = "Mage", position = new Vector3(-0.5f, 0f, 6.5f), yaw = 180f, placeholderScale = new Vector3(0.6f, 1f, 0.6f) },
            new() { model = "Enemies/Enemy_Totem.fbx", name = "Totem", position = new Vector3(2.5f, 0f, 7.5f), yaw = 180f, placeholderScale = new Vector3(0.6f, 1.4f, 0.6f) },
            new() { model = "Bosses/Boss_CrystalGolem.fbx", name = "Golem", position = new Vector3(1f, 0f, 9.5f), yaw = 180f, placeholderScale = new Vector3(1.8f, 1.8f, 1.8f) },
            new() { model = "Props/Prop_Crate.fbx", name = "Crate", position = new Vector3(-2.5f, 0f, 8.5f), yaw = 0f, placeholderScale = new Vector3(0.8f, 0.8f, 0.8f) },
            new() { model = "Props/Prop_Pillar.fbx", name = "Pillar", position = new Vector3(3f, 0f, 5.5f), yaw = 0f, placeholderScale = new Vector3(0.7f, 1.5f, 0.7f) },
            new() { model = "Environment/Env_WallTorch.fbx", name = "Torch", position = new Vector3(3.6f, 0f, 4f), yaw = -90f, placeholderScale = new Vector3(0.3f, 1.2f, 0.3f) },
            new() { model = "Environment/Env_Crystal_A.fbx", name = "Crystal", position = new Vector3(-3.4f, 0f, 8f), yaw = 30f, placeholderScale = new Vector3(0.7f, 1.2f, 0.7f) },
            new() { model = "Environment/Env_Tree_A.fbx", name = "Tree", position = new Vector3(-4.6f, 0f, 11f), yaw = 0f, placeholderScale = new Vector3(1.5f, 3f, 1.5f) },
            new() { model = "Environment/Env_RuinArch.fbx", name = "Arch", position = new Vector3(3.5f, 0f, 11.5f), yaw = 0f, placeholderScale = new Vector3(2f, 3f, 0.6f) },
            new() { model = "Environment/Env_WallSegment.fbx", name = "WallL", position = new Vector3(-4.2f, 0f, 5f), yaw = 90f, placeholderScale = new Vector3(0.4f, 1f, 2f) },
            new() { model = "Environment/Env_WallSegment.fbx", name = "WallR", position = new Vector3(4.2f, 0f, 8f), yaw = -90f, placeholderScale = new Vector3(0.4f, 1f, 2f) },
        };

        [MenuItem("Nex/Billiard Rogue/Render Test Scene", priority = 34)]
        static void Menu() => Debug.Log(Run());

        #region Public Methods

        public static string Run()
        {
            var active = SceneManager.GetActiveScene();
            if (active.isDirty) return "[RenderTestSceneBuilder] refused: the open scene has unsaved changes.";
            var previousPath = active.path;
            var report = new StringBuilder("[RenderTestSceneBuilder]");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Populate(report);
            BuilderAssets.EnsureFolder(System.IO.Path.GetDirectoryName(ScenePath)!.Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, ScenePath);
            report.Append($" saved {ScenePath} roots={scene.rootCount}");
            if (!string.IsNullOrEmpty(previousPath)) EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);
            return report.ToString();
        }

        #endregion

        #region Scene

        static void Populate(StringBuilder report)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.42f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.16f, 0.14f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.6f, 0.7f, 0.75f);
            RenderSettings.fogDensity = 0.012f;

            var layer = LayerMask.NameToLayer(RenderPipelineBuilder.WorldLayerName);
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(WorldCameraRigBuilder.PrefabPath);
            if (rig != null)
            {
                PrefabUtility.InstantiatePrefab(rig);
            }
            else
            {
                Debug.LogWarning("[RenderTestSceneBuilder] WorldCameraRig.prefab missing (WorldCameraRigBuilder); scene has no camera.");
            }

            var set = new MaterialSet
            {
                palette = LoadMaterial("M_Palette"),
                topSurface = LoadMaterial("M_Surface_StoneFloor"),
                sideSurface = LoadMaterial("M_Surface_MossyBrick"),
            };
            BuildLights(layer);
            var world = new GameObject("World").transform;
            BuildFloor(world, layer, set);
            foreach (var placement in Placements)
            {
                PlaceModel(placement, world, layer, set, report);
            }

            BuildBalls(world, layer);
            BuildLightShaft(world, layer);
            BuildAimGuide(world, layer);
            BuildEmissiveProbe(world, layer);
        }

        static void BuildLights(int layer)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.8f);
            sun.intensity = 1.15f;
            // From the upper left, slightly towards the camera: tops and camera-facing sides sit in different bands and
            // the hard shadows fall to the right where the floor shows them.
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(0.55f, -0.7f, -0.45f).normalized);
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 1f;
            SetLayer(sun.gameObject, layer);

            var torch = new GameObject("TorchLight").AddComponent<Light>();
            torch.type = LightType.Point;
            torch.color = new Color(1f, 0.6f, 0.25f);
            torch.intensity = 7f;
            torch.range = 5f;
            torch.transform.position = new Vector3(3.2f, 1.6f, 4f);
            SetLayer(torch.gameObject, layer);

            var crystal = new GameObject("CrystalLight").AddComponent<Light>();
            crystal.type = LightType.Point;
            crystal.color = new Color(0.4f, 0.8f, 1f);
            crystal.intensity = 6f;
            crystal.range = 5f;
            crystal.transform.position = new Vector3(-3.2f, 1.2f, 8f);
            SetLayer(crystal.gameObject, layer);
        }

        static void BuildFloor(Transform parent, int layer, MaterialSet set)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor_StoneFloor";
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.transform.SetParent(parent, false);
            floor.transform.position = new Vector3(0f, 0f, 6f);
            floor.transform.localScale = new Vector3(1.4f, 1f, 1.8f);
            // Primitives have no 1 UV = 1 m mapping, so the plane exercises the _WORLD_UV box projection.
            floor.GetComponent<MeshRenderer>().sharedMaterial = PersistedVariant("M_RenderTest_FloorWorldUv", set.topSurface, material =>
            {
                material.EnableKeyword("_WORLD_UV");
                if (material.HasProperty("_UseWorldUv")) material.SetFloat("_UseWorldUv", 1f);
            });
            SetLayer(floor, layer);

            var tiles = new GameObject("FloorTiles").transform;
            tiles.SetParent(parent, false);
            var tile = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelRoot}/Environment/Env_FloorTile.fbx");
            if (tile == null) return;
            for (var x = -3; x <= 3; x++)
            {
                for (var z = 1; z <= 4; z++)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(tile, tiles);
                    instance.name = $"Tile_{x}_{z}";
                    instance.transform.position = new Vector3(x, 0.001f, z + 0.5f);
                    ApplyMaterials(instance, set);
                    SetLayer(instance, layer);
                }
            }
        }

        static void PlaceModel(Placement placement, Transform parent, int layer, MaterialSet set, StringBuilder report)
        {
            var container = new GameObject(placement.name);
            container.transform.SetParent(parent, false);
            container.transform.position = placement.position;
            container.transform.rotation = Quaternion.Euler(0f, placement.yaw, 0f);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelRoot}/{placement.model}");
            if (model != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, container.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                ApplyMaterials(instance, set);
            }
            else
            {
                report.Append($" placeholder:{placement.name}");
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Placeholder";
                UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(container.transform, false);
                cube.transform.localPosition = new Vector3(0f, placement.placeholderScale.y * 0.5f, 0f);
                cube.transform.localScale = placement.placeholderScale;
                cube.GetComponent<MeshRenderer>().sharedMaterial = set.palette;
            }

            SetLayer(container, layer);
        }

        static void BuildBalls(Transform parent, int layer)
        {
            var ballModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelRoot}/Balls/Ball.fbx");
            var types = new[] { "Basic", "Flame", "Frost", "Thunder" };
            for (var i = 0; i < types.Length; i++)
            {
                GameObject ball;
                if (ballModel != null)
                {
                    ball = (GameObject)PrefabUtility.InstantiatePrefab(ballModel, parent);
                }
                else
                {
                    ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    UnityEngine.Object.DestroyImmediate(ball.GetComponent<Collider>());
                    ball.transform.SetParent(parent, false);
                }

                ball.name = $"Ball_{types[i]}";
                ball.transform.position = new Vector3(-1.5f + i, 0.25f, 1.6f + i * 0.4f);
                ball.transform.localScale = Vector3.one * 0.5f;
                SetAllMaterials(ball, LoadMaterial($"M_Ball_{types[i]}"));
                SetLayer(ball, layer);
            }
        }

        // Env_LightShaft is a unit cone with its pivot at the source; layouts scale it to (width, length, width) and aim
        // it with Euler angles, so the test uses the Act 1 layout's shaft pose brought into view.
        static void BuildLightShaft(Transform parent, int layer)
        {
            var material = LoadMaterial("M_LightShaft");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelRoot}/Environment/Env_LightShaft.fbx");
            GameObject shaft;
            if (model != null)
            {
                shaft = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
                shaft.transform.localScale = new Vector3(0.85f, 10f, 0.85f);
            }
            else
            {
                shaft = GameObject.CreatePrimitive(PrimitiveType.Quad);
                UnityEngine.Object.DestroyImmediate(shaft.GetComponent<Collider>());
                shaft.transform.SetParent(parent, false);
                shaft.transform.localScale = new Vector3(1.5f, 10f, 1f);
            }

            shaft.name = "LightShaft";
            shaft.transform.position = new Vector3(-6.5f, 6.7f, 9f);
            shaft.transform.rotation = Quaternion.Euler(41.1f, 279.16f, 0f);
            SetAllMaterials(shaft, material);
            SetLayer(shaft, layer);
            foreach (var renderer in shaft.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        static void BuildAimGuide(Transform parent, int layer)
        {
            var go = new GameObject("AimGuide");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.12f;
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = LoadMaterial("M_AimGuide");
            line.positionCount = 3;
            line.SetPositions(new[] { new Vector3(0f, 0.15f, 0.8f), new Vector3(2.4f, 0.15f, 4.2f), new Vector3(-1.6f, 0.15f, 9f) });
            line.startColor = new Color(0.6f, 0.9f, 1f, 1f);
            line.endColor = new Color(0.6f, 0.9f, 1f, 1f);
            SetLayer(go, layer);
        }

        // Guarantees a bloom source even while the palette emission map is subtle.
        static void BuildEmissiveProbe(Transform parent, int layer)
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            probe.name = "EmissiveProbe";
            UnityEngine.Object.DestroyImmediate(probe.GetComponent<Collider>());
            probe.transform.SetParent(parent, false);
            probe.transform.position = new Vector3(2.2f, 0.9f, 6.2f);
            probe.transform.localScale = Vector3.one * 0.45f;
            probe.GetComponent<MeshRenderer>().sharedMaterial = PersistedVariant("M_RenderTest_Emissive", LoadMaterial("M_DangerTile"), material =>
            {
                material.SetColor("_BaseColor", new Color(0.2f, 0.5f, 1f));
                material.SetColor("_EmissionColor", new Color(0.3f, 0.7f, 1f) * 3f);
                material.SetFloat("_EmissionStrength", 1f);
            });
            SetLayer(probe, layer);
        }

        #endregion

        #region Helpers

        static Material LoadMaterial(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialRoot}/{name}.mat");
            if (material != null) return material;
            Debug.LogWarning($"[RenderTestSceneBuilder] {name}.mat missing (MaterialsBuilder); using URP Lit.");
            return new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = $"{name}_Fallback" };
        }

        // Scene renderers cannot reference unsaved materials, so test-only variants live next to the generated textures.
        static Material PersistedVariant(string name, Material source, Action<Material> configure)
        {
            var path = $"{MaterialsBuilder.GeneratedRoot}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                BuilderAssets.EnsureFolder(MaterialsBuilder.GeneratedRoot);
                material = new Material(source) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.CopyPropertiesFromMaterial(source);
                material.shader = source.shader;
            }

            configure(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Mirrors the arena kit convention: *_Surface parts carry 1 UV = 1 m and take the tiling surface materials.
        static void ApplyMaterials(GameObject root, MaterialSet set)
        {
            var renderers = new List<Renderer>();
            root.GetComponentsInChildren(true, renderers);
            foreach (var renderer in renderers)
            {
                var material = set.palette;
                if (renderer.name.EndsWith(SurfaceSuffix, StringComparison.Ordinal))
                {
                    material = renderer.name.StartsWith(SideSurfacePrefix, StringComparison.Ordinal) ? set.sideSurface : set.topSurface;
                }

                SetMaterial(renderer, material);
            }
        }

        static void SetAllMaterials(GameObject root, Material material)
        {
            var renderers = new List<Renderer>();
            root.GetComponentsInChildren(true, renderers);
            foreach (var renderer in renderers)
            {
                SetMaterial(renderer, material);
            }
        }

        static void SetMaterial(Renderer renderer, Material material)
        {
            var materials = renderer.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials.Length == 0 ? new[] { material } : materials;
        }

        static void SetLayer(GameObject root, int layer)
        {
            if (layer < 0) return;
            root.layer = layer;
            var t = root.transform;
            for (var i = 0; i < t.childCount; i++)
            {
                SetLayer(t.GetChild(i).gameObject, layer);
            }
        }

        #endregion
    }
}
