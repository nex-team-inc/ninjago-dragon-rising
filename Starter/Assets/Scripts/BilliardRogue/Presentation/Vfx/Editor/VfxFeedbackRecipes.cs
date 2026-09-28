#nullable enable

using System.Collections.Generic;
using UnityEngine;
using static Nex.BilliardRogue.Editor.VfxPalette;
using Fx = Nex.VfxManager.VisualEffect;
using L = Nex.BilliardRogue.Editor.VfxLayerSpec;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Support, pickup and board feedback bursts. DustPuff is spawned at floor height (0.02 m; boss landings scale it 2×),
    /// LevelUpBurst on the cats at stage start (1.5×) and on power shots, the rest at the object centre.
    /// </summary>
    public static class VfxFeedbackRecipes
    {
        public static void AddTo(List<VfxRecipe> recipes)
        {
            recipes.Add(HealSparkle());
            recipes.Add(SplitPop());
            recipes.Add(PortalFlash());
            recipes.Add(PickupSparkle());
            recipes.Add(DustPuff());
            recipes.Add(PlayerHurtFlash());
            recipes.Add(CratePieces());
            recipes.Add(LevelUpBurst());
        }

        #region Support

        static VfxRecipe HealSparkle() => VfxRecipe.Burst(Fx.HealSparkle, 2, 6)
            .Add(L.Glow("Hearts", "Heart").Burst(3).Life(FullSheet).Speed(0.4f, 0.8f).Sphere(0.35f, 0.5f).Drag(1f).Gravity(-0.35f)
                .Size(0.57f).Colors(HeartPink, Pink).Frames(0, 0))
            .Add(L.Glow("Stars", "Star").Delay(0.05f).Burst(6).Life(6 * Frame, FullSheet).Speed(0.3f, 0.7f).Sphere(0.5f, 0.4f).Gravity(-0.3f)
                .Size(0.45f, 0.57f).Colors(HealGreen, WarmWhite).Frames(0, 1))
            .Add(L.Glow("Flash", "Flash").Burst(1).Life(3 * Frame).Size(0.9f).Colors(HealGreen));

        static VfxRecipe SplitPop() => VfxRecipe.Burst(Fx.SplitPop, 2, 6)
            .Add(L.Glow("Ring", "Ring").Burst(1).Life(5 * Frame).Size(1f).Colors(Pink).Frames(1, 1).Fade())
            .Add(L.Glow("Stars", "Star").Burst(6).Life(0.4f, 0.55f).Speed(2f, 3.2f).Sphere(0.1f).Drag(4f)
                .Size(0.5f, 0.57f).Colors(Pink, Violet).Frames(1, 2))
            .Add(L.Glow("Sparks", "Spark").Burst(4).Life(0.3f).Speed(3f, 4f).Sphere(0.05f).Drag(5f)
                .Size(0.45f).Colors(WarmWhite).Frames(0, 1).Fade());

        static VfxRecipe PortalFlash() => VfxRecipe.Burst(Fx.PortalFlash, 2, 6)
            .Add(L.Glow("RingOut", "Ring").Burst(1).Life(5 * Frame).Size(1.3f).Colors(Violet).Frames(1, 1).Fade())
            .Add(L.Glow("RingIn", "Ring").Delay(0.12f).Burst(1).Life(5 * Frame).Size(0.9f).Colors(Cyan).Frames(0, 0).Fade())
            .Add(L.Glow("Motes", "Mote").Burst(8).Life(0.33f, 0.4f).Speed(-2.2f, -1.6f).Sphere(0.7f, 0.1f)
                .Size(0.4f, 0.5f).Colors(Violet, Cyan).Frames(0, 7))
            .Add(L.Glow("Flash", "Flash").Burst(1).Life(3 * Frame).Size(0.9f).Colors(Pink).SizeOverLife(Punch()));

        static VfxRecipe PickupSparkle() => VfxRecipe.Burst(Fx.PickupSparkle, 2, 6)
            .Add(L.Glow("Stars", "Star").Burst(7).Life(6 * Frame, FullSheet).Speed(0.4f, 1f).Sphere(0.45f, 0.5f).Gravity(-0.35f)
                .Size(0.5f, 0.62f).Colors(Gold, WarmWhite).Frames(0, 1))
            .Add(L.Glow("Flash", "Flash").Burst(1).Life(3 * Frame).Size(0.9f).Colors(PaleYellow).SizeOverLife(Punch()))
            .Add(L.Glow("Sparks", "Spark").Burst(5).Life(0.3f, 0.4f).Speed(2.5f, 4f).Sphere(0.05f).Drag(5f)
                .Size(0.45f).Colors(SparkYellow, WarmWhite).Frames(0, 1).Fade());

        static VfxRecipe LevelUpBurst() => VfxRecipe.Burst(Fx.LevelUpBurst, 2, 4)
            .Add(L.Glow("RingA", "Ring").Burst(1).Life(6 * Frame).Size(1.4f).Colors(Gold).Fade())
            .Add(L.Glow("RingB", "Ring").Delay(0.15f).Burst(1).Life(6 * Frame).Size(1.8f).Colors(PaleYellow).Fade())
            .Add(L.Glow("Stars", "Star").Burst(10).Life(7 * Frame, FullSheet).Speed(0.2f, 0.5f).Circle(0.5f, 0.2f).Gravity(-0.5f)
                .Size(0.5f, 0.62f).Colors(Gold, WarmWhite).Frames(0, 1))
            .Add(L.Glow("Sparks", "Spark").Burst(8).Life(0.4f, 0.6f).Speed(3f, 5f).Cone(20f, 0.2f).Drag(3f)
                .Size(0.5f).Colors(SparkYellow, WarmWhite).Frames(0, 1).Fade())
            .Add(L.Glow("Flash", "Flash").Burst(1).Life(3 * Frame).Size(1.2f).Colors(PaleYellow).SizeOverLife(Punch()));

        #endregion

        #region Board

        static VfxRecipe DustPuff() => VfxRecipe.Burst(Fx.DustPuff, 8, 24)
            .Add(L.Lit("Dust", "Dust").Burst(5, 6).Life(6 * Frame, 7 * Frame).Speed(0.7f, 1.3f).Circle(0.25f).Offset(new Vector3(0f, 0.15f, 0f))
                .Drag(3f).Gravity(-0.05f).Size(0.5f, 0.62f).Colors(Dust, DustDark).Frames(0, 1));

        static VfxRecipe PlayerHurtFlash() => VfxRecipe.Burst(Fx.PlayerHurtFlash, 1, 3)
            .Add(L.Glow("Flash", "Flash").Burst(1).Life(4 * Frame).Size(1.6f).Colors(Red).SizeOverLife(Punch()))
            .Add(L.Glow("Ring", "Ring").Burst(1).Life(5 * Frame).Size(1.5f).Colors(Red).Frames(1, 1).Fade())
            .Add(L.Glow("Sparks", "Spark").Burst(8).Life(0.35f, 0.5f).Speed(3.5f, 5.5f).Sphere(0.1f).Drag(5f).Gravity(0.5f)
                .Size(0.5f, 0.57f).Colors(Red, PaleYellow).Frames(0, 1).Fade())
            .Add(L.Glow("BrokenHearts", "Heart").Burst(2).Life(5 * Frame).Speed(0.8f, 1.2f).Sphere(0.2f).Gravity(0.4f)
                .Size(0.57f).Colors(HeartPink).Frames(3, 3));

        static VfxRecipe CratePieces() => VfxRecipe.Burst(Fx.CratePieces, 2, 6)
            .Add(L.Lit("Planks", "Debris").Burst(9, 10).Life(0.9f, 1.3f).Speed(2.5f, 4.5f).Hemisphere(0.35f).Gravity(2.2f)
                .Size(0.5f, 0.57f).Colors(Wood, WoodLight).Frames(0, 7).Bounce(0.4f, 0.3f))
            .Add(L.Lit("Dust", "Dust").Burst(4).Life(6 * Frame).Speed(0.5f, 1f).Sphere(0.35f).Drag(3f)
                .Size(0.62f, 0.75f).Colors(Dust).Frames(0, 1))
            .Add(L.Glow("Sparks", "Spark").Burst(3).Life(3 * Frame).Speed(2f, 3f).Sphere(0.05f).Drag(4f)
                .Size(0.45f).Colors(WarmWhite).Frames(1, 2).Fade());

        #endregion
    }
}
