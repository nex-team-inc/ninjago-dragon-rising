#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>Per-ball combo feedback: rising SFX pitch on successive hits and the "x{n} COMBO" float past the threshold.</summary>
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

        public void OnComboChanged(int combo, Vector3 world, Color playerColor)
        {
            if (combo < juice.ComboShowThreshold) return;
            labels.ShowCombo(world + Vector3.up * 0.3f, combo, playerColor);
        }
    }
}
