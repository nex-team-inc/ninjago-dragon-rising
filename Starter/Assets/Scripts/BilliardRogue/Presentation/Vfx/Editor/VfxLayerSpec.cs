#nullable enable

using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// How a VFX layer is shaded (TDD §16). Lit: LitParticle lit by the scene (debris, smoke, leaves). Emissive: LitParticle
    /// unlit with an emission boost, opaque pixels that keep their hue on any floor and bloom through the HDR channel (sparks,
    /// stars, embers, rings). Glow: additive GlowParticle for light-like flashes and soft motes.
    /// </summary>
    public enum VfxShading
    {
        Lit,
        Emissive,
        Glow,
    }

    /// <summary>Emission shape of a VFX layer; hemisphere and cone point up (+Y), the circle lies flat on XZ.</summary>
    public enum VfxShape
    {
        Point,
        Sphere,
        Hemisphere,
        Circle,
        Box,
        Cone,
    }

    /// <summary>
    /// One ParticleSystem of a VFX prefab, described as data so VfxParticleFactory can rebuild every prefab from code.
    /// Ranges are (min, max); sizes and distances are metres (1 cell = 1 m); times are seconds of scaled time.
    /// </summary>
    public sealed class VfxLayerSpec
    {
        #region Data

        public readonly string name;
        public readonly string sheet;
        public readonly VfxShading shading;

        public float delay;
        public Vector2 lifetime = new(0.5f, 0.5f);
        public Vector2Int burst = new(1, 1);
        public float rate;
        public int maxAlive;
        public float loopDuration;

        public Vector2 speed;
        public float gravity;
        public float drag;
        public bool hasDrift;
        public Vector3 driftMin;
        public Vector3 driftMax;
        public float noiseStrength;
        public float noiseFrequency = 0.4f;

        public VfxShape shape = VfxShape.Point;
        public float radius;
        public float radiusThickness = 1f;
        public float coneAngle = 25f;
        public Vector3 boxSize = Vector3.one;
        public Vector3 offset;

        public Vector2 size = new(0.57f, 0.57f);
        public Color colorA = Color.white;
        public Color colorB = Color.white;
        public Color? endColor;
        public bool fadeOut;
        public AnimationCurve? sizeOverLife;
        public Vector2Int frames;
        public ParticleSystemRenderMode renderMode = ParticleSystemRenderMode.Billboard;

        public bool bounce;
        public float bounciness = 0.35f;
        public float bounceDampen = 0.3f;

        #endregion

        #region Construction

        VfxLayerSpec(string name, string sheet, VfxShading shading)
        {
            this.name = name;
            this.sheet = sheet;
            this.shading = shading;
        }

        public static VfxLayerSpec Lit(string name, string sheet) => new(name, sheet, VfxShading.Lit);

        public static VfxLayerSpec Emissive(string name, string sheet) => new(name, sheet, VfxShading.Emissive);

        public static VfxLayerSpec Glow(string name, string sheet) => new(name, sheet, VfxShading.Glow);

        #endregion

        #region Fluent setters

        public VfxLayerSpec Delay(float seconds)
        {
            delay = seconds;
            return this;
        }

        public VfxLayerSpec Life(float min, float max)
        {
            lifetime = new Vector2(min, max);
            return this;
        }

        public VfxLayerSpec Life(float seconds) => Life(seconds, seconds);

        public VfxLayerSpec Burst(int min, int max)
        {
            burst = new Vector2Int(min, max);
            return this;
        }

        public VfxLayerSpec Burst(int count) => Burst(count, count);

        /// <summary>Continuous looping emission (ambient): rate per second, alive cap and the loop length.</summary>
        public VfxLayerSpec Stream(float perSecond, int alive, float loopSeconds)
        {
            rate = perSecond;
            maxAlive = alive;
            loopDuration = loopSeconds;
            burst = Vector2Int.zero;
            return this;
        }

        public VfxLayerSpec Speed(float min, float max)
        {
            speed = new Vector2(min, max);
            return this;
        }

        /// <summary>Multiplier of Physics.gravity; negative values make particles rise.</summary>
        public VfxLayerSpec Gravity(float modifier)
        {
            gravity = modifier;
            return this;
        }

        public VfxLayerSpec Drag(float amount)
        {
            drag = amount;
            return this;
        }

        public VfxLayerSpec Drift(Vector3 min, Vector3 max)
        {
            hasDrift = true;
            driftMin = min;
            driftMax = max;
            return this;
        }

        public VfxLayerSpec Noise(float strength, float frequency)
        {
            noiseStrength = strength;
            noiseFrequency = frequency;
            return this;
        }

        public VfxLayerSpec Sphere(float r, float thickness = 1f) => Shape(VfxShape.Sphere, r, thickness);

        public VfxLayerSpec Hemisphere(float r, float thickness = 1f) => Shape(VfxShape.Hemisphere, r, thickness);

        public VfxLayerSpec Circle(float r, float thickness = 1f) => Shape(VfxShape.Circle, r, thickness);

        public VfxLayerSpec Cone(float angle, float r)
        {
            coneAngle = angle;
            return Shape(VfxShape.Cone, r, 1f);
        }

        public VfxLayerSpec Box(Vector3 extents, Vector3 center)
        {
            boxSize = extents;
            offset = center;
            shape = VfxShape.Box;
            return this;
        }

        public VfxLayerSpec Offset(Vector3 localOffset)
        {
            offset = localOffset;
            return this;
        }

        public VfxLayerSpec Size(float min, float max)
        {
            size = new Vector2(min, max);
            return this;
        }

        public VfxLayerSpec Size(float metres) => Size(metres, metres);

        public VfxLayerSpec Colors(Color a, Color b)
        {
            colorA = a;
            colorB = b;
            return this;
        }

        public VfxLayerSpec Colors(Color color) => Colors(color, color);

        /// <summary>Tints toward end over the lifetime (multiplies the start colour).</summary>
        public VfxLayerSpec TintTo(Color end)
        {
            endColor = end;
            return this;
        }

        /// <summary>Additive glows fade out over the last part of their life; alpha-clipped layers ignore it (they would pop early).</summary>
        public VfxLayerSpec Fade()
        {
            fadeOut = true;
            return this;
        }

        public VfxLayerSpec SizeOverLife(AnimationCurve curve)
        {
            sizeOverLife = curve;
            return this;
        }

        /// <summary>Random start frame range (inclusive frame indices of the flipbook).</summary>
        public VfxLayerSpec Frames(int first, int last)
        {
            frames = new Vector2Int(first, last);
            return this;
        }

        public VfxLayerSpec Flat()
        {
            renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            return this;
        }

        public VfxLayerSpec Bounce(float bounceFactor, float dampen)
        {
            bounce = true;
            bounciness = bounceFactor;
            bounceDampen = dampen;
            return this;
        }

        #endregion

        #region Derived

        public bool IsStream => rate > 0f;

        public float LatestEnd => delay + lifetime.y;

        public int ParticleBudget => IsStream ? maxAlive : Mathf.Max(1, burst.y);

        VfxLayerSpec Shape(VfxShape type, float r, float thickness)
        {
            shape = type;
            radius = r;
            radiusThickness = thickness;
            return this;
        }

        #endregion
    }
}
