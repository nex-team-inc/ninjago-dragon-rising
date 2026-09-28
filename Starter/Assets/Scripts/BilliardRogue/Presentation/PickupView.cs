#nullable enable

using System;
using UnityEngine;

// Implemented by Presentation module (bobbing pickup with collect burst).
namespace Nex.BilliardRogue
{
    public sealed class PickupView : MonoBehaviour, IPoolableObject
    {
#pragma warning disable CS0067
        public event Action<Component>? OnRelease;
#pragma warning restore CS0067
    }
}
