#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>Menu entries for the Billiard Rogue builders. Build All runs every known builder's static Run() in TDD §17 order, skipping missing ones with a warning.</summary>
    public static class BilliardRogueMenu
    {
        const string Menu = "Nex/Billiard Rogue/";

        // TDD §17 order plus the module dependencies: RenderPipeline creates the World/WorldVolume layers and the
        // World renderer before any builder assigns them, VolumeProfiles precede the rig that ships Volume_Default,
        // and LocalizationSeeder runs before FontAssetsBuilder so the CJK atlas covers every seeded string.
        static readonly string[] BuildOrder =
        {
            "ImportSettingsBuilder",
            "ConfigAssetsBuilder",
            "Es3SettingsRepair",
            "EnumDictionaryRepair",
            "RenderPipelineBuilder",
            "MaterialsBuilder",
            "VolumeProfilesBuilder",
            "WorldCameraRigBuilder",
            "LocalizationSeeder",
            "FontAssetsBuilder",
            "DetectionPrefabsBuilder",
            "InputPrefabsBuilder",
            "WorldPrefabsBuilder",
            "EnvironmentBuilder",
            "VfxPrefabsBuilder",
            "AudioRegistryBuilder",
            "UiViewsBuilder",
            "FlowPrefabsBuilder",
            "MainSceneBuilder",
        };

        [MenuItem(Menu + "Build All", priority = 0)]
        public static void BuildAll() => Debug.Log(RunAll());

        /// <summary>Runs every builder in order; a builder that throws is logged and skipped so the rest still run.</summary>
        public static string RunAll()
        {
            var ran = 0;
            var failed = new List<string>();
            foreach (var name in BuildOrder)
            {
                try
                {
                    if (RunBuilder(name)) ran++;
                }
                catch (Exception e)
                {
                    failed.Add(name);
                    Debug.LogError($"[BilliardRogue] {name} failed: {e}");
                }
            }

            var summary = $"[BilliardRogue] Build All finished: {ran}/{BuildOrder.Length} builders ran"
                          + (failed.Count > 0 ? $", FAILED: {string.Join(", ", failed)}" : "") + ".";
            Debug.Log(summary);
            return summary;
        }

        [MenuItem(Menu + "Config Assets", priority = 20)]
        public static void ConfigAssets() => ConfigAssetsBuilder.Run();

        [MenuItem(Menu + "Repair ES3 Assemblies", priority = 21)]
        public static void RepairEs3() => Debug.Log(Es3SettingsRepair.Run());

        [MenuItem(Menu + "Repair Enum Dictionaries", priority = 22)]
        public static void RepairEnumDictionaries() => EnumDictionaryRepair.Run();

        /// <summary>Runs Nex.BilliardRogue.Editor.&lt;typeName&gt;.Run() by reflection; false when the builder does not exist yet.</summary>
        public static bool RunBuilder(string typeName)
        {
            var type = typeof(BilliardRogueMenu).Assembly.GetType($"Nex.BilliardRogue.Editor.{typeName}");
            if (type == null)
            {
                Debug.LogWarning($"[BilliardRogue] {typeName}: not present, skipped.");
                return false;
            }

            var method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (method == null)
            {
                Debug.LogWarning($"[BilliardRogue] {typeName} has no public static Run(); skipped.");
                return false;
            }

            var result = method.Invoke(null, null);
            Debug.Log($"[BilliardRogue] {typeName}.Run() → {result ?? "done"}");
            return true;
        }
    }
}
