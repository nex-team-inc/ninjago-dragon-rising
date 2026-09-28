#nullable enable

using UnityEngine;
using UnityEngine.Rendering;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Writes a VfxLayerSpec into a ParticleSystem. Every module is set explicitly (unused ones disabled) so a rebuild
    /// over an existing prefab is deterministic. Pool contract (research/audio-vfx-analytics.md §4.1): world space,
    /// no play on awake, root stopAction Callback, children None, AlwaysSimulate so an off-screen burst still returns.
    /// </summary>
    public static class VfxParticleFactory
    {
        const float MaxScreenFraction = 0.5f;
        const float NoiseScrollSpeed = 0.25f;

        #region Public Methods

        public static void Configure(ParticleSystem system, VfxLayerSpec spec, VfxSheet sheet, Material material, Transform? floorPlane, bool isRoot, bool ambient)
        {
            ConfigureMain(system, spec, isRoot, ambient);
            ConfigureEmission(system, spec);
            ConfigureShape(system, spec);
            ConfigureMotion(system, spec);
            ConfigureLook(system, spec, sheet);
            ConfigureCollision(system, spec, floorPlane);
            DisableUnused(system);
            ConfigureRenderer(system.GetComponent<ParticleSystemRenderer>(), spec, material);
        }

        /// <summary>Root duration covers every child so VfxManager's stop callback never hides a child mid-flight.</summary>
        public static void FitRootDuration(ParticleSystem root, float latestEnd)
        {
            var main = root.main;
            main.duration = Mathf.Max(0.05f, latestEnd);
        }

        #endregion

        #region Modules

        static void ConfigureMain(ParticleSystem system, VfxLayerSpec spec, bool isRoot, bool ambient)
        {
            var main = system.main;
            main.duration = spec.IsStream ? Mathf.Max(0.5f, spec.loopDuration) : Mathf.Max(0.05f, spec.lifetime.y);
            main.loop = ambient;
            main.prewarm = ambient;
            main.playOnAwake = ambient;
            main.startDelay = spec.delay;
            main.startLifetime = Range(spec.lifetime);
            main.startSpeed = Range(spec.speed);
            main.startSize3D = false;
            main.startSize = Range(spec.size);
            main.startRotation3D = false;
            main.startRotation = 0f;
            main.flipRotation = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(spec.colorA, spec.colorB);
            main.gravityModifier = spec.gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.simulationSpeed = 1f;
            main.useUnscaledTime = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Transform;
            main.maxParticles = spec.ParticleBudget;
            main.stopAction = isRoot && !ambient ? ParticleSystemStopAction.Callback : ParticleSystemStopAction.None;
            main.cullingMode = ambient ? ParticleSystemCullingMode.Automatic : ParticleSystemCullingMode.AlwaysSimulate;
            main.ringBufferMode = ParticleSystemRingBufferMode.Disabled;
            system.useAutoRandomSeed = true;
        }

        static void ConfigureEmission(ParticleSystem system, VfxLayerSpec spec)
        {
            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = spec.rate;
            emission.rateOverDistance = 0f;
            if (spec.IsStream)
            {
                emission.SetBursts(System.Array.Empty<ParticleSystem.Burst>());
                return;
            }

            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)spec.burst.x, (short)spec.burst.y) });
        }

        static void ConfigureShape(ParticleSystem system, VfxLayerSpec spec)
        {
            var shape = system.shape;
            shape.enabled = true;
            shape.position = spec.offset;
            shape.rotation = Vector3.zero;
            shape.scale = Vector3.one;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
            shape.randomDirectionAmount = 0f;
            shape.sphericalDirectionAmount = 0f;
            shape.alignToDirection = false;
            shape.radiusThickness = spec.radiusThickness;
            switch (spec.shape)
            {
                case VfxShape.Point:
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.01f;
                    break;
                case VfxShape.Sphere:
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = spec.radius;
                    break;
                case VfxShape.Hemisphere:
                    shape.shapeType = ParticleSystemShapeType.Hemisphere;
                    shape.radius = spec.radius;
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    break;
                case VfxShape.Circle:
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = spec.radius;
                    shape.rotation = new Vector3(90f, 0f, 0f);
                    break;
                case VfxShape.Box:
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.boxThickness = Vector3.zero;
                    shape.scale = spec.boxSize;
                    break;
                case VfxShape.Cone:
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = spec.coneAngle;
                    shape.radius = Mathf.Max(0.01f, spec.radius);
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    break;
            }
        }

        static void ConfigureMotion(ParticleSystem system, VfxLayerSpec spec)
        {
            var velocity = system.velocityOverLifetime;
            velocity.enabled = spec.hasDrift;
            if (spec.hasDrift)
            {
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(spec.driftMin.x, spec.driftMax.x);
                velocity.y = new ParticleSystem.MinMaxCurve(spec.driftMin.y, spec.driftMax.y);
                velocity.z = new ParticleSystem.MinMaxCurve(spec.driftMin.z, spec.driftMax.z);
            }

            var limit = system.limitVelocityOverLifetime;
            limit.enabled = spec.drag > 0f;
            limit.separateAxes = false;
            limit.limit = 100f;
            limit.dampen = 0f;
            limit.drag = spec.drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;

            var noise = system.noise;
            noise.enabled = spec.noiseStrength > 0f;
            if (!noise.enabled) return;
            noise.separateAxes = false;
            noise.strength = spec.noiseStrength;
            noise.frequency = spec.noiseFrequency;
            noise.scrollSpeed = NoiseScrollSpeed;
            noise.damping = true;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.positionAmount = 1f;
            noise.rotationAmount = 0f;
            noise.sizeAmount = 0f;
        }

        static void ConfigureLook(ParticleSystem system, VfxLayerSpec spec, VfxSheet sheet)
        {
            var color = system.colorOverLifetime;
            color.enabled = spec.fadeOut || spec.endColor.HasValue;
            if (color.enabled) color.color = LifetimeGradient(spec);

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = spec.sizeOverLife != null;
            if (spec.sizeOverLife != null)
            {
                sizeOverLifetime.separateAxes = false;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, spec.sizeOverLife);
            }

            var sheetAnimation = system.textureSheetAnimation;
            var tiles = sheet.columns * sheet.rows;
            sheetAnimation.enabled = sheet.texture != null && tiles > 1;
            if (!sheetAnimation.enabled) return;
            sheetAnimation.mode = ParticleSystemAnimationMode.Grid;
            sheetAnimation.numTilesX = sheet.columns;
            sheetAnimation.numTilesY = sheet.rows;
            sheetAnimation.animation = ParticleSystemAnimationType.WholeSheet;
            sheetAnimation.timeMode = ParticleSystemAnimationTimeMode.FPS;
            sheetAnimation.fps = sheet.fps;
            sheetAnimation.cycleCount = 1;
            sheetAnimation.uvChannelMask = UVChannelFlags.UV0;
            // startFrame is a normalized sheet position; frames outside the strip are clamped.
            var first = Mathf.Clamp(spec.frames.x, 0, tiles - 1) / (float)tiles;
            var last = Mathf.Clamp(spec.frames.y, spec.frames.x, tiles - 1) / (float)tiles;
            sheetAnimation.startFrame = new ParticleSystem.MinMaxCurve(first, last + 0.5f / tiles);
        }

        static void ConfigureCollision(ParticleSystem system, VfxLayerSpec spec, Transform? floorPlane)
        {
            var collision = system.collision;
            collision.enabled = spec.bounce && floorPlane != null;
            if (!collision.enabled) return;
            collision.type = ParticleSystemCollisionType.Planes;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.SetPlane(0, floorPlane);
            collision.bounce = spec.bounciness;
            collision.dampen = spec.bounceDampen;
            collision.lifetimeLoss = 0f;
            collision.minKillSpeed = 0f;
            collision.radiusScale = 1f;
            collision.enableDynamicColliders = false;
            collision.sendCollisionMessages = false;
        }

        static void DisableUnused(ParticleSystem system)
        {
            var inheritVelocity = system.inheritVelocity;
            inheritVelocity.enabled = false;
            var lifetimeByEmitterSpeed = system.lifetimeByEmitterSpeed;
            lifetimeByEmitterSpeed.enabled = false;
            var force = system.forceOverLifetime;
            force.enabled = false;
            var colorBySpeed = system.colorBySpeed;
            colorBySpeed.enabled = false;
            var sizeBySpeed = system.sizeBySpeed;
            sizeBySpeed.enabled = false;
            var rotation = system.rotationOverLifetime;
            rotation.enabled = false;
            var rotationBySpeed = system.rotationBySpeed;
            rotationBySpeed.enabled = false;
            var externalForces = system.externalForces;
            externalForces.enabled = false;
            var trigger = system.trigger;
            trigger.enabled = false;
            var subEmitters = system.subEmitters;
            subEmitters.enabled = false;
            var lights = system.lights;
            lights.enabled = false;
            var trails = system.trails;
            trails.enabled = false;
            var customData = system.customData;
            customData.enabled = false;
        }

        static void ConfigureRenderer(ParticleSystemRenderer renderer, VfxLayerSpec spec, Material material)
        {
            renderer.enabled = true;
            renderer.renderMode = spec.renderMode;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material;
            renderer.trailMaterial = null;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.sortingFudge = spec.shading == VfxShading.Glow ? -1f : 0f;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = MaxScreenFraction;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowRoll = false;
            renderer.pivot = Vector3.zero;
            renderer.flip = Vector3.zero;
        }

        #endregion

        #region Helpers

        static ParticleSystem.MinMaxCurve Range(Vector2 range)
        {
            return Mathf.Approximately(range.x, range.y) ? new ParticleSystem.MinMaxCurve(range.x) : new ParticleSystem.MinMaxCurve(range.x, range.y);
        }

        static Gradient LifetimeGradient(VfxLayerSpec spec)
        {
            var end = spec.endColor ?? Color.white;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(end, 1f) },
                spec.fadeOut
                    ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        #endregion
    }
}
