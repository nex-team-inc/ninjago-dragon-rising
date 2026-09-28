# Audio · VFX · Feedback · Analytics — reference note (Billiard Rogue)

Scope: `SfxManager`, `BgmManager`, `VolumeManager` + `Main.mixer`, `MMF_SfxManager`, `VfxManager`,
Epic Toon FX (ETFX), ChocDino UIFX, Feel (`MMF_Player`), DOTween, `ObjectPooler`, `AnalyticsManager` /
`Nex.Platform.GameAnalytics`, plus the shared-assets audio plan. All paths are relative to
`/Users/simonbut/project/VibeProject3/Starter/` unless absolute. Everything below was read from source
unless marked **UNVERIFIED**. Enum names follow `Docs/BilliardRogue/TDD.md` §5.

---

## 0. TL;DR (read this first)

| # | Finding | Impact |
|---|---|---|
| 1 | **Volume settings are not wired.** `PlayerDataManager` exposes `Master/Sfx/BgmVolumeProperty`, but nothing subscribes to them. `VolumeManager` only has setters. | The Settings sliders will do nothing until someone binds them (see §11.3). |
| 2 | **Every prefab `EnumDictionary` is empty or null.** `SfxManager` has 3 keys with `clips: []` (so `GenericEnter`/`GenericExit`, used by `ViewManager` Back/Debug, are silent). `BgmManager.Main` is `{fileID: 0}`. `VfxManager.effectSpecs.pairs: []`, and its enum is empty. | All audio and VFX must be populated by an editor script (§3.3). |
| 3 | **Adding enum values without re-serializing the prefab breaks things at runtime** (see §3.1): keys missing *between* existing keys become `null` and cause a NRE in `SfxManager.Awake`/`VfxManager.Awake`. Keys *after* the last key are missing from the dict and cause a `KeyNotFoundException` on play. | The builder must write **every** enum key, in ascending value order. |
| 4 | **ETFX is not URP-ready.** 300 of 358 materials use built-in `Particles/Standard Unlit` (fileID 211), and 37 use `Standard`. They render magenta in URP. The package's URP upgrade `.unitypackage` is missing, here and in the shared repo. 90 materials have soft particles on, but URP `m_RequireDepthTexture: 0`. | Convert curated materials with URP `ParticleUpgrader` from an editor script (§4.4). |
| 5 | **ETFX prefabs are unsafe in a pool as they ship.** 347 have an `AudioSource` (`PlayOnAwake`, no mixer group, so they skip the volume sliders). 190 have a `Light` + `ETFXLightFade`, which `Destroy`s the Light on first use. 276 use world particle collision (`type: 1`). 145 use Noise. `m_MaxParticleSize` goes up to 4. Child durations can be longer than the root's (e.g. `FrostExplosion` root 1 s, child 5 s). | Sanitize curated copies (§4.5). |
| 6 | `VfxManager.PlayVisualEffect` returns `void`, has no rotation/scale/parent, and relies on the root's `OnParticleSystemStopped`. **Looping FX never go back to the pool.** Every spec needs `maxPoolSize > 0`, or `ObjectPool` throws in `Awake`. | Fire-and-forget bursts only; extend it or own looping FX per entity (§11.5). |
| 7 | `SfxManager` = one 2D `AudioSource` + `PlayOneShot`. There is no pitch control, no rate limit, and only per-frame same-clip dedup. `RealVoiceCount` is 32. | The GDD's "pitch rises with combo" needs pre-pitched clips (the project already has a 12-note scale) or a small `AudioSource` pool (§11.4). |
| 8 | `BgmManager` fades use DOTween with **scaled time**, so they hang when `Time.timeScale = 0` (pause). There is a single source (no crossfade) and `Play` does not reset volume. | Extend: `SetUpdate(true)`, 2 sources, ducking, stingers (§11.4). |
| 9 | Analytics: `AnalyticsManager.TrackEvent(name, props)` sends `c_<name>`. Props are a `Dictionary<string, GameAnalyticsValue>` with implicit int/float/double/bool/string/array conversions. **Session props override event props that use the same key** (the original is kept as `o_<key>`). PLAY-scope session props are **reset by `TrackGameStart` and `TrackGameStop`**. | Taxonomy and helper in §9. |
| 10 | Naming risk: TDD §12 names the wrapper `Analytics/GameAnalytics.cs`. `Nex.Platform.GameAnalytics` already exists and is used by `AnalyticsManager`. | Rename it (e.g. `RunAnalytics`) to avoid confusion and ambiguity errors in files that `using Nex.Platform;`. |
| 11 | Shared repo `~/Documents/music-cell-shared-assets` is a **partial clone (`blob:none`) + cone-mode sparse checkout + Git LFS**. Only `NEX/BGM` and `NEX/Sound effect` are checked out. Cone mode accepts **directories only**. | Use the per-file `git show … \| git lfs smudge` recipe (§10.4). Never copy `.meta`. |

---

## 1. File map

| Thing | Path | Namespace |
|---|---|---|
| SfxManager | `Assets/Scripts/Sfx/SfxManager.cs` | `Nex` |
| BgmManager | `Assets/Scripts/Bgm/BgmManager.cs` | `Nex` |
| VolumeManager / AudioMixerConstants / AudioMixerUtils | `Assets/Scripts/Audio/*.cs` | `Nex` |
| MMF_SfxManager | `Assets/Scripts/Feedbacks/MMF_SfxManager.cs` | `Nex.MMF` |
| MMF_ImageSpriteSequence / MMF_SpriteRenderSpriteSequence | `Assets/Scripts/Feedbacks/` | `Nex.MMF` |
| VfxManager | `Assets/Scripts/Vfx/VfxManager.cs` | `Nex` |
| ObjectPooler / IPoolableObject | `Assets/Scripts/ObjectPooling/ObjectPooler.cs` | `Nex` |
| MMFeedbacksExtension.PlayAsUniTask | `Assets/Scripts/Utils/MMFeedbacksExtension.cs` | **`Nex.Utils`** |
| AnalyticsManager | `Assets/Scripts/Analytics/AnalyticsManager.cs` | `Nex` |
| GameAnalytics, GameAnalyticsProperties/Value | `Library/PackageCache/team.nex.platform-utils@7615141761cc/Scripts/` | `Nex.Platform` |
| Mixpanel `Value` overloads (extension) | `Library/PackageCache/team.nex.platform-utils.analytics-ext@113548f9ad50/Mixpanel/GameAnalyticsExtension.cs` | `Nex.Platform.AnalyticsExtension` |
| EnumDictionary | `Library/PackageCache/team.nex.common-utils@d0649bb973c6/Runtime/EnumDictionary/EnumDictionary.cs` | `Nex.Util` |
| Prefabs | `Assets/Prefabs/Singletons/{SfxManager,BgmManager,VfxManager,VolumeManager,AnalyticsManager}.prefab` | |
| Mixer | `Assets/Audio/AudioMixer/Main.mixer` (guid `842207d1e3e8e4ef89ebb072adddda24`) | |
| Mixpanel settings | `Assets/Resources/Mixpanel.asset` | |
| Feel 5.8 | `Assets/Libraries/Feel/{MMFeedbacks,MMTools,NiceVibrations}` (one asm `MoreMountains.Tools` + asmrefs) | `MoreMountains.Feedbacks` |
| DOTween 1.2.632 | `Library/PackageCache/team.nex.as-dotween@c09be27052b7/DOTween/` (dll + `DOTween.Modules` asmdef) | `DG.Tweening` |
| Epic Toon FX v1.81 | `Assets/Libraries/Epic Toon FX/` (1447 prefabs, 358 mats, 78 wavs) | `EpicToonFX` (scripts) |
| ChocDino UIFX | `Assets/Libraries/ChocDino/{UIFX,UIFX-TMP,UIFX-UITK}` | `ChocDino.UIFX` |

Bootstrap: `CommonSingletons.prefab` nests DebugPrinter, PlayerDataManager, **VfxManager, VolumeManager,
SfxManager**, GameConfigsManager, CherryIntegrationManager, **BgmManager**. `StartUpSingletons.prefab`
nests ScreenBlockerManager, **AnalyticsManager**, ApplicationManager. `CommonSingletons` has only
Transform/name overrides, so **edit the source singleton prefabs**, not the nest.

---

## 2. Audio

### 2.1 `Main.mixer` (read-only YAML inspection)
```
Master (exposed "MasterVolume")
├── Sfx   group fileID -7067896505692204098 (exposed "SfxVolume")   ← SfxManager AudioSource output
└── Music group fileID  5251897629169556355 (exposed "MusicVolume") ← BgmManager AudioSource output
```
- The mixer has one snapshot ("Snapshot"), only Attenuation effects, and no ducking/send.
  `m_EnableSuspend: 1`, `m_SuspendThreshold: -80`.
- Constants: `AudioMixerConstants.masterVolumeParameterName = "MasterVolume"`, `sfxVolumeParameterName = "SfxVolume"`,
  `musicVolumeParameterName = "MusicVolume"`, `volumeMultiplier = 20f`.
- `AudioMixerUtils.GetMixerVolumeValueFrom01Value(float v) => Mathf.Log10(Mathf.Max(v, 0.001f)) * 20f` (0 maps to -60 dB).
- Rule: never call `AudioMixer.SetFloat` from features; go through `VolumeManager`. Duck the music with
  `AudioSource.volume` in `BgmManager`, not with the mixer.

### 2.2 `VolumeManager : Singleton<VolumeManager>`
```csharp
[SerializeField] AudioMixer audioMixer;            // prefab → Main.mixer
public void SetMasterVolume(float value);          // 0..1
public void SetMusicVolume(float value);
public void SetSfxVolume(float value);
```
- `PlayerDataManager` (Awake loads ES3 `playerPreferenceData`) exposes:
  `IAsyncReactiveProperty<float> MasterVolumeProperty, SfxVolumeProperty, BgmVolumeProperty` (initialized from
  `PlayerPreference.masterVolume/sfxVolume/bgmVolume`, default 1). There is also
  `InstallTemporaryProperties(tempMaster, tempSfx, tempBgm, ct)`, which binds temp properties into the real ones
  (for a settings screen preview).
- **GAP: nothing binds the properties to `VolumeManager`.** Only `DebugSettings.MuteMusic/MuteSfx/SetMusicVolume/SetSfxVolume`
  call `VolumeManager`. Naming mismatch: PlayerDataManager calls it "Bgm", the mixer calls it "Music".
- Known Unity behavior: `AudioMixer.SetFloat` in `Awake` can be overwritten by snapshot init. Apply the values from
  `Start` or later (this is what the snippet in §11.3 does).

### 2.3 `SfxManager : Singleton<SfxManager>`
```csharp
public enum SoundEffect { None = -1, GenericEnter = 0, GenericExit = 1 }
[SerializeField] AudioSource audioSource;                                   // prefab: 2D, Loop 0, PlayOnAwake 0, → Sfx group
[Serializable] class SoundEffectSpec { [SerializeField] public AudioClip[] clips; }   // private nested
[SerializeField] EnumDictionary<SoundEffect, SoundEffectSpec> soundEffectDict;
public void PlaySoundEffect(SoundEffect effect, AudioSource? customAudioSource = null);
public void PlayAudioClip(AudioClip clip, AudioSource? customAudioSource);  // public helper, usable for arbitrary clips
```
- Clip choice: `Initialize()` picks a random start index. `PickSingleClip()` returns `clips[index]`, then jumps by a random
  non-zero step, so there is **no immediate repeat** when `clips.Length > 1`. Round-robin variants are just more
  entries in `clips`.
- Dedup: `currentlyStartedClips` (instance IDs) is cleared in `LateUpdate`. The same `AudioClip` plays at most once
  per frame. Different variants of one effect *can* stack in the same frame.
- `None` is a no-op. An empty `clips` array plays nothing (it returns `null!`).
- Playback is `PlayOneShot` on the shared source (or on `customAudioSource`). Pitch, pan and volume come from that
  source, so **per-shot pitch needs a different AudioSource**.
- Used by the framework: `ViewManager.HandleTopLevelButton(Back)` → `GenericExit`, and `OpenDebugSettings` → `GenericEnter`.
  **Both are currently silent** (no clips). Suggested clips: `Assets/Audio/Sfx/Generic/SFX_UI_Click_Open_1.wav` /
  `SFX_UI_Click_Close_1.wav`. Note that ViewManager does **not** play SFX on every push/pop, despite what the rule text suggests.

Prefab YAML (current):
```yaml
audioSource: {fileID: 3197796902356267032}     # OutputAudioMixerGroup = Sfx
soundEffectDict:
  pairs:
  - {key: -1, value: {clips: []}}
  - {key: 0,  value: {clips: []}}
  - {key: 1,  value: {clips: []}}
```

### 2.4 `MMF_SfxManager : MMF_Feedback` (Feel path **"Nex/SfxManager"**)
```csharp
public SfxManager.SoundEffect soundEffect = SfxManager.SoundEffect.GenericEnter;
protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1)
{ if (Owner.DurationMultiplier < 0.9f) return; SfxManager.Instance.PlaySoundEffect(soundEffect); }
```
- It is **skipped in "instant" mode**: `PlayAsUniTask(animate:false)` sets `DurationMultiplier = 0.001`.
- It is not used in any prefab yet. Use it for view/HUD feedback chains. Do **not** use Feel's `MMF_Sound` or
  `MMF_MMSoundManager*`: they bypass the Nex mixer and volume flow.

### 2.5 `BgmManager : Singleton<BgmManager>`
```csharp
public enum BgmType { Main }                       // TDD: += Title, Act1, Act2, Act3, Boss, Reward (keep Main)
[SerializeField] AudioSource audioSource;          // prefab: Loop 1, PlayOnAwake 0, → Music group
[SerializeField] EnumDictionary<BgmType, AudioClip> bgmDict;   // Main → {fileID: 0}
public void Play(BgmType type);                    // sets clip + Play(); does NOT touch volume
public void Stop();
public async UniTask FadeIn(float duration = 0.5f);  // audioSource.DOFade(1, d).WithCancellation(destroyToken)
public async UniTask FadeOut(float duration = 0.5f); // DOFade(0, d)
```
Gotchas:
- `DOFade` uses scaled time (DOTween `defaultTimeScaleIndependent: 0`), so a fade awaited while paused **never finishes**.
- After `FadeOut`, a later `Play` stays at volume 0 until `FadeIn`.
- There is one `AudioSource`, so no crossfade and no stinger layer. `WithCancellation` needs `UNITASK_DOTWEEN_SUPPORT`,
  which is defined for Android and Standalone only.

### 2.6 Existing clips in `Assets/Audio` (ffprobe; sample rate, channels, seconds)

| Folder | Files (duration s) | Notes |
|---|---|---|
| `Sfx/Generic/` | `Hit.wav` 96k/2 0.32 · `SFX_UI_Button_Click_Select_1` 0.13 · `SFX_UI_Click_Close_1` 0.16, `_2` 0.20 · `SFX_UI_Click_Generic_1/2/3` 0.16/0.17/0.16 · `SFX_UI_Click_Open_1/2` 0.12 | 44.1k mono UI clicks (Cute UI / Merge packs) |
| `Sfx/Popup/` | `SFX_UI_Popup_1/2/3` 0.15/0.12/0.12 | mono |
| `Sfx/Swipe/` | `SFX_UI_Swipe_1/2/3` 0.27/0.47/0.50 · `SFX_UI_Notification_Popup_2` 0.41 | |
| `Sfx/Countdown/` | `SFX_UI_Countdown_Blow_1/2` 0.46 · `SFX_UI_Countdown_End_1` 1.89 · `SFX_UI_Fillup_Organic_Tier_1/2/3` 0.50 | countdown tick + go |
| `Sfx/CA/` (correct answer) | `SFX_UI_Collect_Pop_Ring_01Bb/02D/03F/04_Bb` 1.0 · `SFX_UI_Collect_Pop_Mallet_Tier_1/2` 1.03, `_3` 2.0 · `PUZZLE_Success_Beep_Three_Note_Climb_Dry_stereo` 0.19 · `SFX_Booster_Strong_1` 2.0 · `Marcello Del Monaco - Gaming - answer` 96k 0.55 | |
| `Sfx/WA/` (wrong answer) | `Bjorn Lynne - Multimedia - Wrong Answer` 1.06 · `CB Sounddesign - Activation - Descending Beeps Wrong` 0.37 · `Ni Sound - Funny Game UI - Quick Error` 0.75 | 96k stereo |
| `Sfx/GameEnding/` | `Epic Stock Media - Vibrant Game - Victory Kazoo` 3.50 · `Ni Sound - Melodic Transitions - Kalimba Retro Video Game Theme Bright Positive` 2.69 · `Bjorn Lynne - Multimedia - Sunshine Bright Bells` 1.31 · `MUSIC_EFFECT_Solo_Xylophone_Positive_16_stereo` 1.24 · `Unrealsfx - Ding a Ling - Call Bell` 4.50 | |
| `Sfx/ScoreCounting/` | `Summary Score count 1.ogg` 0.12 | |
| `Sfx/Scales/Set1/` | `SFX_Merge_Pop_Mallet_01Bb…12F` ×12, 0.47 each | **12-step rising scale, ready for combo pitch** |
| `Sfx/Scales/Set2/` | `SFX_Merge_Solid_Soft_01Bb…12Bb` ×12, 1.0–2.6 | 12-step scale (softer) |
| `Sfx/Scales/Set3/` | `SFX_UI_Fillup_Organic_Tier_1/2/3` 0.5 | 3-step |

- There are **no BGM files**, and `Assets/Audio/Bgm/` does not exist yet.
- Also in the project at zero cost: `Assets/Libraries/Epic Toon FX/Sound/*.wav` (78 files), e.g. `etfx_explosion_frost` 0.84,
  `etfx_explosion_lightning` 1.06, `etfx_explosion_fireball` 0.77, `etfx_explosion_poisoncloud` 1.92, `etfx_explosion_acid` 1.62,
  `etfx_explosion_magic` 0.75, `etfx_explosion_grenade` 0.76, `etfx_spawn` 0.44, `etfx_target_hit` 0.60, `etfx_stun01` 0.55,
  `etfx_impact_metal01` 0.33, `etfx_pop_balloon` 0.24.
- The current import settings are Unity defaults (`loadType: 0` DecompressOnLoad, `compressionFormat: 1` Vorbis,
  `preloadAudioData: 1`). There are no AudioImporter presets. `ProjectSettings/AudioManager.asset`: RealVoices 32,
  Virtual 512, DSP buffer 1024.

### 2.7 Recommended import settings (apply in the builder via `AudioImporter`)

| Kind | loadType | format | quality | other |
|---|---|---|---|---|
| BGM loops (`Bgm/BilliardRogue`) | `Streaming` | Vorbis | 0.5–0.6 | `preloadAudioData=false`, keep stereo |
| Stingers (2–8 s) | `CompressedInMemory` | Vorbis | 0.6 | stereo ok |
| Short SFX (< 1 s) | `DecompressOnLoad` | ADPCM | — | `forceToMono=true`, `sampleRateSetting=OptimizeSampleRate` |

```csharp
var imp = (AudioImporter)AssetImporter.GetAtPath(path);
imp.forceToMono = isSfx;
var s = imp.defaultSampleSettings;
s.loadType = isBgm ? AudioClipLoadType.Streaming : isStinger ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
s.compressionFormat = isSfx ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.Vorbis;
s.quality = 0.55f; s.preloadAudioData = !isBgm;          // Unity 6: preloadAudioData lives on SampleSettings
s.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
imp.defaultSampleSettings = s; imp.SaveAndReimport();
```
(UNVERIFIED: in Unity 6 `preloadAudioData` moved from `AudioImporter` to `AudioImporterSampleSettings`. Check it
compiles and fall back to `imp.preloadAudioData` if not.)

---

## 3. `Nex.Util.EnumDictionary<TKey,TValue>` — the serialization contract

### 3.1 Runtime behavior (from source)
- Serialized as `pairs: List<KeyValuePairStruct{ TKey key; TValue value; }>`. The key is stored as the **enum's integer
  value** (YAML `key: -1`).
- `static TKey[] allKeys` = `Enum.GetValues` sorted ascending. The constructor fills every key with `default`.
- `OnAfterDeserialize`: `dict.Clear()`, then walks `pairs` in order. It adds `default` for any key smaller than the
  current pair, then the pair itself. It logs an error for unknown or out-of-order keys (`"EnumDictionary: Encountered undefined key …"`).
  **Keys greater than the last serialized pair are never added.**
- The indexer getter is `dict[key]`. Its setter exists, but `Add`/`Remove` throw. `TryGetValue` and `ContainsKey` are public.
- Consequences: a gap-filled key has value `null` for class specs. `SfxManager.Awake` and `VfxManager.Awake` call
  `pair.Value.Initialize()` on it and get a **NRE**. A trailing key causes a **KeyNotFoundException** in
  `PlaySoundEffect`/`PlayVisualEffect`/`Play`.
- The editor drawer (`EnumDictionaryEditorUtils.RectifyPropertyIfNeeded`) re-sorts and fills keys, but **only when an
  inspector draws it**. A CLI builder must write the full, sorted list itself.
- Rules: give enum members **explicit ints**, never renumber them, keep declarations in ascending value order, and
  avoid `[Flags]` enums.

### 3.2 Serialized property paths
| Prefab/component | Property path | Value shape |
|---|---|---|
| SfxManager | `soundEffectDict.pairs[i].key` / `.value.clips` | `AudioClip[]` |
| BgmManager | `bgmDict.pairs[i].key` / `.value` | `AudioClip` |
| VfxManager | `effectSpecs.pairs[i].key` / `.value.prefab` / `.value.defaultPoolSize` / `.value.maxPoolSize` | `ParticleSystem`, int, int (**max > 0**) |

### 3.3 Builder pattern (Editor, run via Unity CLI; Unity mints everything)
```csharp
// Assets/Scripts/BilliardRogue/Editor/AudioRegistryBuilder.cs  (namespace Nex.BilliardRogue.Editor)
static void FillEnumDict<TEnum>(string prefabPath, System.Type comp, string dictField,
    System.Action<SerializedProperty /*value*/, TEnum> writeValue) where TEnum : System.Enum
{
    var root = PrefabUtility.LoadPrefabContents(prefabPath);
    try
    {
        var so = new SerializedObject(root.GetComponent(comp));
        var pairs = so.FindProperty(dictField + ".pairs");
        var keys = (TEnum[])System.Enum.GetValues(typeof(TEnum));
        System.Array.Sort(keys);                                   // same order as EnumDictionary.allKeys
        pairs.arraySize = keys.Length;
        for (var i = 0; i < keys.Length; i++)
        {
            var pair = pairs.GetArrayElementAtIndex(i);
            pair.FindPropertyRelative("key").intValue = System.Convert.ToInt32(keys[i]); // raw enum value (UNVERIFIED: intValue vs enumValueFlag on Unity 6 — assert YAML afterwards)
            writeValue(pair.FindPropertyRelative("value"), keys[i]);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
    }
    finally { PrefabUtility.UnloadPrefabContents(root); }
}
// Sfx: value.FindPropertyRelative("clips") → arraySize + objectReferenceValue per clip
// Bgm: value.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(p)
// Vfx: value.FindPropertyRelative("prefab").objectReferenceValue = prefabGO.GetComponent<ParticleSystem>();
//      value.FindPropertyRelative("defaultPoolSize").intValue = 4; value.FindPropertyRelative("maxPoolSize").intValue = 16;
```
Afterwards, check the YAML with `grep -A2 "key:"`: keys should be sorted and complete, with no `{fileID: 0}` for used entries.

---

## 4. VFX

### 4.1 `VfxManager : Singleton<VfxManager>`
```csharp
public enum VisualEffect { }                               // EMPTY today
[Serializable] class VisualEffectSpec { [SerializeField] ParticleSystem prefab; [SerializeField] int defaultPoolSize; [SerializeField] int maxPoolSize; }
[SerializeField] EnumDictionary<VisualEffect, VisualEffectSpec> effectSpecs;   // prefab: pairs: []
public void PlayVisualEffect(VisualEffect effect, Vector3 position);           // world position only
```
Internals: `Awake` adds one hidden `VisualEffectPool` component per spec to the VfxManager GameObject (DontDestroyOnLoad).
Each pool is `UnityEngine.Pool.ObjectPool<ParticleSystem>(create, get, release, destroy, collectionCheck:false, defaultPoolSize, maxPoolSize)`.
- Create: `Instantiate(prefab, vfxManager.transform)`, add a `Returner`, set `main.stopAction = Callback` on the **root** only.
- Get: `SetActive(true)` + `Play()`. `PlayVisualEffect` then sets `transform.position` and calls `Play()` again.
- Return: `Returner.OnParticleSystemStopped()` → `pool.Release` → `SetActive(false)`.
Gotchas:
- **No prewarm.** `defaultPoolSize` is only list capacity, so the first use instantiates (hitch). Extend with `Prewarm()`.
- `maxPoolSize` caps how many are *kept*, not how many are alive. Extra releases are destroyed. `maxPoolSize <= 0` makes
  the `ObjectPool` constructor throw `ArgumentException` inside `Awake`, and the specs after it never initialize.
- Only the root gets the stop callback. If a child outlives the root (e.g. ETFX `FrostExplosion`: root `lengthInSec 1`,
  one child 5 s), the instance may be released (hidden) **before the children finish**. (UNVERIFIED whether Unity waits
  for child systems before the callback. Make root duration + lifetime ≥ every child in sanitized copies.)
- Looping systems never stop, so they **never return**. There is no handle to stop them.
- Instances are children of VfxManager (scale 1). ETFX `scalingMode: 0` (Hierarchy), so scaling the instance transform
  works, but the API has no scale parameter. **The instance keeps the prefab's layer**: put VFX prefabs on the layer the
  HD-2D low-res world camera renders.
- Particles use scaled time (`useUnscaledTime: 0`), so they freeze with hit-stop or pause. That is usually what you want.

### 4.2 Epic Toon FX: useful prefabs (root = `Assets/Libraries/Epic Toon FX/Prefabs/`)
PS = ParticleSystems in prefab, L = has Light, A = has AudioSource, loop = looping PS count.

| Use (TDD `VisualEffect`) | Prefab | PS / L / A / loop |
|---|---|---|
| HitSpark (small hit) | `Combat/Brawling/Toon/Radial/ToonRadialPunchLight.prefab` (also `…Medium`, `…Heavy`, `…Extreme`) | 3/–/–/0 |
| HitSpark alt (colored) | `Combat/Brawling/RoundHit/RoundHit{Blue,Green,Red,Yellow}.prefab` | 3/–/–/0 |
| WallSpark | `Combat/Explosions (Misc)/SparkExplosion.prefab` | 2/–/–/0 |
| CritSpark | `Combat/Sword/Hit/SwordHitCritical/SwordHit{Yellow,Blue,Red,Green}Critical.prefab`; mini: `Sword/Hit/SwordHitMini/*` | 5/–/–/0 |
| Text pops | `Combat/Explosions (Text)/{Critical,Pow,Boom,KaPow,Hit,Miss,Zap,Wow,…}.prefab` | 4–5/–/–/0 |
| EnemyPoof | `Combat/Death/Skulls/CuteDeath.prefab`, `Combat/Death/Souls/SoulGenericDeath.prefab`; element variants `Skulls/{Fire,Frost,Poison,ElectricDeathBlue}Death` | 4–5/–/A/0 |
| BossPoof | `Combat/Explosions/MegaExplosion/MegaExplosion{Yellow,Red,Blue,Green,Pink}.prefab` (heavy; rare use only) | 10/–/A/0 |
| Explosion | `Combat/Explosions/SmallExplosion/SmallExplosionFire.prefab`; `FireballRoundExplosion/ExplosionFireballFire.prefab` | 5/–/A/0 |
| FreezeBurst | `Combat/Explosions/SnowExplosion/SnowExplosion.prefab` (4/L/A), `FrostExplosion/FrostExplosion.prefab` (6/L/A), `Combat/Nova/Frost/NovaFrost.prefab` (6/L/A) | |
| BurnBurst / burning status | `Combat/Explosions/MagicExplosion/MagicExplosionFire.prefab`; status loop `Environment/Fire/Cartoon/Radial/ToonRadialFire{Red,Blue,…}.prefab` (3 loop); burning-ball trail `Environment/Fire/Trails/ToonFireTrail.prefab` (3 loop) | |
| LightningHit | `Environment/Lightning/Soft Strike/LightningStrike{Blue,Pink}.prefab` (5), `Combat/Explosions/LightningExplosion/LightningExplosion{Yellow,Blue,…}.prefab` (5/L/A), `Combat/Nova/Lightning/NovaLightning*` | chain arcs: no ETFX beam, so use a LineRenderer |
| PoisonBurst | `Combat/Explosions (Misc)/PoisonExplosion.prefab` (2/–/A), `PoisonExplosionSoft`, `PoisonSkullExplosion`; `Combat/Explosions/GasExplosion/GasExplosionGreen.prefab` | |
| HealSparkle | `Interactive/Healing/HealOnce.prefab` (3), `HealNova.prefab` (4), `HealOnceBurst` | |
| Shield (status loop) / Blocked | `Combat/Shield/ShieldSoft{Blue,…}.prefab` (1 loop), `Combat/Magic/Shield/MagicShield*` (2 loop); block burst `Combat/Brawling/RoundHit/RoundHitBlue.prefab` | |
| SplitPop | `Interactive/Loot/ItemSparkleBurst/ItemSparkleBurst{Yellow,…}.prefab` (2, 0.3 s, cheapest), `Interactive/Stars/StarPoof.prefab` (5) | |
| PortalFlash / portal loop | `Interactive/Portals/SimplePortal/SimplePortal{Purple,Blue,…}.prefab` (4 loop), `SwirlPortal/*` (2 loop + ETFXRotation) | |
| PickupSparkle | `Interactive/Powerups/PowerupActivate/PowerupActivate{Yellow,…}.prefab` (2); coins `Interactive/Money/Coins/GoldCoinBlast.prefab` (4) | |
| LevelUpBurst | `Interactive/Level Up/Nova/LevelupNova{Yellow,…}.prefab` (4); `…/Cylinder/LevelupCylinder*` (6) | |
| DustPuff (enemy advance, landing) | `Environment/Dust/DustDirtyPoof.prefab`, `DustDirtyPoofSoft.prefab` | |
| Stage clear / victory | `Environment/Confetti/Blast/ConfettiBlastRainbow.prefab` (3), `Environment/Firework/Firework*` | |
| Telegraphs | `Interactive/Warning/Warning{Skull,Exclamation,Bolt,Explosive}.prefab` (2, 1 loop); stun `Combat/Brawling/Stun/StunnedCirclingStarsSimple.prefab` (loop) | |
| Cue charge | `Combat/Magic/Charge/MagicCharge{Blue,Green,Yellow}.prefab` (3 loop) | |
| Ambient (acts) | `Environment/Dust/DustMotes{Calm,Lively}`, `Environment/Fireflies/*`, `Environment/Stars/StarFog/*` | |

Typical prefab hierarchy: the root GO has a ParticleSystem, with 2–9 child GOs, each with a ParticleSystem (+renderer).
Example `ToonRadialPunchMedium` → children `Sparks`, `ImpactGlow`. `FrostExplosion` → `FrostSmoke`, `SnowFlakes`,
`IceDebris`, `CentralOrb`, `Glow`, with the Light + `ETFXLightFade` on one child. Most systems are
`moveWithTransform: 0` (Local). `cullingMode: 3` (AlwaysSimulate). Renderers: 2966 Billboard, 943 Stretch, 542 Mesh.

ETFX scripts (`namespace EpicToonFX`): `ETFXLightFade` (dims a Light over `life`, default `onLifeEnd = Destroy` → the
component is gone after the first pooled play), `ETFXPitchRandomizer` (randomizes `AudioSource.pitch` in Start),
`ETFXRotation` (rotates in Update).

### 4.3 URP compatibility (verified from material YAML)

| Shader reference | Count | Meaning | URP result |
|---|---|---|---|
| `{fileID: 211, guid: 0000…f000…}` | 300 | built-in **Particles/Standard Unlit** (`_Mode`: 205 Fade, 77 Additive, 52 Opaque) | magenta; upgrade with `ParticleUpgrader` |
| `{fileID: 46, …}` | 37 | built-in **Standard** (demo + Powerbox metal + models) | magenta; `StandardUpgrader` |
| `ETFX_PowerboxUnlit` / `Lit` (Amplify, `CGPROGRAM #pragma surface`) | 17 | built-in surface shader | not URP; avoid Powerbox prefabs |
| `{fileID: 104}` / `210` / `208` | 4 | skybox / legacy (demo only) | ignore |

- `Upgrade/URP Compatibility.txt` refers to an "ETFX 2019.4.24f1 URP Upgrade" package that is **not present** (it is
  missing in the shared repo too).
- Soft particles are on in 90 materials, camera fading in 27, distortion in 0. URP asset: `m_RequireDepthTexture: 0`,
  **`m_SupportsHDR: 0`**, MSAA 1. Soft particles need the depth texture (otherwise they turn invisible), so turn them
  off. With HDR off, bloom works in LDR: additive glows must push close to white, and the bloom threshold must be < 1.
- Textures: 335 files, 83×512², 35×1024², 20×2048², 2×4096². For Android, override max size to 256–512 on the curated set.

### 4.4 Conversion (editor script; URP 17.3 APIs verified in PackageCache)
```csharp
using UnityEditor.Rendering;                    // MaterialUpgrader (core)
using UnityEditor.Rendering.Universal;          // ParticleUpgrader, StandardUpgrader
using UnityEditor.Rendering.Universal.ShaderGUI; // ParticleGUI
var upgraders = new List<MaterialUpgrader> {
    new ParticleUpgrader("Particles/Standard Unlit"),   // → "Universal Render Pipeline/Particles/Unlit"
    new ParticleUpgrader("Particles/Standard Surface"), // → ".../Particles/Lit"
    new StandardUpgrader("Standard") };
foreach (var matPath in AssetDatabase.GetDependencies(curatedPrefabPath, true).Where(p => p.EndsWith(".mat")))   // using System.Linq
{
    var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
    if (m.shader.name.StartsWith("Universal Render Pipeline")) continue;
    MaterialUpgrader.Upgrade(m, upgraders, MaterialUpgrader.UpgradeFlags.None); // _Mode→_Surface/_Blend, _MainTex→_BaseMap, _Color→_BaseColor
    m.SetFloat("_SoftParticlesEnabled", 0); m.SetFloat("_CameraFadingEnabled", 0);
    if (m.shader.name.Contains("/Particles/"))                                   // Lit (from Standard) validates itself on import
        BaseShaderGUI.SetMaterialKeywords(m, null, ParticleGUI.SetMaterialKeywords); // same as ParticlesUnlitShader.ValidateMaterial
    EditorUtility.SetDirty(m);
}
AssetDatabase.SaveAssets();
```
In-place upgrade of library `.mat` files through Unity is allowed (Unity writes them). Built-in shaders are broken in
URP anyway. Alternative: the GUI Render Pipeline Converter ("Material Upgrade"), but it is not CLI-friendly.

### 4.5 Sanitizing curated prefabs for pooling + low-end Android
Copy with `AssetDatabase.CopyAsset(src, "Assets/Prefabs/BilliardRogue/Vfx/<VisualEffect>.prefab")` so Unity mints a new
GUID. Then open it with `PrefabUtility.LoadPrefabContents` and, for every `ParticleSystem` in children:
- Remove `Light`, `ETFXLightFade`, `AudioSource`, `ETFXPitchRandomizer` (`Object.DestroyImmediate(c, true)`). Audio goes
  through SfxManager. Lights cost URP per-object light slots.
- `collision.enabled = false` (world collision in 276 prefabs). Set `noise.enabled = false` (145 prefabs) unless it is essential.
  Set `trails.enabled = false` (38) unless it is essential.
- `main.maxParticles` ≤ 32–64. `main.cullingMode = Automatic` for bursts. `playOnAwake` can stay (the pool calls Play).
- Renderer: `maxParticleSize` ≤ 0.2 (values up to 4 exist, which means full-screen quads). `shadowCastingMode = Off`,
  `receiveShadows = false`. Set `sortingFudge`/`sortingOrder` consistently.
- Root: `main.duration` and `startLifetime` ≥ the longest child, so the root stop callback fires last.
- Set the layer to the HD-2D world-FX layer. Use a uniform scale on the root if needed.
Cost model:
- About **1 draw call per sub-system renderer**, so an ETFX hit ≈ 3–6 DC. 10 concurrent hits ≈ 30–60 DC.
  (UNVERIFIED: whether URP dynamic-batches particle systems that share a material. Check in the Frame Debugger.)
- The main fill-rate risk is the big additive `Glow`/`Nova` quads at 1080p. Rendering the world (including particles)
  into the TDD's 640×360 low-res RT cuts overdraw cost by about 9×. Budget ≤ 12 concurrent pooled FX and
  ≤ 400 live particles.

---

## 5. `ObjectPooler<T>` (`Nex`)
```csharp
public interface IPoolableObject { public event Action<Component>? OnRelease; }
public abstract class ObjectPooler<T> : MonoBehaviour where T : MonoBehaviour, IPoolableObject
{
    public void Initialize(T prefab, int defaultCapacity = 10);  // collectionCheck false, no maxSize → default 10000
    public T Get();                                             // SetActive(true); tracked in ActiveInstances
    public IEnumerable<T> ActiveInstances { get; }
    protected virtual T CreatePooledItem();                     // Instantiate(prefab, transform) + subscribe OnRelease
    public void OnDestroy();                                    // disposes pool — NOT virtual
}
```
- It is abstract, so declare `sealed class DamageNumberPool : ObjectPooler<DamageNumber> {}`. The item raises
  `OnRelease?.Invoke(this)` to return itself (the pooler then calls `SetActive(false)`).
- Gotcha: if the subclass declares its own `void OnDestroy()`, Unity calls only that one and the pool is never disposed.
  Don't declare it (or call `base.OnDestroy()` explicitly).
- There is no reset hook: reset state in your own `Spawn(...)` after `Get()`.
- Use it for damage numbers, HP bars, balls, enemy views and aim-dots. Use `VfxManager` for one-shot particles.

---

## 6. Feel (MMFeedbacks 5.8)
- `MMF_Player : MMFeedbacks` (`Core/MMF_Player/MMF_Player.cs`). Legacy `MMFeedbacks : MonoBehaviour` (`Core/Legacy/MMFeedbacks.cs`).
  **All 16 project prefabs use `MMF_Player`**, serialized into fields typed `MMFeedbacks` (`SimpleView.entryAnimator`,
  `toBackgroundAnimator`, `PreviewsManager.moveInAnimator/moveOutAnimator`, `ScreenBlockerManager`). Only
  `MMF_Position` and `MMF_CanvasGroup` feedbacks are used. Always add `MMF_Player`, never the legacy component.
- Feedback lists are `[SerializeReference] List<MMF_Feedback> FeedbacksList`. Build them in editor scripts with
  `player.AddFeedback(typeof(MMF_Scale))` (returns `MMF_Feedback`), then set fields and `SaveAsPrefabAsset`.
- Useful players and fields: `PlayFeedbacks()`, `PlayFeedbacks(Vector3 pos, float intensity)`, `StopFeedbacks()`,
  `SkipToTheEnd()`, `RestoreInitialValues()`, `IsPlaying`, `TotalDuration`, `DurationMultiplier`, `Direction`,
  `InitializationMode` (Start default), `CanPlayWhileAlreadyPlaying = true`, `CooldownDuration`, `ChanceToPlay`,
  `ForceTimescaleMode` + `ForcedTimescaleMode` (**set Unscaled for pause/UI players**), `StopFeedbacksOnDisable`,
  `GetFeedbackOfType<T>()`.

```csharp
// Nex.Utils.MMFeedbacksExtension
public static UniTask PlayAsUniTask(this MMFeedbacks feedbacks, bool animate = true,
    MMFeedbacks.Directions direction = MMFeedbacks.Directions.TopToBottom, bool reverted = false,
    float durationMultiplier = 1f, CancellationToken cancellationToken = default);
// sets feedbacks.Direction (flipped if reverted) and feedbacks.DurationMultiplier = (animate?1:0.001)*durationMultiplier (persists!)
// then awaits PlayFeedbacksCoroutine(Vector3.zero).ToUniTask(ct)  → loops while IsPlaying
```
- `MainInitializerExample` notes `ScreenBlockerManager.Hide()` finishing "within 0.1 s". Expect `PlayAsUniTask` to
  return early if the player is disabled or inactive (`IsPlaying` false).
- Asm defines: all of Feel compiles into `MoreMountains.Tools`, and `versionDefines` set `MM_URP`, `MM_TEXTMESHPRO`, `MM_UI`, …
  So `MMF_Bloom_URP`, `MMF_Vignette_URP`, `MMF_ChromaticAberration_URP` etc. are available (they need the matching
  `MM*Shaker_URP` on a Volume). Cinemachine feedbacks are inert (package absent).
- Good for Billiard Rogue: `MMF_ScaleShake`/`MMF_SquashAndStretch`/`MMF_PositionShake` (enemy hit), `MMF_Flicker`
  (hit flash; set `UseMaterialPropertyBlocks = true` and `Mode = PropertyName` with your cel shader's color property.
  The default `_Color` is not URP's `_BaseColor`), `MMF_CameraShake` (needs `MMCameraShaker` + `MMWiggle` on the camera
  rig), `MMF_TMPCountTo`, `MMF_TMPTextReveal`, `MMF_CanvasGroup`, `MMF_Position`, `MMF_ImageSpriteSequence` /
  `MMF_SpriteRenderSpriteSequence` (Nex, 12 fps pixel sprites).
- Avoid: `MMF_FreezeFrame` / `MMF_TimescaleModifier`. They auto-create `MMTimeManager`, which owns `Time.timeScale` and
  fights pause and `DebugTimeScaleChanger` (T key). Write a small gameplay-owned hit-stop instead.

---

## 7. DOTween (`team.nex.as-dotween` 1.2.632)
- DLL + modules: `DOTweenModuleAudio` (`AudioSource.DOFade`, `DOPitch`, `AudioMixer.DOSetFloat`), `…UI`
  (`Image.DOFade/DOColor/DOFillAmount`, `RectTransform.DOAnchorPos`, `CanvasGroup.DOFade`), `…Sprite`, `…Physics(2D)`,
  `…UnityVersion`, `…Utils`, `…EPOOutline`. **No TMP module**, so use `DOTween.To(() => v, x => { v = x; tmp.text = …; }, target, d)`.
- Settings (`DOTween/Resources/DOTweenSettings.asset`, inside PackageCache, read-only): `useSafeMode: 1`,
  `defaultAutoKill: 1`, `defaultRecyclable: 0`, `defaultTimeScaleIndependent: 0`, `defaultEaseType: 6` (OutQuad).
  Do **not** run the DOTween Utility Panel "Setup" (it writes into the package).
- UniTask bridge: `UNITASK_DOTWEEN_SUPPORT` is defined (Android/Standalone), so `await tween.WithCancellation(ct)`,
  `await tween.ToUniTask(TweenCancelBehaviour.Kill, ct)`.
- Use `.SetUpdate(true)` for anything that must run while paused. Use `.SetLink(gameObject)` to auto-kill on destroy.
  For many tweens, call `DOTween.SetTweensCapacity(500, 100)` once at boot (UNVERIFIED need: it auto-grows with a warning).

---

## 8. ChocDino UIFX (uGUI filters, `namespace ChocDino.UIFX`)
- Filters (components on a uGUI `Graphic`, add menu `UI/Chocolate Dinosaur UIFX/Filters/…`): `GlowFilter`,
  `OutlineFilter`, `DropShadowFilter`, `BlurFilter`, `BlurDirectionalFilter`, `BlurZoomFilter`, `ColorAdjustFilter`,
  `DissolveFilter`, `FillColorFilter`, `FillGradientFilter`, `FillTextureFilter`, `FrameFilter`, `PixelateFilter`,
  `ExtrudeFilter`, `LongShadowFilter`, `GooeyFilter`, `DoomMeltFilter` (beta), `MipmapFilter`. Effects: `MotionBlurReal`,
  `MotionBlurSimple(TMP)`, `TextLetterSpacing`, `VertexSkew(TMP)`, Trail. For TMP, add `FilterStackTextMeshPro`
  (`ChocDino.UIFX.TMP`).
- Common API (`FilterBase`): `float Strength` (0..1, 0 = off, skip render), `FilterRenderSpace RenderSpace` (Canvas/Screen), `ForceUpdate()`.
  - `GlowFilter`: `EdgeSide`, `DistanceShape`, `MaxDistance` (≤1024), `FalloffMode` (Exponential/Curve), `ExpFalloffEnergy/Power/Offset`,
    `FillMode` (Color/Gradient/Texture), `Color`, `Gradient`, `Blur` (≤28), `NoiseScale`, `Additive`, `SourceAlpha`, `ReuseDistanceMap`.
  - `OutlineFilter`: `Method` (DistanceMap…), `Size`, `DistanceShape`, `Blur`, `Softness`, `SourceAlpha`, `FillMode`, `Color`, `Gradient`, `GradientLinearAngle`, `Direction` (Outside/…).
- Cost: each filtered Graphic renders into RenderTextures, with GPU passes whenever it is dirty (any property change →
  `ForceUpdate`). Use it for **static** glow and outline on reward cards, title logo, and focused buttons. Don't animate
  filter params every frame. Pulse with `CanvasGroup`/`Image` alpha (MMF/DOTween) on the already-filtered graphic instead.
  It is not used anywhere in the project yet (0 references).

---

## 9. Analytics

### 9.1 Pipeline
`AnalyticsManager (Nex, StartUpSingletons)` → `Nex.Platform.GameAnalytics.Instance` (plain C# singleton) → provider:
- Editor / fallback builds: `DefaultGameAnalyticsServiceProvider` (`team.nex.default-app-support`) is **dry-run**. It logs
  `[GameAnalytics(dry-run)] Track c_<event>: {json}` (only `#if !PRODUCTION`), which is how you verify in the Editor.
- Playground builds (`Env.isPlaygroundBuild`, OLYMPIA or device `NexNPG001`): `NexGameAnalyticsServiceProvider` sends
  over AIDL to `team.nex.playground.launcher` (`…analytics.AnalyticsService`). The launcher owns delivery.
- `Assets/Resources/Mixpanel.asset` (`ShowDebug 0`, `ManualInitialization 0`, `APIHostAddress https://api.mixpanel.com/`,
  Runtime/Debug tokens set, `FlushInterval 60`). The Mixpanel SDK self-initializes. `MixpanelGameAnalyticsServiceProvider`
  exists in platform-utils, but **no binding registers it**. Never call `Mixpanel.*` directly. (UNVERIFIED: whether the
  Mixpanel SDK sends any automatic events on its own.)
- MDK already logs `c_mdk_camera_opened/closed`, `c_mdk_detection_started/stopped`, `c_mdk_tracking_started/stopped`
  (internal `Jazz.AnalyticsManager`, no name clash).

### 9.2 API (verbatim signatures)
```csharp
// Nex.AnalyticsManager : Singleton<AnalyticsManager>
public void TrackEvent(string eventName, GameAnalyticsProperties? props = null);          // → GameAnalytics.Track → "c_"+eventName
public void TrackPause();                                                                  // no props overload
public void TrackResume();
public void TrackGameStart(string screen, int numPlayer, string gameMode="default", GameAnalyticsProperties? props=null); // "screen" is stored as content_name
public void TrackGameStop(GameAnalyticsProperties? props=null);
public void TrackScreen(string screenName, GameAnalyticsProperties? props = null);        // ViewManager calls this on every ActivateView with non-empty AnalyticsScreenName

// Nex.Platform.GameAnalytics (not wrapped yet — add wrappers to AnalyticsManager)
public void SendStartSetupSessionEvent(String game, int numPlayers, String gameMode="default", GameAnalyticsProperties? props=null, GameAnalyticsDisposableContext? parentContext=null);
public void SendPauseSetupSessionEvent(GameAnalyticsProperties? props=null, …);  public void SendResumeSetupSessionEvent(…);
public void SendStopSetupSessionEvent(GameAnalyticsProperties? props=null, …);
public void SendPausePlaySessionEvent(GameAnalyticsProperties? props=null, …);  public void SendResumePlaySessionEvent(…);
public void SendErrorEvent(GameAnalytics.ErrorTypeEvent errorType, GameAnalyticsProperties? props=null, …); // LOGIN, CONNECTION, USER, INTERNAL, CAMERA, STORAGE, TRACKING_LOSS
public void RegisterSessionProperties(String key, GameAnalyticsValue value, SessionPropertiesScope scope = INSTALL); // INSTALL | APP | PLAY
public void RegisterSessionProperties(GameAnalyticsProperties props, SessionPropertiesScope scope = INSTALL);
public void UnregisterSessionProperties(String key, SessionPropertiesScope scope = INSTALL);
public UniTask<bool> Initialized { get; }
public static GameAnalyticsOptions Options { get; }   // .gameNoPauseState
```

### 9.3 Building properties (`Nex.Platform.GameAnalyticsProperties : Dictionary<string, GameAnalyticsValue>`)
- Public ctor: `new GameAnalyticsProperties()`. Members: `new void Add(key, value)` (**throws on a duplicate key**),
  `new this[key] { get; set; }` (upsert), `Remove`, `Clear`, `Merge(other, backup=false)`, `SanitizeKey()`, `ToJsonString()`.
- `GameAnalyticsValue` has implicit conversions from `bool, int, float, double, string, char, GameAnalyticsProperties`
  and from arrays of each (`int[]`, `string[]`, …). There is **no `long`, enum, or Vector** conversion: use
  `(double)longVal`, `enumVal.ToString()`, or split `x`/`y`. Floats serialize with InvariantCulture. NaN/Infinity
  become strings.
- Reserved keys (stripped by `SanitizeKey()` in Track): anything starting with `$`, `distinct_id`, `user_id`,
  `device_tracking_id`, `customer_tracking_id`, `mp_lib`, `time`, `token`.
```csharp
AnalyticsManager.Instance.TrackEvent("shot_fired", new GameAnalyticsProperties
{
    ["turn"] = 3, ["ball_type"] = BallType.Fire.ToString(), ["angle_deg"] = 72.5f, ["power"] = 0.8f, ["shooter"] = 0,
});
```

### 9.4 Semantics and gotchas (from `GameAnalytics.cs`)
- `Track`: final = event props + `{custom_event:true}` + session props (PLAY scope while a play session exists, else APP).
  Merges use `backup:true`: **a session prop overwrites an event prop with the same key** (the original moves to
  `o_<key>`). Keep the key sets disjoint.
- `SendStartPlaySessionEvent`: auto-stops a running setup session (`reason: GAME_START`, `auto: true`),
  **resets PLAY-scope session props**, and stores `content_name`, `players`, `game_mode`. Register PLAY props *after*
  `TrackGameStart`. Every play event carries `play_session_id`, `duration`, `delta_duration`, `play_session_state`.
- `Pause` is a no-op unless live. `Resume` is a no-op unless paused. `Stop` auto-resumes a paused session first, then
  resets PLAY props. App background automatically pauses the play session and **stops** the setup session
  (`APP_PAUSE`). Double calls are harmless no-ops.
- Screen events: each `TrackScreen` first sends `screen_exit` for the previous screen (with duration). Screen names in the
  project are kebab-case (`"title"`, `"debug-settings"`). ViewManager re-sends the screen event when a view becomes top
  again after a pop (fine).
- `GAME_STOP` also submits `GameHub` metrics `ga::play-session-duration` / `ga::play-session-count`.
- Privacy (org policy): never log player names, photos, camera frames, free text, or device identifiers. Log only
  enums, counts, and timings.

### 9.5 Event taxonomy for Billiard Rogue
Conventions: event names are `snake_case` (sent as `c_<name>`). Keys are `snake_case`. Ids are enum `ToString()`.
Time is in seconds (float, 1 dp). Angles are degrees. `power` is 0..1. Screens are kebab-case via `AnalyticsScreenName`.
PLAY-scope session props registered right after `TrackGameStart`: `run_id` (Guid "N"), `num_players`, `is_continue`,
`hero` (e.g. "cat"), `build_ver`. **Do not reuse these keys in event props.**

| Event (call) | When | Props |
|---|---|---|
| `TrackScreen` (auto) | every view: `title`, `player-mode`, `calibration`, `gameplay`, `stage-intro`, `reward`, `pause`, `tracking-lost`, `summary`, `settings`, `debug-settings` | — |
| `ui_button_click` | every button/remote action | `screen`, `button`, `index` (int, −1 if n/a), `input` ("remote"/"keyboard"/"motion") |
| `ui_back` | back button / Esc | `screen` |
| `settings_changed` | on commit of each setting | `setting`, `old_value`, `new_value` (string or float) |
| `secret_code_entered` | SecretCodeSequenceDetector fired | `code` = "konami", `screen` |
| setup session: `SendStartSetupSessionEvent("billiard_rogue", n, "setup")` | calibration view enters | — |
| `setup_step` | each SetupStateManager step reached | `step` (enum), `player_index`, `elapsed_s` |
| `setup_complete` | all players calibrated | `num_players`, `duration_s`, `retries` |
| `setup_failed` / `setup_skipped` | timeout / debug skip | `step`, `duration_s` |
| `SendStopSetupSessionEvent` | leaving calibration without starting | `reason` |
| **`TrackGameStart("billiard_rogue", numPlayers, mode)`** | run begins (mode "new"/"continue"/"tutorial") | `start_act`, `start_stage`, `seed` |
| `stage_start` | stage intro done | `act`, `stage`, `stage_kind` ("normal"/"elite"/"boss"), `enemy_count`, `hp`, `hp_max`, `balls` (string[]) |
| `turn_start` | player turn begins | `act`, `stage`, `turn`, `player_index`, `hp`, `balls_in_hand`, `enemies_alive`, `front_row` (rows to wall) |
| `shot_fired` | each strike detected | `turn`, `shot_index`, `player_index`, `ball_type`, `ball_level`, `angle_deg`, `power`, `is_power_shot`, `aim_time_s`, `input` ("paw"/"debug"/"bot") |
| `ball_result` | ball returned/destroyed (split children aggregated into parent) | `turn`, `shot_index`, `ball_type`, `hits`, `bounces`, `damage`, `kills`, `crits`, `max_combo`, `status_applied` (string[]), `flight_s` |
| `turn_end` | after enemy phase | `turn`, `shots`, `damage_dealt`, `kills`, `enemies_advanced`, `damage_taken`, `blocked`, `hp`, `enemies_alive` |
| `boss_spawn` | boss enters | `boss_id`, `act`, `stage`, `boss_hp` |
| `boss_defeated` | boss HP 0 | `boss_id`, `turns`, `duration_s`, `hp` |
| `stage_clear` | last enemy dead | `act`, `stage`, `turns`, `duration_s`, `damage_taken`, `hp`, `kills` |
| `reward_offered` | reward view shown | `act`, `stage`, `source` ("stage"/"elite"/"boss"), `options` (string[]), `option_levels` (int[]) |
| `reward_chosen` | pick or skip | `choice` (id or "skip"), `index` (−1 skip), `options` (string[]), `decision_s` |
| `level_up` | ball upgraded | `ball_type`, `new_level` |
| `TrackPause()` / `TrackResume()` | pause view push/pop, `CherryIntegrationManager.PreferGameStopped` | (extend AnalyticsManager to accept props: `reason` = "menu"/"platform"/"tracking_lost") |
| `tracking_lost` (+ `SendErrorEvent(TRACKING_LOSS)`) | ShotInputRouter TrackingLost | `player_index`, `phase` ("aiming"/"flight"/"menu") |
| `tracking_regained` | tracking back | `player_index`, `lost_s` |
| `run_end` | victory/defeat/abandon, sent **before** TrackGameStop | `result` ("victory"/"defeat"/"abandon"), `act`, `stage`, `turns_total`, `duration_s`, `kills_total`, `damage_total`, `balls` (string[]), `cause` (enemy id or "") |
| **`TrackGameStop(props)`** | right after `run_end` | `result`, `act`, `stage`, `turns_total` |

Volume guard: 1 `shot_fired` + ≤ N `ball_result` per shot. Never log per-bounce or per-hit events. Aggregate them in the
simulation event drain.

### 9.6 Wrapper (recommended; name it `RunAnalytics`, not `GameAnalytics`)
```csharp
// Assets/Scripts/BilliardRogue/Analytics/RunAnalytics.cs
using Nex.Platform;
namespace Nex.BilliardRogue
{
    public static class RunAnalytics
    {
        public const string ContentName = "billiard_rogue";
        static AnalyticsManager A => AnalyticsManager.Instance;
        public static void UiAction(string screen, string button, int index = -1, string input = "remote") =>
            A.TrackEvent("ui_button_click", new GameAnalyticsProperties { ["screen"] = screen, ["button"] = button, ["index"] = index, ["input"] = input });
        public static void SessionStart(int numPlayers, bool isContinue, int seed)
        {
            A.TrackGameStart(ContentName, numPlayers, isContinue ? "continue" : "new", new GameAnalyticsProperties { ["seed"] = seed });
            var ga = GameAnalytics.Instance;                           // PLAY props AFTER start (start resets them)
            ga.RegisterSessionProperties("run_id", System.Guid.NewGuid().ToString("N"), GameAnalytics.SessionPropertiesScope.PLAY);
            ga.RegisterSessionProperties("num_players", numPlayers, GameAnalytics.SessionPropertiesScope.PLAY);
        }
        public static void ShotFired(int turn, int shotIndex, int player, string ballType, int level, float angleDeg, float power, bool powerShot, string input) =>
            A.TrackEvent("shot_fired", new GameAnalyticsProperties { ["turn"] = turn, ["shot_index"] = shotIndex, ["player_index"] = player,
                ["ball_type"] = ballType, ["ball_level"] = level, ["angle_deg"] = Mathf.Round(angleDeg * 10) / 10, ["power"] = power,
                ["is_power_shot"] = powerShot, ["input"] = input });
        public static void RunEnd(string result, int act, int stage, int turns, float seconds, int kills, string[] balls)
        {
            var p = new GameAnalyticsProperties { ["result"] = result, ["act"] = act, ["stage"] = stage, ["turns_total"] = turns,
                ["duration_s"] = seconds, ["kills_total"] = kills, ["balls"] = balls };
            A.TrackEvent("run_end", p);
            A.TrackGameStop(new GameAnalyticsProperties { ["result"] = result, ["act"] = act, ["stage"] = stage, ["turns_total"] = turns });
        }
        // … TurnStart/TurnEnd/BallResult/StageStart/StageClear/RewardOffered/RewardChosen/BossSpawn/BossDefeated/TrackingLost/SettingChanged/SetupStep
    }
}
```
Add to `AnalyticsManager` (the single owner, keep its Debug.Log style): `TrackSetupStart(int numPlayers)`,
`TrackSetupStop(GameAnalyticsProperties? props)`, `TrackError(GameAnalytics.ErrorTypeEvent type, GameAnalyticsProperties? props)`,
and props-accepting `TrackPause(GameAnalyticsProperties? props = null)` / `TrackResume(...)`. Existing callers keep compiling.

---

## 10. Audio asset plan (team shared repo)

### 10.1 Repo facts (checked)
- Location: `~/Documents/music-cell-shared-assets` (README states NEX owns usage rights). The Unity project is nested,
  so asset paths are `music-cell-shared-assets/Assets/<pack>/…`.
- `remote.origin` is a **partial clone `[blob:none]`**. `core.sparseCheckout=true`, **`core.sparseCheckoutCone=true`**.
  Current sparse set: `music-cell-shared-assets/Assets/NEX/BGM` and `…/NEX/Sound effect` (these files are real WAVs on
  disk; NEX BGM are full songs of tens of MB each, e.g. `2050 - Turbo Power.wav` is 42 MB and `Dor Friedman - Elves Ceremony.wav` is 169 s).
- All audio is **Git LFS** (blobs are ~130-byte pointers; sizes below come from the pointers). There is no local LFS
  object for anything outside the sparse set.
- Cone mode accepts **directories only**. `git sparse-checkout add <file>` is rejected or treated as a directory, so
  adding e.g. `Ultimate Game Music Collection/Combat` would pull the whole folder (~75 tracks of 7–20 MB each).
  Prefer §10.4 option A.
- Durations below are **estimates**: size ÷ 176 400 B/s (44.1 kHz/16-bit stereo). Run `ffprobe` after materializing.
- The GDD says "pitch rises with combo". Use the in-project `Assets/Audio/Sfx/Scales/Set1` (12 notes), UGMC
  `Short Cues/Scales/Combo Hit 1–7.wav`, or Merge `SFX/Merge/Rising_Tone/SFX_Merge_RisingTone_01Bb…07Ab.wav`.

### 10.2 BGM + stingers (dest `Assets/Audio/Bgm/BilliardRogue/<BgmType>.wav`; all loops are UGMC "LOOP" edits)
UGMC = `Ultimate Game Music Collection/`

| Slot (`BgmType`/stinger) | Primary | Size / est. | Alternates |
|---|---|---|---|
| `Title` (cozy, adventurous) | UGMC `Fantasy Orchestral/Upbeat City LOOP.wav` | 14.8 MB / ~88 s | UGMC `Locations/Tavern LOOP LIVELY.wav` (14.5 MB, cozy); UGMC `Platform/Soaring LOOP NO SOLO.wav` (14.6 MB); NEX-owned full songs (not loop-edited): `NEX/BGM/Ian Post - Wee Folk.wav`, `Dor Friedman - Elves Ceremony.wav` |
| `Act1` (light dungeon) | UGMC `Combat/Skeletons LOOP.wav` | 7.2 MB / ~43 s | UGMC `Platform/Platform Action LOOP.wav` (7.5 MB) |
| `Act2` (darker dungeon) | UGMC `Combat/Dark Dungeon ACTION LOOP.wav` | 8.6 MB / ~51 s | UGMC `Combat/Enemies LOOP.wav` (21.5 MB) |
| `Act3` (epic) | UGMC `Combat/Epic Combat LOOP.wav` | 8.1 MB / ~48 s | UGMC `Combat/Frantic Battle LOOP.wav` (15.5 MB) |
| `Boss` | UGMC `Combat/Boss Battle 1 Loop.wav` | 14.6 MB / ~87 s | `Boss Battle 3 Loop.wav` (15.5 MB); final boss `Boss Battle 4 Loop.wav` (12.9 MB); intro layer `Boss Battle 1 (drums only) Loop.wav` (14.6 MB) |
| `Reward` (calm shop) | UGMC `Short Cues/Item Stores/Item Store 1/Item Store 1 LOOP.wav` | 2.7 MB / ~16 s | `Item Store 2/Item Store 2 LOOP.wav` (4.0 MB); `Puzzles/Calm Bright LOOP.wav` (13.3 MB); `Dungeons/Pretty Dungeon LOOP.wav` (12.1 MB) |
| Stinger `Victory` | UGMC `Short Cues/Winning/Triumphant Victory.wav` | 1.2 MB / ~7 s | `Short Cues/Winning/Fanfare Win.wav` (1.5 MB) |
| Stinger `Defeat`/`GameOver` | UGMC `Short Cues/Losing/Dark Defeat 1.wav` | 1.2 MB / ~7 s | `Dramatic Defeat SHORT.wav` (3.4 MB), `Casual Lose 2.wav` (0.95 MB) |
| Stinger `StageClear` | UGMC `Short Cues/Quest SFX/Quest Finish DRUMS SMALL.wav` | 1.0 MB / ~6 s | Merge `AUDIO/STINGER_SUCCESS/Stinger/STGR_Success_1.wav` (0.62 MB); UGMC `Short Cues/Winning/Casual Win 1.wav` (0.81 MB) |
| Stinger `BossAppear` | UGMC `Combat/Danger LOOP PARTS/Danger HORN.wav` | 0.61 MB / ~4 s | UGMC `Short Cues/Scares/Scare DRUMS BIG.wav` (1.0 MB) |
| Stinger `StageStart` (optional) | UGMC `Short Cues/Quest SFX/Quest Start DRUMS SMALL.wav` | 0.88 MB / ~5 s | — |

Raw total for the primary BGM ≈ 56 MB WAV. As Vorbis q0.55 it should be ≈ 5–7 MB in the APK.

### 10.3 SFX (dest `Assets/Audio/Sfx/BilliardRogue/<SoundEffect>_<n>.wav`)
U = `Universal Sound FX/`, C = `Cute UI _ Interact Sound Effects Pack/AUDIO/`, M = `Merge Games Sound Effects and Music Pack/AUDIO/`.
Picks are chosen by name. **Audition them before committing.** All sizes are under 0.35 MB unless noted.

| Request → TDD `SoundEffect` | Primary clip(s) | Alternates |
|---|---|---|
| cue_strike → `CueStrike` | U `SPORTS/Pool/POOL_Ball_Hit_03_mono.wav` | U `RETRO_LOFI/RETRO_Punch_03_mono.wav`; other `POOL_Ball_Hit_01…11` |
| ball_launch → `BallLaunch` | U `RETRO_LOFI/RETRO_Pew_01_mono.wav` | U `8BIT/Jumping/8BIT_RETRO_Jump_Glide_Up_Bright_Quick_mono.wav` |
| ball_wall_bounce → `BallWallBounce` (3 RR) | U `RETRO_LOFI/RETRO_Bump_01_mono.wav`, `RETRO_Bump_02_mono.wav`, `RETRO_Bump_03_mono.wav` | U `8BIT/Hits_Bumps/8BIT_RETRO_Hit_Bump_Distorted_Tap_Bright_mono.wav` |
| ball_enemy_hit ×3 → `BallHitSoft` | U `IMPACTS/Shoot_Em_Up/Short/IMPACT_ShootEmUp_Short_01_RR 01_mono.wav`, `…_RR 02_mono.wav`, `…_RR 03_mono.wav` | — |
| → `BallHitMid` | U `IMPACTS/Shoot_Em_Up/Medium/IMPACT_ShootEmUp_Medium_03_RR 01/02/03_mono.wav` | — |
| → `BallHitHard` | U `RETRO_LOFI/RETRO_Impact_01/02/03_mono.wav` | — |
| crit_hit → `CritHit` | U `RETRO_LOFI/RETRO_Melee_Attack_Kick_Punch_05_mono.wav` | U `8BIT/Hits_Bumps/8BIT_RETRO_Hit_Bump_Deep_Zap_mono.wav`; UGMC `Short Cues/Scales/Combo Hit 1–7.wav` (0.35–0.7 MB) for combo |
| enemy_death → `EnemyDeath` | U `RETRO_LOFI/RETRO_Destroy_01_mono.wav` | U `8BIT/Explosions/8BIT_RETRO_Explosion_Zap_Destroy_mono.wav` |
| boss_hit → `BossHit` (3 RR) | U `IMPACTS/Shoot_Em_Up/Medium/IMPACT_ShootEmUp_Medium_10_RR 01/02/03_mono.wav` | — |
| boss_death → `BossDeath` | U `RETRO_LOFI/RETRO_Explosion_Nuclear_01_mono.wav` | U `8BIT/Explosions/8BIT_RETRO_Explosion_Long_Sweep_Down_mono.wav` |
| explosion → `Explosion` | U `8BIT/Explosions/8BIT_RETRO_Explosion_Short_Deep_mono.wav` | U `RETRO_LOFI/RETRO_Explode_02_mono.wav`; in-project `etfx_explosion_grenade.wav` |
| freeze → `Freeze` | U `ELEMENTS/Ice/ICE_Cracking_04_mono.wav` (0.10 MB) + shatter U `SHATTER/SHATTER_Glass_Small_01_mono.wav` | in-project `etfx_explosion_frost.wav` (0.84 s). Avoid `ICE_Cracking_05` (1 MB, long) |
| burn → `Burn` | U `MAGIC_SPELLS/MAGIC_SPELL_Flame_01_mono.wav` | in-project `etfx_explosion_fireball.wav` |
| lightning_chain → `Lightning` (3 RR) | U `ELECTRICITY/ELECTRICITY_Spark_01/02/03_mono.wav` | U `ZAPS/ZAP_Electric_01_mono.wav`; in-project `etfx_explosion_lightning.wav` |
| poison → `Poison` | U `MAGIC_SPELLS/MAGIC_SPELL_Deep_Tone_Bubbling_Zaps_Subtle_mono.wav` | C `Pop/Liquid/SFX_Pop_Liquid_1.wav`; in-project `etfx_explosion_poisoncloud.wav` |
| heal → `Heal` / `PickupHeal` | C `Powerup/SFX_Powerup_Potion_1.wav` | U `RETRO_LOFI/RETRO_Powerup_03_mono.wav`; NEX `Sound effect/Unrealsfx - Magical Game - Magic Potion Pickup.wav` (on disk, 2.25 s) |
| shield_block → `Blocked` | U `FORCE_FIELDS/FORCE_FIELD_Scifi_Pulse_01_mono.wav` | U `MAGIC_SPELLS/MAGIC_SPELL_Shield_mono.wav`, U `IMPACTS/Metal/IMPACT_Metal_Cling_Bright_mono.wav` |
| split → `Split` (3 RR) | C `Pop/Mouth/SFX_Pop_Mouth_High_Sharp_1/2/3.wav` | — |
| portal → `Portal` | U `SPACE_WARPS/SPACE_WARP_Quick_Charge_Jump_Away_01_mono.wav` | U `MAGIC_SPELLS/MAGIC_SPELL_Teleport_mono.wav` |
| pickup_coin → (add `PickupCoin` if coins exist) | U `8BIT/Coin_Collect/8BIT_RETRO_Coin_Collect_Two_Note_Bright_Fast_mono.wav` | U `RETRO_LOFI/LOFI_Coin_01_mono.wav` |
| pickup_ball → `PickupBall` | U `8BIT/Powerups/8BIT_RETRO_Powerup_Spawn_Quick_Climbing_mono.wav` | U `RETRO_LOFI/RETRO_Powerup_01_mono.wav` |
| (TDD) `PickupPower` / `PowerShot` | U `8BIT/Powerups/8BIT_RETRO_Powerup_Spawn_Flash_Aggressive_mono.wav` / U `8BIT/Weapons/8BIT_RETRO_Fire_Blaster_Deep_Glide_mono.wav` | — |
| enemy_advance_step → `EnemyStep` | U `THUDS_THUMPS/THUD_Dark_03_Short_mono.wav` | U `RETRO_LOFI/LOFI_Marching_01_mono.wav` |
| (TDD) `EnemyAttack` / `EnemyCast` / `CrateBreak` | U `RETRO_LOFI/RETRO_Melee_Attack_Kick_Punch_02_mono.wav` / U `MAGIC_SPELLS/MAGIC_SPELL_Dark_Pulse_Echo_Subtle_mono.wav` / U `RETRO_LOFI/LOFI_Break_01_mono.wav` | — |
| player_hurt → `PlayerHurt` | U `8BIT/Hits_Bumps/8BIT_RETRO_Hit_Bump_Quick_Dark_Drop_mono.wav` (+ optional cat voice U `ANIMALS/ANIMAL_Cat_Meow_Short_01_mono.wav`) | — |
| player_low_hp_warning → `LowHpWarning` | UGMC `Short Cues/Sound Effects/Heartbeat SINGLE 1.wav` (0.17 MB) | U `USER_INTERFACES/Beeps/UI_Beep_Double_Quick_Deep_stereo.wav` |
| ball_return → `BallReturn` | U `8BIT/Various/8BIT_RETRO_Effect_Reverse_Zoom_mono.wav` | C `Collect/Pop/SFX_Player_Collect_Pop_1.wav` |
| turn_start → `TurnStart` | U `USER_INTERFACES/Beeps/UI_Beep_Double_Clean_Up_stereo.wav` | UGMC `Short Cues/Quest SFX/Quest Start DRUMS SMALL.wav` (long; use for stage start) |
| reward_card_reveal → `RewardReveal` (3 RR) | U `CARDS/CARDS_Deal_01_RR1/RR2/RR3_mono.wav` | C `Chimes/SFX_Chimes_Glowing_Stars_1.wav` (1.8 MB, long; trim) |
| reward_pick → `RewardPick` | C `UI/Bonus/SFX_UI_Bonus_Rich_1.wav` | M `SFX/UI/Claim_Purchase_Upgrade/SFX_UI_Claim_1.wav` |
| level_up → `LevelUp` | U `RETRO_LOFI/RETRO_Powerup_05_mono.wav` | UGMC `Short Cues/Level Up/Level Up ETHNIC DRUMS.wav` (1.0 MB) / `Level Up BRASS.wav` (2.5 MB) |
| ui_move → `UiMove` | U `8BIT/Beeps/8BIT_RETRO_Beep_1_Very_Short_mono.wav` | in-project `Sfx/Generic/SFX_UI_Click_Generic_1.wav` |
| ui_select → `UiSelect` | U `RETRO_LOFI/RETRO_Click_01_mono.wav` | in-project `Sfx/Generic/SFX_UI_Button_Click_Select_1.wav` |
| ui_back → `UiBack` | U `USER_INTERFACES/Beeps/UI_Beep_Double_Clean_Down_stereo.wav` | in-project `Sfx/Generic/SFX_UI_Click_Close_1.wav` |
| ui_pause → `UiPause` (+ resume) | U `TIME_WARPS/TIME_WARP_Stop_01_mono.wav` / resume `TIME_WARP_Start_01_mono.wav` | — |
| countdown_tick → `CountdownTick` | U `8BIT/Beeps/8BIT_RETRO_Beep_Short_Bright_mono.wav` | in-project `Sfx/Countdown/SFX_UI_Countdown_Blow_1.wav` (+ `_End_1` for "go") |
| stage_clear → `StageClear` | U `RETRO_LOFI/RETRO_Bonus_01_mono.wav` (+ BGM stinger above) | — |
| game_over → `GameOver` | U `MUSIC_EFFECTS/Solo_Chip_Square/MUSIC_EFFECT_Solo_Chip_Square_Negative_01_stereo.wav` | `…Negative_02…16` |
| victory → `Victory` | U `MUSIC_EFFECTS/Solo_Chip_Square/MUSIC_EFFECT_Solo_Chip_Square_Positive_01_stereo.wav` | `…Positive_02…16` |

`GenericEnter`/`GenericExit` (framework): in-project `Sfx/Generic/SFX_UI_Click_Open_1.wav` / `SFX_UI_Click_Close_1.wav`.

### 10.4 Materialization (documented only, not executed)
Option A: per file, no sparse change, downloads only what you need (recommended).
```bash
R=~/Documents/music-cell-shared-assets
SFX=/Users/simonbut/project/VibeProject3/Starter/Assets/Audio/Sfx/BilliardRogue
BGM=/Users/simonbut/project/VibeProject3/Starter/Assets/Audio/Bgm/BilliardRogue
mkdir -p "$SFX" "$BGM"
fetch() {  # $1 = path under music-cell-shared-assets/Assets/, $2 = destination file
  local src="music-cell-shared-assets/Assets/$1"
  git -C "$R" show "HEAD:$src" | git -C "$R" lfs smudge -- "$src" > "$2"   # pointer blob → LFS content (cached in .git/lfs)
  head -c 4 "$2" | grep -q RIFF || { echo "LFS fetch failed: $1"; rm -f "$2"; return 1; }
}
fetch "Ultimate Game Music Collection/Fantasy Orchestral/Upbeat City LOOP.wav" "$BGM/Title.wav"
fetch "Ultimate Game Music Collection/Combat/Skeletons LOOP.wav"               "$BGM/Act1.wav"
fetch "Universal Sound FX/RETRO_LOFI/RETRO_Bump_01_mono.wav"                   "$SFX/BallWallBounce_1.wav"
# … one line per row of §10.2/§10.3; keep the mapping in Tools/Audio/audio_manifest.json (TDD §14.3)
# NEX files are already on disk: cp "$R/music-cell-shared-assets/Assets/NEX/Sound effect/<file>.wav" "$SFX/…"
```
Option B: sparse checkout (cone mode means **directory** granularity; LFS smudge runs on checkout).
```bash
git -C "$R" config core.sparseCheckoutCone      # → true
git -C "$R" sparse-checkout add \
  "music-cell-shared-assets/Assets/Universal Sound FX/RETRO_LOFI" \
  "music-cell-shared-assets/Assets/Universal Sound FX/8BIT" \
  "music-cell-shared-assets/Assets/Ultimate Game Music Collection/Short Cues/Winning"   # etc. (whole folders)
git -C "$R" lfs pull --include="music-cell-shared-assets/Assets/Universal Sound FX/RETRO_LOFI/**"   # only if smudge was skipped
cp "$R/music-cell-shared-assets/Assets/Universal Sound FX/RETRO_LOFI/RETRO_Pew_01_mono.wav" "$SFX/BallLaunch_1.wav"
```
Rules:
- **Copy only the `.wav`, never the shared repo's `.meta`**, because that would duplicate GUIDs. Let the running
  Editor import the files and mint the `.meta`, or run `AssetDatabase.Refresh()` via the CLI.
- Then run the import-settings builder (§2.7), `ffprobe` every file, and finally the `AudioRegistryBuilder` (§3.3).

---

## 11. How Billiard Rogue should use this

### 11.1 Enum extensions (explicit ints; the order of declaration equals value order)
```csharp
// SfxManager.SoundEffect — keep None=-1, GenericEnter=0, GenericExit=1
UiMove = 100, UiSelect = 101, UiBack = 102, UiPause = 103, UiResume = 104, CountdownTick = 105,
CueStrike = 200, BallLaunch = 201, BallWallBounce = 202, BallHitSoft = 203, BallHitMid = 204, BallHitHard = 205,
CritHit = 206, Blocked = 207, BallReturn = 208, PowerShot = 209,
EnemyDeath = 300, BossHit = 301, BossDeath = 302, EnemyStep = 303, EnemyAttack = 304, EnemyCast = 305, CrateBreak = 306,
Explosion = 400, Freeze = 401, Burn = 402, Lightning = 403, Poison = 404, Heal = 405, Split = 406, Portal = 407,
PickupBall = 500, PickupHeal = 501, PickupPower = 502, PlayerHurt = 503, LowHpWarning = 504,
TurnStart = 600, RewardReveal = 601, RewardPick = 602, LevelUp = 603, StageClear = 604, BossAppear = 605, GameOver = 606, Victory = 607,
// BgmManager.BgmType: Main = 0, Title = 1, Act1 = 10, Act2 = 11, Act3 = 12, Boss = 20, Reward = 30
// VfxManager.VisualEffect (TDD list): HitSpark = 0, CritSpark = 1, WallSpark = 2, EnemyPoof = 10, BossPoof = 11, Explosion = 20,
//   FreezeBurst = 21, BurnBurst = 22, PoisonBurst = 23, LightningHit = 24, HealSparkle = 25, SplitPop = 26, PortalFlash = 27,
//   PickupSparkle = 30, DustPuff = 31, PlayerHurtFlash = 32, CratePieces = 33, LevelUpBurst = 34
```
After editing any enum: re-run `AudioRegistryBuilder` in the same change, or the next Play session throws (§3.1).

### 11.2 Build order (Editor scripts via Unity CLI)
1. Materialize the audio (§10.4). Refresh. Run `ImportSettingsBuilder` (audio rules §2.7).
2. `VfxPrefabsBuilder`: copy the curated ETFX prefabs → sanitize (§4.5) → upgrade their materials (§4.4). Set their layer.
3. `AudioRegistryBuilder`: fill `SfxManager.soundEffectDict`, `BgmManager.bgmDict` (+ stinger dict if added), and
   `VfxManager.effectSpecs` (pool 4/16 for hits, 2/6 for bosses) from `Tools/Audio/audio_manifest.json`.
4. Add the second `AudioSource` to `BgmManager.prefab` with `ObjectFactory.AddComponent<AudioSource>` +
   `outputAudioMixerGroup` = the Music group (load via `AssetDatabase.LoadAllAssetsAtPath(mixerPath).OfType<AudioMixerGroup>()`).

### 11.3 Wire volumes (add to `VolumeManager`, the single owner)
```csharp
using Cysharp.Threading.Tasks; using Cysharp.Threading.Tasks.Linq;
void Start()   // not Awake: mixer snapshot init can overwrite SetFloat
{
    var pdm = PlayerDataManager.Instance; var ct = this.GetCancellationTokenOnDestroy();
    pdm.MasterVolumeProperty.Subscribe(SetMasterVolume).AddTo(ct);   // AsyncReactiveProperty emits current value first
    pdm.SfxVolumeProperty.Subscribe(SetSfxVolume).AddTo(ct);
    pdm.BgmVolumeProperty.Subscribe(SetMusicVolume).AddTo(ct);
}
// Settings view: on slider change → set the property (live) ; on confirm →
// PlayerDataManager.Instance.ScopedPlayerPreferenceUpdate(p => { p.masterVolume = m; p.sfxVolume = s; p.bgmVolume = b; });
// + RunAnalytics.SettingChanged("sfx_volume", old, new)
```
(`PlayerDataManager` is in CommonSingletons with VolumeManager. The order of `Awake` among siblings is not guaranteed,
but `Start` runs after all `Awake` calls.)

### 11.4 BgmManager / SfxManager extensions (sketch)
```csharp
// BgmManager: [SerializeField] AudioSource secondarySource; [SerializeField] EnumDictionary<StingerType, AudioClip> stingers;
public enum StingerType { StageClear = 0, BossAppear = 1, Victory = 2, Defeat = 3 }
BgmType? current; AudioSource Active => …;
public async UniTask CrossFadeTo(BgmType type, float d = 0.8f, CancellationToken ct = default)
{
    if (current == type) return; current = type;
    var from = Active; var to = Inactive; to.clip = bgmDict[type]; to.volume = 0; to.Play();
    await UniTask.WhenAll(
        from.DOFade(0, d).SetUpdate(true).ToUniTask(cancellationToken: ct),
        to.DOFade(1, d).SetUpdate(true).ToUniTask(cancellationToken: ct));
    from.Stop(); SwapActive();
}
public void PlayStinger(StingerType s, float duck = 0.3f)   // duck main, one-shot on the Music-routed secondary/extra source
{ var c = stingers[s]; stingerSource.PlayOneShot(c); Active.DOFade(duck, 0.15f).SetUpdate(true);
  DOVirtual.DelayedCall(c.length, () => Active.DOFade(1, 0.6f).SetUpdate(true), ignoreTimeScale: true); }
```
- SfxManager combo pitch: keep `PlaySoundEffect` for normal SFX. For combo hits, call
  `SfxManager.Instance.PlayAudioClip(comboScale[Mathf.Min(combo, comboScale.Length - 1)], null)` with
  `comboScale` = the 12 `Scales/Set1` clips stored in a config. Alternatively, add a 4-source pool to SfxManager
  (outputs → Sfx group) with `PlaySoundEffect(effect, float pitch)`.
- Add a per-effect rate limit, e.g. skip if the same `SoundEffect` played < 35 ms ago, max 4 per frame. This keeps many
  simultaneous balls under the 32 real voices.

### 11.5 VfxManager extension (keep the existing method)
```csharp
public ParticleSystem Play(VisualEffect e, Vector3 pos, Quaternion? rot = null, float scale = 1f)   // not `= default`: default(Quaternion) != identity
{ var ps = effectSpecs[e].Get(); var t = ps.transform; t.SetPositionAndRotation(pos, rot ?? Quaternion.identity);
  t.localScale = Vector3.one * scale; ps.Play(true); return ps; }        // caller may Stop() looping FX → returns to pool
public void Prewarm(VisualEffect e, int count) { /* Get() count instances, then Stop(true, StopEmittingAndClear) */ }
```
Status loops (burn aura, shield, portal) belong to the entity view (instantiate a child from the curated prefab, or pool
it with `ObjectPooler`). Don't send them through fire-and-forget.

### 11.6 Hit feedback recipe (per `BoardPresenter.Consume(events)`)
```csharp
void OnBallHitEnemy(in HitEvent h)
{
    var sfx = h.isCrit ? SoundEffect.CritHit : h.damage >= 10 ? SoundEffect.BallHitHard : h.damage >= 4 ? SoundEffect.BallHitMid : SoundEffect.BallHitSoft;
    SfxManager.Instance.PlaySoundEffect(h.isBoss ? SoundEffect.BossHit : sfx);
    VfxManager.Instance.PlayVisualEffect(h.isCrit ? VisualEffect.CritSpark : VisualEffect.HitSpark, h.worldPos);
    enemyView.HitFeedback.PlayFeedbacks(h.worldPos, Mathf.Clamp01(h.damage / 10f)); // MMF_Player: MMF_ScaleShake + MMF_Flicker(UseMaterialPropertyBlocks)
    damageNumberPool.Get().Show(h.damage, h.worldPos, h.isCrit);                     // ObjectPooler<DamageNumber>
    analyticsAgg.AddHit(h);                                                          // aggregated into ball_result
}
```
- Views: put `MMF_SfxManager(UiSelect)` in button press feedbacks and `MMF_SfxManager(RewardReveal)` in the reward card
  entry player. Set `ForceTimescaleMode = true` / `Unscaled` on pause-menu players. Call
  `RunAnalytics.UiAction(AnalyticsScreenName, "<button>")` in every button handler.
- Pause view: `TrackPause`, `BgmManager` duck (`SetUpdate(true)`), `UiPause` SFX. Do the reverse on resume. Feed
  `CherryIntegrationManager.PreferGameStopped` into the same path.

---

## 12. Unverified / to check in Editor
- Whether `OnParticleSystemStopped` on the root waits for child systems (affects §4.1 release timing).
- Whether particle systems sharing a material batch under URP 17 (Frame Debugger).
- `SerializedProperty.intValue` vs `enumValueFlag` for writing enum keys with negative values (`None = -1`). Assert the YAML after the build.
- Unity 6 location of `preloadAudioData` (`AudioImporter` vs `AudioImporterSampleSettings`).
- Whether the Mixpanel SDK emits automatic events by itself.
- Real durations and formats of the shared-repo picks (only LFS sizes were read). Audition every pick; they were chosen by name.
