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
        [Tooltip("Looping atmosphere played through VfxManager at the diorama's ambient anchor (Vfx_Ambient_Act{n}, VfxPrefabsBuilder).")]
        [SerializeField] VfxManager.VisualEffect ambientEffect = VfxManager.VisualEffect.AmbientAct1;

        [Header("Audio")]
        [SerializeField] BgmManager.BgmType battleBgm = BgmManager.BgmType.Act1;

        public ActRules Rules => rules;
        public string NameKey => nameKey;
        public GameObject EnvironmentPrefab => environmentPrefab;
        public ActLightingPreset Lighting => lighting;
        public VolumeProfile VolumeProfile => volumeProfile;
        public VfxManager.VisualEffect AmbientEffect => ambientEffect;
        public BgmManager.BgmType BattleBgm => battleBgm;
    }
}
