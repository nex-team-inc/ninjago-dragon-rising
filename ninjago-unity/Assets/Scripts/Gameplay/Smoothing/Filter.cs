// Copied from Jazz

using System;
using System.Collections.Generic;
using Jazz;
using UnityEngine;

namespace Nex
{
    public interface IFilter
    {
        public float Filter(float x, float? timestamp=null);
    }

    public interface IFilter2D
    {
        public Vector2 Filter(float x, float y, float? timestamp=null);
    }

    public class ComposedFilter2D<T> : IFilter2D
    {
        readonly IFilter filterX;
        readonly IFilter filterY;

        public ComposedFilter2D(T filterX, T filterY)
        {
            this.filterX = filterX as IFilter;
            this.filterY = filterY as IFilter;
        }

        public Vector2 Filter(float x, float y, float? timestamp=null)
        {
            return new Vector2(filterX.Filter(x, timestamp), filterY.Filter(y, timestamp));
        }
    }

    public class WrappedKalmanFilter2D : IFilter2D
    {
        readonly KalmanFilter2D kalmanFilter2D;

        public WrappedKalmanFilter2D(KalmanFilter2D filter)
        {
            kalmanFilter2D = filter;
        }

        public Vector2 Filter(float x, float y, float? timestamp=null)
        {
            return kalmanFilter2D.PositionWithKalmanFilter(x, y).ToVector2();
        }
    }


    public class MovingAverageFilter : IFilter
    {
        // Consider using List.Average
        static float Mean(List<float> x) {
            var sum = 0.0f;
            foreach (var t in x)
            {
                sum += t;
            }
            return sum / x.Count;
        }

        // Consider doing it more efficiently
        // ReSharper disable once UnusedMember.Local
        float Median(List<float> x) {
            x.Sort();

            if (x.Count % 2 == 0)
            {
                return (x[x.Count / 2 - 1] + x[x.Count / 2]) / 2;
            }

            return x[x.Count / 2];
        }

        readonly List<float> mvWindow = new();
        readonly int mvWindowSize;

        public MovingAverageFilter(int window=14)
        {
            mvWindowSize = window;
        }

        public float Filter(float x, float? timestamp=null)
        {
            if (mvWindow.Count == mvWindowSize)
            {
                mvWindow.RemoveAt(0);
            }
            mvWindow.Add(x);
            return Mean(mvWindow);
        }
    }

    public class SingleExponentialFilter : IFilter
    {
        readonly string seVariant;
        float seLastEstimate;

        public float? lastX { get; private set; }

        public float alpha { get; set; }

        public SingleExponentialFilter(float alpha=0.01f, string variant="predict")
        {
            this.alpha = alpha;
            seVariant = variant;
        }

        float Predict()
        {
            Debug.Assert(lastX != null, nameof(lastX) + " != null");
            return alpha * (float)lastX + (1.0f - alpha) * seLastEstimate;
        }

        float Lowpass(float x)
        {
            return alpha * x + (1.0f - alpha) * seLastEstimate;
        }

        public float Filter(float x, float? timestamp=null)
        {
            if (lastX == null)
            {
                seLastEstimate = x;
            }
            else
            {
                seLastEstimate = seVariant == "predict" ? Predict() : Lowpass(x);
            }
            lastX = x;

            return seLastEstimate;
        }
    }

    public class DoubleExponentialFilter : IFilter
    {
        readonly float deAlpha;
        readonly float deGamma;
        float? deLastX;
        float? deLastEstimate;
        float deB;

        public DoubleExponentialFilter()
        {
            deAlpha = 0.01f;
            deGamma = 0.01f;
        }

        public float Filter(float x, float? timestamp=null) {
            if (deLastX == null)
            {
                deLastEstimate = x;
                deB = 0.0f;
            }
            else
            {
                Debug.Assert(deLastEstimate != null, nameof(deLastEstimate) + " != null");
                var s = deAlpha * x + (1.0f - deAlpha) * ((float)deLastEstimate + deB);
                deB = deGamma * (s - (float)deLastEstimate) + (1.0f - deGamma) * deB;
                deLastEstimate = s;
            }
            deLastX = x;

            return (float)deLastEstimate;
        }
    }

    public class OneEuroFilter : IFilter
    {
        float oeFreq;
        readonly float oeMinCutOff;
        readonly float oeBeta;
        readonly float oeDCutOff;

        readonly SingleExponentialFilter oeX;
        readonly SingleExponentialFilter oeDx;

        float? oeLastTime;

        public OneEuroFilter(float minCutOff=1.0f, float beta=0.007f, float dCutOff=1.0f)
        {
            oeFreq = 25.0f;
            oeMinCutOff = minCutOff;
            oeBeta = beta;
            oeDCutOff = dCutOff;

            oeX = new SingleExponentialFilter(ComputeAlpha(oeMinCutOff), "lowpass");
            oeDx = new SingleExponentialFilter(ComputeAlpha(oeDCutOff), "lowpass");
        }

        float ComputeAlpha(float cutoff)
        {
            var te = 1.0f / oeFreq;
            var tau = 1.0f / (2.0f * Math.PI * cutoff);
            return (float)(1.0f / (1.0f + tau / te));
        }

        public float Filter(float x, float? timestamp=null)
        {
            if (oeLastTime != null && timestamp != null && oeLastTime != timestamp)
            {
                oeFreq = 1.0f / ((float)timestamp - (float)oeLastTime);
            }

            oeLastTime = timestamp;

            var previousX = oeX.lastX;
            var dx = previousX == null ? 0 : (x - (float)previousX) * oeFreq;
            oeDx.alpha = ComputeAlpha(oeDCutOff);

            var edx = oeDx.Filter(dx);
            var cutoff = oeMinCutOff + oeBeta * Math.Abs(edx);
            oeX.alpha = ComputeAlpha(cutoff);
            return oeX.Filter(x);
        }
    }
}
