#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

// Run state = the save file (Easy Save via PlayerDataManager). Public fields, parameterless constructors,
// lists where collections grow, no readonly fields (ES3 skips them), no UnityEngine.Object references.
namespace Nex.BilliardRogue.Simulation
{
    [Serializable]
    public struct GridPos
    {
        public int col;
        public int row;

        public GridPos(int col, int row)
        {
            this.col = col;
            this.row = row;
        }
    }

    [Serializable, Preserve]
    public class StatusStacks
    {
        public int burn;
        public int poison;
        public int frozenTurns;
    }

    [Serializable, Preserve]
    public class EnemyState
    {
        public int id;
        public EnemyType type;
        public int col;
        public int row;
        public int width = 1;
        public int height = 1;
        public int hp;
        public int maxHp;
        public int attack;
        public StatusStacks status = new();
        public int turnCounter;
        public Face shieldFace;
        public bool bossHalfTriggered;
    }

    [Serializable, Preserve]
    public class FieldObjectState
    {
        public int id;
        public FieldObjectType type;
        public int col;
        public int row;
        public int hp;
        /// <summary>Portal partner id, -1 when unpaired.</summary>
        public int pairId = -1;
    }

    [Serializable, Preserve]
    public class PickupState
    {
        public int id;
        public PickupType type;
        public int col;
        public int row;
    }

    [Serializable, Preserve]
    public class BallInstance
    {
        public BallType type;
        public int level = 1;
    }

    [Serializable, Preserve]
    public class WaveCell
    {
        public int col;
        public bool isPickup;
        public EnemyType enemy;
        public PickupType pickup;
    }

    [Serializable, Preserve]
    public class WaveRow
    {
        public List<WaveCell> cells = new();
    }

    [Serializable, Preserve]
    public class StagePlan
    {
        public int actIndex;
        public int stageInAct;
        public bool isBoss;
        public List<WaveRow> waves = new();
        public List<FieldObjectState> fieldObjects = new();
    }

    [Serializable, Preserve]
    public class BoardState
    {
        public List<EnemyState> enemies = new();
        public List<FieldObjectState> fieldObjects = new();
        public List<PickupState> pickups = new();
        public int nextId = 1;
    }

    [Serializable, Preserve]
    public class RunStats
    {
        public int turns;
        public int shots;
        public int hits;
        public int kills;
        public int damageDealt;
        public int damageTaken;
        public int bestCombo;
        public int bossesDefeated;
        public float playSeconds;
    }

    [Serializable, Preserve]
    public class RewardOption
    {
        public RewardKind kind;
        public BallType ballType;
        /// <summary>Bag index for UpgradeBall, -1 otherwise.</summary>
        public int bagIndex = -1;
        public int amount;
    }

    [Serializable, Preserve]
    public class RunState
    {
        public string runId = "";
        public int seed;
        public ulong rngState;
        public int numPlayers = 1;
        public int actIndex;
        public int stageInAct;
        /// <summary>0..11 across the whole run.</summary>
        public int stageNumber;
        public int turnInStage;
        public int playerHp;
        public int playerMaxHp;
        public List<BallInstance> bag = new();
        public BoardState board = new();
        public StagePlan stage = new();
        public int nextWaveIndex;
        public int activePlayerIndex;
        public RunStats stats = new();
        public RunOutcome outcome;
        public bool awaitingReward;
        public List<RewardOption> pendingRewards = new();
        /// <summary>Extra Basic shots granted by pickups for the current turn.</summary>
        public int extraBalls;
        /// <summary>Next fired ball deals double damage on its first hit (Power pickup).</summary>
        public bool powerPickupArmed;
    }

    [Serializable, Preserve]
    public class MetaProgressData
    {
        public int runsStarted;
        public int runsWon;
        public int bestStageNumber = -1;
        public int highestUnlockTier;
        public int totalKills;
        public bool tutorialSeen;
    }
}
