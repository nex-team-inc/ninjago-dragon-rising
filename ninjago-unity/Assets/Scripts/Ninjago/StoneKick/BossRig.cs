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
        [SerializeField] float armRestDegrees = 12f;
        [Header("Arm Wind-up Angle")]
        [Tooltip("Swung back, away from the player.")]
        [SerializeField] float armWindupDegrees = -70f;
        [Header("Arm Release Angle")]
        [Tooltip("Swung forward and up: where the rock leaves the hand.")]
        [SerializeField] float armReleaseDegrees = 110f;
        [Header("Arm Settle Seconds")]
        [SerializeField] float armSettleSeconds = 0.4f;
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

        /// <summary>Underhand lob: the arm swings back, then forward and up over the given seconds; the rock leaves at the end.</summary>
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
            var elapsed = now - throwStart;
            float angle;
            if (elapsed < throwSeconds * 0.6f)
            {
                angle = Mathf.Lerp(armRestDegrees, armWindupDegrees, Mathf.SmoothStep(0f, 1f, elapsed / (throwSeconds * 0.6f)));
            }
            else if (elapsed < throwSeconds)
            {
                angle = Mathf.Lerp(armWindupDegrees, armReleaseDegrees, (elapsed - throwSeconds * 0.6f) / (throwSeconds * 0.4f));
            }
            else
            {
                angle = Mathf.Lerp(armReleaseDegrees, armRestDegrees, Mathf.SmoothStep(0f, 1f, (elapsed - throwSeconds) / armSettleSeconds));
            }

            armPivot.localRotation = Quaternion.Euler(angle, 0f, 0f);

            var flinch = 1f - Mathf.Clamp01((now - flinchStart) / flinchSeconds);
            var shake = flinch > 0f ? Mathf.Sin(now * 70f) * 0.12f * flinch : 0f;
            bodyRoot.localPosition = restPosition + new Vector3(shake, 0f, flinch * 0.35f);
            bodyRoot.localRotation = Quaternion.Euler(-flinch * 14f, 0f, 0f);
        }

        #endregion
    }
}
