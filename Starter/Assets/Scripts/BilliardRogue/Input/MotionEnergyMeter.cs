#nullable enable

using Jazz;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Whole-body motion energy of one player for Hype (GDD v2 §3). Once per camera frame (original.frameTime
    /// changes, ~30 Hz) it reads the raw pose — wrists, elbows, shoulders, hips, knees and nose, straight from the
    /// detection so no engine node accessor is involved (the engine has no wrist nodes, and its auto-hide freezes on
    /// scaled time) — in body-normalized inches (aspect-normalized position / the engine's averaged pixels per inch)
    /// and feeds MotionEnergyFilter; Update eases the energy on unscaled time. Not tracked (no camera frame within
    /// ControlConfig.staleFrameSeconds, or fewer than motionMinNodes nodes) lets the energy fall to 0.
    /// Allocation-free per frame.
    /// </summary>
    public sealed class MotionEnergyMeter : MonoBehaviour, IMotionEnergy
    {
        const int NodeCount = 11;

        readonly Vector2[] positions = new Vector2[NodeCount];
        readonly bool[] detected = new bool[NodeCount];
        OnePlayerDetectionEngine engine = null!;
        ControlConfig config = null!;
        MotionEnergyFilter filter = null!;
        double lastRawFrameTime = double.NegativeInfinity;
        float lastRawFrameArrival = float.NegativeInfinity;
        int detectedNodes;

        public int PlayerIndex { get; private set; }
        /// <summary>0 before Initialize.</summary>
        public float Energy01 => filter != null ? filter.Energy01 : 0f;
        public bool IsTracked { get; private set; }

        #region Readout

        /// <summary>Unsmoothed intensity of the last camera frame pair (0..1).</summary>
        public float Intensity01 => filter != null ? filter.Target01 : 0f;
        /// <summary>Mean node speed above the deadzone (in/s) of the last measurement.</summary>
        public float MeanExcessSpeed => filter != null ? filter.MeanExcessSpeed : 0f;
        /// <summary>Nodes detected in the newest camera frame (of 11).</summary>
        public int DetectedNodes => detectedNodes;

        #endregion

        #region Life Cycle

        public void Initialize(int playerIndex, OnePlayerDetectionEngine aEngine, ControlConfig aConfig)
        {
            PlayerIndex = playerIndex;
            engine = aEngine;
            config = aConfig;
            filter = new MotionEnergyFilter(NodeCount, config.MotionEnergySettings);
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

        // Never touches the engine: CameraSession may destroy it before this component.
        void Update()
        {
            filter.Settings = config.MotionEnergySettings;
            var tracked = Time.realtimeSinceStartup - lastRawFrameArrival <= config.StaleFrameSeconds
                          && detectedNodes >= config.MotionEnergySettings.minNodes;
            if (!tracked && IsTracked)
            {
                filter.MarkNoBody();
            }
            IsTracked = tracked;
            filter.Tick(Time.unscaledDeltaTime);
        }

        #endregion

        #region Detection

        // The smoother re-emits the last detection every Update; only a new camera frame is a new raw sample.
        void HandleDetection(BodyPoseDetectionResult result)
        {
            var frameTime = result.original.frameTime;
            if (frameTime == lastRawFrameTime) return;

            if (frameTime < lastRawFrameTime)
            {
                // Wall-clock frame time stepped back: restart the sample clock.
                filter.MarkNoBody();
            }
            lastRawFrameTime = frameTime;
            lastRawFrameArrival = Time.realtimeSinceStartup;

            var pose = result.original.GetPlayerPose(PlayerIndex)?.bodyPose;
            var ppi = engine.RawPpi;
            if (pose == null || ppi <= 0f)
            {
                detectedNodes = 0;
                filter.MarkNoBody();
                return;
            }

            var perInch = 1f / ppi;
            var count = 0;
            for (var i = 0; i < NodeCount; i++)
            {
                var node = Node(pose, i);
                detected[i] = node.isDetected;
                if (!node.isDetected) continue;
                positions[i] = node.ToVector2() * perInch;
                count++;
            }
            detectedNodes = count;
            filter.AddSample(frameTime, positions, detected);
        }

        static PoseNode Node(BodyPose pose, int index)
        {
            return index switch
            {
                0 => pose.LeftWrist(),
                1 => pose.RightWrist(),
                2 => pose.LeftElbow(),
                3 => pose.RightElbow(),
                4 => pose.LeftShoulder(),
                5 => pose.RightShoulder(),
                6 => pose.LeftHip(),
                7 => pose.RightHip(),
                8 => pose.LeftKnee(),
                9 => pose.RightKnee(),
                _ => pose.Nose(),
            };
        }

        #endregion
    }
}
