#nullable enable

using System.Collections.Generic;
using UnityEngine;
using static Nex.BilliardRogue.Editor.VfxPalette;
using Fx = Nex.VfxManager.VisualEffect;
using L = Nex.BilliardRogue.Editor.VfxLayerSpec;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Ball impacts, deaths and element bursts. Designed at scale 1 for a 1 m cell seen at ~30 px/m (640×360 world RT):
    /// 16 px sprites at 0.57 m render close to 1:1. BoardEventPlayer spawns them at the enemy centre height (0.45 m) and
    /// scales CritSpark by JuiceConfig.critVfxScale and Explosion by the blast radius in cells.
    /// </summary>
    public static class VfxCombatRecipes
    {
        public static void AddTo(List<VfxRecipe> recipes)
        {
            recipes.Add(HitSpark());
            recipes.Add(CritSpark());
            recipes.Add(WallSpark());
            recipes.Add(EnemyPoof());
            recipes.Add(BossPoof());
            recipes.Add(Explosion());
            recipes.Add(FreezeBurst());
            recipes.Add(BurnBurst());
            recipes.Add(PoisonBurst());
            recipes.Add(LightningHit());
        }

        #region Impacts

        static VfxRecipe HitSpark() => VfxRecipe.Burst(Fx.HitSpark, 6, 16)
            .Add(L.Glow("Flash", "Flash").Burst(1).Life(3 * Frame).Size(1.05f).Colors(PaleYellow).SizeOverLife(Punch()))
            .Add(L.Emissive("Sparks", "Spark").Burst(6, 8).Life(0.33f, 0.5f).Speed(3.5f, 5.5f).Sphere(0.1f).Drag(5f).Gravity(0.4f)
                .Size(0.45f, 0.57f).Colors(SparkYellow, PaleYellow).Frames(0, 1));

        static VfxRecipe CritSpark() => VfxRecipe.Burst(Fx.CritSpark, 3, 8)
            .Add(L.Glow("Flash", "Flash").Burst(1).Life(4 * Frame).Size(1.3f).Colors(PaleYellow).SizeOverLife(Punch()))
            .Add(L.Emissive("Ring", "Ring").Burst(1).Life(5 * Frame).Size(1.2f).Colors(Gold).Frames(1, 1))
            .Add(L.Emissive("Sparks", "Spark").Burst(10, 12).Life(0.35f, 0.55f).Speed(4f, 7f).Sphere(0.1f).Drag(5f).Gravity(0.5f)
                .Size(0.5f, 0.6f).Colors(Gold, WarmWhite).Frames(0, 1))
            .Add(L.Emissive("Stars", "Star").Delay(0.05f).Burst(4).Life(6 * Frame).Speed(0.3f, 0.6f).Sphere(0.6f, 0.5f).Gravity(-0.2f)
                .Size(0.57f).Colors(WarmWhite, Gold).Frames(1, 2));

        static VfxRecipe WallSpark() => VfxRecipe.Burst(Fx.WallSpark, 6, 16)
            .Add(L.Glow("Flash", "Flash").Burst(1).Life(2 * Frame).Size(0.6f).Colors(WarmWhite))
            .Add(L.Emissive("Sparks", "Spark").Burst(5, 6).Life(0.25f, 0.4f).Speed(2.5f, 4f).Sphere(0.05f).Drag(6f).Gravity(0.8f)
                .Size(0.4f, 0.5f).Colors(PaleYellow, Ice).Frames(1, 2))
            .Add(L.Lit("Grit", "Dust").Burst(2).Life(5 * Frame, 6 * Frame).Speed(0.3f, 0.6f).Sphere(0.1f).Drag(2f)
                .Size(0.45f).Colors(Stone));

        #endregion

        #region Deaths

        static VfxRecipe EnemyPoof() => VfxRecipe.Burst(Fx.EnemyPoof, 4, 10)
            .Add(L.Lit("Puff", "Smoke").Burst(7, 8).Life(6 * Frame, 7 * Frame).Speed(1f, 1.8f).Sphere(0.3f).Drag(3f).Gravity(-0.15f)
                .Size(0.75f, 0.95f).Colors(SmokeLight, SmokeLavender).Frames(0, 1))
            .Add(L.Glow("Flash", "Flash").Burst(1).Life(3 * Frame).Size(1f).Colors(WarmWhite).SizeOverLife(Punch()))
            .Add(L.Emissive("Stars", "Star").Delay(0.08f).Burst(4, 5).Life(6 * Frame).Speed(1f, 1.8f).Sphere(0.5f, 0.3f).Drag(3f).Gravity(-0.1f)
                .Size(0.57f).Colors(Gold, WarmWhite).Frames(1, 2));

        static VfxRecipe BossPoof() => VfxRecipe.Burst(Fx.BossPoof, 1, 2)
            .Add(L.Glow("Flash", "Flash").Burst(1).Life(5 * Frame).Size(3f).Colors(WarmWhite).SizeOverLife(Punch()))
            .Add(L.Emissive("RingA", "Ring").Burst(1).Life(6 * Frame).Size(2.6f).Colors(PaleYellow).Frames(1, 1))
            .Add(L.Emissive("RingB", "Ring").Delay(0.2f).Burst(1).Life(6 * Frame).Size(3.6f).Colors(Orange).Frames(1, 1))
            .Add(L.Lit("Smoke", "Smoke").Burst(14).Life(6 * Frame, 8 * Frame).Speed(1.2f, 2.4f).Sphere(0.9f).Drag(2.5f).Gravity(-0.2f)
                .Size(1f, 1.4f).Colors(SmokeLight, SmokeLavender).Frames(0, 1))
            .Add(L.Lit("SmokeLate", "Smoke").Delay(0.25f).Burst(8).Life(6 * Frame, 8 * Frame).Speed(0.6f, 1.2f).Sphere(0.7f).Gravity(-0.3f)
                .Size(0.9f, 1.2f).Colors(SmokeLavender, SmokeLight).Frames(0, 1))
            .Add(L.Emissive("Sparks", "Spark").Burst(14).Life(0.4f, 0.7f).Speed(5f, 9f).Sphere(0.3f).Drag(4f).Gravity(0.6f)
                .Size(0.57f, 0.7f).Colors(Gold, WarmWhite).Frames(0, 1))
            .Add(L.Emissive("Stars", "Star").Delay(0.1f).Burst(8).Life(FullSheet).Speed(0.8f, 1.6f).Sphere(1.2f, 0.4f).Gravity(-0.25f)
                .Size(0.57f, 0.8f).Colors(WarmWhite, Gold).Frames(1, 2))
            .Add(L.Lit("Debris", "Debris").Burst(10).Life(1f, 1.4f).Speed(3f, 5.5f).Hemisphere(0.6f).Gravity(2f)
                .Size(0.45f, 0.57f).Colors(SmokeLavender, Rubble).Frames(0, 7).Bounce(0.4f, 0.25f));

        #endregion

        #region Elements

        static VfxRecipe Explosion() => VfxRecipe.Burst(Fx.Explosion, 2, 6)
            .Add(L.Glow("Flash", "Flash").Burst(1).Life(4 * Frame).Size(2.2f).Colors(PaleYellow).SizeOverLife(Punch()))
            .Add(L.Emissive("Ring", "Ring").Burst(1).Life(5 * Frame).Size(2.4f).Colors(Orange).Frames(1, 1))
            .Add(L.Emissive("Fire", "Smoke").Burst(8, 9).Life(0.33f, 0.5f).Speed(1.8f, 3.2f).Sphere(0.4f).Drag(4f).Gravity(-0.2f)
                .Size(0.9f, 1.2f).Colors(PaleYellow, Orange).TintTo(DeepOrange).Frames(0, 1))
            .Add(L.Lit("Soot", "Smoke").Delay(0.12f).Burst(6).Life(6 * Frame, 7 * Frame).Speed(0.6f, 1.2f).Sphere(0.6f).Gravity(-0.35f)
                .Size(0.9f, 1.1f).Colors(Soot, SootDark).Frames(1, 2))
            .Add(L.Emissive("Embers", "Ember").Burst(10).Life(0.5f, 0.9f).Speed(3f, 6f).Hemisphere(0.3f).Drag(2f).Gravity(1.2f)
                .Size(0.45f, 0.57f).Colors(SparkYellow, Orange).Frames(0, 7))
            .Add(L.Lit("Rubble", "Debris").Burst(6).Life(0.9f, 1.2f).Speed(2.5f, 4.5f).Hemisphere(0.4f).Gravity(2.2f)
                .Size(0.4f, 0.5f).Colors(Rubble, RubbleDark).Frames(0, 7).Bounce(0.4f, 0.3f));

        static VfxRecipe FreezeBurst() => VfxRecipe.Burst(Fx.FreezeBurst, 3, 8)
            .Add(L.Glow("Flash", "Flash").Burst(1).Life(3 * Frame).Size(1f).Colors(Ice).SizeOverLife(Punch()))
            .Add(L.Emissive("Ring", "Ring").Burst(1).Life(5 * Frame).Size(1.3f).Colors(Cyan).Frames(1, 1))
            .Add(L.Lit("Shards", "IceShard").Burst(7, 8).Life(0.8f, 1.1f).Speed(2.2f, 3.8f).Hemisphere(0.25f).Gravity(1.8f)
                .Size(0.5f, 0.57f).Colors(Ice, Cyan).Frames(0, 7).Bounce(0.45f, 0.25f))
            .Add(L.Emissive("Flakes", "Snow").Delay(0.05f).Burst(6).Life(0.6f, 0.9f).Speed(0.4f, 0.9f).Sphere(0.5f).Drag(2f).Gravity(0.05f)
                .Size(0.45f, 0.57f).Colors(Ice, Cyan).Frames(0, 7));

        // Flame-ball hit and burn tick (0.7×): a licking fire puff with rising flame tongues and a wisp of soot.
        static VfxRecipe BurnBurst() => VfxRecipe.Burst(Fx.BurnBurst, 6, 16)
            .Add(L.Glow("Flash", "Flash").Burst(1).Life(2 * Frame).Size(0.9f).Colors(Orange))
            .Add(L.Emissive("Fire", "Smoke").Burst(3, 4).Life(5 * Frame, 6 * Frame).Speed(0.5f, 1.1f).Sphere(0.25f).Drag(3f).Gravity(-0.6f)
                .Size(0.62f, 0.8f).Colors(PaleYellow, Gold).TintTo(Red).Frames(0, 1))
            .Add(L.Emissive("Flames", "Ember").Burst(6, 8).Life(0.5f, 0.8f).Speed(0.8f, 1.6f).Sphere(0.35f).Drag(2f).Gravity(-0.8f)
                .Size(0.62f, 0.8f).Colors(Gold, Orange).TintTo(DeepOrange).Frames(0, 7).SizeOverLife(Taper()))
            .Add(L.Lit("Smoke", "Smoke").Delay(0.15f).Burst(2, 3).Life(6 * Frame).Speed(0.3f, 0.6f).Sphere(0.2f).Gravity(-0.4f)
                .Size(0.57f, 0.7f).Colors(Soot, SootDark).Frames(1, 2));

        // Poison hit and tick (0.7×): a purple miasma cloud (GDD: purple poison numbers) with toxic green bubbles popping out.
        static VfxRecipe PoisonBurst() => VfxRecipe.Burst(Fx.PoisonBurst, 6, 16)
            .Add(L.Emissive("Miasma", "Smoke").Burst(4).Life(6 * Frame, 7 * Frame).Speed(0.5f, 1f).Sphere(0.35f).Drag(2f).Gravity(-0.1f)
                .Size(0.8f, 0.95f).Colors(PoisonPurple, Violet).TintTo(PoisonDark).Frames(0, 1))
            .Add(L.Emissive("Bubbles", "Bubble").Delay(0.04f).Burst(6, 7).Life(7 * Frame, FullSheet).Speed(0.4f, 1f).Sphere(0.45f).Drag(1.5f)
                .Gravity(-0.4f).Size(0.57f, 0.7f).Colors(PoisonGreen, HealGreen).Frames(0, 1))
            .Add(L.Glow("Flash", "Flash").Burst(1).Life(2 * Frame).Size(0.8f).Colors(PoisonPurple));

        // One crisp bolt strikes down onto the target, a second flickers in a frame later with another shape.
        static VfxRecipe LightningHit() => VfxRecipe.Burst(Fx.LightningHit, 4, 10)
            .Add(L.Emissive("Bolt", "Bolt").Burst(1).Life(3 * Frame).Offset(new Vector3(0f, 0.75f, 0f))
                .Size(1.6f).Colors(SparkYellow).Frames(0, 7))
            .Add(L.Emissive("BoltFlicker", "Bolt").Delay(Frame).Burst(1).Life(2 * Frame).Offset(new Vector3(0.1f, 0.7f, 0f))
                .Size(1.4f).Colors(Cyan).Frames(0, 7))
            .Add(L.Glow("Flash", "Flash").Burst(1).Life(2 * Frame).Size(0.7f).Colors(PaleYellow))
            .Add(L.Emissive("Sparks", "Spark").Burst(8).Life(0.25f, 0.4f).Speed(3f, 5.5f).Sphere(0.1f).Drag(5f).Gravity(0.3f)
                .Size(0.45f, 0.57f).Colors(PaleYellow, Cyan).Frames(0, 1));

        #endregion
    }
}
