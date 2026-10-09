#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Scenery that streams toward the camera and wraps around (lane dashes, roadside blocks, clouds).</summary>
    public class ScrollingStrip : MonoBehaviour
    {
        [Header("Items")]
        [SerializeField] Transform[] items = null!;
        [Header("Wrap Length")]
        [Tooltip("Distance an item jumps forward once it passes Min Z.")]
        [SerializeField] float wrapLength = 120f;
        [Header("Min Z")]
        [SerializeField] float minZ = -20f;

        #region Public API

        public void Scroll(float distance)
        {
            foreach (var item in items)
            {
                var position = item.localPosition;
                position.z -= distance;
                if (position.z < minZ) position.z += wrapLength;
                item.localPosition = position;
            }
        }

        #endregion
    }
}
