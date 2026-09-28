#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue.Simulation
{
    /// <summary>Mutable state of one pooled in-flight ball owned by BallSimulator.</summary>
    internal sealed class BallSlot
    {
        public const int MaxPierced = 16;

        public bool active;
        public int id;
        public BallType type;
        public int level;
        public bool isMini;
        public int shooterIndex;
        /// <summary>RunStats.shots value at launch; keys the deterministic proc rolls.</summary>
        public int shotNumber;
        public int hashSalt;
        public Vector2 position;
        /// <summary>Unit direction; speed is kept separately so reflections and the stall pull never change it.</summary>
        public Vector2 direction;
        public float speed;
        public float radius;
        public float flightTime;
        public float stallTime;
        public bool stalled;
        public int bounces;
        public int wallBounces;
        public int idleWallBounces;
        public int combo;
        public int hitIndex;
        public bool firstHitPending;
        public bool powerShot;
        public bool powerPickup;
        public bool areaUsed;
        public bool splitUsed;
        /// <summary>Enemies the ball is currently passing through; dropped once it leaves them.</summary>
        public readonly int[] piercedIds = new int[MaxPierced];
        public int piercedCount;
        public int piercesUsed;
        public int healedThisShot;
        public float portalLock;
        public float slowTimer;
        public int mudInsideId = -1;

        public void Begin(int ballId, BallType ballType, int ballLevel, bool mini, int shooter, int shot, int salt,
            Vector2 origin, Vector2 unitDirection, float ballSpeed, float ballRadius)
        {
            id = ballId;
            type = ballType;
            level = ballLevel;
            isMini = mini;
            shooterIndex = shooter;
            shotNumber = shot;
            hashSalt = salt;
            position = origin;
            direction = unitDirection;
            speed = ballSpeed;
            radius = ballRadius;
            flightTime = 0f;
            stallTime = 0f;
            stalled = false;
            bounces = 0;
            wallBounces = 0;
            idleWallBounces = 0;
            combo = 0;
            hitIndex = 0;
            firstHitPending = true;
            powerShot = false;
            powerPickup = false;
            areaUsed = false;
            splitUsed = false;
            piercedCount = 0;
            piercesUsed = 0;
            healedThisShot = 0;
            portalLock = 0f;
            slowTimer = 0f;
            mudInsideId = -1;
        }
    }
}
