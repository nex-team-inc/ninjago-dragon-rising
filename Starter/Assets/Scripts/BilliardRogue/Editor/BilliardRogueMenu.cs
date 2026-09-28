#nullable enable

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>Menu entries for the Billiard Rogue builders. Build All runs every known builder's static Run() in TDD §17 order, skipping missing ones with a warning.</summary>
    public static class BilliardRogueMenu
    {
        const string Menu = "Nex/Billiard Rogue/";

        // TDD §17 order: RenderPipeline creates the World/WorldVolume layers before any builder assigns them,
        // and LocalizationSeeder runs before the views that bind its keys.
        static readonly string[] BuildOrder =
        {
            "ImportSettingsBuilder",
            "ConfigAssetsBuilder",
            "Es3SettingsRepair",
            "EnumDictionaryRepair",
            "MaterialsBuilder",
            "RenderPipelineBuilder",
            "WorldCameraRigBuilder",
            "FontAssetsBuilder",
            "LocalizationSeeder",
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
        public static void BuildAll()
        {
            var ran = 0;
            foreach (var name in BuildOrder)
            {
                if (RunBuilder(name)) ran++;
            }

            Debug.Log($"[BilliardRogue] Build All finished: {ran}/{BuildOrder.Length} builders ran.");
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
