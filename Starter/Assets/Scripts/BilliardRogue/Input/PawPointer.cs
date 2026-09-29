#nullable enable

using Jazz;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Both hands of one player as screen pointers for motion UI such as the reward pick (GDD v2 §4). The smoothed
    /// left / right paws relative to the chest (body-normalized inches) are filtered with OneEuro and mapped with
    /// ControlConfig's paw pointer centre and half range to 0..1 screen space (PawPointerMath), the same mapping for
    /// both hands so hands brought together meet on screen. Tracked like PawShotInput: the raw camera pose showed the
    /// chest, elbows and wrists within trackingDropSeconds, on realtime (the engine's node auto-hide freezes while
    /// gameplay is paused). Allocation-free per frame.
    /// </summary>
    public sealed class PawPointer : MonoBehaviour, IPawPointer
    {
        OnePlayerDetectionEngine engine = null!;
        ControlConfig config = null!;
        OneEuroFilter leftX = null!;
        OneEuroFilter leftY = null!;
        OneEuroFilter rightX = null!;
        OneEuroFilter rightY = null!;
        double lastRawFrameTime = double.NegativeInfinity;
        float lastRawFrameArrival = float.NegativeInfinity;
        float lastPoseTime = float.NegativeInfinity;
        bool rawPoseDetected;
        Vector2 left01 = new(0.25f, 0.2f);
        Vector2 right01 = new(0.75f, 0.2f);

        public int PlayerIndex { get; private set; }

        public void Initialize(int playerIndex, OnePlayerDetectionEngine aEngine, ControlConfig aConfig)
        {
            PlayerIndex = playerIndex;
            engine = aEngine;
            config = aConfig;
            ResetFilters();
            engine.NewDetectionCapturedAndProcessed += HandleDetection;
            enabled = true;
        }

        void OnDestroy()
        {
            if (engine != null)
            {
                engine.NewDetectionCapturedAndProcessed -= HandleDetection;
            }
        }

        public bool TryGetPaws(out Vector2 aLeft01, out Vector2 aRight01)
        {
            aLeft01 = left01;
            aRight01 = right01;
            if (config == null) return false;
            var now = Time.realtimeSinceStartup;
            return now - lastPoseTime <= config.TrackingDropSeconds && now - lastRawFrameArrival <= config.StaleFrameSeconds;
        }

        void HandleDetection(BodyPoseDetectionResult result)
        {
            var now = Time.realtimeSinceStartup;
            var frameTime = result.original.frameTime;
            if (frameTime != lastRawFrameTime)
            {
                lastRawFrameTime = frameTime;
                lastRawFrameArrival = now;
                rawPoseDetected = PawSampling.ArePawsDetected(result.original, PlayerIndex);
            }
            if (!rawPoseDetected || !PawSampling.TrySampleHands(engine, true, out var left, out var right)) return;

            // A new tracking segment starts from scratch instead of easing in from the pose before the gap.
            if (now - lastPoseTime > config.TrackingDropSeconds)
            {
                ResetFilters();
            }
            lastPoseTime = now;
            var center = config.PawPointerCenterInches;
            var halfRange = config.PawPointerHalfRangeInches;
            left01 = PawPointerMath.ToScreen01(new Vector2(leftX.Filter(left.x, now), leftY.Filter(left.y, now)), center, halfRange);
            right01 = PawPointerMath.ToScreen01(new Vector2(rightX.Filter(right.x, now), rightY.Filter(right.y, now)), center, halfRange);
        }

        void ResetFilters()
        {
            leftX = new OneEuroFilter(config.PawPointerMinCutoff, config.PawPointerBeta);
            leftY = new OneEuroFilter(config.PawPointerMinCutoff, config.PawPointerBeta);
            rightX = new OneEuroFilter(config.PawPointerMinCutoff, config.PawPointerBeta);
            rightY = new OneEuroFilter(config.PawPointerMinCutoff, config.PawPointerBeta);
        }
    }
}
