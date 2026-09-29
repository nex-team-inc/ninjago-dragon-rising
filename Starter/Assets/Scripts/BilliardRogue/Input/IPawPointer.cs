#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Hands as on-screen pointers for motion UI such as the reward pick (GDD v2 §4).</summary>
    public interface IPawPointer
    {
        /// <summary>Left/right paw in normalized screen space (0..1, y up); false while the paws are not tracked.</summary>
        bool TryGetPaws(out Vector2 left01, out Vector2 right01);
    }
}
