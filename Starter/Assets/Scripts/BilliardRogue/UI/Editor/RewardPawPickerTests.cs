#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace Nex.BilliardRogue.Editor.Tests
{
    // Dual-paw hold of the motion reward pick (GDD v2 §4): both paws on one ball fill it in holdSeconds and pick it,
    // a single paw only hovers, leaving drains, untracked paws never hover.
    public class RewardPawPickerTests
    {
        const float Hold = 0.8f;
        const float Drain = 2f;
        const float Dt = 1f / 60f;

        static RewardPawPicker ThreeBalls()
        {
            var picker = new RewardPawPicker();
            picker.Reset(3);
            for (var i = 0; i < 3; i++)
            {
                picker.SetBall(i, new Vector2((i - 1) * 500f, 0f), 150f);
            }

            return picker;
        }

        static int Run(RewardPawPicker picker, Vector2 left, Vector2 right, float seconds, bool tracked = true)
        {
            var steps = Mathf.CeilToInt(seconds / Dt);
            for (var i = 0; i < steps; i++)
            {
                var picked = picker.Step(tracked, left, right, Dt, Hold, Drain);
                if (picked >= 0) return picked;
            }

            return -1;
        }

        [Test]
        public void BothPawsOnOneBallPickItAfterTheHold()
        {
            var picker = ThreeBalls();
            var ball = new Vector2(500f, 0f);
            Assert.AreEqual(-1, Run(picker, ball - new Vector2(40f, 0f), ball + new Vector2(40f, 0f), Hold * 0.9f));
            Assert.AreEqual(2, picker.BothHover);
            Assert.Greater(picker.Fill(2), 0.85f);
            Assert.AreEqual(2, Run(picker, ball, ball, Hold * 0.2f));
        }

        [Test]
        public void OnePawOnlyHoversWithoutFilling()
        {
            var picker = ThreeBalls();
            Assert.AreEqual(-1, Run(picker, new Vector2(-500f, 20f), new Vector2(0f, 400f), Hold * 2f));
            Assert.AreEqual(0, picker.LeftHover);
            Assert.AreEqual(-1, picker.RightHover);
            Assert.AreEqual(0, picker.Hovered);
            Assert.AreEqual(0f, picker.Fill(0));
        }

        [Test]
        public void LeavingDrainsTheFillAndSplitPawsNeverPick()
        {
            var picker = ThreeBalls();
            Run(picker, Vector2.zero, Vector2.zero, Hold * 0.5f);
            var filled = picker.Fill(1);
            Assert.Greater(filled, 0.4f);
            Assert.AreEqual(-1, Run(picker, new Vector2(-500f, 0f), new Vector2(500f, 0f), Hold));
            Assert.AreEqual(0f, picker.Fill(1));
            Assert.AreEqual(-1, picker.BothHover);
        }

        [Test]
        public void UntrackedPawsNeverHover()
        {
            var picker = ThreeBalls();
            Run(picker, Vector2.zero, Vector2.zero, Hold * 0.5f);
            Assert.AreEqual(-1, Run(picker, Vector2.zero, Vector2.zero, Hold, tracked: false));
            Assert.AreEqual(-1, picker.Hovered);
            Assert.AreEqual(0f, picker.Fill(1));
        }
    }
}
