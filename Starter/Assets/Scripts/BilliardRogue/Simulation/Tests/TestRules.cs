#nullable enable

namespace Nex.BilliardRogue.Simulation.Tests
{
    /// <summary>
    /// GameRules with the GDD default numbers. Mirrors Editor/BallDefinitionsBuilder, EnemyDefinitionsBuilder and
    /// ActDefinitionsBuilder so tests exercise the same values as the generated config assets; keep them in sync.
    /// </summary>
    public static class TestRules
    {
        #region Public Methods

        public static GameRules Create()
        {
            var balls = new BallRules[SimConstants.BallTypeCount];
            for (var i = 0; i < balls.Length; i++)
            {
                balls[i] = Ball((BallType)i);
            }

            var enemies = new EnemyRules[SimConstants.EnemyTypeCount];
            for (var i = 0; i < enemies.Length; i++)
            {
                enemies[i] = Enemy((EnemyType)i);
                enemies[i].type = (EnemyType)i;
            }

            var acts = new ActRules[SimConstants.ActCount];
            for (var i = 0; i < acts.Length; i++)
            {
                acts[i] = Act(i);
            }

            return new GameRules { arena = new ArenaRules(), balance = new BalanceRules(), balls = balls, enemies = enemies, acts = acts };
        }

        #endregion

        #region GDD Data

        static BallRules Ball(BallType type)
        {
            return type switch
            {
                BallType.Basic => Rules(type, BallRarity.Common, 0, new() { damage = 1 }, new() { damage = 2 }, new() { damage = 3 }),
                BallType.Flame => Rules(type, BallRarity.Common, 0,
                    new() { damage = 1, statusStacks = 2 }, new() { damage = 1, statusStacks = 3 },
                    new() { damage = 1, statusStacks = 4, spreadBurnOnDeath = true }),
                BallType.Frost => Rules(type, BallRarity.Uncommon, 0,
                    new() { damage = 1, procChance = 0.35f }, new() { damage = 1, procChance = 0.5f },
                    new() { damage = 1, procChance = 0.65f, frozenBonusDamage = 2 }),
                BallType.Thunder => Rules(type, BallRarity.Uncommon, 0,
                    new() { damage = 1, chainCount = 2, chainRange = 2.2f, chainDamage = 1 },
                    new() { damage = 1, chainCount = 3, chainRange = 2.2f, chainDamage = 1 },
                    new() { damage = 1, chainCount = 4, chainRange = 2.2f, chainDamage = 2 }),
                BallType.Bomb => Rules(type, BallRarity.Uncommon, 0,
                    new() { damage = 1, areaDamage = 3, areaRadius = 1 }, new() { damage = 1, areaDamage = 4, areaRadius = 1 },
                    new() { damage = 1, areaDamage = 5, areaRadius = 2, areaCross = true }),
                BallType.Splitter => Rules(type, BallRarity.Rare, 1,
                    new() { damage = 1, splitCount = 2, splitDamage = 1 }, new() { damage = 1, splitCount = 3, splitDamage = 1 },
                    new() { damage = 1, splitCount = 3, splitDamage = 2 }),
                BallType.Piercer => Rules(type, BallRarity.Rare, 2,
                    new() { damage = 2, pierceCount = 4 }, new() { damage = 2, pierceCount = 6 }, new() { damage = 3, pierceCount = 8 }),
                BallType.Iron => Rules(type, BallRarity.Common, 0,
                    new() { damage = 3, speedMultiplier = 0.75f, maxBounces = 25 },
                    new() { damage = 4, speedMultiplier = 0.75f, maxBounces = 25 },
                    new() { damage = 5, speedMultiplier = 0.75f, maxBounces = 25 }),
                BallType.Venom => Rules(type, BallRarity.Uncommon, 0,
                    new() { damage = 1, statusStacks = 1 }, new() { damage = 1, statusStacks = 2 }, new() { damage = 1, statusStacks = 3 }),
                BallType.Vampire => Rules(type, BallRarity.Rare, 1,
                    new() { damage = 1, healPerHit = 1, healCapPerShot = 2 }, new() { damage = 1, healPerHit = 1, healCapPerShot = 3 },
                    new() { damage = 1, healPerHit = 1, healCapPerShot = 4 }),
                BallType.Rubber => Rules(type, BallRarity.Common, 0,
                    new() { damage = 1, bonusPerWallBounce = 1, bonusCap = 4 }, new() { damage = 1, bonusPerWallBounce = 1, bonusCap = 6 },
                    new() { damage = 1, bonusPerWallBounce = 1, bonusCap = 8 }),
                _ => Rules(type, BallRarity.Rare, 2,
                    new() { damage = 1, critChance = 0.25f, critMultiplier = 3 }, new() { damage = 1, critChance = 0.35f, critMultiplier = 3 },
                    new() { damage = 1, critChance = 0.45f, critMultiplier = 4 }),
            };
        }

        static BallRules Rules(BallType type, BallRarity rarity, int unlockTier, BallLevelStats l1, BallLevelStats l2, BallLevelStats l3)
        {
            return new BallRules { type = type, rarity = rarity, unlockTier = unlockTier, levels = new[] { l1, l2, l3 } };
        }

        static EnemyRules Enemy(EnemyType type)
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
                _ => new EnemyRules { hp = 4, attack = 0, moveRows = 0 },
            };
        }

        static WaveEntryWeight Entry(EnemyType type, float weight, int cost) => new() { type = type, weight = weight, cost = cost };

        static ActRules Act(int actIndex)
        {
            return actIndex switch
            {
                0 => new ActRules
                {
                    actIndex = 0, bossType = EnemyType.KingSlime, baseBudgetPerRow = 3, budgetGrowthPerStage = 1, maxFieldObjects = 2,
                    minEnemiesPerBatch = 10, spawnEveryNTurns = 3, batchesPerStage = 3, spawnForbiddenNearRows = 3, pickupsPerBatch = 2, batchBudgetRows = 4,
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
                    minEnemiesPerBatch = 10, spawnEveryNTurns = 3, batchesPerStage = 3, spawnForbiddenNearRows = 3, pickupsPerBatch = 2, batchBudgetRows = 4,
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
                    minEnemiesPerBatch = 10, spawnEveryNTurns = 3, batchesPerStage = 3, spawnForbiddenNearRows = 3, pickupsPerBatch = 2, batchBudgetRows = 4,
                    rarityWeights = new[] { 0.3f, 0.45f, 0.25f },
                    enemyPool = new[]
                    {
                        Entry(EnemyType.Bomber, 2f, 2), Entry(EnemyType.Mage, 2f, 2), Entry(EnemyType.ShieldKnight, 1.5f, 2),
                        Entry(EnemyType.Totem, 1f, 3), Entry(EnemyType.Skeleton, 1.5f, 2), Entry(EnemyType.Healer, 1f, 2), Entry(EnemyType.Bat, 1f, 1),
                    },
                },
            };
        }

        #endregion
    }
}
