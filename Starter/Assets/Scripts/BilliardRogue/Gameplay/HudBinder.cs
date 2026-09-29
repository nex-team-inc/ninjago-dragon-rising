#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Pushes run values to IGameplayHud only when they change, so per-frame refreshes never rebuild HUD text.
    /// GameSession only pushes values through here; it never reads the HUD.
    /// </summary>
    public sealed class HudBinder
    {
        readonly IGameplayHud hud;
        readonly RunState run;
        readonly RunSimulation sim;
        readonly ShotSequencer sequencer;
        int hp = -1;
        int maxHp = -1;
        bool bossVisible;
        int bossHp = -1;
        int bossMaxHp = -1;
        EnemyType bossType;
        bool powerArmed;
        bool fastForward;
        int activePlayer = -1;
        int queueIndex = -1;
        int queueExtra = -1;
        int queueBagCount = -1;
        int remaining = -1;
        int total = -1;
        float hype = -1f;
        int hypeTier = -1;
        bool movePrompt;

        public HudBinder(IGameplayHud aHud, RunState aRun, RunSimulation aSim, ShotSequencer aSequencer)
        {
            hud = aHud;
            run = aRun;
            sim = aSim;
            sequencer = aSequencer;
        }

        #region Refresh

        public void RefreshAll()
        {
            RefreshHp();
            RefreshQueue();
            RefreshActivePlayer();
            RefreshPower();
            RefreshBoss();
        }

        public void RefreshHp()
        {
            if (run.playerHp == hp && run.playerMaxHp == maxHp) return;
            hp = run.playerHp;
            maxHp = run.playerMaxHp;
            hud.SetHp(hp, maxHp);
        }

        public void RefreshQueue()
        {
            var bagCount = run.bag.Count;
            var index = sequencer.NextIndex;
            var extra = run.extraBalls;
            if (index != queueIndex || extra != queueExtra || bagCount != queueBagCount)
            {
                queueIndex = index;
                queueExtra = extra;
                queueBagCount = bagCount;
                hud.SetBallQueue(run.bag, index, extra);
            }

            var left = sequencer.Remaining;
            var all = sequencer.Total;
            if (left == remaining && all == total) return;
            remaining = left;
            total = all;
            hud.SetBallsRemaining(left, all);
        }

        public void RefreshActivePlayer()
        {
            if (run.activePlayerIndex == activePlayer) return;
            activePlayer = run.activePlayerIndex;
            hud.SetActivePlayer(activePlayer, run.numPlayers);
        }

        public void RefreshPower()
        {
            if (run.powerPickupArmed == powerArmed) return;
            powerArmed = run.powerPickupArmed;
            hud.SetPowerArmed(powerArmed);
        }

        public void RefreshBoss()
        {
            var boss = run.stage.isBoss ? sim.FindBoss() : null;
            if (boss == null)
            {
                if (!bossVisible) return;
                bossVisible = false;
                bossHp = -1;
                bossMaxHp = -1;
                hud.SetBossHp(false, 0, 0, bossType);
                return;
            }

            if (bossVisible && boss.hp == bossHp && boss.maxHp == bossMaxHp && boss.type == bossType) return;
            bossVisible = true;
            bossHp = boss.hp;
            bossMaxHp = boss.maxHp;
            bossType = boss.type;
            hud.SetBossHp(true, bossHp, bossMaxHp, bossType);
        }

        #endregion

        #region Pushes

        public void SetStage()
        {
            hud.SetStage(run.actIndex, run.stageInAct, run.stage.isBoss);
            // A reward can level up a bag ball without changing the count or the next index: re-push the bag.
            queueIndex = -1;
        }

        public void SetFastForward(bool on)
        {
            if (on == fastForward) return;
            fastForward = on;
            hud.SetFastForward(on);
        }

        public void SetTrackingWarning(int playerIndex, bool lost) => hud.SetTrackingWarning(playerIndex, lost);

        /// <summary>Hype meter; pushed when the tier changes or the value moves by at least 0.01 (or reaches 0 / 1).</summary>
        public void SetHype(float hype01, int tier)
        {
            var changed = tier != hypeTier || Mathf.Abs(hype01 - hype) >= 0.01f
                          || (hype01 <= 0f && hype > 0f) || (hype01 >= 1f && hype < 1f);
            if (!changed) return;
            hype = hype01;
            hypeTier = tier;
            hud.SetHype(hype01, tier);
        }

        public void ShowMovePrompt(bool visible)
        {
            if (visible == movePrompt) return;
            movePrompt = visible;
            hud.ShowMovePrompt(visible);
        }

        public void ShowTurnBanner(int turn) => hud.ShowTurnBanner(turn);

        public void ShowShooterBanner(int playerIndex) => hud.ShowShooterBanner(playerIndex);

        public void ShowIncomingBanner() => hud.ShowIncomingBanner();

        #endregion
    }
}
