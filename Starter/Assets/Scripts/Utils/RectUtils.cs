using UnityEngine;

namespace Nex.Utils
{
    public static class RectUtils
    {
        public static Rect GetIntersection(Rect rect1, Rect rect2)
        {
            var xMin = Mathf.Max(rect1.xMin, rect2.xMin);
            var yMin = Mathf.Max(rect1.yMin, rect2.yMin);
            var xMax = Mathf.Min(rect1.xMax, rect2.xMax);
            var yMax = Mathf.Min(rect1.yMax, rect2.yMax);

            if (xMin < xMax && yMin < yMax)
            {
                return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
            }

            // No intersection
            return Rect.zero;
        }

        public static Rect FromFrameSpaceToNormalizedSpace(Rect frameRect, Vector2 frameSize)
        {
            var x = frameRect.x / frameSize.x;
            var y = frameRect.y / frameSize.y;
            var width = frameRect.width / frameSize.x;
            var height = frameRect.height / frameSize.y;
            return new Rect(x, y, width, height);
        }

        public static Rect MirrorNormalizedRect(Rect rect)
        {
            return new Rect(1 - rect.x - rect.width, rect.y, rect.width, rect.height);
        }
    }
}
