#nullable enable

using UnityEngine;

namespace Nex.Ninjago
{
    /// <summary>
    /// Time scale of one courtyard. Split screen runs two exchanges at once, so the counter slow motion is per player
    /// instead of Time.timeScale, which would also slow the other player's staff.
    /// </summary>
    public sealed class LocalClock
    {
        float target = 1f;
        float ratePerSecond;

        public float Scale { get; private set; } = 1f;
        public float DeltaTime => Time.unscaledDeltaTime * Scale;

        public void EaseTo(float aTarget, float seconds)
        {
            target = aTarget;
            ratePerSecond = seconds <= 0f ? float.MaxValue : Mathf.Abs(target - Scale) / seconds;
        }

        public void Tick()
        {
            Scale = Mathf.MoveTowards(Scale, target, ratePerSecond * Time.unscaledDeltaTime);
        }
    }
}
