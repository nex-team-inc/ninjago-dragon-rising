#nullable enable

using System.Collections.Generic;
using System.Text;
using Nex.BilliardRogue.Simulation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Presentation-World builder (TDD §13, §17): World/Arena.prefab, Environment/Env_Act{1,2,3}.prefab from
    /// Tools/Blender/environment/layouts.json and Environment/WorldLighting.prefab (ActEnvironmentController, sun,
    /// grading volumes). Creates EnvironmentConfig.asset when missing, seeds each act's lighting preset from the layout
    /// once, and fills empty config slots (ArenaConfig models, ActDefinition environment / volume / ambient VFX,
    /// title volume). Missing models, materials, volumes or VFX degrade to placeholders with warnings.
    /// CLI: unity command eval 'return Nex.BilliardRogue.Editor.EnvironmentBuilder.Run();'
    /// </summary>
    public static class EnvironmentBuilder
    {
        public const string WorldLightingPath = "Assets/Prefabs/BilliardRogue/Environment/WorldLighting.prefab";
        public const string EnvironmentConfigPath = BuilderAssets.ConfigRoot + "/EnvironmentConfig.asset";
        const string ArenaConfigPath = BuilderAssets.ConfigRoot + "/ArenaConfig.asset";
        const string ActPathFormat = BuilderAssets.ConfigRoot + "/Acts/Act_{0}.asset";
        const string VolumePathFormat = "Assets/Settings/BilliardRogue/Volumes/Volume_{0}.asset";
        const string AmbientVfxPathFormat = "Assets/Prefabs/BilliardRogue/Vfx/Vfx_Ambient_Act{0}.prefab";
        const string WorldVolumeLayer = "WorldVolume";

        #region Entry Point

        [MenuItem("Nex/Billiard Rogue/Environment (Arena + Acts)", priority = 56)]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            EnvironmentPieces.Reset();
            var report = new StringBuilder("[EnvironmentBuilder]");
            var arena = AssetDatabase.LoadAssetAtPath<ArenaConfig>(ArenaConfigPath);
            if (arena == null) return report.Append(" ArenaConfig.asset missing: run ConfigAssetsBuilder first.").ToString();

            var layout = EnvironmentLayout.Load(out var layoutError);
            if (layout == null) EnvironmentPieces.Warn($"layouts.json unavailable ({layoutError}); only the arena and lighting are built.");
            var acts = LoadActs();
            var config = LoadOrCreateConfig(out var configCreated);
            report.Append(configCreated ? " EnvironmentConfig created," : "");
            report.Append($" arena slots +{FillArenaSlots(arena)}, presets seeded {SeedLighting(acts, layout)}");

            WorldPrefabModels.BeginStaging();
            try
            {
                report.Append("; ").Append(EnvironmentArenaBuilder.Build(arena, layout?.Act(1)));
                for (var id = 1; id <= SimConstants.ActCount; id++)
                {
                    var act = layout?.Act(id);
                    if (act == null)
                    {
                        EnvironmentPieces.Warn($"layouts.json has no act {id}; Env_Act{id}.prefab not rebuilt.");
                        continue;
                    }

                    report.Append("; ").Append(EnvironmentActBuilder.Build(act, arena));
                }

                BuildWorldLighting(config, acts.Count > 0 ? acts[0] : null);
            }
            finally
            {
                WorldPrefabModels.EndStaging();
            }

            report.Append($"; config slots +{FillActSlots(acts) + FillTitleVolume(config)}");
            AssetDatabase.SaveAssets();
            report.Append($"; warnings {EnvironmentPieces.Warnings.Count}");
            foreach (var warning in EnvironmentPieces.Warnings)
            {
                report.Append("\n  - ").Append(warning);
            }

            Debug.Log(report.ToString());
            return report.ToString();
        }

        /// <summary>
        /// For MainSceneBuilder: wires the WorldLighting instance under world to the Arena, the WorldCameraRig and the
        /// Env_Act instances placed there. Returns false when no ActEnvironmentController is under world.
        /// </summary>
        public static bool WireScene(GameObject world)
        {
            var controller = world.GetComponentInChildren<ActEnvironmentController>(true);
            if (controller == null) return false;
            var environments = new List<ActEnvironment>(world.GetComponentsInChildren<ActEnvironment>(true));
            environments.Sort((a, b) => a.ActIndex.CompareTo(b.ActIndex));
            var so = new SerializedObject(controller);
            so.FindProperty("arena").objectReferenceValue = world.GetComponentInChildren<ArenaView>(true);
            so.FindProperty("cameraRig").objectReferenceValue = world.GetComponentInChildren<WorldCameraRig>(true);
            var list = so.FindProperty("environments");
            list.arraySize = environments.Count;
            for (var i = 0; i < environments.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = environments[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        #endregion

        #region World lighting prefab

        static void BuildWorldLighting(EnvironmentConfig config, ActDefinition? firstAct)
        {
            var root = WorldPrefabModels.NewRoot("WorldLighting", typeof(ActEnvironmentController));
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root.transform, false);
            var sun = sunGo.AddComponent<Light>();
            var preset = firstAct != null ? firstAct.Lighting : config.TitleLighting;
            sun.type = LightType.Directional;
            sun.color = preset.sunColor;
            sun.intensity = preset.sunIntensity;
            sun.transform.localRotation = Quaternion.Euler(preset.sunEuler);
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = preset.shadowStrength;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;
            sun.renderMode = LightRenderMode.ForcePixel;
            sun.lightmapBakeType = LightmapBakeType.Realtime;
            sun.bounceIntensity = 0f;

            var blend = CreateVolume(root.transform, "GradeBlendVolume");
            var so = new SerializedObject(root.GetComponent<ActEnvironmentController>());
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("sun").objectReferenceValue = sun;
            so.FindProperty("blendVolume").objectReferenceValue = blend;
            so.ApplyModifiedPropertiesWithoutUndo();
            WorldPrefabModels.SavePrefab(root, WorldLightingPath);
        }

        static Volume CreateVolume(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var layer = LayerMask.NameToLayer(WorldVolumeLayer);
            if (layer >= 0) go.layer = layer;
            else EnvironmentPieces.Warn($"Layer '{WorldVolumeLayer}' missing (RenderPipelineBuilder); grading volumes stay on Default and the WorldCamera will not see them.");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.weight = 0f;
            volume.priority = 1f;
            volume.enabled = false;
            return volume;
        }

        #endregion

        #region Config slots

        static List<ActDefinition> LoadActs()
        {
            var acts = new List<ActDefinition>();
            for (var id = 1; id <= SimConstants.ActCount; id++)
            {
                var act = AssetDatabase.LoadAssetAtPath<ActDefinition>(string.Format(ActPathFormat, id));
                if (act == null)
                {
                    EnvironmentPieces.Warn($"{string.Format(ActPathFormat, id)} missing (ConfigAssetsBuilder).");
                    continue;
                }

                acts.Add(act);
            }

            return acts;
        }

        static EnvironmentConfig LoadOrCreateConfig(out bool created)
        {
            var config = BuilderAssets.LoadOrCreate<EnvironmentConfig>(EnvironmentConfigPath, out created);
            if (!created) return config;
            FillTitleLook(config.TitleLighting);
            config.TitleLighting.seededFromLayout = true;
            EditorUtility.SetDirty(config);
            return config;
        }

        // Cozy golden hour for the Title: the Act 1 ruins under a lower, warmer sun with stronger god rays.
        static void FillTitleLook(ActLightingPreset look)
        {
            look.sunColor = new Color(1f, 0.74f, 0.46f);
            look.sunIntensity = 1.45f;
            look.sunEuler = new Vector3(36f, 99.2f, 0f);
            look.shadowStrength = 0.72f;
            look.ambientSky = new Color(0.58f, 0.5f, 0.66f);
            look.ambientEquator = new Color(0.52f, 0.4f, 0.42f);
            look.ambientGround = new Color(0.26f, 0.2f, 0.2f);
            look.fogColor = new Color(1f, 0.8f, 0.55f);
            look.fogDensity = 0.01f;
            look.rimColor = new Color(1f, 0.82f, 0.55f);
            look.additionalLightTint = new Color(1f, 0.78f, 0.5f);
            look.godRayColor = new Color(1f, 0.78f, 0.45f);
            look.godRayIntensity = 1.35f;
            look.particleTint = new Color(1f, 0.88f, 0.7f);
        }

        static int SeedLighting(List<ActDefinition> acts, EnvironmentLayout? layout)
        {
            if (layout == null) return 0;
            var seeded = 0;
            foreach (var act in acts)
            {
                var preset = act.Lighting;
                var source = layout.Act(act.Rules.actIndex + 1)?.lighting.preset;
                if (preset.seededFromLayout || source is not { IsValid: true }) continue;
                Undo.RecordObject(act, "Seed act lighting from layouts.json");
                source.CopyTo(preset);
                preset.seededFromLayout = true;
                EditorUtility.SetDirty(act);
                seeded++;
            }

            return seeded;
        }

        static int FillArenaSlots(ArenaConfig arena)
        {
            var so = new SerializedObject(arena);
            var filled = 0;
            filled += FillModel(so, "floorTilePrefab", EnvironmentArenaBuilder.FloorModel);
            filled += FillModel(so, "dangerTilePrefab", EnvironmentArenaBuilder.DangerModel);
            filled += FillModel(so, "wallPrefab", EnvironmentArenaBuilder.WallModel);
            filled += FillModel(so, "cornerPostPrefab", EnvironmentArenaBuilder.CornerModel);
            filled += FillModel(so, "launchPadPrefab", EnvironmentArenaBuilder.LaunchPadModel);
            so.ApplyModifiedPropertiesWithoutUndo();
            if (filled > 0) EditorUtility.SetDirty(arena);
            return filled;
        }

        static int FillModel(SerializedObject so, string property, string piece)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvironmentPieces.ModelRoot}/{piece}.fbx");
            return model != null && BuilderAssets.FillIfNull(so, property, model) ? 1 : 0;
        }

        static int FillActSlots(List<ActDefinition> acts)
        {
            var filled = 0;
            foreach (var act in acts)
            {
                var id = act.Rules.actIndex + 1;
                var so = new SerializedObject(act);
                filled += FillAsset<GameObject>(so, "environmentPrefab", string.Format(EnvironmentActBuilder.PathFormat, id), "Presentation-World");
                filled += FillAsset<VolumeProfile>(so, "volumeProfile", string.Format(VolumePathFormat, $"Act{id}"), "Rendering");
                filled += FillAsset<GameObject>(so, "ambientParticlesPrefab", string.Format(AmbientVfxPathFormat, id), "VFX");
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(act);
            }

            return filled;
        }

        static int FillTitleVolume(EnvironmentConfig config)
        {
            var so = new SerializedObject(config);
            var filled = FillAsset<VolumeProfile>(so, "titleVolumeProfile", string.Format(VolumePathFormat, "Title"), "Rendering");
            so.ApplyModifiedPropertiesWithoutUndo();
            if (filled > 0) EditorUtility.SetDirty(config);
            return filled;
        }

        static int FillAsset<T>(SerializedObject so, string property, string path, string owner) where T : Object
        {
            if (so.FindProperty(property).objectReferenceValue != null) return 0;
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                EnvironmentPieces.Warn($"{path} not built yet ({owner}); {so.targetObject.name}.{property} stays empty.");
                return 0;
            }

            return BuilderAssets.FillIfNull(so, property, asset) ? 1 : 0;
        }

        #endregion
    }
}
