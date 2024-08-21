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
            objectPool.Release(instance);
        }

        public IEnumerable<T> ActiveInstances => activeInstances;

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
