#nullable enable

using System.Collections.Generic;
using UnityEngine;
using static Nex.BilliardRogue.Editor.VfxPalette;
using L = Nex.BilliardRogue.Editor.VfxLayerSpec;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Looping act atmosphere (GDD §7: Mossy Ruins leaves, Sunken Crypt embers, Crystal Hollow motes). The prefab root sits
    /// at the arena origin (launch-line centre, TDD §14.1); emitter boxes cover the arena (x ±3.5, z 0..11.6) plus the
    /// dressing margin the camera sees. Prewarmed so the air is already full when the act loads; ≤ 60 live particles each.
    /// </summary>
    public static class VfxAmbientRecipes
    {
        const float Loop = 6f;
        static readonly Vector3 ArenaCentre = new(0f, 0f, 6.5f);
        static readonly Vector3 Span = new(16f, 0f, 15f);

        public static void AddTo(List<VfxRecipe> recipes)
        {
            recipes.Add(Act1());
            recipes.Add(Act2());
            recipes.Add(Act3());
        }

        static Vector3 Volume(float height) => new(Span.x, height, Span.z);

        static Vector3 Centre(float y) => ArenaCentre + new Vector3(0f, y, 0f);

        // Falling leaves settle on the floor (plane at the root) before they shrink away; golden motes hover low.
        static VfxRecipe Act1() => VfxRecipe.Ambient(1)
            .Add(L.Lit("Leaves", "Leaf").Stream(3.5f, 36, Loop).Life(8f, 10f).Box(Volume(0.5f), Centre(6f))
                .Drift(new Vector3(-0.1f, -0.9f, -0.25f), new Vector3(0.45f, -0.6f, 0.1f)).Noise(0.6f, 0.35f)
                .Size(0.5f, 0.57f).Colors(LeafGreen, LeafAutumn).Frames(0, 7).SizeOverLife(PopInOut()).Bounce(0.05f, 0.9f))
            .Add(L.Glow("Motes", "Mote").Stream(2.5f, 20, Loop).Life(5f, 7f).Box(Volume(3f), Centre(1.8f))
                .Drift(new Vector3(-0.1f, 0.05f, -0.1f), new Vector3(0.1f, 0.25f, 0.1f)).Noise(0.3f, 0.5f)
                .Size(0.4f, 0.5f).Colors(Gold, PaleYellow).Frames(0, 7).SizeOverLife(PopInOut()));

        // Embers rise from the floor near the torches' warmth; dust motes are lit sprites so torchlight picks them out.
        static VfxRecipe Act2() => VfxRecipe.Ambient(2)
            .Add(L.Emissive("Embers", "Ember").Stream(3f, 24, Loop).Life(4f, 6f).Box(Volume(0.5f), Centre(0.4f))
                .Drift(new Vector3(-0.15f, 0.35f, -0.1f), new Vector3(0.2f, 0.7f, 0.2f)).Noise(0.5f, 0.5f)
                .Size(0.45f, 0.57f).Colors(Orange, SparkYellow).Frames(0, 7).SizeOverLife(PopInOut()))
            .Add(L.Lit("Dust", "Mote").Stream(3f, 24, Loop).Life(6f, 8f).Box(Volume(4f), Centre(2f))
                .Drift(new Vector3(-0.1f, -0.05f, -0.1f), new Vector3(0.1f, 0.08f, 0.1f)).Noise(0.2f, 0.3f)
                .Size(0.35f, 0.45f).Colors(new Color(0.75f, 0.78f, 0.88f), new Color(0.6f, 0.62f, 0.75f)).Frames(0, 7).SizeOverLife(PopInOut()));

        // Crystal motes drift upward in the violet/cyan glow; short star twinkles pop in between them.
        static VfxRecipe Act3() => VfxRecipe.Ambient(3)
            .Add(L.Glow("CrystalMotes", "Mote").Stream(3.5f, 30, Loop).Life(6f, 8f).Box(Volume(3.5f), Centre(2f))
                .Drift(new Vector3(-0.08f, 0.1f, -0.08f), new Vector3(0.08f, 0.3f, 0.08f)).Noise(0.35f, 0.4f)
                .Size(0.4f, 0.57f).Colors(Cyan, Violet).Frames(0, 7).SizeOverLife(PopInOut()))
            .Add(L.Emissive("Sparkles", "Star").Stream(4f, 6, Loop).Life(FullSheet).Box(Volume(3f), Centre(1.6f))
                .Size(0.5f, 0.57f).Colors(WarmWhite, Ice).Frames(0, 0));
    }
}
