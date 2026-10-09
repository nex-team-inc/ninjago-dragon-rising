#nullable enable

using System;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Nex.BilliardRogue
{
    /// <summary>How one floating text looks and moves (WorldLabelLayer fills it from JuiceConfig).</summary>
    public struct NumberStyle
    {
        /// <summary>Deep colour at the bottom of the gradient.</summary>
        public Color color;
        /// <summary>Light colour at the top of the gradient.</summary>
        public Color highlight;
        public Color outline;
        /// <summary>Font size; multiples of 16 stay crisp.</summary>
        public float size;
        /// <summary>Pop-in overshoot (0 = none).</summary>
        public float pop;
        public float lifetime;
        /// <summary>Offset from the anchor at spawn and launch velocity, canvas pixels (per second).</summary>
        public Vector2 start;
        public Vector2 velocity;
        public float gravity;
        /// <summary>Lean at launch in degrees, easing upright.</summary>
        public float tilt;
        /// <summary>Impact shake in pixels over the first third of the life.</summary>
        public float shake;
        /// <summary>Seconds shown white-hot before the colours.</summary>
        public float flashSeconds;
        /// <summary>Rainbow cycle in hue turns per second (0 = the colours above).</summary>
        public float rainbowSpeed;
        public float rainbowPhase;
    }

    /// <summary>
    /// Pooled floating text (damage number, BLOCK / CRIT / POWER, combo) above a world point: a gradient text over an
    /// outline copy in the outline font (the same glyphs dilated 1 px) dropped one font pixel for a 3D pixel edge.
    /// It pops in, arcs under gravity, leans and fades; the colours change only on the flash and the crit rainbow's
    /// 12 fps steps, the fade goes through the CanvasRenderer alpha, so TMP rebuilds its mesh rarely.
    /// WorldLabelLayer owns ageing and projection so the text stays attached under camera shake.
    /// </summary>
    public sealed class DamageNumber : MonoBehaviour, IPoolableObject
    {
        const float FontRasterPx = 16f;
        const float RainbowStepSeconds = 1f / 12f;
        const float FadeStart = 0.7f;

        public event Action<Component>? OnRelease;

        [Header("Wiring (BoardPrefabBuilder)")]
        [SerializeField] RectTransform rect = null!;
        [SerializeField] TextMeshProUGUI text = null!;
        [Tooltip("Outline font copy of the text, drawn under it.")]
        [SerializeField] TextMeshProUGUI outline = null!;

        NumberStyle style;
        bool flashed;
        float rainbowTimer;

        public RectTransform Rect => rect;
        public Vector3 WorldPosition { get; private set; }
        public float Age { get; private set; }
        /// <summary>Canvas-pixel offset from the projected anchor this frame.</summary>
        public Vector2 Offset { get; private set; }

        public void Show(string value, in NumberStyle aStyle, Vector3 world)
        {
            style = aStyle;
            style.lifetime = Mathf.Max(0.05f, style.lifetime);
            WorldPosition = world;
            Age = 0f;
            Offset = style.start;
            flashed = style.flashSeconds <= 0f;
            rainbowTimer = 0f;

            text.SetText(value);
            outline.SetText(value);
            text.fontSize = style.size;
            outline.fontSize = style.size;
            text.enableVertexGradient = true;
            text.color = Color.white;
            outline.color = style.outline;
            outline.rectTransform.anchoredPosition = new Vector2(0f, -Mathf.Max(1f, Mathf.Round(style.size / FontRasterPx)));
            if (flashed) ShowColours();
            else SetGradient(Color.white, Color.Lerp(style.color, Color.white, 0.75f));
            SetAlpha(1f);

            rect.anchoredPosition = new Vector2(-9999f, -9999f);
            rect.localScale = Vector3.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, style.tilt);
            // The newest number draws over older ones and over the HP labels.
            rect.SetAsLastSibling();
        }

        /// <summary>Advances the age; returns false once the text has expired (caller releases it).</summary>
        public bool Tick(float dt)
        {
            Age += dt;
            var t = Age / style.lifetime;
            if (t >= 1f) return false;

            if (!flashed && Age >= style.flashSeconds)
            {
                flashed = true;
                ShowColours();
            }
            else if (flashed && style.rainbowSpeed > 0f)
            {
                StepRainbow(dt);
            }

            // Pop in (overshooting by pop), settle, hold, then shrink while fading out.
            var pop = 1f + style.pop;
            var scale = t < 0.1f ? Easing.OutBack(t / 0.1f) * pop
                : t < 0.28f ? Mathf.Lerp(pop, 1f, Easing.OutQuad((t - 0.1f) / 0.18f))
                : t > FadeStart ? Mathf.Lerp(1f, 0.6f, (t - FadeStart) / (1f - FadeStart))
                : 1f;
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.localRotation = Quaternion.Euler(0f, 0f, style.tilt * (1f - Easing.OutQuad(Mathf.Min(1f, t * 2f))));
            SetAlpha(t > FadeStart ? 1f - (t - FadeStart) / (1f - FadeStart) : 1f);

            var offset = style.start + style.velocity * Age;
            offset.y -= 0.5f * style.gravity * Age * Age;
            if (style.shake > 0f && t < 0.33f)
            {
                offset += Random.insideUnitCircle * (style.shake * (1f - t / 0.33f));
            }

            Offset = offset;
            return true;
        }

        public void Release()
        {
            OnRelease?.Invoke(this);
        }

        void ShowColours()
        {
            if (style.rainbowSpeed > 0f)
            {
                rainbowTimer = 0f;
                StepRainbow(0f);
                return;
            }

            SetGradient(style.highlight, style.color);
        }

        void StepRainbow(float dt)
        {
            rainbowTimer -= dt;
            if (rainbowTimer > 0f) return;
            rainbowTimer += RainbowStepSeconds;
            var hue = Mathf.Repeat(style.rainbowPhase + Age * style.rainbowSpeed, 1f);
            SetGradient(Color.HSVToRGB(hue, 0.3f, 1f), Color.HSVToRGB(Mathf.Repeat(hue + 0.12f, 1f), 0.95f, 1f));
        }

        void SetGradient(Color top, Color bottom)
        {
            text.colorGradient = new VertexGradient(top, top, bottom, bottom);
        }

        void SetAlpha(float alpha)
        {
            text.canvasRenderer.SetAlpha(alpha);
            outline.canvasRenderer.SetAlpha(alpha);
        }
    }
}
