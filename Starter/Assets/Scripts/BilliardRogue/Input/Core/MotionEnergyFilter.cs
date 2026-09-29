#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Motion energy thresholds in body-normalized inches and seconds (built from ControlConfig).</summary>
    public struct MotionEnergySettings
    {
        /// <summary>Per-node speed (in/s) treated as jitter and subtracted before averaging.</summary>
        public float deadzone;
        /// <summary>Mean per-node speed above the deadzone (in/s) mapped to 1.</summary>
        public float fullSpeed;
        /// <summary>A node's speed is clamped to this (in/s): one mis-detected frame cannot max the meter.</summary>
        public float maxNodeSpeed;
        /// <summary>Time constant (s) while the energy rises.</summary>
        public float attackSeconds;
        /// <summary>Time constant (s) while the energy falls.</summary>
        public float releaseSeconds;
        /// <summary>Nodes detected in both frames of a pair needed for a measurement; fewer = no motion (0).</summary>
        public int minNodes;
        /// <summary>Samples further apart than this are a gap: no speed is measured across it.</summary>
        public float maxSampleGapSeconds;
    }

    /// <summary>
    /// Whole-body motion intensity (GDD v2 §3). Each raw sample (one camera frame) gives node positions in inches;
    /// each node detected in two consecutive samples contributes its speed, clamped to maxNodeSpeed, minus the jitter
    /// deadzone; the mean over those nodes / fullSpeed is the target (0..1). Tick eases Energy01 toward it with a fast
    /// attack and a slow release (unscaled frame time). Allocation-free after construction.
    /// </summary>
    public sealed class MotionEnergyFilter
    {
        readonly Vector2[] previous;
        readonly bool[] previousDetected;
        bool hasPrevious;
        double previousTime;

        public MotionEnergyFilter(int nodeCount, MotionEnergySettings settings)
        {
            previous = new Vector2[nodeCount];
            previousDetected = new bool[nodeCount];
            Settings = settings;
        }

        /// <summary>Thresholds; may be replaced at any time (live Inspector edits).</summary>
        public MotionEnergySettings Settings { get; set; }

        /// <summary>Smoothed energy (0..1).</summary>
        public float Energy01 { get; private set; }

        /// <summary>Unsmoothed intensity of the last measured sample pair (0..1); 0 after MarkNoBody.</summary>
        public float Target01 { get; private set; }

        /// <summary>Mean per-node speed above the deadzone of the last measurement (in/s, readout).</summary>
        public float MeanExcessSpeed { get; private set; }

        /// <summary>Nodes measured in the last sample pair (readout).</summary>
        public int MeasuredNodes { get; private set; }

        #region Public Methods

        /// <summary>
        /// Feeds one sample at a strictly increasing time: positions (inches, any shared origin) and whether each node
        /// was detected, both of length nodeCount. Repeated or older times only resync the history.
        /// </summary>
        public void AddSample(double time, Vector2[] positions, bool[] detected)
        {
            var settings = Settings;
            var dt = time - previousTime;
            if (hasPrevious && dt > 0.0 && dt <= settings.maxSampleGapSeconds)
            {
                Measure((float)dt, positions, detected, settings);
            }
            Remember(time, positions, detected);
        }

        /// <summary>No body this frame (lost tracking): the target drops to 0 and the history resyncs.</summary>
        public void MarkNoBody()
        {
            hasPrevious = false;
            Target01 = 0f;
            MeanExcessSpeed = 0f;
            MeasuredNodes = 0;
        }

        /// <summary>Eases Energy01 toward the target over dt (unscaled seconds).</summary>
        public void Tick(float dt)
        {
            var settings = Settings;
            Energy01 = Smooth(Energy01, Target01, dt, settings.attackSeconds, settings.releaseSeconds);
        }

        /// <summary>Energy and history back to zero.</summary>
        public void Reset()
        {
            MarkNoBody();
            Energy01 = 0f;
        }

        /// <summary>Exponential ease toward target with time constant attack (rising) or release (falling).</summary>
        public static float Smooth(float current, float target, float dt, float attackSeconds, float releaseSeconds)
        {
            var tau = target > current ? attackSeconds : releaseSeconds;
            if (tau <= 0f || dt <= 0f)
            {
                return tau <= 0f ? target : current;
            }
            return current + (target - current) * (1f - Mathf.Exp(-dt / tau));
        }

        #endregion

        #region Helpers

        void Measure(float dt, Vector2[] positions, bool[] detected, in MotionEnergySettings settings)
        {
            var sum = 0f;
            var count = 0;
            var length = Mathf.Min(positions.Length, previous.Length);
            for (var i = 0; i < length; i++)
            {
                if (!detected[i] || !previousDetected[i])
                {
                    continue;
                }
                var speed = Mathf.Min((positions[i] - previous[i]).magnitude / dt, settings.maxNodeSpeed);
                sum += Mathf.Max(0f, speed - settings.deadzone);
                count++;
            }

            MeasuredNodes = count;
            if (count < Mathf.Max(1, settings.minNodes))
            {
                MeanExcessSpeed = 0f;
                Target01 = 0f;
                return;
            }
            MeanExcessSpeed = sum / count;
            Target01 = settings.fullSpeed > 0f ? Mathf.Clamp01(MeanExcessSpeed / settings.fullSpeed) : 0f;
        }

        void Remember(double time, Vector2[] positions, bool[] detected)
        {
            var length = Mathf.Min(positions.Length, previous.Length);
            for (var i = 0; i < length; i++)
            {
                previous[i] = positions[i];
                previousDetected[i] = detected[i];
            }
            hasPrevious = true;
            previousTime = time;
        }

        #endregion
    }
}
