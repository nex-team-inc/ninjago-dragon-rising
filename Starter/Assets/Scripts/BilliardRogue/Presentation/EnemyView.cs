#nullable enable

using System;
using UnityEngine;

// Implemented by Presentation module (model root, squash/hop/flash/death, status overlays, telegraph, shield).
namespace Nex.BilliardRogue
{
    public sealed class EnemyView : MonoBehaviour, IPoolableObject
    {
#pragma warning disable CS0067
        public event Action<Component>? OnRelease;
#pragma warning restore CS0067
    }
}
