#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Unity calibration of the act looks, applied by EnvironmentBuilder when it seeds a lighting preset (once per
    /// preset, see ActLightingPreset.seededFromLayout). layouts.json carries the art direction from the Blender preview
    /// (sun angles, shaft and particle tints); these values re-balance exposure and colour for BilliardRogue/ToonLit
    /// under the act's Volume grade, tuned from play-mode captures at the ArenaConfig camera pose (gameplay-view polish):
    /// the sun stays low enough (46–58° elevation) for hard, readable cast shadows under actors, its colour is only
    /// mildly tinted so the grade (not the light) carries the mood, ambient stays a step below the floor value, and
    /// the act lights are scaled until their pools read against the sun without washing the table.
    /// </summary>
    public static class EnvironmentLooks
    {
        static Color C(float r, float g, float b) => new(r, g, b, 1f);

        /// <summary>Overrides the layout-seeded preset of act actId (1-based) with its calibrated values.</summary>
        public static void Calibrate(int actId, ActLightingPreset look)
        {
            switch (actId)
            {
                case 1:
                    MossyRuins(look);
                    break;
                case 2:
                    SunkenCrypt(look);
                    break;
                case 3:
                    CrystalHollow(look);
                    break;
            }
        }

        /// <summary>Cozy golden hour behind the Title: the Act 1 ruins under a low, warm sun with strong god rays and glowing lanterns.</summary>
        public static void Title(ActLightingPreset look)
        {
            look.sunColor = C(1f, 0.7f, 0.42f);
            look.sunIntensity = 1.45f;
            look.sunEuler = new Vector3(30f, 112f, 0f);
            look.shadowStrength = 0.85f;
            look.ambientSky = C(0.58f, 0.46f, 0.56f);
            look.ambientEquator = C(0.52f, 0.38f, 0.32f);
            look.ambientGround = C(0.26f, 0.18f, 0.16f);
            look.fogColor = C(1f, 0.72f, 0.45f);
            look.fogDensity = 0.011f;
            look.rimColor = C(1f, 0.78f, 0.5f);
            look.additionalLightTint = C(1f, 0.72f, 0.42f);
            look.additionalLightIntensity = 2.2f;
            look.godRayColor = C(1f, 0.72f, 0.4f);
            look.godRayIntensity = 1.6f;
            look.particleTint = C(1f, 0.85f, 0.6f);
        }

        // Sunny afternoon in the ruins: a warm but not yellow sun from the upper left at 46°, long hard shadows,
        // cooler violet ambient in the shade, stone lanterns as accents only.
        static void MossyRuins(ActLightingPreset look)
        {
            look.sunColor = C(1f, 0.9f, 0.74f);
            look.sunIntensity = 1.3f;
            look.sunEuler = new Vector3(46f, 108f, 0f);
            look.shadowStrength = 0.92f;
            look.ambientSky = C(0.46f, 0.5f, 0.72f);
            look.ambientEquator = C(0.38f, 0.34f, 0.46f);
            look.ambientGround = C(0.18f, 0.16f, 0.2f);
            look.fogColor = C(0.86f, 0.76f, 0.6f);
            look.fogDensity = 0.006f;
            look.rimColor = C(1f, 0.88f, 0.66f);
            look.additionalLightTint = C(1f, 0.8f, 0.5f);
            look.additionalLightIntensity = 1.2f;
            look.godRayColor = C(1f, 0.86f, 0.6f);
            look.godRayIntensity = 0.7f;
        }

        // Moonlit crypt: a cool, near-neutral key from the upper right at 55° (the act grade adds the blue), dark
        // ambient so the torch and brazier pools stay warm accents instead of a haze.
        static void SunkenCrypt(ActLightingPreset look)
        {
            look.sunColor = C(0.76f, 0.82f, 1f);
            look.sunIntensity = 1.05f;
            look.sunEuler = new Vector3(55f, -112f, 0f);
            look.shadowStrength = 0.9f;
            look.ambientSky = C(0.28f, 0.32f, 0.5f);
            look.ambientEquator = C(0.2f, 0.21f, 0.3f);
            look.ambientGround = C(0.08f, 0.08f, 0.12f);
            look.fogColor = C(0.07f, 0.09f, 0.17f);
            look.fogDensity = 0.009f;
            look.rimColor = C(0.6f, 0.72f, 1f);
            look.additionalLightTint = C(1f, 0.7f, 0.42f);
            look.additionalLightIntensity = 2f;
            look.godRayColor = C(0.6f, 0.72f, 1f);
            look.godRayIntensity = 0.5f;
        }

        // Crystal cave: a soft violet key from ceiling cracks at 58° (was near-vertical: no readable shadows),
        // desaturated enough that enemy colours survive; the cyan / violet crystals glow but do not flood the table.
        static void CrystalHollow(ActLightingPreset look)
        {
            look.sunColor = C(0.86f, 0.8f, 1f);
            look.sunIntensity = 1.15f;
            look.sunEuler = new Vector3(58f, 120f, 0f);
            look.shadowStrength = 0.9f;
            look.ambientSky = C(0.36f, 0.32f, 0.5f);
            look.ambientEquator = C(0.24f, 0.22f, 0.34f);
            look.ambientGround = C(0.1f, 0.08f, 0.14f);
            look.fogColor = C(0.18f, 0.12f, 0.3f);
            look.fogDensity = 0.008f;
            look.rimColor = C(0.6f, 1f, 1f);
            look.additionalLightTint = C(0.8f, 0.95f, 1f);
            look.additionalLightIntensity = 1.7f;
            look.godRayColor = C(0.9f, 0.9f, 1f);
            look.godRayIntensity = 0.6f;
        }
    }
}
