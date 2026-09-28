# Bootstrap, Persistence, Debug Tooling, Platform Integration: Reference Note

Scope: how the starter boots global managers, persists data (Easy Save 3 via `PlayerDataManager`),
exposes developer tooling (DebugSettings panel, DebugPrinter, DebugInput, time scale), and integrates
with the Nex platform (pause, camera mute, keyboard lock). Also covers `ObjectPooler<T>` and small utils.
Everything below was read from source unless marked **(unverified)**. Paths are relative to
`/Users/simonbut/project/VibeProject3/Starter/`.

---

## 0. File map

| Thing | Path | Namespace |
|---|---|---|
| `Singleton<T>` | `Assets/Scripts/Singleton/Singleton.cs` | `Nex` |
| `SingletonSpawner` | `Assets/Scripts/Singleton/SingletonSpawner.cs` | `Nex` |
| `ScreenBlockerManager` | `Assets/Scripts/Singleton/ScreenBlockerManager.cs` | `Nex` |
| `ApplicationManager` | `Assets/Scripts/Application/ApplicationManager.cs` | `Nex` |
| `CherryIntegrationManager` | `Assets/Scripts/Application/CherryIntegrationManager.cs` | `Nex` |
| `GameConfigsManager` | `Assets/Scripts/Configs/GameConfigsManager.cs` | `Nex` |
| `VolumeManager`, `AudioMixerConstants`, `AudioMixerUtils` | `Assets/Scripts/Audio/` | `Nex` |
| `PlayerDataManager` (+ nested `AppViewState`) | `Assets/Scripts/PlayerData/PlayerDataManager.cs` | `Nex` |
| `PlayerPreference` | `Assets/Scripts/PlayerData/PlayerPreference.cs` | `Nex` |
| `DebugSettings` | `Assets/Scripts/PlayerData/DebugSettings.cs` | `Nex` |
| `RemoteConfigManager`, `RemoteConfig` | `Assets/Scripts/RemoteConfig/` | `Nex` |
| `DebugSettingsView` | `Assets/Scripts/Main/Views/Debug/DebugSettingsView.cs` | `Nex` |
| `DebugPrinter` | `Assets/Scripts/Utils/DebugPrinter.cs` | `Nex` |
| `DebugInput`, `DebugTimeScaleChanger` | `Assets/Scripts/Utils/` | `Nex.Dev` |
| `ObjectPooler<T>`, `IPoolableObject` | `Assets/Scripts/ObjectPooling/ObjectPooler.cs` | `Nex` |
| `Filter.cs` (OneEuro etc.) | `Assets/Scripts/Gameplay/Smoothing/Filter.cs` (NOT in Utils) | `Nex` |
| Utils (`History`, `FloatRange`, `RemapUtils`, `Vector2Utils`, `PlatformUtils`, `ListExtensions`, `RectUtils`, `MMFeedbacksExtension`) | `Assets/Scripts/Utils/` | `Nex.Utils` |
| Utils (`WeightedFloatHistory`, `DetectionUtils`, `TextureUtils`, `EnvironmentInfo`) | `Assets/Scripts/Utils/` | `Nex` |
| Singleton prefabs | `Assets/Prefabs/Singletons/*.prefab` | |
| Debug panel package | `Library/PackageCache/team.nex.debug-settings@c367bcc8ae17/` (v1.1.8) | `Nex.Dev`, `Nex.Dev.Attributes` |
| Easy Save 3 (v3.5.24) | `Assets/Libraries/Easy Save 3/` (no asmdef, compiles into Assembly-CSharp) | global `ES3` |
| ES3 settings asset | `Assets/Libraries/Easy Save 3/Resources/ES3/ES3Defaults.asset` | |
| Audio mixer | `Assets/Audio/AudioMixer/Main.mixer` | |
| `AppInfo` | `Library/PackageCache/team.nex.app-info-wrapper@b6253a5cd9f6/Runtime/AppInfo.cs` | `Nex.Util` |
| `GameActionDelegate`, `DeviceActionDelegate`, `Env` | `Library/PackageCache/team.nex.platform-utils@7615141761cc/Scripts/` | `Nex.Platform` |
| `CameraGuardController` | `Library/PackageCache/team.nex.camera-guard@b992c30a6285/Scripts/` | `Nex.CameraGuard` |
| `SecretCodeSequenceDetector`, `KeyboardNavigationController` | `Library/PackageCache/team.nex.keyboard-navigation@85c1b57f3096/Runtime/` | `Nex.KeyboardNavigation` |

No asmdefs exist under `Assets/Scripts`. **Keep all Billiard Rogue runtime code in Assembly-CSharp**: an asmdef
assembly cannot reference Assembly-CSharp (so it could not see `PlayerDataManager`, `ES3`, `ViewManager`), and
ES3 resolves saved types only from assemblies listed in `ES3Defaults.asset > assemblyNames`.

---

## 1. Bootstrap

### 1.1 `Singleton<T>`

```csharp
public abstract class Singleton<T> : MonoBehaviour
{
    protected virtual void Awake()     { Instance = GetThis(); }
    protected virtual void OnDestroy() { Instance = default!; }
    protected abstract T GetThis();
    public static T Instance { get; private set; } = default!;
}
```

Gotchas:
- No duplicate guard. A second copy overwrites `Instance`, and when **either** copy is destroyed, `Instance`
  becomes null. Never put a manager copy in a scene; managers belong only in the singleton prefabs.
- `Instance` is null until the spawner's async instantiate finishes. Anything that runs before that
  (for example an active scene object's `Awake`) must not touch singletons. This is why scene orchestrators start inactive.
- Override `Awake`/`OnDestroy` with `base.Awake()`/`base.OnDestroy()`.

### 1.2 `SingletonSpawner` (Addressables)

```csharp
public enum SingletonType { StartUp, Common }
[Serializable] public struct SingletonConfig { public SingletonType type; public AssetReferenceGameObject prefab; }
[SerializeField] SingletonConfig[] configs;
[SerializeField] GameObject[] activatePostSpawn;     // SetActive(true) after spawn
[SerializeField] MonoBehaviour[] enablePostSpawn;    // enabled = true after spawn
public static void KillAllSingletons();              // Destroy + Addressables.Release all spawned roots
```

Behavior (from source):
1. `Awake`: filters `configs` whose `type` is not yet in the static `singletonDict`. If none, it activates immediately.
2. Otherwise it runs `InstantiateSingletonAsync`, **sequentially in array order**: `prefab.InstantiateAsync(null, true)`,
   awaits it, calls `DontDestroyOnLoad`, and records the handle. Then `ActivatePostSpawn()`.
3. `ActivatePostSpawn()` activates `activatePostSpawn` objects, enables `enablePostSpawn` behaviours, and then
   **`Destroy(gameObject)`** destroys the spawner's own GameObject. Put nothing else on that GameObject.
4. The static dict is cleared on `SubsystemRegistration`, so it is safe with domain reload disabled.
5. Dedup is by `SingletonType`, not by prefab. A later scene's spawner skips any type that is already spawned.

Scene wiring in the examples (read from YAML):
- `GameUIExample.unity` (the main scene) has a `SingletonSpawner` GameObject with configs `[StartUp → StartUpSingletons.prefab, Common → CommonSingletons.prefab]` and `activatePostSpawn = [MainInitializerExample]`. `MainInitializerExample` is **inactive (`m_IsActive: 0`) in the scene**.
- `NonARGameExample.unity` has the same two configs and `activatePostSpawn = [NonARGameSceneExample]` (inactive), so it can run standalone.
- `ARGameExample.unity` has no spawner, so it only works when loaded from the main scene.
- `MainCoordinator.prefab` also contains a spawner with `[Common]` only and empty post-spawn lists. It is a no-op when already spawned.

Addressables (`Assets/AddressableAssetsData/AssetGroups/`):
- `Default Local Group.asset` → `Assets/Prefabs/Singletons/StartUpSingletons.prefab` (guid `67aae64b32e144c8b91c503eb4041dca`)
- `Singletons.asset` → `Assets/Prefabs/Singletons/CommonSingletons.prefab` (guid `af7dd7192fd8a423db7225be78323338`)
- Only the two roots are Addressable. The child manager prefabs are ordinary nested prefab instances, not Addressable.
- `Scenes.asset` group is empty. Scenes load by name through Build Settings (`SceneManager.LoadScene`).

### 1.3 Singleton prefabs (nested prefab instances, in child order)

`StartUpSingletons.prefab` (spawned first):

| Child | Component | Notes |
|---|---|---|
| AnalyticsManager | `AnalyticsManager` | `TrackEvent/TrackPause/TrackResume/TrackGameStart/TrackGameStop/TrackScreen` (other note covers it) |
| ApplicationManager | `ApplicationManager` | `targetFrameRate: 60`; the prefab also has a stale serialized `mainSceneReference` that the code does not use |
| ScreenBlockerManager | `ScreenBlockerManager` | Overlay canvas (`RenderMode 0`, `sortingOrder 128`), black `Blocker` Image, CanvasGroup fade 0.5 s. **Active at spawn**, so the screen stays black until someone calls `Hide()` |

`CommonSingletons.prefab` (spawned second):

| Child (order) | Component | Key serialized values |
|---|---|---|
| SfxManager | `SfxManager` | AudioSource → `Main.mixer` group **Sfx** |
| BgmManager | `BgmManager` | AudioSource → group **Music** |
| VolumeManager | `VolumeManager` | `audioMixer: Main.mixer` |
| VfxManager | `VfxManager` | |
| PlayerDataManager | `PlayerDataManager` | 3 reactive volume props (serialized `latestValue: 0`, which `Awake` overwrites) |
| CherryIntegrationManager | `CherryIntegrationManager` | none |
| GameConfigsManager | `GameConfigsManager` | `mainScene: GameUIExample`, `arGameScene: ARGameExample`, `nonARGameScene: NonARGameExample` |
| DebugPrinter | `DebugPrinter` | `textColor (1,0.5,0.5,1)`, `fontSize 40`, font null (uses the default) |

Not spawned anywhere: `RemoteConfigManager.prefab` (it exists in the folder but is not a child of either root),
`Assets/Prefabs/Debug/DebugTimeScaleChanger.prefab`, and the `CameraGuard.prefab` package prefab (see 1.8).

All `Awake`s of one instantiated prefab run before any of its `Start`s. The relative `Awake` order between siblings is
not something to rely on. StartUp managers are guaranteed awake before any Common manager.

### 1.4 Boot sequence (reference: `MainInitializerExample`, `MainCoordinator`)

```
Scene load → SingletonSpawner.Awake → (await StartUp) → (await Common) → activate MainInitializerExample → Destroy(spawner)
MainInitializerExample.Start:
  if ApplicationManager.Instance.FirstAppStart: FirstAppStart = false; splash (skipped in Editor unless debugShowSplash)
  mainCoordinatorReference.InstantiateAsync() → coordinator.Initialize() (waits for OnEnable)
  await UniTask.Delay(0.1 s)   // comment: ScreenBlockerManager.Hide() is buggy without it
  await ScreenBlockerManager.Instance.Hide()
  await coordinator.StartMain() → ViewManager.PushView(WelcomeScreenView)
  GlobalOptions.shared.frameResolution = (1920, 1080)   // MDK
```

`MainCoordinator` loads game scenes with bare `SceneManager.LoadScene(GameConfigsManager.Instance.NonARGameScene)`.
It does not call `ScreenBlockerManager.Show()` first, and the game scene calls `Hide()` after its own init.

### 1.5 `GameConfigsManager`

```csharp
[SerializeField, Scene] string mainScene, arGameScene, nonARGameScene;   // NaughtyAttributes [Scene] = dropdown of Build Settings scenes
public string MainScene { get; } public string ARGameScene { get; } public string NonARGameScene { get; }
```
Build Settings currently list `GameUIExample`, `ARGameExample`, and `NonARGameExample`. A new scene must be added to
Build Settings (editor script: `EditorBuildSettings.scenes = ...`) before `[Scene]` can show it or `LoadScene(name)` can find it.

### 1.6 `ApplicationManager`

- `public bool FirstAppStart { get; set; } = true;` is **in memory only** (not persisted). It is true once per process and is used to decide whether to show the splash.
- `Awake`: `Screen.sleepTimeout = SleepTimeout.SystemSetting`; `Application.targetFrameRate = Math.Min(targetFrameRate(60), round(refreshRate))`; disables the on-screen dev console.
- **(verify with the platform team)** Motion-only input does not reset Android's idle timer. If the TV dims during long camera play, use `SleepTimeout.NeverSleep` during gameplay.

### 1.7 `ScreenBlockerManager`

```csharp
public UniTask Show(bool animate = true);       // SetActive(true) + showAnimator.PlayAsUniTask(animate)
public async UniTask Hide(bool animate = true); // hideAnimator.PlayAsUniTask(animate) then SetActive(false)
```
It lives in DontDestroyOnLoad, so it survives scene loads. Its overlay canvas sits above camera-rendered and pixelated content.

### 1.8 Platform integration: `CherryIntegrationManager`, `GameActionDelegate`, CameraGuard

Public API:
```csharp
public static IReadOnlyAsyncReactiveProperty<bool> IsCameraMutedProperty { get; } // CameraGuardController.Instance?.isMuted, else a static fake (always false)
public IReadOnlyAsyncReactiveProperty<bool> PreferGameStopped { get; }           // rawPreferGameStopped || isCameraMuted
public UniTask WaitForResumeIfNeeded(CancellationToken ct);                       // returns immediately if not stopped; else awaits until false
public bool IsKeyboardControlDisabled();
public void SetIsCameraMutedForDebug(bool value);                                 // reflection into CameraGuardController.mutableMuted
```

Wiring:
- `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` sets `Nex.Platform.GameActionDelegate.Instance = new GameActionDelegate()` (a private sealed nested class):
  - `StopPlayingGameIfNeeded()` → `rawPreferGameStoppedProperty.Value = true`
  - `ResumeStoppedGameIfNeeded()` → `false`
  - `DisableAllKeyboardControl()` / `RestoreDisabledKeyboardControl()` → static property → `ViewManager.SuspendKeyboardNavigation()` / `ResumeKeyboardNavigation()`
  - `BackToInitialScreen()` = **TODO no-op**, `IsInitialScreen()` = **TODO `true`**, `GetActiveScreenName()` = **TODO `""`**
  - `StopGameSetupIfNeeded()` is **not overridden**. The base logs the warning "It is required to implement game setup events..." on every app pause.
- Who calls it:
  - Editor or universal fallback build: `Nex.DefaultAppSupport.DefaultBinding` creates `DefaultStateManager` (`OnApplicationPause` → Stop/Resume). In the Editor, **Ctrl+Alt+P** toggles simulated pause.
  - Playground device (`Env.isPlaygroundBuild`): `Nex.AppSupport.NexBinding` creates `NexStateManager` (same `DefaultStateManager` class) → `OnApplicationPause`.
  - `CameraGuardController` (camera mute) calls `DetectionController.PauseAllDetectionForCamera()`, `DisableAllKeyboardControl`, and `StopPlayingGameIfNeeded`, and shows an overlay. On unmute it resumes.
- `PreferGameStopped` is built once in `Awake` from `IsCameraMutedProperty`. If `CameraGuardController.Instance` is null at that moment, the binding stays on the fake property for the rest of the session.

**Camera-mute path is not wired in this starter (verified by grep):** no scene or prefab in `Assets/` instantiates
`Packages/team.nex.camera-guard/Prefabs/CameraGuard.prefab`, and nothing assigns `CameraGuardController.cameraStatusProvider`.
So on device, `IsCameraMutedProperty` is the always-false fake, and only app pause drives `PreferGameStopped`.
Whether the host OS pauses the app on camera mute is **unverified; ask the platform team**.

Editor hotkeys (raw `Input`, not `DebugInput`):

| Keys | Where | Effect |
|---|---|---|
| Ctrl+Alt+P | `DefaultStateManager` (Editor) | toggle app pause → `PreferGameStopped` |
| Ctrl+M | `CameraGuardController` (Editor or non-PRODUCTION, needs the CameraGuard prefab) | toggle camera mute |
| Cmd+Shift+M | `CherryIntegrationManager` (Editor) | `SetIsCameraMutedForDebug`. When there is no `CameraGuardController`, `GetValue(null)` on an instance field will throw `TargetException` **(inferred from code)** |

`DeviceActionDelegate.Instance.ExitGame()` is the exit path (used by `MainCoordinator` in builds). `CameraGuardOverlayManager` on Escape:
if `IsInitialScreen()` is true it calls `ExitGame()`, otherwise `BackToInitialScreen()`. Because `IsInitialScreen` is stubbed to `true`,
**Escape on the camera-mute overlay exits the app from any screen.**

### 1.9 `VolumeManager` + AudioMixer

```csharp
public void SetMasterVolume(float value01); public void SetMusicVolume(float value01); public void SetSfxVolume(float value01);
// mixer dB = Log10(Max(v, 0.001)) * 20   → 0..1 maps to -60..0 dB
```
`Main.mixer` groups are Master, Music, and Sfx. The exposed params are `MasterVolume`, `MusicVolume`, and `SfxVolume` (`AudioMixerConstants`).
Any new `AudioSource` (ball impacts, enemy barks) **must set `outputAudioMixerGroup` to Sfx or Music**, or it bypasses volume settings.

**Gap (verified by grep):** nothing subscribes `PlayerDataManager.*VolumeProperty` to `VolumeManager`, and nothing writes property
changes back to `PlayerPreference`. Saved volumes are never applied at boot. `DebugSettings.SetMusicVolume` etc. call `VolumeManager`
directly and bypass preferences. Section 6.3 has the fix. `AudioMixer.SetFloat` called in `Awake` does not stick (known Unity behavior),
so bind in `Start` or later.

### 1.10 `AppInfo` (`Nex.Util`)

`AppInfo.Instance` is created `BeforeSceneLoad`, so it is always available. It exposes `PackageName`, `Version`, `VersionCode` (long, from the Android
PackageManager), and `VersionDisplayString` (`"-- Unity Editor --"` in the Editor, `"1.2.3 (45) [DEV]"` in debug builds). `WelcomeScreenView` shows it through a
smart-string argument. Use it in the debug panel or analytics. Its editor post-processor strips the Unity dev watermark from development builds.

### 1.11 `RemoteConfigManager` (NOT enabled)

This manager is not in any singleton root. If enabled, `Start` calls `UnityServices.InitializeAsync` (env `production` when `Env.isProduction`, else
`staging`) and `SignInAnonymouslyAsync`, then fetches configs. API: `RemoteConfig` (`IReadOnlyAsyncReactiveProperty<RemoteConfig?>`),
`RemoteConfigOrDefault`, `OnUpdate`, `CurrentAndOnUpdate`, `GetRemoteConfigAsync(ct)`, and `configFetchedTaskCompletionSource`. The `RemoteConfig`
class has a single `exampleConfig` string, and each new key needs a line in `RemoteConfig.Create`. **Recommendation:** leave it off for Billiard Rogue.
It adds a network and auth dependency. Tune through ScriptableObject configs instead.

### 1.12 `Env` / `PlatformUtils`

- `Nex.Platform.Env`: `isEditor`, `isAndroid`, `isProduction` (`PRODUCTION`), `isPlaygroundBuild`, `isDebug`, `isFallbackBuild`, and others.
- `Nex.Utils.PlatformUtils` (static bools): `IsEditor`, `IsOnIosDevice`, `IsOnAndroidDevice`, `IsProdBuildOnMacOrPC`, `IsOnStandaloneMacOsDevice`,
  `IsOnStandaloneWindowsDevice`, `IsProduction` (`PRODUCTION`), `IsOnOlympia` (`OLYMPIA`), `IsOnSkyTv` (`SKY_TV_BUILD`), `IsForVideoShooting` (`VIDEO_SHOOTING`).
- Gotcha: `OLYMPIA` is `#define`d locally inside `Env.cs` only. `PlatformUtils.IsOnOlympia` is false unless `OLYMPIA` is a project define,
  so use `Env.isPlaygroundBuild`.
- Current Android defines (`ProjectSettings.asset`): `MOREMOUNTAINS_*`, `UNITASK_DOTWEEN_SUPPORT`, `ES3_TMPRO`, `ES3_UGUI`.
  **`PRODUCTION`, `ENABLE_DEBUG_SETTINGS`, and `DISABLE_PERSISTENCE` are not defined.** The release pipeline must add `PRODUCTION`.

---

## 2. Persistence: `PlayerDataManager`

### 2.1 Full public surface (file has no `#nullable enable`)

```csharp
public class PlayerDataManager : Singleton<PlayerDataManager>
{
    // Reactive volumes (init from PlayerPreference in Awake; NOT written back, NOT applied to the mixer)
    public IAsyncReactiveProperty<float> MasterVolumeProperty { get; }
    public IAsyncReactiveProperty<float> SfxVolumeProperty { get; }
    public IAsyncReactiveProperty<float> BgmVolumeProperty { get; }
    public void InstallTemporaryProperties(AsyncReactiveProperty<float> tempMaster, AsyncReactiveProperty<float> tempSfx,
        AsyncReactiveProperty<float> tempBgm, CancellationToken ct);  // temp.BindTo(main): every temp change is pushed into main until ct is cancelled

    // Player preference (key "playerPreferenceData")
    public PlayerPreference PlayerPreference { get; private set; }
    public void ResetPlayerPreference();                       // new() + ES3.Save
    public void SavePlayerPreference();                        // ES3.Save
    public void ScopedPlayerPreferenceUpdate(Action<PlayerPreference> modifier); // modifier(pref); Save

    // Debug settings (key "debugSettingsData")
    public DebugSettings DebugSettings { get; private set; }
    public void SaveDebugSettings();
    // private LoadDebugSettings(), private ResetDebugSettings() (unused)

    // In-memory only, survives scene loads (DontDestroyOnLoad), NOT persisted
    public class AppViewState : AbstractViewState { ViewIdentifier => Empty; public bool enableHighlighting = true; }
    public readonly AppViewState appViewState = new();
}
```

`Awake` order: `base.Awake()` → `DebugSettings = new()`, `PlayerPreference = new()` → **only under
`ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR`** `LoadDebugSettings()` → `LoadPlayerPreference()` → `InitializeProperties()`.
In a release build, `DebugSettings` is therefore always a fresh default instance.

Load failure handling: `LoadPlayerPreference` catches any exception (first boot throws `FileNotFoundException`, a renamed type throws
`TypeLoadException`) and calls `ResetPlayerPreference()`, which **overwrites the saved preference**. `LoadDebugSettings` falls back to `new()` without saving.

`DISABLE_PERSISTENCE`: every `ES3.*` call is wrapped in `#if !DISABLE_PERSISTENCE` (loads return `new()`). New persistence code must follow the same pattern.

`AppViewState` / `AbstractViewState` (`Assets/Scripts/Main/Views/Framework/AbstractViewState.cs`) is a singly linked chain (`NextViewState`,
whose setter disposes the old one), plus `HasValidNextViewStateOrClear(id[, id2[, id3]])` and a `CancellationToken` that is cancelled on `Dispose`.
Use it to restore menu state when coming back from the game scene (for example, "show the run summary", or "focus the Continue button").

### 2.2 `PlayerPreference` (current)

```csharp
public class PlayerPreference { public float masterVolume = 1f; public float sfxVolume = 1f; public float bgmVolume = 1f; }
```

### 2.3 `DebugSettings` (current)

```csharp
public class DebugSettings
{
    public bool enableDebugPrinter = false;          // gates DebugPrinter.OnGUI
    public void MuteMusic(); public void MuteSfx();  // VolumeManager.Set*(0), bypasses preferences
    public void SetMusicVolume(float volume); public void SetSfxVolume(float volume);
    public void ReloadMainScene();                   // EMPTY BODY (commented out), a button that does nothing
    public void ClearAllDataCache();                 // only ResetPlayerPreference()
}
```

### 2.4 Easy Save 3 configuration (`ES3Defaults.asset`, read-only)

| Setting | Value | Meaning |
|---|---|---|
| `_location` | 0 | `Location.File` |
| `directory` | 0 | `Application.persistentDataPath` |
| `path` | `SaveFile.es3` | single default file for every key |
| `format` / `prettyPrint` | JSON / on | human-readable |
| `encryptionType` / `compressionType` | None / None | |
| `typeChecking` / `safeReflection` | on / on | |
| `referenceMode` | 2 (ByRefAndValue) | only matters for `UnityEngine.Object` |
| `assemblyNames` | includes `Assembly-CSharp` and team.nex.* | types in other assemblies will not resolve |

Write mechanics (source `ES3.Save<T>` / `ES3Writer.Save`): every `ES3.Save` opens a writer, writes the key, **merges all other keys** from
the existing file, writes to a backup file, and then commits it by rename (`ES3IO.CommitBackup`). The whole file is rewritten
synchronously on the main thread on every save. That is fine at turn boundaries. Never save per frame or per ball hit.
`ES3.DeleteKey` rewrites the file too, and it is a no-op if the file does not exist. `ES3.KeyExists` returns false when the file is missing.
`ES3.Load<T>(key)` throws if the file or key is missing. `ES3.Load<T>(key, defaultValue)` does not throw.
Data survives app updates and is lost on uninstall or clear data.

### 2.5 What ES3 serializes (from `ES3Reflection.GetSerializableFields/Properties`, `ES3TypeMgr.CreateES3Type`)

| Member / type | Saved? |
|---|---|
| public instance field of a supported type | yes |
| private field | only with `[SerializeField]` or `[ES3Serializable]` |
| `readonly` field (`IsInitOnly`) or `const` | **no, silently skipped.** `public readonly List<X> items = new()` is NOT saved |
| public **static** field | **yes** (bindings include `Static`). Avoid static fields in save classes |
| `[NonSerialized]`, `[Obsolete]`, `[ES3NonSerializable]` | no |
| properties | only with `[SerializeField]`/`[ES3Serializable]` and get+set (safe mode). Computed properties are ignored |
| field whose type equals the declaring class | skipped (cycle guard) |
| primitives, string, enums, `Vector2/3/4`, `Vector2Int/3Int`, `Color`, `Quaternion`, `Rect`, `Bounds`, `DateTime`, `Guid`, `Nullable<T>` | yes |
| `T[]` (1D–3D), `List<T>`, `Dictionary<K,V>`, `HashSet<T>`, `Queue<T>`, `Stack<T>` | yes, if the elements are supported |
| nested plain C# classes / structs | yes, by reflection (`ES3ReflectedObjectType` / `ES3ReflectedValueType`) |
| `UnityEngine.Object` refs (ScriptableObject configs, Sprites) | **do not use.** They need an ES3ReferenceMgr (`addMgrToSceneAutomatically: 0`), and a ScriptableObject is saved by value (a new instance on load). Store IDs or enums |
| `System.Random` | ES3 type exists but depends on private fields `inext/inextp/SeedArray`. **Do not rely on it**; store your own RNG state |

Schema-evolution rules:
- Load does `Activator.CreateInstance` (field initializers run), then overwrites only the members present in the file. **Added fields
  keep their initializer defaults. Removed fields are skipped (`reader.Skip()`).** A public parameterless constructor is required,
  otherwise ES3 uses `GetUninitializedObject` and initializers do not run.
- Enums are stored as their **underlying int**. Give every persisted enum explicit values, append new values only, and never reorder.
- The root value is stored with its type string (`"Nex.PlayerPreference,Assembly-CSharp"`) and `typeChecking` is on. **Renaming or moving the
  class or namespace, or moving it to another assembly, makes load throw.** For `PlayerPreference` that means a silent reset.
- IL2CPP is used on Android. Stripping level is default, and Minimal does not strip user assemblies. Add `[UnityEngine.Scripting.Preserve]`
  to save DTOs anyway, as cheap insurance.

---

## 3. Debug tooling

### 3.1 Debug panel flow

- Entry points (only under `ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR`):
  1. `ViewManager.ComputeControlConfig` adds `TopLevelControlPanel.ControlConfig.DebugSettings` to every view except the debug view. The top panel
     button calls `ViewManager.OpenDebugSettings()`, which plays the `GenericEnter` SFX and runs `PushViewPrefab(debugSettingsPrefab)`.
  2. Secret code: `ViewManager.Awake` does `if (TryGetComponent<SecretCodeSequenceDetector>(out var d)) d.AddListener(0, OpenDebugSettings);`.
     **No `SecretCodeSequenceDetector` exists in any prefab or scene (verified by guid grep)**, so the code currently does nothing.
- The only `ViewManager` lives on the `MainViewManager` GameObject in `Assets/Prefabs/Coordinators/MainViewManager.prefab` (nested in
  `MainCoordinator.prefab`; `debugSettingsPrefab` → `Assets/Prefabs/Views/DebugSettingsView.prefab`). The example game scenes have **no
  ViewManager**, so the debug panel cannot be reached in gameplay unless the game scene hosts a `MainViewManager` instance.
- `DebugSettingsView` (`SimpleCanvasView`, `Identifier = DebugSettings`, `Controls = Back`, `AnalyticsScreenName = "debug-settings"`) in `Awake`:
  ```csharp
  debugSettingsPanel.Initialize(() => { pdm.SaveDebugSettings(); pdm.SavePlayerPreference(); }, () => PopSelf());
  debugSettingsPanel.PopulateRows(pdm.DebugSettings, minVisibilityLevel: 0);
  ```
- **Values are applied only by the panel's Save button** (`CloseSelf(true)` → write every row back into the object → save callback → pop).
  The top-level **Back** button calls `SimpleView.OnBackButton` → `PopSelf()` and **discards edits**.
- **Method rows always close the panel first**, then invoke the method (`CloseSelf(save).ContinueWith(invoke)`). Pending field edits
  are saved only if the method has `[SaveBeforeInvoking]`. Otherwise they are discarded.
- The panel reads values once when it is built. There is no change event, so gameplay must read `DebugSettings` live or react to a
  save hook (6.5).
- Keyboard nav: the rows container `DynamicGroupKeyResponder` has `fetchChildResponders: 1`, which fetches in its own `Awake`.
  `DebugSettingsView` uses the non-generic `Initialize` + `PopulateRows`. It does not call the generic `Initialize<T>` overload, which also runs
  `rowsKeyResponder.ReinitializeWithChildResponders()`. **(unverified)** Rows may not be keyboard-reachable depending on Awake
  order. If arrow keys skip rows, switch the view to `debugSettingsPanel.Initialize(settings, save, close, 0)`.
- A filter box on the top row does a lowercase substring match against labels with the spaces removed.

### 3.2 How `DebugSettingsPanel.PopulateRows<TSettings>(TSettings target, int minVisibilityLevel)` reflects members

It uses `typeof(TSettings).GetMembers()`, which returns **public instance and static** members.

| Member | Condition | Row |
|---|---|---|
| field | `IsPublic` | by type (below). Written back with `FieldInfo.SetValue` on Save |
| property | `CanRead` | by type. `Interactable = CanWrite` (get-only = read-only display, evaluated once at open) |
| method | public, non-abstract, `void`, every parameter one of bool/string/(s)byte/(u)short/(u)int/(u)long/float/double/decimal, not a property setter | button + one input per parameter |
| type `bool` | | toggle |
| `enum` (not `Nullable<enum>`) | | dropdown of every value (`ToString()` names) |
| (s)byte, (u)int16/32, uint64, float, double | | numeric up/down input |
| `long` | `[DateTimeTicks]` → date-time ticks row, else numeric | |
| `string` | `[CopyableTextArea(rows)]` → text area, else input | |
| `DateTime` | | date-time row |
| anything else (lists, classes, structs, Vector2, `Nullable`) | | **silently no row** |

Visibility: shown if `(VisibilityLevel?.Level ?? 0) >= minVisibilityLevel` (the view passes 0). `[VisibilityLevel(-1)]` hides a member.
Order: members with `[DebugOrder(n)]` come first in ascending order. The rest follow in declaration order, fields and properties before methods.
Labels: `[Description("...")]`, or the member name split on case changes (`startStageIndex` → "Start Stage Index").

Attributes (`using Nex.Dev.Attributes;`):
```csharp
[VisibilityLevel(int level)]                 // field/property/method
[Description(string text)]                   // label
[DebugOrder(int order)]
[NumericSteps(Steps = 0.25, Min = 0, Max = 4)]  // named args (double); also IntSteps/IntMin/IntMax (long). Left/Right keys step the value; EndEdit clamps
[IntChoices("Off", "Low", "High")]           // int dropdown mapping names → 0..n-1
[StringChoices("en", "ja")] / [StringChoices(new[]{"en","ja"}, new[]{"English","日本語"})]
[SaveBeforeInvoking]                         // methods
[CopyableTextArea(rows)], [DebugContentWidth(float)], [DateTimeTicks]
```
- There is **no slider and no section or header attribute**. For a "slider", use `NumericSteps` with Min and Max. For sections, use `DebugOrder`
  blocks plus a label prefix such as `[Description("Run: Start Stage")]`, which the filter box can also match.
- `IntChoices(params (string,int)[])` exists, but tuples are not legal attribute arguments, so only the `params string[]` form is usable.
- Do not add public `const`/static fields to `DebugSettings`. They would become rows, and Save would call `SetValue` on a const, which throws. ES3 would also save statics.

### 3.3 `DebugPrinter` (`Nex`, in CommonSingletons)

```csharp
public void Print(string key, string message); public void Print(string key, object obj); public void Clear();
```
It draws `key: value` lines through IMGUI `OnGUI` at (10,10), and only while `PlayerDataManager.Instance.DebugSettings.enableDebugPrinter`
is on. That is never the case in release, since DebugSettings is not loaded there. Entries persist until overwritten or `Clear()`.
`Print(key, object)` allocates (`ToString`) even when hidden, and `OnGUI` builds strings with LINQ each frame when on. Guard hot-path calls (6.5).

### 3.4 `Nex.Dev.DebugInput`, `DebugTimeScaleChanger`, `SecretCodeSequenceDetector`

```csharp
public static bool DebugInput.GetKey(KeyCode k); GetKeyDown(KeyCode k); GetKeyUp(KeyCode k);   // always false under PRODUCTION
```
- `DebugTimeScaleChanger : MonoBehaviour` has `[SerializeField] List<float> timeScales`. On `DebugInput.GetKeyDown(T)` it moves to the next entry after
  the current `Time.timeScale`, or 1 if the current value is not in the list. The prefab `Assets/Prefabs/Debug/DebugTimeScaleChanger.prefab` has `timeScales = [1, 2, 0, 0.5]`
  and is **not placed in any scene**. Because it keys on `DebugInput`, it is active in every non-PRODUCTION build.
  Physics note: `fixedDeltaTime` is in game time, so at 2x the physics steps per real second double (CPU), and at 0 `FixedUpdate` stops.
- `SecretCodeSequenceDetector` (`[AddComponentMenu("Nex/Keyboard Navigation/Secret Code Sequence Detector")]`) has private serialized
  `SequenceConfig[] configs` (each with `KeyboardNavigationController.Key[] sequence` and `UnityEvent onTriggered`) and an optional `keyboardController`,
  which falls back to `GetComponent<KeyboardNavigationController>()` in `Start`. `ViewManager` `AddComponent`s that controller in `Awake`.
  `public void AddListener(int configIndex, UnityAction action)`. Key enum: `Escape=0, Enter=1, Up=2, Down=3, Left=4, Right=5`.
  Matching uses KMP, so overlapping prefixes work. **`configs` must contain at least one element**, or `ViewManager.Awake` throws IndexOutOfRange.

---

## 4. `ObjectPooler<T>` / `IPoolableObject`

```csharp
public interface IPoolableObject { public event Action<Component>? OnRelease; }
public abstract class ObjectPooler<T> : MonoBehaviour where T : MonoBehaviour, IPoolableObject
{
    protected T objectPrefab; protected ObjectPool<T> objectPool;
    public void Initialize(T prefab, int defaultCapacity = 10);   // UnityEngine.Pool.ObjectPool, collectionCheck:false, default maxSize
    public T Get();                                             // SetActive(true); tracked in ActiveInstances
    public IEnumerable<T> ActiveInstances { get; }              // backed by a HashSet
    protected virtual T CreatePooledItem();                     // Instantiate(prefab, transform) + subscribes OnRelease
    public void OnDestroy();                                    // objectPool.Dispose()   (public, NOT virtual)
}
```
Gotchas: it is **abstract generic**, so make a concrete subclass (`public class BallViewPool : ObjectPooler<BallView> {}`), which Unity can
add as a component. Call `Initialize` before `Get`; an uninitialized pooler throws NRE in `OnDestroy`. The item returns itself by raising
`OnRelease?.Invoke(this)`. Double release is not detected (`collectionCheck:false`) and corrupts the pool. Items are not reparented or reset
on return, only deactivated, so reset state in `OnEnable` or in your own `Reset...()`. Copy `ActiveInstances` into a list before releasing all
(the HashSet is mutated during iteration). A subclass that declares `OnDestroy` hides the base one and must call it. It is unused in the starter today.
`VfxManager` has its own internal pool.

---

## 5. Utils: signatures

```csharp
// Assets/Scripts/Gameplay/Smoothing/Filter.cs  (namespace Nex; "copied from Jazz")
public interface IFilter   { float Filter(float x, float? timestamp = null); }
public interface IFilter2D { Vector2 Filter(float x, float y, float? timestamp = null); }
public class ComposedFilter2D<T> : IFilter2D { public ComposedFilter2D(T filterX, T filterY); }   // casts with `as IFilter`
public class WrappedKalmanFilter2D : IFilter2D { public WrappedKalmanFilter2D(Jazz.KalmanFilter2D f); }
public class MovingAverageFilter : IFilter { public MovingAverageFilter(int window = 14); }
public class SingleExponentialFilter : IFilter { public SingleExponentialFilter(float alpha = 0.01f, string variant = "predict"); public float? lastX { get; } public float alpha { get; set; } }
public class DoubleExponentialFilter : IFilter { public DoubleExponentialFilter(); }        // alpha = gamma = 0.01
public class OneEuroFilter : IFilter { public OneEuroFilter(float minCutOff = 1f, float beta = 0.007f, float dCutOff = 1f); } // assumes 25 Hz until timestamps (seconds) are passed; no Reset(), so create a new one

// Nex.Utils.History<T> : IEnumerable<HistoryItem<T>>   (HistoryItem<T> { T Item; float Timestamp; })
public History(float maxAge); public void Add(float timestamp, T data) /* ignores timestamp <= Last */; public void Clear();
public HistoryItem<T>? Last { get; }  /* newest */   public HistoryItem<T>? Peek { get; } /* oldest */   // enumerates oldest→newest; prunes items older than Last - maxAge

// Nex.WeightedFloatHistory   (uses Jazz.TimedItem<(float value, float weight)> { item; readonly double frameTime; })
public WeightedFloatHistory(double timeWindow); public void Add(float item, float weight, double frameTime); // newest first, ignores non-newer
public void UpdateCurrentFrameTime(double frameTime) /* prunes */; public float WeightedAverage(); public float WeightedSum();
public double TimeSpan(); public List<TimedItem<(float,float)>> ItemsFromNewToOld(); public void Clear();
// BUG: Clear() resets totalWeightedSum but NOT totalWeight, so WeightedAverage() is wrong after Clear(). Construct a new instance instead.

[Serializable] public struct Nex.Utils.FloatRange { public float min, max; public FloatRange(float min, float max); public float RandomValue { get; } } // UnityEngine.Random, not seedable; has a drawer
public static class Nex.Utils.RemapUtils { float Remap(float x, float a, float b, float c, float d); float RemapAndClamp(float x, float a, float b, float c, float d); } // a≈b → returns c
public static class Nex.DetectionUtils { static readonly Vector2 AspectNormalizedFrameSize = (16/9, 1); static readonly Rect AspectNormalizedFrameRect; }
public static class Nex.Utils.Vector2Utils { Vector2 PolarDeg(float angleDeg, float radius = 1); Vector2 Polar(float angleRad, float radius = 1); float ToPolarDeg(this Vector2 v) /* [0,360) */; }
public static class Nex.Utils.ListExtensions { void Shuffle<T>(this IList<T> list); }   // UnityEngine.Random, not seedable
public static class Nex.Utils.RectUtils { Rect GetIntersection(Rect a, Rect b); Rect FromFrameSpaceToNormalizedSpace(Rect frameRect, Vector2 frameSize); }
public static class Nex.TextureUtils { Rect ComputeTextureRect(Texture2D tex, Rect normalizedRect); }
public static class Nex.Utils.MMFeedbacksExtension { UniTask PlayAsUniTask(this MMFeedbacks f, bool animate = true, MMFeedbacks.Directions direction = TopToBottom, bool reverted = false, float durationMultiplier = 1f, CancellationToken ct = default); } // animate:false → duration × 0.001
public static class Nex.EnvironmentInfo { const string ProductionEnvironmentName = "production", StagingEnvironmentName = "staging"; }
// Nex.Util.AsyncEnumerableExtensions (team.nex.unitask-utils): void BindTo<T>(this IUniTaskAsyncEnumerable<T> src, IAsyncReactiveProperty<T> dest, CancellationToken ct); WhereNonNull<T>()
```
For a seeded roguelike, **do not use `FloatRange.RandomValue` or `ListExtensions.Shuffle` in run logic**. Use a run-owned RNG with serializable state (6.4).

---

## 6. How Billiard Rogue should use this

### 6.1 Scene and bootstrap plan

1. **Entry/title scene** `BilliardRogueMain`: a `SingletonSpawner` with `[StartUp, Common]` and `activatePostSpawn = [BilliardMainInitializer]` (inactive
   in the scene). Copy the `MainInitializerExample` pattern: splash when `FirstAppStart` → coordinator → `Hide()` blocker → push title view.
2. **Game scene** `BilliardRogueGame`: also a spawner (so it runs standalone from the Editor), with an inactive `BilliardGameOrchestrator` in `activatePostSpawn`, and a
   **`MainViewManager` prefab instance** (HUD, pause, reward-pick views, and the debug panel in gameplay).
3. Scene transitions always use the blocker:
   ```csharp
   async UniTask LoadSceneCovered(string sceneName)
   {
       await ScreenBlockerManager.Instance.Show();
       await SceneManager.LoadSceneAsync(sceneName);   // UniTask awaits AsyncOperation
   }
   // In the target scene orchestrator, after Initialize + first frame is ready:
   await UniTask.Delay(TimeSpan.FromSeconds(0.1f));   // same workaround as MainInitializerExample
   await ScreenBlockerManager.Instance.Hide();
   ```
4. `GameConfigsManager`: add fields rather than repurposing the example ones.
   ```csharp
   [SerializeField, Scene] string billiardRogueScene = null!;
   public string BilliardRogueScene => billiardRogueScene;
   ```
   Then set `mainScene` to `BilliardRogueMain` and the new field to `BilliardRogueGame` through an editor script (below).
5. Global managers stay in `CommonSingletons`. **Do not add a new manager for saves.** Run and meta persistence go into `PlayerDataManager` (6.4).

Editor-script recipes (run via the Unity CLI; never hand-edit YAML):
```csharp
// Set serialized scene names on the GameConfigsManager prefab
const string path = "Assets/Prefabs/Singletons/GameConfigsManager.prefab";
var root = PrefabUtility.LoadPrefabContents(path);
var so = new SerializedObject(root.GetComponent<GameConfigsManager>());
so.FindProperty("mainScene").stringValue = "BilliardRogueMain";
so.FindProperty("billiardRogueScene").stringValue = "BilliardRogueGame";
so.ApplyModifiedPropertiesWithoutUndo();
PrefabUtility.SaveAsPrefabAsset(root, path);
PrefabUtility.UnloadPrefabContents(root);

// Enable the secret code on the shared ViewManager prefab (applies to every scene that uses it)
const string vmPath = "Assets/Prefabs/Coordinators/MainViewManager.prefab";
var vmRoot = PrefabUtility.LoadPrefabContents(vmPath);            // root GO "MainViewManager" holds ViewManager
var detector = vmRoot.AddComponent<SecretCodeSequenceDetector>();
var dso = new SerializedObject(detector);
var configs = dso.FindProperty("configs");
configs.arraySize = 1;
var seq = configs.GetArrayElementAtIndex(0).FindPropertyRelative("sequence");
int[] keys = { 2, 2, 3, 3, 4, 5, 4, 5 };                            // Up Up Down Down Left Right Left Right
seq.arraySize = keys.Length;
for (var i = 0; i < keys.Length; i++) seq.GetArrayElementAtIndex(i).enumValueIndex = keys[i];
dso.ApplyModifiedPropertiesWithoutUndo();
PrefabUtility.SaveAsPrefabAsset(vmRoot, vmPath);
PrefabUtility.UnloadPrefabContents(vmRoot);

// Add a nested manager prefab under CommonSingletons (only if a genuinely new global service is approved)
var common = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Singletons/CommonSingletons.prefab");
var child = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Singletons/X.prefab"), common.transform);
PrefabUtility.SaveAsPrefabAsset(common, "Assets/Prefabs/Singletons/CommonSingletons.prefab");
PrefabUtility.UnloadPrefabContents(common);

// Build Settings
EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/BilliardRogue/BilliardRogueMain.unity", true),
                                     new EditorBuildSettingsScene("Assets/Scenes/BilliardRogue/BilliardRogueGame.unity", true) };
```
Place `DebugTimeScaleChanger.prefab` into the game scene with `PrefabUtility.InstantiatePrefab` + `EditorSceneManager.SaveScene`.

### 6.2 Platform pause and camera mute: how gameplay must respond

Rules:
- On `PreferGameStopped == true`: freeze the simulation (no shot resolution, no enemy phase), ignore shot gestures, show the pause
  view, call `AnalyticsManager.Instance.TrackPause()`, and **snapshot the run if in a stable phase**, because Android may kill a backgrounded app.
- On `false`: **do not auto-resume**. Leave the pause view up. The player resumes through `ViewManager.PauseViewResumeClicked`. If the camera was
  muted, or if detection was paused for a long time, re-run the setup/calibration step before accepting shots (the player may have moved).
- Before every async step that needs the player (setup waits, turn start, reward pick), call `await CherryIntegrationManager.Instance.WaitForResumeIfNeeded(ct)`.
- Do not use `Time.timeScale = 0` as the pause mechanism, because it fights `DebugTimeScaleChanger` and the debug time scale. Gate the turn state machine and
  the physics step instead. If timeScale must be used, restore the previous value, not 1.

```csharp
// BilliardGameOrchestrator (MonoBehaviour), called from its async start after Initialize(...)
void BindPlatformPause()
{
    // Subscribe emits the current value first, so a game started while stopped pauses at once.
    CherryIntegrationManager.Instance.PreferGameStopped.Subscribe(HandlePreferGameStopped, destroyCancellationToken);
}

void HandlePreferGameStopped(bool stopped)
{
    if (!stopped) return;                       // resume only via the pause view
    turnController.Suspend();
    runSaver.SaveIfStable();                    // → PlayerDataManager.SaveRun(snapshot) when phase is PlayerTurn/RewardSelection
    AnalyticsManager.Instance.TrackPause();
    pauseFlow.ShowPauseViewWhenPossible();      // PushView throws while viewManager.IsInTransition, so retry after transition
}
```

Recommended platform fixes in `CherryIntegrationManager.GameActionDelegate` (small, in-place):
- Override `StopGameSetupIfNeeded()` to set `rawPreferGameStoppedProperty.Value = true`. That value is already set by `StopPlayingGameIfNeeded`, so this just removes the warning.
- Implement `IsInitialScreen` / `BackToInitialScreen` / `GetActiveScreenName` through static hooks that the coordinator sets. Examples: `IsInitialScreen` is true when the title view is on top,
  `BackToInitialScreen` saves the run and returns to the title, and `GetActiveScreenName` returns the top view's `AnalyticsScreenName`. Without this, Escape on the camera guard exits the app.
- Camera-mute support needs `CameraGuard.prefab` awake **before** `CherryIntegrationManager.Awake` (for example as the first child of StartUpSingletons) plus a
  `cameraStatusProvider`. Hold this until the platform team confirms how mute is delivered on Playground.

### 6.3 Settings: extend `PlayerPreference` and finish the volume pipeline

```csharp
// PlayerPreference.cs: public mutable fields only; enums explicit + append-only
public class PlayerPreference
{
    public float masterVolume = 1f;
    public float sfxVolume = 1f;
    public float bgmVolume = 1f;

    public int numberOfPlayers = 1;                                             // 1..2, last chosen
    public ControlHandedness[] handednessByPlayer = { ControlHandedness.LeftPawBall, ControlHandedness.LeftPawBall };
    public AimAssistLevel aimAssist = AimAssistLevel.Low;
    public string localeCodeOverride = "";                                      // "" = platform/system locale
}

public enum ControlHandedness { LeftPawBall = 0, RightPawBall = 1 }   // LeftPawBall: left paw = ball position, right paw = cue/strike
public enum AimAssistLevel { Off = 0, Low = 1, High = 2 }
```
- Read settings: `PlayerDataManager.Instance.PlayerPreference.aimAssist`. Write them: `PlayerDataManager.Instance.ScopedPlayerPreferenceUpdate(p => p.aimAssist = value);`.
  Each call writes the file, so call it on confirm or on value commit, not on every slider tick.
- After load, validate array length (`handednessByPlayer.Length < 2` → resize) because old files keep the old length.
- Language: the project uses `CommandLineLocaleSelector → SystemLocaleSelector → SpecificLocaleSelector(en)` (locales en, fr-CA, ja, zh-Hans,
  zh-Hant). The platform owns the language. Only apply `localeCodeOverride` if design adds a picker:
  `LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(code)` after `LocalizationSettings.InitializationOperation`.
- Volume pipeline. The rule says "PlayerDataManager volume properties applied by VolumeManager":
  ```csharp
  // VolumeManager: both managers live in CommonSingletons; all Awakes finish before Start; mixer SetFloat must not run in Awake.
  void Start()
  {
      var data = PlayerDataManager.Instance;
      data.MasterVolumeProperty.Subscribe(SetMasterVolume, destroyCancellationToken);   // using Cysharp.Threading.Tasks.Linq
      data.SfxVolumeProperty.Subscribe(SetSfxVolume, destroyCancellationToken);
      data.BgmVolumeProperty.Subscribe(SetMusicVolume, destroyCancellationToken);
  }
  ```
  Settings view: create temp `AsyncReactiveProperty<float>` initialized from the main properties, call `InstallTemporaryProperties(temp..., viewCt)`,
  and bind sliders to the temps (live preview). On confirm, `ScopedPlayerPreferenceUpdate(p => { p.masterVolume = data.MasterVolumeProperty.Value; ... })`.
  On cancel, set the main properties back from `PlayerPreference`. Change the `DebugSettings.SetMusicVolume/SetSfxVolume/Mute*` methods to set the
  properties instead of calling `VolumeManager` directly.

### 6.4 Run and meta persistence through `PlayerDataManager`

DTOs (new file `Assets/Scripts/PlayerData/RunSaveData.cs` and `MetaProgress.cs`; `#nullable enable`; namespace `Nex`):
```csharp
[Preserve] public class RunSaveData
{
    public const int CurrentVersion = 1;          // const: not serialized
    public int version = CurrentVersion;
    public long savedAtUtcTicks;

    public int seed;
    public ulong rngState;                        // run-owned RNG (e.g. xorshift64); never System.Random / UnityEngine.Random
    public RunPhase phase = RunPhase.PlayerTurn;
    public int stageIndex;
    public int turnNumber;
    public int gold;
    public int numberOfPlayers = 1;
    public int activePlayerIndex;
    public int heroHp;
    public int heroMaxHp;

    public List<BallSaveData> balls = new();      // inventory in shot order
    public List<EnemySaveData> enemies = new();
    public List<BallType> pendingRewardChoices = new();   // non-empty only in RewardSelection
}
[Preserve] public class BallSaveData  { public BallType type; public int level = 1; }
[Preserve] public class EnemySaveData { public EnemyType type; public Vector2Int cell; public int hp; public int maxHp; public bool isBoss; public List<StatusSaveData> statuses = new(); }
[Preserve] public class StatusSaveData { public StatusEffectType type; public int stacks; public int turnsLeft; }
public enum RunPhase { PlayerTurn = 0, RewardSelection = 1, StageIntro = 2 }

[Preserve] public class MetaProgress
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public int bestStageIndex = -1;
    public int runsPlayed;
    public int runsWon;
    public bool tutorialSeen;
    public List<BallType> unlockedBalls = new();  // starter balls come from the ball config, not stored here
}
// BallType / EnemyType / StatusEffectType: gameplay enums with explicit values, append-only (ES3 stores ints).
```

`PlayerDataManager` additions. Add `#nullable enable` at the top; this also requires `= null!` on the existing `PlayerPreference`/`DebugSettings` properties.
Then in `Awake`, after `LoadPlayerPreference()`, call `LoadMetaProgress(); LoadSavedRun();`.
```csharp
#region Meta Progress

const string metaProgressDataKey = "metaProgressData";

public MetaProgress MetaProgress { get; private set; } = null!;

public void ScopedMetaProgressUpdate(Action<MetaProgress> modifier)
{
    modifier(MetaProgress);
    SaveMetaProgress();
}

public void ResetMetaProgress()
{
    MetaProgress = new MetaProgress();
    SaveMetaProgress();
}

void SaveMetaProgress()
{
#if !DISABLE_PERSISTENCE
    ES3.Save(metaProgressDataKey, MetaProgress);
#endif
}

void LoadMetaProgress()
{
#if DISABLE_PERSISTENCE
    MetaProgress = new MetaProgress();
#else
    try
    {
        MetaProgress = ES3.Load(metaProgressDataKey, new MetaProgress());
    }
    catch (Exception e)
    {
        // Do not overwrite on failure (unlike PlayerPreference): keep the file for diagnosis until the next real save.
        Debug.LogError($"[PlayerDataManager] Meta progress unreadable, using defaults: {e}");
        MetaProgress = new MetaProgress();
    }
#endif
}

#endregion

#region Run Save

const string runSaveDataKey = "runSaveData";

public RunSaveData? SavedRun { get; private set; }

public void SaveRun(RunSaveData snapshot)
{
    snapshot.version = RunSaveData.CurrentVersion;
    snapshot.savedAtUtcTicks = DateTime.UtcNow.Ticks;
    SavedRun = snapshot;
#if !DISABLE_PERSISTENCE
    ES3.Save(runSaveDataKey, snapshot);
#endif
}

public void ClearSavedRun()
{
    SavedRun = null;
#if !DISABLE_PERSISTENCE
    ES3.DeleteKey(runSaveDataKey);
#endif
}

void LoadSavedRun()
{
#if DISABLE_PERSISTENCE
    SavedRun = null;
#else
    try
    {
        var run = ES3.KeyExists(runSaveDataKey) ? ES3.Load<RunSaveData>(runSaveDataKey) : null;
        SavedRun = run != null && run.version == RunSaveData.CurrentVersion ? run : null;
    }
    catch (Exception e)
    {
        Debug.LogWarning($"[PlayerDataManager] Discarding unreadable run save: {e}");
        SavedRun = null;
    }
#endif
}

#endregion
```
Also extend `DebugSettings.ClearAllDataCache()` to call `ResetPlayerPreference()`, `ResetMetaProgress()`, and `ClearSavedRun()`.

Save policy (the live run state is owned by the run controller; `SavedRun` is only a snapshot):
- The run controller builds a **fresh** `RunSaveData` via `runState.ToSaveData()` and calls `SaveRun` at stable points only: the start of each player turn
  (after the enemy advance and attack have resolved), entering reward selection, stage start, and on `PreferGameStopped == true` if the phase is stable.
  **Never mid-shot.** Ball physics is not reproducible, so a mid-shot kill replays the turn from the start-of-turn snapshot,
  including `rngState`, which prevents rerolling.
- Do not mutate `SavedRun` in place from gameplay. Treat it as read-only input to "Continue run".
- End of run (win or lose): `ScopedMetaProgressUpdate(m => { m.runsPlayed++; if (won) m.runsWon++; m.bestStageIndex = Math.Max(m.bestStageIndex, stage); })`,
  **then** `ClearSavedRun()`, then show the summary. An AppViewState entry can carry "show summary" back to the title scene.
- New run: `ClearSavedRun()` first. Seed from `DebugSettings.fixedSeed` when non-zero (debug), otherwise from time.
- Analytics: log `run_saved` and `run_resumed` (with stage and turn) through `AnalyticsManager.TrackEvent`.

### 6.5 DebugSettings extension (flat fields; all have safe "off" defaults)

```csharp
// DebugSettings.cs (keep it a plain class; no const/static fields, no nested types in rows)
using System;
using Nex.Dev.Attributes;

public class DebugSettings
{
    [DebugOrder(0)] public bool enableDebugPrinter = false;
    [DebugOrder(1), Description("Show FPS / Turn Info")] public bool showRunInfo;

    [DebugOrder(10), Description("Cheat: God Mode")] public bool godMode;
    [DebugOrder(11), Description("Cheat: Infinite Balls")] public bool infiniteBalls;
    [DebugOrder(12), Description("Cheat: One Hit Kill")] public bool oneHitKill;

    [DebugOrder(20), Description("Run: Start Stage"), NumericSteps(IntSteps = 1, IntMin = 0, IntMax = 60)] public int startStageIndex;
    [DebugOrder(21), Description("Run: Fixed Seed (0 = random)")] public int fixedSeed;
    [DebugOrder(22), Description("Run: Start Gold"), NumericSteps(IntSteps = 10, IntMin = 0, IntMax = 9999)] public int startGold;
    [DebugOrder(23), Description("Run: Force Ball Type")] public bool forceBallType;
    [DebugOrder(24), Description("Run: Forced Ball")] public BallType forcedBall;      // enum row; Nullable<enum> would get no row

    [DebugOrder(30), Description("Flow: Skip Setup (Editor only)")] public bool skipSetup;
    [DebugOrder(31), Description("Flow: Auto Aim Bot")] public bool autoAimBot;
    [DebugOrder(32), Description("Flow: Game Time Scale"), NumericSteps(Steps = 0.25, Min = 0.25, Max = 4)] public float gameTimeScale = 1f;

    [DebugOrder(40), Description("Debug: Physics Gizmos")] public bool showPhysicsDebug;
    [DebugOrder(41), Description("Debug: Pose Overlay")] public bool showPoseOverlay;

    [DebugOrder(50), Description("Render: Pixelation")] public bool pixelationEnabled = true;
    [DebugOrder(51), Description("Render: Pixel Scale"), IntChoices("1x", "2x", "3x", "4x")] public int pixelScaleIndex = 2;
    [DebugOrder(52), Description("Render: Bloom")] public bool bloomEnabled = true;
    [DebugOrder(53), Description("Render: Tilt-Shift DoF")] public bool tiltShiftEnabled = true;
    [DebugOrder(54), Description("Render: God Rays")] public bool godRaysEnabled = true;

    public enum Command { KillAllEnemies = 0, WinStage = 1, SkipToBoss = 2, RefillBalls = 3 }
    public static event Action<Command>? CommandIssued;      // private backing field → neither a row nor saved; add_/remove_ get no row

    [DebugOrder(60)] public void KillAllEnemies() => CommandIssued?.Invoke(Command.KillAllEnemies);   // runs after the panel pops
    [DebugOrder(61)] public void WinStage() => CommandIssued?.Invoke(Command.WinStage);
    [DebugOrder(62)] public void SkipToBoss() => CommandIssued?.Invoke(Command.SkipToBoss);
    [DebugOrder(63), SaveBeforeInvoking] public void UnlockAllBalls() { /* ScopedMetaProgressUpdate(...) */ }
    [DebugOrder(64), SaveBeforeInvoking] public void ClearSavedRun() => PlayerDataManager.Instance.ClearSavedRun();
    // existing: MuteMusic, MuteSfx, SetMusicVolume(float), SetSfxVolume(float), ClearAllDataCache; delete or implement ReloadMainScene
}
```
Nested `public enum Command` is a type, not a member row, so it is fine. `GetMembers` returns nested types, which the panel ignores.

Guarding. `DebugSettings` fields always compile. Release builds never load them, so they hold defaults. Gameplay still reads cheats through
one guarded accessor so that release builds provably ignore them, even if a future change loads debug data:
```csharp
#nullable enable
namespace Nex
{
    public static class BilliardDebugFlags
    {
        static DebugSettings? Active
        {
            get
            {
#if ENABLE_DEBUG_SETTINGS || DEVELOPMENT_BUILD || UNITY_EDITOR
                return PlayerDataManager.Instance.DebugSettings;
#else
                return null;
#endif
            }
        }

        public static bool GodMode => Active is { godMode: true };
        public static bool InfiniteBalls => Active is { infiniteBalls: true };
        public static int StartStageIndex => Active?.startStageIndex ?? 0;
        public static int FixedSeed => Active?.fixedSeed ?? 0;
#if UNITY_EDITOR
        public static bool SkipSetup => Active is { skipSetup: true };     // the camera setup must always run on device
#else
        public static bool SkipSetup => false;
#endif
        // ... one line per flag

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD"),
         System.Diagnostics.Conditional("ENABLE_DEBUG_SETTINGS")]
        public static void Print(string key, object value) => DebugPrinter.Instance.Print(key, value);   // args not evaluated in release
    }
}
```
- Read flags **live at the point of use** (damage application, ball consumption, run creation) so that panel edits apply mid-run.
- For render and post-FX toggles, add a hook in `PlayerDataManager`: `public event Action? DebugSettingsSaved;`, raised at the end of `SaveDebugSettings()`.
  The panel's Save callback already calls it. A render-settings component subscribes and re-applies URP feature and volume toggles.
- `gameTimeScale`: apply it once at gameplay start (`Time.timeScale = BilliardDebugFlags.GameTimeScale`). The T key (`DebugTimeScaleChanger`) cycles at runtime.
- Gameplay subscribes to `DebugSettings.CommandIssued` while the run is active and unsubscribes in `OnDestroy`.
- Hotkeys for development use `Nex.Dev.DebugInput.GetKeyDown(...)` only. Never use raw `Input` (it is disabled under `PRODUCTION`).

### 6.6 Pooling example

```csharp
public class BallViewPool : ObjectPooler<BallView> { }             // component on the game scene / arena prefab

public class BallView : MonoBehaviour, IPoolableObject
{
    public event Action<Component>? OnRelease;
    public void Release() => OnRelease?.Invoke(this);               // exactly once per Get()
    void OnEnable() { /* reset trail, velocity, visuals */ }
}

ballViewPool.Initialize(ballViewPrefab, defaultCapacity: 16);    // in the orchestrator's Initialize
var ball = ballViewPool.Get();
```
Pool damage numbers and hit sparks the same way. Particle one-shots go through `VfxManager.PlayVisualEffect`, which already pools.

---

## 7. Risks and open questions

1. **Camera mute is unwired**: no CameraGuard prefab instance and no `cameraStatusProvider`. `PreferGameStopped` only reflects app pause. Confirm the Playground behavior with the platform team.
2. **Saved volumes are never applied, and changes are never persisted** (6.3 fix). Mixer-bypassing AudioSources will ignore settings.
3. **The secret code does nothing** (no `SecretCodeSequenceDetector`). The debug panel is only reachable via the top-level button, and only in scenes that have a `ViewManager`.
4. `IsInitialScreen() == true` stub: Escape on the camera guard overlay exits the app from gameplay.
5. ES3 pitfalls: `readonly` fields are not saved; public statics are saved; enum reorder or type rename breaks saves. `PlayerPreference` silently resets on any load exception.
6. `PRODUCTION` is not defined in ProjectSettings. `DebugInput`, the T-key time scale, and CameraGuard Ctrl+M stay live in any non-PRODUCTION release build.
7. `WeightedFloatHistory.Clear()` bug; `FloatRange.RandomValue` and `ListExtensions.Shuffle` are not seedable.
8. **(unverified)** Keyboard navigation of debug rows (3.1). Awake order between `CommonSingletons` siblings is not guaranteed, so do cross-manager wiring in `Start`.
