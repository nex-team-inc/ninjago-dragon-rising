#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Tier-3 Hype aura (GDD v2 §3): one looped LineRenderer (M_BallTrail, additive HDR) around the arena rim that fades
    /// in and pulses while Hype is at the top tier. A single persistent line: no pooling, one draw call, no GC.
    /// </summary>
    public sealed class HypeAuraView : MonoBehaviour
    {
        const int Corners = 4;

        [Header("Wiring (BoardPrefabBuilder)")]
        [SerializeField] LineRenderer line = null!;

        JuiceConfig.HypeSettings settings = null!;
        readonly Vector3[] corners = new Vector3[Corners];
        Color color = Color.white;
        float baseWidth;
        float fade;
        float phase;
        bool visible;

        public float Fade => fade;

        public void Initialize(ArenaLayout layout, JuiceConfig juice)
        {
            settings = juice.Hype;
            var size = layout.SimSize;
            var height = settings.auraHeight;
            corners[0] = layout.ToWorld(new Vector2(0f, 0f), height);
            corners[1] = layout.ToWorld(new Vector2(0f, size.y), height);
            corners[2] = layout.ToWorld(new Vector2(size.x, size.y), height);
            corners[3] = layout.ToWorld(new Vector2(size.x, 0f), height);
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = Corners;
            line.SetPositions(corners);
            baseWidth = settings.auraWidth * layout.CellSize;
            fade = 0f;
            visible = false;
            line.enabled = false;
        }

        public void SetVisible(bool isVisible, Color aColor)
        {
            visible = isVisible;
            color = aColor;
        }

        void Update()
        {
            if (settings == null) return;
            var step = Time.unscaledDeltaTime / Mathf.Max(0.01f, settings.auraFade);
            fade = Mathf.MoveTowards(fade, visible ? 1f : 0f, step);
            if (fade <= 0f)
            {
                if (line.enabled) line.enabled = false;
                return;
            }

            if (!line.enabled) line.enabled = true;
            phase += Time.unscaledDeltaTime / Mathf.Max(0.05f, settings.auraPulsePeriod);
            var pulse = 0.5f + 0.5f * Mathf.Sin(phase * Mathf.PI * 2f);
            var strength = fade * (1f - settings.auraPulseAmount + settings.auraPulseAmount * pulse);
            line.widthMultiplier = baseWidth * (0.6f + 0.4f * fade) * (1f + 0.5f * settings.auraPulseAmount * pulse);
            var c = new Color(color.r, color.g, color.b, strength);
            line.startColor = c;
            line.endColor = c;
        }
    }
}
