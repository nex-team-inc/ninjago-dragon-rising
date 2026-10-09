#nullable enable

using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Moves the existing ControlConfig.asset to the GDD v2 defaults (easier strike §2, motion energy §3, paw pointer
    /// §4) without touching the other tuned values: each listed field is copied from a fresh ControlConfig (its field
    /// initializers are the single source of the numbers) through SerializedObject, then the asset is saved.
    /// Idempotent. CLI: unity command eval 'return Nex.BilliardRogue.Editor.ControlConfigV2Upgrade.Run();'
    /// </summary>
    public static class ControlConfigV2Upgrade
    {
        const string AssetPath = BuilderAssets.ConfigRoot + "/ControlConfig.asset";

        static readonly string[] V2Fields =
        {
            // §2 easier strike
            "strikeSpeedInchesPerSec",
            "contactDistanceInches",
            "strikeAngleToleranceDeg",
            "lineCrossMaxOffsetInches",
            "armDistanceInches",
            "rearmSeconds",
            "maxStrikeSeconds",
            "powerShotSpeedMultiplier",
            "fullPowerSpeedInchesPerSec",
            // §3 motion energy (+ debug / bot simulation)
            "motionDeadzoneInchesPerSec",
            "motionFullInchesPerSec",
            "motionMaxNodeInchesPerSec",
            "motionAttackSeconds",
            "motionReleaseSeconds",
            "motionMinNodes",
            "debugMotionFullScreensPerSec",
            "botMotionFrequencyHz",
            "botMotionMin",
            "botMotionMax",
            // §4 paw pointer
            "pawPointerCenterXInches",
            "pawPointerCenterYInches",
            "pawPointerHalfWidthInches",
            "pawPointerHalfHeightInches",
            "pawPointerMinCutoff",
            "pawPointerBeta",
            "debugPawOffset01",
        };

        [MenuItem("Nex/Billiard Rogue/Upgrade Control Config (v2)", priority = 42)]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ControlConfig>(AssetPath);
            if (asset == null) throw new InvalidOperationException($"{AssetPath} not found (run the Config Assets builder first)");

            var defaults = ScriptableObject.CreateInstance<ControlConfig>();
            var log = new StringBuilder($"[ControlConfigV2Upgrade] {AssetPath}:");
            try
            {
                var source = new SerializedObject(defaults);
                var target = new SerializedObject(asset);
                foreach (var name in V2Fields)
                {
                    var from = source.FindProperty(name) ?? throw new InvalidOperationException($"ControlConfig has no field '{name}'");
                    var to = target.FindProperty(name);
                    var before = to != null ? Describe(to) : "missing";
                    target.CopyFromSerializedProperty(from);
                    log.Append($" {name} {before}->{Describe(from)};");
                }
                target.ApplyModifiedPropertiesWithoutUndo();
            }
            finally
            {
                Object.DestroyImmediate(defaults);
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            return log.ToString();
        }

        static string Describe(SerializedProperty property)
        {
            return property.propertyType switch
            {
                SerializedPropertyType.Float => property.floatValue.ToString("0.###"),
                SerializedPropertyType.Integer => property.intValue.ToString(),
                _ => property.propertyType.ToString(),
            };
        }
    }
}
