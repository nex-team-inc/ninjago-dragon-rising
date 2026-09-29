# Billiard Rogue — control demo

A barely playable build for trying the paw controls:

- **Upper paw** (either hand, whichever is higher): holds the ball and moves it along the launch line.
- **Lower paw**: the cue. The aim is the direction from the lower paw to the upper paw.
- **Strike**: thrust the lower paw fast up into the upper paw to shoot.

The demo adds three things to the normal game:

| | What it does |
|---|---|
| **Practice mode** | God mode: a run never ends. Off by default since playtest 4 (the player loses HP as in the game); turns still end after the turn's shot and the enemies still advance. |
| **Control readout** | A panel in the lower right corner, one per player. It shows what the body tracking sees and why a thrust did or did not shoot. It appears during the calibration test strike and during gameplay once you turn it on (hidden by default since the second playtest). |
| **Live tuning** | Debug Settings rows that scale the strike speed, the contact distance, the aim smoothing and the launch range while you play. |

In the Playground APK, practice mode and the readout both start off. Turn the readout on in Debug Settings
with **Debug: Show Control Readout** (see [Debug Settings](#4-debug-settings-open-it-and-tune)). In the Editor you turn
both on once in Debug Settings.

## What's new after the fifth playtest (GDD v2 §16–§18)

| | Behaviour now | How to try it |
|---|---|---|
| **Upper paw = ball, lower paw = cue** | Roles follow height, not hands: whichever paw is higher holds the ball and the lower one is the cue, so either hand can shoot. The roles swap only when the lower paw stays at least 4 in above the other for 0.25 s, and never in the middle of a strike (a thrust that ends above the ball paw does not swap them). The left-handed setting is gone. The calibration card shows the new pose. | Hold one paw high, thrust the other up into it. Then swap which hand is up. |
| **One shot a turn** | Each player has **1 shot a turn** (still a volley of the whole bag). The panel reads **Shots 1/1**; a +1 Ball pickup still adds a bonus volley. Enemy HP was lowered to match (see §18). | Shoot once: the enemies step forward right after. |
| **Dance again, with energy** | While your balls fly, dancing fills the POWER bar (up to 1.8× faster, 2.5× stronger balls). The bar never drops on its own and resets at the start of every turn. Dancing **costs energy**: a full bar costs 12, you start a run with 12, every enemy you defeat gives 1 (a boss 10), and you hold at most 30. With no energy dancing does nothing and the meter says **No energy!**. The energy count is at the top right of the POWER panel. **MOVE!** only appears when you have energy to spend. | Shoot, then dance. Save energy by standing still if you want a full bar for a boss. |

## What's new after the fourth playtest (GDD v2 §10–§15)

| | Behaviour now | How to try it |
|---|---|---|
| **Every shot is a volley** | You have **3 shots a turn** (1 since the fifth playtest), and each shot launches **every ball in your bag**, one after another along the aim you struck. Every ball you win makes every later shot stronger. The panel in the top right reads **Shots 3/3**. A +1 Ball pickup gives one extra volley this turn. | Strike once and watch the whole bag stream out. |
| **POWER comes from combos** | Replaced after the fifth playtest (dancing with energy, above). No dancing: every enemy hit while balls fly adds to the combo, and **24 hits give full POWER** (balls up to 1.8× faster and 2.5× stronger). The meter in the lower left shows `×1.4!`, `×1.9!!` and `MAX!!!`; the board shows **x5 COMBO**, **x10 COMBO**, … and turns rainbow at full POWER. POWER drops to 0 once all balls are back; shooting again while balls still fly keeps the combo going. | Aim a volley into a crowd. |
| **3 HP** | You have 3 HP, and every attack takes exactly 1: an enemy on the red danger row, a Mage's bolt or the Bone Lich's bolt. A stage clear and a heal pickup each heal 1, and a boss clear refills. | Let an enemy reach the red row. |
| **2 players shoot together** | Both players shoot at the same time from their own spot, each with 3 shots, their own cue and aim line. Enemies have double HP. | Start a 2-player run. |
| **Tougher enemies** | Enemy HP is doubled and grows faster every stage to keep up with the volleys, so HP numbers climb into the hundreds late in a run. | Play into act 2. |

## What's new after the second playtest (GDD v2 §5–§6)

| | Behaviour now | How to try it |
|---|---|---|
| **Enemies pop in** | Enemies no longer march in as rows from the top. They appear **from nowhere at random free cells**, each with a portal flash, a dust puff and a pop sound, one after another (0.06 s apart), scaling up from nothing with a little bounce. Pickups pop in the same way. Nothing ever appears in the **3 rows nearest the cat** (with 10 rows: only rows 1–7 counted from the top; the red danger row and the two above it stay clear). Every row keeps at least one free cell. | Start a run: after the stage banner the first batch pops in, with an **"Enemies incoming!"** ribbon at the top. |
| **Batches of 10 every 3 turns** | A batch has **at least 10 enemies** (fewer only when the free cells run out) plus 2 pickups. A stage has 3 batches: on turn 1, turn 4 and turn 7. Enemies still move down one row per enemy phase and attack from the danger row. | Watch the turn banner: new enemies arrive with turns 4 and 7. |
| **No empty turns** | When the field is empty (Bone Walls do not count), your turn ends right away, and if the stage still has a batch it arrives immediately. The turn counter jumps to that batch's turn, so after clearing the field on turn 2 the next banner reads **Turn 4**. With no batch left, an empty field clears the stage. | Clear the field before turn 4. |
| **Boss stages** | The boss appears at the top centre (2×2, rows 1–2) together with the first escort batch. More escort batches (10 each, fewer when the board is full) arrive every 3 turns while the boss lives, and none after it dies. The bosses' own summons are unchanged. | Debug Settings **Run: Force Start Stage** 3, 7 or 11, or `DebugHooks.GotoStage(3)`. |
| **Board sounds** | Hits, bounces, launches, enemy steps and deaths, pickups and the pop-ins now make sound. A bug had silenced every sound effect on the board before (the HUD and menu sounds were fine). | Shoot a ball. |
| **Debug overlay hidden** | The control readout (the debug canvas in the lower right) is **off by default** in every build, this demo included. The **Debug: Show Control Readout** toggle still brings it back. A value saved by an older demo is ignored once, so the readout starts hidden after the update. The debug printer stays off by default too. | Open Debug Settings and turn **Debug: Show Control Readout** on. |

The numbers are config values: `ActRules.minEnemiesPerBatch` (10), `spawnEveryNTurns` (3), `batchesPerStage` (3),
`spawnForbiddenNearRows` (3), `pickupsPerBatch` (2) and `batchBudgetRows` (4) in `Assets/Configs/BilliardRogue/Acts/Act_1..3`;
the stagger and the ribbon time are `PacingConfig.batchSpawnStagger` (0.06 s) and `incomingBannerDuration` (0.9 s).
Balance check with the simulation bot (6 seeds): an aiming bot wins every run, and a bot that shoots 60% of its balls at
random still wins 5 of 6, so the batches are not too hard. A run saved before this update continues its stage with the new rules.

## What's new in v2 (after the first device playtest)

The spec is `GDD-v2-Changes.md`. What changes when you play:

| | v2 behaviour | How to try it |
|---|---|---|
| **Easier strike** | A strike starts when the cue paw moves toward the ball paw at **19 in/s** or more (v1: 35), heading within 60° of it. It fires on contact within **9 in** (v1: 5), or when the cue paw **passes the ball paw's line** no more than 12 in beside it. Re-arm is **0.25 s**, and the paws must first be **14 in** apart. A power shot needs 3.5× the threshold (about 66 in/s, close to v1's 70). | Thrust the cue paw at the ball paw. It no longer has to touch. The readout log shows `STRIKE … (line)` when the strike fired by passing the ball paw. |
| **POWER (was Hype)** | Charged by the combo after the fourth playtest, and by dancing again after the fifth, now paid with energy (see above). The effects are the same: every flying ball up to **1.8× faster** and **2.5× stronger** (+1 damage at least from tier 2), plus more hit-stop, shake, sparks, bigger damage numbers, a glowing ball with a tier-coloured trail and a pink arena aura at MAX. | Aim a volley into a crowd and watch the **POWER** meter. |
| **Rewards are balls only** | Every reward offers 3 different balls: a big ball picture, its name and a 1–3 word effect ("Burns", "Chain zap", …). A new ball joins the bag. When the bag is full, only balls you own are offered, and a pick levels one up (`Lv 1 → Lv 2`). Heal and Max HP cards are gone. Instead, every stage clear heals (1 of your 3 HP since the fourth playtest). | Clear a stage. |
| **Pick a ball with your hands** | Two cat arms rise from the bottom corners, and each paw follows your hand. Put **both paws on the same ball** and hold them there: a ring fills in 0.8 s, then the paws grab the ball and pull it down. Moving a paw away drains the ring. A paw must move a little after the view opens before a hold counts, so resting hands never pick by accident. The remote still works: **Left/Right + OK**. In 2P the players take turns choosing (P1, then P2, …). A "P2 picks!" banner shows whose turn it is, and only that player's arms show: P1 orange, P2 charcoal. | Bring both hands together over one ball and keep them still. |
| **Readout: Hype row** | `ENERGY 0.62`: the body's motion energy (shown for reference; POWER no longer uses it since the fourth playtest). `BODY 0.58 11/11 23in/s`: the body meter itself, with its energy, the body nodes it sees (of 11), and their mean speed above the jitter deadzone. `BODY -` means the body is not tracked. | Watch it while you move. |

In the Editor without a body:
- The mouse drives both paws in the reward view while it is inside the Game view. Wiggle it a little first, so the
  hold arms.
- Debug Settings **Run: Force POWER while balls fly** (0..1), or `DebugHooks.SetHype(0.6f)`, forces POWER while balls
  fly, and `-1` turns it off.

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
   - Raise one paw in front of your chest. The **upper paw** is the ball.
   - Hold the other paw below it. The **lower paw** is the cue.
   - Thrust the lower paw up into the upper paw. One strike passes the test, and then gameplay starts.
   - In the game, move the upper paw sideways to move the ball. Move the lower paw around below it to aim. Pull
     the paws apart, then thrust again for the next shot.

In the Editor, when your paws are not tracked, the game switches to mouse and keyboard, and the readout header shows
`P1 DEBUG`. It switches back to `P1 PAW` as soon as tracking returns. The keys are: arrows move the cat, **A**/**D**
aim, **Space** strikes (**Shift+Space** is a power strike), and **S** skips a calibration step. The Editor runs at
about 10 fps while Unity is not the focused window, so click the Game view before you judge latency.

## 2. Try it on the Playground (APK)

| | |
|---|---|
| APK | `/Users/simonbut/project/VibeProject3/Builds/Android/BilliardRogue_ControlDemo.apk` (gitignored, `/[Bb]uilds/`) |
| Size | 247,676,948 bytes (about 236 MiB). The first build was 209,729,060 bytes; the development `libil2cpp.so` alone is 139 MB |
| App id | `team.nex.starter.staging` (the starter's id, unchanged), version 1.0 (1), label "Starter" |
| Contents | `Assets/Scenes/BilliardRogue/Main.unity` only (index 0), Addressables content in `assets/aa` |
| Player | Development build, IL2CPP, `arm64-v8a` only (`lib/arm64-v8a/libil2cpp.so`), OpenGL ES 3, min SDK 30, target SDK 36 |
| Defines | the project's Android defines + `BR_CONTROL_DEMO;ENABLE_DEBUG_SETTINGS`, for this build only. `ProjectSettings.asset` is restored afterwards |
| Demo defaults | `BR_CONTROL_DEMO`: practice mode and the control readout start off (GDD v2 §6, §10) |
| Build time | cold: 315 s (Addressables 93 s + player 221 s). Incremental after a code change: 52–92 s |
| Summary | `Builds/Android/BilliardRogue_ControlDemo.build.json` (result, size, timings, errors), rewritten on every build |

The build dates from 2026-09-29 14:13 HKT (commit dc4f84f3, 85 s incremental). It includes the control lab (practice
mode, the readout and live tuning), all of v2 (the easier strike, Hype, balls-only rewards and the paw pick), the
second-playtest changes (batch pop-in spawning, skipped empty turns, readout hidden by default) and perf pass 3. It is
installed on the Playground at 10.4.6.137, where it starts on the title screen with no Unity errors or warnings in logcat. `aapt dump badging`: package `team.nex.starter.staging`, `native-code: 'arm64-v8a'`, leanback
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
Stand 2–3 m from the Playground camera. If Debug Settings were saved on this device with practice mode turned off, it
stays off; turn it back on in Debug Settings. The readout starts hidden even where an older demo saved it on (the setting
was renamed); turn it on with **Debug: Show Control Readout** when you want it.

## 3. Reading the control readout

The readout panel sits in the lower right, over the balls panel, and never covers the arena. With 2 players, P1's
panel is on top and P2's is below. The panel hides while pause, rewards, stage intros or Debug Settings are open.

| Row | Meaning |
|---|---|
| `P1 PAW` / `P1 DEBUG` / `P1 BOT` | The input the game is using right now: body tracking, mouse and keyboard, or the auto-aim bot. The body values below always come from the camera, even while another input is active. |
| `TRACKED` / `ACQUIRING` / `NO TRACKING: PAWS` / `NO TRACKING: CAMERA` | Body tracking state. `PAWS`: camera frames arrive, but the chest, both elbows and both wrists are not all visible. `CAMERA`: no camera frames. `ACQUIRING`: the paws were just found (about 0.2 s). |
| `X 0.42` | Launch position, from 0 (left wall) to 1 (right wall). It comes from the upper (ball) paw's sideways offset from the chest. |
| `AIM 87°` | Aim angle. 90° is straight up. It is clamped to 12°–168°. |
| `BALL L` / `BALL R` | Which paw is up and holds the ball (the other one is the cue). |
| `SPEED 42 in/s` + `NEED 19` + bar | How fast the cue paw closes in on the ball paw, in inches per second, measured relative to your body size. `NEED` is the strike threshold. On the bar, the white tick is the threshold, the pink tick is the power-shot speed (3.5x the threshold), and the faint mark is the peak of the last second. The fill is yellow below the threshold, green above it, and pink at power speed. |
| `DIST 12.3 in` + `CONTACT 9.0` + bar | Distance between the paws. The paws must come within `CONTACT` for a strike, or the cue paw must pass the ball paw's line. On the bar, the white tick is the contact distance and the grey tick is the arm distance (14 in): the paws must open past it before the next strike. |
| `ARMED` / `THRUST` / `COOLDOWN 0.21s` / `OPEN PAWS` | Strike detector state. `ARMED`: ready. `THRUST`: a fast approach is in progress. `COOLDOWN`: re-arm delay after a strike (0.25 s). `OPEN PAWS`: pull the paws apart past the arm distance first. |
| `STRIKES 3` | Strikes detected since this input was created. The count restarts when gameplay begins after calibration. |
| `LAST 42 in/s POWER` | Peak speed of the last strike, and whether it was a power shot. |
| `ENERGY 0.62` + `BODY 0.58 11/11 23in/s` | Hype input (v2). `ENERGY` is the motion energy the game uses: the body meter, or the debug key, mouse or bot simulation if that is higher. It turns pink from 0.25, and red when nothing is tracked. `BODY` is the camera's own meter: energy, body nodes seen out of 11, and mean node speed above the 8 in/s jitter deadzone. 30 in/s gives full energy. |
| Log (newest on top) | `STRIKE 42 in/s [POWER] [(line)]`: a strike fired, by contact or, with `(line)`, by passing the ball paw. `missed: too slow 21 in/s`: the paws touched, but the thrust peaked at 21 in/s, below the threshold. `missed: paws too far 8.1 in`: a fast thrust stopped 8.1 in short of contact. `missed: short thrust`: the paws touched fast, but the thrust started too close (under 4 in of travel). `cooldown`: a fast touch within 0.25 s of the last strike. `not armed: open paws`: a fast touch before the paws were pulled apart. `dropped: no tracking`: a strike arrived while tracking was not confirmed. `expired: not fired`: a strike was detected but the game could not fire it in time (for example during the shot cooldown, the enemy phase or a hand-off). |

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
| **Input: Contact Distance x** | The log says `missed: paws too far`. Raise it. | 1.3–1.6. It also scales the 12 in line-cross distance, and the arm distance grows with it when needed. |
| **Input: Aim Smoothing x** | The aim guide shakes while you hold still. Raise it. The aim lags behind your paw. Lower it. | 1.5–2 (steadier), 0.6–0.8 (quicker) |
| **Input: Launch Range x** | You must stretch too far to reach the walls. Lower it. The cat is too twitchy. Raise it. | 0.7–0.8 (less arm travel), 1.2 (calmer) |
| **Cheat: Practice Mode** / **Debug: Show Control Readout** | Turn the demo helpers off or on. | — |

All four scales range from 0.25 to 3, in steps of 0.05, and 1 is the default. Leave **Flow: Auto Aim Bot** and
**Input: Force Mouse/Keyboard** off, or the paws are ignored.

## 5. What feedback to send

Please note the Debug Settings scales you used. A photo of the readout at the moment something felt wrong helps a lot.

- **Strike sensitivity.** Too sensitive: it fires when you didn't mean it to, for example while moving the ball paw
  or while resetting the paws. Not sensitive enough: thrusts that don't fire. Which log line appeared (`too slow` with
  its speed, `paws too far` with its distance, `cooldown`, `open paws`)? How hard do you have to thrust, and is a
  power shot reachable?
- **Aim jitter.** Does the aim guide shake while you hold still? Does the aim jump when you thrust? Does it lag
  behind the cue paw?
- **Launch range.** Can you reach both walls comfortably? Where does the cat sit when your ball paw rests in front
  of your chest?
- **Latency.** How long from the thrust to the ball launch? How long before the cat and the aim follow your paws?
- **Tracking.** How often did `NO TRACKING: PAWS` appear, and at what distance and lighting? Which pose lost it?
- **The pose itself.** Is the left-paw-ball, right-paw-cue pose comfortable over a few minutes? What would feel more
  natural?
- **Upper / lower paw (fifth playtest).** Does the ball stay with the hand you expect? Did the roles ever swap
  when you did not want them to, for example right after a strike?
- **Dance and energy (fifth playtest).** Is 12 energy for a full bar too stingy or too generous? Do you notice
  **No energy!**, and does saving energy for a boss feel worth it?
- **Volleys and POWER (fourth playtest).** Does a volley feel strong, and does a new ball feel like it matters? How
  often do you reach `MAX!!!`? Are the COMBO floats readable with a full bag? Is 3 HP too few, and which attacks hit you?
- **Paw pick (v2).** Can you put both paws on the ball you want? Is 0.8 s of holding too long or too short? Did a
  ball ever get picked by accident?
- **Spawning (second playtest).** Are 10 enemies every 3 turns too many or too few? Can you tell where the next ones
  appear, and is the pop-in fast enough? Did a turn ever start with nothing to shoot?

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
| Demo defaults | `#if BR_CONTROL_DEMO` initializers in `PlayerData/DebugSettings.cs` (the readout is `showControlReadout`, off everywhere) |
| Batch spawning (v2 §5) | `Simulation/StageGenerator.cs` (batch plan), `Simulation/BoardSpawning.cs` (free-cell placement, via `BoardOps.SpawnBatch`), `Simulation/StageSchedule.cs` (cadence, skipped turns, v1 save upgrade), values in `ActRules` (Act assets upgraded by `Editor/ActSpawnRulesUpgrade.cs`) |
| Pop-in + "Enemies incoming!" | `Presentation/BatchSpawnPlayer.cs`, `Gameplay/BatchArrivals.cs` (stage-start pop-in after the intro), `UI/Hud/GameplayHud.ShowIncomingBanner`, timings in `PacingConfig` (`batchSpawnStagger`, `incomingBannerDuration`) |
| Strike (v2) | `Input/Core/StrikeDetector.cs`, `Input/Core/StrikeTypes.cs`, values in `Configs/ControlConfig.cs` (asset upgraded by `Editor/ControlConfigV2Upgrade.cs`) |
| Motion energy + paw pointer | `Input/MotionEnergyMeter.cs`, `Input/Core/MotionEnergyFilter.cs`, `Input/PawPointer.cs`, `Input/Core/PawPointerMath.cs`, routed by `Input/RoutedBodyInput.cs` (`ShotInputRouter.MotionEnergy` / `.PawPointer`) |
| Hype | `Gameplay/HypeController.cs` (energy → Hype, tiers, MOVE prompt), `Configs/HypeConfig.cs` + `HypeConfig.asset`, `Simulation/BallSimulator.SetHype` (speed, damage), `Presentation/HypeJuice.cs` + `HypeAuraView.cs` (look), `UI/Hud/HypeMeterWidget.cs` + `MovePromptWidget.cs` |
| Reward pick | `UI/Views/RewardView.cs`, `UI/Views/RewardPawPicker.cs`, `UI/Widgets/RewardBallOption.cs` + `PawArm.cs`, `Simulation/RewardGenerator.cs` (balls only), hold tuning in `UiTheme` (`rewardHoldSeconds`, `rewardPawArmDistance`) |
| Readout Hype row | `UI/Debug/ControlReadoutEnergyRow.cs` |
