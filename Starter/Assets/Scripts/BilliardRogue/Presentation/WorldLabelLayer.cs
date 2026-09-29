#nullable enable

using System.Collections.Generic;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    public enum NumberKind
    {
        Normal,
        Crit,
        Block,
        Heal,
        Burn,
        Poison,
        PlayerHurt,
    }

    /// <summary>
    /// Full-stretch UI layer over the world display: pooled HP labels attached to enemies and crates, pooled
    /// damage numbers and floating texts, all re-projected every LateUpdate through
    /// PixelWorldDisplay.TryWorldToCanvas so they stay crisp at native resolution and follow camera shake.
    /// </summary>
    public sealed class WorldLabelLayer : MonoBehaviour
    {
        [Header("Wiring (BoardPrefabBuilder)")]
        [SerializeField] RectTransform rect = null!;
        [SerializeField] WorldLabelPool labelPool = null!;
        [SerializeField] DamageNumberPool numberPool = null!;
        [Tooltip("Indexed by EnemyPhaseResolver telegraph code: Spawn, Cast, Heal, Quake.")]
        [SerializeField] Sprite[] telegraphIcons = System.Array.Empty<Sprite>();

        PixelWorldDisplay display = null!;
        JuiceConfig juice = null!;
        FloatTextCache texts = null!;
        readonly Dictionary<int, WorldLabel> labelsById = new();
        readonly List<WorldLabel> labels = new();
        readonly List<DamageNumber> numbers = new();

        public FloatTextCache Texts => texts;

        #region Life Cycle

        public void Initialize(PixelWorldDisplay aDisplay, JuiceConfig aJuice, WorldLabel labelPrefab, DamageNumber numberPrefab)
        {
            display = aDisplay;
            juice = aJuice;
            labelPool.Initialize(labelPrefab, 24);
            labelPool.Prewarm(16);
            numberPool.Initialize(numberPrefab, 24);
            numberPool.Prewarm(16);
            texts = new FloatTextCache(destroyCancellationToken);
        }

        void OnDestroy()
        {
            texts?.Dispose();
        }

        void LateUpdate()
        {
            var offset = juice.Labels.hpLabelOffset;
            for (var i = 0; i < labels.Count; i++)
            {
                var label = labels[i];
                Project(label.Rect, label.WorldPosition, offset);
            }

            var dt = Time.deltaTime;
            for (var i = numbers.Count - 1; i >= 0; i--)
            {
                var number = numbers[i];
                if (!number.Tick(dt))
                {
                    numbers.RemoveAt(i);
                    number.Release();
                    continue;
                }

                var rise = number.Rise * Easing.OutQuad(number.Age / number.Lifetime);
                Project(number.Rect, number.WorldPosition, number.Jitter + new Vector2(0f, rise));
            }
        }

        #endregion

        #region Labels

        public WorldLabel AttachEnemy(EnemyView view)
        {
            var label = Acquire(view.Id);
            label.AttachEnemy(view, juice.Labels);
            return label;
        }

        public WorldLabel AttachCrate(FieldObjectView view)
        {
            var label = Acquire(view.Id);
            label.AttachCrate(view, juice.Labels);
            return label;
        }

        public bool TryGetLabel(int id, out WorldLabel label) => labelsById.TryGetValue(id, out label);

        public void Detach(int id)
        {
            if (!labelsById.TryGetValue(id, out var label)) return;
            labelsById.Remove(id);
            labels.Remove(label);
            label.Release();
        }

        public void SetTelegraph(int id, int telegraphCode)
        {
            if (!labelsById.TryGetValue(id, out var label)) return;
            var icon = telegraphCode >= 0 && telegraphCode < telegraphIcons.Length ? telegraphIcons[telegraphCode] : null;
            label.SetTelegraph(icon);
        }

        public void ClearAll()
        {
            for (var i = 0; i < labels.Count; i++)
            {
                labels[i].Release();
            }

            labels.Clear();
            labelsById.Clear();
            for (var i = 0; i < numbers.Count; i++)
            {
                numbers[i].Release();
            }

            numbers.Clear();
        }

        #endregion

        #region Numbers & floats

        public void ShowNumber(Vector3 world, int value, NumberKind kind)
        {
            var size = kind == NumberKind.Crit ? juice.CritNumberSize : juice.DamageNumberSize;
            var text = kind == NumberKind.Heal ? texts.Heal(value) : NumberStrings.Get(value);
            Show(text, ColorFor(kind), size, world, juice.DamageNumberLifetime, juice.DamageNumberRise, true);
        }

        public void ShowText(Vector3 world, string text, Color color)
        {
            Show(text, color, juice.Labels.floatTextSize, world, juice.Labels.floatLifetime, juice.Labels.floatRise, false);
        }

        public void ShowCombo(Vector3 world, int combo, Color color)
        {
            Show(texts.Combo(combo), color, juice.Labels.comboTextSize, world, juice.Labels.floatLifetime, juice.Labels.floatRise, false);
        }

        public Color ColorFor(NumberKind kind)
        {
            return kind switch
            {
                NumberKind.Crit => juice.CritDamageColor,
                NumberKind.Block => juice.BlockColor,
                NumberKind.Heal => juice.HealColor,
                NumberKind.Burn => juice.BurnColor,
                NumberKind.Poison => juice.PoisonColor,
                NumberKind.PlayerHurt => juice.PlayerHurtFlash,
                _ => juice.NormalDamageColor,
            };
        }

        public Color ColorFor(StatusType status)
        {
            return status switch
            {
                StatusType.Burn => juice.BurnColor,
                StatusType.Poison => juice.PoisonColor,
                _ => juice.EnemyFreezeTint,
            };
        }

        #endregion

        #region Helpers

        WorldLabel Acquire(int id)
        {
            if (labelsById.TryGetValue(id, out var existing)) return existing;
            var label = labelPool.Get();
            labelsById[id] = label;
            labels.Add(label);
            return label;
        }

        void Show(string text, Color color, float size, Vector3 world, float lifetime, float rise, bool jitter)
        {
            var number = numberPool.Get();
            var j = jitter ? new Vector2(Random.Range(-juice.Labels.numberJitter, juice.Labels.numberJitter), 0f) : Vector2.zero;
            number.Show(text, color, size, world, lifetime, rise, j);
            numbers.Add(number);
        }

        void Project(RectTransform target, Vector3 world, Vector2 offset)
        {
            if (display.TryWorldToCanvas(world, rect, out var anchored))
            {
                target.anchoredPosition = anchored + offset;
                return;
            }

            target.anchoredPosition = new Vector2(-9999f, -9999f);
        }

        #endregion
    }
}
