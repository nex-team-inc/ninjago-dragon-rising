#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// The block-built ninja: slips sideways, staggers when hit, spins when the swirl fills. Animated procedurally in
    /// its courtyard's local time (Tick).
    /// </summary>
    public class NinjaRig : MonoBehaviour
    {
        [Header("Body Root")]
        [SerializeField] Transform bodyRoot = null!;
        [Header("Player Color Renderers")]
        [SerializeField] Renderer[] colorRenderers = null!;
        [Header("Player Marker")]
        [SerializeField] PlayerTagLabel marker = null!;
        [Header("Swirl Ring Root")]
        [SerializeField] GameObject swirlRingRoot = null!;
        [Header("Swirl Ring Fill")]
        [SerializeField] Image swirlRing = null!;
        [Header("Slip Distance")]
        [SerializeField] float slipDistance = 1.1f;
        [Header("Slip Lean Degrees")]
        [SerializeField] float slipLeanDegrees = 20f;
        [Header("Slip Sharpness")]
        [Tooltip("Exponential approach rate toward the slip target, per second of courtyard time.")]
        [SerializeField] float slipSharpness = 16f;
        [Header("Spin Turns")]
        [SerializeField] float spinTurns = 4f;
        [Header("Hit Tilt Degrees")]
        [SerializeField] float hitTiltDegrees = 28f;
        [Header("Out Color")]
        [SerializeField] Color outColor = new(0.45f, 0.45f, 0.45f);

        float slipTarget;
        float slip;
        float leanHint;
        float spinElapsed;
        float spinDuration;
        float hitElapsed;
        float hitDuration;
        bool isOut;

        #region Initialization

        public void Initialize(int playerIndex, Color color)
        {
            foreach (var colorRenderer in colorRenderers)
            {
                colorRenderer.material.color = color;
            }

            marker.Initialize(playerIndex, color);
            swirlRing.color = color;
            ShowSwirl(false);
        }

        #endregion

        #region Public API

        public Vector3 FeetPosition => bodyRoot.position;

        public void SlipTo(SweepSide side)
        {
            slipTarget = side.Sign();
            leanHint = 0f;
        }

        public void ReturnToCenter()
        {
            slipTarget = 0f;
        }

        /// <summary>A small tilt that follows the player's chest before the slip decides (-1..1 of a full lean).</summary>
        public void SetLeanHint(float amount)
        {
            leanHint = Mathf.Clamp(amount, -1f, 1f);
        }

        public void PlayHit(float duration)
        {
            hitElapsed = 0f;
            hitDuration = duration;
        }

        public void PlaySpin(float duration)
        {
            spinElapsed = 0f;
            spinDuration = duration;
        }

        public void ShowSwirl(bool visible)
        {
            swirlRingRoot.SetActive(visible);
            swirlRing.fillAmount = 0f;
        }

        public void SetSwirlFill(float fill01)
        {
            swirlRing.fillAmount = fill01;
        }

        public void SetOut()
        {
            isOut = true;
            foreach (var colorRenderer in colorRenderers)
            {
                colorRenderer.material.color = outColor;
            }
        }

        public void Tick(float deltaTime)
        {
            slip = Mathf.Lerp(slip, slipTarget, 1f - Mathf.Exp(-slipSharpness * deltaTime));
            var sideways = slip + leanHint * 0.3f;
            var roll = -sideways * slipLeanDegrees;

            var yaw = 0f;
            if (spinElapsed < spinDuration)
            {
                spinElapsed += deltaTime;
                var t = Mathf.Clamp01(spinElapsed / spinDuration);
                yaw = (1f - (1f - t) * (1f - t)) * spinTurns * 360f;
            }

            var pitch = 0f;
            if (hitElapsed < hitDuration)
            {
                hitElapsed += deltaTime;
                pitch = -Mathf.Sin(Mathf.Clamp01(hitElapsed / hitDuration) * Mathf.PI) * hitTiltDegrees;
            }

            if (isOut) pitch = -80f;
            var offset = new Vector3(sideways * slipDistance, 0f, 0f);
            bodyRoot.localPosition = offset;
            bodyRoot.localRotation = Quaternion.Euler(pitch, yaw, roll);
            // The ring stays flat on the ground under the ninja: it follows the slip, not the lean or the spin.
            var ring = swirlRingRoot.transform;
            ring.localPosition = new Vector3(offset.x, ring.localPosition.y, ring.localPosition.z);
        }

        #endregion
    }
}
