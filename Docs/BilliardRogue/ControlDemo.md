# Billiard Rogue — control demo

A barely playable build for trying the paw controls:

- **Left paw**: moves the cat and the ball along the launch line.
- **Right paw**: aims the cue. The aim is the direction from the right paw to the left paw.
- **Strike**: thrust the right paw fast into the left paw to shoot.

The demo adds three things to the normal game:

| | What it does |
|---|---|
| **Practice mode** | God mode and infinite balls. Enemies stop one row above the red danger row. A run never ends, so you can keep shooting. |
| **Control readout** | A panel in the lower right corner, one per player. It shows what the body tracking sees and why a thrust did or did not shoot. It appears during the calibration test strike and during gameplay. |
| **Live tuning** | Debug Settings rows that scale the strike speed, the contact distance, the aim smoothing and the launch range while you play. |

In the Playground APK, practice mode and the readout are on from the start. In the Editor you turn them on once in
Debug Settings (see [Debug Settings](#4-debug-settings-open-it-and-tune)).

## 1. Try it in the Editor (Mac webcam)

1. Open `/Users/simonbut/project/VibeProject3/Starter` in Unity 6000.3.9f1.
2. Open `Assets/Scenes/BilliardRogue/Main.unity` and press **Play**. Set the Game view to 1920x1080 (16:9), so the layout matches the TV.
3. The first time only: open Debug Settings (arrow keys **Up Up Down Down Left Right Left Right**, or the debug button
   in the top control panel). Turn on **Cheat: Practice Mode (god, infinite balls, no danger row)** and
   **Debug: Show Control Readout**, then press **Save**. The Editor keeps these settings.
4. macOS must let Unity use the camera: **System Settings > Privacy & Security > Camera > Unity**. Without camera
   access, the readout shows `NO TRACKING: CAMERA` and the Console logs `CameraFrameProvider: Camera timeout reached`.
5. Stand about 2–3 m from the webcam. Keep your upper body in the frame, with both elbows and both wrists visible.
6. Choose **New Run**, then **1 Player**. Calibration runs four steps: move into the frame, raise a hand, a short pose
   tutorial, then a **test strike**.
7. The pose:
   - Raise the **left paw** in front of your chest. This paw is the ball.
   - Hold the **right paw** below the left paw and to its right. This paw is the cue.
   - Thrust the right paw into the left paw. One strike passes the test, and then gameplay starts.
   - In the game, move the left paw sideways to move the cat. Move the right paw around the left paw to aim. Pull
     the paws apart, then thrust again for the next ball.

In the Editor, when your paws are not tracked, the game switches to mouse and keyboard, and the readout header shows
`P1 DEBUG`. It switches back to `P1 PAW` as soon as tracking returns. The keys are: arrows move the cat, **A**/**D**
aim, **Space** strikes (**Shift+Space** is a power strike), and **S** skips a calibration step. The Editor runs at
about 10 fps while Unity is not the focused window, so click the Game view before you judge latency.

## 2. Try it on the Playground (APK)

| | |
|---|---|
| APK | `/Users/simonbut/project/VibeProject3/Builds/Android/BilliardRogue_ControlDemo.apk` (gitignored, `/[Bb]uilds/`) |
| Size | 247,632,739 bytes (about 236 MiB). The first build was 209,729,060 bytes; the development `libil2cpp.so` alone is 139 MB |
| App id | `team.nex.starter.staging` (the starter's id, unchanged), version 1.0 (1), label "Starter" |
| Contents | `Assets/Scenes/BilliardRogue/Main.unity` only (index 0), Addressables content in `assets/aa` |
| Player | Development build, IL2CPP, `arm64-v8a` only (`lib/arm64-v8a/libil2cpp.so`), OpenGL ES 3, min SDK 30, target SDK 36 |
| Defines | the project's Android defines + `BR_CONTROL_DEMO;ENABLE_DEBUG_SETTINGS`, for this build only. `ProjectSettings.asset` is restored afterwards |
| Demo defaults | `BR_CONTROL_DEMO`: practice mode and the control readout start on |
| Build time | cold: 315 s (Addressables 93 s + player 221 s). Incremental after a code change: 52–87 s |
| Summary | `Builds/Android/BilliardRogue_ControlDemo.build.json` (result, size, timings, errors), rewritten on every build |

The build dates from 2026-09-29 12:03 HKT (commit eac8d293). It includes the control lab: practice mode, the readout
and live tuning. `aapt dump badging`: package `team.nex.starter.staging`, `native-code: 'arm64-v8a'`, leanback
launchable `com.unity3d.player.UnityPlayerActivity`.

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

The app shares its id with the starter's staging app, so installing it replaces that app on the device. The APK is
signed with the local Unity debug keystore. If the device holds a copy signed with another key, `adb install -r`
fails with `INSTALL_FAILED_UPDATE_INCOMPATIBLE`. In that case, uninstall the old app first with
`adb uninstall team.nex.starter.staging`. This also deletes that app's saved data.

On the device, play the same way as in the Editor: **New Run**, **1 Player**, calibration, then the test strike.
Stand 2–3 m from the Playground camera. If Debug Settings were saved on this device with practice mode or the readout
turned off, they stay off. Turn them back on in Debug Settings.

## 3. Reading the control readout

The readout panel sits in the lower right, over the balls panel, and never covers the arena. With 2 players, P1's
panel is on top and P2's is below. The panel hides while pause, rewards, stage intros or Debug Settings are open.

| Row | Meaning |
|---|---|
| `P1 PAW` / `P1 DEBUG` / `P1 BOT` | The input the game is using right now: body tracking, mouse and keyboard, or the auto-aim bot. The body values below always come from the camera, even while another input is active. |
| `TRACKED` / `ACQUIRING` / `NO TRACKING: PAWS` / `NO TRACKING: CAMERA` | Body tracking state. `PAWS`: camera frames arrive, but the chest, both elbows and both wrists are not all visible. `CAMERA`: no camera frames. `ACQUIRING`: the paws were just found (about 0.2 s). |
| `X 0.42` | Launch position, from 0 (left wall) to 1 (right wall). It comes from the left paw's sideways offset from the chest. |
| `AIM 87°` | Aim angle. 90° is straight up. It is clamped to 12°–168°. |
| `R-HAND` / `L-HAND` | Which paw is the cue. Left-handed swaps the paws. |
| `SPEED 42 in/s` + `NEED 35` + bar | How fast the right paw closes in on the left paw, in inches per second, measured relative to your body size. `NEED` is the strike threshold. On the bar, the white tick is the threshold, the pink tick is the power-shot speed (2x the threshold), and the faint mark is the peak of the last second. The fill is yellow below the threshold, green above it, and pink at power speed. |
| `DIST 12.3 in` + `CONTACT 5.0` + bar | Distance between the paws. The paws must come within `CONTACT` for a strike. On the bar, the white tick is the contact distance and the grey tick is the arm distance (10 in): the paws must open past it before the next strike. |
| `ARMED` / `THRUST` / `COOLDOWN 0.21s` / `OPEN PAWS` | Strike detector state. `ARMED`: ready. `THRUST`: a fast approach is in progress. `COOLDOWN`: re-arm delay after a strike (0.35 s). `OPEN PAWS`: pull the paws apart past the arm distance first. |
| `STRIKES 3` | Strikes detected since this input was created. The count restarts when gameplay begins after calibration. |
| `LAST 42 in/s POWER` | Peak speed of the last strike, and whether it was a power shot. |
| Log (newest on top) | `STRIKE 42 in/s [POWER]`: a strike fired. `missed: too slow 21 in/s`: the paws touched, but the thrust peaked at 21 in/s, below the threshold. `missed: paws too far 8.1 in`: a fast thrust stopped 8.1 in short of contact. `missed: short thrust`: the paws touched fast, but the thrust started too close (under 4 in of travel). `cooldown`: a fast touch within 0.35 s of the last strike. `not armed: open paws`: a fast touch before the paws were pulled apart. `dropped: no tracking`: a strike arrived while tracking was not confirmed. `expired: not fired`: a strike was detected but the game could not fire it in time (for example during the shot cooldown, the enemy phase or a hand-off). |

## 4. Debug Settings: open it and tune

To open Debug Settings, press **Up Up Down Down Left Right Left Right** on the remote (arrow keys in the Editor), or
use the debug button in the top control panel. Change a number with Left/Right on its row, and toggle a switch by
selecting it (Enter, or OK on the remote). Then press **Save**:
Back closes the panel without applying your changes. The values are saved on the device and apply immediately,
including mid-run. The asset values in `ControlConfig` are not changed: the game uses asset value x scale.

Try these rows first:

| Row | Try it when | Values to try |
|---|---|---|
| **Input: Strike Speed x** | Thrusts don't fire and the log says `missed: too slow`. Lower it. Strikes fire by accident. Raise it. | 0.7–0.8 (easier), 1.2–1.4 (harder). It scales the threshold and the power-shot speed together. |
| **Input: Contact Distance x** | The log says `missed: paws too far`. Raise it. | 1.3–1.6. The arm distance grows with it when needed. |
| **Input: Aim Smoothing x** | The aim guide shakes while you hold still. Raise it. The aim lags behind your paw. Lower it. | 1.5–2 (steadier), 0.6–0.8 (quicker) |
| **Input: Launch Range x** | You must stretch too far to reach the walls. Lower it. The cat is too twitchy. Raise it. | 0.7–0.8 (less arm travel), 1.2 (calmer) |
| **Input: Toggle Left-Handed Cue** | You play with the right paw as the ball. This is also in Settings > Left-handed cue. | — |
| **Cheat: Practice Mode** / **Debug: Show Control Readout** | Turn the demo helpers off or on. | — |

All four scales range from 0.25 to 3, in steps of 0.05, and 1 is the default. Leave **Flow: Auto Aim Bot** and
**Input: Force Mouse/Keyboard** off, or the paws are ignored.

## 5. What feedback to send

Please note the Debug Settings scales you used. A photo of the readout at the moment something felt wrong helps a lot.

- **Strike sensitivity.** Too sensitive: it fires when you didn't mean it to, for example while moving the left paw
  or while resetting the paws. Not sensitive enough: thrusts that don't fire. Which log line appeared (`too slow` with
  its speed, `paws too far` with its distance, `cooldown`, `open paws`)? How hard do you have to thrust, and is a
  power shot reachable?
- **Aim jitter.** Does the aim guide shake while you hold still? Does the aim jump when you thrust? Does it lag
  behind the right paw?
- **Launch range.** Can you reach both walls comfortably? Where does the cat sit when your left paw rests in front
  of your chest?
- **Latency.** How long from the thrust to the ball launch? How long before the cat and the aim follow your paws?
- **Tracking.** How often did `NO TRACKING: PAWS` appear, and at what distance and lighting? Which pose lost it?
- **The pose itself.** Is the left-paw-ball, right-paw-cue pose comfortable over a few minutes? What would feel more
  natural?

## Rebuild

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

## Where the code lives

| Feature | Code |
|---|---|
| Practice mode | `PlayerData/DebugSettings.cs` (`practiceMode`), `Gameplay/SessionServices.cs` (`GodMode`, `InfiniteBalls`, `HoldEnemiesBeforeDangerRow`), `Simulation/EnemyAdvance.cs` |
| Readout | `UI/Debug/ControlReadoutOverlay.cs`, `UI/Debug/ControlReadoutPanel.cs`, prefab from `Editor/ControlReadoutBuilder.cs` (run by `FlowPrefabsBuilder`); data from `StrikeDetector.Readout` and `PawShotInput` |
| Live tuning | `Input/ControlTuning.cs`, `Configs/ControlConfig.cs` (`StrikeSettingsFor`, `AimMinCutoffFor`, `LaunchRangeFor`), read in `Flow/PlayerShotInputFactory.cs` |
| Demo defaults | `#if BR_CONTROL_DEMO` initializers in `PlayerData/DebugSettings.cs` |
