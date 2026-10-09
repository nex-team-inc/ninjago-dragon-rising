#nullable enable

using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds Assets/Scenes/BilliardRogue/Tests/VfxGallery.unity for visual review: every burst prefab on a labelled grid
    /// (instances overridden to loop and play on awake), Act 1 ambient active (Act 2/3 inactive, toggle to compare),
    /// a warm sun plus a torch light for the lit sprites, and the world camera's pitch/FOV. Press Play to watch.
    /// The scene is created additively, saved and closed, so the Editor's open scene is never touched.
    /// </summary>
    public static class VfxGalleryBuilder
    {
        public const string ScenePath = "Assets/Scenes/BilliardRogue/Tests/VfxGallery.unity";
        const string FloorMaterialPath = "Assets/Materials/BilliardRogue/M_Surface_StoneFloor.mat";
        const string VolumePath = "Assets/Settings/BilliardRogue/Volumes/Volume_Act1.asset";
        const string LabelFontPath = "Assets/Fonts/BilliardRogue/BilliardPixel_TMP.asset";
        const int Columns = 6;
        const float Spacing = 3f;
        const float CentreHeight = 0.45f;
        const float GroundHeight = 0.02f;
        const float LoopSeconds = 2.5f;
        const float CameraPitch = 58f;
        const float CameraFov = 28f;
        const float CameraDistance = 26f;

        public static string Build(IReadOnlyList<VfxRecipe> recipes, List<string> warnings)
        {
            BuilderAssets.EnsureFolder("Assets/Scenes/BilliardRogue/Tests");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var rows = Populate(recipes, warnings);
                AddStage(rows, warnings);
                EditorSceneManager.SaveScene(scene, ScenePath);
                return ScenePath;
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        #region Content

        static int Populate(IReadOnlyList<VfxRecipe> recipes, List<string> warnings)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LabelFontPath);
            var effects = new GameObject("Effects").transform;
            var ambient = new GameObject("Ambient").transform;
            var index = 0;
            var act = 0;
            foreach (var recipe in recipes)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VfxPrefabWriter.PathOf(recipe));
                if (prefab == null)
                {
                    warnings.Add($"Gallery: {recipe.prefabName}.prefab missing.");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, recipe.IsAmbient ? ambient : effects);
                if (recipe.IsAmbient)
                {
                    instance.SetActive(act++ == 0);
                    continue;
                }

                var grounded = recipe.effect == VfxManager.VisualEffect.DustPuff;
                var cell = new Vector3((index % Columns - (Columns - 1) * 0.5f) * Spacing, 0f, (index / Columns) * Spacing + 1f);
                instance.transform.position = cell + Vector3.up * (grounded ? GroundHeight : CentreHeight);
                MakeLooping(instance);
                AddLabel(recipe.prefabName.Replace("Vfx_", ""), cell, effects, font);
                index++;
            }

            return (index + Columns - 1) / Columns;
        }

        static void MakeLooping(GameObject instance)
        {
            foreach (var system in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                main.loop = true;
                main.playOnAwake = true;
                main.duration = LoopSeconds;
                main.stopAction = ParticleSystemStopAction.None;
                PrefabUtility.RecordPrefabInstancePropertyModifications(system);
            }
        }

        static void AddLabel(string text, Vector3 cell, Transform parent, TMP_FontAsset? font)
        {
            var label = new GameObject("Label_" + text, typeof(TextMeshPro));
            label.transform.SetParent(parent, false);
            label.transform.SetPositionAndRotation(cell + new Vector3(0f, 0.05f, -1.1f), Quaternion.Euler(CameraPitch, 0f, 0f));
            var tmp = label.GetComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = 4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(3f, 0.6f);
        }

        #endregion

        #region Stage

        static void AddStage(int rows, List<string> warnings)
        {
            var depth = Mathf.Max(1, rows) * Spacing;
            var centre = new Vector3(0f, 0f, depth * 0.5f);

            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.92f, 0.8f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var torch = new GameObject("TorchLight", typeof(Light)).GetComponent<Light>();
            torch.type = LightType.Point;
            torch.color = new Color(1f, 0.6f, 0.3f);
            torch.intensity = 2f;
            torch.range = 6f;
            torch.transform.position = new Vector3(-Columns * Spacing * 0.5f, 1.4f, 1f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.transform.position = centre;
            floor.transform.localScale = new Vector3(Columns * Spacing / 10f + 1f, 1f, depth / 10f + 1f);
            var floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            if (floorMaterial != null) floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
            else warnings.Add("Gallery: M_Surface_StoneFloor.mat missing (MaterialsBuilder); the floor keeps the default material.");

            var camera = new GameObject("GalleryCamera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = CameraFov;
            camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.18f);
            var rotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            camera.transform.SetPositionAndRotation(centre - rotation * Vector3.forward * CameraDistance, rotation);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                warnings.Add("Gallery: Volume_Act1.asset missing (Rendering); no bloom in the gallery until it exists.");
                return;
            }

            var volume = new GameObject("Volume", typeof(Volume)).GetComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        #endregion
    }
}
