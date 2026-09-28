#nullable enable

using Nex.BilliardRogue.Simulation;
using Nex.Util;
using UnityEngine;

namespace Nex.BilliardRogue
{
    // Crate HP is a balance rule (BalanceConfig → BalanceRules.crateHp), not a catalog value.
    [CreateAssetMenu(fileName = "FieldObjectCatalog", menuName = "Nex/Billiard Rogue/Field Object Catalog", order = 30)]
    public sealed class FieldObjectCatalog : ScriptableObject
    {
        [Header("Prefabs")]
        [SerializeField] EnumDictionary<FieldObjectType, FieldObjectView> objectPrefabs = new();
        [SerializeField] EnumDictionary<PickupType, PickupView> pickupPrefabs = new();

        public EnumDictionary<FieldObjectType, FieldObjectView> ObjectPrefabs => objectPrefabs;
        public EnumDictionary<PickupType, PickupView> PickupPrefabs => pickupPrefabs;
    }
}
