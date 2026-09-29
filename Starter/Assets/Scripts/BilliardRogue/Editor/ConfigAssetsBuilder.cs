#nullable enable

using System.Text;
using Nex.BilliardRogue.Simulation;
using Nex.Util;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Creates every config asset of TDD §4 under Assets/Configs/BilliardRogue, only when missing, and wires the
    /// catalogs and the root config (null slots only). Designers own the values afterwards.
    /// CLI: unity command eval 'return Nex.BilliardRogue.Editor.ConfigAssetsBuilder.Run();'
    /// </summary>
    public static class ConfigAssetsBuilder
    {
        const string Root = BuilderAssets.ConfigRoot;

        #region Entry Point

        public static string Run()
        {
            var report = new StringBuilder("[ConfigAssetsBuilder]");
            BuilderAssets.EnsureFolder(Root);

            var balls = BallDefinitionsBuilder.Build(out var ballsCreated);
            var enemies = EnemyDefinitionsBuilder.Build(out var enemiesCreated);
            var acts = ActDefinitionsBuilder.Build(out var actsCreated);
            report.Append($" balls +{ballsCreated}, enemies +{enemiesCreated}, acts +{actsCreated}");

            var arena = BuilderAssets.LoadOrCreate<ArenaConfig>($"{Root}/ArenaConfig.asset", out var arenaCreated);
            var balance = BuilderAssets.LoadOrCreate<BalanceConfig>($"{Root}/BalanceConfig.asset", out var balanceCreated);
            var control = BuilderAssets.LoadOrCreate<ControlConfig>($"{Root}/ControlConfig.asset", out var controlCreated);
            var pacing = BuilderAssets.LoadOrCreate<PacingConfig>($"{Root}/PacingConfig.asset", out var pacingCreated);
            var juice = BuilderAssets.LoadOrCreate<JuiceConfig>($"{Root}/JuiceConfig.asset", out var juiceCreated);
            var hype = BuilderAssets.LoadOrCreate<HypeConfig>($"{Root}/HypeConfig.asset", out var hypeCreated);
            var visual = BuilderAssets.LoadOrCreate<HD2DVisualConfig>($"{Root}/HD2DVisualConfig.asset", out var visualCreated);
            if (visualCreated) FillVisualDefaults(visual);
            var singles = (arenaCreated ? 1 : 0) + (balanceCreated ? 1 : 0) + (controlCreated ? 1 : 0) + (pacingCreated ? 1 : 0) + (juiceCreated ? 1 : 0) + (visualCreated ? 1 : 0)
                          + (hypeCreated ? 1 : 0);
            report.Append($", singles +{singles}");

            var ballCatalog = BuilderAssets.LoadOrCreate<BallCatalog>($"{Root}/BallCatalog.asset", out _);
            var enemyCatalog = BuilderAssets.LoadOrCreate<EnemyCatalog>($"{Root}/EnemyCatalog.asset", out _);
            var fieldObjectCatalog = BuilderAssets.LoadOrCreate<FieldObjectCatalog>($"{Root}/FieldObjectCatalog.asset", out _);
            var ballsWired = WireCatalog<BallType>(ballCatalog, balls);
            var enemiesWired = WireCatalog<EnemyType>(enemyCatalog, enemies);
            EnsureAllKeys<FieldObjectType>(fieldObjectCatalog, "objectPrefabs");
            EnsureAllKeys<PickupType>(fieldObjectCatalog, "pickupPrefabs");
            report.Append($", catalog slots wired {ballsWired}+{enemiesWired}");

            var root = BuilderAssets.LoadOrCreate<BilliardRogueConfig>($"{Root}/BilliardRogueConfig.asset", out _);
            var rootWired = WireRoot(root, ballCatalog, enemyCatalog, fieldObjectCatalog, acts, arena, balance, control, pacing, hype, juice, visual);
            report.Append($", root slots wired {rootWired}");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
            return report.ToString();
        }

        #endregion

        #region Wiring

        static int WireCatalog<TEnum>(ScriptableObject catalog, ScriptableObject[] definitions) where TEnum : System.Enum
        {
            var so = new SerializedObject(catalog);
            var dict = so.FindProperty("definitions");
            var wired = 0;
            for (var i = 0; i < definitions.Length; i++)
            {
                var value = EnumDictionaryEditorUtils.GetValueProperty(dict, i);
                if (value.objectReferenceValue != null) continue;
                value.objectReferenceValue = definitions[i];
                wired++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return wired;
        }

        // Prefab slots stay empty (Presentation fills them); the keys must exist so the runtime dictionary is complete.
        static void EnsureAllKeys<TEnum>(ScriptableObject catalog, string dictField) where TEnum : System.Enum
        {
            var so = new SerializedObject(catalog);
            var dict = so.FindProperty(dictField);
            foreach (var key in System.Enum.GetValues(typeof(TEnum)))
            {
                EnumDictionaryEditorUtils.GetValueProperty(dict, System.Convert.ToInt32(key));
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        static int WireRoot(BilliardRogueConfig root, BallCatalog balls, EnemyCatalog enemies, FieldObjectCatalog fieldObjects, ActDefinition[] acts,
            ArenaConfig arena, BalanceConfig balance, ControlConfig control, PacingConfig pacing, HypeConfig hype, JuiceConfig juice,
            HD2DVisualConfig visual)
        {
            var so = new SerializedObject(root);
            var wired = 0;
            if (BuilderAssets.FillIfNull(so, "balls", balls)) wired++;
            if (BuilderAssets.FillIfNull(so, "enemies", enemies)) wired++;
            if (BuilderAssets.FillIfNull(so, "fieldObjects", fieldObjects)) wired++;
            if (BuilderAssets.FillIfNull(so, "arena", arena)) wired++;
            if (BuilderAssets.FillIfNull(so, "balance", balance)) wired++;
            if (BuilderAssets.FillIfNull(so, "control", control)) wired++;
            if (BuilderAssets.FillIfNull(so, "pacing", pacing)) wired++;
            if (BuilderAssets.FillIfNull(so, "hype", hype)) wired++;
            if (BuilderAssets.FillIfNull(so, "juice", juice)) wired++;
            if (BuilderAssets.FillIfNull(so, "visual", visual)) wired++;

            var actsProperty = so.FindProperty("acts");
            if (actsProperty.arraySize < acts.Length) actsProperty.arraySize = acts.Length;
            for (var i = 0; i < acts.Length; i++)
            {
                var slot = actsProperty.GetArrayElementAtIndex(i);
                if (slot.objectReferenceValue != null) continue;
                slot.objectReferenceValue = acts[i];
                wired++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(root);
            return wired;
        }

        #endregion

        #region Defaults

        static void FillVisualDefaults(HD2DVisualConfig visual)
        {
            var so = new SerializedObject(visual);
            var overrides = new[]
            {
                new HD2DVisualConfig.QualityOverride { qualityName = "Low", renderHeight = 270, bloom = false, tiltShift = false, shadows = false },
                new HD2DVisualConfig.QualityOverride { qualityName = "Medium", renderHeight = 360, bloom = true, tiltShift = false, shadows = true },
                new HD2DVisualConfig.QualityOverride { qualityName = "High", renderHeight = 360, bloom = true, tiltShift = true, shadows = true },
            };
            SerializedPropertyWriter.Write(so.FindProperty("qualityOverrides"), overrides);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visual);
        }

        #endregion
    }
}
