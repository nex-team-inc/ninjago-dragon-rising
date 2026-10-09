#nullable enable

using System;
using System.IO;
using Nex.Util;
using TMPro;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using static Nex.Ninjago.Editor.NinjagoEditorUtils;
using Object = UnityEngine.Object;

namespace Nex.Ninjago.Editor
{
    /// <summary>Sprites, materials, tuning assets and the audio registries for Ninjago.</summary>
    public static class NinjagoAssetsBuilder
    {
        public const string FightConfigPath = ConfigsRoot + "/FightConfig.asset";
        public const string ChaseConfigPath = ConfigsRoot + "/ChaseConfig.asset";
        public const string PlayersConfigPath = ConfigsRoot + "/NinjagoPlayersConfig.asset";
        public const string OutlineMaterialPath = MaterialsRoot + "/Play Chickens Outline.mat";

        #region Sprites

        public static void BuildSprites()
        {
            EnsureFolder(SpritesRoot);
            BuildSprite("Circle", 128, (u, v) => new Vector2(u, v).magnitude <= 0.94f, Vector4.zero);
            BuildSprite("Ring", 256, (u, v) =>
            {
                var r = new Vector2(u, v).magnitude;
                return r <= 0.96f && r >= 0.74f;
            }, Vector4.zero);
            BuildSprite("Heart", 128, (u, v) =>
            {
                var x = u * 1.25f;
                var y = v * 1.25f + 0.2f;
                var a = x * x + y * y - 1f;
                return a * a * a - x * x * y * y * y <= 0f;
            }, Vector4.zero);
            BuildSprite("Arrow", 128, (u, v) =>
            {
                var shaft = u >= -0.9f && u <= 0.1f && Mathf.Abs(v) <= 0.22f;
                var head = u >= 0.05f && u <= 0.92f && Mathf.Abs(v) <= (0.92f - u) * 0.95f;
                return shaft || head;
            }, Vector4.zero);
            BuildSprite("Panel", 64, (u, v) =>
            {
                var q = new Vector2(Mathf.Max(Mathf.Abs(u) - 0.5f, 0f), Mathf.Max(Mathf.Abs(v) - 0.5f, 0f));
                return q.magnitude <= 0.45f;
            }, new Vector4(24, 24, 24, 24));
        }

        public static Sprite GetSprite(string name) => Load<Sprite>($"{SpritesRoot}/{name}.png");

        // 4x4 supersampled white shape on transparent; inside(u, v) with u, v in -1..1, v up.
        static void BuildSprite(string name, int size, Func<float, float, bool> inside, Vector4 border)
        {
            var path = $"{SpritesRoot}/{name}.png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var hits = 0;
                    for (var sy = 0; sy < 4; sy++)
                    {
                        for (var sx = 0; sx < 4; sx++)
                        {
                            var u = (x + (sx + 0.5f) / 4f) / size * 2f - 1f;
                            var v = (y + (sy + 0.5f) / 4f) / size * 2f - 1f;
                            if (inside(u, v)) hits++;
                        }
                    }

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        #endregion

        #region Materials

        public static Material Lit(string name, Color color, float smoothness = 0.15f, Color? emission = null)
        {
            var material = MaterialAsset(name, "Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>URP particle material. transparent = soft alpha billboards, otherwise opaque lit mesh bricks.</summary>
        public static Material Particle(string name, bool transparent)
        {
            var material = MaterialAsset(name, transparent ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Particles/Simple Lit");
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", transparent ? 1f : 0f);
            material.SetFloat("_Blend", 0f);
            if (transparent) material.SetTexture("_BaseMap", GetSprite("Circle").texture);
            BaseShaderGUI.SetMaterialKeywords(material, null, ParticleGUI.SetMaterialKeywords);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material MaterialAsset(string name, string shaderName)
        {
            EnsureFolder(MaterialsRoot);
            var path = $"{MaterialsRoot}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find(shaderName) ?? throw new InvalidOperationException($"Shader {shaderName} not found"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static Material OutlineTextMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (material == null)
            {
                EnsureFolder(MaterialsRoot);
                material = new Material(Load<TMP_FontAsset>(FontPath).material);
                AssetDatabase.CreateAsset(material, OutlineMaterialPath);
            }

            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat("_OutlineWidth", 0.25f);
            material.SetColor("_OutlineColor", new Color(0.05f, 0.05f, 0.08f, 1f));
            material.SetFloat("_FaceDilate", 0.15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        #endregion

        #region Configs

        public static void BuildConfigs()
        {
            var fight = LoadOrCreate<FightConfig>(FightConfigPath);
            var players = LoadOrCreate<NinjagoPlayersConfig>(PlayersConfigPath);
            var chase = LoadOrCreate<ChaseConfig>(ChaseConfigPath);
            var serialized = new SerializedObject(chase);
            var vehicles = serialized.FindProperty("vehicles");
            WriteVehicle(EnumDictionaryEditorUtils.GetValueProperty(vehicles, (int)VehicleType.Car),
                speed: 14f, usesVerticalLean: false, centerHeight: 0f, halfHeight: 0f, smoothing: 0.35f, spacing: 1.7f, extents: new Vector2(0.7f, 0.3f));
            WriteVehicle(EnumDictionaryEditorUtils.GetValueProperty(vehicles, (int)VehicleType.Skycraft),
                speed: 13f, usesVerticalLean: true, centerHeight: 3.2f, halfHeight: 1.6f, smoothing: 0.3f, spacing: 1.9f, extents: new Vector2(0.65f, 0.25f));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(chase);

            const string managerPath = "Assets/Prefabs/Singletons/GameConfigsManager.prefab";
            var root = PrefabUtility.LoadPrefabContents(managerPath);
            try
            {
                var manager = root.GetComponent<GameConfigsManager>();
                Set(manager, "fightConfig", fight);
                Set(manager, "chaseConfig", chase);
                Set(manager, "playersConfig", players);
                PrefabUtility.SaveAsPrefabAsset(root, managerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void WriteVehicle(SerializedProperty value, float speed, bool usesVerticalLean, float centerHeight, float halfHeight,
            float smoothing, float spacing, Vector2 extents)
        {
            value.FindPropertyRelative("segmentSeconds").floatValue = 20f;
            value.FindPropertyRelative("speed").floatValue = speed;
            value.FindPropertyRelative("usesVerticalLean").boolValue = usesVerticalLean;
            value.FindPropertyRelative("halfWidth").floatValue = 2.2f;
            value.FindPropertyRelative("centerHeight").floatValue = centerHeight;
            value.FindPropertyRelative("halfHeight").floatValue = halfHeight;
            value.FindPropertyRelative("steerSmoothingSeconds").floatValue = smoothing;
            value.FindPropertyRelative("maxSteerSpeed").floatValue = 7f;
            value.FindPropertyRelative("obstacleSpacingSeconds").floatValue = spacing;
            value.FindPropertyRelative("obstacleLeadSeconds").floatValue = 1.5f;
            value.FindPropertyRelative("openingSeconds").floatValue = 2f;
            value.FindPropertyRelative("cellWidth").floatValue = 2f;
            value.FindPropertyRelative("cellHeight").floatValue = 1.6f;
            value.FindPropertyRelative("vehicleHalfExtents").vector2Value = extents;
        }

        #endregion

        #region Audio

        public static void BuildAudioRegistry()
        {
            const string sfxRoot = "Assets/Audio/Sfx/Ninjago";
            const string bgmRoot = "Assets/Audio/Bgm/Ninjago";
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { sfxRoot, bgmRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var isMusic = path.StartsWith(bgmRoot, StringComparison.Ordinal);
                var settings = importer.defaultSampleSettings;
                settings.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = isMusic ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = !isMusic;
                importer.SaveAndReimport();
            }

            EditPrefab<SfxManager>("Assets/Prefabs/Singletons/SfxManager.prefab", "soundEffectDict", dict =>
            {
                Clips(dict, SfxManager.SoundEffect.StaffWhoosh, sfxRoot, "StaffWhoosh_1", "StaffWhoosh_2");
                Clips(dict, SfxManager.SoundEffect.NinjaSlip, sfxRoot, "NinjaSlip_1");
                Clips(dict, SfxManager.SoundEffect.NinjaSpin, sfxRoot, "NinjaSpin_1", "NinjaSpin_2");
                Clips(dict, SfxManager.SoundEffect.NinjaHit, sfxRoot, "NinjaHit_1", "NinjaHit_2");
                Clips(dict, SfxManager.SoundEffect.VehicleBump, sfxRoot, "VehicleBump_1", "VehicleBump_2");
            });
            EditPrefab<BgmManager>("Assets/Prefabs/Singletons/BgmManager.prefab", "bgmDict", dict =>
            {
                EnumDictionaryEditorUtils.GetValueProperty(dict, (int)BgmManager.BgmType.NinjaFight).objectReferenceValue =
                    Load<AudioClip>($"{bgmRoot}/NinjaFight.ogg");
                EnumDictionaryEditorUtils.GetValueProperty(dict, (int)BgmManager.BgmType.NinjaChase).objectReferenceValue =
                    Load<AudioClip>($"{bgmRoot}/NinjaChase.ogg");
            });
        }

        internal static void Clips(SerializedProperty dict, SfxManager.SoundEffect effect, string root, params string[] names)
        {
            var clips = EnumDictionaryEditorUtils.GetValueProperty(dict, (int)effect).FindPropertyRelative("clips");
            clips.arraySize = names.Length;
            for (var i = 0; i < names.Length; i++)
            {
                clips.GetArrayElementAtIndex(i).objectReferenceValue = Load<AudioClip>($"{root}/{names[i]}.wav");
            }
        }

        /// <summary>Opens a singleton prefab, edits one EnumDictionary field and saves it.</summary>
        public static void EditPrefab<T>(string prefabPath, string dictField, Action<SerializedProperty> edit) where T : Component
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var serialized = new SerializedObject(root.GetComponent<T>());
                edit(serialized.FindProperty(dictField));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        #endregion
    }
}
