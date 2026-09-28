#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Root config: the only object the coordinator hands to the game. Wired by ConfigAssetsBuilder.</summary>
    [CreateAssetMenu(fileName = "BilliardRogueConfig", menuName = "Nex/Billiard Rogue/Root Config", order = 0)]
    public sealed class BilliardRogueConfig : ScriptableObject
    {
        [Header("Catalogs")]
        [SerializeField] BallCatalog balls = null!;
        [SerializeField] EnemyCatalog enemies = null!;
        [SerializeField] FieldObjectCatalog fieldObjects = null!;
        [Tooltip("Act 1..3 in order.")]
        [SerializeField] ActDefinition[] acts = null!;

        [Header("Rules")]
        [SerializeField] ArenaConfig arena = null!;
        [SerializeField] BalanceConfig balance = null!;
        [SerializeField] ControlConfig control = null!;
        [SerializeField] PacingConfig pacing = null!;

        [Header("Presentation")]
        [SerializeField] JuiceConfig juice = null!;
        [SerializeField] HD2DVisualConfig visual = null!;

        public BallCatalog Balls => balls;
        public EnemyCatalog Enemies => enemies;
        public FieldObjectCatalog FieldObjects => fieldObjects;
        public ActDefinition[] Acts => acts;
        public ArenaConfig Arena => arena;
        public BalanceConfig Balance => balance;
        public ControlConfig Control => control;
        public PacingConfig Pacing => pacing;
        public JuiceConfig Juice => juice;
        public HD2DVisualConfig Visual => visual;
    }
}
