#nullable enable

using NUnit.Framework;

namespace Nex.BilliardRogue.Editor.Tests
{
    // The platform-pause races GameplayView must survive: a stop while an overlay closes, a stop already raised when
    // the run starts, repeated stops, and pauses after the run ended.
    public class GameplayPauseGateTests
    {
        [Test]
        public void StopWhileAnOverlayClosesKeepsThePauseUntilThePauseViewShows()
        {
            var gate = new GameplayPauseGate();
            Assert.IsTrue(gate.TryBegin());

            // The overlay's pop makes GameplayView the top view before the queued pause view was pushed.
            Assert.IsFalse(gate.ShouldResumeOnTop());
            Assert.IsTrue(gate.IsPaused);
            Assert.IsTrue(gate.TryShowOverlay());
        }

        [Test]
        public void ShownPauseViewThatLeftWithoutAResumeResumesOnTop()
        {
            var gate = new GameplayPauseGate();
            gate.TryBegin();
            gate.TryShowOverlay();

            Assert.IsTrue(gate.ShouldResumeOnTop());
            Assert.IsTrue(gate.TryResume());
            Assert.IsFalse(gate.ShouldResumeOnTop());
            Assert.IsFalse(gate.IsPaused);
        }

        [Test]
        public void RepeatedStopsQueueASinglePauseView()
        {
            var gate = new GameplayPauseGate();
            Assert.IsTrue(gate.TryBegin());
            Assert.IsFalse(gate.TryBegin());
            Assert.IsTrue(gate.TryShowOverlay());
            Assert.IsFalse(gate.TryShowOverlay());
        }

        [Test]
        public void SessionOverlaysWaitWhilePaused()
        {
            var gate = new GameplayPauseGate();
            Assert.IsFalse(gate.BlocksOverlays);
            gate.TryBegin();
            Assert.IsTrue(gate.BlocksOverlays);
            gate.TryShowOverlay();
            gate.TryResume();
            Assert.IsFalse(gate.BlocksOverlays);
        }

        [Test]
        public void ResumeWithoutAPauseIsIgnoredAndANewPauseCanFollowAResume()
        {
            var gate = new GameplayPauseGate();
            Assert.IsFalse(gate.TryResume());

            gate.TryBegin();
            gate.TryShowOverlay();
            gate.TryResume();
            Assert.IsTrue(gate.TryBegin());
            Assert.IsTrue(gate.TryShowOverlay());
        }

        [Test]
        public void NoPauseAfterTheRunEnded()
        {
            var ended = new GameplayPauseGate();
            ended.EndRun();
            Assert.IsFalse(ended.TryBegin());

            var pending = new GameplayPauseGate();
            pending.TryBegin();
            pending.EndRun();
            Assert.IsFalse(pending.TryShowOverlay());
        }
    }
}
