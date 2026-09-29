#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>
    /// One presentation-facing fact produced by the simulation, appended to a caller-owned list without
    /// allocating. Positions are in sim space; presentation converts them through ArenaLayout.
    /// </summary>
    public struct SimEvent
    {
        public SimEventKind kind;
        /// <summary>Enemy-phase stage 0..4 (status ticks, abilities, advance, attacks, spawn) for staged playback.</summary>
        public int step;
        public int ballId;
        public int targetId;
        public int sourceId;
        public int value;
        public int value2;
        public BallType ballType;
        public EnemyType enemyType;
        public StatusType status;
        public PickupType pickup;
        public Vector2 position;
        public Vector2 position2;
        public bool flag;
        /// <summary>EnemyHit from a flying ball: the part of value added by Hype (BallSimulator.SetHype), 0 without Hype.</summary>
        public int hypeBonus;
    }

    /// <summary>Who dealt damage, so BoardOps can apply poison bonuses, crits and attribution consistently.</summary>
    public struct DamageSource
    {
        public BallType ballType;
        public int ballId;
        public bool isStatusTick;
        public bool isExplosion;
        public bool isChain;
        public bool isCrit;
        /// <summary>Damage the Hype multiplier added to amount (reported as SimEvent.hypeBonus on the EnemyHit).</summary>
        public int hypeBonus;
    }

    /// <summary>
    /// Read-only snapshot of one in-flight ball for presentation (see BallSimulator.ForEachBall). Not BallView:
    /// that name is the Presentation MonoBehaviour (TDD §8), which would shadow a struct of the same name inside
    /// namespace Nex.BilliardRogue.
    /// </summary>
    public readonly struct BallSnapshot
    {
        public readonly int id;
        public readonly BallType type;
        public readonly int level;
        public readonly Vector2 position;
        public readonly Vector2 velocity;
        public readonly bool isMini;
        public readonly float radius;

        public BallSnapshot(int id, BallType type, int level, Vector2 position, Vector2 velocity, bool isMini, float radius)
        {
            this.id = id;
            this.type = type;
            this.level = level;
            this.position = position;
            this.velocity = velocity;
            this.isMini = isMini;
            this.radius = radius;
        }
    }

    public delegate void BallVisitor(in BallSnapshot ball);
}
