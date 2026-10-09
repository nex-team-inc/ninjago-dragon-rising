using System.Threading;
using Cysharp.Threading.Tasks;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace Nex.Utils
{
    // ReSharper disable once InconsistentNaming
    public static class MMFeedbacksExtension
    {
        const float animateRatio = 1f;
        const float instantRatio = 0.001f;

        public static UniTask PlayAsUniTask(
            this MMFeedbacks feedbacks,
            bool animate = true,
            MMFeedbacks.Directions direction = MMFeedbacks.Directions.TopToBottom,
            bool reverted = false,
            float durationMultiplier = 1f,
            CancellationToken cancellationToken = default)
        {
            feedbacks.Direction = reverted
                ? direction == MMFeedbacks.Directions.TopToBottom
                    ? MMFeedbacks.Directions.BottomToTop
                    : MMFeedbacks.Directions.TopToBottom
                : direction;
            feedbacks.DurationMultiplier =
                (animate ? animateRatio : instantRatio) * durationMultiplier;

            return feedbacks.PlayFeedbacksCoroutine(Vector3.zero).ToUniTask(cancellationToken: cancellationToken);
        }
    }
}
