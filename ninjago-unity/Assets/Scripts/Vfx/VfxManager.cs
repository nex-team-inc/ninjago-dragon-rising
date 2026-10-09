#nullable enable

using System;
using System.Collections.Generic;
using Nex.Util;
using UnityEngine;
using UnityEngine.Pool;

namespace Nex
{
    public class VfxManager : Singleton<VfxManager>
    {
        protected override VfxManager GetThis() => this;

        // Values are persisted in the prefab EnumDictionary: explicit ints, append only, keep ascending order.
        public enum VisualEffect
        {
            HitSpark = 0,
            CritSpark = 1,
            WallSpark = 2,
            EnemyPoof = 10,
            BossPoof = 11,
            Explosion = 20,
            FreezeBurst = 21,
            BurnBurst = 22,
            PoisonBurst = 23,
            LightningHit = 24,
            HealSparkle = 25,
            SplitPop = 26,
            PortalFlash = 27,
            PickupSparkle = 30,
            DustPuff = 31,
            PlayerHurtFlash = 32,
            CratePieces = 33,
            LevelUpBurst = 34,
            // Looping act atmospheres (Vfx_Ambient_Act{n}); ActEnvironment plays and stops them.
            AmbientAct1 = 40,
            AmbientAct2 = 41,
            AmbientAct3 = 42,
        }

        #region Pool

        class VisualEffectPool : MonoBehaviour
        {
            ParticleSystem prefab = null!;

            ObjectPool<ParticleSystem> pool = null!;

            class Returner : MonoBehaviour
            {
                public ObjectPool<ParticleSystem> pool = null!;
                ParticleSystem self = null!;

                void Awake()
                {
                    self = GetComponent<ParticleSystem>();
                }

                void OnParticleSystemStopped()
                {
                    pool.Release(self);
                }
            }

            public VisualEffectPool Initialize(ParticleSystem inputPrefab, int defaultPoolSize, int maxPoolSize)
            {
                prefab = inputPrefab;

                // ObjectPool throws on maxSize <= 0; a mis-authored spec must not break every later spec's Awake.
                var safeMax = Mathf.Max(1, maxPoolSize);
                var safeDefault = Mathf.Clamp(defaultPoolSize, 0, safeMax);
                pool = new ObjectPool<ParticleSystem>(HandleCreate, HandleGet, HandleRelease, HandleDestroy, false, safeDefault, safeMax);

                return this;
            }

            void OnDestroy()
            {
                pool.Dispose();
            }

            ParticleSystem HandleCreate()
            {
                var system = Instantiate(prefab, transform);
                system.gameObject.AddComponent<Returner>().pool = pool;
                var mainModule = system.main;
                mainModule.stopAction = ParticleSystemStopAction.Callback;
                return system;
            }

            static void HandleGet(ParticleSystem system)
            {
                system.gameObject.SetActive(true);
                system.Play();
            }

            static void HandleRelease(ParticleSystem system)
            {
                system.gameObject.SetActive(false);
            }

            static void HandleDestroy(ParticleSystem system)
            {
                Destroy(system.gameObject);
            }

            public ParticleSystem Get() => pool.Get();

            // Instantiates up to count instances now so the first hit of a stage does not hitch.
            public void Prewarm(int count)
            {
                var instances = new ParticleSystem[count];
                for (var i = 0; i < count; i++)
                {
                    instances[i] = pool.Get();
                }

                for (var i = 0; i < count; i++)
                {
                    pool.Release(instances[i]);
                }
            }
        }

        [Serializable]
        class VisualEffectSpec
        {
            [Tooltip("Optional until the registry is filled: an entry without a prefab never plays.")]
            [SerializeField] ParticleSystem? prefab;
            [SerializeField] int defaultPoolSize;
            [SerializeField] int maxPoolSize;

            VisualEffectPool? pool;

            public bool IsReady => pool != null;

            public void Initialize(GameObject host)
            {
                if (prefab == null) return;
                pool = host.AddComponent<VisualEffectPool>().Initialize(prefab, defaultPoolSize, maxPoolSize);
            }

            public ParticleSystem Get() => pool!.Get();

            public void Prewarm(int count) => pool!.Prewarm(count);
        }

        #endregion

        [SerializeField] EnumDictionary<VisualEffect, VisualEffectSpec> effectSpecs = null!;

        readonly HashSet<int> warnedMissingEffects = new();

        #region Life Cycle

        protected override void Awake()
        {
            base.Awake();
            var host = gameObject;
            foreach (var pair in effectSpecs)
            {
                // The prefab dictionary can lag behind the enum; missing specs are tolerated.
                pair.Value?.Initialize(host);
            }
        }

        #endregion

        #region Public Methods

        public void PlayVisualEffect(VisualEffect effect, Vector3 position)
        {
            PlayVisualEffect(effect, position, Quaternion.identity);
        }

        /// <summary>Plays a pooled effect; returns the instance (callers may Stop looping ones) or null when the effect is not registered.</summary>
        public ParticleSystem? PlayVisualEffect(VisualEffect effect, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            if (!TryGetSpec(effect, out var spec)) return null;
            var system = spec.Get();
            var systemTransform = system.transform;
            systemTransform.SetPositionAndRotation(position, rotation);
            systemTransform.localScale = Vector3.one * scale;
            system.Play();
            return system;
        }

        public void Prewarm(VisualEffect effect, int count)
        {
            if (!TryGetSpec(effect, out var spec)) return;
            spec.Prewarm(count);
        }

        #endregion

        #region Helpers

        bool TryGetSpec(VisualEffect effect, out VisualEffectSpec spec)
        {
            if (effectSpecs.TryGetValue(effect, out spec) && spec != null && spec.IsReady) return true;
            if (warnedMissingEffects.Add((int)effect))
            {
                Debug.LogWarning($"[VfxManager] No prefab registered for {effect}; the effect is skipped.");
            }

            spec = null!;
            return false;
        }

        #endregion
    }
}
