#nullable enable

using System;
using Nex.Util;
using UnityEngine;

namespace Nex.BilliardRogue
{
    /// <summary>
    /// Root component of Arena.prefab next to ArenaLayout (TDD §17): configures the layout from ArenaConfig, swaps
    /// the kit's *_Surface materials per act and exposes the danger-row pulse to Presentation-Core.
    /// </summary>
    public sealed class ArenaView : MonoBehaviour
    {
        [Serializable]
        public struct SurfaceRenderers
        {
            public ArenaSurface surface;
            public Renderer[] renderers;
        }

        [Header("Parts")]
        [SerializeField] ArenaLayout layout = null!;
        [SerializeField] DangerRowPulse dangerRow = null!;
        [Tooltip("Renderers of the kit's *_Surface parts per surface slot (baked by EnvironmentBuilder).")]
        [SerializeField] SurfaceRenderers[] surfaces = Array.Empty<SurfaceRenderers>();

        public ArenaLayout Layout => layout;
        public DangerRowPulse DangerRow => dangerRow;

        public void Initialize(ArenaConfig config)
        {
            layout.Initialize(config.Rules, config.WorldScale);
            dangerRow.Initialize(config.DangerRow);
        }

        /// <summary>Assigns the act's surface materials; slots without a material keep their current one.</summary>
        public void ApplySurfaces(EnumDictionary<ArenaSurface, Material> materials)
        {
            foreach (var slot in surfaces)
            {
                if (!materials.TryGetValue(slot.surface, out var material) || material == null) continue;
                foreach (var surfaceRenderer in slot.renderers)
                {
                    surfaceRenderer.sharedMaterial = material;
                }
            }
        }

        /// <summary>0 = danger row empty (steady dim glow) … 1 = occupied / about to be hit (fast bright pulse).</summary>
        public void SetDangerLevel(float level01) => dangerRow.SetDangerLevel(level01);
    }
}
