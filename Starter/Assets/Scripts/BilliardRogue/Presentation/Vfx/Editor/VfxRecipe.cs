#nullable enable

using System.Collections.Generic;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// One VFX prefab: its layers (the first is the root ParticleSystem that VfxManager pools and whose stop callback
    /// returns the instance), its VfxManager registration (pooled bursts) or ambient looping mode (act atmosphere).
    /// </summary>
    public sealed class VfxRecipe
    {
        public readonly string prefabName;
        public readonly VfxManager.VisualEffect? effect;
        public readonly int defaultPoolSize;
        public readonly int maxPoolSize;
        public readonly List<VfxLayerSpec> layers = new();

        VfxRecipe(string prefabName, VfxManager.VisualEffect? effect, int defaultPoolSize, int maxPoolSize)
        {
            this.prefabName = prefabName;
            this.effect = effect;
            this.defaultPoolSize = defaultPoolSize;
            this.maxPoolSize = maxPoolSize;
        }

        public bool IsAmbient => effect == null;

        public static VfxRecipe Burst(VfxManager.VisualEffect effect, int defaultPool, int maxPool) =>
            new($"Vfx_{effect}", effect, defaultPool, maxPool);

        public static VfxRecipe Ambient(int act) => new($"Vfx_Ambient_Act{act}", null, 0, 0);

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
