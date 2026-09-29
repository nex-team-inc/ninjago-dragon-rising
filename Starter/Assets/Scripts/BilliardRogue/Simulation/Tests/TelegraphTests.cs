#nullable enable

using NUnit.Framework;

namespace Nex.BilliardRogue.Simulation.Tests
{
    /// <summary>EnemyPhaseResolver.PendingTelegraph mirrors the telegraphs a phase emits, so a continued run can rebuild the icons.</summary>
    public class TelegraphTests
    {
        GameRules rules = null!;
        RunState run = null!;
        BoardOps ops = null!;
        EnemyPhaseResolver resolver = null!;
        SimRandom rng = null!;

        [SetUp]
        public void SetUp()
        {
            rules = SimTest.Rules();
            run = SimTest.NewRun(rules);
            ops = new BoardOps(rules);
            resolver = new EnemyPhaseResolver(rules, ops);
            rng = new SimRandom(SimRandom.SeedToState(1));
        }

        [Test]
        public void CadenceTwoCasterWarnsOnlyThePhaseBeforeItFires()
        {
            var mage = SimTest.Put(run, ops, EnemyType.Mage, 3, 2);
            mage.turnCounter = 0;
            Assert.AreEqual(-1, EnemyPhaseResolver.PendingTelegraph(rules, mage), "just spawned: acts first in the next phase, nothing due");
            mage.turnCounter = 1;
            Assert.AreEqual(EnemyPhaseResolver.TelegraphCast, EnemyPhaseResolver.PendingTelegraph(rules, mage));
            mage.turnCounter = 2;
            Assert.AreEqual(-1, EnemyPhaseResolver.PendingTelegraph(rules, mage), "fired this phase");
        }

        [Test]
        public void FrozenDeadAndEveryPhaseActionsShowNothing()
        {
            var mage = SimTest.Put(run, ops, EnemyType.Mage, 3, 2);
            mage.turnCounter = 1;
            mage.status.frozenTurns = 1;
            Assert.AreEqual(-1, EnemyPhaseResolver.PendingTelegraph(rules, mage), "frozen for the next phase");
            mage.status.frozenTurns = 0;
            mage.hp = 0;
            Assert.AreEqual(-1, EnemyPhaseResolver.PendingTelegraph(rules, mage), "dead");

            var healer = SimTest.Put(run, ops, EnemyType.Healer, 5, 2);
            healer.turnCounter = 3;
            Assert.AreEqual(-1, EnemyPhaseResolver.PendingTelegraph(rules, healer), "cadence 1 is never telegraphed");
        }

        [Test]
        public void SpawnWinsWhenBothActionsAreDue()
        {
            // Test rules: the Bone Lich is a ranged caster (cadence 2) that also raises walls (cadence 3).
            var lich = SimTest.Put(run, ops, EnemyType.BoneLich, 2, 1);
            lich.turnCounter = 5;
            Assert.AreEqual(EnemyPhaseResolver.TelegraphSpawn, EnemyPhaseResolver.PendingTelegraph(rules, lich), "next = 6: cast (2) and walls (3) both due");
            lich.turnCounter = 3;
            Assert.AreEqual(EnemyPhaseResolver.TelegraphCast, EnemyPhaseResolver.PendingTelegraph(rules, lich));
            lich.turnCounter = 2;
            Assert.AreEqual(EnemyPhaseResolver.TelegraphSpawn, EnemyPhaseResolver.PendingTelegraph(rules, lich));

            var totem = SimTest.Put(run, ops, EnemyType.Totem, 5, 1);
            totem.turnCounter = 1;
            Assert.AreEqual(EnemyPhaseResolver.TelegraphSpawn, EnemyPhaseResolver.PendingTelegraph(rules, totem));
        }

        [Test]
        public void MatchesTheTelegraphAPhaseEmits()
        {
            var mage = SimTest.Put(run, ops, EnemyType.Mage, 3, 1);
            var totem = SimTest.Put(run, ops, EnemyType.Totem, 5, 1);
            for (var phase = 0; phase < 6; phase++)
            {
                var events = SimTest.Resolve(resolver, run, rng);
                AssertMatches(events, mage);
                AssertMatches(events, totem);
            }
        }

        void AssertMatches(System.Collections.Generic.List<SimEvent> events, EnemyState enemy)
        {
            var emitted = -1;
            foreach (var ev in events)
            {
                if (ev.kind == SimEventKind.EnemyAbilityTelegraph && !ev.flag && ev.targetId == enemy.id) emitted = ev.value;
            }
            Assert.AreEqual(emitted, EnemyPhaseResolver.PendingTelegraph(rules, enemy), $"{enemy.type} after counter {enemy.turnCounter}");
        }
    }
}
