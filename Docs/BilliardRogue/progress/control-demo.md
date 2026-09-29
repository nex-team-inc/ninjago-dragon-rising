# Control demo — checkpoint

Goal: a barely playable Android development APK, so the user can try the paw controls on a Nex Playground:
- LEFT paw: ball position along the launch line.
- RIGHT paw: cue direction.
- A fast right-paw strike into the left paw: shoot.

## Build engineer (Android development APK): done

- [x] Checked the setup:
  - Android is the active target. IL2CPP, ARM64 only, OpenGLES3 only. App id `team.nex.starter.staging`, min SDK 30.
  - No build profiles. Addressables 2.9.1 with `BuildAddressablesWithPlayerBuild = PreferencesValue`.
  - `/[Bb]uilds/` was already in the root `.gitignore`.
- [x] `Starter/Assets/Scripts/BilliardRogue/Editor/ControlDemoBuild.cs`: `Nex.BilliardRogue.Editor.ControlDemoBuild.Run()`,
      with the menu item **Nex > Billiard Rogue > Build Control Demo APK**.
- [x] Built `Builds/Android/BilliardRogue_ControlDemo.apk`. The cold build succeeded the first time, with 0 errors, in 315 s.
      The incremental rebuild took 32 s. Size is 209,729,060 bytes.
- [x] Fixes after build 1:
  - The build had saved `ProjectSettings.asset` with the demo defines and Localization's temporary preloaded asset.
    `Run()` now calls `AssetDatabase.SaveAssets()` after restoring. Rebuild 2 left `ProjectSettings.asset` clean.
  - Committed the build-written Unity data: URP prefilter flags, the URP runtime settings list, and Addressables'
    `ProfileDataSourceSettings.asset`.
  - Gitignored `Starter/SerializedBuildInfo/` (Asset Hunter Pro build logs).
- [x] Verified with `aapt dump badging`: package `team.nex.starter.staging`, `native-code: 'arm64-v8a'`, leanback launchable
      `com.unity3d.player.UnityPlayerActivity`. `lib/arm64-v8a/libil2cpp.so` is present, and so are the debug hook types
      (`DebugHooks`, `ShotInputDebugCommands`).
- [x] Install and launch steps are in `Docs/BilliardRogue/ControlDemo.md`.

Note: no code read `BR_CONTROL_DEMO` when the APK was built. Rebuild after demo code guarded by that define lands.
The rebuild takes about 30 s.

## Control lab (practice mode, control readout, live tuning): in progress

Resume: read this section + `git log --oneline -15`. Editor work goes through `Tools/editor_lock.sh demo …`.

- [x] 0. Paused perf pass edits reviewed and committed (7f6da517: shadow flags in the builders, board teardown guard).
- [ ] 1. `DebugSettings.practiceMode`: god mode + infinite balls, enemies stop one row above the danger row.
- [ ] 2. Control readout overlay (`showControlDebug`): calibration test strike + gameplay, right column.
- [ ] 3. Live tuning rows (strike speed, contact distance, aim smoothing, launch range) + left-handed row.
- [ ] 4. `#if BR_CONTROL_DEMO` defaults (showControlDebug, practiceMode on).
- [ ] 5. Editor verification (screenshots in scratchpad `demo/`), EditMode tests.
- [ ] 6. APK rebuild.
- [ ] 7. `Docs/BilliardRogue/ControlDemo.md` how-to.
