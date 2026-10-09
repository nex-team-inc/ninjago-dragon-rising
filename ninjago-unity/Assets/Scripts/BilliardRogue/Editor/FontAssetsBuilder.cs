#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds the Billiard Rogue TMP font assets (TDD D12 + §17, research/localization-fonts.md §11–12): static, 1-bit
    /// RASTER_HINTED, 16 px sampling, 1 px padding, point-filtered atlases (see <see cref="TmpStaticFontAssetBuilder"/>):
    /// <list type="bullet">
    /// <item><c>BilliardPixel_TMP</c> / <c>BilliardPixelBold_TMP</c>: the Latin pixel fonts (UI code loads them by path).</item>
    /// <item><c>BilliardPixelOutline_TMP</c> / <c>BilliardPixelBoldOutline_TMP</c>: the same glyphs dilated by 1 px with
    /// identical advances and metrics. For HP / damage numbers put a second TMP text with the outline asset, same size,
    /// alignment and rect, in a dark colour (#0a0c18) directly under the main text (earlier sibling).</item>
    /// <item><c>BilliardPixel_CJK_TMP</c>: GlowSansJ-Normal-Bold with every character found in the LocalizationTable string
    /// tables plus ASCII, kana, CJK punctuation and fullwidth forms.</item>
    /// </list>
    /// One fallback chain for every locale: each Latin asset → CJK (outline assets too, so CJK text lays out identically in
    /// both layers). Assets are Static (no source font in the build) and not named "*SDF.asset", so DynamicFontSanitizer
    /// leaves them. Idempotent: an asset whose stamp (settings + SHA-1 of the font file + SHA-1 of the character set) and
    /// atlas settings match is left untouched; otherwise it is rebuilt in place (GUID and references survive), so a
    /// regenerated TTF always reaches the atlas. Re-run after the LocalizationSeeder adds CJK text.
    /// Display at TMP fontSize 16 × n (48 = one font pixel per 3x UI art pixel), no faux bold/italic.
    /// CLI: unity command eval 'return Nex.BilliardRogue.Editor.FontAssetsBuilder.Run();' --project-path .../Starter
    /// </summary>
    public static class FontAssetsBuilder
    {
        public const string Folder = "Assets/Fonts/BilliardRogue";
        public const string RegularFontPath = Folder + "/BilliardPixel.ttf";
        public const string BoldFontPath = Folder + "/BilliardPixel-Bold.ttf";
        public const string RegularOutlineFontPath = Folder + "/BilliardPixel-Outline.ttf";
        public const string BoldOutlineFontPath = Folder + "/BilliardPixel-BoldOutline.ttf";
        public const string CharsetPath = Folder + "/BilliardPixel_charset.txt";
        public const string CjkSourceFontPath = "Assets/Fonts/Localization/GlowSans-ja/GlowSansJ-Normal-Bold.otf";

        // TDD §17 names (UI builders load these by path)
        public const string RegularAssetPath = Folder + "/BilliardPixel_TMP.asset";
        public const string BoldAssetPath = Folder + "/BilliardPixelBold_TMP.asset";
        public const string CjkAssetPath = Folder + "/BilliardPixel_CJK_TMP.asset";
        public const string RegularOutlineAssetPath = Folder + "/BilliardPixelOutline_TMP.asset";
        public const string BoldOutlineAssetPath = Folder + "/BilliardPixelBoldOutline_TMP.asset";
        public const int SamplingPointSize = TmpStaticFontAssetBuilder.SamplingPointSize;

        const string TableCollection = "LocalizationTable";

        static readonly (string Label, string Font, string Asset)[] LatinAssets =
        {
            ("regular", RegularFontPath, RegularAssetPath),
            ("bold", BoldFontPath, BoldAssetPath),
            ("regular outline", RegularOutlineFontPath, RegularOutlineAssetPath),
            ("bold outline", BoldOutlineFontPath, BoldOutlineAssetPath),
        };

        /// <summary>Hiragana, katakana, CJK symbols/punctuation and fullwidth forms, kept even before any CJK string exists.</summary>
        static readonly (int First, int Last)[] CjkBaseRanges =
        {
            (0x3000, 0x303F), (0x3041, 0x3096), (0x3099, 0x30FF), (0xFF01, 0xFF5E),
        };

        #region Entry Points

        [MenuItem("Nex/Billiard Rogue/Font Assets", priority = 30)]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            var summary = new StringBuilder("[FontAssetsBuilder]");
            var warnings = new List<string>();
            var tableText = ReadTableText(warnings);
            var latin = ReadLatinCharset(warnings);
            var stamps = new List<(string Path, string Stamp)>();
            var cjk = TmpStaticFontAssetBuilder.Build(CjkSourceFontPath, CjkAssetPath, CjkCharacters(tableText), false,
                summary, warnings, out var cjkStamp, out var cjkRebuilt);
            if (cjkRebuilt)
            {
                stamps.Add((CjkAssetPath, cjkStamp));
            }

            var latinAssets = new List<(string Label, TMP_FontAsset Asset)>();
            foreach (var (label, fontPath, assetPath) in LatinAssets)
            {
                var asset = TmpStaticFontAssetBuilder.Build(fontPath, assetPath, latin, true, summary, warnings, out var stamp, out var rebuilt);
                if (rebuilt)
                {
                    stamps.Add((assetPath, stamp));
                }

                TmpStaticFontAssetBuilder.SetFallback(asset, cjk);
                latinAssets.Add((label, asset));
            }

            AssetDatabase.SaveAssets();
            foreach (var (path, stamp) in stamps)
            {
                TmpStaticFontAssetBuilder.WriteStamp(path, stamp); // only once the atlas is on disk
            }

            foreach (var (label, asset) in latinAssets)
            {
                Validate(asset, label, tableText, warnings);
            }

            if (warnings.Count > 0)
            {
                summary.Append($"; WARNINGS ({warnings.Count}): {string.Join(" | ", warnings)}");
                Debug.LogWarning(summary.ToString());
            }
            else
            {
                summary.Append("; every LocalizationTable string renders with the chain");
                Debug.Log(summary.ToString());
            }

            return summary.ToString();
        }

        #endregion

        #region Character Sets

        /// <summary>Locale code → every string value in the LocalizationTable collection.</summary>
        static Dictionary<string, List<string>> ReadTableText(List<string> warnings)
        {
            var result = new Dictionary<string, List<string>>();
            var collection = LocalizationEditorSettings.GetStringTableCollection(TableCollection);
            if (collection == null)
            {
                warnings.Add($"string table collection '{TableCollection}' not found; CJK atlas holds the base set only");
                return result;
            }

            foreach (var table in collection.StringTables)
            {
                var values = new List<string>();
                foreach (var entry in table.Values)
                {
                    if (!string.IsNullOrEmpty(entry.Value))
                    {
                        values.Add(entry.Value);
                    }
                }

                result[table.LocaleIdentifier.Code] = values;
            }

            return result;
        }

        static string ReadLatinCharset(List<string> warnings)
        {
            var file = Path.Combine(ProjectFolder(), CharsetPath);
            if (File.Exists(file)) return Distinct(File.ReadAllText(file, Encoding.UTF8));
            warnings.Add($"{CharsetPath} missing; Latin atlases use ASCII + Latin-1");
            var fallback = new StringBuilder();
            for (var c = 0x20; c < 0x100; c++)
            {
                if (c < 0x7F || c >= 0xA0)
                {
                    fallback.Append((char)c);
                }
            }

            return fallback.ToString();
        }

        /// <summary>
        /// ASCII + CJK base ranges + every character of every table (Latin ones included, so the CJK asset also works as
        /// the only font of a stray TMP component; the Latin pixel font still wins for them in the chain).
        /// </summary>
        static string CjkCharacters(Dictionary<string, List<string>> tableText)
        {
            var text = new StringBuilder();
            for (var c = 0x20; c < 0x7F; c++)
            {
                text.Append((char)c);
            }

            foreach (var (first, last) in CjkBaseRanges)
            {
                for (var c = first; c <= last; c++)
                {
                    text.Append((char)c);
                }
            }

            foreach (var values in tableText.Values)
            {
                foreach (var value in values)
                {
                    text.Append(value);
                }
            }

            return Distinct(text.ToString());
        }

        /// <summary>Unique characters ≥ U+0020, sorted by code point (deterministic atlas packing).</summary>
        static string Distinct(string text)
        {
            var set = new SortedSet<char>();
            foreach (var c in text)
            {
                if (c >= 0x20 && !char.IsSurrogate(c))
                {
                    set.Add(c);
                }
            }

            var result = new StringBuilder(set.Count);
            foreach (var c in set)
            {
                result.Append(c);
            }

            return result.ToString();
        }

        #endregion

        #region Validation

        static void Validate(TMP_FontAsset primary, string label, Dictionary<string, List<string>> tableText, List<string> warnings)
        {
            foreach (var (locale, values) in tableText)
            {
                var missing = new SortedSet<char>();
                foreach (var value in values)
                {
                    if (primary.HasCharacters(value, out uint[] absent, searchFallbacks: true, tryAddCharacter: false)) continue;
                    foreach (var unicode in absent)
                    {
                        if (unicode >= 0x20)
                        {
                            missing.Add((char)unicode);
                        }
                    }
                }

                if (missing.Count > 0)
                {
                    warnings.Add($"{label} chain misses {missing.Count} chars in {locale}: {Escape(string.Concat(missing))}");
                }
            }
        }

        #endregion

        #region Helpers

        internal static string Escape(string characters)
        {
            var text = new StringBuilder();
            var count = 0;
            foreach (var c in characters)
            {
                if (count++ == 40)
                {
                    text.Append('…');
                    break;
                }

                text.Append(char.IsWhiteSpace(c) ? $"U+{(int)c:X4}" : c.ToString());
            }

            return text.ToString();
        }

        internal static string ProjectFolder()
        {
            return Path.GetDirectoryName(Application.dataPath)!;
        }

        #endregion
    }
}
