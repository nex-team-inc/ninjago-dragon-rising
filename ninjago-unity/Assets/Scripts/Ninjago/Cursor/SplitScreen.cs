#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Screen regions (normalized, y up) of the split-screen layout: the full screen alone, halves for two.</summary>
    public static class SplitScreen
    {
        public static Rect Region(int slot, int slotCount) => slotCount > 1 ? new Rect(slot * 0.5f, 0f, 0.5f, 1f) : new Rect(0f, 0f, 1f, 1f);
    }
}
