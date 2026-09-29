#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Sets explicit importer settings for the generated Billiard Rogue content (TDD D9; no global AssetPostprocessor, so
    /// nothing outside our folders is ever reimported): models, textures (palette, surfaces), sprites (UI with 9-slice
    /// borders from ui_slices.json, icons), particle flipbooks, audio (SFX / BGM / stingers) and fonts, each with an
    /// Android override. Idempotent: an importer whose serialized settings did not change is not saved or reimported.
    /// Run it after copying Tools/Staging/Assets into Starter/Assets (the first import of a new file uses Unity defaults).
    /// CLI: unity command eval 'return Nex.BilliardRogue.Editor.ImportSettingsBuilder.Run();' --project-path .../Starter
    /// </summary>
    public static class ImportSettingsBuilder
    {
        const string ModelsRoot = "Assets/Models/BilliardRogue";
        const string TexturesRoot = "Assets/Textures/BilliardRogue";
        const string SpritesRoot = "Assets/Sprites/BilliardRogue";
        const string ParticlesRoot = "Assets/Sprites/BilliardRogue/Particles";
        const string SfxRoot = "Assets/Audio/Sfx/BilliardRogue";
        const string BgmRoot = "Assets/Audio/Bgm/BilliardRogue";
        const string FontsRoot = "Assets/Fonts/BilliardRogue";
        const string UiSlicesPath = "Assets/Sprites/BilliardRogue/UI/ui_slices.json";
        const string AndroidPlatform = "Android";
        const float UiPixelsPerUnit = 100f / 3f; // UI pixel art shows at an integer 3x on the 1920x1080 canvas
        const int UncompressedMaxSize = 256; // pixel art up to 256 px stays exact (ETC2/ASTC smear hard edges)

        static readonly string[] Roots = { ModelsRoot, TexturesRoot, SpritesRoot, SfxRoot, BgmRoot, FontsRoot };

        sealed class Report
        {
            public readonly SortedDictionary<string, int[]> Counts = new(StringComparer.Ordinal); // category → {seen, changed}
            public readonly List<string> Warnings = new();

            public void Count(string category, bool changed)
            {
                if (!Counts.TryGetValue(category, out var c)) Counts[category] = c = new int[2];
                c[0]++;
                if (changed) c[1]++;
            }
        }

        #region Entry Points

        [MenuItem("Nex/Billiard Rogue/Import Settings", priority = 10)]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var report = new Report();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); // files copied in while the Editor was unfocused
            var slices = ReadSlices(report);
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var root in Roots)
                {
                    foreach (var path in AssetFiles(root)) Apply(path, slices, report);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            return Summarize(report);
        }

        #endregion

        #region Dispatch

        static void Apply(string path, Dictionary<string, (Vector4 Border, bool Bilinear)> slices, Report report)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null) return;
            var before = Fingerprint(importer);
            var category = importer switch
            {
                ModelImporter model => ConfigureModel(model, path),
                TextureImporter texture when path.StartsWith(TexturesRoot + "/", StringComparison.Ordinal) => ConfigureTexture(texture, path),
                TextureImporter texture when path.StartsWith(ParticlesRoot + "/", StringComparison.Ordinal) => ConfigureParticle(texture),
                TextureImporter texture => ConfigureSprite(texture, path, slices, report),
                AudioImporter audio when path.StartsWith(SfxRoot + "/", StringComparison.Ordinal) => ConfigureSfx(audio),
                AudioImporter audio => ConfigureMusic(audio, path),
                TrueTypeFontImporter font => ConfigureFont(font),
                _ => null,
            };
            if (category == null) return; // json / txt / other importers: nothing to set
            var changed = Fingerprint(importer) != before;
            report.Count(category, changed);
            if (changed) importer.SaveAndReimport();
        }

        /// <summary>Serialized importer state incl. the Android override (setters mark importers dirty even when nothing changes).</summary>
        static string Fingerprint(AssetImporter importer)
        {
            var json = EditorJsonUtility.ToJson(importer);
            return importer switch
            {
                TextureImporter texture => json + JsonUtility.ToJson(texture.GetPlatformTextureSettings(AndroidPlatform)),
                AudioImporter audio => json + audio.ContainsSampleSettingsOverride(AndroidPlatform)
                                            + JsonUtility.ToJson(audio.GetOverrideSampleSettings(AndroidPlatform)),
                _ => json,
            };
        }

        #endregion

        #region Models

        /// <summary>Rigid-part models: file scale, no axis bake, hierarchy kept, no materials; animation only for "*_Anim".</summary>
        static string ConfigureModel(ModelImporter importer, string path)
        {
            var animated = Path.GetFileNameWithoutExtension(path).EndsWith("_Anim", StringComparison.Ordinal);
            importer.globalScale = 1f; // the Blender export already bakes axis + unit conversion (asset-toolchain.md §5)
            importer.useFileScale = true;
            importer.bakeAxisConversion = false; // true flips our models to face -Z
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.preserveHierarchy = true; // named rigid parts (Body, Head, ...) are tweened by code
            importer.sortHierarchyByName = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.addCollider = false;
            importer.keepQuads = false;
            importer.weldVertices = true;
            importer.indexFormat = ModelImporterIndexFormat.Auto;
            importer.generateSecondaryUV = false;
            importer.importNormals = ModelImporterNormals.Import; // Blender's per-face normals keep low-poly facets crisp
            importer.importTangents = ModelImporterTangents.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; // prefab builders assign the palette material
            importer.animationType = animated ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.importAnimation = animated;
            importer.importConstraints = false;
            importer.importAnimatedCustomProperties = false;
            if (animated)
            {
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                importer.resampleCurves = true;
            }

            return "models";
        }

        #endregion

        #region Textures

        /// <summary>Palette, surface albedo / normal / cavity: point, no mips, uncompressed (palette exact), Repeat except palette + Vfx.</summary>
        static string ConfigureTexture(TextureImporter importer, string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var isNormal = name.EndsWith("_Normal", StringComparison.Ordinal);
            var isMask = name.EndsWith("_Cavity", StringComparison.Ordinal) || name.EndsWith("_Height", StringComparison.Ordinal)
                         || name.EndsWith("_Mask", StringComparison.Ordinal);
            var isPalette = path.Contains("/Palette/");
            var clamp = isPalette || path.Contains("/Vfx/");
            importer.textureType = isNormal ? TextureImporterType.NormalMap : isMask ? TextureImporterType.SingleChannel : TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = !isNormal && !isMask;
            importer.alphaSource = isMask || isNormal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = clamp && !isPalette;
            importer.convertToNormalmap = false; // our normal maps are authored (OpenGL, +G up), not grey heights
            importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            SetPixelArtCommon(importer);
            if (isMask)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red; // R8
                importer.SetTextureSettings(settings);
            }

            ApplyCompression(importer, isPalette || isNormal);
            return isPalette ? "palette" : isNormal ? "normal maps" : isMask ? "cavity maps" : "textures";
        }

        /// <summary>Particle flipbooks are material textures (Texture Sheet Animation grid 8x1), not UI sprites.</summary>
        static string ConfigureParticle(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            SetPixelArtCommon(importer);
            ApplyCompression(importer, true);
            return "particles";
        }

        /// <summary>UI + icon sprites: Single, PPU 100/3 (native size = 3x), point, FullRect mesh, 9-slice border from ui_slices.json.</summary>
        static string ConfigureSprite(TextureImporter importer, string path, Dictionary<string, (Vector4 Border, bool Bilinear)> slices, Report report)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var inUi = path.Contains("/UI/");
            // The composed overlay backdrop (UiOverlaySpriteComposer) imports like the vignette it is made from.
            var sliceName = name == UiOverlaySpriteComposer.ComposedName ? "Overlay_Vignette" : name;
            if (!slices.TryGetValue(sliceName, out var slice))
            {
                slice = (Vector4.zero, false);
                if (inUi && name.StartsWith("Frame_", StringComparison.Ordinal)) report.Warnings.Add($"{name}: frame without a ui_slices.json entry");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = UiPixelsPerUnit;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            SetPixelArtCommon(importer);
            if (slice.Bilinear) importer.filterMode = FilterMode.Bilinear; // smooth-alpha overlays (vignette), not pixel art
            importer.spriteBorder = slice.Border;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // required for Sliced / Tiled images
            settings.spriteExtrude = 0;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);

            ApplyCompression(importer, name.StartsWith("Logo_", StringComparison.Ordinal));
            return inUi ? "ui sprites" : "icon sprites";
        }

        static void SetPixelArtCommon(TextureImporter importer)
        {
            importer.mipmapEnabled = false;
            importer.streamingMipmaps = false;
            importer.filterMode = FilterMode.Point;
            importer.anisoLevel = 0;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
        }

        /// <summary>Pixel art up to 256 px (and anything forced) stays uncompressed; larger art uses ASTC 4x4 on Android.</summary>
        static void ApplyCompression(TextureImporter importer, bool forceUncompressed)
        {
            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            var longest = Mathf.Max(width, height);
            var uncompressed = forceUncompressed || longest <= UncompressedMaxSize;
            importer.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(longest), 32, 2048);
            importer.textureCompression = uncompressed ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;

            var android = importer.GetPlatformTextureSettings(AndroidPlatform);
            android.overridden = true;
            android.maxTextureSize = importer.maxTextureSize;
            android.textureCompression = importer.textureCompression;
            android.format = uncompressed ? TextureImporterFormat.Automatic : TextureImporterFormat.ASTC_4x4;
            android.crunchedCompression = false;
            importer.SetPlatformTextureSettings(android);
        }

        static Dictionary<string, (Vector4 Border, bool Bilinear)> ReadSlices(Report report)
        {
            var result = new Dictionary<string, (Vector4 Border, bool Bilinear)>(StringComparer.Ordinal);
            var file = Path.Combine(ProjectFolder(), UiSlicesPath);
            if (!File.Exists(file))
            {
                report.Warnings.Add($"{UiSlicesPath} missing: UI sprites get no 9-slice borders");
                return result;
            }

            var json = JObject.Parse(File.ReadAllText(file));
            if (json["sprites"] is not JArray sprites) return result;
            foreach (var sprite in sprites)
            {
                var border = sprite["border"];
                var name = sprite.Value<string>("name");
                if (border == null || string.IsNullOrEmpty(name)) continue;
                // Sprite.border order: x = left, y = bottom, z = right, w = top
                var values = new Vector4(border.Value<float>("left"), border.Value<float>("bottom"),
                    border.Value<float>("right"), border.Value<float>("top"));
                result[name!] = (values, sprite.Value<string>("filter") == "Bilinear"); // ui_slices.json: 9-slice border + smooth art flag
            }

            return result;
        }

        #endregion

        #region Audio and Fonts

        /// <summary>
        /// SFX: ADPCM, decompress on load, preloaded (short, frequent, zero decode cost at play time). The files are
        /// built mono with their loudness baked in, so Force To Mono stays off: it would re-normalize the peaks.
        /// </summary>
        static string ConfigureSfx(AudioImporter importer)
        {
            importer.forceToMono = false;
            importer.loadInBackground = false;
            importer.ambisonic = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.ADPCM;
            settings.preloadAudioData = true;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.SetOverrideSampleSettings(AndroidPlatform, settings);
            return "sfx";
        }

        /// <summary>BGM loops: stereo Vorbis 0.6, streaming, loaded in background. Stingers (short, must start on cue): in memory.</summary>
        static string ConfigureMusic(AudioImporter importer, string path)
        {
            var stinger = Path.GetFileName(path).StartsWith("Stinger_", StringComparison.Ordinal);
            importer.forceToMono = false;
            importer.loadInBackground = !stinger;
            importer.ambisonic = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = stinger ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            settings.preloadAudioData = stinger;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.SetOverrideSampleSettings(AndroidPlatform, settings);
            return stinger ? "stingers" : "bgm";
        }

        /// <summary>Pixel TTFs: font data included (FontAssetsBuilder rasterises them), hinted raster for any legacy Text use.</summary>
        static string ConfigureFont(TrueTypeFontImporter importer)
        {
            importer.includeFontData = true;
            importer.fontTextureCase = FontTextureCase.Dynamic;
            importer.fontRenderingMode = FontRenderingMode.HintedRaster;
            importer.characterPadding = 0;
            importer.shouldRoundAdvanceValue = true;
            return "fonts";
        }

        #endregion

        #region Helpers

        /// <summary>Project-relative asset files under root (on disk, so freshly copied files are found too).</summary>
        static IEnumerable<string> AssetFiles(string root)
        {
            var absolute = Path.Combine(ProjectFolder(), root);
            if (!Directory.Exists(absolute)) yield break;
            var files = Directory.GetFiles(absolute, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith(".", StringComparison.Ordinal) || name.EndsWith(".meta", StringComparison.Ordinal)) continue;
                yield return root + file.Substring(absolute.Length).Replace('\\', '/');
            }
        }

        static string ProjectFolder()
        {
            return Path.GetDirectoryName(Application.dataPath)!;
        }

        static string Summarize(Report report)
        {
            var text = new StringBuilder("[ImportSettingsBuilder]");
            var total = 0;
            var changed = 0;
            foreach (var (category, c) in report.Counts)
            {
                text.Append($" {category} {c[0]} ({c[1]} reimported);");
                total += c[0];
                changed += c[1];
            }

            text.Append($" total {total}, reimported {changed}");
            if (total == 0) text.Append(" (no generated assets found: copy Tools/Staging/Assets into Starter/Assets first)");
            if (report.Warnings.Count > 0) text.Append($"; WARNINGS: {string.Join(" | ", report.Warnings)}");
            var summary = text.ToString();
            if (report.Warnings.Count > 0)
            {
                Debug.LogWarning(summary);
            }
            else
            {
                Debug.Log(summary);
            }

            return summary;
        }

        #endregion
    }
}
