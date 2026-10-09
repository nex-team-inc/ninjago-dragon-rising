#nullable enable

using Nex.BilliardRogue.Simulation;
using Nex.Util;
using UnityEngine;

namespace Nex.BilliardRogue
{
    [CreateAssetMenu(fileName = "BallCatalog", menuName = "Nex/Billiard Rogue/Ball Catalog", order = 11)]
    public sealed class BallCatalog : ScriptableObject
    {
        [SerializeField] EnumDictionary<BallType, BallDefinition> definitions = new();

        public EnumDictionary<BallType, BallDefinition> Definitions => definitions;

        public BallDefinition Get(BallType type) => definitions[type];
    }
}
