#nullable enable

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Composes the overlay backdrop (UiTheme Dim sprite + tint under the Vignette sprite + tint) into one sprite,
    /// Overlay_DimVignette.png, so Pause / Reward / Tracking lost / Summary / menus dim the screen with one full-screen
    /// layer instead of two (every 1080p layer is ~0.7 ms of fill on the Mali-G52). Blending is done in the project's
    /// Gamma space, exactly as the two stacked UI images blended, so the look is unchanged. The file is rewritten only
    /// when its pixels change; its import settings are the vignette's (bilinear, uncompressed, UI pixels per unit).
    /// </summary>
    public static class UiOverlaySpriteComposer
    {
        public const string ComposedName = "Overlay_DimVignette";
        public const string ComposedPath = "Assets/Sprites/BilliardRogue/UI/" + ComposedName + ".png";
        const string AndroidPlatform = "Android";

        public static Sprite? Compose(UiTheme theme, ICollection<string> warnings)
        {
            if (theme.Dim == null || theme.Vignette == null)
            {
                warnings.Add("theme Dim / Vignette sprite missing: overlays keep two backdrop layers");
                return null;
            }

            var dim = ReadPixels(theme.Dim, out _, out _);
            var vignette = ReadPixels(theme.Vignette, out var width, out var height);
            // The dim art is one flat colour (Tools/Textures/make_ui.py overlay_dim).
            var under = (Color)dim[0] * theme.DimColor;
            var tint = theme.VignetteColor;
            var pixels = new Color32[vignette.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                var over = (Color)vignette[i] * tint;
                var alpha = 1f - (1f - over.a) * (1f - under.a);
                var rgb = alpha > 0f ? (over * over.a + under * (under.a * (1f - over.a))) / alpha : Color.clear;
                pixels[i] = new Color(rgb.r, rgb.g, rgb.b, alpha);
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            var png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            var file = Path.Combine(Directory.GetCurrentDirectory(), ComposedPath);
            if (!File.Exists(file) || !SameBytes(File.ReadAllBytes(file), png))
            {
                File.WriteAllBytes(file, png);
                AssetDatabase.ImportAsset(ComposedPath, ImportAssetOptions.ForceSynchronousImport);
            }

            CopyImportSettings(AssetDatabase.GetAssetPath(theme.Vignette));
            return AssetDatabase.LoadAssetAtPath<Sprite>(ComposedPath);
        }

        #region Helpers

        // The source PNG as authored: importer settings (compression, readability) do not matter here.
        static Color32[] ReadPixels(Sprite sprite, out int width, out int height)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
            width = texture.width;
            height = texture.height;
            var pixels = texture.GetPixels32();
            Object.DestroyImmediate(texture);
            return pixels;
        }

        static void CopyImportSettings(string templatePath)
        {
            var template = (TextureImporter)AssetImporter.GetAtPath(templatePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ComposedPath);
            var before = EditorJsonUtility.ToJson(importer) + JsonUtility.ToJson(importer.GetPlatformTextureSettings(AndroidPlatform));
            var settings = new TextureImporterSettings();
            template.ReadTextureSettings(settings);
            settings.spriteBorder = Vector4.zero;
            importer.SetTextureSettings(settings);
            importer.textureCompression = template.textureCompression;
            importer.maxTextureSize = template.maxTextureSize;
            var android = template.GetPlatformTextureSettings(AndroidPlatform);
            android.name = AndroidPlatform;
            importer.SetPlatformTextureSettings(android);
            var after = EditorJsonUtility.ToJson(importer) + JsonUtility.ToJson(importer.GetPlatformTextureSettings(AndroidPlatform));
            if (after != before)
            {
                importer.SaveAndReimport();
            }
        }

        static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}
