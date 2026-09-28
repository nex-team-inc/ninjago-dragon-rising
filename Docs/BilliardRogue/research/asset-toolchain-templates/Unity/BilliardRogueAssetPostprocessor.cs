#nullable enable

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Enforces import settings for generated Billiard Rogue content by folder. Settings are applied on
    /// every (re)import so generated assets never depend on hand-tweaked inspector values.
    /// After editing a rule run "Nex/Billiard Rogue/Reimport Generated Assets" (BilliardRogueReimport).
    /// Do NOT bump <see cref="GetVersion"/>: Unity then reimports every texture, model and audio clip in
    /// the project (Packages included), not just these folders (measured).
    /// </summary>
    public class BilliardRogueAssetPostprocessor : AssetPostprocessor
    {
        const string ModelsRoot = "Assets/Models/BilliardRogue/";
        const string TexturesRoot = "Assets/Textures/BilliardRogue/";
        const string SpritesRoot = "Assets/Sprites/BilliardRogue/";
        const string SfxRoot = "Assets/Audio/Sfx/BilliardRogue/";
        const string BgmRoot = "Assets/Audio/Bgm/BilliardRogue/";
        const string AndroidPlatform = "Android";
        const int UncompressedMaxSize = 128;
        const float WorldSpritePixelsPerUnit = 32f;

        public override uint GetVersion() => 1;

        #region Models

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsRoot)) return;
            var importer = (ModelImporter)assetImporter;
            var hasAnimation = assetPath.Contains("_Anim");

            // Scene: the Blender export already bakes axis + unit conversion (see asset-toolchain.md)
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false; // true would flip our models to face -Z
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.preserveHierarchy = true; // keep "Body" as a child: tween it without scaling the root
            importer.sortHierarchyByName = true;

            // Meshes
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.addCollider = false;
            importer.keepQuads = false;
            importer.weldVertices = true;
            importer.indexFormat = ModelImporterIndexFormat.Auto;
            importer.generateSecondaryUV = false;

            // Geometry: keep Blender's per-face normals (flat faces stay flat, smooth faces stay smooth)
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;

            // Materials: the prefab builder assigns the shared palette material
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            // Rig / animation: rigid-part clips only when the file name opts in with "_Anim"
            importer.animationType = hasAnimation ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            importer.importAnimation = hasAnimation;
            importer.importConstraints = false;
            importer.importAnimatedCustomProperties = false;
            if (hasAnimation)
            {
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                importer.resampleCurves = true;
            }
        }

        #endregion

        #region Textures

        void OnPreprocessTexture()
        {
            var importer = (TextureImporter)assetImporter;
            if (assetPath.StartsWith(TexturesRoot)) ConfigurePixelTexture(importer);
            else if (assetPath.StartsWith(SpritesRoot)) ConfigureSprite(importer);
        }

        void ConfigurePixelTexture(TextureImporter importer)
        {
            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            var isNormal = fileName.EndsWith("_Normal");
            var isMask = fileName.EndsWith("_Cavity") || fileName.EndsWith("_Height") || fileName.EndsWith("_Mask");
            var isPalette = fileName.Contains("Palette");
            var clamp = assetPath.Contains("/Vfx/") || isPalette;

            importer.textureType = isNormal ? TextureImporterType.NormalMap
                : isMask ? TextureImporterType.SingleChannel
                : TextureImporterType.Default;
            importer.sRGBTexture = !isNormal && !isMask;
            importer.alphaSource = isMask ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = clamp;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.anisoLevel = 0;
            importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            importer.streamingMipmaps = false;
            if (isMask)
            {
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
                importer.SetTextureSettings(settings);
            }
            ApplyCompression(importer, isNormal || isPalette);
        }

        void ConfigureSprite(TextureImporter importer)
        {
            var isWorldPixelArt = assetPath.Contains("/World/");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = isWorldPixelArt ? WorldSpritePixelsPerUnit : 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = isWorldPixelArt ? FilterMode.Point : FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);

            ApplyCompression(importer, false);
        }

        /// <summary>
        /// Palette, normal maps and small pixel textures stay uncompressed: ETC2/ASTC blocks shift palette
        /// cells and smear hard pixel edges (measured: ETC2 changes ~100% of palette texels). Larger
        /// textures use ASTC 4x4 on Android, the highest-quality block size.
        /// </summary>
        void ApplyCompression(TextureImporter importer, bool forceUncompressed)
        {
            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            var longest = Mathf.Max(width, height);
            var uncompressed = forceUncompressed || longest <= UncompressedMaxSize;
            importer.maxTextureSize = Mathf.Clamp(Mathf.NextPowerOfTwo(longest), 32, 2048);
            importer.textureCompression = uncompressed ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;

            var android = importer.GetPlatformTextureSettings(AndroidPlatform);
            android.overridden = true;
            android.maxTextureSize = importer.maxTextureSize;
            android.textureCompression = importer.textureCompression;
            android.format = uncompressed ? TextureImporterFormat.Automatic : TextureImporterFormat.ASTC_4x4;
            importer.SetPlatformTextureSettings(android);
        }

        #endregion

        #region Audio

        void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;
            if (assetPath.StartsWith(SfxRoot)) ConfigureSfx(importer);
            else if (assetPath.StartsWith(BgmRoot)) ConfigureBgm(importer);
        }

        static void ConfigureSfx(AudioImporter importer)
        {
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.ambisonic = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.ADPCM;
            settings.preloadAudioData = true;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }

        static void ConfigureBgm(AudioImporter importer)
        {
            importer.forceToMono = false;
            importer.loadInBackground = true;
            importer.ambisonic = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            settings.preloadAudioData = false;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }

        #endregion
    }
}
