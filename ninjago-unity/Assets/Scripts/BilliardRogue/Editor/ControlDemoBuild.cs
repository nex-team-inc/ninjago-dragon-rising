#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Nex.BilliardRogue.Editor
{
    /// <summary>
    /// Builds the Android development APK of Main.unity used to try the paw controls on a Nex Playground.
    /// The control-demo defines are added for this build only and restored afterwards, so the project stays unchanged.
    /// </summary>
    public static class ControlDemoBuild
    {
        const string ScenePath = "Assets/Scenes/BilliardRogue/Main.unity";
        const string OutputRelativePath = "../Builds/Android/BilliardRogue_ControlDemo.apk";
        // Read by the Addressables player-build processor when the settings asset defers to the user preference.
        const string BuildAddressablesWithPlayerPref = "Addressables.BuildAddressablesWithPlayerBuild";
        static readonly string[] DemoDefines = { "BR_CONTROL_DEMO", "ENABLE_DEBUG_SETTINGS" };

        [Serializable]
        public class Summary
        {
            public string result = "NotStarted";
            public string apkPath = "";
            public long apkSizeBytes;
            public string applicationId = "";
            public string defines = "";
            public double addressablesSeconds;
            public double playerSeconds;
            public double totalSeconds;
            public int totalErrors;
            public int totalWarnings;
            public string[] settingsChanged = Array.Empty<string>();
            public string[] errors = Array.Empty<string>();
        }

        #region Public Methods

        [MenuItem("Nex/Billiard Rogue/Build Control Demo APK", priority = 100)]
        static void BuildFromMenu() => Debug.Log($"[ControlDemoBuild] {JsonUtility.ToJson(Run(), true)}");

        /// <summary>Builds Addressables content, then the development APK; the summary is also written next to the APK.</summary>
        public static Summary Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop play mode before building.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                throw new InvalidOperationException($"Active build target is {EditorUserBuildSettings.activeBuildTarget}; switch to Android first.");
            }

            var summary = new Summary
            {
                apkPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), OutputRelativePath)),
                applicationId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),
            };
            var total = Stopwatch.StartNew();
            var target = NamedBuildTarget.Android;
            var originalDefines = PlayerSettings.GetScriptingDefineSymbols(target);
            var originalAppBundle = EditorUserBuildSettings.buildAppBundle;
            var originalExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            var hadAddressablesPref = EditorPrefs.HasKey(BuildAddressablesWithPlayerPref);
            var originalAddressablesPref = EditorPrefs.GetBool(BuildAddressablesWithPlayerPref, true);
            try
            {
                summary.settingsChanged = EnsurePlayerSettings().ToArray();
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

                summary.defines = AddDefines(originalDefines);
                PlayerSettings.SetScriptingDefineSymbols(target, summary.defines);

                if (!BuildAddressables(summary)) return Finish(summary, total);

                // Content is already built above; stop the player-build processor from building it a second time.
                if (AddressableAssetSettingsDefaultObject.Settings.BuildAddressablesWithPlayerBuild == AddressableAssetSettings.PlayerBuildOption.PreferencesValue)
                {
                    EditorPrefs.SetBool(BuildAddressablesWithPlayerPref, false);
                }

                BuildPlayer(summary);
            }
            catch (Exception e)
            {
                summary.result = "Exception";
                summary.errors = summary.errors.Append(e.ToString()).ToArray();
            }
            finally
            {
                PlayerSettings.SetScriptingDefineSymbols(target, originalDefines);
                EditorUserBuildSettings.buildAppBundle = originalAppBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = originalExport;
                if (hadAddressablesPref)
                {
                    EditorPrefs.SetBool(BuildAddressablesWithPlayerPref, originalAddressablesPref);
                }
                else
                {
                    EditorPrefs.DeleteKey(BuildAddressablesWithPlayerPref);
                }

                // The build saves ProjectSettings.asset while the demo defines (and Localization's preloaded asset) are set.
                AssetDatabase.SaveAssets();
            }

            return Finish(summary, total);
        }

        #endregion

        #region Helpers

        /// <summary>IL2CPP + ARM64 are required on the Playground; everything else (package id, API levels, graphics APIs) is kept.</summary>
        static List<string> EnsurePlayerSettings()
        {
            var changed = new List<string>();
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                changed.Add("scriptingBackend=IL2CPP");
            }

            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
            {
                PlayerSettings.Android.targetArchitectures |= AndroidArchitecture.ARM64;
                changed.Add("targetArchitectures+=ARM64");
            }

            return changed;
        }

        static string AddDefines(string defines)
        {
            var list = defines.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
            foreach (var define in DemoDefines)
            {
                if (!list.Contains(define)) list.Add(define);
            }

            return string.Join(";", list);
        }

        static bool BuildAddressables(Summary summary)
        {
            var watch = Stopwatch.StartNew();
            AddressableAssetSettings.BuildPlayerContent(out var result);
            summary.addressablesSeconds = watch.Elapsed.TotalSeconds;
            if (string.IsNullOrEmpty(result.Error)) return true;

            summary.result = "AddressablesFailed";
            summary.errors = new[] { result.Error };
            return false;
        }

        static void BuildPlayer(Summary summary)
        {
            if (EditorApplication.isCompiling) throw new InvalidOperationException("Scripts are compiling; retry when the Editor is idle.");

            Directory.CreateDirectory(Path.GetDirectoryName(summary.apkPath)!);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = summary.apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
                // Also passed here so the player scripts get the defines even if the PlayerSettings change is not picked up.
                extraScriptingDefines = DemoDefines,
            };
            var watch = Stopwatch.StartNew();
            // Inside namespace Nex, BuildPipeline binds to Nex.BuildPipeline, which has no BuildPlayer.
            var report = UnityEditor.BuildPipeline.BuildPlayer(options);
            summary.playerSeconds = watch.Elapsed.TotalSeconds;
            summary.result = report.summary.result.ToString();
            summary.totalErrors = report.summary.totalErrors;
            summary.totalWarnings = report.summary.totalWarnings;
            summary.errors = report.steps
                .SelectMany(step => step.messages)
                .Where(message => message.type is LogType.Error or LogType.Exception)
                .Select(message => message.content)
                .Distinct()
                .Take(50)
                .ToArray();
        }

        static Summary Finish(Summary summary, Stopwatch total)
        {
            summary.totalSeconds = total.Elapsed.TotalSeconds;
            summary.apkSizeBytes = File.Exists(summary.apkPath) ? new FileInfo(summary.apkPath).Length : 0;
            var directory = Path.GetDirectoryName(summary.apkPath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.ChangeExtension(summary.apkPath, ".build.json"), JsonUtility.ToJson(summary, true));
            return summary;
        }

        #endregion
    }
}
