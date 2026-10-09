#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// A player's reach in body inches around the chest (x right, y up), mapped linearly onto that player's screen
    /// region (screen-normalized, 0..1, y up). The box takes the region's pixel aspect so the mapping is uniform.
    /// </summary>
    public readonly struct ReachBox
    {
        public readonly Rect region;
        public readonly Vector2 center;
        public readonly Vector2 size;

        public ReachBox(Rect region, Vector2 center, Vector2 size)
        {
            this.region = region;
            this.center = center;
            this.size = size;
        }

        public static ReachBox For(Rect region, Vector2 screenPixels, float heightInches, float centerAboveChestInches)
        {
            var aspect = region.width * screenPixels.x / (region.height * screenPixels.y);
            return new ReachBox(region, new Vector2(0f, centerAboveChestInches), new Vector2(heightInches * aspect, heightInches));
        }

        /// <summary>Screen-normalized position of a hand offset; outside the reach it sticks to the region's edge.</summary>
        public Vector2 ToScreen(Vector2 inches)
        {
            var normalized = new Vector2(
                Mathf.Clamp01((inches.x - center.x) / size.x + 0.5f),
                Mathf.Clamp01((inches.y - center.y) / size.y + 0.5f));
            return region.min + Vector2.Scale(normalized, region.size);
        }

        /// <summary>Hand offset (body inches) that maps onto a screen-normalized position, clamped to the region.</summary>
        public Vector2 ToInches(Vector2 screen)
        {
            var normalized = new Vector2(
                Mathf.Clamp01((screen.x - region.xMin) / region.width),
                Mathf.Clamp01((screen.y - region.yMin) / region.height));
            return center + Vector2.Scale(normalized - new Vector2(0.5f, 0.5f), size);
        }
    }
}
