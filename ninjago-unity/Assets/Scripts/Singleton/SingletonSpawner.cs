using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

#nullable enable

namespace Nex
{
    // The singleton spawner will spawn singletons at top level at Awake. The spawned object will be placed under
    // DontDestroyOnLoad as well, so it will survive across scenes.
    // However, if there is already an existing singleton, it would not be spawned twice.
    // Additionally, there is static method to kill all spawned objects.
    public class SingletonSpawner : MonoBehaviour
    {
        public enum SingletonType
        {
            StartUp,
            Common
        }

        static readonly Dictionary<SingletonType, AsyncOperationHandle<GameObject>> singletonDict = new();

        [Serializable]
        public struct SingletonConfig
        {
            public SingletonType type;
            public AssetReferenceGameObject prefab;
        }

        [SerializeField] SingletonConfig[] configs = null!;
        [SerializeField] GameObject[] activatePostSpawn = null!;
        [SerializeField] MonoBehaviour[] enablePostSpawn = null!;

        void Awake()
        {
            // Check if there is any singleton that requires spawning.
            var needsSpawning = configs.Where(config => !singletonDict.ContainsKey(config.type)).ToArray();
            if (needsSpawning.Length == 0)
            {
                ActivatePostSpawn();
                return;
            }
            InstantiateSingletonAsync(needsSpawning).Forget();
        }

        async UniTaskVoid InstantiateSingletonAsync(IEnumerable<SingletonConfig> needsSpawning)
        {
            foreach (var config in needsSpawning)
            {
                // Check once more if a singleton is required.
                if (singletonDict.ContainsKey(config.type)) continue;
                var handle = config.prefab.InstantiateAsync(null, true);
                var spawned = await handle;
                DontDestroyOnLoad(spawned);
                singletonDict.Add(config.type, handle);
            }
            ActivatePostSpawn();
        }

        void ActivatePostSpawn()
        {
            foreach (var obj in activatePostSpawn)
            {
                obj.SetActive(true);
            }

            foreach (var behaviour in enablePostSpawn)
            {
                behaviour.enabled = true;
            }
            // Destroy self, now that everything is done.
            Destroy(gameObject);
        }

        public static void KillAllSingletons()
        {
            foreach (var pair in singletonDict)
            {
                Destroy(pair.Value.Result);
                Addressables.Release(pair.Value);
            }

            singletonDict.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Cleanup()
        {
            singletonDict.Clear();
        }
    }
}
