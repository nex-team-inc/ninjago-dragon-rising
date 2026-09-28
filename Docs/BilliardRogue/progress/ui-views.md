# UI-Views module — progress

Owner paths: `Starter/Assets/Scripts/BilliardRogue/UI/**` (incl. `UI/Editor/**`, `UI/LocKeys.UI.cs`), `Editor/UiViewsBuilder.cs`,
`Assets/Prefabs/BilliardRogue/Views/{Title,PlayerMode,Settings,StageIntro,Reward,Pause,TrackingLost,Summary}View.prefab`,
`Assets/Prefabs/BilliardRogue/UI/GameplayHud.prefab`, `Assets/Configs/BilliardRogue/UiTheme.asset`.

## Done (all committed, compile_check green, no warnings in these files)
- Runtime: `UiTheme` (colours, kit sprites, fonts, unscaled motion timings, UI sounds), `RogueView` base (DOTween
  present/dismiss/cover fades on unscaled time, never throws; `TrackButton` = RunAnalytics.UiAction + select SFX),
  widgets (`TextLabel` drop-shadow TMP stack + localized key/args, `FocusHighlight`, `UiButtonKeyResponder`,
  `SettingRow`, `RewardCard`), 8 views, `GameplayHud` + HUD widgets (HpBar, BossBar, BallQueue, HudChip, HudBanner,
  PlayerTags).
- Builder: `Editor/UiViewsBuilder.cs` (entry, theme asset, prefab roots) + `UI/Editor/{UiPrefabKit,UiFields,
  UiMenuViewsBuilder,UiOverlayViewsBuilder,UiHudBuilder}.cs`. Idempotent; prefab roots + root components keep fileIDs
  (verified: PauseView → SettingsView reference survives rebuilds). Edit-mode wiring check: 211 components, no null
  required refs.
- Preview renders (edit mode, isolated preview scene, in-memory art at final PPU/borders + in-memory pixel TMP font):
  scratchpad `modules/ui-views/shots/*.png`. Tools (scratch, not committed): `UiPreview.cs`, `UiValidate.cs`, `run_all.sh`.

## Next steps (if resumed)
- Integration: re-run UiViewsBuilder after ImportSettingsBuilder + FontAssetsBuilder (Build All order does this).
- Flow integration of the APIs below (Flow owns the call sites).

## Decisions
- Enter/exit animations in code (DOTween SetUpdate(true)) in `RogueView`; no MMF players in views.
- Settings apply live and persist immediately; Back just closes (no Save/Cancel pair on a TV remote → no revert snapshot).
- PauseView Controls = Back (Back = Resume; TDD §9 table says None).
- The opener (GameplayView) calls `Manager.AnnouncePaused()`; PauseView announces Resume / Home only.
- NexLocalizedString bound by table collection GUID + key name (works before the seeder adds entries, survives re-adds).
- Drop shadow = second TMP layer (earlier sibling, one font pixel down-right) with its own NexLocalizedString.
- HUD queue slots at 2x (80 px) to fit 12 balls in the right column; rest of the kit at 3x.
- Top-level Back already plays GenericExit (same clip as UiBack) in ViewManager → views only log `RunAnalytics.UiBack`.

## API exposed
- `RogueView.WaitClosedAsync(ct)` — completes when the view is destroyed after its pop/replace.
- `TitleView`: events `ContinueRequested, NewRunRequested, SettingsRequested, ExitRequested`; `SetContinueInfo(RunState?)`
  (null hides Continue; call before push), `SetBest(MetaProgressData)`. Controls Exit; Back is a no-op.
- `PlayerModeView`: `event Action<int> PlayersChosen` (already saved to PlayerPreference.numPlayers). Back pops.
- `SettingsView`: `event Action<string> SettingChanged` (analytics id). Back pops.
- `StageIntroView`: `Initialize(PacingConfig, EnemyCatalog, IReadOnlyList<ActDefinition>)`, `Show(actIndex, stageInAct, isBoss)`
  (0-based, before push) → push → `WaitClosedAsync`. Pops itself after `PacingConfig.StageIntroDuration`.
- `RewardView`: `Initialize(PacingConfig)`; push (don't await) then `UniTask<int> ChooseAsync(options, BallCatalog, RunState, ct = default)`
  (pops itself before returning); `Hover(int)`, `bool TryChoose(int)` (DebugHooks.ChooseReward).
- `PauseView`: Resume → `AnnouncePauseViewResumeClicked` + pop; Save & Quit → `AnnouncePauseViewHomeClicked`; Settings pushes
  its own SettingsView; `event Action<string> SettingChanged` (relayed).
- `TrackingLostView`: `Initialize(playerIndex, numPlayers, Func<bool> isTracked)` → push → `WaitClosedAsync`; `RequestClose()`.
- `SummaryView`: `Initialize(BallCatalog)`, `Show(RunState, MetaProgressData, bool newRecord, int unlockTierBefore = -1)` (before
  replace); events `PlayAgainRequested, TitleRequested`. Controls None.
- `GameplayHud : IGameplayHud`: `Initialize(BallCatalog, PacingConfig)`; setters skip unchanged values; turn is 1-based,
  act/stage 0-based. Prefab root carries a nested Canvas; must be parented under the GameplayView canvas.

## Requests (files I don't own)
- Localization seeder: add the 13 keys of `UI/LocKeys.UI.cs` (smart: br.hud.bonusBalls, br.hud.trackingWarningPlayer,
  br.reward.levelUp); 73 keys used by the prefabs are not in the table yet.
- Build order: ImportSettingsBuilder + FontAssetsBuilder before UiViewsBuilder (Build All already); committed prefabs
  still reference Multiple-mode `*_0` sprites and the TMP default font until that re-run.
- Flow: wire the call sites above; call `Manager.AnnouncePaused()` before pushing PauseView; nest GameplayHud.prefab in
  GameplayView and call `Initialize`; capture `MetaProgress.highestUnlockTier` before `RunPersistence.CompleteRun`.
- Foundation LocKeys.cs: `Reward.Hint` uses ◀ ▶ (missing from the pixel font); the UI uses the new `Reward.ChooseHint`.
- TDD §9: PauseView Controls = Back.
