using UnityEngine;

namespace Nex.Utils
{
    [System.Serializable]
    public struct FloatRange
    {
        public float min;
        public float max;

        public float RandomValue => Random.Range(min, max);

        public FloatRange(float min, float max)
        {
            this.min = min;
            this.max = max;
        }
    }
}