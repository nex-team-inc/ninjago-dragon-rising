#nullable enable

using System;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Emissive pulse of the danger-row inlays. Presentation-Core reports how threatened the row is through
    /// SetDangerLevel (0 = empty: steady dim glow, 1 = fully occupied: fast bright pulse). All inlays share one
    /// runtime material instance so they stay in one SRP batch; the pulse runs on unscaled time so hit-stop and
    /// pause never freeze it. The component starts disabled and enables itself in Initialize.
    /// </summary>
    public sealed class DangerRowPulse : MonoBehaviour
    {
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        const float SettledEpsilon = 0.001f;

        [Tooltip("Inlay renderers (DangerInlay_Emissive parts), one per column.")]
        [SerializeField] Renderer[] inlays = Array.Empty<Renderer>();

        ArenaConfig.DangerRowSettings settings = null!;
        Material? material;
        float targetLevel;
        float level;
        float phase;
        bool settledIdle;

        /// <summary>The last level requested through SetDangerLevel.</summary>
        public float DangerLevel => targetLevel;

        public void Initialize(ArenaConfig.DangerRowSettings dangerSettings)
        {
            settings = dangerSettings;
            if (material == null)
            {
                var instance = new Material(inlays[0].sharedMaterial) { name = "DangerRowPulse (runtime)" };
                foreach (var inlay in inlays)
                {
                    inlay.sharedMaterial = instance;
                }

                material = instance;
            }

            targetLevel = 0f;
            level = 0f;
            settledIdle = false;
            ApplyStrength(settings.idleStrength);
            enabled = true;
        }

        /// <summary>0 = danger row empty, 1 = fully occupied / imminent hit. Values are clamped.</summary>
        public void SetDangerLevel(float level01)
        {
            targetLevel = Mathf.Clamp01(level01);
            if (targetLevel > 0f) settledIdle = false;
        }

        void Update()
        {
            if (settledIdle) return;
            var dt = Time.unscaledDeltaTime;
            level += (targetLevel - level) * (1f - Mathf.Exp(-settings.levelResponse * dt));
            if (targetLevel <= 0f && level < SettledEpsilon)
            {
                level = 0f;
                settledIdle = true;
                ApplyStrength(settings.idleStrength);
                return;
            }

            phase += dt * Mathf.Lerp(settings.calmPulseHz, settings.pulseHz, level);
            if (phase > 1f) phase -= 1f;
            var wave = 0.5f + 0.5f * Mathf.Sin(phase * 2f * Mathf.PI);
            var pulse = Mathf.Lerp(settings.pulseMinStrength, settings.pulseMaxStrength, wave);
            ApplyStrength(Mathf.Lerp(settings.idleStrength, pulse, level));
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        void ApplyStrength(float strength)
        {
            material!.SetColor(EmissionColorId, settings.emissionColor * strength);
        }
    }
}
