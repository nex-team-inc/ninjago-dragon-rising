#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.InputCore.Tests
{
    public class PawPointerMathTests
    {
        static readonly Vector2 center = new(0f, 4f);
        static readonly Vector2 halfRange = new(16f, 12f);

        [Test]
        public void CentreMapsToTheScreenCentreAndTheRangeToTheEdges()
        {
            Assert.AreEqual(new Vector2(0.5f, 0.5f), PawPointerMath.ToScreen01(center, center, halfRange));
            Assert.AreEqual(new Vector2(0f, 0f), PawPointerMath.ToScreen01(center - halfRange, center, halfRange));
            Assert.AreEqual(new Vector2(1f, 1f), PawPointerMath.ToScreen01(center + halfRange, center, halfRange));
        }

        [Test]
        public void BeyondTheRangeIsClampedToTheScreen()
        {
            var p = PawPointerMath.ToScreen01(new Vector2(-60f, 50f), center, halfRange);
            Assert.AreEqual(new Vector2(0f, 1f), p);
        }

        [Test]
        public void HandsBroughtTogetherMeetOnScreen()
        {
            // Both hands 8 in to the chest's left at shoulder height: the same screen point, left of centre.
            var hand = new Vector2(-8f, 6f);
            var p = PawPointerMath.ToScreen01(hand, center, halfRange);
            Assert.AreEqual(0.25f, p.x, 1e-5f);
            Assert.Greater(p.y, 0.5f);
        }
    }
}
