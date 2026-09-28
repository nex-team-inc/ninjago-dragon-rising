# Localization & Fonts — research note (Billiard Rogue)

Scope: Unity Localization setup in the Starter, `NexLocalizedString` / `EnumStringSelector`, the
`team.nex.localization` package, locale selection/persistence, smart strings, editor-script authoring
of string tables (no Google credentials), runtime lookup, TMP font inventory, glyph coverage, the
`DynamicFontSanitizer`, and a concrete pixel-font plan for en / fr-CA / zh-Hans / zh-Hant / ja.

Versions (verified in `Starter/Packages/manifest.json` + `Library/PackageCache`):
Unity 6000.3.9f1 · `com.unity.localization` 1.5.11 (manifest pins 1.5.10, cache resolves 1.5.11) ·
`com.unity.ugui` 2.0.0 (contains TMP; `com.unity.textmeshpro` 5.0.0 is an empty shim) ·
`com.unity.addressables` 2.9.1 · `team.nex.localization` 0.1.14 · `team.nex.dynamic-font-sanitizer` 0.0.3.

Legend: **[V]** verified in source/asset, **[U]** not verified (needs an Editor/device run).

---

## 0. TL;DR for implementers

| Topic | Fact / decision |
|---|---|
| Collection | Use the existing **`LocalizationTable`** string-table collection (TDD §11). Keys `br.*`. |
| Table ref | Serialize as `GUID:19d51c5b557f64038ab34ae2016b2cf6` (SharedTableData GUID = `TableCollectionNameGuid`) + `m_KeyId`. **[V]** |
| Locales | `en` (project locale), `fr-CA`, `zh-Hans`, `zh-Hant`, `ja`; all have `FallbackLocale = en`, but **`UseFallback` is off** in the string DB → empty/missing entries show `No translation found for '{key}' …` on screen. **[V]** |
| Component | `Nex.Localization.NexLocalizedString` (Assembly-CSharp, `Assets/Libraries/Localization`). Requires **`TextMeshProUGUI`** (not 3D `TextMeshPro`). **[V]** |
| Authoring | Editor C# upsert via `LocalizationEditorSettings.GetStringTableCollection` → `SharedData.GetEntry(key) ?? SharedData.AddKey(key)` → `StringTable.AddEntry(id, value)` → `IsSmart` → `SetDirty` → `SaveAssets`. Compiled OK against the project's assemblies (§7). |
| Google Sheets | Extension configured, provider asset is **encrypted/missing** → Pull throws. If someone *with* credentials pulls: `RemoveMissingPulledKeys = 1` **deletes every `br.*` key** and overwrites values. Never Push. Seeder = source of truth; re-run after any pull. **[V]** |
| Locale persistence | None today (selectors: CommandLine → System → Specific `en`). Add `PlayerPreference.localeCode` and apply after `InitializationOperation` (§5). |
| System locale gap | A `fr-FR`/`fr` device resolves to **English** (only parent-chain matching; there is no `fr` locale). Map by `Application.systemLanguage` (§5). **[V]** |
| Starter gap | `SetupWarningMessage.cs` hard-codes English (`"Step back"`, `"Move closer"`…). Must be localized for the setup view. **[V]** |
| Fonts in project | Only **Glow Sans J** (OFL 1.1) covers CJK: 100% of GB2312 (6763), Big5 level-1 (5401), JIS level-1 (2965), kana, CJK punct. Japanese glyph forms only (no zh `locl`). **[V]** |
| Pixel font plan | (a) original Latin pixel TTF generated with fontTools from bitmap strings (prototype verified pixel-exact), (b) CJK = TMP **static RASTER_HINTED** atlas rendered from `GlowSansJ-Normal-Bold.otf` at 16 px, point-filtered; both on a **16 px em grid**, displayed at `fontSize = 16 × n`. |
| Sanitizer | Clears dynamic TMP data on save for any file ending in `SDF.asset` (default behaviour `Clear`). Static assets are untouched. **[V]** |

---

## 1. Unity Localization assets (`Starter/Assets/Localization/`)

### 1.1 `LocalizationSettings.asset` (GUID `75f49534210af427d97855d391b050ab`, registered as `com.unity.localization.settings` in `ProjectSettings/EditorBuildSettings.asset`) **[V]**

| Field | Value | Meaning |
|---|---|---|
| `m_StartupSelectors` | `CommandLineLocaleSelector(-language=)`, `SystemLocaleSelector`, `SpecificLocaleSelector(en)` | Evaluated in order at init |
| `m_ProjectLocaleIdentifier` | `en` | |
| `m_PreloadBehavior` | `1` = `PreloadSelectedLocale` | Only tables with the Addressables label `Preload` are preloaded — **none are today** |
| `m_InitializeSynchronously` | `0` | Init is async |
| StringDatabase `m_UseFallback` | `0` | Fallback locale not used |
| StringDatabase `m_MissingTranslationState` | `1` = `ShowMissingTranslationMessage` | Shows `No translation found for '{key}' in {table.TableCollectionName}` |
| AssetDatabase `m_UseFallback` | `0` | |
| SmartFormatter sources | List, PersistentVariables (no groups), Dictionary, ValueTuple, Xml, Reflection, Default | |
| SmartFormatter formatters | List(`list`,`l`), Plural(`plural`,`p`, default `en`), Conditional(`cond`), Time(`t`), XElement, Choose(`choose`,`c`, split `|`), SubString, IsMatch, Default | |
| Parser | braces `{ }`, alphanumeric selectors + `_-`, operators `[]().,` | Placeholder names: letters/digits/`_`/`-`; `.` = nested selector |

Missing = entry absent **or empty string** (`GetTableEntryOperation`: `entry == null || IsNullOrEmpty(entry.Data.Localized)`). **[V]**

### 1.2 Locales (`Assets/Localization/Locales/*.asset`) **[V]**

| Code | GUID | SortOrder | Metadata |
|---|---|---|---|
| `en` | `176e41cecbf4b4f6ab53d5733be7e18a` | 0 | — |
| `zh-Hans` | `07f422df914f641bfb678f9c21fba4db` | 1 | FallbackLocale → en |
| `zh-Hant` | `b05335d97e87745e6bc9280df576361c` | 2 | FallbackLocale → en |
| `fr-CA` | `6e89b00fba475494ca3041ceebf1f6eb` | 10000 | FallbackLocale → en |
| `ja` | `8d95ab966e25243fe932d0a84fd9e7ad` | 10000 | FallbackLocale → en |

`m_LocaleName` equals the code (not a display name) → use loc keys for a language picker (§13.6).

### 1.3 String table collection (`Assets/Localization/LocalizationTables/`) **[V]**

| Asset | GUID |
|---|---|
| `LocalizationTable.asset` (StringTableCollection, group "String Table") | `33c47dd900f7f460a9e158519c1d4fd3` |
| `LocalizationTable Shared Data.asset` (`m_TableCollectionName: LocalizationTable`, `m_TableCollectionNameGuidString` = own GUID) | `19d51c5b557f64038ab34ae2016b2cf6` |
| `LocalizationTable_en.asset` | `14067c579729c409ead74c7b7b6d5b77` |
| `LocalizationTable_fr-CA.asset` | `80fefe345c12343ec84b4e53ae0a5171` |
| `LocalizationTable_zh-Hans.asset` | `7667d850af50843578d042827c3ff831` |
| `LocalizationTable_zh-Hant.asset` | `cef617fa3ab244683974a6d3d18fc181` |
| `LocalizationTable_ja.asset` | `75b8a5405c3cb4287aea2021ad2f0242` |

Key generator: `DistributedUIDGenerator` (custom epoch 1744711785802) — ids are 64-bit, never hand-pick them.

Existing starter keys (keep them; the starter `WelcomeScreenView` uses them):

| Key | Id | Smart |
|---|---|---|
| `welcome_screen_title` | 302859718483968 | no |
| `welcome_screen_play` | 302859739455488 | no |
| `welcome_screen_play_ar_game_example` | 68424769643470848 | no |
| `welcome_screen_play_non_ar_game_example` | 68424769651859456 | no |
| `welcome_screen_smart_string_example` | 308996924760064 | yes (`Today's lucky number is {0}.`) |
| `welcome_screen_version` | 68491977904087041 | yes (`Version {0}`) |
| `welcome_screen_version2` | 68492077074210816 | no |

How "smart" is serialized: each locale table has one table-level `SmartFormatTag` metadata whose
`m_SharedEntries` lists the smart key ids, and each smart entry's `m_Metadata` references it. Set it
through `StringTableEntry.IsSmart = true` (adds/removes the tag) — never by YAML.

### 1.4 Google Sheets extension (on `LocalizationTable.asset`) **[V]**

- `GoogleSheetsExtension`: spreadsheet `1iHLVXWwKGaYmfNkWK7_EgBO0a_6cxsKgn-bXoDUFn-A`, sheet id `68802780`.
- Columns: A=Key; E=en, G=en smart; H=zh-Hans, J=smart; K=zh-Hant, M=smart; N=ja, P=smart; Q=fr-CA, S=smart
  (`SmartStringColumn`, `Assets/Libraries/Localization/SmartSringColumn.cs` — note the file-name typo; class
  is referenced by the extension as `{class: SmartStringColumn, ns: , asm: Assembly-CSharp}` → do not move it into an asmdef).
- `m_SheetsServiceProvider` → GUID `b7a48ffc0727647929e4447cce23f8da`, which only exists as the encrypted
  `Assets/Editor/secrets/GoogleSheetsService.asset.enc` (+ `.meta.enc`). The reference is **missing** → Pull/Push throw.
- **`m_RemoveMissingPulledKeys: 1`** → a successful Pull deletes every key not present in the sheet.
- Menu `Localization/Google Sheets/Pull All Google Sheets Extensions` (from `GoogleSheetsUtils.PullAllExtensions`) is **enabled**;
  the Push menu item is commented out, but Push is still reachable from the Tables window extension UI.
- Rule for agents: never Pull or Push. If a human pulls, re-run `LocalizationSeeder` (idempotent upsert).

### 1.5 Addressables **[V]**

Groups (`Assets/AddressableAssetsData/AssetGroups/`): `Localization-Locales` (5 locales, label `Locale`),
`Localization-Assets-Shared` (Shared Data), `Localization-String-Tables-{en,fr-CA,ja,zh-Hans,zh-Hant}`
(label `Locale-<code>`). **No table carries the `Preload` label.**

`Assets/Localization/LocalizationAddressableGroupRules.asset` (GUID `129fb90bade284e4299f4a53fe04ae29`) uses
pattern `Localization-Strings-{LocaleName}`, but it is **not registered** (`EditorBuildSettings` has no
`com.unity.localization.addressable-group-rules` entry), so the package default
`Localization-String-Tables-{LocaleName}` is active — matching the existing groups. Do not register that asset:
`GroupResolver.AddToGroup` would *move* existing table entries into new `Localization-Strings-*` groups.

`AddressableAssetSettings.m_BuildAddressablesWithPlayerBuild: 0` = follow Editor preference **[U: value of the
preference on the build machine]**. String-table edits only reach a device build after an Addressables content build.

---

## 2. Starter scripts (`Starter/Assets/Libraries/Localization/`, no asmdef → Assembly-CSharp)

### 2.1 `NexLocalizedString` — `namespace Nex.Localization` **[V]**

```csharp
[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshProUGUI))]
[AddComponentMenu("Localization/Nex Localized String")]
public class NexLocalizedString : LocalizeStringEvent
{
    public bool ShouldOverrideFontChange { get; set; }          // unused anywhere
    public void SetSmartStringArgument(string key, object value);
    // inherited: LocalizedString StringReference {get;set;}, UnityEventString OnUpdateString, RefreshString(),
    //            SetTable(string), SetEntry(string)
}
```

Behaviour:
- `Awake`: caches `GetComponent<TextMeshProUGUI>()` (play mode only; `[ExecuteAlways]` of the base is neutralized).
- `Start`: `await LocalizationSettings.InitializationOperation` → adds an `OnUpdateString` listener that writes
  `tmpText.text`, then `await StringReference.GetLocalizedStringAsync()` and writes the result.
- Base `LocalizeStringEvent`: `OnEnable` subscribes `StringReference.StringChanged` (fires on locale change and
  `RefreshString`), `OnDisable` unsubscribes; the `StringReference` setter re-subscribes when enabled.
- `SetSmartStringArgument(key, value)`: finds/creates a `Dictionary<string, object>` argument, sets `dict[key]`,
  **allocates a new `List<object>`**, calls `RefreshString()`. Key `"0"` fills `{0}`; key `"damage"` fills `{damage}`.

Gotchas:
1. Only `TextMeshProUGUI`. World-space 3D `TextMeshPro` needs something else (the package `NexLocalizeString` accepts `TMP_Text`).
2. The first frame shows the TMP's authored text (async load). Author the English text into `TMP_Text.text` in the builder so layout/preview are right.
3. Nothing is shown in edit mode (no persistent `OnUpdateString` calls).
4. `SetSmartStringArgument` mutates the **shared** `LocalizedString` instance. If you assign the same `LocalizedString`
   object to several components (e.g. from an `EnumDictionary`), their arguments leak into each other. Assign
   `new LocalizedString(table, key)` per component when using arguments.
5. Allocates on every call → never call per frame. For per-frame numbers use a separate plain TMP + `SetText("{0}", value)`.
6. Never write `tmp.text` directly on a GameObject that has `NexLocalizedString`: the next locale change/refresh overwrites it.
7. The entry must have `IsSmart = true` for `{…}` placeholders to be formatted.

Starter usages: `WelcomeScreenView` (4 components; `versionLabel.StringReference.GetLocalizedString();` then
`SetSmartStringArgument("0", AppInfo.Instance.VersionDisplayString)`), `LuckyNumberSmartStringExample`.
Prefab YAML of a bound component (WelcomeScreenView.prefab):

```yaml
m_StringReference:
  m_TableReference:
    m_TableCollectionName: GUID:19d51c5b557f64038ab34ae2016b2cf6
  m_TableEntryReference:
    m_KeyId: 302859718483968
    m_Key:
  m_FallbackState: 0
  m_WaitForCompletion: 0
  m_LocalVariables: []
m_FormatArguments: []
m_UpdateString: { m_PersistentCalls: { m_Calls: [] } }
```

### 2.2 `EnumStringSelector<TEnum>` — `Assets/Libraries/Localization/EnumStringSelector.cs` **[V]**

```csharp
[DisallowMultipleComponent]
[RequireComponent(typeof(NexLocalizedString))]
public class EnumStringSelector<TEnum> : MonoBehaviour where TEnum : Enum
{
    [SerializeField] EnumDictionary<TEnum, LocalizedString> stringDictionary = null!;
    public TEnum Value { get; set; }   // setter: target.StringReference = stringDictionary[value];
}
```
- Generic → Unity cannot add it; declare a concrete subclass in its **own file with the same name**:
  `public class BallTypeStringSelector : EnumStringSelector<BallType> { }`.
- `target` is fetched in `Awake`; setting `Value` on a never-activated object throws NRE.
- Assigns the dictionary's `LocalizedString` instance itself (see gotcha 4 above).
- `EnumDictionary` (`Nex.Util`, team.nex.common-utils) serializes `List<KeyValuePairStruct> pairs` (`key`, `value`);
  editor helper `Nex.Util.EnumDictionaryEditorUtils.GetValueProperty(SerializedProperty dictProp, int enumValue)`
  returns (inserting if needed) the value property — used in §7.3.

### 2.3 Others
- `CustomizedLocalizedString` (`[Serializable] { Locale locale; string localizedString; }`) — plain data holder, unused.
- `GoogleSheetsUtils` (global namespace, `#if UNITY_EDITOR`) — Pull menu (see §1.4).

---

## 3. `team.nex.localization` 0.1.14 (`Library/PackageCache/team.nex.localization@8829ba00f5ab`)

Runtime asmdef `team.nex.localization` (root namespace `Nex.Localization`, autoReferenced). **None of its runtime
components/assets are used by the Starter** (0 GUID references in Assets/ProjectSettings). **[V]**

| Type | Purpose | Notes |
|---|---|---|
| `NexLocalizeString : LocalizeStringEvent` (`[RequireComponent(typeof(TMP_Text))]`) | Localized text + per-locale font/material swap (`LocalizeFontMode.Global/Custom/None`) | `StringReference` setter clones via `LocalizedStringHelper.CreateRuntimeInstance` (no shared-state bug); `IsLoaded`, `WaitUntilLoadedAsync`, `SetStringReferenceAsync`, `ClearString()`. **Global mode needs `NexLocalizeManager` in the scene, otherwise `IsLoaded` never becomes true** → use `None` if you use it without the manager. |
| `NexLocalizeManager : MonoBehaviour` | DontDestroyOnLoad; sets `CultureInfo.CurrentCulture/UICulture` on locale change; global font table loader; dev overlays | Debug key sequences (legacy Input only; project uses legacy, `activeInputHandler: 0`): tracker view ↑←↓→↓←↑, language overlay ↓↓←←↑↑→→. No conflict with the game code ↑↑↓↓←→←→. |
| `NexLocalizeLocaleSelector : IStartupLocaleSelector` | Dev `PlayerPrefs` override (`nex_locale_override`) → force-English flag → system → `en-US` | Not installed in our settings. Uses `PlayerPrefs` (project rule: persist via PlayerDataManager) — don't adopt. |
| `NexLocalizeLangSelectorView` | IMGUI dev language picker (writes `PlayerPrefs`) | Only via manager. |
| `NexLocalizeFontTable` (SO) / `NexLocalizeFontLoader` / `INexLocalizeFontReceiver` | Map source `TMP_FontAsset` → `LocalizedTmpFont` + materials per locale (Asset Table Collection) | Not needed: one fallback chain covers all our locales (§12). |
| `LocalizedStringHelper` | `CreateRuntimeInstance(LocalizedString)`, extension `SetSmartStringArgument(this LocalizedString, key, value)` | Handy, allocation per call. |
| `OrdinalPluralFormatter` | Smart-string ordinal formatter | Not registered in our SmartFormatter. |
| Voice-over / sprite / texture / audio localizers, preload helpers, trackers | — | Not needed. |

Editor menus (`Nex/Localization/...`): Quick Localize Selected Game Object, Smart String Manager, TMP Text
Reference Finder, Localization Usage Scanner, Crowdin sync windows, Ruby converter, Whisper setup,
`Startup/Fix All Locales`, `Startup/Add Manager to Scene`, `Startup/Init NexLocalizePreferences`.
**Do not run `Fix All Locales`**: it adds `en-US, en-GB, fr, de, es-419, ko, zh-TW, zh-CN`, opens a folder
dialog (blocks the CLI), sets project locale to `en-US` and installs `NexLocalizeLocaleSelector`.
Crowdin needs a token in `NexLocalizePreferences` (not created) — don't use.

---

## 4. `team.nex.dynamic-font-sanitizer` 0.0.3 **[V]**

`Nex.Utils.Editor.DynamicFontSanitizer : AssetModificationProcessor` — `OnWillSaveAssets`: for every saved path
that **ends with `SDF.asset`** and loads as `TMP_FontAsset`, apply the configured behaviour:

| Behaviour | Effect |
|---|---|
| `Clear` (**project default**, `ProjectSettings/DynamicFontSanitizer.asset`, lists empty) | if `atlasPopulationMode != Static` → `ClearFontAssetData(setAtlasSizeToZero: true)` |
| `Drop` | the file is removed from the save list (never written) |
| `Pass` | nothing |

Settings UI: *Project Settings → Nex → Dynamic Font Sanitizer*. Purpose: keep runtime-populated glyphs out of git.
Consequences: (1) you cannot "pre-warm" a *dynamic* `… SDF.asset` from an editor script — it is wiped on save;
bake a **Static** asset instead. (2) Assets not named `*SDF.asset` (e.g. `BRPixel Raster.asset`) are ignored.

---

## 5. Locale selection & persistence

Today **[V]**: first matching of `-language=` (standalone only) → `SystemLocaleSelector` → `en`.
On Android `SystemLocaleSelector` calls `java.util.Locale.getDefault().toLanguageTag()` then CultureInfo,
then `Application.systemLanguage`. `LocalesProvider.GetLocale(id)` = exact match, else **parent chain only**
(`FindFallbackLocale`). Nothing persists the choice.

| Device language | Result | Why |
|---|---|---|
| `en-US` | en | parent `en` |
| `ja-JP` | ja | parent `ja` |
| `fr-CA` | fr-CA | exact |
| `fr-FR` / `fr` | **en** | no `fr` locale; siblings are not searched |
| `zh-Hans-CN` / `zh-Hant-TW` | zh-Hans / zh-Hant | parent chain **[U: Mono/IL2CPP culture data for `zh-CN`/`zh-TW` tags]** |

Recommended (fits "persist via PlayerDataManager only"):

```csharp
// Assets/Scripts/PlayerData/PlayerPreference.cs  (add; ES3 serializes public fields, old saves default to "")
public string localeCode = "";
```

```csharp
// at boot, after SingletonSpawner/PlayerDataManager are ready and before the first localized view
var code = PlayerDataManager.Instance.PlayerPreference.localeCode;
if (string.IsNullOrEmpty(code)) code = LocaleResolver.FromSystemLanguage(Application.systemLanguage); // may be null
await LocText.ApplyLocaleAsync(code, destroyCancellationToken);

public static class LocaleResolver
{
    public static string? FromSystemLanguage(SystemLanguage lang) => lang switch
    {
        SystemLanguage.French => "fr-CA",
        SystemLanguage.Japanese => "ja",
        SystemLanguage.ChineseTraditional => "zh-Hant",
        SystemLanguage.ChineseSimplified or SystemLanguage.Chinese => "zh-Hans",
        _ => null,  // keep what SystemLocaleSelector picked (en fallback)
    };
}

// settings/debug change
LocalizationSettings.SelectedLocale = locale;   // every LocalizedString/NexLocalizedString refreshes itself
PlayerDataManager.Instance.ScopedPlayerPreferenceUpdate(p => p.localeCode = locale.Identifier.Code);
```

Dev switch: `DebugSettings` public methods show up in `DebugSettingsView` → `public void CycleLanguage() => LocText.CycleLocale();`.
`AvailableLocales.Locales` order follows `SortOrder` (en, zh-Hans, zh-Hant, then fr-CA/ja tie) **[U: tie order]**.

---

## 6. Smart strings

- Enable per entry (`IsSmart`). Non-smart entries print braces literally.
- Arguments: `NexLocalizedString.SetSmartStringArgument("name", v)` → `{name}` (DictionarySource). Positional
  `GetLocalizedString(table, key, new object[]{v})` → `{0}`.
- Plural (CLDR rules of the selected locale): `{count:plural:{} ball|{} balls}`; fr: 0 and 1 are "one";
  zh/ja have only "other" → write one form (`{count} 个球`, no `plural:` needed).
- Choose: `{isBoss:choose(True|False):BOSS|}`; Conditional: `{hp:cond:>0?alive|dead}`.
- Culture: numbers format with the locale's culture. `{0}` on an `int` has no grouping; `{0:N0}` in fr-CA emits
  U+00A0 or U+202F as group separator **[U: which one in IL2CPP]** → the Latin font must contain both.
- Avoid `ReflectionSource` paths (`{run.Stage}`) — IL2CPP stripping risk; pass dictionaries.
- Placeholder names cannot contain `.` (it is the nested-selector operator); keys like `br.hud.turn` are fine (keys are not parsed).
- Rich text in values is fine (`<color=#FFD700>{dmg}</color>`); TMP parses after formatting.

---

## 7. Authoring string tables from an Editor C# script (no Google credentials)

All APIs below were compile-checked (scratch build with Unity's Roslyn + the project's `.rsp`, 0 errors).
Public signatures used **[V]**:

```csharp
// UnityEditor.Localization
static StringTableCollection LocalizationEditorSettings.GetStringTableCollection(TableReference tableNameOrGuid);
static LocalizationEditorEvents LocalizationEditorSettings.EditorEvents { get; }
void LocalizationEditorEvents.RaiseCollectionModified(object sender, LocalizationTableCollection c); // only public Raise*
SharedTableData LocalizationTableCollection.SharedData { get; }
LocalizationTable LocalizationTableCollection.GetTable(LocaleIdentifier id);   // cast to StringTable
void LocalizationTableCollection.SetPreloadTableFlag(bool preload, bool createUndo = false); // Addressables label "Preload"
bool LocalizationTableCollection.IsPreloadTableFlagSet();
void StringTableCollection.RemoveEntry(TableEntryReference entryReference);   // shared key + all tables
string StringTableCollection.GenerateCharacterSet(params LocaleIdentifier[] ids); // literal chars, excludes placeholders
// UnityEngine.Localization.Tables
SharedTableEntry SharedTableData.GetEntry(string key);          // null if missing
SharedTableEntry SharedTableData.AddKey(string key = null);     // !! if key exists it creates "key 1", "key 2"…
long SharedTableData.GetId(string key, bool addNewKey);
List<SharedTableEntry> SharedTableData.Entries { get; }
Guid SharedTableData.TableCollectionNameGuid { get; }
StringTableEntry StringTable.AddEntry(long keyId, string localized);  // add or update value
StringTableEntry StringTable.AddEntry(string key, string localized);  // adds the key to SharedData if missing
StringTableEntry StringTable.GetEntry(long keyId);
string StringTableEntry.Value { get; set; }
bool StringTableEntry.IsSmart { get; set; }
```

### 7.1 Idempotent upsert (drop into `Assets/Scripts/BilliardRogue/Editor/LocalizationSeeder.cs`)

```csharp
#nullable enable
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace Nex.BilliardRogue.Editor
{
    public static class LocalizationTableWriter
    {
        public const string CollectionName = "LocalizationTable";
        public static readonly string[] LocaleCodes = { "en", "fr-CA", "zh-Hans", "zh-Hant", "ja" };

        public readonly struct Row
        {
            public readonly string Key; public readonly bool Smart; public readonly string[] Values; // LocaleCodes order
            public Row(string key, bool smart, params string[] values) { Key = key; Smart = smart; Values = values; }
        }

        public static StringTableCollection GetCollection() =>
            LocalizationEditorSettings.GetStringTableCollection(CollectionName)
            ?? throw new InvalidOperationException($"String table collection '{CollectionName}' not found");

        public static void Upsert(IReadOnlyList<Row> rows, string ownedPrefix = "br.")
        {
            var collection = GetCollection();
            var shared = collection.SharedData;
            var tables = new StringTable[LocaleCodes.Length];
            for (var i = 0; i < LocaleCodes.Length; i++)
                tables[i] = collection.GetTable(LocaleCodes[i]) as StringTable
                            ?? throw new InvalidOperationException($"Missing table for {LocaleCodes[i]}");

            var wanted = new HashSet<string>();
            foreach (var row in rows)
            {
                if (row.Values.Length != LocaleCodes.Length) throw new ArgumentException($"{row.Key}: need 5 values");
                foreach (var v in row.Values)
                    if (string.IsNullOrEmpty(v)) throw new ArgumentException($"{row.Key}: empty translation");
                wanted.Add(row.Key);
                var sharedEntry = shared.GetEntry(row.Key) ?? shared.AddKey(row.Key); // GetEntry first: AddKey suffixes duplicates
                for (var i = 0; i < tables.Length; i++)
                {
                    var entry = tables[i].GetEntry(sharedEntry.Id) ?? tables[i].AddEntry(sharedEntry.Id, row.Values[i]);
                    if (entry.Value != row.Values[i]) entry.Value = row.Values[i];
                    if (entry.IsSmart != row.Smart) entry.IsSmart = row.Smart;
                }
            }

            var stale = new List<long>();                       // remove only keys we own
            foreach (var e in shared.Entries)
                if (e.Key.StartsWith(ownedPrefix, StringComparison.Ordinal) && !wanted.Contains(e.Key)) stale.Add(e.Id);
            foreach (var id in stale) collection.RemoveEntry(id);

            EditorUtility.SetDirty(shared);
            foreach (var t in tables) EditorUtility.SetDirty(t);
            if (!collection.IsPreloadTableFlagSet()) collection.SetPreloadTableFlag(true); // sync lookups after init are cheap
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
            AssetDatabase.SaveAssets();
        }

        public static long KeyId(string key) =>
            GetCollection().SharedData.GetEntry(key)?.Id ?? throw new InvalidOperationException($"Missing key '{key}'");
    }
}
```

Usage (`LocalizationSeeder.Run()` holds all translations, TDD §11):

```csharp
LocalizationTableWriter.Upsert(new[]
{
    new LocalizationTableWriter.Row(LocKeys.UiTitlePlay, false, "Play", "Jouer", "开始", "開始", "スタート"),
    new LocalizationTableWriter.Row(LocKeys.HudTurn, true, "Turn {turn}", "Tour {turn}", "第{turn}回合", "第{turn}回合", "ターン{turn}"),
});
```

Optional one-time settings change (API, not YAML) if you want English instead of the red "No translation found":
`LocalizationEditorSettings.ActiveLocalizationSettings.GetStringDatabase().UseFallback = true; EditorUtility.SetDirty(LocalizationEditorSettings.ActiveLocalizationSettings);`
(compile-checked). The seeder's non-empty check is the primary guard.

### 7.2 Binding a `NexLocalizedString` from a builder

```csharp
// (a) direct — edit mode, e.g. inside PrefabUtility.LoadPrefabContents(...) … SaveAsPrefabAsset(...)
var shared = LocalizationTableWriter.GetCollection().SharedData;
label.StringReference = new LocalizedString(shared.TableCollectionNameGuid, LocalizationTableWriter.KeyId(key)); // Guid → TableReference, long → TableEntryReference
label.GetComponent<TextMeshProUGUI>().text = englishPreview;
EditorUtility.SetDirty(label);

// (b) SerializedProperty — works for prefab instances and for LocalizedString values inside EnumDictionary
public static void WriteLocalizedString(SerializedProperty p, string key)
{
    var shared = LocalizationTableWriter.GetCollection().SharedData;
    p.FindPropertyRelative("m_TableReference.m_TableCollectionName").stringValue = "GUID:" + shared.TableCollectionNameGuid.ToString("N");
    p.FindPropertyRelative("m_TableEntryReference.m_KeyId").longValue = LocalizationTableWriter.KeyId(key);
    p.FindPropertyRelative("m_TableEntryReference.m_Key").stringValue = string.Empty;
}
var so = new SerializedObject(label);
WriteLocalizedString(so.FindProperty("m_StringReference"), key);
so.ApplyModifiedPropertiesWithoutUndo();
```

Add components in this order: `TextMeshProUGUI` (configure font/size/text) → `AddComponent<NexLocalizedString>()`
(RequireComponent would otherwise auto-add a default-configured TMP).

### 7.3 Binding an `EnumStringSelector`

```csharp
public static void BindSelector<TEnum>(EnumStringSelector<TEnum> selector, Func<TEnum, string> keyOf) where TEnum : Enum
{
    var so = new SerializedObject(selector);
    var dict = so.FindProperty("stringDictionary");
    foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
        WriteLocalizedString(Nex.Util.EnumDictionaryEditorUtils.GetValueProperty(dict, Convert.ToInt32(value)), keyOf(value));
    so.ApplyModifiedPropertiesWithoutUndo();
}
```

### 7.4 Pitfalls checklist
- Never edit the table/prefab YAML; use the APIs above through `unity command eval`.
- `SharedTableData.AddKey(existing)` silently creates `existing 1` — always `GetEntry` first.
- `SetDirty` **both** SharedTableData and every StringTable, then `AssetDatabase.SaveAssets()`.
- `RemoveEntry` only for your `br.` prefix — the starter `welcome_screen_*` keys are referenced by the starter prefab.
- Google Sheets Pull (credentials) wipes `br.*` (`RemoveMissingPulledKeys`) — re-run the seeder. Never Push (shared starter sheet).
- Addressables: table content ships in bundles; rebuild Addressables before a device build.
- Keys referenced by id: renaming a key keeps bindings; deleting and re-adding gives a new id and breaks bindings → re-run `UiViewsBuilder`.
- Store `TableReference` as GUID (renaming the collection keeps links; package 0.1.14 editor tools do the same).

---

## 8. Runtime lookup

Signatures **[V]** (`UnityEngine.Localization.Settings`):
```csharp
static AsyncOperationHandle<LocalizationSettings> LocalizationSettings.InitializationOperation { get; }
static Locale LocalizationSettings.SelectedLocale { get; set; }
static event Action<Locale> LocalizationSettings.SelectedLocaleChanged;
static ILocalesProvider LocalizationSettings.AvailableLocales { get; }      // .Locales : List<Locale>, .GetLocale(string code)
string LocalizedStringDatabase.GetLocalizedString(TableReference t, TableEntryReference e, Locale locale = null, FallbackBehavior fb = UseProjectSettings, params object[] arguments);
string LocalizedStringDatabase.GetLocalizedString(TableReference t, TableEntryReference e, IList<object> arguments, Locale locale = null, FallbackBehavior fb = UseProjectSettings);
AsyncOperationHandle<string> LocalizedStringDatabase.GetLocalizedStringAsync(TableReference t, TableEntryReference e, Locale locale = null, FallbackBehavior fb = UseProjectSettings, params object[] arguments);
AsyncOperationHandle<StringTable> LocalizedDatabase.GetTableAsync(TableReference t, Locale locale = null);
LocalizedString(TableReference tableReference, TableEntryReference entryReference); // .GetLocalizedString(), .GetLocalizedStringAsync(), .Arguments, event StringChanged
```

Helper (compile-checked) — `Assets/Scripts/BilliardRogue/Localization/LocText.cs`:

```csharp
#nullable enable
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Nex.BilliardRogue
{
    public static class LocText
    {
        public const string Table = "LocalizationTable";
        public static LocalizedString Ref(string key) => new LocalizedString(Table, key);            // new instance per use
        public static string Get(string key) => LocalizationSettings.StringDatabase.GetLocalizedString(Table, key); // after init
        public static string Format(string key, IList<object> args) => LocalizationSettings.StringDatabase.GetLocalizedString(Table, key, args);

        public static async UniTask<string> GetAsync(string key, CancellationToken ct)
        {
            await LocalizationSettings.InitializationOperation.ToUniTask(cancellationToken: ct);
            return await LocalizationSettings.StringDatabase.GetLocalizedStringAsync(Table, key).ToUniTask(cancellationToken: ct);
        }

        public static async UniTask ApplyLocaleAsync(string? code, CancellationToken ct)
        {
            await LocalizationSettings.InitializationOperation.ToUniTask(cancellationToken: ct);
            if (string.IsNullOrEmpty(code)) return;
            var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale != null) LocalizationSettings.SelectedLocale = locale;
        }

        public static Locale CycleLocale()
        {
            var locales = LocalizationSettings.AvailableLocales.Locales;
            var next = locales[(locales.IndexOf(LocalizationSettings.SelectedLocale) + 1) % locales.Count];
            LocalizationSettings.SelectedLocale = next;
            return next;
        }
    }
}
```

Hot-path words (floating `CRIT!`, `BLOCK`, `COMBO`): cache once per locale (no per-hit lookup, no GC):

```csharp
var table = await LocalizationSettings.StringDatabase.GetTableAsync(LocText.Table).ToUniTask(cancellationToken: ct);
for (var i = 0; i < keys.Length; i++) values[i] = table.GetEntry(keys[i])?.GetLocalizedString() ?? keys[i];
// re-run on LocalizationSettings.SelectedLocaleChanged; numbers: tmp.SetText("{0}", damage) (zero-alloc TMP overload)
```

Sync `GetLocalizedString` uses `WaitForCompletion`; it is only cheap once init is done **and** the table is preloaded
(§7.1 sets the `Preload` label; `PreloadBehavior = PreloadSelectedLocale` then makes `InitializationOperation`
wait for it). Before that, prefer `NexLocalizedString` or `GetAsync`.

---

## 9. Font inventory

### 9.1 Font files + licenses (fontTools `name` table; **no OFL.txt / license file exists anywhere under `Assets/Fonts`**) **[V]**

| File(s) | Family | Glyphs / cmap | License evidence | Usable for BR? |
|---|---|---|---|---|
| `Fonts/Localization/GlowSans-ja/GlowSansJ-{Normal,Wide}-{Medium,Bold,ExtraBold,Heavy}.otf` (7 files, 11.2–11.9 MB each, CFF) | Glow Sans J (c) 2021 Project Welai, v0.93 | 44 854 / 43 401 | nameID 13: SIL OFL 1.1; fsType 0 | **Yes** (OFL; ship notice) |
| `TextMesh Pro/Fonts/LiberationSans.ttf` | Liberation Sans 2.00.1 | 2587 | OFL 1.1 | Yes (no CJK) |
| `Fonts/Doodle/Shantell_Sans-Normal-{SemiBold,ExtraBold}.otf` | Shantell Sans | 964 | OFL 1.1 | Yes (not pixel) |
| `Fonts/BrainParty/LTSaeada-Medium.otf` | LT Saeada "Pre-release", "All rights reserved" | 243 | contradictory (OFL URL + LyonsType site) | Avoid |
| `Fonts/BrainParty/Play Chickens.otf` | Khurasan 2024 "All rights reserved" | 116 | khurasanstudio.com/license (commercial) | Avoid (starter uses it) |
| `Fonts/Doodle/Alaska-{Thin,Regular,Bold,ExtraBold}.otf` | ©newglyph 2020 | 538 | none; fsType 4 (preview & print) | Avoid |
| `Fonts/Doodle/BeautifulFreakBold.otf` | © Simon Stratford, all rights reserved | 536 | none | Avoid |
| `Fonts/Doodle/Quirked.ttf` | © Letterhend Studio, all rights reserved | 255 | none | Avoid |

OFL obligations: keep the copyright + license with any distributed copy of the font software; a *derived* font
must stay OFL and must not use a Reserved Font Name. Glow Sans derives from Source Han Sans (Adobe, RFN "Source")
**[U: exact RFN list — no license file in repo]** → name derived fonts neutrally (e.g. "BR Pixel CJK"). Rasterized
atlas bitmaps are generally treated as rendered output, not the font software **[U: legal review]**; to be safe add
the Glow Sans OFL notice to credits/legal notices.

### 9.2 TMP font assets **[V]** (render-mode enum values read from `UnityEngine.TextCoreFontEngineModule.dll`:
SMOOTH=4117, SMOOTH_HINTED=4121, RASTER=4118, RASTER_HINTED=4122, SDF=4134, SDFAA=4165, SDFAA_HINTED=4169)

| Asset | Population | Render | Atlas | Pad | Sampling pt | Multi-atlas | Chars | Fallback | Used by |
|---|---|---|---|---|---|---|---|---|---|
| `TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF` (TMP default) | Static | SDFAA_HINTED | 1024² | 9 | 86 | – | 250 | LiberationSans SDF - Fallback | 3rd-party demos |
| `…/LiberationSans SDF - Fallback` | Dynamic | SDFAA_HINTED | 512² | 9 | 86 | on | 0 | – | – |
| `Fonts/BrainParty/Play Chickens SDF` | Static | SDFAA | 1024² | 9 | 90 | off | 118 | GlowSansJ-Normal-Medium SDF | `WelcomeScreenView`, `PreviewsManager` |
| `Fonts/BrainParty/LTSaeada-Medium SDF` | Static | SDFAA_HINTED | 1024×2048 | 9 | 90 | off | 204 | GlowSansJ-Normal-Medium SDF | `SetupWarningMessage` |
| `Fonts/Doodle/Alaska-{Regular,Thin} SDF` / `Alaska-Bold SDF` | Static | SDFAA / SDFAA_HINTED | 1024² | 7 | 85 | off | 208 | GlowSans Medium / Bold | – |
| `Fonts/Doodle/Quirked-SDF`, `Shantell_*-SDF`, `BeautifulFreakBold-SDF` | Dynamic (with baked glyphs) | mixed | 1024²/2048×1024 | 7 | 85 | mostly off | 0–57 | GlowSans | – |
| `Fonts/Localization/GlowSans-ja/GlowSansJ-* SDF` (7) | **Dynamic**, empty | SDFAA_HINTED | 1024² | 7 | 85 | **on** | 0 | none | as fallbacks |

TMP Settings (`Assets/TextMesh Pro/Resources/TMP Settings.asset`): default font `LiberationSans SDF`, default size 36,
`m_fallbackFontAssets: []`, `m_ClearDynamicDataOnBuild: 1`, `m_GetFontFeaturesAtRuntime: 1`, extra padding off,
kerning on, emoji on, `m_missingGlyphCharacter: 0`, line-breaking files for leading/following (kinsoku) chars present.
TMP shaders are imported in `Assets/TextMesh Pro/Shaders` (`TextMeshPro/Bitmap`, `TextMeshPro/Mobile/Bitmap`,
`TextMeshPro/Mobile/Distance Field`, …).

Build-size note: a *Dynamic* font asset references its source font (`m_SourceFontFile`) → the starter's setup UI
(Play Chickens → GlowSans Medium dynamic) already pulls one 11 MB OTF into the build. Setting a font asset to
`Static` in the Editor nulls `m_SourceFontFile` (TMP `atlasPopulationMode` setter) → source not shipped. **[V]**

---

## 10. Glyph coverage (fontTools cmap, `Tools/.venv/bin/python`) **[V]**

Common-hanzi proxies (no 3500-list offline): GB2312 level-1 (3755 most common simplified), all GB2312 hanzi (6763),
Big5 level-1 (5401 common traditional), JIS X 0208 level-1 (2965 kanji).

| Set | GlowSansJ (all 7) | LiberationSans | Shantell | LTSaeada | Play Chickens | Alaska | Quirked | BeautifulFreak |
|---|---|---|---|---|---|---|---|---|
| ASCII 95 | 95 | 95 | 95 | 95 | 95 | 95 | 95 | 95 |
| Latin-1 C0–FF 64 | 64 | 64 | 64 | 64 | **2** | 64 | 61 | 62 |
| Fr extras `ŒœŸ«»’“”…€` NBSP NNBSP (12) | 10 (no `Ÿ`, U+202F) | 12 | 11 | 11 | 7 | 11 | 7 | 12 |
| GB2312 L1 / all | 3755 / 6763 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Big5 L1 | 5401 | 0 | … | | | | | |
| JIS L1 kanji | 2965 | 0 | | | | | | |
| Hiragana 86 / Katakana 92 | 86 / 92 | 0 | | | | | | |
| CJK punct 3000–303F / Fullwidth FF01–FF5E | 64 / 94 | 0 | | | | | | |
| Sample simp `这们说时发经过动应关门见话击弹级` | 16/16 | | | | | | | |
| Sample trad `這們說時發經過動應關門見話擊彈級` | 16/16 | | | | | | | |

Glow Sans J totals: 20 976 CJK Unified Ideographs + 6 582 Ext-A. GSUB has only `DFLT` script, features
`aalt ccmp dlig fwid hist hwid jp78 jp83 jp90 liga nlck pwid ruby vert vrt2` — **no zh `locl`**, so Chinese renders
with Japanese regional forms (visible e.g. in 骨 直 角 and the 单/単 component). TMP would not apply `locl` anyway.
Acceptable for the prototype; flag for native-speaker review.

Legibility test (Pillow/FreeType 2.14 1-bit render, scratch only): 12 px — traditional (擊 關 應) muddy; 14 px — ok;
**16 px Normal-Bold — clean, reads as a pixel font** for zh-Hans, zh-Hant and ja. At 16 px the ideograph box spans
+14 … −2 px around the baseline, advance 16 px.

---

## 11. Pixel-font options (no external downloads)

| Option | Look | Effort | Runtime cost | Verdict |
|---|---|---|---|---|
| A. Keep GlowSans dynamic SDF | smooth sans, not pixel | none | 11 MB OTF in build, runtime FreeType+SDF per new glyph, ≈100 glyphs per 1 MB 1024² page at 85 pt/pad 7 (multi-atlas grows; dynamic pages keep a CPU copy) | fallback only |
| **B. Latin: original pixel TTF from bitmap strings (fontTools)** | designed pixel font, pixel-exact | medium (draw ~190 glyphs incl. French) | tiny static atlas | **Recommended primary** |
| **C. CJK: TMP static RASTER_HINTED atlas from GlowSansJ-Normal-Bold @16 px** | 1-bit pixel CJK, same grid as B | low (editor builder) | ~1 MB Alpha8 for ≤3000 glyphs, no OTF in build | **Recommended CJK fallback** |
| D. CJK: vectorize 16 px bitmaps into a pixel TTF subset (Pillow + fontTools) | same as C | medium | same | only if CJK needs SDF outline/glow; derived font = OFL, rename |
| E. SDF of the pixel TTFs (SDFAA, sampling 64, pad 8) | pixel squares with slightly soft corners, supports outline/underlay/glow | low once B/D exist | 16× the atlas area per glyph | for a few outlined headlines only |

### 11.1 (B) Latin pixel TTF generator — `Tools/Fonts/build_pixel_font.py`

Prototype run in scratch: output rendered at 16 px and 32 px had exactly 1× and 4× the designed ink pixels (pixel-exact).

```python
# Tools/.venv/bin/python Tools/Fonts/build_pixel_font.py  → Starter/Assets/Fonts/BilliardRogue/BRPixel-Regular.ttf
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

UPM, PX = 1024, 64          # 16 design px per em → TMP samplingPointSize 16 maps 1 design px = 1 texel
ASC, DESC = 14, 3           # match Glow Sans @16 px (+14/−2) so CJK fallback sits on the same baseline
GLYPHS = {                  # name: (codepoint, baseRow, rows top→bottom); baseRow = row just above baseline
    "A": (0x41, 8, [".###.", "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#", "#...#"]),
    # … every char in GenerateCharacterSet(en, fr-CA) + ASCII + À Â Æ Ç É È Ê Ë Î Ï Ô Œ Ù Û Ü Ÿ (and lower case)
    #   « » ’ “ ” – — … € ° × and U+00A0 / U+202F as spaces; a visible .notdef box for QA
}

def draw(rows, base):
    pen = TTGlyphPen(None)
    for r, line in enumerate(rows):
        y0, c = (base - r) * PX, 0
        while c < len(line):
            if line[c] != "#": c += 1; continue
            s = c
            while c < len(line) and line[c] == "#": c += 1
            x0, x1, y1 = s * PX, c * PX, y0 + PX            # one rectangle per horizontal run, clockwise
            pen.moveTo((x0, y0)); pen.lineTo((x0, y1)); pen.lineTo((x1, y1)); pen.lineTo((x1, y0)); pen.closePath()
    return pen.glyph()

order = [".notdef", "space"] + list(GLYPHS)
fb = FontBuilder(UPM, isTTF=True)
fb.setupGlyphOrder(order)
fb.setupCharacterMap({0x20: "space", 0xA0: "space", 0x202F: "space", **{cp: n for n, (cp, _, _) in GLYPHS.items()}})
glyf = {".notdef": TTGlyphPen(None).glyph(), "space": TTGlyphPen(None).glyph()}
hmtx = {".notdef": (6 * PX, 0), "space": (4 * PX, 0)}
for n, (cp, base, rows) in GLYPHS.items():
    glyf[n] = draw(rows, base)
    hmtx[n] = ((max(map(len, rows)) + 1) * PX, 0)          # +1 px tracking baked in; integer advances
fb.setupGlyf(glyf); fb.setupHorizontalMetrics(hmtx)
fb.setupHorizontalHeader(ascent=ASC * PX, descent=-DESC * PX)
fb.setupNameTable({"familyName": "BR Pixel", "styleName": "Regular", "copyright": "Copyright (c) 2026 Nex"})
fb.setupOS2(sTypoAscender=ASC * PX, sTypoDescender=-DESC * PX, sTypoLineGap=0,
            usWinAscent=ASC * PX, usWinDescent=DESC * PX, fsType=0)
fb.setupPost()
fb.save("Starter/Assets/Fonts/BilliardRogue/BRPixel-Regular.ttf")
```
Design guidance: cap height 9 px, x-height 7 px, descender 3 px, 1 px strokes (or 2 px "bold" set), accents
fit in rows 10–14; keep all advances integer; no kerning table (TMP static build does not import features by
default). Keep glyph definitions in a `.py`/`.txt` data file so designers can edit them. Unity imports the TTF on
focus and mints its `.meta` (no CLI needed for the import).

### 11.2 (C) Building the TMP font assets — editor builder (compile-checked)

```csharp
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static TMP_FontAsset BuildStatic(string fontPath, string assetPath, string characters,
    int samplingPointSize, int padding, int atlasSize, GlyphRenderMode renderMode, out string missing)
{
    var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
    var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
    if (fa == null)
    {
        fa = TMP_FontAsset.CreateFontAsset(font, samplingPointSize, padding, renderMode, atlasSize, atlasSize,
            AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);   // RASTER* → material "TextMeshPro/Mobile/Bitmap"
        fa.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(fa, assetPath);                           // Unity mints .asset/.meta
        fa.atlasTextures[0].name = fa.name + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);            // CreateFontAsset leaves these in memory
        fa.material.name = fa.name + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
    }
    else
    {   // regenerate in place → GUID and all references survive
        fa.atlasPopulationMode = AtlasPopulationMode.Dynamic;               // TryAddCharacters refuses Static
        fa.ClearFontAssetData(setAtlasSizeToZero: false);
    }
    fa.TryAddCharacters(characters, out missing);                          // includeFontFeatures=false → no kerning
    fa.atlasTexture.filterMode = FilterMode.Point;                         // Font Asset Creator does this for RASTER
    fa.atlasPopulationMode = AtlasPopulationMode.Static;                   // nulls m_SourceFontFile → OTF not in build
    EditorUtility.SetDirty(fa.atlasTexture);
    EditorUtility.SetDirty(fa);
    AssetDatabase.SaveAssets();
    return fa;
}
```
**[U]** in-place branch: TMP recovers the source font via `m_SourceFontFileGUID` inside `LoadFontFace` (verified in
source), but it has not been run in the Editor yet — check `missing` is empty and the atlas is non-blank.

Recommended calls (paths per TDD §1):

```csharp
var cjkChars = BaseCjkSet + collection.GenerateCharacterSet("zh-Hans", "zh-Hant", "ja"); // BaseCjkSet: kana, 、。「」『』・ー！？（）：；，…〜 + fullwidth digits
var cjk = BuildStatic("Assets/Fonts/Localization/GlowSans-ja/GlowSansJ-Normal-Bold.otf",
    "Assets/Fonts/BilliardRogue/BRPixelCJK Raster.asset", cjkChars, 16, 1, 1024, GlyphRenderMode.RASTER_HINTED, out var m1);
var latin = BuildStatic("Assets/Fonts/BilliardRogue/BRPixel-Regular.ttf",
    "Assets/Fonts/BilliardRogue/BRPixel Raster.asset", LatinSet, 16, 1, 512, GlyphRenderMode.RASTER, out var m2);
latin.fallbackFontAssetTable = new List<TMP_FontAsset> { cjk };
EditorUtility.SetDirty(latin); AssetDatabase.SaveAssets();
```
- Name them `… Raster.asset` (not `SDF.asset`): honest, and the sanitizer ignores them (they are Static anyway).
- `RASTER_HINTED` for Glow Sans (CFF hints → stems snap, integer advances). `RASTER` for BR Pixel (outlines already on the grid).
- `GlyphRenderMode.RASTER*` = FreeType 1-bit monochrome **[V docs]** — same renderer family as the Pillow test; TMP
  "point size" is the FreeType pixel size **[U: visual confirmation in Unity]**.
- Filter CJK chars to code points ≥ U+2E80 plus fullwidth forms if you want to keep Latin out of the CJK atlas (harmless either way).
- Validation step in the same builder (fail the build on missing glyphs):
  `latin.HasCharacters(text, out uint[] missing, searchFallbacks: true, tryAddCharacter: false)` for every entry value of every locale.
- Atlas budget: 16 px + 1 px padding ≈ 17–18 px cells → ~3200 glyphs per 1024² (1 MB Alpha8, no mips);
  Latin fits in 512² or smaller. Compare: GlowSans dynamic SDF at 85 pt/pad 7 ≈ 100 glyphs per 1 MB page.
- Weight: Normal-Bold looked best at 16 px; Medium is lighter (1 px strokes) — pick one to keep one atlas.

### 11.3 TMP settings for crisp pixel text (Unity 6.3, ugui 2.0)

| Setting | Value |
|---|---|
| Atlas render mode | `RASTER` / `RASTER_HINTED` (bitmap shader, no SDF spread) |
| Sampling point size | = em grid (16) for both primary and fallback — TMP scales each font by `fontSize / faceInfo.pointSize` **[V: TMP_Text.cs]**, so equal point sizes keep 1 design px identical across Latin/CJK |
| Padding | 1 (bitmap packing modifier is 0; 1 px stops point-sampling bleed) |
| Atlas texture | `FilterMode.Point`, no mipmaps (TMP atlases have none), Alpha8 |
| Population | **Static** (no runtime FreeType, no source font in build, not touched by sanitizer) |
| Multi-atlas | off for static (size the page instead) |
| TMP component `fontSize` | integer multiples of 16 at CanvasScaler scale 1.0 (starter canvases: ScaleWithScreenSize 1920×1080, match width → exactly 1.0 on the 1080p console): 32 body, 48 headings, 64 titles; `enableAutoSizing = false` |
| Spacing | `characterSpacing` / `lineSpacing` in whole design px: 1 px = 100/16 = 6.25 units at any size (em/100 units) |
| Layout | integer `anchoredPosition` and even rect sizes; prefer left/right alignment; center alignment can land on half pixels → enable `Canvas.pixelPerfect` on UI canvases **[U: cost of rebatching and effect on TMP sub-meshes]** |
| Style | no `<b>`/`<i>` faux styles (they break the grid); author a bold glyph set instead |
| Outline/shadow | Bitmap shader has none. Use a second TMP behind offset by one design px (same material → batches), 9-slice pixel frames (GDD), or option E for headlines |
| Tweens | scale punches (MMFeedbacks) show uneven pixels mid-tween; accept or use option E for animated big text |
| Post-processing | keep UI on Screen Space – Overlay (or a UI camera without the post stack) so HD-2D bloom/tilt-shift does not smear text; the world renders to the low-res RT, UI stays native (GDD: "rendered crisp at native resolution") |
| Missing glyphs | give BR Pixel a visible `.notdef`; `TMP Settings.m_missingGlyphCharacter` stays 0 |

---

## 12. Font wiring decision

- One chain for every locale: `BRPixel Raster` (primary) → `BRPixelCJK Raster` (fallback). No per-locale font swap
  (`NexLocalizeFontTable` not needed); Latin letters/digits inside CJK strings use the Latin pixel font, which is the
  usual pixel-game look.
- Assign the font explicitly in `UiViewsBuilder` for every TMP (do not rely on TMP Settings default = LiberationSans).
  Leave TMP Settings and the starter fonts untouched (starter setup UI keeps Play Chickens → GlowSans dynamic).
- Optional: add `BRPixelCJK Raster` to `TMP_Settings.fallbackFontAssets` (via `TMP_Settings.fallbackFontAssets` +
  `EditorUtility.SetDirty(TMP_Settings.instance)`) so stray TMP components still find CJK glyphs.
- If a view needs SDF effects (glow title), build `BRPixel SDF.asset` (SDFAA, sampling 64, pad 8, Static, bilinear)
  and, for CJK locales, option D's vectorized subset as its fallback; otherwise outlines appear on Latin glyphs only.

---

## 13. How Billiard Rogue should use this (checklist)

1. **Keys**: `LocKeys.cs` constants (`br.ui.<view>.<item>`, `br.ball.<type>.name`, `br.hud.*`, `br.setup.*`, …) — TDD §11.
2. **Seeder**: `LocalizationSeeder.Run()` → `LocalizationTableWriter.Upsert(rows)` (§7.1). Every row has 5 non-empty values;
   mark smart rows; the seeder removes stale `br.*` keys, sets `Preload`, saves. Run through the Unity CLI
   (`unity command eval 'Nex.BilliardRogue.Editor.LocalizationSeeder.Run();' --project-path …`).
3. **Views**: every static label = `TextMeshProUGUI` (BR Pixel Raster, size 16×n, English preview text) + `NexLocalizedString`
   bound by GUID + key id (§7.2). Enum-driven labels = concrete `EnumStringSelector` subclass bound via §7.3.
4. **Dynamic text**: numbers via plain TMP `SetText("{0}", v)`; formatted sentences via `NexLocalizedString.SetSmartStringArgument`
   on events (not per frame); floating words via a per-locale cache (§8).
5. **Setup view**: replace the hard-coded strings in `Assets/Scripts/Gameplay/Setup/SetupWarningMessage.cs` with `br.setup.*`
   lookups (cache per locale; it updates every tracker tick).
6. **Language option**: Settings view cycles `LocalizationSettings.AvailableLocales.Locales`; labels from
   `br.ui.settings.language.<code>` holding endonyms in all 5 tables (English, Français, 简体中文, 繁體中文, 日本語);
   persist `PlayerPreference.localeCode`; log `GameAnalytics.SettingChanged("language", code)`.
7. **Boot**: apply saved/system-mapped locale after `InitializationOperation`, before the title view (§5).
8. **Debug**: `DebugSettings.CycleLanguage()`; a builder/CI check that every table value renders with the font chain (§11.2).
9. **Fonts**: `Tools/Fonts/build_pixel_font.py` → `Assets/Fonts/BilliardRogue/BRPixel-Regular.ttf`; `FontAssetsBuilder` (editor)
   bakes both static raster assets from the current tables; re-run after every seeder change that adds CJK characters.
10. **Credits**: include the Glow Sans OFL 1.1 notice ("Glow Sans (c) 2021 Project Welai", SIL OFL 1.1).
11. **Device build**: rebuild Addressables content (string tables) with the player build.

French typography (fr-CA): use `’` (U+2019), « » with U+00A0 inside, U+00A0 before `:`; Quebec usage puts no space before
`! ? ;`. Chinese/Japanese: full-width punctuation `，。！？：「」`; TMP kinsoku files are already configured.

---

## 14. Risks / open items

- **Google Sheets pull clobber** (`RemoveMissingPulledKeys = 1`) and accidental Push to the shared starter sheet (§1.4).
- **French-France devices get English** unless the `Application.systemLanguage` mapping is added (§5).
- **UseFallback off**: an empty translation shows a red "No translation found" string in-game.
- **Japanese glyph forms for Chinese** (Glow Sans J only; no SC/TC font in the repo or in `music-cell-shared-assets`).
- **Font licenses**: only Glow Sans, Liberation Sans, Shantell Sans are clearly OFL; the other starter fonts are proprietary/unclear.
  No license text files are present in the repo.
- **Point-filtered text at non-integer scales** (editor Game view sizes, tweens, non-1080p outputs) looks uneven; SDF variant is the escape hatch.
- **[U]** Not run in the Editor: TMP raster atlas output equivalence with the Pillow preview, the in-place regeneration branch,
  `Canvas.pixelPerfect` behaviour with TMP, zh-CN/zh-TW culture parent chains on IL2CPP, Addressables "build with player" preference.
