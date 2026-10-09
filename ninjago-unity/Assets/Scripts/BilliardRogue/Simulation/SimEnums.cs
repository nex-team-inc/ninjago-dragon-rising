#nullable enable

// Enum values are persisted by Easy Save as ints: keep explicit values, append only, never reorder.
namespace Nex.BilliardRogue.Simulation
{
    public enum BallType
    {
        Basic = 0,
        Flame = 1,
        Frost = 2,
        Thunder = 3,
        Bomb = 4,
        Splitter = 5,
        Piercer = 6,
        Iron = 7,
        Venom = 8,
        Vampire = 9,
        Rubber = 10,
        Lucky = 11,
    }

    public enum BallRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
    }

    public enum EnemyType
    {
        Slime = 0,
        Bat = 1,
        Skeleton = 2,
        ShieldKnight = 3,
        Mage = 4,
        Healer = 5,
        Bomber = 6,
        Totem = 7,
        KingSlime = 8,
        BoneLich = 9,
        CrystalGolem = 10,
        // Boss-summoned destructible obstacle modelled as an enemy with moveRows 0 and attack 0.
        BoneWall = 11,
    }

    public enum FieldObjectType
    {
        Pillar = 0,
        Crate = 1,
        Portal = 2,
        Mud = 3,
    }

    public enum PickupType
    {
        ExtraBall = 0,
        Heal = 1,
        Power = 2,
    }

    public enum StatusType
    {
        Burn = 0,
        Poison = 1,
        Freeze = 2,
    }

    /// <summary>Shield faces of an enemy footprint. Bottom faces the player (the launch line).</summary>
    public enum Face
    {
        None = 0,
        Bottom = 1,
        Top = 2,
        Left = 3,
        Right = 4,
    }

    public enum RewardKind
    {
        NewBall = 0,
        UpgradeBall = 1,
        Heal = 2,
        MaxHp = 3,
    }

    public enum RunOutcome
    {
        None = 0,
        Victory = 1,
        Defeat = 2,
        Abandoned = 3,
    }

    public enum SimEventKind
    {
        BallLaunched = 0,
        BallWallBounce = 1,
        BallExited = 2,
        BallSplit = 3,
        BallTeleported = 4,
        BallSlowed = 5,
        /// <summary>value = damage, flag = crit.</summary>
        EnemyHit = 10,
        EnemyBlocked = 11,
        EnemyKilled = 12,
        EnemyHealed = 13,
        /// <summary>value = hp, sourceId = caster (0 = a batch); flag = batch pop-in, value2 = its stagger index in the batch.</summary>
        EnemySpawned = 14,
        /// <summary>position = from, position2 = to.</summary>
        EnemyMoved = 15,
        /// <summary>value = damage, flag = ranged.</summary>
        EnemyAttack = 16,
        EnemyAbilityTelegraph = 17,
        EnemyShieldRotated = 18,
        /// <summary>status, value = stacks.</summary>
        StatusApplied = 20,
        /// <summary>status, value = damage.</summary>
        StatusTick = 21,
        FreezeExpired = 22,
        /// <summary>position → position2.</summary>
        ChainLightning = 23,
        /// <summary>position, value = radius.</summary>
        Explosion = 24,
        CrateHit = 30,
        CrateBroken = 31,
        PickupCollected = 32,
        PickupExpired = 33,
        /// <summary>Batch pickup popping in: targetId = pickup id, pickup, position, flag = true, value2 = stagger index.</summary>
        PickupSpawned = 34,
        /// <summary>value = damage.</summary>
        PlayerDamaged = 40,
        /// <summary>value = heal amount.</summary>
        PlayerHealed = 41,
        PlayerDied = 42,
        PowerShotConsumed = 43,
        /// <summary>ballId, value = combo.</summary>
        ComboChanged = 50,
        BossPhaseChanged = 51,
        /// <summary>v1 row wave (legacy saves only).</summary>
        WaveSpawned = 52,
        StageCleared = 53,
        /// <summary>
        /// After a batch's EnemySpawned / PickupSpawned events: value = batch index in the stage, value2 = entries spawned,
        /// flag = entries dropped for lack of free cells, sourceId = empty turns skipped to spawn it (0 = on its turn).
        /// </summary>
        BatchSpawned = 54,
    }

    /// <summary>Sizes of the enum-indexed tables so callers never need Enum.GetValues at runtime.</summary>
    public static class SimConstants
    {
        public const int BallTypeCount = 12;
        public const int EnemyTypeCount = 12;
        public const int FieldObjectTypeCount = 4;
        public const int PickupTypeCount = 3;
        public const int MaxBallLevel = 3;
        public const int ActCount = 3;
        public const int StagesPerAct = 4;
        public const int StageCount = ActCount * StagesPerAct;
    }
}
