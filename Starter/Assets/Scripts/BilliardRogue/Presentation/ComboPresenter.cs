#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Combo feedback: rising SFX pitch on a ball's successive hits, and the "x{n} COMBO" float for the flight's combo
    /// (every enemy hit while balls fly) at JuiceConfig's milestones, so a volley shows one climbing count instead of a
    /// float per ball.
    /// </summary>
    public sealed class ComboPresenter
    {
        readonly JuiceConfig juice;
        readonly WorldLabelLayer labels;

        public ComboPresenter(JuiceConfig aJuice, WorldLabelLayer aLabels)
        {
            juice = aJuice;
            labels = aLabels;
        }

        public float Pitch(int combo)
        {
            return Mathf.Min(juice.ComboPitchMax, 1f + juice.ComboPitchStep * Mathf.Max(0, combo - 1));
        }

        public void OnFlightCombo(int combo, Vector3 world, Color color)
        {
            var first = juice.ComboShowThreshold;
            if (combo < first || (combo - first) % juice.ComboShowEvery != 0) return;
            labels.ShowCombo(world + Vector3.up * 0.3f, combo, color);
        }
    }
}
