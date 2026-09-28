#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Rigid-part idle life of an enemy model (research asset-toolchain §6): body bob and breath, eye blinks and
    /// wing flaps, all on the FBX parts the prefab builder wired by name. Paused while frozen.
    /// </summary>
    public sealed class EnemyIdleMotion : MonoBehaviour
    {
        [Header("Parts (wired by WorldPrefabsBuilder)")]
        [Tooltip("Body root that bobs and breathes (Body / Stem / Base / Robe).")]
        [SerializeField] Transform? bobPart;
        [Tooltip("Eye parts scaled on Y for blinks.")]
        [SerializeField] Transform[] blinkParts = System.Array.Empty<Transform>();
        [Tooltip("Wing parts rotated around Z, mirrored by their X side.")]
        [SerializeField] Transform[] flapParts = System.Array.Empty<Transform>();

        JuiceConfig.EnemyMotionSettings settings = null!;
        Vector3 bobBase;
        Vector3 bobBaseScale = Vector3.one;
        float phase;
        float blinkTimer;
        float blinkRemaining;
        bool paused;

        public bool Paused
        {
            get => paused;
            set => paused = value;
        }

        #region Life Cycle

        public void Initialize(JuiceConfig.EnemyMotionSettings aSettings)
        {
            settings = aSettings;
            phase = Random.value * settings.idleBobPeriod;
            blinkTimer = Random.Range(settings.blinkIntervalMin, settings.blinkIntervalMax);
            blinkRemaining = 0f;
            paused = false;
            if (bobPart != null)
            {
                bobBase = bobPart.localPosition;
                bobBaseScale = bobPart.localScale;
            }

            for (var i = 0; i < blinkParts.Length; i++)
            {
                blinkParts[i].localScale = Vector3.one;
            }

            for (var i = 0; i < flapParts.Length; i++)
            {
                flapParts[i].localRotation = Quaternion.identity;
            }
        }

        void Update()
        {
            if (paused) return;
            var dt = Time.deltaTime;
            phase += dt;
            var wave = Mathf.Sin(phase / settings.idleBobPeriod * Mathf.PI * 2f);
            if (bobPart != null)
            {
                bobPart.localPosition = bobBase + new Vector3(0f, settings.idleBobAmplitude * (wave + 1f) * 0.5f, 0f);
                var breath = settings.breathAmplitude * wave;
                bobPart.localScale = new Vector3(bobBaseScale.x * (1f + breath * 0.5f), bobBaseScale.y * (1f - breath), bobBaseScale.z * (1f + breath * 0.5f));
            }

            if (flapParts.Length > 0)
            {
                var flap = Mathf.Sin(phase * settings.flapSpeed) * settings.flapAngle;
                for (var i = 0; i < flapParts.Length; i++)
                {
                    var part = flapParts[i];
                    var side = part.localPosition.x >= 0f ? 1f : -1f;
                    part.localRotation = Quaternion.Euler(0f, 0f, -flap * side);
                }
            }

            if (blinkParts.Length == 0) return;
            if (blinkRemaining > 0f)
            {
                blinkRemaining -= dt;
                var closed = blinkRemaining > 0f;
                SetEyes(closed ? 0.12f : 1f);
                return;
            }

            blinkTimer -= dt;
            if (blinkTimer > 0f) return;
            blinkTimer = Random.Range(settings.blinkIntervalMin, settings.blinkIntervalMax);
            blinkRemaining = settings.blinkDuration;
            SetEyes(0.12f);
        }

        #endregion

        #region Helpers

        void SetEyes(float scaleY)
        {
            for (var i = 0; i < blinkParts.Length; i++)
            {
                blinkParts[i].localScale = new Vector3(1f, scaleY, 1f);
            }
        }

        #endregion
    }
}
