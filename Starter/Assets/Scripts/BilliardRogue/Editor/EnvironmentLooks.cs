#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Unity calibration of the act looks, applied by EnvironmentBuilder when it seeds a lighting preset (once per
    /// preset, see ActLightingPreset.seededFromLayout). layouts.json carries the art direction from the Blender preview
    /// (sun angles, shaft and particle tints); these values re-balance exposure and colour for BilliardRogue/ToonLit
    /// under the act's Volume grade, tuned from edit-mode renders at the ArenaConfig camera pose: the grid keeps a
    /// mid value in every act, night acts stay near-neutral so their grade carries the mood, and the act lights are
    /// scaled until their pools read against the sun.
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
            look.sunColor = C(1f, 0.64f, 0.36f);
            look.sunIntensity = 1.55f;
            look.sunEuler = new Vector3(30f, 112f, 0f);
            look.shadowStrength = 0.8f;
            look.ambientSky = C(0.62f, 0.48f, 0.56f);
            look.ambientEquator = C(0.58f, 0.4f, 0.32f);
            look.ambientGround = C(0.3f, 0.2f, 0.16f);
            look.fogColor = C(1f, 0.7f, 0.42f);
            look.fogDensity = 0.012f;
            look.rimColor = C(1f, 0.78f, 0.5f);
            look.additionalLightTint = C(1f, 0.72f, 0.42f);
            look.additionalLightIntensity = 2.5f;
            look.godRayColor = C(1f, 0.7f, 0.36f);
            look.godRayIntensity = 2f;
            look.particleTint = C(1f, 0.85f, 0.6f);
        }

        // Sunny afternoon: the layout look holds; only the stone lanterns need more punch against the sun.
        static void MossyRuins(ActLightingPreset look)
        {
            look.additionalLightIntensity = 1.5f;
        }

        // Moonlit crypt: near-neutral cool light (the act grade adds the blue), warm torch and brazier pools.
        static void SunkenCrypt(ActLightingPreset look)
        {
            look.sunColor = C(0.72f, 0.8f, 1f);
            look.sunIntensity = 1.1f;
            look.shadowStrength = 0.85f;
            look.ambientSky = C(0.34f, 0.38f, 0.54f);
            look.ambientEquator = C(0.25f, 0.25f, 0.33f);
            look.ambientGround = C(0.1f, 0.1f, 0.13f);
            look.fogColor = C(0.08f, 0.1f, 0.18f);
            look.fogDensity = 0.012f;
            look.rimColor = C(0.55f, 0.68f, 1f);
            look.additionalLightTint = C(1f, 0.72f, 0.45f);
            look.additionalLightIntensity = 3f;
            look.godRayColor = C(0.6f, 0.72f, 1f);
            look.godRayIntensity = 0.7f;
        }

        // Crystal cave: soft violet key, desaturated enough that enemy colours survive; cyan / violet crystal pools.
        static void CrystalHollow(ActLightingPreset look)
        {
            look.sunColor = C(0.82f, 0.76f, 1f);
            look.sunIntensity = 1f;
            look.ambientSky = C(0.44f, 0.38f, 0.58f);
            look.ambientEquator = C(0.3f, 0.27f, 0.4f);
            look.ambientGround = C(0.13f, 0.1f, 0.17f);
            look.fogColor = C(0.2f, 0.13f, 0.32f);
            look.fogDensity = 0.012f;
            look.additionalLightTint = C(0.8f, 0.95f, 1f);
            look.additionalLightIntensity = 2.5f;
            look.godRayIntensity = 1f;
        }
    }
}
