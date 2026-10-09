#nullable enable

using System;
using Nex.BilliardRogue.Simulation;
using Nex.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Crisp native-resolution HP label that follows an enemy or crate: HP number, status icons (burn / poison /
    /// freeze) and the telegraph icon of the next ability. Positioned every LateUpdate by WorldLabelLayer.
    /// </summary>
    public sealed class WorldLabel : MonoBehaviour, IPoolableObject
    {
        public event Action<Component>? OnRelease;

        [Header("Wiring (BoardPrefabBuilder)")]
        [SerializeField] RectTransform rect = null!;
        [SerializeField] TextMeshProUGUI hpText = null!;
        [Tooltip("Dark rounded pill behind the HP number (sized to the text by SetHp).")]
        [SerializeField] Image? hpPill;
        [SerializeField] EnumDictionary<StatusType, Image> statusIcons = new();
        [SerializeField] Image telegraphIcon = null!;

        EnemyView? enemy;
        FieldObjectView? crate;
        JuiceConfig.LabelSettings? labels;
        bool hpVisible = true;
        float appliedScale = 1f;

        public int Id { get; private set; }
        public RectTransform Rect => rect;
        public Vector3 WorldPosition => enemy != null ? enemy.LabelAnchor : crate != null ? crate.LabelAnchor : Vector3.zero;
        public bool Attached => enemy != null || crate != null;
        /// <summary>The enemy's spawn pop scale (1 for crates and once popped); WorldLabelLayer applies it.</summary>
        public float PopScale => enemy != null ? enemy.PopScale : 1f;

        #region Public Methods

        /// <summary>Bosses keep their status / telegraph icons but no HP number: the HUD boss bar carries it.</summary>
        public void AttachEnemy(EnemyView view, JuiceConfig.LabelSettings settings)
        {
            enemy = view;
            crate = null;
            Id = view.Id;
            Begin(settings, !view.IsBoss);
            SetHp(view.Hp, view.MaxHp);
        }

        public void AttachCrate(FieldObjectView view, JuiceConfig.LabelSettings settings)
        {
            crate = view;
            enemy = null;
            Id = view.Id;
            Begin(settings, true);
            SetHp(view.Hp, view.MaxHp);
        }

        public void SetHp(int hp, int maxHp)
        {
            if (!hpVisible) return;
            var text = NumberStrings.Get(Mathf.Max(0, hp));
            hpText.SetText(text);
            var fraction = maxHp > 0 ? Mathf.Clamp01(hp / (float)maxHp) : 1f;
            var settings = labels;
            if (settings != null)
            {
                // Full → mid over the upper half, mid → low over the lower half.
                hpText.color = fraction >= 0.5f
                    ? Color.Lerp(settings.hpMidColor, settings.hpFullColor, (fraction - 0.5f) * 2f)
                    : Color.Lerp(settings.hpLowColor, settings.hpMidColor, fraction * 2f);
                if (hpPill != null)
                {
                    var size = hpText.GetPreferredValues(text, 400f, 100f);
                    hpPill.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(size.x) + settings.hpPillPadding.x * 2f, Mathf.Ceil(hpText.fontSize) + settings.hpPillPadding.y * 2f);
                }
            }
            else
            {
                hpText.color = fraction <= 0.34f ? new Color(1f, 0.45f, 0.4f) : Color.white;
            }
        }

        public void SetStatus(int burn, int poison, bool frozen)
        {
            statusIcons[StatusType.Burn].enabled = burn > 0;
            statusIcons[StatusType.Poison].enabled = poison > 0;
            statusIcons[StatusType.Freeze].enabled = frozen;
        }

        public void SetTelegraph(Sprite? icon)
        {
            telegraphIcon.sprite = icon;
            telegraphIcon.enabled = icon != null;
        }

        /// <summary>Scales the label (only when the value changed, so settled labels never touch their transform).</summary>
        public void ApplyScale(float scale)
        {
            if (Mathf.Approximately(scale, appliedScale)) return;
            appliedScale = scale;
            rect.localScale = new Vector3(scale, scale, 1f);
        }

        public void Release()
        {
            enemy = null;
            crate = null;
            OnRelease?.Invoke(this);
        }

        #endregion

        #region Helpers

        void Begin(JuiceConfig.LabelSettings settings, bool showHp)
        {
            labels = settings;
            hpVisible = showHp;
            hpText.fontSize = settings.hpLabelSize;
            hpText.enabled = showHp;
            if (hpPill != null)
            {
                hpPill.enabled = showHp;
                hpPill.color = settings.hpPillColor;
            }

            var statuses = EnumDictionary<StatusType, Image>.allKeys;
            for (var i = 0; i < statuses.Length; i++)
            {
                statusIcons[statuses[i]].enabled = false;
            }

            telegraphIcon.enabled = false;
            rect.anchoredPosition = new Vector2(-9999f, -9999f);
            appliedScale = 1f;
            rect.localScale = Vector3.one;
        }

        #endregion
    }
}
