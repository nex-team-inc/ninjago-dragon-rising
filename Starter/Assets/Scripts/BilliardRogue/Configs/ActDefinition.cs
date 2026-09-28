#nullable enable

using Nex.BilliardRogue.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nex.BilliardRogue
{
    [CreateAssetMenu(fileName = "Act_1", menuName = "Nex/Billiard Rogue/Act Definition", order = 40)]
    public sealed class ActDefinition : ScriptableObject
    {
        [Header("Rules")]
        [SerializeField] ActRules rules = new();

        [Header("Localization")]
        [Tooltip("br.act.<n>.name")]
        [SerializeField] string nameKey = "";

        [Header("Environment")]
        [SerializeField] GameObject environmentPrefab = null!;
        [SerializeField] ActLightingPreset lighting = new();
        [SerializeField] VolumeProfile volumeProfile = null!;
        [SerializeField] GameObject? ambientParticlesPrefab;

        [Header("Audio")]
        [SerializeField] BgmManager.BgmType battleBgm = BgmManager.BgmType.Act1;

        public ActRules Rules => rules;
        public string NameKey => nameKey;
        public GameObject EnvironmentPrefab => environmentPrefab;
        public ActLightingPreset Lighting => lighting;
        public VolumeProfile VolumeProfile => volumeProfile;
        public GameObject? AmbientParticlesPrefab => ambientParticlesPrefab;
        public BgmManager.BgmType BattleBgm => battleBgm;
    }
}
