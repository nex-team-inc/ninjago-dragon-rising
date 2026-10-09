#nullable enable

using System.Collections.Generic;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// One VFX prefab: its layers (the first is the root ParticleSystem that VfxManager pools and whose stop callback
    /// returns the instance) and its VfxManager registration: a pooled burst, or the looping act atmosphere
    /// (IsAmbient, registered as AmbientAct{n} with a pool of one that ActEnvironment plays and stops).
    /// </summary>
    public sealed class VfxRecipe
    {
        public readonly string prefabName;
        public readonly VfxManager.VisualEffect effect;
        public readonly int defaultPoolSize;
        public readonly int maxPoolSize;
        public readonly List<VfxLayerSpec> layers = new();

        VfxRecipe(string prefabName, VfxManager.VisualEffect effect, int defaultPoolSize, int maxPoolSize, bool ambient)
        {
            this.prefabName = prefabName;
            this.effect = effect;
            this.defaultPoolSize = defaultPoolSize;
            this.maxPoolSize = maxPoolSize;
            IsAmbient = ambient;
        }

        public bool IsAmbient { get; }

        public static VfxRecipe Burst(VfxManager.VisualEffect effect, int defaultPool, int maxPool) =>
            new($"Vfx_{effect}", effect, defaultPool, maxPool, false);

        public static VfxRecipe Ambient(int act) => new($"Vfx_Ambient_Act{act}", AmbientEffect(act), 1, 1, true);

        /// <summary>The registry key of act (1-based) ambient prefab.</summary>
        public static VfxManager.VisualEffect AmbientEffect(int act) => act switch
        {
            1 => VfxManager.VisualEffect.AmbientAct1,
            2 => VfxManager.VisualEffect.AmbientAct2,
            _ => VfxManager.VisualEffect.AmbientAct3,
        };

        public VfxRecipe Add(VfxLayerSpec layer)
        {
            layers.Add(layer);
            return this;
        }

        public bool NeedsFloorPlane()
        {
            foreach (var layer in layers)
            {
                if (layer.bounce) return true;
            }

            return false;
        }

        public int ParticleBudget()
        {
            var total = 0;
            foreach (var layer in layers)
            {
                total += layer.ParticleBudget;
            }

            return total;
        }
    }
}
