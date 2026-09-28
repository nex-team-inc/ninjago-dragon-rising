# Integration — Localization lead (progress checkpoint)

Owns: `Starter/Assets/Localization/BilliardRogue/**`, `Starter/Assets/Scripts/BilliardRogue/Editor/LocalizationSeeder.cs`,
the `LocalizationTable` string-table assets it updates (br.* keys only).

## Done
- Key inventory from every LocKeys partial (Localization/LocKeys.cs, UI/LocKeys.UI.cs, Presentation/LocKeys.Presentation.cs,
  Flow/LocKeys.Flow.cs): 206 br.* keys incl. the dynamic arrays (Ball.Names ×12, Ball.Descriptions 12×3, Enemy.Names ×12,
  Act.Names ×3) and the Settings language keys (SettingsView prefix + locale code). Ball descriptions cross-checked against
  `Configs/BilliardRogue/Balls/Ball_*.asset` rules: all numbers match.
- `Starter/Assets/Localization/BilliardRogue/BilliardRogueStrings.csv`: 208 rows (206 LocKeys + 2 proposed paw tags), 28 smart,
  en / fr-CA / zh-Hans / zh-Hant / ja, glossary + typography rules in the comment header. `LocKeys.Reward.Hint` was removed by
  the integrator, so its row is gone too.
- `Starter/Assets/Localization/BilliardRogue/Editor/LocalizationCsv.cs` (CSV reader + validation + fr-CA typography pass) and
  `Starter/Assets/Scripts/BilliardRogue/Editor/LocalizationSeeder.cs` (`Run()` upsert, `Verify()` StringDatabase check, menu
  `Nex/Billiard Rogue/Localization Seeder`). compile_check green, no warnings in these files.
- Editor (inside the lock): recompile; `Run()` twice → `208 rows (28 smart): added 0, updated 0, unchanged 208, removed stale 0;
  per locale en=208 fr-CA=208 zh-Hans=208 zh-Hant=208 ja=208; preload flag already set; LocKeys declared 206, missing rows 0,
  file-only 2` (the first write happened through the integrator's Build All, which picked up the seeder); `Verify()` →
  `OK 1040/1040`; sample lookups per locale correct, starter `welcome_screen_*` keys intact (215 shared entries), scene clean.
- Latin glyph coverage: every non-CJK character in the file is in `Fonts/BilliardRogue/BilliardPixel_charset.txt` (+ NBSP).

## Next
- Nothing pending. When CJK text changes: seeder → FontAssetsBuilder (Build All order already does this).

## Findings (for the integrator; files I don't own)
1. `Editor/FlowViewPrefabsBuilder.cs:83` / `:85` hard-code the calibration paw tags `"L"` / `"R"` with no key. Rows
   `br.ui.calibration.leftPawTag` / `rightPawTag` are seeded (L/R, G/D, 左/右): add
   `LeftPawTag`/`RightPawTag` constants to `Flow/LocKeys.Flow.cs` and pass them as `locKey` to `FlowUiFactory.CreateLabel`.
2. `UI/Widgets/SettingRow.cs:98` formats volume as `"{0}%"` for every locale (fr-CA convention is `80 %`). Low priority.
3. Declared but never referenced (translated anyway): `Hud.PipTitle` (Flow asked for it; GameplayPip binds no title label),
   `StageIntro.BossAppears`, `StageIntro.StageClear`, `Reward.UpgradeDesc`, `Reward.BagFull`, `Reward.Unlocked`, `Hud.Next`,
   `Hud.Balls`, `Hud.ExtraBall`, `Hud.TrackingWarning`, `Hud.Pause`, `Hud.EnemiesLeft`, `Hud.WavesLeft`, `Float.FastForward`,
   `Setup.HoldStill`, `Setup.Good`, `Setup.ShowBothPaws`, `Common.*`.
4. `UI/Editor/UiOverlayViewsBuilder.cs:142` reward stars preview `★` is in the pixel font; OK. English fallbacks in
   `Presentation/FloatTextCache.cs:23-27,93` and `Gameplay/Setup/SetupWarningMessage.cs:106-115` only show before the table loads; OK.
5. `AddressableAssetSettings.asset` re-serialized by Addressables 2.9 when the Preload label was added (field upgrades only).
