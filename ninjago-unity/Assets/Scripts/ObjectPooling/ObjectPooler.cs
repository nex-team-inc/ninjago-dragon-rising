#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Nex
{
    public interface IPoolableObject
    {
        public event Action<Component>? OnRelease;
    }

    public abstract class ObjectPooler<T> : MonoBehaviour where T : MonoBehaviour, IPoolableObject
    {
        protected T objectPrefab = null!;
        protected ObjectPool<T> objectPool = null!;

        public void Initialize(T prefab, int defaultCapacity = 10)
        {
            objectPool = new ObjectPool<T>(CreatePooledItem, OnTakeFromPool, OnReturnToPool, OnDestroyPoolObject, false,
                defaultCapacity);
            objectPrefab = prefab;
        }

        HashSet<T> activeInstances = new();

        public T Get()
        {
            var instance = objectPool.Get();
            activeInstances.Add(instance);
            return instance;
        }

        void Release(Component component)
        {
            var instance = (T)component;
            activeInstances.Remove(instance);
            // Already destroyed by a scene teardown that reached it before its owner: nothing left to pool.
            if (instance == null) return;
            objectPool.Release(instance);
        }

        public IEnumerable<T> ActiveInstances => activeInstances;

        /// <summary>Creates count inactive instances up front so the first spawns of a stage do not instantiate mid-play.</summary>
        public void Prewarm(int count)
        {
            var instances = new T[count];
            for (var i = 0; i < count; i++)
            {
                instances[i] = objectPool.Get();
            }

            for (var i = 0; i < count; i++)
            {
                objectPool.Release(instances[i]);
            }
        }

        protected virtual T CreatePooledItem()
        {
            var ret = Instantiate(objectPrefab, transform);
            ret.OnRelease += Release;
            return ret;
        }

        static void OnReturnToPool(T obj)
        {
            obj.gameObject.SetActive(false);
        }

        static void OnTakeFromPool(T obj)
        {
            obj.gameObject.SetActive(true);
        }

        static void OnDestroyPoolObject(T obj)
        {
            Destroy(obj.gameObject);
        }

        public void OnDestroy()
        {
            objectPool.Dispose();
        }
    }
}
