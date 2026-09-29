#nullable enable

using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Moves the existing Act_1..3 assets to the GDD v2 §5 batch spawning values without touching their other tuned
    /// fields: each listed ActRules field is written from ActDefinitionsBuilder.RulesFor (the single source of the numbers)
    /// through SerializedObject, then the asset is saved. Idempotent.
    /// CLI: unity command eval 'return Nex.BilliardRogue.Editor.ActSpawnRulesUpgrade.Run();'
    /// </summary>
    public static class ActSpawnRulesUpgrade
    {
        static readonly string[] SpawnFields =
        {
            nameof(Simulation.ActRules.minEnemiesPerBatch),
            nameof(Simulation.ActRules.spawnEveryNTurns),
            nameof(Simulation.ActRules.batchesPerStage),
            nameof(Simulation.ActRules.spawnForbiddenNearRows),
            nameof(Simulation.ActRules.pickupsPerBatch),
            nameof(Simulation.ActRules.batchBudgetRows),
        };

        [MenuItem("Nex/Billiard Rogue/Upgrade Act Spawn Rules (v2)", priority = 43)]
        public static void RunFromMenu() => Debug.Log(Run());

        public static string Run()
        {
            var log = new StringBuilder("[ActSpawnRulesUpgrade]");
            for (var i = 0; i < Simulation.SimConstants.ActCount; i++)
            {
                var path = $"{BuilderAssets.ConfigRoot}/Acts/Act_{i + 1}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<ActDefinition>(path);
                if (asset == null) throw new InvalidOperationException($"{path} not found (run the Config Assets builder first)");

                var values = ActDefinitionsBuilder.RulesFor(i);
                var so = new SerializedObject(asset);
                var rules = so.FindProperty("rules") ?? throw new InvalidOperationException($"{path}: no rules property");
                log.Append($" Act_{i + 1}:");
                foreach (var name in SpawnFields)
                {
                    var property = rules.FindPropertyRelative(name) ?? throw new InvalidOperationException($"ActRules has no serialized field '{name}'");
                    var value = (int)(typeof(Simulation.ActRules).GetField(name)!.GetValue(values));
                    log.Append($" {name} {property.intValue}->{value}");
                    property.intValue = value;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                log.Append(';');
            }

            return log.ToString();
        }
    }
}
