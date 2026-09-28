# UI-Views module — progress

Owner paths: `Starter/Assets/Scripts/BilliardRogue/UI/**`, `UI/LocKeys.UI.cs`, `Editor/UiViewsBuilder.cs`,
`Assets/Prefabs/BilliardRogue/{Views/<8 views>,UI}/**`, `Assets/Configs/BilliardRogue/UiTheme.asset`.

## Done
- Milestone 1 (compiles, no warnings): runtime UI code (UiTheme, RogueView base, widgets, 8 views, GameplayHud + HUD
  widgets) and the builder (Editor/UiViewsBuilder.cs + UI/Editor/{UiPrefabKit,UiMenuViewsBuilder,UiOverlayViewsBuilder,UiHudBuilder}.cs).

## In progress
- Editor: recompile, run UiViewsBuilder, preview renders, iterate on layout.

## Next steps
1. Editor lock: recompile → run builder → commit prefabs + .meta files + UiTheme.asset.
2. Preview renderer (scratchpad run_script) → PNGs → iterate.
3. Final report (API + Requests).

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
