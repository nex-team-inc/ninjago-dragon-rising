#nullable enable

using System;
using Nex.Util;
using UnityEngine;
using UnityEngine.Pool;

namespace Nex
{
    public class VfxManager : Singleton<VfxManager>
    {
        protected override VfxManager GetThis() => this;

        public enum VisualEffect
        {
        }

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

                pool = new ObjectPool<ParticleSystem>(HandleCreate, HandleGet, HandleRelease, HandleDestroy, false, defaultPoolSize, maxPoolSize);

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
        }

        [Serializable]
        class VisualEffectSpec
        {
            [SerializeField] ParticleSystem prefab = null!;
            [SerializeField] int defaultPoolSize;
            [SerializeField] int maxPoolSize;

            VisualEffectPool pool = null!;

            public void Initialize(GameObject host)
            {
                pool = host.AddComponent<VisualEffectPool>().Initialize(prefab, defaultPoolSize, maxPoolSize);
            }

            public ParticleSystem Get() => pool.Get();
        }

        [SerializeField] EnumDictionary<VisualEffect, VisualEffectSpec> effectSpecs = null!;

        protected override void Awake()
        {
            base.Awake();
            var host = gameObject;
            foreach (var pair in effectSpecs)
            {
                pair.Value.Initialize(host);
            }
        }

        public void PlayVisualEffect(VisualEffect effect, Vector3 position)
        {
            var system = effectSpecs[effect].Get();
            system.transform.position = position;
            system.Play();
        }
    }
}
