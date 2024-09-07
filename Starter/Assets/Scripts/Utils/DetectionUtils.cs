using UnityEngine;

namespace Nex
{
    public static class DetectionUtils
    {
        public static readonly Vector2 AspectNormalizedFrameSize = new(16f / 9f, 1f);
        public static readonly Rect AspectNormalizedFrameRect = new(Vector2.zero, AspectNormalizedFrameSize);
    }
}
