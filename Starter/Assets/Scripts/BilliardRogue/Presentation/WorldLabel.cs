#nullable enable

using System;
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
        [Tooltip("Indexed by StatusType: Burn, Poison, Freeze.")]
        [SerializeField] Image[] statusIcons = Array.Empty<Image>();
        [SerializeField] Image telegraphIcon = null!;

        EnemyView? enemy;
        FieldObjectView? crate;
        Color fullColor = Color.white;
        Color lowColor = new(1f, 0.45f, 0.4f);

        public int Id { get; private set; }
        public RectTransform Rect => rect;
        public Vector3 WorldPosition => enemy != null ? enemy.LabelAnchor : crate != null ? crate.LabelAnchor : Vector3.zero;
        public bool Attached => enemy != null || crate != null;

        #region Public Methods

        public void AttachEnemy(EnemyView view, float fontSize)
        {
            enemy = view;
            crate = null;
            Id = view.Id;
            Begin(fontSize);
            SetHp(view.Hp, view.MaxHp);
        }

        public void AttachCrate(FieldObjectView view, float fontSize)
        {
            crate = view;
            enemy = null;
            Id = view.Id;
            Begin(fontSize);
            SetHp(view.Hp, view.MaxHp);
        }

        public void SetHp(int hp, int maxHp)
        {
            hpText.SetText(NumberStrings.Get(Mathf.Max(0, hp)));
            var fraction = maxHp > 0 ? hp / (float)maxHp : 1f;
            hpText.color = fraction <= 0.34f ? lowColor : fullColor;
        }

        public void SetStatus(int burn, int poison, bool frozen)
        {
            SetIcon(0, burn > 0);
            SetIcon(1, poison > 0);
            SetIcon(2, frozen);
        }

        public void SetTelegraph(Sprite? icon)
        {
            telegraphIcon.sprite = icon;
            telegraphIcon.enabled = icon != null;
        }

        public void Release()
        {
            enemy = null;
            crate = null;
            OnRelease?.Invoke(this);
        }

        #endregion

        #region Helpers

        void Begin(float fontSize)
        {
            hpText.fontSize = fontSize;
            for (var i = 0; i < statusIcons.Length; i++)
            {
                statusIcons[i].enabled = false;
            }

            telegraphIcon.enabled = false;
            rect.anchoredPosition = new Vector2(-9999f, -9999f);
        }

        void SetIcon(int index, bool on)
        {
            if (index >= statusIcons.Length) return;
            statusIcons[index].enabled = on;
        }

        #endregion
    }
}
