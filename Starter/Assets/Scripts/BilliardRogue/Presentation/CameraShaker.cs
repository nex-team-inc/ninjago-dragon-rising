#nullable enable

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Trauma-based shake and push-in for the world camera rig. Offsets the rig transform (the camera's parent when
    /// it has one, otherwise the camera) along its right/up/forward from a base pose captured at Initialize, so the
    /// PixelWorldDisplay texel snapping still sees one consistent camera pose per frame. Amplitudes are given in
    /// world-display pixels (JuiceConfig) and converted at the arena focus distance. Honours PlayerPreference.screenShake.
    /// </summary>
    public sealed class CameraShaker : MonoBehaviour
    {
        [Header("Shake")]
        [Tooltip("Trauma lost per second (trauma² scales the amplitude).")]
        [SerializeField, Range(0.5f, 10f)] float traumaDecay = 3.2f;
        [SerializeField, Range(1f, 60f)] float noiseFrequency = 22f;
        [Tooltip("Roll in degrees at full trauma.")]
        [SerializeField, Range(0f, 5f)] float maxRoll = 0.8f;

        Transform rig = null!;
        Vector3 basePosition;
        Quaternion baseRotation;
        float trauma;
        float amplitudeWorld;
        float worldPerPixel = 0.02f;
        float pushIn;
        float noiseSeed;
        bool initialized;

        public float PushIn => pushIn;

        #region Life Cycle

        /// <summary>worldCamera: the camera whose rig is shaken; focus: the world point the pixel size is measured at.</summary>
        public void Initialize(Camera worldCamera, Vector3 focus, int renderHeight)
        {
            var cameraTransform = worldCamera.transform;
            var parent = cameraTransform.parent;
            rig = parent != null ? parent : cameraTransform;
            CaptureBase();
            var distance = Mathf.Max(1f, Vector3.Dot(focus - cameraTransform.position, cameraTransform.forward));
            worldPerPixel = 2f * distance * Mathf.Tan(0.5f * worldCamera.fieldOfView * Mathf.Deg2Rad) / Mathf.Max(90, renderHeight);
            noiseSeed = UnityEngine.Random.value * 100f;
            trauma = 0f;
            pushIn = 0f;
            initialized = true;
        }

        void LateUpdate()
        {
            if (!initialized) return;
            if (trauma > 0f)
            {
                trauma = Mathf.Max(0f, trauma - Time.deltaTime * traumaDecay);
            }

            var shake = trauma * trauma;
            var t = Time.time * noiseFrequency;
            var ox = (Mathf.PerlinNoise(noiseSeed, t) * 2f - 1f) * amplitudeWorld * shake;
            var oy = (Mathf.PerlinNoise(noiseSeed + 17f, t) * 2f - 1f) * amplitudeWorld * shake;
            var roll = (Mathf.PerlinNoise(noiseSeed + 41f, t) * 2f - 1f) * maxRoll * shake;
            var right = baseRotation * Vector3.right;
            var up = baseRotation * Vector3.up;
            var forward = baseRotation * Vector3.forward;
            rig.SetPositionAndRotation(basePosition + right * ox + up * oy + forward * pushIn, baseRotation * Quaternion.Euler(0f, 0f, roll));
        }

        #endregion

        #region Public Methods

        /// <summary>Re-captures the rest pose (call after the rig has been repositioned by its owner).</summary>
        public void CaptureBase()
        {
            basePosition = rig.position;
            baseRotation = rig.rotation;
        }

        /// <summary>Adds shake; amplitudePixels is the peak offset in world-display pixels.</summary>
        public void Shake(float amplitudePixels)
        {
            if (!initialized || amplitudePixels <= 0f) return;
            if (!PlayerDataManager.Instance.PlayerPreference.screenShake) return;
            var amplitude = amplitudePixels * worldPerPixel;
            // A stronger hit re-scales the noise instead of only adding trauma, so big shakes read big.
            amplitudeWorld = Mathf.Max(amplitude, amplitudeWorld * trauma);
            trauma = Mathf.Clamp01(trauma + Mathf.Clamp01(amplitudePixels / 12f));
        }

        /// <summary>Moves the rig forward by distance over inDuration, holds, then returns over outDuration.</summary>
        public async UniTask PushInAsync(float distance, float inDuration, float hold, float outDuration, CancellationToken ct)
        {
            await Tween(pushIn, distance, inDuration, ct);
            if (hold > 0f) await UniTask.Delay(TimeSpan.FromSeconds(hold), cancellationToken: ct);
            await Tween(pushIn, 0f, outDuration, ct);
        }

        public void ResetPushIn()
        {
            pushIn = 0f;
        }

        #endregion

        #region Helpers

        async UniTask Tween(float from, float to, float duration, CancellationToken ct)
        {
            if (duration <= 0f)
            {
                pushIn = to;
                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                ct.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                pushIn = Mathf.Lerp(from, to, Easing.OutQuad(Mathf.Clamp01(elapsed / duration)));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            pushIn = to;
        }

        #endregion
    }
}
