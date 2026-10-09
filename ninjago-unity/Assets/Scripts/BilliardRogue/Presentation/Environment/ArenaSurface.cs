#nullable enable

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Arena parts whose tiling surface material changes per act (the *_Surface parts of the arena kit, TDD §14.2).
    /// Serialized as ints in prefabs: append only, never reorder.
    /// </summary>
    public enum ArenaSurface
    {
        FloorTop = 0,
        DangerTop = 1,
        LaunchPadTop = 2,
        WallSide = 3,
        WallTop = 4,
        CornerSide = 5,
    }
}
