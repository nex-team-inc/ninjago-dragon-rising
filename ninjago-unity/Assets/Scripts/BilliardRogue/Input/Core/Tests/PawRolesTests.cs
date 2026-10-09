#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.InputCore.Tests
{
    public class PawRolesTests
    {
        static readonly Vector2 Low = new(0f, -10f);
        static readonly Vector2 High = new(0f, 6f);

        [Test]
        public void TheHigherPawTakesTheBallWhenTrackingStarts()
        {
            var roles = new PawRoles(4f, 0.25f);
            roles.Assign(High, Low);
            Assert.IsFalse(roles.BallIsRight);
            Assert.AreEqual(High, roles.Ball(High, Low));
            Assert.AreEqual(Low, roles.Cue(High, Low));

            roles.Assign(Low, High);
            Assert.IsTrue(roles.BallIsRight);
            Assert.AreEqual(High, roles.Ball(Low, High));
        }

        [Test]
        public void TheRolesSwapOnlyAfterTheCuePawStaysHigherByTheMargin()
        {
            var roles = new PawRoles(4f, 0.25f);
            roles.Assign(High, Low);
            // The right (cue) paw rises just above the left one: inside the margin, no swap.
            Assert.IsFalse(roles.Update(0.0, High, High + new Vector2(0f, 3f), false));
            Assert.IsFalse(roles.Update(1.0, High, High + new Vector2(0f, 3f), false));
            Assert.IsFalse(roles.BallIsRight);

            var above = High + new Vector2(0f, 8f);
            Assert.IsFalse(roles.Update(2.0, High, above, false));
            Assert.IsFalse(roles.Update(2.2, High, above, false), "not held long enough yet");
            Assert.IsTrue(roles.Update(2.3, High, above, false));
            Assert.IsTrue(roles.BallIsRight, "the right paw is up: it holds the ball now");
            Assert.IsFalse(roles.Update(2.4, High, above, false), "already swapped");
        }

        [Test]
        public void DippingBackBelowTheMarginRestartsTheHold()
        {
            var roles = new PawRoles(4f, 0.25f);
            roles.Assign(High, Low);
            var above = High + new Vector2(0f, 8f);
            roles.Update(0.0, High, above, false);
            roles.Update(0.2, High, Low, false);
            Assert.IsFalse(roles.Update(0.3, High, above, false));
            Assert.IsFalse(roles.Update(0.5, High, above, false));
            Assert.IsTrue(roles.Update(0.56, High, above, false));
        }

        [Test]
        public void AStrikeInProgressNeverSwapsTheRoles()
        {
            var roles = new PawRoles(4f, 0.25f);
            roles.Assign(High, Low);
            var above = High + new Vector2(0f, 8f);
            for (var t = 0.0; t < 1.0; t += 0.05)
            {
                Assert.IsFalse(roles.Update(t, High, above, true));
            }

            Assert.IsFalse(roles.BallIsRight);
            Assert.IsFalse(roles.Update(1.0, High, above, false), "the hold starts once the strike is over");
            Assert.IsTrue(roles.Update(1.25, High, above, false));
        }
    }
}
