#nullable enable

using Nex.BilliardRogue.Simulation;
using Nex.Util;
using UnityEngine;

namespace Nex.BilliardRogue
{
    [CreateAssetMenu(fileName = "EnemyCatalog", menuName = "Nex/Billiard Rogue/Enemy Catalog", order = 21)]
    public sealed class EnemyCatalog : ScriptableObject
    {
        [SerializeField] EnumDictionary<EnemyType, EnemyDefinition> definitions = new();

        public EnumDictionary<EnemyType, EnemyDefinition> Definitions => definitions;

        public EnemyDefinition Get(EnemyType type) => definitions[type];
    }
}
