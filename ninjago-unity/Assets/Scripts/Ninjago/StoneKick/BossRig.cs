#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>The grey block boss on the far side of a Stone Kick half: swings its arm to throw, flinches when hit.</summary>
    public class BossRig : MonoBehaviour
    {
        [Header("Body Root")]
        [SerializeField] Transform bodyRoot = null!;
        [Header("Throwing Arm Pivot")]
        [Tooltip("Shoulder pivot; the arm hangs along its local -Y.")]
        [SerializeField] Transform armPivot = null!;
        [Header("Hand Point")]
        [Tooltip("Where a thrown rock leaves the boss.")]
        [SerializeField] Transform handPoint = null!;
        [Header("Chest Point")]
        [Tooltip("Where kicked pieces hit the boss.")]
        [SerializeField] Transform chestPoint = null!;
        [Header("Player Marker")]
        [SerializeField] PlayerTagLabel marker = null!;
        [Header("Arm Rest Angle")]
        [SerializeField] float armRestDegrees = 15f;
        [Header("Arm Wind-up Angle")]
        [SerializeField] float armWindupDegrees = -150f;
        [Header("Flinch Seconds")]
        [SerializeField] float flinchSeconds = 0.35f;

        float throwStart = float.NegativeInfinity;
        float throwSeconds = 1f;
        float flinchStart = float.NegativeInfinity;
        Vector3 restPosition;

        public Vector3 HandPosition => handPoint.position;
        public Vector3 ChestPosition => chestPoint.position;

        #region Initialization

        public void Initialize(int playerIndex, Color color)
        {
            marker.Initialize(playerIndex, color);
            restPosition = bodyRoot.localPosition;
        }

        #endregion

        #region Public API

        /// <summary>Winds the arm back and swings it forward over the given seconds; the rock leaves at the end.</summary>
        public void PlayThrow(float seconds)
        {
            throwStart = Time.unscaledTime;
            throwSeconds = Mathf.Max(0.05f, seconds);
        }

        public void PlayFlinch()
        {
            flinchStart = Time.unscaledTime;
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            var now = Time.unscaledTime;
            var t = Mathf.Clamp01((now - throwStart) / throwSeconds);
            // Back over the first 70%, then a fast swing through and past the rest angle.
            var angle = t < 0.7f
                ? Mathf.Lerp(armRestDegrees, armWindupDegrees, Mathf.SmoothStep(0f, 1f, t / 0.7f))
                : Mathf.Lerp(armWindupDegrees, armRestDegrees, (t - 0.7f) / 0.3f);
            armPivot.localRotation = Quaternion.Euler(angle, 0f, 0f);

            var flinch = 1f - Mathf.Clamp01((now - flinchStart) / flinchSeconds);
            var shake = flinch > 0f ? Mathf.Sin(now * 70f) * 0.12f * flinch : 0f;
            bodyRoot.localPosition = restPosition + new Vector3(shake, 0f, flinch * 0.35f);
            bodyRoot.localRotation = Quaternion.Euler(-flinch * 14f, 0f, 0f);
        }

        #endregion
    }
}
