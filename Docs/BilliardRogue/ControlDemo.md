# Billiard Rogue — control demo

## Android build (Nex Playground)

| | |
|---|---|
| APK | `/Users/simonbut/project/VibeProject3/Builds/Android/BilliardRogue_ControlDemo.apk` (gitignored, `/[Bb]uilds/`) |
| Size | 209,729,060 bytes (about 200 MB) |
| App id | `team.nex.starter.staging` (the starter's id, unchanged), version 1.0 (1), label "Starter" |
| Contents | `Assets/Scenes/BilliardRogue/Main.unity` only (index 0), Addressables content in `assets/aa` |
| Player | Development build, IL2CPP, `arm64-v8a` only (`lib/arm64-v8a/libil2cpp.so`), OpenGL ES 3, min SDK 30, target SDK 36 |
| Defines | the project's Android defines + `BR_CONTROL_DEMO;ENABLE_DEBUG_SETTINGS`, for this build only. `ProjectSettings.asset` is restored afterwards |
| Build time | cold: 315 s (Addressables 93 s + player 221 s). Incremental rebuild: 32 s |
| Summary | `Builds/Android/BilliardRogue_ControlDemo.build.json` (result, size, timings, errors), rewritten on every build |

The build dates from 2026-09-29 11:24 HKT. It includes the code in the tree at that time. At that point no code
read `BR_CONTROL_DEMO` yet. Code guarded by that define only ships after a rebuild.

### Install and launch

```bash
adb devices                                   # the Playground must be listed (USB or `adb connect <ip>:5555`)
adb install -r /Users/simonbut/project/VibeProject3/Builds/Android/BilliardRogue_ControlDemo.apk
adb shell am start -n team.nex.starter.staging/com.unity3d.player.UnityPlayerActivity
# or through the TV launcher intent:
adb shell monkey -p team.nex.starter.staging -c android.intent.category.LEANBACK_LAUNCHER 1
adb logcat -s Unity                           # game logs
adb shell am force-stop team.nex.starter.staging
```

The app shares its id with the starter's staging app, so installing replaces that app on the device. The APK is
signed with the local Unity debug keystore. If the device holds a copy signed with another key, `adb install -r`
fails with `INSTALL_FAILED_UPDATE_INCOMPATIBLE`. In that case, uninstall the old app first with
`adb uninstall team.nex.starter.staging`. This also deletes that app's saved data.

### Rebuild

- Editor menu: **Nex > Billiard Rogue > Build Control Demo APK**.
- CLI (open Editor, not in play mode):
  ```bash
  Tools/editor_lock.sh control-demo-build bash -c 'unity command eval "return Nex.BilliardRogue.Editor.ControlDemoBuild.Run();" 5400000 \
    --timeout 5500 --project-path /Users/simonbut/project/VibeProject3/Starter --json | jq ".data.result.result"'
  ```

`ControlDemoBuild.Run()` (`Starter/Assets/Scripts/BilliardRogue/Editor/ControlDemoBuild.cs`) runs these steps:
1. Keeps IL2CPP + ARM64 (sets them only if missing). Builds an APK, not an AAB.
2. Adds the demo defines. Builds Addressables content (`AddressableAssetSettings.BuildPlayerContent`). Turns off
   the Addressables "build with player" preference for the player build, so the content is not built twice.
3. Runs `BuildPipeline.BuildPlayer` with `BuildOptions.Development`.
4. In `finally`, restores the defines, the Android build flags and the Addressables preference. Then it saves the
   assets, because the build writes `ProjectSettings.asset` while the demo defines are set.

After a build the Editor recompiles once, because the defines changed back.

Every player build also rewrites some Unity files. The first build's versions are committed, so later builds leave no diff:
- URP's shader prefilter flags in `URPAsset.asset`.
- The runtime settings list in `UniversalRenderPipelineGlobalSettings.asset`.
- Addressables' `ProfileDataSourceSettings.asset`.

Asset Hunter Pro's per-build logs (`Starter/SerializedBuildInfo/`) are gitignored.
