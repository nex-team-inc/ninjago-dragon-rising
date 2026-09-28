#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    [CreateAssetMenu(fileName = "BalanceConfig", menuName = "Nex/Billiard Rogue/Balance Config", order = 51)]
    public sealed class BalanceConfig : ScriptableObject
    {
        [SerializeField] BalanceRules rules = new();

        public BalanceRules Rules => rules;
    }
}
