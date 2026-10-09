#nullable enable

using Jazz;
using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Feeds each player's HandStormMeter while that player's counter window is open. The signal is the
    /// team.nex.mdk.hand detector (playerHands[playerIndex] only, so one player's hands never fill another's swirl);
    /// a hand it has not reported recently falls back to that player's body hand node.
    /// </summary>
    public class HandStormSampler : MonoBehaviour
    {
        [Header("Hand Detector")]
        [Tooltip("Inactive HandPoseDetector instance; activated once its detection managers are assigned.")]
        [SerializeField] HandDetectionManager handDetector = null!;
        [Header("Simulated Storm Radius")]
        [SerializeField] float simulatedRadiusInches = 10f;
        [Header("Simulated Storm Frequency")]
        [SerializeField] float simulatedHz = 1.6f;

        readonly HandStormMeter[] meters = { new(), new() };
        readonly PlayerBody?[] listening = new PlayerBody?[SimulatedBody.MaxPlayers];
        readonly float[] lastDetectorSampleTime = new float[SimulatedBody.MaxPlayers * 2];
        readonly double[] lastBodyFrameTime = new double[SimulatedBody.MaxPlayers];
        FightConfig config = null!;

        #region Initialization

        public void Initialize(DetectionManager detection, FightConfig aConfig)
        {
            config = aConfig;
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

        /// <summary>Hand detection runs only while the fight is on screen; the runner never reads hands.</summary>
        public void SetDetecting(bool detecting)
        {
            handDetector.shouldDetect = detecting;
        }

        public HandStormMeter Begin(PlayerBody body)
        {
            var playerIndex = body.PlayerIndex;
            meters[playerIndex].Begin(config.HandTravelInches, config.HandSpeedPeakInchesPerSecond, config.HandNoiseFloorInches,
                config.HandSampleGapSeconds);
            listening[playerIndex] = body;
            lastDetectorSampleTime[playerIndex * 2] = float.NegativeInfinity;
            lastDetectorSampleTime[playerIndex * 2 + 1] = float.NegativeInfinity;
            return meters[playerIndex];
        }

        public void End(int playerIndex)
        {
            listening[playerIndex] = null;
        }

        #endregion

        #region Sampling

        void HandleHandPoseDetection(HandPoseDetection detection)
        {
            if (detection.playerHands == null) return;
            for (var playerIndex = 0; playerIndex < listening.Length; playerIndex++)
            {
                var body = listening[playerIndex];
                if (body == null || playerIndex >= detection.playerHands.Count) continue;
                // Hand landmarks are raw frame pixels; the body ppi is frame heights per inch.
                var pixelsPerInch = body.RawPpi * detection.frameSize.y;
                if (pixelsPerInch <= 0f) continue;
                var hands = detection.playerHands[playerIndex];
                AddDetectorSample(playerIndex, 0, hands.left, pixelsPerInch, detection.frameTime);
                AddDetectorSample(playerIndex, 1, hands.right, pixelsPerInch, detection.frameTime);
            }
        }

        void AddDetectorSample(int playerIndex, int hand, HandPose? pose, float pixelsPerInch, double frameTime)
        {
            if (pose == null) return;
            meters[playerIndex].AddSample(hand, pose.Center2D() / pixelsPerInch, frameTime, HandStormMeter.Source.HandDetector);
            lastDetectorSampleTime[playerIndex * 2 + hand] = Time.unscaledTime;
        }

        void Update()
        {
            for (var playerIndex = 0; playerIndex < listening.Length; playerIndex++)
            {
                var body = listening[playerIndex];
                if (body == null) continue;
                if (SimulatedBody.IsEnabled)
                {
                    AddSimulatedSamples(playerIndex);
                    continue;
                }

                if (!config.UseBodyHandFallback) continue;
                var frameTime = body.BodyFrameTime;
                if (frameTime <= lastBodyFrameTime[playerIndex]) continue;
                lastBodyFrameTime[playerIndex] = frameTime;
                for (var hand = 0; hand < 2; hand++)
                {
                    if (Time.unscaledTime - lastDetectorSampleTime[playerIndex * 2 + hand] < config.BodyFallbackAfterSeconds) continue;
                    if (body.TryGetHandInches(hand == 0, out var inches))
                    {
                        meters[playerIndex].AddSample(hand, inches, frameTime, HandStormMeter.Source.BodyNode);
                    }
                }
            }
        }

        void AddSimulatedSamples(int playerIndex)
        {
            if (!SimulatedBody.IsStorming(playerIndex)) return;
            var time = Time.unscaledTimeAsDouble;
            var angle = (float)(time * simulatedHz * Mathf.PI * 2d);
            for (var hand = 0; hand < 2; hand++)
            {
                var direction = new Vector2(Mathf.Cos(angle + hand * Mathf.PI), Mathf.Sin(angle + hand * Mathf.PI));
                meters[playerIndex].AddSample(hand, direction * simulatedRadiusInches, time, HandStormMeter.Source.Simulated);
            }
        }

        #endregion
    }
}
