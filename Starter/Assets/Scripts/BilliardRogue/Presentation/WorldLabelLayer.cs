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
        int hypeTier;
        Color hypeColor = Color.white;
        float hypeSizePerTier;
        float hypePopPerTier;
        float hypeTint;

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
                label.ApplyScale(label.PopScale);
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

                Project(number.Rect, number.WorldPosition, number.Offset);
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

        /// <summary>
        /// A damage or heal number (JuiceConfig.numbers). Ball hits (Normal, Crit) wear the ball's colour, grow and pop
        /// harder with damage and with the ball's combo, a killing blow flashes a pale shine and crits cycle the rainbow;
        /// Hype tiers add size and pop and tint the shine. Numbers arc away from the hit; heals rise straight up.
        /// </summary>
        public void ShowNumber(Vector3 world, int value, NumberKind kind, BallType ballType = BallType.Basic, bool killingBlow = false, int combo = 1)
        {
            var n = juice.Numbers;
            var hits = juice.Sequences;
            var ballHit = kind == NumberKind.Normal || kind == NumberKind.Crit;
            var color = kind == NumberKind.Normal ? n.ballColors[ballType] : Opaque(ColorFor(kind));
            var heavy = value >= hits.midDamage;
            var size = juice.DamageNumberSize + (value >= hits.hardDamage ? 2f : heavy ? 1f : 0f) * n.sizeStep;
            var pop = ballHit ? Mathf.Lerp(n.popSmall, n.popBig, Mathf.InverseLerp(1f, hits.hardDamage, value)) : n.popSmall;
            var highlight = Color.Lerp(color, Color.white, n.highlight);
            if (ballHit)
            {
                pop += Mathf.Min(n.popComboMax, (combo - 1) * n.popPerCombo);
                // Hype (GDD v2 §3): bigger numbers, harder pops, the tier colour in their shine.
                if (hypeTier > 0)
                {
                    size += hypeTier * hypeSizePerTier;
                    pop += hypeTier * hypePopPerTier;
                    highlight = Color.Lerp(highlight, hypeColor, hypeTint);
                }
            }

            if (kind == NumberKind.Crit) size = Mathf.Max(size, juice.CritNumberSize) + n.critSizeBonus;
            if (killingBlow)
            {
                size += n.killSizeBonus;
                pop += n.killPopBonus;
                highlight = n.killHighlight;
            }

            var text = kind == NumberKind.Heal ? texts.Heal(value) : NumberStrings.Get(value);
            if (kind == NumberKind.Heal)
            {
                var heal = FloatStyle(color, size);
                heal.highlight = highlight;
                Show(text, heal, world);
                return;
            }

            var lingers = heavy || kind == NumberKind.Crit || killingBlow;
            var side = Random.Range(n.launchSide.x, n.launchSide.y) * (Random.value < 0.5f ? -1f : 1f);
            Show(text, new NumberStyle
            {
                color = color,
                highlight = highlight,
                outline = Color.Lerp(n.outlineColor, color, n.outlineTint),
                size = Mathf.Min(size, n.maxSize),
                pop = pop,
                lifetime = juice.DamageNumberLifetime * (lingers ? n.bigLifetimeScale : 1f),
                start = new Vector2(Random.Range(-juice.Labels.numberJitter, juice.Labels.numberJitter), 0f),
                velocity = new Vector2(side, Random.Range(n.launchUp.x, n.launchUp.y)),
                gravity = n.gravity,
                tilt = -Mathf.Sign(side) * n.tilt * (lingers ? 1.5f : 1f),
                shake = heavy || kind == NumberKind.PlayerHurt ? n.shake : 0f,
                flashSeconds = n.flashSeconds,
                rainbowSpeed = kind == NumberKind.Crit ? n.rainbowSpeed : 0f,
                rainbowPhase = Random.value,
            }, world);
        }

        /// <summary>Hype tier (0 = none) and its colour for the next damage numbers (HypeJuice).</summary>
        public void SetHype(int tier, Color tierColor, JuiceConfig.HypeSettings settings)
        {
            hypeTier = tier;
            hypeColor = tierColor;
            hypeSizePerTier = settings.numberSizePerTier;
            hypePopPerTier = settings.numberPopPerTier;
            hypeTint = settings.numberTint;
        }

        public void ShowText(Vector3 world, string text, Color color)
        {
            Show(text, FloatStyle(color, juice.Labels.floatTextSize), world);
        }

        /// <summary>COMBO text: warm at the show threshold, hotter as the combo grows, the rainbow from comboRainbowAt; rimmed in the shooter's colour.</summary>
        public void ShowCombo(Vector3 world, int combo, Color playerColor)
        {
            var n = juice.Numbers;
            var style = FloatStyle(Color.Lerp(n.comboLow, n.comboHigh, Mathf.InverseLerp(juice.ComboShowThreshold, n.comboRainbowAt, combo)),
                juice.Labels.comboTextSize);
            style.outline = Color.Lerp(n.outlineColor, Opaque(playerColor), 0.35f);
            style.pop = n.popSmall + Mathf.Min(n.popComboMax, combo * n.popPerCombo);
            if (combo >= n.comboRainbowAt)
            {
                style.rainbowSpeed = n.rainbowSpeed;
                style.rainbowPhase = Random.value;
            }

            Show(texts.Combo(combo), style, world);
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

        void Show(string text, in NumberStyle style, Vector3 world)
        {
            var number = numberPool.Get();
            number.Show(text, style, world);
            numbers.Add(number);
        }

        /// <summary>A floating word or heal: rises straight up, slowing to a stop at floatRise when it expires.</summary>
        NumberStyle FloatStyle(Color color, float size)
        {
            var n = juice.Numbers;
            var lifetime = juice.Labels.floatLifetime;
            var rise = juice.Labels.floatRise;
            color = Opaque(color);
            return new NumberStyle
            {
                color = color,
                highlight = Color.Lerp(color, Color.white, n.highlight),
                outline = Color.Lerp(n.outlineColor, color, n.outlineTint),
                size = size,
                pop = n.popSmall,
                lifetime = lifetime,
                velocity = new Vector2(0f, 2f * rise / lifetime),
                gravity = 2f * rise / (lifetime * lifetime),
                flashSeconds = n.flashSeconds,
            };
        }

        static Color Opaque(Color color) => new(color.r, color.g, color.b, 1f);

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
