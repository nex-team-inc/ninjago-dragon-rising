# UI-Views module — progress

Owner paths: `Starter/Assets/Scripts/BilliardRogue/UI/**`, `UI/LocKeys.UI.cs`, `Editor/UiViewsBuilder.cs`,
`Assets/Prefabs/BilliardRogue/{Views/<8 views>,UI}/**`, `Assets/Configs/BilliardRogue/UiTheme.asset`.

## Done
- Milestone 1 (compiles, no warnings): runtime UI code (UiTheme, RogueView base, widgets, 8 views, GameplayHud + HUD
  widgets) and the builder (Editor/UiViewsBuilder.cs + UI/Editor/{UiPrefabKit,UiMenuViewsBuilder,UiOverlayViewsBuilder,UiHudBuilder}.cs).

- Milestone 2: builder run in the Editor (9 prefabs + UiTheme.asset committed); root components keep their fileIDs
  across rebuilds (checked: PauseView.settingsViewPrefab ref stable). Preview renders iterated (title, player mode,
  settings, stage intro normal/boss, reward, pause, tracking lost, summary victory/defeat, HUD).
  Preview tool (scratch, not committed): scratchpad/modules/ui-views/UiPreview.cs + run_all.sh.

## In progress
- Final review pass + final report.

## Next steps
1. Final report (API + Requests). Re-run UiViewsBuilder after ImportSettingsBuilder + FontAssetsBuilder (Build All order).

## Decisions
- Enter/exit animations in code (DOTween, SetUpdate(true)) in the shared base `RogueView`; no MMF players in views.
- Settings apply live (volumes, language, toggles); Back keeps them — no revert snapshot (no Save/Cancel pair on TV).
- PauseView Controls = Back (Back = Resume; TDD table says None) — TV remote Back must resume.
- NexLocalizedString bound by table collection GUID + key name (works before LocalizationSeeder creates the entries).
- Drop-shadow text = second TMP (earlier sibling) with its own NexLocalizedString on the same key, offset by one font pixel.

## API exposed
(see final report)

## Requests
(see final report)
