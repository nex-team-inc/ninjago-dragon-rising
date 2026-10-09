#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// The cursor rule shared by Stone Kick and Earth Seal: each hand is a cursor mapped from that player's reach box
    /// (body inches around the chest) onto that player's screen region. Setters exist for the Debug Settings rows.
    /// </summary>
    [CreateAssetMenu(fileName = "HandCursorConfig", menuName = "Nex/Ninjago/Hand Cursor Config")]
    public class HandCursorConfig : ScriptableObject
    {
        [Header("Reach")]
        [Tooltip("Vertical span of a player's reach box in body inches; it covers the full height of that player's screen region. The width follows the region's aspect, so a circle drawn by the hand stays a circle on screen.")]
        [SerializeField, Range(10f, 60f)] float reachHeightInches = 30f;
        [Tooltip("Height of the reach box center above the chest, in body inches.")]
        [SerializeField, Range(-10f, 20f)] float reachCenterAboveChestInches = 5f;

        [Header("Smoothing")]
        [Tooltip("One Euro minimum cutoff (Hz). Lower = steadier cursor while held still, with more lag.")]
        [SerializeField, Range(0.1f, 10f)] float smoothingMinCutoffHz = 1.5f;
        [Tooltip("One Euro speed response (per inch/s). Higher = less lag during fast strokes.")]
        [SerializeField, Range(0f, 1f)] float smoothingSpeedResponse = 0.06f;

        [Header("Signal")]
        [Tooltip("Drive a cursor from the body hand node while the hand detector has not reported that hand recently.")]
        [SerializeField] bool useBodyHandFallback = true;
        [Tooltip("Seconds without a hand-detector sample before the body hand node takes over that cursor.")]
        [SerializeField, Range(0.05f, 1f)] float bodyFallbackAfterSeconds = 0.2f;
        [Tooltip("Seconds without any sample before a cursor hides and stops hitting anything.")]
        [SerializeField, Range(0.1f, 2f)] float lostAfterSeconds = 0.5f;

        #region Public API

        public float ReachHeightInches { get => reachHeightInches; set => reachHeightInches = Mathf.Max(5f, value); }
        public float ReachCenterAboveChestInches { get => reachCenterAboveChestInches; set => reachCenterAboveChestInches = value; }
        public float SmoothingMinCutoffHz { get => smoothingMinCutoffHz; set => smoothingMinCutoffHz = Mathf.Max(0.05f, value); }
        public float SmoothingSpeedResponse { get => smoothingSpeedResponse; set => smoothingSpeedResponse = Mathf.Max(0f, value); }
        public bool UseBodyHandFallback { get => useBodyHandFallback; set => useBodyHandFallback = value; }
        public float BodyFallbackAfterSeconds => bodyFallbackAfterSeconds;
        public float LostAfterSeconds => lostAfterSeconds;

        #endregion
    }
}
