#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Nex.BilliardRogue.Editor.Tests
{
    // Guards the Presentation-World outputs: layouts.json parsing, the generated Arena / Env_Act / WorldLighting
    // prefabs, and ActEnvironmentController applying acts (run in a preview scene with overridden lighting settings,
    // so the open scene is never touched).
    public class EnvironmentBuilderTests
    {
        const string ArenaConfigPath = BuilderAssets.ConfigRoot + "/ArenaConfig.asset";
        const string ActPathFormat = BuilderAssets.ConfigRoot + "/Acts/Act_{0}.asset";

        [Test]
        public void LayoutsJsonHasThreeDressedActs()
        {
            var layout = EnvironmentLayout.Load(out var error);
            Assert.IsNotNull(layout, error);
            for (var id = 1; id <= 3; id++)
            {
                var act = layout!.Act(id);
                Assert.IsNotNull(act, $"act {id}");
                Assert.Greater(act!.props.Length, 50, $"act {id} props");
                Assert.IsTrue(act.lighting.preset.IsValid, $"act {id} preset");
                Assert.Greater(act.ground.tiles.Length, 0, $"act {id} ground");
                Assert.IsNotEmpty(act.SurfaceFor(EnvironmentArenaBuilder.FloorModel, "Top_Surface"), $"act {id} floor surface");
            }
        }

        [Test]
        public void ArenaPrefabMatchesArenaConfig()
        {
            var arena = LoadOrIgnore<GameObject>(EnvironmentArenaBuilder.ArenaPath);
            var config = LoadOrIgnore<ArenaConfig>(ArenaConfigPath);
            var rules = config.Rules;
            Assert.IsNotNull(arena.GetComponent<ArenaLayout>());
            var view = arena.GetComponent<ArenaView>();
            Assert.IsNotNull(view);
            Assert.AreSame(arena.GetComponent<ArenaLayout>(), view.Layout);
            Assert.AreEqual(rules.columns * (rules.rows - 1), arena.transform.Find("Floor").childCount);
            Assert.AreEqual(rules.columns, arena.transform.Find("DangerRow").childCount);
            Assert.IsFalse(view.DangerRow.enabled, "DangerRowPulse must start disabled until Initialize");

            var surfaces = new SerializedObject(view).FindProperty("surfaces");
            Assert.AreEqual(System.Enum.GetValues(typeof(ArenaSurface)).Length, surfaces.arraySize);
            for (var i = 0; i < surfaces.arraySize; i++)
            {
                Assert.Greater(surfaces.GetArrayElementAtIndex(i).FindPropertyRelative("renderers").arraySize, 0, $"surface slot {i}");
            }
        }

        [Test]
        public void ActPrefabsRespectTheLightBudget()
        {
            for (var id = 1; id <= 3; id++)
            {
                var prefab = LoadOrIgnore<GameObject>(string.Format(EnvironmentActBuilder.PathFormat, id));
                var environment = prefab.GetComponent<ActEnvironment>();
                Assert.AreEqual(id - 1, environment.ActIndex);
                var lights = prefab.GetComponentsInChildren<Light>(true);
                Assert.LessOrEqual(lights.Length, EnvironmentActBuilder.MaxLightsPerAct, $"act {id} lights");
                Assert.AreEqual(lights.Length, environment.LightCount, $"act {id} animated lights");
                foreach (var light in lights)
                {
                    Assert.AreEqual(LightType.Point, light.type);
                    Assert.AreEqual(LightShadows.None, light.shadows);
                }

                Assert.AreEqual(Vector3.zero, environment.AmbientAnchor.localPosition);
                Assert.AreEqual(System.Enum.GetValues(typeof(ArenaSurface)).Length, environment.ArenaSurfaces.Count);
                Assert.IsNotNull(environment.ArenaSurfaces[ArenaSurface.FloorTop], $"act {id} floor surface");
                Assert.IsFalse(prefab.GetComponent<DioramaAnimator>().enabled, "DioramaAnimator must start disabled until Initialize");
            }
        }

        [Test]
        public void ControllerAppliesActsAndTitle()
        {
            var arenaPrefab = LoadOrIgnore<GameObject>(EnvironmentArenaBuilder.ArenaPath);
            var lightingPrefab = LoadOrIgnore<GameObject>(EnvironmentBuilder.WorldLightingPath);
            var config = LoadOrIgnore<ArenaConfig>(ArenaConfigPath);
            var acts = new ActDefinition[3];
            for (var i = 0; i < acts.Length; i++)
            {
                acts[i] = LoadOrIgnore<ActDefinition>(string.Format(ActPathFormat, i + 1));
            }

            var scene = EditorSceneManager.NewPreviewScene();
            Unsupported.SetOverrideLightingSettings(scene);
            try
            {
                var world = new GameObject("World");
                SceneManager.MoveGameObjectToScene(world, scene);
                var arena = ((GameObject)PrefabUtility.InstantiatePrefab(arenaPrefab, world.transform)).GetComponent<ArenaView>();
                var controller = ((GameObject)PrefabUtility.InstantiatePrefab(lightingPrefab, world.transform)).GetComponent<ActEnvironmentController>();
                var rigVolume = StandInRig(world.transform);
                Assert.IsTrue(EnvironmentBuilder.WireScene(world));

                controller.Initialize(config);
                Assert.AreEqual(ActEnvironmentController.NoActIndex, controller.CurrentActIndex);
                Assert.IsTrue(arena.DangerRow.enabled);

                controller.ApplyTitle(acts[0], instant: true);
                Assert.AreEqual(ActEnvironmentController.TitleActIndex, controller.CurrentActIndex);

                for (var i = 0; i < acts.Length; i++)
                {
                    controller.ApplyAct(acts[i], instant: true);
                    Assert.AreEqual(i, controller.CurrentActIndex);
                    Assert.IsFalse(controller.IsTransitioning);
                    var lighting = acts[i].Lighting;
                    Assert.AreEqual(lighting.fogDensity, RenderSettings.fogDensity, 1e-4f);
                    Assert.AreEqual(AmbientMode.Trilight, RenderSettings.ambientMode);
                    Assert.AreEqual(lighting.ambientSky, RenderSettings.ambientSkyColor);
                    Assert.AreEqual(lighting.sunIntensity, RenderSettings.sun.intensity, 1e-4f);
                    if (acts[i].VolumeProfile != null) Assert.AreSame(acts[i].VolumeProfile, rigVolume.sharedProfile);

                    var visible = 0;
                    foreach (var environment in world.GetComponentsInChildren<ActEnvironment>(true))
                    {
                        if (!environment.gameObject.activeSelf) continue;
                        visible++;
                        Assert.AreEqual(i, environment.ActIndex);
                    }

                    Assert.AreEqual(1, visible, $"act {i + 1}: exactly one diorama visible");
                }
            }
            finally
            {
                Unsupported.RestoreOverrideLightingSettings();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static Volume StandInRig(Transform parent)
        {
            var go = new GameObject("StandInRig");
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<WorldCameraRig>();
            var volume = new GameObject("WorldVolume").AddComponent<Volume>();
            volume.transform.SetParent(go.transform, false);
            volume.isGlobal = true;
            var so = new SerializedObject(rig);
            so.FindProperty("worldVolume").objectReferenceValue = volume;
            so.ApplyModifiedPropertiesWithoutUndo();
            return volume;
        }

        static T LoadOrIgnore<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Assert.Ignore($"{path} not built (run EnvironmentBuilder)");
            return asset!;
        }
    }
}
