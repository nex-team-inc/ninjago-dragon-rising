#nullable enable

using NUnit.Framework;

namespace Nex.BilliardRogue.Editor.Tests
{
    // TDD D1 fallback: the scene reload on a player-count change must see the count of the last started session,
    // because every flow stops the camera before the next calibration.
    public class CameraSessionTests
    {
        [TestCase(0, 1, false)]
        [TestCase(1, 1, false)]
        [TestCase(1, 2, true)]
        [TestCase(2, 1, true)]
        public void PlayerCountChangeComparesTheLastStartedSession(int lastStarted, int requested, bool expected)
        {
            Assert.AreEqual(expected, CameraSession.IsPlayerCountChange(lastStarted, requested));
        }
    }
}
