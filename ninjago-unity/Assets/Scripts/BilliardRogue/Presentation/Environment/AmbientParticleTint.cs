#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Tints the start colours of a pooled ambient particle instance (root and children) from their authored values,
    /// so re-applying on pool reuse or for another look (Title tint, act tint) never compounds. Added to the
    /// instance by ActEnvironment on first use.
    /// </summary>
    public sealed class AmbientParticleTint : MonoBehaviour
    {
        readonly List<ParticleSystem> systems = new();
        readonly List<ParticleSystem.MinMaxGradient> authored = new();

        #region Public Methods

        public void Apply(Color tint)
        {
            if (systems.Count == 0) Capture();
            for (var i = 0; i < systems.Count; i++)
            {
                var main = systems[i].main;
                main.startColor = Tint(authored[i], tint);
            }
        }

        #endregion

        #region Helpers

        void Capture()
        {
            GetComponentsInChildren(true, systems);
            for (var i = 0; i < systems.Count; i++)
            {
                authored.Add(systems[i].main.startColor);
            }
        }

        static ParticleSystem.MinMaxGradient Tint(ParticleSystem.MinMaxGradient gradient, Color tint)
        {
            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.Color:
                    gradient.color *= tint;
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    gradient.colorMin *= tint;
                    gradient.colorMax *= tint;
                    break;
            }

            return gradient;
        }

        #endregion
    }
}
