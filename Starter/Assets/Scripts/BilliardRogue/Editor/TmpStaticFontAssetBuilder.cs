#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds one static, 1-bit RASTER_HINTED, point-filtered TMP font asset (FontAssetsBuilder's worker). The asset's
    /// importer userData records a stamp of the builder settings + SHA-1 of the source font file + SHA-1 of the character
    /// set, so a regenerated TTF with new glyph shapes or metrics (same code points) is rebuilt instead of reported as
    /// "up to date". Existing assets are regenerated in place (GUID and references survive).
    /// </summary>
    public static class TmpStaticFontAssetBuilder
    {
        public const int SamplingPointSize = 16;

        /// <summary>Bump when the atlas settings below change, so every asset rebuilds once.</summary>
        const int SettingsVersion = 2;
        const int Padding = 1;
        const int MaxAtlasSize = 4096;
        const GlyphRenderMode RenderMode = GlyphRenderMode.RASTER_HINTED;

        #region Public Methods

        /// <summary>
        /// Builds or refreshes <paramref name="assetPath"/> from <paramref name="fontPath"/> with the covered subset of
        /// <paramref name="characters"/>. Returns the asset and whether it was rebuilt; call <see cref="WriteStamp"/> with
        /// <paramref name="stamp"/> after AssetDatabase.SaveAssets() for rebuilt assets.
        /// </summary>
        public static TMP_FontAsset Build(string fontPath, string assetPath, string characters, bool warnUncovered,
            StringBuilder summary, List<string> warnings, out string stamp, out bool rebuilt)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                throw new FileNotFoundException($"Font not imported: {fontPath} (copy it from Tools/Staging and focus the Editor)");
            }

            var wanted = CoveredBy(font, characters, out var uncovered);
            var label = Path.GetFileNameWithoutExtension(assetPath);
            if (uncovered.Length > 0 && warnUncovered)
            {
                warnings.Add($"{label}: {uncovered.Length} requested characters not in the font: {FontAssetsBuilder.Escape(uncovered)}");
            }

            stamp = Stamp(fontPath, wanted);
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (asset != null && IsUpToDate(asset, assetPath, wanted, stamp))
            {
                summary.Append($" {label}: up to date ({asset.characterTable.Count} chars, {asset.atlasWidth}²);");
                rebuilt = false;
                return asset;
            }

            var size = AtlasSizeFor(wanted.Length);
            while (true)
            {
                asset = asset == null ? Create(font, assetPath, size) : Reset(asset, font, size);
                asset.TryAddCharacters(wanted, out var notAdded);
                if (string.IsNullOrEmpty(notAdded)) break;
                if (size >= MaxAtlasSize)
                {
                    warnings.Add($"{label}: atlas {size}² full, {notAdded.Length} characters left out");
                    break;
                }

                size *= 2;
            }

            Finish(asset);
            summary.Append($" {label}: rebuilt ({asset.characterTable.Count} chars, {size}²);");
            rebuilt = true;
            return asset;
        }

        /// <summary>Records the build stamp in the asset's .meta (importer userData). Call after the asset is saved.</summary>
        public static void WriteStamp(string assetPath, string stamp)
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null || importer.userData == stamp)
            {
                return;
            }

            importer.userData = stamp;
            AssetDatabase.WriteImportSettingsIfDirty(assetPath);
        }

        public static void SetFallback(TMP_FontAsset primary, TMP_FontAsset fallback)
        {
            var table = primary.fallbackFontAssetTable;
            if (table != null && table.Count == 1 && table[0] == fallback)
            {
                return;
            }

            primary.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
            EditorUtility.SetDirty(primary);
        }

        #endregion

        #region Up-To-Date Check

        /// <summary>"v&lt;settings&gt;;font=&lt;sha1 of the font file&gt;;chars=&lt;sha1 of the character set&gt;".</summary>
        static string Stamp(string fontPath, string characters)
        {
            var fontBytes = File.ReadAllBytes(Path.Combine(FontAssetsBuilder.ProjectFolder(), fontPath));
            var settings = $"{SettingsVersion}.{(int)RenderMode}.{Padding}.{SamplingPointSize}";
            return $"v{settings};font={Sha1(fontBytes)};chars={Sha1(Encoding.UTF8.GetBytes(characters))}";
        }

        static string Sha1(byte[] bytes)
        {
            using var sha = SHA1.Create();
            var hash = sha.ComputeHash(bytes);
            var text = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                text.Append(b.ToString("x2"));
            }

            return text.ToString();
        }

        static bool IsUpToDate(TMP_FontAsset asset, string assetPath, string wanted, string stamp)
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null || importer.userData != stamp)
            {
                return false; // new TTF bytes, new characters or new settings
            }

            if (asset.atlasPopulationMode != AtlasPopulationMode.Static || asset.atlasRenderMode != RenderMode)
            {
                return false;
            }

            if (asset.atlasPadding != Padding || asset.faceInfo.pointSize != SamplingPointSize)
            {
                return false;
            }

            if (asset.atlasTexture == null || asset.atlasTexture.filterMode != FilterMode.Point)
            {
                return false;
            }

            var present = new HashSet<uint>();
            foreach (var character in asset.characterTable)
            {
                present.Add(character.unicode);
            }

            foreach (var c in wanted)
            {
                if (!present.Contains(c)) return false;
            }

            return present.Count <= wanted.Length + 1; // + TMP's NBSP/space substitutes
        }

        #endregion

        #region Atlas

        /// <summary>The subset of <paramref name="characters"/> the font has a glyph for (space is always kept).</summary>
        static string CoveredBy(Font font, string characters, out string uncovered)
        {
            if (FontEngine.LoadFontFace(font, SamplingPointSize) != FontEngineError.Success)
            {
                throw new InvalidOperationException($"FontEngine cannot load {AssetDatabase.GetAssetPath(font)} (Include Font Data off?)");
            }

            var covered = new StringBuilder();
            var missing = new StringBuilder();
            foreach (var c in characters)
            {
                var ok = c == ' ' || (FontEngine.TryGetGlyphIndex(c, out var index) && index != 0);
                (ok ? covered : missing).Append(c);
            }

            uncovered = missing.ToString();
            return covered.ToString();
        }

        /// <summary>Smallest power-of-two square that fits the glyphs (16 px glyph + padding ≈ 18² px cells, 20% slack).</summary>
        static int AtlasSizeFor(int glyphCount)
        {
            var area = glyphCount * (SamplingPointSize + 2) * (SamplingPointSize + 2) * 1.2f;
            var size = 128;
            while (size * size < area && size < MaxAtlasSize)
            {
                size *= 2;
            }

            return size;
        }

        static TMP_FontAsset Create(Font font, string assetPath, int size)
        {
            var asset = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, Padding, RenderMode, size, size,
                AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
            if (asset == null)
            {
                throw new InvalidOperationException($"TMP could not create a font asset from {AssetDatabase.GetAssetPath(font)}");
            }

            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);
            asset.atlasTexture.name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            return asset;
        }

        /// <summary>Regenerates an existing asset in place with the builder's settings (keeps GUID, texture and material objects).</summary>
        static TMP_FontAsset Reset(TMP_FontAsset asset, Font font, int size)
        {
            var so = new SerializedObject(asset);
            so.FindProperty("m_AtlasWidth").intValue = size;
            so.FindProperty("m_AtlasHeight").intValue = size;
            so.FindProperty("m_AtlasPadding").intValue = Padding;
            so.FindProperty("m_AtlasRenderMode").intValue = (int)RenderMode;
            so.ApplyModifiedPropertiesWithoutUndo();
            asset.isMultiAtlasTexturesEnabled = false;
            if (FontEngine.LoadFontFace(font, SamplingPointSize) == FontEngineError.Success)
            {
                asset.faceInfo = FontEngine.GetFaceInfo(); // new metrics from a regenerated TTF
            }

            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic; // restores the source font (editor ref); TryAddCharacters refuses Static
            asset.ClearFontAssetData(setAtlasSizeToZero: false); // re-initialises the atlas at the new size
            return asset;
        }

        static void Finish(TMP_FontAsset asset)
        {
            var atlas = asset.atlasTexture;
            atlas.filterMode = FilterMode.Point;
            atlas.wrapMode = TextureWrapMode.Clamp;
            asset.material.SetFloat(ShaderUtilities.ID_TextureWidth, atlas.width);
            asset.material.SetFloat(ShaderUtilities.ID_TextureHeight, atlas.height);
            asset.atlasPopulationMode = AtlasPopulationMode.Static; // nulls the source font reference → no TTF/OTF in the build
            SetAtlasReadable(atlas, false);
            EditorUtility.SetDirty(atlas);
            EditorUtility.SetDirty(asset.material);
            EditorUtility.SetDirty(asset);
        }

        /// <summary>
        /// Static atlases do not need a CPU copy at runtime (the TMP Font Asset Creator clears it the same way). The TextCore
        /// helper is internal, so it is called by reflection; if it ever moves the atlas simply stays readable.
        /// </summary>
        static void SetAtlasReadable(Texture2D atlas, bool readable)
        {
            var type = Type.GetType("UnityEditor.TextCore.LowLevel.FontEngineEditorUtilities, UnityEditor.TextCoreFontEngineModule");
            var method = type?.GetMethod("SetAtlasTextureIsReadable", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            method?.Invoke(null, new object[] { atlas, readable });
        }

        #endregion
    }
}
