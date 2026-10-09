#nullable enable

using System.Collections.Generic;
using Jazz;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Two cursors per player. The signal is the team.nex.mdk.hand detector (playerHands[playerIndex] only, so one
    /// player's hands never move another's cursors); a hand it has not reported recently falls back to that player's
    /// body hand node. Each cursor maps into that player's screen region, set by the view on screen.
    /// </summary>
    // Before gameplay Updates, so hit tests in the same frame read this frame's cursors.
    [DefaultExecutionOrder(-100)]
    public class HandCursorTracker : MonoBehaviour
    {
        [Header("Hand Detector")]
        [Tooltip("Inactive HandPoseDetector instance; activated once its detection managers and tracked hands are assigned.")]
        [SerializeField] HandDetectionManager handDetector = null!;

        struct DetectorSample
        {
            public bool valid;
            public Vector2 aspectNormalized;
            public float time;
        }

        readonly List<HandCursor> cursors = new();
        IReadOnlyList<PlayerBody> bodies = null!;
        HandCursorConfig config = null!;
        Rect[] regions = null!;
        DetectorSample[] samples = null!;

        #region Initialization

        public void Initialize(DetectionManager detection, HandCursorConfig aConfig, IReadOnlyList<PlayerBody> aBodies)
        {
            config = aConfig;
            bodies = aBodies;
            regions = new Rect[bodies.Count];
            samples = new DetectorSample[bodies.Count * 2];
            var trackHands = new List<ShouldTrackHands>(bodies.Count);
            for (var playerIndex = 0; playerIndex < bodies.Count; playerIndex++)
            {
                regions[playerIndex] = new Rect(0f, 0f, 1f, 1f);
                cursors.Add(new HandCursor(playerIndex, 0));
                cursors.Add(new HandCursor(playerIndex, 1));
                trackHands.Add(new ShouldTrackHands(true, true));
            }

            // The prefab tracks no hand for player 1; the engine reads this list when it starts.
            handDetector.handDetectionConfig.trackHandsList = trackHands;
            handDetector.coreDetectionManager = detection.CvDetectionManager;
            handDetector.optionalBodyPoseDetector = detection.BodyPoseDetectionManager;
            handDetector.shouldDetect = false;
            handDetector.gameObject.SetActive(true);
            handDetector.captureHandPoseDetection += HandleHandPoseDetection;
        }

        void OnDestroy()
        {
            handDetector.captureHandPoseDetection -= HandleHandPoseDetection;
        }

        #endregion

        #region Public API

        public void SetDetecting(bool detecting)
        {
            handDetector.shouldDetect = detecting;
        }

        /// <summary>Part of the screen (normalized, y up) this player's cursors cover.</summary>
        public void SetRegion(int playerIndex, Rect screenRegion)
        {
            regions[playerIndex] = screenRegion;
        }

        public HandCursor GetCursor(int playerIndex, int hand) => cursors[playerIndex * 2 + hand];

        #endregion

        #region Sampling

        void HandleHandPoseDetection(HandPoseDetection detection)
        {
            if (detection.playerHands == null) return;
            // Hand landmarks are raw frame pixels (y down); aspect-normalized space is frame heights with y up.
            var frameHeight = detection.frameSize.y;
            if (frameHeight <= 0f) return;
            for (var playerIndex = 0; playerIndex < bodies.Count && playerIndex < detection.playerHands.Count; playerIndex++)
            {
                var hands = detection.playerHands[playerIndex];
                if (hands == null) continue;
                Store(playerIndex, 0, hands.left, frameHeight);
                Store(playerIndex, 1, hands.right, frameHeight);
            }
        }

        void Store(int playerIndex, int hand, HandPose? pose, float frameHeight)
        {
            if (pose == null) return;
            var pixels = pose.Center2D();
            samples[playerIndex * 2 + hand] = new DetectorSample
            {
                valid = true,
                aspectNormalized = new Vector2(pixels.x / frameHeight, 1f - pixels.y / frameHeight),
                time = Time.unscaledTime,
            };
        }

        void Update()
        {
            var now = Time.unscaledTime;
            var screenPixels = new Vector2(Screen.width, Screen.height);
            for (var playerIndex = 0; playerIndex < bodies.Count; playerIndex++)
            {
                var box = ReachBox.For(regions[playerIndex], screenPixels, config.ReachHeightInches, config.ReachCenterAboveChestInches);
                for (var hand = 0; hand < 2; hand++)
                {
                    var cursor = cursors[playerIndex * 2 + hand];
                    if (TryGetRawInches(bodies[playerIndex], hand, now, box, out var inches, out var source))
                    {
                        cursor.Track(inches, source, now, box, config.SmoothingMinCutoffHz, config.SmoothingSpeedResponse);
                    }
                    else if (cursor.IsVisible && now - cursor.LastSampleTime > config.LostAfterSeconds)
                    {
                        cursor.Hide();
                    }
                }
            }
        }

        bool TryGetRawInches(PlayerBody body, int hand, float now, in ReachBox box, out Vector2 inches, out HandCursor.Source source)
        {
            inches = default;
            if (SimulatedBody.IsEnabled)
            {
                source = HandCursor.Source.Simulated;
                if (!SimulatedBody.TryGetHand(body.PlayerIndex, hand, out var screenPosition)) return false;
                inches = box.ToInches(screenPosition);
                return true;
            }

            source = HandCursor.Source.HandDetector;
            if (!body.TryGetChest(out var chest)) return false;
            var sample = samples[body.PlayerIndex * 2 + hand];
            if (sample.valid && now - sample.time <= config.BodyFallbackAfterSeconds)
            {
                inches = (sample.aspectNormalized - chest) / body.RawPpi;
                return true;
            }

            source = HandCursor.Source.BodyNode;
            if (!config.UseBodyHandFallback || !body.TryGetHand(hand == 0, out var bodyHand)) return false;
            inches = (bodyHand - chest) / body.RawPpi;
            return true;
        }

        #endregion
    }
}
