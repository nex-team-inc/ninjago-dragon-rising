#nullable enable

using Nex.BilliardRogue.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.Editor.Tests
{
    // Guards the TDD §14.1 world convention: sim x → +x, sim y → +z, origin at the launch-line centre.
    public class ArenaLayoutTests
    {
        GameObject root = null!;
        ArenaLayout layout = null!;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ArenaLayoutTests");
            layout = root.AddComponent<ArenaLayout>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void OriginIsTheLaunchLineCentreAndAxesFollowTheTdd()
        {
            var rules = new ArenaRules();
            layout.Initialize(rules, 1f);
            var half = rules.columns * 0.5f;

            AssertVector(Vector3.zero, layout.ToWorld(new Vector2(half, 0f)));
            AssertVector(new Vector3(half, 0f, 0f), layout.ToWorld(new Vector2(rules.columns, 0f)));
            AssertVector(new Vector3(0f, 0f, 5f), layout.ToWorld(new Vector2(half, 5f)));
            AssertVector(new Vector3(0f, 2f, 0f), layout.ToWorld(new Vector2(half, 0f), 2f));
            AssertVector(Vector3.forward, layout.DirectionToWorld(Vector2.up));
            AssertVector(Vector3.right, layout.DirectionToWorld(Vector2.right));
            AssertVector(Vector3.zero, layout.LaunchLineCenterWorld);
            AssertVector(new Vector3(0f, 0f, rules.launchZoneHeight + rules.rows * 0.5f), layout.GridCenterWorld);
            AssertVector(new Vector3(0f, 0f, (rules.launchZoneHeight + rules.rows) * 0.5f), layout.CenterWorld);
        }

        [Test]
        public void ToSimInvertsToWorldUnderScaleAndTransform()
        {
            root.transform.SetPositionAndRotation(new Vector3(3f, -1f, 10f), Quaternion.Euler(0f, 90f, 0f));
            var rules = new ArenaRules();
            layout.Initialize(rules, 2f);

            var points = new[] { Vector2.zero, new Vector2(3.5f, 1.6f), new Vector2(7f, 11.6f), new Vector2(1.25f, 4.75f) };
            foreach (var sim in points)
            {
                var world = layout.ToWorld(sim, 0.5f);
                var back = layout.ToSim(world);
                Assert.AreEqual(sim.x, back.x, 1e-4f);
                Assert.AreEqual(sim.y, back.y, 1e-4f);
                Assert.AreEqual(0.5f, layout.HeightOf(world), 1e-4f);
            }

            Assert.AreEqual(2f, layout.CellSize, 1e-6f);
            var launchLine = Vector3.Distance(layout.ToWorld(Vector2.zero), layout.ToWorld(new Vector2(rules.columns, 0f)));
            Assert.AreEqual(rules.columns * 2f, launchLine, 1e-4f);
        }

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, "x");
            Assert.AreEqual(expected.y, actual.y, 1e-4f, "y");
            Assert.AreEqual(expected.z, actual.z, 1e-4f, "z");
        }
    }
}
