using UnityEngine;

namespace Nex.Utils
{
    public static class Vector2Utils
    {
        public static Vector2 PolarDeg(float angleDeg, float radius = 1) => Polar(angleDeg * Mathf.Deg2Rad, radius);

        public static Vector2 Polar(float angle, float radius = 1)
        {
            var cos = Mathf.Cos(angle);
            var sin = Mathf.Sin(angle);
            return new Vector2(radius * cos, radius * sin);
        }

        public static float ToPolarDeg(this Vector2 vector) =>
            (Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg + 360) % 360;
    }
}
