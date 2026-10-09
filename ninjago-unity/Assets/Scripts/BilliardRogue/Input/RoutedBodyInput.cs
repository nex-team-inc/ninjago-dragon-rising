#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// ShotInputRouter's MotionEnergy and PawPointer: one stable object consumers may cache, following the active
    /// shot input source. Energy = max(body meter, the active source's simulated energy: debug keys / mouse, bot);
    /// tracked when either is. Paws = the body pointer while it tracks, else the active source's fallback pointer
    /// (the debug mouse), else none.
    /// </summary>
    public sealed class RoutedBodyInput : IMotionEnergy, IPawPointer
    {
        readonly IMotionEnergy? body;
        readonly IPawPointer? bodyPointer;
        IMotionEnergy? simulated;
        IPawPointer? fallbackPointer;

        public RoutedBodyInput(IMotionEnergy? aBody, IPawPointer? aBodyPointer)
        {
            body = aBody;
            bodyPointer = aBodyPointer;
        }

        public float Energy01 => Mathf.Max(body?.Energy01 ?? 0f, simulated?.Energy01 ?? 0f);

        public bool IsTracked => (body?.IsTracked ?? false) || (simulated?.IsTracked ?? false);

        /// <summary>The active source's simulated energy and fallback pointer (null for the body source).</summary>
        public void Route(IMotionEnergy? aSimulated, IPawPointer? aFallbackPointer)
        {
            simulated = aSimulated;
            fallbackPointer = aFallbackPointer;
        }

        public bool TryGetPaws(out Vector2 left01, out Vector2 right01)
        {
            if (bodyPointer != null && bodyPointer.TryGetPaws(out left01, out right01)) return true;
            if (fallbackPointer != null) return fallbackPointer.TryGetPaws(out left01, out right01);
            left01 = default;
            right01 = default;
            return false;
        }
    }
}
