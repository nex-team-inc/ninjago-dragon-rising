#nullable enable

namespace Nex.Ninjago
{
    /// <summary>One player's kick pulses: that player's knee and hip nodes only, or that player's simulated knee.</summary>
    public sealed class PlayerKicks
    {
        readonly PlayerBody body;
        readonly KickDetector detector = new();
        int seenSimulatedPulses;

        public PlayerKicks(PlayerBody body)
        {
            this.body = body;
            detector.Begin(body.RestingKneeDrops);
            seenSimulatedPulses = SimulatedBody.GetKneePulses(body.PlayerIndex);
        }

        #region Public API

        public KickDetector.Pulse Poll(float time, in KickDetector.Settings settings)
        {
            if (SimulatedBody.IsEnabled)
            {
                var pulses = SimulatedBody.GetKneePulses(body.PlayerIndex);
                if (pulses == seenSimulatedPulses) return KickDetector.Pulse.None;
                seenSimulatedPulses = pulses;
                return detector.External(time, settings.cooldownSeconds);
            }

            return body.TryGetKneeDrops(out var drops) ? detector.Update(drops, time, settings) : KickDetector.Pulse.None;
        }

        #endregion
    }
}
