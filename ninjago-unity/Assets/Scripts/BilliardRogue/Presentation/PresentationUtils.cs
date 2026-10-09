#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>World-layer helpers shared by the presentation views and pools.</summary>
    public static class WorldLayers
    {
        public const string WorldLayerName = "World";

        /// <summary>Index of the World layer, or -1 when RenderPipelineBuilder has not created it yet.</summary>
        public static int Resolve() => LayerMask.NameToLayer(WorldLayerName);

        public static void Apply(GameObject root, int layer)
        {
            if (layer < 0) return;
            root.layer = layer;
            var t = root.transform;
            for (var i = 0; i < t.childCount; i++)
            {
                Apply(t.GetChild(i).gameObject, layer);
            }
        }
    }

    /// <summary>Cached decimal strings so damage numbers never build strings on the hot path.</summary>
    public static class NumberStrings
    {
        const int CacheSize = 1000;
        static readonly string[] cache = new string[CacheSize];

        public static string Get(int value)
        {
            if (value < 0 || value >= CacheSize) return value.ToString();
            return cache[value] ??= value.ToString();
        }
    }

    /// <summary>Allocation-free easing used by the manual view tweens.</summary>
    public static class Easing
    {
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);

        public static float InQuad(float t) => t * t;

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = 2f * Mathf.PI / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        /// <summary>Parabolic arc 0 → 1 → 0.</summary>
        public static float Arc(float t) => 4f * t * (1f - t);

        /// <summary>Punch 0 → 1 → 0 with a quick attack and slower decay.</summary>
        public static float Punch(float t) => t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
    }
}
