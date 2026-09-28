#nullable enable

using System;
using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Pooled enemy visual: model root placement (faces the camera by a 180° yaw, TDD §14.1), hop with squash and
    /// stretch, hit punch, knockback, spawn pop, death shrink, attack lunge and cast raise as small manual tweens
    /// evaluated in Update (no allocations). Status tints, flash and shields live in EnemyStatusVisuals, idle life
    /// in EnemyIdleMotion. Hierarchy: view root (position) → Model (FBX root: offsets and scale) → parts.
    /// </summary>
    public sealed class EnemyView : MonoBehaviour, IPoolableObject
    {
        public event Action<Component>? OnRelease;

        [Header("Wiring (WorldPrefabsBuilder)")]
        [SerializeField] Transform model = null!;
        [SerializeField] EnemyIdleMotion idle = null!;
        [SerializeField] EnemyStatusVisuals statusVisuals = null!;
        [Tooltip("Height of the HP label anchor above the feet, in cells.")]
        [SerializeField, Range(0.2f, 4f)] float labelHeight = 1.1f;
        [Tooltip("Visual centre height in cells (hit sparks, chain lightning).")]
        [SerializeField, Range(0.1f, 3f)] float centerHeight = 0.45f;

        JuiceConfig.EnemyMotionSettings motion = null!;
        float cellSize = 1f;
        int width = 1;
        int height = 1;
        Vector3 basePosition;
        Vector3 hopFrom;
        Vector3 hopTo;
        float hopDuration;
        float hopHeight;
        float hopT = 1f;
        float punchT = 1f;
        float knockT = 1f;
        Vector3 knockDirection;
        float popT = 1f;
        float deathT = 1f;
        float lungeT = 1f;
        float castT = 1f;
        float roarT = 1f;
        float lungeDuration = 0.35f;
        float castDuration = 0.45f;
        bool dying;

        public int Id { get; private set; }
        public EnemyType Type { get; private set; }
        public int MaxHp { get; private set; }
        public int Hp { get; private set; }
        public bool IsBoss { get; private set; }
        public EnemyStatusVisuals Status => statusVisuals;
        /// <summary>World position of the feet (footprint centre on the floor).</summary>
        public Vector3 Position => basePosition;
        public Vector3 LabelAnchor => basePosition + Vector3.up * (labelHeight * cellSize);
        public Vector3 Center => basePosition + Vector3.up * (centerHeight * cellSize);
        public bool IsAnimating => hopT < 1f || lungeT < 1f || castT < 1f || deathT < 1f || popT < 1f;

        #region Life Cycle

        /// <summary>Called right after the pool hands the instance out; resets every tween and places the view.</summary>
        public void Spawn(EnemyState state, bool isBoss, ArenaLayout layout, JuiceConfig juice, bool pop)
        {
            Id = state.id;
            Type = state.type;
            width = state.width;
            height = state.height;
            MaxHp = state.maxHp;
            Hp = state.hp;
            IsBoss = isBoss;
            motion = juice.EnemyMotion;
            cellSize = layout.CellSize;
            basePosition = layout.FootprintCenterWorld(state.col, state.row, state.width, state.height);
            transform.SetPositionAndRotation(basePosition, layout.transform.rotation * Quaternion.Euler(0f, 180f, 0f));
            model.localPosition = Vector3.zero;
            model.localScale = Vector3.one;
            hopT = punchT = knockT = deathT = lungeT = castT = roarT = 1f;
            popT = pop ? 0f : 1f;
            dying = false;
            idle.Initialize(motion);
            idle.Paused = state.status.frozenTurns > 0;
            statusVisuals.Initialize(juice, width, height, cellSize);
            statusVisuals.SetStatus(state.status.burn, state.status.poison, state.status.frozenTurns > 0);
            statusVisuals.SetShield(state.shieldFace, false);
            if (pop) model.localScale = Vector3.zero;
        }

        void Update()
        {
            var dt = Time.deltaTime;
            var position = basePosition;
            var squash = 0f;
            if (hopT < 1f)
            {
                hopT = Mathf.Min(1f, hopT + dt / hopDuration);
                var t = hopT;
                position = Vector3.Lerp(hopFrom, hopTo, Easing.OutQuad(t)) + Vector3.up * (Easing.Arc(t) * hopHeight);
                // Stretch mid-air, squash on take-off and landing.
                squash = t < 0.15f ? -motion.hopSquash * (1f - t / 0.15f) : t > 0.85f ? -motion.hopSquash * ((t - 0.85f) / 0.15f) : motion.hopSquash * 0.6f * Easing.Arc(t);
                if (hopT >= 1f) position = basePosition;
            }

            transform.position = position;

            var offset = Vector3.zero;
            if (knockT < 1f)
            {
                knockT = Mathf.Min(1f, knockT + dt / motion.knockbackDuration);
                offset += knockDirection * (motion.knockbackDistance * cellSize * Easing.Punch(knockT));
            }

            if (lungeT < 1f)
            {
                lungeT = Mathf.Min(1f, lungeT + dt / lungeDuration);
                offset += new Vector3(0f, 0f, motion.attackLunge * cellSize * Easing.Punch(lungeT));
            }

            if (castT < 1f)
            {
                castT = Mathf.Min(1f, castT + dt / castDuration);
                offset += new Vector3(0f, motion.castRaise * cellSize * Easing.Arc(castT), 0f);
            }

            model.localPosition = offset;

            var scale = 1f;
            if (popT < 1f)
            {
                popT = Mathf.Min(1f, popT + dt / motion.spawnPopDuration);
                scale *= Easing.OutBack(popT);
            }

            if (punchT < 1f)
            {
                punchT = Mathf.Min(1f, punchT + dt / (motion.knockbackDuration * 1.2f));
                squash += motion.hitPunch * Easing.Punch(punchT);
            }

            if (roarT < 1f)
            {
                roarT = Mathf.Min(1f, roarT + dt / 0.5f);
                scale *= 1f + motion.bossPhaseRoarScale * Easing.Arc(roarT);
            }

            if (deathT < 1f)
            {
                deathT = Mathf.Min(1f, deathT + dt / motion.deathDuration);
                scale *= 1f - Easing.InQuad(deathT);
                squash -= 0.3f * deathT;
            }

            model.localScale = new Vector3(scale * (1f + squash * 0.6f), scale * (1f - squash), scale * (1f + squash * 0.6f));
            if (dying && deathT >= 1f)
            {
                dying = false;
                OnRelease?.Invoke(this);
            }
        }

        #endregion

        #region Public Methods

        public void SetHp(int hp)
        {
            Hp = hp;
        }

        public void SnapTo(Vector3 world)
        {
            basePosition = world;
            hopT = 1f;
            transform.position = world;
        }

        public void HopTo(Vector3 world, float duration, float heightCells)
        {
            hopFrom = basePosition;
            hopTo = world;
            basePosition = world;
            hopDuration = Mathf.Max(0.05f, duration);
            hopHeight = heightCells * cellSize;
            hopT = 0f;
        }

        /// <summary>Flash, scale punch and a knockback along the ball direction (world space).</summary>
        public void PlayHit(Vector3 worldDirection, bool crit, Color flashColor)
        {
            statusVisuals.Flash(crit ? 1f : 0.8f, flashColor);
            punchT = 0f;
            var local = transform.InverseTransformDirection(worldDirection);
            local.y = 0f;
            knockDirection = local.sqrMagnitude > 0.001f ? local.normalized : Vector3.forward;
            knockT = 0f;
        }

        public void PlayDeath()
        {
            if (dying) return;
            dying = true;
            deathT = 0f;
            idle.Paused = true;
        }

        public void PlayAttackLunge(float duration)
        {
            lungeDuration = Mathf.Max(0.05f, duration);
            lungeT = 0f;
        }

        public void PlayCast(float duration)
        {
            castDuration = Mathf.Max(0.05f, duration);
            castT = 0f;
        }

        public void PlayRoar()
        {
            roarT = 0f;
        }

        public void SetFrozen(bool frozen, int burn, int poison)
        {
            idle.Paused = frozen || dying;
            statusVisuals.SetStatus(burn, poison, frozen);
        }

        #endregion
    }
}
