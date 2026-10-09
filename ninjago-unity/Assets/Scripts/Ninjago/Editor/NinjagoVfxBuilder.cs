#nullable enable

using Nex.Util;
using UnityEditor;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;

namespace Nex.Ninjago.Editor
{
    /// <summary>Particle prefabs for every Ninjago effect, registered in the VfxManager prefab (it pools them).</summary>
    public static class NinjagoVfxBuilder
    {
        const string VfxRoot = PrefabsRoot + "/Vfx";

        #region Entry Point

        public static void Build()
        {
            var soft = NinjagoAssetsBuilder.Particle("Particle Soft", true);
            var brick = NinjagoAssetsBuilder.Particle("Particle Brick", false);
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            var dust = Save("Vfx_NinjaDustKick", BuildDust(soft));
            var brickPop = Save("Vfx_NinjaBrickPop", BuildBrickPop(brick, cube));
            var swirl = Save("Vfx_SpinjitzuSwirl", BuildSwirl(soft));
            var burst = Save("Vfx_SpinjitzuBurst", BuildBurst(soft));
            var spray = Save("Vfx_BruteBrickSpray", BuildBrickSpray("Vfx_BruteBrickSpray", brick, cube, new Color(0.62f, 0.63f, 0.66f), 16, -1f));
            var bump = Save("Vfx_VehicleBump", BuildBrickSpray("Vfx_VehicleBump", brick, cube, new Color(0.9f, 0.35f, 0.2f), 14, 1f));

            NinjagoAssetsBuilder.EditPrefab<VfxManager>("Assets/Prefabs/Singletons/VfxManager.prefab", "effectSpecs", dict =>
            {
                Register(dict, VfxManager.VisualEffect.NinjaDustKick, dust, 2, 4);
                Register(dict, VfxManager.VisualEffect.NinjaBrickPop, brickPop, 2, 4);
                Register(dict, VfxManager.VisualEffect.SpinjitzuSwirl, swirl, 2, 4);
                Register(dict, VfxManager.VisualEffect.SpinjitzuBurst, burst, 2, 4);
                Register(dict, VfxManager.VisualEffect.BruteBrickSpray, spray, 2, 4);
                Register(dict, VfxManager.VisualEffect.VehicleBump, bump, 2, 4);
            });
        }

        static void Register(SerializedProperty dict, VfxManager.VisualEffect effect, ParticleSystem prefab, int defaultSize, int maxSize)
        {
            var spec = EnumDictionaryEditorUtils.GetValueProperty(dict, (int)effect);
            spec.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            spec.FindPropertyRelative("defaultPoolSize").intValue = defaultSize;
            spec.FindPropertyRelative("maxPoolSize").intValue = maxSize;
        }

        static ParticleSystem Save(string name, ParticleSystem system)
        {
            return SavePrefab(system.gameObject, $"{VfxRoot}/{name}.prefab").GetComponent<ParticleSystem>();
        }

        #endregion

        #region Effects

        static ParticleSystem BuildDust(Material material)
        {
            var system = NewSystem("Vfx_NinjaDustKick", material, 0.6f, false, 40);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startColor = new Color(0.86f, 0.78f, 0.62f, 0.85f);
            main.gravityModifier = 0.3f;
            Burst(system, 22);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            FadeOut(system);
            GrowOverLife(system, 1.8f);
            return system;
        }

        // Bricks fly out and snap back: radial velocity starts outward and turns inward over their life.
        static ParticleSystem BuildBrickPop(Material material, Mesh cube)
        {
            var system = NewSystem("Vfx_NinjaBrickPop", material, 0.8f, false, 16);
            var main = system.main;
            main.startLifetime = 0.7f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.24f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Burst(system, 8);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.radial = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 7f), new Keyframe(0.45f, 0f), new Keyframe(1f, -10f)));
            var rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
            MeshRenderer(system, cube);
            return system;
        }

        static ParticleSystem BuildSwirl(Material material)
        {
            var system = NewSystem("Vfx_SpinjitzuSwirl", material, 1f, true, 220);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            var emission = system.emission;
            emission.rateOverTime = 6f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.9f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Orbit(system, 9f, -0.4f, 2.2f);
            FadeOut(system);
            return system;
        }

        static ParticleSystem BuildBurst(Material material)
        {
            var system = NewSystem("Vfx_SpinjitzuBurst", material, 0.8f, false, 80);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
            Burst(system, 55);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            Orbit(system, 14f, 3f, 3.2f);
            FadeOut(system);
            return system;
        }

        // Grey bricks off the brute toward the ninja (forwardZ -1), or orange bricks off a barrier (forwardZ +1).
        static ParticleSystem BuildBrickSpray(string name, Material material, Mesh cube, Color color, int count, float forwardZ)
        {
            var system = NewSystem(name, material, 1.4f, false, 32);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            main.startColor = color;
            main.gravityModifier = 1.4f;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            Burst(system, count);
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f;
            shape.radius = 0.4f;
            shape.rotation = new Vector3(-35f, forwardZ < 0f ? 180f : 0f, 0f);
            var rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            MeshRenderer(system, cube);
            return system;
        }

        #endregion

        #region Helpers

        static ParticleSystem NewSystem(string name, Material material, float duration, bool loop, int maxParticles)
        {
            var go = new GameObject(name);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = duration;
            main.loop = loop;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        static void Burst(ParticleSystem system, int count)
        {
            var emission = system.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        static void Orbit(ParticleSystem system, float orbitalY, float radial, float rise)
        {
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.orbitalY = orbitalY;
            velocity.radial = radial;
            velocity.y = rise;
        }

        static void FadeOut(ParticleSystem system)
        {
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
        }

        static void GrowOverLife(ParticleSystem system, float endScale)
        {
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, endScale));
        }

        static void MeshRenderer(ParticleSystem system, Mesh mesh)
        {
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        #endregion
    }
}
