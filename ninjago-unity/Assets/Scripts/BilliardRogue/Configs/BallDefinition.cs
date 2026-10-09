#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEngine;

namespace Nex.BilliardRogue
{
    [CreateAssetMenu(fileName = "Ball_Basic", menuName = "Nex/Billiard Rogue/Ball Definition", order = 10)]
    public sealed class BallDefinition : ScriptableObject
    {
        [Header("Rules")]
        [SerializeField] BallRules rules = new();

        [Header("Look")]
        [SerializeField] Sprite icon = null!;
        [SerializeField] Color color = Color.white;
        [Tooltip("Emissive colour used by the ball material and trail; pushed above 1 for bloom.")]
        [SerializeField, ColorUsage(false, true)] Color glowColor = Color.white;
        [SerializeField] Material material = null!;
        [SerializeField] GameObject? trailPrefab;

        [Header("Feedback")]
        [SerializeField] SfxManager.SoundEffect hitSfx = SfxManager.SoundEffect.BallHitSoft;
        [SerializeField] SfxManager.SoundEffect launchSfx = SfxManager.SoundEffect.BallLaunch;
        [SerializeField] VfxManager.VisualEffect hitVfx = VfxManager.VisualEffect.HitSpark;

        [Header("Localization")]
        [Tooltip("br.ball.<type>.name")]
        [SerializeField] string nameKey = "";
        [Tooltip("br.ball.<type>.desc. — the level (1..3) is appended.")]
        [SerializeField] string descKeyPrefix = "";

        public BallRules Rules => rules;
        public Sprite Icon => icon;
        public Color Color => color;
        public Color GlowColor => glowColor;
        public Material Material => material;
        public GameObject? TrailPrefab => trailPrefab;
        public SfxManager.SoundEffect HitSfx => hitSfx;
        public SfxManager.SoundEffect LaunchSfx => launchSfx;
        public VfxManager.VisualEffect HitVfx => hitVfx;
        public string NameKey => nameKey;
        public string DescKeyPrefix => descKeyPrefix;
    }
}
