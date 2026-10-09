#nullable enable

using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// The session's analytics surface: forwards to RunAnalytics (TDD §12) and stays silent in headless smoke runs,
    /// where no AnalyticsManager exists. Every session/turn/shot/stage/reward/boss/run event goes through here.
    /// </summary>
    public sealed class SessionAnalytics
    {
        readonly RunAnalytics? inner;

        public SessionAnalytics(RunAnalytics? aInner)
        {
            inner = aInner;
        }

        public void SessionStart(int numPlayers, bool isContinue, string runId, int seed) => inner?.SessionStart(numPlayers, isContinue, runId, seed);

        public void SessionStop(RunOutcome outcome) => inner?.SessionStop(outcome);

        public void Pause() => inner?.Pause();

        public void Resume() => inner?.Resume();

        public void TurnStart(int stage, int turn, int balls, int hp) => inner?.TurnStart(stage, turn, balls, hp);

        public void ShotFired(BallType ballType, int level, float angleDeg, float power, int shooter)
        {
            inner?.ShotFired(ballType, level, angleDeg, power, shooter);
        }

        /// <summary>One ball's result; hypeAverage / hypeMax over its flight (0..1), hypeDamage = damage Hype added.</summary>
        public void ShotResult(BallType ballType, int hits, int damage, int bounces, int kills, int combo, float hypeAverage, float hypeMax, int hypeDamage)
        {
            inner?.ShotResult(ballType, hits, damage, bounces, kills, combo, hypeAverage, hypeMax, hypeDamage);
        }

        public void TurnEnd(int stage, int turn, int enemiesAdvanced, int damageTaken, int hp)
        {
            inner?.TurnEnd(stage, turn, enemiesAdvanced, damageTaken, hp);
        }

        public void StageStart(int act, int stage, bool isBoss) => inner?.StageStart(act, stage, isBoss);

        public void StageClear(int act, int stage, int turns, int hp) => inner?.StageClear(act, stage, turns, hp);

        public void RewardOffered(IReadOnlyList<RewardOption> options) => inner?.RewardOffered(options);

        public void RewardChosen(RewardOption option, int index) => inner?.RewardChosen(option, index);

        public void BossSpawn(EnemyType type) => inner?.BossSpawn(type);

        public void BossDefeated(EnemyType type, int turns) => inner?.BossDefeated(type, turns);

        public void RunEnd(RunOutcome outcome, int stageNumber, int turns, float seconds, int kills)
        {
            inner?.RunEnd(outcome, stageNumber, turns, seconds, kills);
        }
    }
}
