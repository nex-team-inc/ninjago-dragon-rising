#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>Creates Act_1..3 (GDD §7: pools, bosses, budgets, lighting themes) only when missing.</summary>
    public static class ActDefinitionsBuilder
    {
        #region Build

        /// <summary>Returns the acts in order; created is the number of new assets.</summary>
        public static ActDefinition[] Build(out int created)
        {
            created = 0;
            var result = new ActDefinition[SimConstants.ActCount];
            for (var i = 0; i < result.Length; i++)
            {
                var path = $"{BuilderAssets.ConfigRoot}/Acts/Act_{i + 1}.asset";
                var definition = BuilderAssets.LoadOrCreate<ActDefinition>(path, out var isNew);
                if (isNew)
                {
                    Fill(definition, i);
                    created++;
                }

                result[i] = definition;
            }

            return result;
        }

        static void Fill(ActDefinition definition, int actIndex)
        {
            var so = new SerializedObject(definition);
            SerializedPropertyWriter.Write(so.FindProperty("rules"), RulesFor(actIndex));
            SerializedPropertyWriter.Write(so.FindProperty("lighting"), LightingFor(actIndex));
            so.FindProperty("nameKey").stringValue = LocKeys.Act.Name(actIndex);
            so.FindProperty("battleBgm").intValue = (int)(actIndex switch
            {
                0 => BgmManager.BgmType.Act1,
                1 => BgmManager.BgmType.Act2,
                _ => BgmManager.BgmType.Act3,
            });
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        #endregion

        #region GDD §7 data

        static WaveEntryWeight Entry(EnemyType type, float weight, int cost) => new() { type = type, weight = weight, cost = cost };

        static ActRules RulesFor(int actIndex)
        {
            return actIndex switch
            {
                0 => new ActRules
                {
                    actIndex = 0, bossType = EnemyType.KingSlime, baseBudgetPerRow = 3, budgetGrowthPerStage = 1, maxFieldObjects = 2,
                    rarityWeights = new[] { 0.6f, 0.3f, 0.1f },
                    enemyPool = new[]
                    {
                        Entry(EnemyType.Slime, 3f, 1), Entry(EnemyType.Bat, 2f, 1), Entry(EnemyType.Skeleton, 1.5f, 2),
                        Entry(EnemyType.ShieldKnight, 0.7f, 2), Entry(EnemyType.Healer, 0.5f, 2),
                    },
                },
                1 => new ActRules
                {
                    actIndex = 1, bossType = EnemyType.BoneLich, baseBudgetPerRow = 4, budgetGrowthPerStage = 1, maxFieldObjects = 3,
                    rarityWeights = new[] { 0.45f, 0.4f, 0.15f },
                    enemyPool = new[]
                    {
                        Entry(EnemyType.Skeleton, 2.5f, 2), Entry(EnemyType.ShieldKnight, 1.5f, 2), Entry(EnemyType.Mage, 1.2f, 2),
                        Entry(EnemyType.Bat, 1.5f, 1), Entry(EnemyType.Healer, 1f, 2), Entry(EnemyType.Bomber, 1f, 2), Entry(EnemyType.Slime, 1f, 1),
                    },
                },
                _ => new ActRules
                {
                    actIndex = 2, bossType = EnemyType.CrystalGolem, baseBudgetPerRow = 5, budgetGrowthPerStage = 1, maxFieldObjects = 3,
                    rarityWeights = new[] { 0.3f, 0.45f, 0.25f },
                    enemyPool = new[]
                    {
                        Entry(EnemyType.Bomber, 2f, 2), Entry(EnemyType.Mage, 2f, 2), Entry(EnemyType.ShieldKnight, 1.5f, 2),
                        Entry(EnemyType.Totem, 1f, 3), Entry(EnemyType.Skeleton, 1.5f, 2), Entry(EnemyType.Healer, 1f, 2), Entry(EnemyType.Bat, 1f, 1),
                    },
                },
            };
        }

        static ActLightingPreset LightingFor(int actIndex)
        {
            return actIndex switch
            {
                // Mossy Ruins: golden hour, warm god rays, falling leaves.
                0 => new ActLightingPreset
                {
                    sunColor = new Color(1f, 0.85f, 0.6f), sunIntensity = 1.6f, sunEuler = new Vector3(48f, -30f, 0f),
                    ambientSky = new Color(0.55f, 0.7f, 0.6f), ambientEquator = new Color(0.45f, 0.5f, 0.35f), ambientGround = new Color(0.2f, 0.18f, 0.12f),
                    fogColor = new Color(0.75f, 0.8f, 0.6f), fogDensity = 0.01f, rimColor = new Color(1f, 0.9f, 0.6f),
                    additionalLightTint = new Color(1f, 0.8f, 0.5f), godRayColor = new Color(1f, 0.85f, 0.55f), godRayIntensity = 1f,
                    particleTint = new Color(0.8f, 0.9f, 0.5f),
                },
                // Sunken Crypt: deep blue night, torches, drifting embers.
                1 => new ActLightingPreset
                {
                    sunColor = new Color(0.35f, 0.45f, 0.8f), sunIntensity = 0.6f, sunEuler = new Vector3(60f, 20f, 0f),
                    ambientSky = new Color(0.12f, 0.16f, 0.3f), ambientEquator = new Color(0.1f, 0.12f, 0.2f), ambientGround = new Color(0.05f, 0.05f, 0.08f),
                    fogColor = new Color(0.08f, 0.1f, 0.2f), fogDensity = 0.03f, rimColor = new Color(0.5f, 0.6f, 1f),
                    additionalLightTint = new Color(1f, 0.6f, 0.3f), godRayColor = new Color(0.4f, 0.5f, 0.9f), godRayIntensity = 0.5f,
                    particleTint = new Color(1f, 0.6f, 0.25f),
                },
                // Crystal Hollow: violet/cyan magical glow, floating motes.
                _ => new ActLightingPreset
                {
                    sunColor = new Color(0.7f, 0.5f, 1f), sunIntensity = 0.9f, sunEuler = new Vector3(55f, -15f, 0f),
                    ambientSky = new Color(0.3f, 0.2f, 0.5f), ambientEquator = new Color(0.2f, 0.3f, 0.45f), ambientGround = new Color(0.1f, 0.08f, 0.18f),
                    fogColor = new Color(0.25f, 0.15f, 0.4f), fogDensity = 0.02f, rimColor = new Color(0.5f, 1f, 1f),
                    additionalLightTint = new Color(0.6f, 0.9f, 1f), godRayColor = new Color(0.7f, 0.5f, 1f), godRayIntensity = 0.9f,
                    particleTint = new Color(0.6f, 1f, 1f),
                },
            };
        }

        #endregion
    }
}
