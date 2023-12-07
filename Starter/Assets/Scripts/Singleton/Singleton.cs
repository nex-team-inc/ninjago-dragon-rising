using UnityEngine;

#nullable enable

namespace Nex
{
    public abstract class Singleton<T> : MonoBehaviour
    {
        protected virtual void Awake()
        {
            Instance = GetThis();
        }

        protected virtual void OnDestroy()
        {
            Instance = default!;
        }

        protected abstract T GetThis();

        public static T Instance { get; private set; } = default!;
    }
}
