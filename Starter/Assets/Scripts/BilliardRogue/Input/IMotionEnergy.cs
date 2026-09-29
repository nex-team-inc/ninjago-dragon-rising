#nullable enable

namespace Nex.BilliardRogue
{
    /// <summary>Whole-body motion intensity of one player, used to charge Hype while balls fly (GDD v2 §3).</summary>
    public interface IMotionEnergy
    {
        /// <summary>Smoothed, body-normalized motion intensity in 0..1.</summary>
        float Energy01 { get; }

        /// <summary>False while the player's body is not tracked; Energy01 then decays to 0.</summary>
        bool IsTracked { get; }
    }
}
