#nullable enable

using Nex.BilliardRogue.Simulation;
using Nex.Util;
using UnityEngine;

namespace Nex.BilliardRogue
{
    [CreateAssetMenu(fileName = "FieldObjectCatalog", menuName = "Nex/Billiard Rogue/Field Object Catalog", order = 30)]
    public sealed class FieldObjectCatalog : ScriptableObject
    {
        [Header("Prefabs")]
        [SerializeField] EnumDictionary<FieldObjectType, FieldObjectView> objectPrefabs = new();
        [SerializeField] EnumDictionary<PickupType, PickupView> pickupPrefabs = new();

        [Header("Rules")]
        [Tooltip("Crate HP at stage 1; scaled like enemy HP.")]
        [SerializeField, Range(1, 20)] int crateBaseHp = 3;

        public EnumDictionary<FieldObjectType, FieldObjectView> ObjectPrefabs => objectPrefabs;
        public EnumDictionary<PickupType, PickupView> PickupPrefabs => pickupPrefabs;
        public int CrateBaseHp => crateBaseHp;
    }
}
