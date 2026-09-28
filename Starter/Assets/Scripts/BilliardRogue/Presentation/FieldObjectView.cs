#nullable enable

using System;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Pooled static field object: pillar (glowing runes), crate (HP pips on top, wobble on hits, break), portal
    /// (spinning swirl) and mud (slow pulse). One prefab per FieldObjectType (FieldObjectCatalog).
    /// </summary>
    public sealed class FieldObjectView : MonoBehaviour, IPoolableObject
    {
        static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public event Action<Component>? OnRelease;

        [Header("Wiring (WorldPrefabsBuilder)")]
        [SerializeField] FieldObjectType type;
        [SerializeField] Transform model = null!;
        [Tooltip("Part spun around Y (portal Swirl).")]
        [SerializeField] Transform? spinPart;
        [SerializeField] Renderer[] emissiveRenderers = Array.Empty<Renderer>();
        [Tooltip("Crate HP pips, shown left to right.")]
        [SerializeField] Renderer[] pips = Array.Empty<Renderer>();
        [Tooltip("Height of the HP label anchor above the floor, in cells.")]
        [SerializeField, Range(0.2f, 3f)] float labelHeight = 1.15f;

        JuiceConfig juice = null!;
        MaterialPropertyBlock block = null!;
        float cellSize = 1f;
        float phase;
        float wobbleT = 1f;
        float breakT = 1f;
        bool breaking;

        public int Id { get; private set; }
        public FieldObjectType Type => type;
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public Vector3 Position { get; private set; }
        public Vector3 LabelAnchor => Position + Vector3.up * (labelHeight * cellSize);
        public Vector3 Center => Position + Vector3.up * (0.5f * cellSize);

        #region Life Cycle

        public void Spawn(FieldObjectState state, int maxHp, ArenaLayout layout, JuiceConfig aJuice)
        {
            Id = state.id;
            juice = aJuice;
            cellSize = layout.CellSize;
            Hp = state.hp;
            MaxHp = Mathf.Max(1, maxHp);
            block ??= new MaterialPropertyBlock();
            Position = layout.CellCenterWorld(state.col, state.row);
            transform.SetPositionAndRotation(Position, layout.transform.rotation);
            model.localScale = Vector3.one;
            model.localRotation = Quaternion.identity;
            phase = UnityEngine.Random.value * 10f;
            wobbleT = breakT = 1f;
            breaking = false;
            SetHp(state.hp);
            ApplyEmission(1f);
        }

        void Update()
        {
            var dt = Time.deltaTime;
            phase += dt;
            var p = juice.Props;
            if (spinPart != null) spinPart.Rotate(0f, p.portalSwirlSpeed * dt, 0f, Space.Self);
            if (emissiveRenderers.Length > 0)
            {
                var e = juice.Emissive;
                ApplyEmission(1f + e.pulseAmount * Mathf.Sin(phase / e.pulsePeriod * Mathf.PI * 2f));
            }

            var scale = Vector3.one;
            if (type == FieldObjectType.Mud)
            {
                var pulse = 1f + p.mudPulse * Mathf.Sin(phase * 1.7f);
                scale = new Vector3(pulse, 1f, pulse);
            }

            if (wobbleT < 1f)
            {
                wobbleT = Mathf.Min(1f, wobbleT + dt / p.crateWobbleDuration);
                var wobble = Mathf.Sin(wobbleT * Mathf.PI * 3f) * (1f - wobbleT) * p.crateWobble;
                model.localRotation = Quaternion.Euler(0f, 0f, wobble * 45f);
                scale *= 1f + wobble * 0.5f;
            }

            if (breakT < 1f)
            {
                breakT = Mathf.Min(1f, breakT + dt / 0.15f);
                scale *= 1f - Easing.InQuad(breakT);
            }

            model.localScale = scale;
            if (breaking && breakT >= 1f)
            {
                breaking = false;
                OnRelease?.Invoke(this);
            }
        }

        #endregion

        #region Public Methods

        public void SetHp(int hp)
        {
            Hp = hp;
            for (var i = 0; i < pips.Length; i++)
            {
                pips[i].gameObject.SetActive(i < hp);
            }
        }

        public void PlayHit()
        {
            wobbleT = 0f;
        }

        public void PlayBreak()
        {
            if (breaking) return;
            breaking = true;
            breakT = 0f;
        }

        public void Release()
        {
            breaking = false;
            OnRelease?.Invoke(this);
        }

        #endregion

        #region Helpers

        void ApplyEmission(float pulse)
        {
            if (emissiveRenderers.Length == 0) return;
            block.Clear();
            block.SetFloat(EmissionStrengthId, juice.Emissive.strength * pulse);
            block.SetColor(BaseColorId, Color.white);
            for (var i = 0; i < emissiveRenderers.Length; i++)
            {
                emissiveRenderers[i].SetPropertyBlock(block);
            }
        }

        #endregion
    }
}
