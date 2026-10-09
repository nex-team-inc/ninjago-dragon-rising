#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>Player identity and setup (chest calibration) tuning shared by both mini-games.</summary>
    [CreateAssetMenu(fileName = "NinjagoPlayersConfig", menuName = "Nex/Ninjago/Players Config")]
    public class NinjagoPlayersConfig : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Color per player index; matches the camera preview indicator sprites (P1 red, P2 blue).")]
        [SerializeField] List<Color> playerColors = new() { new Color(0.95f, 0.3f, 0.25f), new Color(0.2f, 0.55f, 1f) };

        [Header("Calibration")]
        [Tooltip("Seconds a player must stand still in a good position before the chest origin is stored.")]
        [SerializeField, Range(0.3f, 4f)] float calibrationSeconds = 1f;
        [Tooltip("Chest movement (body inches) that restarts the hold-still timer.")]
        [SerializeField, Range(0.5f, 10f)] float calibrationStillnessInches = 3f;
        [Tooltip("Two-player mode: seconds after the first player is calibrated before the game starts without the missing player.")]
        [SerializeField, Range(1f, 30f)] float soloFallbackSeconds = 8f;
        [Tooltip("Seconds the 'Ready' (or 'Starting solo') message stays up before gameplay.")]
        [SerializeField, Range(0f, 3f)] float readyHoldSeconds = 0.8f;

        #region Public API

        public Color GetPlayerColor(int playerIndex) => playerColors[playerIndex];
        public float CalibrationSeconds => calibrationSeconds;
        public float CalibrationStillnessInches => calibrationStillnessInches;
        public float SoloFallbackSeconds { get => soloFallbackSeconds; set => soloFallbackSeconds = value; }
        public float ReadyHoldSeconds => readyHoldSeconds;

        #endregion
    }
}
