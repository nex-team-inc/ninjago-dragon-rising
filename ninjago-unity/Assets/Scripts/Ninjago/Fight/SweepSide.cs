namespace Nex.Ninjago
{
    // Screen sides. The camera feed is mirrored, so the player's own left is screen left.
    public enum SweepSide
    {
        Left = 0,
        Right = 1,
    }

    public static class SweepSideExtensions
    {
        public static SweepSide Opposite(this SweepSide side) => side == SweepSide.Left ? SweepSide.Right : SweepSide.Left;

        public static float Sign(this SweepSide side) => side == SweepSide.Left ? -1f : 1f;
    }
}
