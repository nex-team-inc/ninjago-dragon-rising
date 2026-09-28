#nullable enable

using NUnit.Framework;

namespace Nex.BilliardRogue.Simulation.Tests
{
    public class SimRandomTests
    {
        [Test]
        public void SameSeedProducesSameSequence()
        {
            var a = new SimRandom(SimRandom.SeedToState(1234));
            var b = new SimRandom(SimRandom.SeedToState(1234));
            for (var i = 0; i < 64; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt());
            }
            Assert.AreEqual(a.State, b.State);
        }

        [Test]
        public void DifferentSeedsDiverge()
        {
            var a = new SimRandom(SimRandom.SeedToState(1));
            var b = new SimRandom(SimRandom.SeedToState(2));
            Assert.AreNotEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void SavedStateResumesTheSequence()
        {
            var a = new SimRandom(SimRandom.SeedToState(77));
            a.NextUInt();
            a.NextUInt();
            var resumed = new SimRandom(a.State);
            Assert.AreEqual(a.NextUInt(), resumed.NextUInt());
        }

        [Test]
        public void RangeAndValue01StayInBounds()
        {
            var rng = new SimRandom(SimRandom.SeedToState(-5));
            for (var i = 0; i < 1000; i++)
            {
                var v = rng.Range(-3, 4);
                Assert.That(v, Is.InRange(-3, 3));
                var f = rng.Value01();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
            Assert.AreEqual(9, rng.Range(9, 9));
        }
    }
}
