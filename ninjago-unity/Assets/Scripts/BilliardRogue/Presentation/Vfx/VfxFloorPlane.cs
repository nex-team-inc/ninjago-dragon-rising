#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Collision plane of a pooled VFX prefab (TDD D13: debris bounces on the floor). VfxManager moves and scales the
    /// effect root on every play, so the plane child re-pins itself to the floor height with an upright normal after
    /// all Update calls; the particle simulation of the next frame then collides against the real floor.
    /// </summary>
    public sealed class VfxFloorPlane : MonoBehaviour
    {
        [Tooltip("World-space height of the floor the particles collide with (the arena floor sits at y = 0).")]
        [SerializeField] float floorHeight;

        void OnEnable() => Pin();

        void LateUpdate() => Pin();

        void Pin()
        {
            var planeTransform = transform;
            var position = planeTransform.position;
            if (Mathf.Approximately(position.y, floorHeight) && planeTransform.rotation == Quaternion.identity) return;
            planeTransform.SetPositionAndRotation(new Vector3(position.x, floorHeight, position.z), Quaternion.identity);
        }
    }
}
