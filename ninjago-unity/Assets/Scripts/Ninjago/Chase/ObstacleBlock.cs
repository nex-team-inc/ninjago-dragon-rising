#nullable enable

using System;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>A pooled unit-cube brick barrier or floating block, scaled to its cell and popped in when spawned.</summary>
    public class ObstacleBlock : MonoBehaviour, IPoolableObject
    {
        [Header("Pop In Seconds")]
        [SerializeField] float popInSeconds = 0.15f;

        Vector3 size;
        float age;

        public event Action<Component>? OnRelease;

        #region Public API

        public void Show(Vector3 localPosition, Vector3 aSize)
        {
            transform.localPosition = localPosition;
            size = aSize;
            age = 0f;
            transform.localScale = Vector3.zero;
        }

        public void Release()
        {
            OnRelease?.Invoke(this);
        }

        #endregion

        #region Life Cycle

        void Update()
        {
            if (age >= popInSeconds) return;
            age += Time.deltaTime;
            transform.localScale = size * Mathf.Clamp01(age / popInSeconds);
        }

        #endregion
    }
}
