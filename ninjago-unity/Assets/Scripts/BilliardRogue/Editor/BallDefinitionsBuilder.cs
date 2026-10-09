#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>Creates the 12 BallDefinition assets (GDD §5) only when missing.</summary>
    public static class BallDefinitionsBuilder
    {
        sealed class Seed
        {
            public BallRules rules = new();
            public Color color;
            public Color glow;
            public SfxManager.SoundEffect hitSfx;
            public VfxManager.VisualEffect hitVfx;
        }

        #region Build

        /// <summary>Returns definitions indexed by (int)BallType; created is the number of new assets.</summary>
        public static BallDefinition[] Build(out int created)
        {
            created = 0;
            var result = new BallDefinition[SimConstants.BallTypeCount];
            for (var i = 0; i < result.Length; i++)
            {
                var type = (BallType)i;
                var path = $"{BuilderAssets.ConfigRoot}/Balls/Ball_{type}.asset";
                var definition = BuilderAssets.LoadOrCreate<BallDefinition>(path, out var isNew);
                if (isNew)
                {
                    Fill(definition, type, SeedFor(type));
                    created++;
                }

                result[i] = definition;
            }

            return result;
        }

        static void Fill(BallDefinition definition, BallType type, Seed seed)
        {
            var so = new SerializedObject(definition);
            seed.rules.type = type;
            SerializedPropertyWriter.Write(so.FindProperty("rules"), seed.rules);
            so.FindProperty("color").colorValue = seed.color;
            so.FindProperty("glowColor").colorValue = seed.glow;
            so.FindProperty("hitSfx").intValue = (int)seed.hitSfx;
            so.FindProperty("launchSfx").intValue = (int)SfxManager.SoundEffect.BallLaunch;
            so.FindProperty("hitVfx").intValue = (int)seed.hitVfx;
            so.FindProperty("nameKey").stringValue = LocKeys.Ball.Name(type);
            var firstDesc = LocKeys.Ball.Description(type, 1);
            so.FindProperty("descKeyPrefix").stringValue = firstDesc.Substring(0, firstDesc.Length - 1);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        #endregion

        #region GDD §5 data

        static BallLevelStats Level(int damage) => new() { damage = damage };

        static Seed SeedFor(BallType type)
        {
            var seed = new Seed();
            var r = seed.rules;
            switch (type)
            {
                case BallType.Basic:
                    Set(seed, BallRarity.Common, 0, Level(1), Level(2), Level(3), new Color(0.95f, 0.95f, 0.9f), Color.white, SfxManager.SoundEffect.BallHitSoft, VfxManager.VisualEffect.HitSpark);
                    break;
                case BallType.Flame:
                    Set(seed, BallRarity.Common, 0,
                        new BallLevelStats { damage = 1, statusStacks = 2 },
                        new BallLevelStats { damage = 1, statusStacks = 3 },
                        new BallLevelStats { damage = 1, statusStacks = 4, spreadBurnOnDeath = true },
                        new Color(1f, 0.45f, 0.15f), new Color(2f, 0.9f, 0.3f), SfxManager.SoundEffect.Burn, VfxManager.VisualEffect.BurnBurst);
                    break;
                case BallType.Frost:
                    Set(seed, BallRarity.Uncommon, 0,
                        new BallLevelStats { damage = 1, procChance = 0.35f },
                        new BallLevelStats { damage = 1, procChance = 0.5f },
                        new BallLevelStats { damage = 1, procChance = 0.65f, frozenBonusDamage = 2 },
                        new Color(0.6f, 0.85f, 1f), new Color(0.8f, 1.6f, 2f), SfxManager.SoundEffect.Freeze, VfxManager.VisualEffect.FreezeBurst);
                    break;
                case BallType.Thunder:
                    Set(seed, BallRarity.Uncommon, 0,
                        new BallLevelStats { damage = 1, chainCount = 2, chainRange = 2.2f, chainDamage = 1 },
                        new BallLevelStats { damage = 1, chainCount = 3, chainRange = 2.2f, chainDamage = 1 },
                        new BallLevelStats { damage = 1, chainCount = 4, chainRange = 2.2f, chainDamage = 2 },
                        new Color(1f, 0.95f, 0.3f), new Color(2f, 2f, 0.6f), SfxManager.SoundEffect.Lightning, VfxManager.VisualEffect.LightningHit);
                    break;
                case BallType.Bomb:
                    Set(seed, BallRarity.Uncommon, 0,
                        new BallLevelStats { damage = 1, areaDamage = 3, areaRadius = 1 },
                        new BallLevelStats { damage = 1, areaDamage = 4, areaRadius = 1 },
                        new BallLevelStats { damage = 1, areaDamage = 5, areaRadius = 2, areaCross = true },
                        new Color(0.3f, 0.3f, 0.35f), new Color(2f, 0.6f, 0.2f), SfxManager.SoundEffect.Explosion, VfxManager.VisualEffect.Explosion);
                    break;
                case BallType.Splitter:
                    Set(seed, BallRarity.Rare, 1,
                        new BallLevelStats { damage = 1, splitCount = 2, splitDamage = 1 },
                        new BallLevelStats { damage = 1, splitCount = 3, splitDamage = 1 },
                        new BallLevelStats { damage = 1, splitCount = 3, splitDamage = 2 },
                        new Color(0.9f, 0.6f, 1f), new Color(1.6f, 1f, 2f), SfxManager.SoundEffect.Split, VfxManager.VisualEffect.SplitPop);
                    break;
                case BallType.Piercer:
                    Set(seed, BallRarity.Rare, 2,
                        new BallLevelStats { damage = 2, pierceCount = 4 },
                        new BallLevelStats { damage = 2, pierceCount = 6 },
                        new BallLevelStats { damage = 3, pierceCount = 8 },
                        new Color(0.8f, 0.85f, 0.9f), new Color(1.5f, 1.8f, 2f), SfxManager.SoundEffect.BallHitHard, VfxManager.VisualEffect.HitSpark);
                    break;
                case BallType.Iron:
                    Set(seed, BallRarity.Common, 0,
                        new BallLevelStats { damage = 3, speedMultiplier = 0.75f, maxBounces = 25 },
                        new BallLevelStats { damage = 4, speedMultiplier = 0.75f, maxBounces = 25 },
                        new BallLevelStats { damage = 5, speedMultiplier = 0.75f, maxBounces = 25 },
                        new Color(0.5f, 0.5f, 0.55f), new Color(0.6f, 0.6f, 0.7f), SfxManager.SoundEffect.BallHitHard, VfxManager.VisualEffect.HitSpark);
                    break;
                case BallType.Venom:
                    Set(seed, BallRarity.Uncommon, 0,
                        new BallLevelStats { damage = 1, statusStacks = 1 },
                        new BallLevelStats { damage = 1, statusStacks = 2 },
                        new BallLevelStats { damage = 1, statusStacks = 3 },
                        new Color(0.5f, 0.9f, 0.3f), new Color(0.6f, 2f, 0.4f), SfxManager.SoundEffect.Poison, VfxManager.VisualEffect.PoisonBurst);
                    break;
                case BallType.Vampire:
                    Set(seed, BallRarity.Rare, 1,
                        new BallLevelStats { damage = 1, healPerHit = 1, healCapPerShot = 2 },
                        new BallLevelStats { damage = 1, healPerHit = 1, healCapPerShot = 3 },
                        new BallLevelStats { damage = 1, healPerHit = 1, healCapPerShot = 4 },
                        new Color(0.8f, 0.15f, 0.25f), new Color(2f, 0.3f, 0.5f), SfxManager.SoundEffect.Heal, VfxManager.VisualEffect.HealSparkle);
                    break;
                case BallType.Rubber:
                    Set(seed, BallRarity.Common, 0,
                        new BallLevelStats { damage = 1, bonusPerWallBounce = 1, bonusCap = 4 },
                        new BallLevelStats { damage = 1, bonusPerWallBounce = 1, bonusCap = 6 },
                        new BallLevelStats { damage = 1, bonusPerWallBounce = 1, bonusCap = 8 },
                        new Color(1f, 0.55f, 0.75f), new Color(2f, 1f, 1.5f), SfxManager.SoundEffect.BallHitMid, VfxManager.VisualEffect.HitSpark);
                    break;
                case BallType.Lucky:
                    Set(seed, BallRarity.Rare, 2,
                        new BallLevelStats { damage = 1, critChance = 0.25f, critMultiplier = 3 },
                        new BallLevelStats { damage = 1, critChance = 0.35f, critMultiplier = 3 },
                        new BallLevelStats { damage = 1, critChance = 0.45f, critMultiplier = 4 },
                        new Color(1f, 0.85f, 0.35f), new Color(2f, 1.8f, 0.6f), SfxManager.SoundEffect.BallHitMid, VfxManager.VisualEffect.CritSpark);
                    break;
            }

            r.type = type;
            return seed;
        }

        static void Set(Seed seed, BallRarity rarity, int unlockTier, BallLevelStats l1, BallLevelStats l2, BallLevelStats l3,
            Color color, Color glow, SfxManager.SoundEffect hitSfx, VfxManager.VisualEffect hitVfx)
        {
            seed.rules.rarity = rarity;
            seed.rules.unlockTier = unlockTier;
            seed.rules.levels = new[] { l1, l2, l3 };
            seed.color = color;
            seed.glow = glow;
            seed.hitSfx = hitSfx;
            seed.hitVfx = hitVfx;
        }

        #endregion
    }
}
