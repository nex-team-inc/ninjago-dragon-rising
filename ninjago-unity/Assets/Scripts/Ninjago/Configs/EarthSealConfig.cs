#nullable enable

using System;
using Nex.Util;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Earth Seal tuning. Waves get harder only through more cracks, less warning, less seal time and a bigger grid.
    /// Setters exist for the Debug Settings rows, which edit this asset live during a playtest.
    /// </summary>
    [CreateAssetMenu(fileName = "EarthSealConfig", menuName = "Nex/Ninjago/Earth Seal Config")]
    public class EarthSealConfig : ScriptableObject
    {
        [Serializable]
        public class WaveSettings
        {
            [Tooltip("Tiles per side of the wall grid.")]
            [Range(2, 6)] public int gridSize = 3;
            [Tooltip("Seconds new cracks may start; cracks still live at the end resolve before the next wave.")]
            public float waveSeconds = 20f;
            [Tooltip("Seconds between two rolls; on each roll every intact tile rolls the crack chance.")]
            public float beatSeconds = 1f;
            [Tooltip("Chance (0..1) that an intact tile starts cracking on a roll.")]
            [Range(0f, 1f)] public float crackChance = 0.07f;
            [Tooltip("Most cracks live at the same time.")]
            [Range(1, 6)] public int maxCracks = 1;
            [Tooltip("Tiles (in any direction) a new crack keeps from every live crack; 1 lets cracks touch.")]
            [Range(1, 4)] public int minCrackSpacing = 1;
            [Tooltip("Seconds a cracking tile shakes and glows before its monster starts pushing.")]
            public float warningSeconds = 2.2f;
            [Tooltip("Seconds of cursor hold that fill a seal. More cursors on one tile do not fill it faster.")]
            public float sealSeconds = 2f;
            [Tooltip("Seconds the monster pushes before it breaks through, once the warning is over.")]
            public float breakthroughSeconds = 4f;
        }

        [Header("Waves")]
        [SerializeField] EnumDictionary<EarthSealWave, WaveSettings> waves = new();

        [Header("Wall")]
        [Tooltip("Hearts the shared wall starts with; each breakthrough costs one and the run ends at 0.")]
        [SerializeField, Range(1, 12)] int hearts = 5;

        [Header("Seal")]
        [Tooltip("Seconds a full seal takes to drain to nothing once every cursor has left; a short wobble keeps most of it.")]
        [SerializeField, Range(0.5f, 20f)] float sealDrainSeconds = 4f;

        [Header("Pacing")]
        [Tooltip("Seconds of 'Hold a hand on the cracks' before the first wave.")]
        [SerializeField, Range(0f, 6f)] float introSeconds = 3f;
        [Tooltip("Seconds of the wave banner between two waves.")]
        [SerializeField, Range(0f, 6f)] float waveBreakSeconds = 2.5f;

        #region Public API

        public WaveSettings GetWave(EarthSealWave wave) => waves[wave];
        public int WaveCount => Enum.GetValues(typeof(EarthSealWave)).Length;
        public int Hearts { get => hearts; set => hearts = Mathf.Max(1, value); }
        public float SealDrainSeconds { get => sealDrainSeconds; set => sealDrainSeconds = Mathf.Max(0.1f, value); }
        public float IntroSeconds => introSeconds;
        public float WaveBreakSeconds => waveBreakSeconds;

        #endregion
    }
}
