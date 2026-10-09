#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The grey block brute and its staff. The courtyard drives the staff angle and stance directly so the sweep
    /// lands exactly on the slip window; the rig only applies them and adds an idle sway.
    /// </summary>
    public class BruteRig : MonoBehaviour
    {
        [Header("Body Root")]
        [SerializeField] Transform bodyRoot = null!;
        [Header("Staff Pivot")]
        [Tooltip("The staff lies along local -Z. Yaw 0 points it at the ninja, positive yaw swings it to screen left, positive lift raises the tip.")]
        [SerializeField] Transform staffPivot = null!;
        [Header("Staff Trail")]
        [SerializeField] TrailRenderer staffTrail = null!;
        [Header("Staff Tip")]
        [SerializeField] Transform staffTip = null!;
        [Header("Idle Sway Degrees")]
        [SerializeField] float idleSwayDegrees = 2.5f;
        [Header("Open Lean Degrees")]
        [Tooltip("Backward lean when the brute is open (staff missed) or hit by the spin.")]
        [SerializeField] float openLeanDegrees = 16f;

        float stance;
        float swayTime;

        #region Public API

        public Vector3 StaffTipPosition => staffTip.position;

        public void SetStaff(float yawDegrees, float liftDegrees)
        {
            staffPivot.localRotation = Quaternion.Euler(liftDegrees, yawDegrees, 0f);
        }

        public void SetTrail(bool emitting)
        {
            staffTrail.emitting = emitting;
            if (!emitting) staffTrail.Clear();
        }

        /// <summary>0 = squared up, 1 = rocked back open.</summary>
        public void SetStance(float open01)
        {
            stance = open01;
        }

        public void Tick(float deltaTime)
        {
            swayTime += deltaTime;
            var sway = Mathf.Sin(swayTime * 2.2f) * idleSwayDegrees * (1f - stance);
            bodyRoot.localRotation = Quaternion.Euler(stance * openLeanDegrees, 0f, sway);
        }

        #endregion
    }
}
