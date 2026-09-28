#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.InputCore.Tests
{
    public class AimHistoryTests
    {
        [Test]
        public void EmptyHistoryHasNoAim()
        {
            var history = new AimHistory(4);
            Assert.IsFalse(history.TryGetAt(1.0, out _));
        }

        [Test]
        public void ReturnsTheNewestSampleAtOrBeforeTheTime()
        {
            var history = new AimHistory(8);
            history.Add(1.0, Vector2.left);
            history.Add(2.0, Vector2.up);
            history.Add(3.0, Vector2.right);
            Assert.IsTrue(history.TryGetAt(2.5, out var aim));
            Assert.AreEqual(Vector2.up, aim);
            history.TryGetAt(3.0, out aim);
            Assert.AreEqual(Vector2.right, aim);
            history.TryGetAt(99.0, out aim);
            Assert.AreEqual(Vector2.right, aim);
        }

        [Test]
        public void LookBackBeyondTheBufferFallsBackToTheOldestKeptSample()
        {
            var history = new AimHistory(3);
            for (var i = 0; i < 5; i++)
            {
                history.Add(i, new Vector2(i, 1f));
            }

            Assert.AreEqual(3, history.Count);
            history.TryGetAt(-1.0, out var aim);
            Assert.AreEqual(new Vector2(2f, 1f), aim);
            history.TryGetAt(3.5, out aim);
            Assert.AreEqual(new Vector2(3f, 1f), aim);
        }

        [Test]
        public void ClearForgetsEverything()
        {
            var history = new AimHistory(3);
            history.Add(1.0, Vector2.up);
            history.Clear();
            Assert.AreEqual(0, history.Count);
            Assert.IsFalse(history.TryGetAt(2.0, out _));
        }
    }
}
