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
    /// Fills the singleton prefabs' audio EnumDictionaries from Tools/Audio/audio_manifest.json (TDD §13, §14.3, D11):
    /// SfxManager.soundEffectDict (SoundEffect -> clip variants), BgmManager.bgmDict (BgmType -> loop) and
    /// BgmManager.stingerDict (StingerType -> stereo stinger). Every enum key ends up present in ascending order; keys
    /// listed in the manifest are overwritten, other keys keep their current value. Idempotent: a prefab is saved only
    /// when something changed.
    /// Also verifies the SFX imports: the loudness matching is baked into sample levels (peaks -3 to -13 dBFS), so a clip
    /// that Unity re-normalized (Force To Mono + Normalize) or resampled is reported against the manifest's sfx_levels.
    /// CLI: unity command eval 'return Nex.BilliardRogue.Editor.AudioRegistryBuilder.Run();' --project-path .../Starter
    /// </summary>
    public static class AudioRegistryBuilder
    {
        const string SfxPrefab = "Assets/Prefabs/Singletons/SfxManager.prefab";
        const string BgmPrefab = "Assets/Prefabs/Singletons/BgmManager.prefab";
        const string ManifestFromProject = "../Tools/Audio/audio_manifest.json";
        const float PeakToleranceDb = 0.5f;
        // ADPCM's adaptive step size moves sharp transients by about a dB; larger drifts are real import problems.
        const float AdpcmPeakToleranceDb = 1.5f;
        const int AdpcmBlockSamples = 64;

        sealed class Report
        {
            public readonly List<string> MissingClips = new();
            public readonly List<string> UnknownKeys = new();
            public readonly List<string> EmptyKeys = new();
            public readonly List<string> ImportErrors = new();
            public readonly List<string> Saved = new();
            public int LevelsVerified;
            public int LevelsSkipped;
            public readonly StringBuilder Counts = new();
        }

        #region Entry Points

        [MenuItem("Nex/Billiard Rogue/Audio Registry", priority = 40)]
        static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            return Run(Path.GetFullPath(Path.Combine(ProjectFolder(), ManifestFromProject)));
        }

        public static string Run(string manifestPath)
        {
            var manifest = JObject.Parse(File.ReadAllText(manifestPath));
            var report = new Report();
            var sfx = ReadClips<SfxManager.SoundEffect>(manifest["sfx"] as JObject, "sfx", report);
            var bgm = ReadClips<BgmManager.BgmType>(manifest["bgm"] as JObject, "bgm", report);
            var stingers = ReadClips<BgmManager.StingerType>(manifest["stingers"] as JObject, "stingers", report);

            FillSfx(sfx, report);
            FillBgm(bgm, stingers, report);
            VerifySfxLevels(manifest["sfx_levels"] as JObject, report);
            return Summarize(manifestPath, report);
        }

        #endregion

        #region Manifest

        /// <summary>Maps enum names to loaded clips; a value may be one asset path or an array of paths.</summary>
        static Dictionary<int, List<AudioClip>> ReadClips<TEnum>(JObject? section, string sectionName, Report report)
            where TEnum : struct, Enum
        {
            var result = new Dictionary<int, List<AudioClip>>();
            if (section == null)
            {
                return result;
            }

            foreach (var property in section.Properties())
            {
                if (!TryParseName<TEnum>(property.Name, out var key))
                {
                    report.UnknownKeys.Add($"{sectionName}.{property.Name}");
                    continue;
                }

                var clips = new List<AudioClip>();
                var paths = property.Value is JArray array ? array : new JArray(property.Value);
                foreach (var token in paths)
                {
                    var clip = LoadClip(token.ToString(), report);
                    if (clip != null)
                    {
                        clips.Add(clip);
                    }
                }

                result[Convert.ToInt32(key)] = clips;
            }

            return result;
        }

        static bool TryParseName<TEnum>(string name, out TEnum value) where TEnum : struct, Enum
        {
            // Names only: Enum.TryParse would also accept numeric strings.
            value = default;
            return name.Length > 0 && char.IsLetter(name[0]) && Enum.TryParse(name, false, out value) && Enum.IsDefined(typeof(TEnum), value);
        }

        static AudioClip? LoadClip(string assetPath, Report report)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null && File.Exists(Path.Combine(ProjectFolder(), assetPath)))
            {
                // Copied in from Tools/Staging but not imported yet (the Editor was not focused).
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            }

            if (clip == null)
            {
                report.MissingClips.Add(assetPath);
            }

            return clip;
        }

        #endregion

        #region Import Check

        /// <summary>Compares every SFX clip's imported peak and length with what the build wrote (manifest sfx_levels).</summary>
        static void VerifySfxLevels(JObject? levels, Report report)
        {
            if (levels == null)
            {
                report.ImportErrors.Add("manifest has no sfx_levels (re-run Tools/Audio/build_audio.py)");
                return;
            }

            foreach (var property in levels.Properties())
            {
                var assetPath = property.Name;
                // A clip that failed to load is already listed under the missing clips.
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip == null) continue;
                var name = Path.GetFileName(assetPath);
                var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
                CheckImporter(importer, name, report);
                var expectedPeakDb = property.Value.Value<float>("peak_db");
                var expectedSamples = property.Value.Value<int>("samples");
                if (!SampleCountMatches(clip.samples, expectedSamples, importer))
                {
                    report.ImportErrors.Add($"{name}: {clip.samples} samples, built {expectedSamples} (resampled? keep Preserve Sample Rate)");
                }

                if (!TryGetPeakDb(clip, out var peakDb))
                {
                    report.LevelsSkipped++;
                    continue;
                }

                report.LevelsVerified++;
                var difference = peakDb - expectedPeakDb;
                if (Mathf.Abs(difference) > (IsAdpcm(importer) ? AdpcmPeakToleranceDb : PeakToleranceDb))
                {
                    var hint = difference > 0f ? "re-normalized on import" : "quieter than built";
                    report.ImportErrors.Add($"{name}: imported peak {peakDb:F1} dBFS, built {expectedPeakDb:F1} ({difference:+0.0;-0.0} dB, {hint})");
                }
            }
        }

        /// <summary>ADPCM encodes whole blocks, so an imported clip may carry up to one block of padding.</summary>
        static bool SampleCountMatches(int imported, int built, AudioImporter? importer)
        {
            if (imported == built) return true;
            if (!IsAdpcm(importer)) return false;
            return imported > built && imported - built < AdpcmBlockSamples && imported % AdpcmBlockSamples == 0;
        }

        static bool IsAdpcm(AudioImporter? importer)
        {
            return importer != null && importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.ADPCM;
        }

        /// <summary>Unity normalizes a clip while downmixing it when Force To Mono and Normalize are both on.</summary>
        static void CheckImporter(AudioImporter? importer, string name, Report report)
        {
            if (importer == null) return;
            if (!importer.forceToMono) return;
            var normalize = new SerializedObject(importer).FindProperty("m_Normalize");
            if (normalize == null || normalize.boolValue)
            {
                report.ImportErrors.Add($"{name}: Force To Mono with Normalize on (file is already mono: forceToMono=false or m_Normalize=0)");
            }
        }

        /// <summary>Sample peak of a clip in dBFS; only Decompress On Load clips expose their samples in the Editor.</summary>
        static bool TryGetPeakDb(AudioClip clip, out float peakDb)
        {
            peakDb = 0f;
            if (clip.loadType != AudioClipLoadType.DecompressOnLoad)
            {
                return false;
            }

            if (clip.loadState != AudioDataLoadState.Loaded && !clip.LoadAudioData())
            {
                return false;
            }

            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0))
            {
                return false;
            }

            var peak = 0f;
            foreach (var sample in data)
            {
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }

            peakDb = 20f * Mathf.Log10(Mathf.Max(peak, 1e-6f));
            return true;
        }

        #endregion

        #region Prefabs

        static void FillSfx(Dictionary<int, List<AudioClip>> clips, Report report)
        {
            var root = PrefabUtility.LoadPrefabContents(SfxPrefab);
            try
            {
                // Prefab contents live in an isolated preview scene, so no Undo is recorded; the prefab file is the result.
                var so = new SerializedObject(root.GetComponent<SfxManager>());
                var dict = so.FindProperty("soundEffectDict");
                EnumDictionaryRepair.EnsureAllKeys(dict, typeof(SfxManager.SoundEffect), value => value.FindPropertyRelative("clips").arraySize = 0);
                var pairs = dict.FindPropertyRelative("pairs");
                var filled = 0;
                for (var i = 0; i < pairs.arraySize; i++)
                {
                    var pair = pairs.GetArrayElementAtIndex(i);
                    var key = pair.FindPropertyRelative("key").intValue;
                    var array = pair.FindPropertyRelative("value").FindPropertyRelative("clips");
                    if (clips.TryGetValue(key, out var list))
                    {
                        WriteClips(array, list);
                    }

                    if (array.arraySize > 0)
                    {
                        filled++;
                    }
                    else if (key != (int)SfxManager.SoundEffect.None)
                    {
                        report.EmptyKeys.Add($"SoundEffect.{(SfxManager.SoundEffect)key}");
                    }
                }

                report.Counts.Append($"sfx {filled}/{pairs.arraySize - 1} keys with clips");
                SaveIfChanged(so, root, SfxPrefab, report);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void FillBgm(Dictionary<int, List<AudioClip>> bgm, Dictionary<int, List<AudioClip>> stingers, Report report)
        {
            var root = PrefabUtility.LoadPrefabContents(BgmPrefab);
            try
            {
                var so = new SerializedObject(root.GetComponent<BgmManager>());
                var bgmFilled = FillObjectDict(so.FindProperty("bgmDict"), typeof(BgmManager.BgmType), bgm, "BgmType", report);
                var stingerFilled = FillObjectDict(so.FindProperty("stingerDict"), typeof(BgmManager.StingerType), stingers, "StingerType", report);
                report.Counts.Append($", bgm {bgmFilled}/{Enum.GetValues(typeof(BgmManager.BgmType)).Length}")
                    .Append($", stingers {stingerFilled}/{Enum.GetValues(typeof(BgmManager.StingerType)).Length}");
                SaveIfChanged(so, root, BgmPrefab, report);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>EnumDictionary&lt;TEnum, AudioClip&gt;: all keys ascending, manifest keys set to their first clip.</summary>
        static int FillObjectDict(SerializedProperty dict, Type enumType, Dictionary<int, List<AudioClip>> clips, string label, Report report)
        {
            EnumDictionaryRepair.EnsureAllKeys(dict, enumType, value => value.objectReferenceValue = null);
            var pairs = dict.FindPropertyRelative("pairs");
            var filled = 0;
            for (var i = 0; i < pairs.arraySize; i++)
            {
                var pair = pairs.GetArrayElementAtIndex(i);
                var key = pair.FindPropertyRelative("key").intValue;
                var value = pair.FindPropertyRelative("value");
                if (clips.TryGetValue(key, out var list) && list.Count > 0)
                {
                    value.objectReferenceValue = list[0];
                }

                if (value.objectReferenceValue != null)
                {
                    filled++;
                }
                else
                {
                    report.EmptyKeys.Add($"{label}.{Enum.GetName(enumType, key)}");
                }
            }

            return filled;
        }

        static void WriteClips(SerializedProperty array, List<AudioClip> clips)
        {
            array.arraySize = clips.Count;
            for (var i = 0; i < clips.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }
        }

        static void SaveIfChanged(SerializedObject so, GameObject root, string prefabPath, Report report)
        {
            if (!so.ApplyModifiedPropertiesWithoutUndo()) return;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            report.Saved.Add(Path.GetFileName(prefabPath));
        }

        #endregion

        #region Helpers

        static string ProjectFolder()
        {
            return Path.GetDirectoryName(Application.dataPath)!;
        }

        static string Summarize(string manifestPath, Report report)
        {
            var text = new StringBuilder("[AudioRegistryBuilder] ").Append(report.Counts);
            text.Append(report.Saved.Count > 0 ? $"; saved {string.Join(", ", report.Saved)}" : "; prefabs unchanged");
            text.Append($"; SFX levels verified {report.LevelsVerified}");
            if (report.LevelsSkipped > 0)
            {
                text.Append($" ({report.LevelsSkipped} skipped: not Decompress On Load)");
            }

            AppendList(text, "MISSING clip(s)", report.MissingClips);
            AppendList(text, "manifest keys not in the enums", report.UnknownKeys);
            AppendList(text, "keys without audio", report.EmptyKeys);
            AppendList(text, "IMPORT-SETTINGS ERROR(s)", report.ImportErrors);
            text.Append($" (manifest {manifestPath})");
            var summary = text.ToString();
            if (report.MissingClips.Count > 0 || report.UnknownKeys.Count > 0 || report.ImportErrors.Count > 0)
            {
                Debug.LogWarning(summary);
            }
            else
            {
                Debug.Log(summary);
            }

            return summary;
        }

        static void AppendList(StringBuilder text, string label, List<string> items)
        {
            if (items.Count == 0) return;
            text.Append($"; {label} ({items.Count}): {string.Join(", ", items)}");
        }

        #endregion
    }
}
