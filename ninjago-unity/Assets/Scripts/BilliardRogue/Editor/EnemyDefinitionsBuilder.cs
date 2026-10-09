#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEditor;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>Creates the 12 EnemyDefinition assets (GDD §6, bosses and the Bone Wall) only when missing.</summary>
    public static class EnemyDefinitionsBuilder
    {
        #region Build

        /// <summary>Returns definitions indexed by (int)EnemyType; created is the number of new assets.</summary>
        public static EnemyDefinition[] Build(out int created)
        {
            created = 0;
            var result = new EnemyDefinition[SimConstants.EnemyTypeCount];
            for (var i = 0; i < result.Length; i++)
            {
                var type = (EnemyType)i;
                var path = $"{BuilderAssets.ConfigRoot}/Enemies/Enemy_{type}.asset";
                var definition = BuilderAssets.LoadOrCreate<EnemyDefinition>(path, out var isNew);
                if (isNew)
                {
                    Fill(definition, type);
                    created++;
                }

                result[i] = definition;
            }

            return result;
        }

        static void Fill(EnemyDefinition definition, EnemyType type)
        {
            var rules = RulesFor(type);
            rules.type = type;
            var isBoss = rules.isBoss;
            var so = new SerializedObject(definition);
            SerializedPropertyWriter.Write(so.FindProperty("rules"), rules);
            so.FindProperty("hopHeight").floatValue = HopHeight(type);
            so.FindProperty("hitSfx").intValue = (int)(isBoss ? SfxManager.SoundEffect.BossHit : SfxManager.SoundEffect.BallHitSoft);
            so.FindProperty("deathSfx").intValue = (int)DeathSfx(type, isBoss);
            so.FindProperty("attackSfx").intValue = (int)(rules.ranged ? SfxManager.SoundEffect.EnemyCast : SfxManager.SoundEffect.EnemyAttack);
            so.FindProperty("deathVfx").intValue = (int)DeathVfx(type, isBoss);
            so.FindProperty("nameKey").stringValue = LocKeys.Enemy.Name(type);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        #endregion

        #region GDD §6 data

        static EnemyRules RulesFor(EnemyType type)
        {
            return type switch
            {
                EnemyType.Slime => new EnemyRules { hp = 4, attack = 2 },
                EnemyType.Bat => new EnemyRules { hp = 2, attack = 1, moveRows = 2 },
                EnemyType.Skeleton => new EnemyRules { hp = 7, attack = 3 },
                EnemyType.ShieldKnight => new EnemyRules { hp = 6, attack = 3, shieldFace = Face.Bottom },
                EnemyType.Mage => new EnemyRules { hp = 5, attack = 2, moveEveryNTurns = 2, ranged = true, abilityEveryNTurns = 2, abilityValue = 2 },
                EnemyType.Healer => new EnemyRules { hp = 5, attack = 1, abilityEveryNTurns = 1, healAmount = 2 },
                EnemyType.Bomber => new EnemyRules { hp = 4, attack = 5, deathExplosionDamage = 3 },
                EnemyType.Totem => new EnemyRules { hp = 10, attack = 0, moveRows = 0, spawnEveryNTurns = 2, spawnCount = 1, spawnType = EnemyType.Slime },
                EnemyType.KingSlime => new EnemyRules
                {
                    hp = 70, attack = 5, moveEveryNTurns = 2, width = 2, height = 2, isBoss = true,
                    spawnEveryNTurns = 2, spawnCount = 2, spawnType = EnemyType.Slime, spawnCountBelowHalf = 3,
                },
                EnemyType.BoneLich => new EnemyRules
                {
                    hp = 130, attack = 4, moveEveryNTurns = 3, width = 2, height = 2, isBoss = true, ranged = true,
                    abilityEveryNTurns = 2, abilityValue = 3,
                    spawnEveryNTurns = 3, spawnCount = 3, spawnType = EnemyType.BoneWall,
                    halfHpSummonType = EnemyType.Skeleton, halfHpSummonCount = 2,
                },
                EnemyType.CrystalGolem => new EnemyRules
                {
                    hp = 200, attack = 6, moveEveryNTurns = 2, width = 2, height = 2, isBoss = true,
                    shieldFace = Face.Bottom, rotatingShield = true, abilityEveryNTurns = 3, abilityValue = 1,
                },
                EnemyType.BoneWall => new EnemyRules { hp = 4, attack = 0, moveRows = 0 },
                _ => new EnemyRules(),
            };
        }

        static float HopHeight(EnemyType type)
        {
            return type switch
            {
                EnemyType.Slime => 0.5f,
                EnemyType.Bat => 0.3f,
                EnemyType.Skeleton => 0.4f,
                EnemyType.ShieldKnight => 0.3f,
                EnemyType.Mage => 0.45f,
                EnemyType.Healer => 0.35f,
                EnemyType.Bomber => 0.4f,
                EnemyType.KingSlime => 0.6f,
                EnemyType.BoneLich => 0.3f,
                EnemyType.CrystalGolem => 0.4f,
                _ => 0f,
            };
        }

        static SfxManager.SoundEffect DeathSfx(EnemyType type, bool isBoss)
        {
            if (isBoss) return SfxManager.SoundEffect.BossDeath;
            return type switch
            {
                EnemyType.Bomber => SfxManager.SoundEffect.Explosion,
                EnemyType.BoneWall => SfxManager.SoundEffect.CrateBreak,
                _ => SfxManager.SoundEffect.EnemyDeath,
            };
        }

        static VfxManager.VisualEffect DeathVfx(EnemyType type, bool isBoss)
        {
            if (isBoss) return VfxManager.VisualEffect.BossPoof;
            return type switch
            {
                EnemyType.Bomber => VfxManager.VisualEffect.Explosion,
                EnemyType.BoneWall => VfxManager.VisualEffect.CratePieces,
                _ => VfxManager.VisualEffect.EnemyPoof,
            };
        }

        #endregion
    }
}
