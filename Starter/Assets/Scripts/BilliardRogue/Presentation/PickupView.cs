#nullable enable

using System;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Pooled pickup: bobbing, spinning model that pops in with its batch, hops down with the enemies and pops when collected.</summary>
    public sealed class PickupView : MonoBehaviour, IPoolableObject
    {
        static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");

        public event Action<Component>? OnRelease;

        [Header("Wiring (WorldPrefabsBuilder)")]
        [SerializeField] PickupType type;
        [SerializeField] Transform model = null!;
        [SerializeField] Renderer[] emissiveRenderers = Array.Empty<Renderer>();

        JuiceConfig juice = null!;
        MaterialPropertyBlock block = null!;
        float phase;
        Vector3 basePosition;
        Vector3 moveFrom;
        float moveT = 1f;
        float moveDuration = 0.4f;
        float collectT = 1f;
        float popT = 1f;
        float popDuration = 0.28f;
        bool collecting;

        public int Id { get; private set; }
        public PickupType Type => type;
        public Vector3 Position => basePosition;
        public Vector3 Center => basePosition + Vector3.up * 0.45f;

        #region Life Cycle

        /// <summary>popSeconds &gt; 0 scales the model in from 0 with an overshoot over that time.</summary>
        public void Spawn(PickupState state, ArenaLayout layout, JuiceConfig aJuice, float popSeconds = 0f)
        {
            Id = state.id;
            juice = aJuice;
            block ??= new MaterialPropertyBlock();
            basePosition = layout.CellCenterWorld(state.col, state.row);
            transform.SetPositionAndRotation(basePosition, layout.transform.rotation);
            popT = popSeconds > 0f ? 0f : 1f;
            popDuration = Mathf.Max(0.05f, popSeconds);
            model.localScale = popT < 1f ? Vector3.zero : Vector3.one;
            phase = UnityEngine.Random.value * 10f;
            moveT = collectT = 1f;
            collecting = false;
            if (emissiveRenderers.Length == 0) return;
            block.Clear();
            block.SetFloat(EmissionStrengthId, juice.Emissive.strength);
            for (var i = 0; i < emissiveRenderers.Length; i++)
            {
                emissiveRenderers[i].SetPropertyBlock(block);
            }
        }

        void Update()
        {
            var dt = Time.deltaTime;
            var p = juice.Props;
            phase += dt;
            var position = basePosition;
            if (moveT < 1f)
            {
                moveT = Mathf.Min(1f, moveT + dt / moveDuration);
                position = Vector3.Lerp(moveFrom, basePosition, Easing.OutQuad(moveT)) + Vector3.up * (Easing.Arc(moveT) * 0.3f);
            }

            transform.position = position;
            var bob = (Mathf.Sin(phase / p.pickupBobPeriod * Mathf.PI * 2f) + 1f) * 0.5f * p.pickupBobAmplitude;
            model.localPosition = new Vector3(0f, bob, 0f);
            model.localRotation = Quaternion.Euler(0f, phase * p.pickupSpinSpeed, 0f);
            if (popT < 1f && !collecting)
            {
                popT = Mathf.Min(1f, popT + dt / popDuration);
                var pop = Easing.OutBack(popT);
                model.localScale = new Vector3(pop, pop, pop);
            }

            if (collectT < 1f)
            {
                collectT = Mathf.Min(1f, collectT + dt / 0.18f);
                var s = 1f + 0.4f * Easing.Arc(collectT) - collectT;
                model.localScale = new Vector3(s, s, s);
            }

            if (collecting && collectT >= 1f)
            {
                collecting = false;
                OnRelease?.Invoke(this);
            }
        }

        #endregion

        #region Public Methods

        public void MoveTo(Vector3 world, float duration)
        {
            moveFrom = basePosition;
            basePosition = world;
            moveDuration = Mathf.Max(0.05f, duration);
            moveT = 0f;
        }

        public void SnapTo(Vector3 world)
        {
            basePosition = world;
            moveT = 1f;
        }

        public void PlayCollect()
        {
            if (collecting) return;
            collecting = true;
            collectT = 0f;
        }

        public void Release()
        {
            collecting = false;
            OnRelease?.Invoke(this);
        }

        #endregion
    }
}
