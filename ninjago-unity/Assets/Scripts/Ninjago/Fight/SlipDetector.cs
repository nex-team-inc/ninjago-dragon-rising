#nullable enable

namespace Nex.Ninjago
{
    /// <summary>
    /// Judges one slip from the chest lean. The first threshold crossing decides: toward the safe side is a slip,
    /// toward the danger side locks a wrong-way miss, so swaying both ways cannot pass every staff.
    /// </summary>
    public sealed class SlipDetector
    {
        public enum Outcome
        {
            Pending,
            Slipped,
            WrongWay,
        }

        SweepSide safeSide;
        float thresholdInches;

        public Outcome Current { get; private set; }

        public void Begin(SweepSide aSafeSide, float aThresholdInches)
        {
            safeSide = aSafeSide;
            thresholdInches = aThresholdInches;
            Current = Outcome.Pending;
        }

        /// <param name="leanXInches">Chest lean from the calibrated center, positive to the right.</param>
        public Outcome Update(float leanXInches)
        {
            if (Current != Outcome.Pending) return Current;
            var towardSafe = leanXInches * safeSide.Sign();
            if (towardSafe >= thresholdInches)
            {
                Current = Outcome.Slipped;
            }
            else if (-towardSafe >= thresholdInches)
            {
                Current = Outcome.WrongWay;
            }

            return Current;
        }
    }
}
