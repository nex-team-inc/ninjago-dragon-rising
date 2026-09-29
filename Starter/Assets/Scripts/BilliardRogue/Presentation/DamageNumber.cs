#nullable enable

using System;
using TMPro;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Pooled floating text (damage number, BLOCK / CRIT / POWER, combo) that rises and fades above a world point.
    /// The WorldLabelLayer owns ageing and projection so the text stays attached under camera shake.
    /// </summary>
    public sealed class DamageNumber : MonoBehaviour, IPoolableObject
    {
        public event Action<Component>? OnRelease;

        [Header("Wiring (BoardPrefabBuilder)")]
        [SerializeField] RectTransform rect = null!;
        [SerializeField] TextMeshProUGUI text = null!;

        Color color = Color.white;
        float pop;

        public RectTransform Rect => rect;
        public Vector3 WorldPosition { get; private set; }
        public Vector2 Jitter { get; private set; }
        public float Age { get; private set; }
        public float Lifetime { get; private set; } = 1f;
        public float Rise { get; private set; }

        /// <summary>pop: extra overshoot of the pop-in (0 = the plain pop; Hype tiers make it larger).</summary>
        public void Show(string value, Color aColor, float size, Vector3 world, float lifetime, float rise, Vector2 jitter, float aPop = 0f)
        {
            pop = Mathf.Max(0f, aPop);
            text.SetText(value);
            color = aColor;
            text.color = aColor;
            text.fontSize = size;
            WorldPosition = world;
            Lifetime = Mathf.Max(0.05f, lifetime);
            Rise = rise;
            Jitter = jitter;
            Age = 0f;
            rect.anchoredPosition = new Vector2(-9999f, -9999f);
            rect.localScale = Vector3.one;
        }

        /// <summary>Advances the age; returns false once the number has expired (caller releases it).</summary>
        public bool Tick(float dt)
        {
            Age += dt;
            var t = Age / Lifetime;
            if (t >= 1f) return false;
            // Pop in (overshooting by pop), settle, hold, then fade out over the last 40%.
            var scale = t < 0.12f ? Easing.OutBack(t / 0.12f) * (1f + pop) : t < 0.3f ? Mathf.Lerp(1f + pop, 1f, Easing.OutQuad((t - 0.12f) / 0.18f)) : 1f;
            rect.localScale = new Vector3(scale, scale, 1f);
            var alpha = t > 0.6f ? 1f - (t - 0.6f) / 0.4f : 1f;
            text.color = new Color(color.r, color.g, color.b, color.a * alpha);
            return true;
        }

        public void Release()
        {
            OnRelease?.Invoke(this);
        }
    }
}
