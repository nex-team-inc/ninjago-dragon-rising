#nullable enable

using System;

// Plain rules data filled from ScriptableObjects by RulesFactory (Assembly-CSharp). Public fields on purpose:
// these are inspector-authored value bags, not behaviour.
namespace Nex.BilliardRogue.Simulation
{
    [Serializable]
    public class BallLevelStats
    {
        public int damage = 1;
        public float speedMultiplier = 1f;
        public int maxBounces = 40;
        /// <summary>Burn or poison stacks applied per hit (Flame, Venom).</summary>
        public int statusStacks;
        /// <summary>Chance 0..1 to apply the ball's status on hit (Frost freeze).</summary>
        public float procChance;
        public int chainCount;
        public float chainRange = 2.2f;
        public int chainDamage = 1;
        public int areaDamage;
        public int areaRadius;
        public bool areaCross;
        public int splitCount;
        public int splitDamage = 1;
        /// <summary>Angle between neighbouring minis of a split (the fan is centred on the reflected direction).</summary>
        public float splitFanDegrees = 18f;
        /// <summary>Mini radius as a fraction of the parent ball's.</summary>
        public float miniRadiusScale = 0.7f;
        public int pierceCount;
        public int healPerHit;
        public int healCapPerShot;
        public int bonusPerWallBounce;
        public int bonusCap;
        public float critChance;
        public int critMultiplier = 3;
        public int frozenBonusDamage;
        public bool spreadBurnOnDeath;
    }

    [Serializable]
    public class BallRules
    {
        public BallType type;
        public BallRarity rarity;
        public BallLevelStats[] levels = new BallLevelStats[SimConstants.MaxBallLevel];
        /// <summary>0 = available from the start; N = unlocked once MetaProgressData.highestUnlockTier ≥ N.</summary>
        public int unlockTier;
    }

    [Serializable]
    public class EnemyRules
    {
        public EnemyType type;
        public int hp;
        public int attack;
        public int moveRows = 1;
        public int moveEveryNTurns = 1;
        public int width = 1;
        public int height = 1;
        public bool isBoss;
        public bool ranged;
        /// <summary>Cadence of the primary ability (ranged bolt, heal, quake). 0 = none.</summary>
        public int abilityEveryNTurns;
        /// <summary>Ability magnitude: bolt damage, quake extra rows.</summary>
        public int abilityValue;
        public Face shieldFace = Face.None;
        public bool rotatingShield;
        public int deathExplosionDamage;
        public int healAmount;
        /// <summary>Recurring spawns every spawnEveryNTurns (Totem, King Slime, Bone Lich walls).</summary>
        public int spawnCount;
        public EnemyType spawnType;
        public int spawnEveryNTurns;
        /// <summary>Recurring spawn count once the boss is below 50% HP (0 = unchanged).</summary>
        public int spawnCountBelowHalf;
        /// <summary>One-time summon when HP first drops below 50% (Bone Lich skeletons).</summary>
        public EnemyType halfHpSummonType;
        public int halfHpSummonCount;
    }

    [Serializable]
    public class ArenaRules
    {
        public int columns = 7;
        public int rows = 10;
        public float launchZoneHeight = 1.6f;
        public float launchY = 0.55f;
        public float ballRadius = 0.2f;
        public float ballSpeed = 13f;
        public float enemyInset = 0.08f;
        public float bossInset = 0.1f;
        /// <summary>Cell insets (grid units) a ball must overlap before a trigger fires: pickups, portals, mud.</summary>
        public float pickupInset = 0.2f;
        public float portalInset = 0.25f;
        public float mudInset = 0.1f;
        public float minAimAngleDeg = 12f;
        public float maxFlightSeconds = 7f;
        public int maxIdleWallBounces = 10;
        public float antiStallAccel = 6f;
        /// <summary>
        /// Seconds the anti-stall pull may run before the ball ignores solids and drops straight out at ballSpeed,
        /// bounding every flight to maxFlightSeconds + maxStallSeconds + TopWallY / ballSpeed.
        /// </summary>
        public float maxStallSeconds = 1f;
        public int substepsPerSecond = 240;
    }

    [Serializable]
    public class BalanceRules
    {
        public int playerMaxHp = 30;
        public int bagCap = 12;
        public BallType[] startingBag = { BallType.Basic, BallType.Basic, BallType.Basic, BallType.Basic };
        public int levelCap = 3;
        public float hpScalePerStage = 0.16f;
        public int attackBonusPerAct = 1;
        /// <summary>Legacy v1 Heal card amount; v2 rolls only balls, but a v1 save may still hold a pending Heal card.</summary>
        public int healRewardAmount = 12;
        /// <summary>Legacy v1 Max HP card amount (see healRewardAmount).</summary>
        public int maxHpRewardAmount = 5;
        public float bossHealFraction = 0.5f;
        /// <summary>HP healed after every stage clear (GDD v2 §1: replaces the Heal card); a boss adds bossHealFraction.</summary>
        public int stageClearHeal = 4;
        public int pickupHealAmount = 4;
        /// <summary>v1 row-wave pickups (unused since GDD v2 §5: ActRules.pickupsPerBatch).</summary>
        public float pickupChancePerRow = 0.2f;
        public int poisonMax = 5;
        public int crateHp = 3;
        public float mudSlowFactor = 0.6f;
        public float mudSlowSeconds = 0.8f;
        public float portalLockSeconds = 0.25f;
        public int powerShotBonusDamage = 1;
        public int powerPickupMultiplier = 2;
        /// <summary>Offer Basic as a reward ball while ability balls are available (off: Basic is only the last resort).</summary>
        public bool offerBasicBall;
        /// <summary>
        /// Upper clamp for BallSimulator.SetHype's speed multiplier; the substep count is re-derived from it so a faster
        /// ball never moves further per substep than an unhyped one.
        /// </summary>
        public float maxHypeSpeedMultiplier = 3f;
    }

    [Serializable]
    public class WaveEntryWeight
    {
        public EnemyType type;
        public float weight;
        public int cost;
    }

    [Serializable]
    public class ActRules
    {
        public int actIndex;
        public int normalStages = 3;
        /// <summary>v1 row waves (unused since GDD v2 §5 batches; kept so older assets and saves still load).</summary>
        public int minWaves = 5;
        public int maxWaves = 8;
        public WaveEntryWeight[] enemyPool = Array.Empty<WaveEntryWeight>();
        public EnemyType bossType;
        public int baseBudgetPerRow = 3;
        public int budgetGrowthPerStage = 1;
        public int maxFieldObjects = 3;
        /// <summary>Common / Uncommon / Rare reward weights.</summary>
        public float[] rarityWeights = { 0.6f, 0.3f, 0.1f };
        /// <summary>v1 boss escorts (unused since GDD v2 §5: escort batches follow spawnEveryNTurns).</summary>
        public int bossEscortWaves = 3;
        public int bossEscortEveryNTurns = 2;
        /// <summary>Columns every row keeps free when a batch spawns into it, so a ball can always slip past.</summary>
        public int minOpenColumnsPerRow = 1;

        // GDD v2 §5: enemies pop in as batches at random free cells.
        /// <summary>Enemies per batch; fewer spawn only when the free cells run out.</summary>
        public int minEnemiesPerBatch = 10;
        /// <summary>A batch spawns on the stage's first turn and then every N turns (turn 1, 1 + N, 1 + 2N, …).</summary>
        public int spawnEveryNTurns = 3;
        /// <summary>Batches of a normal stage; a boss stage generates this many escort batches and cycles them.</summary>
        public int batchesPerStage = 3;
        /// <summary>Rows nearest the player (the danger row included) that nothing spawns into.</summary>
        public int spawnForbiddenNearRows = 3;
        /// <summary>Pickups added to each batch (they spawn after its enemies, on the same free-cell rule).</summary>
        public int pickupsPerBatch = 2;
        /// <summary>
        /// Difficulty budget of a batch = batchBudgetRows × (baseBudgetPerRow + budgetGrowthPerStage × stageNumber); a boss
        /// stage's escort batches get half. Enemy costs are drawn within it, cheapest entries fill up to minEnemiesPerBatch.
        /// </summary>
        public int batchBudgetRows = 4;
    }

    public sealed class GameRules
    {
        public ArenaRules arena = new();
        public BalanceRules balance = new();
        /// <summary>Indexed by (int)BallType.</summary>
        public BallRules[] balls = Array.Empty<BallRules>();
        /// <summary>Indexed by (int)EnemyType.</summary>
        public EnemyRules[] enemies = Array.Empty<EnemyRules>();
        /// <summary>Indexed by act index (0..2).</summary>
        public ActRules[] acts = Array.Empty<ActRules>();
    }
}
