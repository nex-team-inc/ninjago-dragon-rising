#nullable enable

using System;
using UnityEngine;

// Implemented by Presentation module (pillar, crate HP, portal swirl, mud).
namespace Nex.BilliardRogue
{
    public sealed class FieldObjectView : MonoBehaviour, IPoolableObject
    {
#pragma warning disable CS0067
        public event Action<Component>? OnRelease;
#pragma warning restore CS0067
    }
}
