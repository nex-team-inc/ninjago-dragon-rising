using System;
using System.Collections.Generic;
using System.Linq;
using Jazz;

namespace Nex
{
    public class WeightedFloatHistory
    {
        public readonly List<TimedItem<(float, float)>> items = new();
        readonly double timeWindow;
        double curFrameTime;
        float totalWeightedSum;
        float totalWeight;

        public WeightedFloatHistory(double timeWindow)
        {
            this.timeWindow = timeWindow;
        }

        void CleanUp()
        {
            while (items.Count > 0 && items.Last().frameTime < curFrameTime - timeWindow)
            {
                totalWeightedSum -= items.Last().item.Item1 * items.Last().item.Item2;
                totalWeight -= items.Last().item.Item2;
                items.RemoveAt(items.Count - 1);
            }
        }

        public void Add(float item, float weight, double frameTime)
        {
            if (items.Count > 0 && frameTime <= items.First().frameTime)
            {
                // No need to add because the item is not newer.
                return;
            }
            totalWeightedSum += item * weight;
            totalWeight += weight;
            items.Insert(0, new TimedItem<(float, float)>((item, weight), frameTime));
        }

        public void Clear()
        {
            totalWeightedSum = 0;
            items.Clear();
        }

        public void UpdateCurrentFrameTime(double frameTime)
        {
            curFrameTime = frameTime;
            CleanUp();
        }

        public List<TimedItem<(float, float)>> ItemsFromNewToOld()
        {
            return items;
        }

        public double TimeSpan()
        {
            if (items.Count > 0)
            {
                return Math.Abs(items.First().frameTime - items.Last().frameTime);
            }

            return 0;
        }

        public float WeightedAverage()
        {
            return totalWeight == 0 ? 0 : totalWeightedSum / totalWeight;
        }

        public float WeightedSum()
        {
            return totalWeightedSum;
        }
    }
}
