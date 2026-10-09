#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.Editor.Tests
{
    /// <summary>
    /// POWER charge (GDD v2 §17, HypeConfig.Charge with its defaults: 0.6 POWER per second of full dancing, 6 energy for
    /// the whole bar): dancing spends energy, no energy charges nothing, the bar stops at full and never drains.
    /// </summary>
    public class HypeChargeTests
    {
        HypeConfig config = null!;

        [SetUp]
        public void SetUp() => config = ScriptableObject.CreateInstance<HypeConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(config);

        [Test]
        public void DancingChargesPowerAndSpendsEnergyForIt()
        {
            var power = config.Charge(0f, 6f, 1f, 1f, out var spent);
            Assert.AreEqual(0.6f, power, 1e-4f);
            Assert.AreEqual(0.6f * config.EnergyPerFullPower, spent, 1e-4f);

            power = config.Charge(0f, 6f, 0.5f, 1f, out spent);
            Assert.AreEqual(0.3f, power, 1e-4f, "half the motion charges half as fast");
        }

        [Test]
        public void WithoutEnergyDancingChargesNothing()
        {
            var power = config.Charge(0.2f, 0f, 1f, 1f, out var spent);
            Assert.AreEqual(0.2f, power);
            Assert.AreEqual(0f, spent);
        }

        [Test]
        public void TheEnergyLeftCapsTheCharge()
        {
            var power = config.Charge(0f, 1.2f, 1f, 1f, out var spent);
            Assert.AreEqual(1.2f / config.EnergyPerFullPower, power, 1e-4f);
            Assert.AreEqual(1.2f, spent, 1e-4f);
        }

        [Test]
        public void TheBarStopsAtFullAndNeverDrainsOnItsOwn()
        {
            var power = config.Charge(0.9f, 30f, 1f, 1f, out var spent);
            Assert.AreEqual(1f, power);
            Assert.AreEqual(0.1f * config.EnergyPerFullPower, spent, 1e-4f, "only the part charged is paid");

            power = config.Charge(0.9f, 30f, 0f, 5f, out spent);
            Assert.AreEqual(0.9f, power, "standing still keeps the charge");
            Assert.AreEqual(0f, spent);
        }
    }
}
