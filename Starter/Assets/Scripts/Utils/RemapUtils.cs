#nullable enable

using UnityEngine;

namespace Nex.Utils
{
    // Ported from Bluey (original author: Wangshu)
    public static class RemapUtils
    {
        public static float Remap(float x, float a, float b, float c, float d)
        {
            if (Mathf.Approximately(a, b)) return c;
            float t = (x - a) / (b - a);
            return c + t * (d - c);
        }

        public static float RemapAndClamp(float x, float a, float b, float c, float d, float lowerMargin, float upperMargin)
        {
            float result = Remap(x, a, b, c, d);
            return Mathf.Clamp(result, Mathf.Min(c, d) + lowerMargin, Mathf.Max(c, d) - upperMargin);
        }
    }
}
