#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    [CreateAssetMenu(fileName = "Enemy_Slime", menuName = "Nex/Billiard Rogue/Enemy Definition", order = 20)]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Rules")]
        [SerializeField] EnemyRules rules = new();

        [Header("Look")]
        [SerializeField] EnemyView prefab = null!;
        [SerializeField] Sprite icon = null!;
        [Tooltip("Hop height in cells for the advance animation.")]
        [SerializeField, Range(0f, 2f)] float hopHeight = 0.4f;

        [Header("Feedback")]
        [SerializeField] SfxManager.SoundEffect hitSfx = SfxManager.SoundEffect.BallHitSoft;
        [SerializeField] SfxManager.SoundEffect deathSfx = SfxManager.SoundEffect.EnemyDeath;
        [SerializeField] SfxManager.SoundEffect attackSfx = SfxManager.SoundEffect.EnemyAttack;
        [SerializeField] VfxManager.VisualEffect deathVfx = VfxManager.VisualEffect.EnemyPoof;

        [Header("Localization")]
        [Tooltip("br.enemy.<type>.name")]
        [SerializeField] string nameKey = "";

        public EnemyRules Rules => rules;
        public EnemyView Prefab => prefab;
        public Sprite Icon => icon;
        public float HopHeight => hopHeight;
        public SfxManager.SoundEffect HitSfx => hitSfx;
        public SfxManager.SoundEffect DeathSfx => deathSfx;
        public SfxManager.SoundEffect AttackSfx => attackSfx;
        public VfxManager.VisualEffect DeathVfx => deathVfx;
        public string NameKey => nameKey;
    }
}
