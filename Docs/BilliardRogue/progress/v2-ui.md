# v2-C UI — checkpoint (key: ui)

Brief: GDD-v2-Changes.md §1 (short ball labels), §3 (Hype meter, MOVE prompt), §4 (hand-controlled RewardView).
Owns: `Assets/Scripts/BilliardRogue/UI/**`, `Editor/UiViewsBuilder.cs`, `UI/LocKeys.UI.cs`, the BilliardRogue strings CSV rows
for new keys, `Tools/Textures/make_arms.py` → `Assets/Sprites/BilliardRogue/UI/{Arm_*,Paw_*,Ring_Hold,...}.png`, and the
SetPawPointer call site in `Flow/GameplayView.Overlays.cs`.
Editor lock key: `ui`. Scratch: `$SCRATCH/v2ui/` (session scratchpad).

## Plan / status
- [x] 1. make_arms.py (`Tools/.venv/bin/python Tools/Textures/make_arms.py`, deterministic): Arm_P1/P2 (24x16 tiling
      sleeve), Paw_P1/P2_Open/Grab (36x40), Ring_Hold, Glow_Disc, Shadow_Ball, Bar_Fill_Hype → Starter UI sprites + staging.
- [x] 2. Code (compile_check green, not yet committed): RewardView v2 (RewardBallOption widget replaces RewardCard, RewardPawArms, RewardPawPicker), HUD
      HypeMeterWidget + MovePromptWidget, GameplayHud.SetHype/ShowMovePrompt, UiTheme fields, LocKeys (ball shorts, hype,
      move, reward paw hint, chooser banner), builders (UiRewardViewBuilder, UiHypeBuilder).
- [x] 3. Flow call site: `view.SetPawPointer((context.inputs[chooser] as ShotInputRouter)?.PawPointer, chooser, numPlayers)`
      with chooser = run.stageNumber % numPlayers.
- [ ] 4. CSV rows (5 locales) + LocalizationSeeder; ImportSettingsBuilder for new sprites; UiViewsBuilder.Run().
- [ ] 5. Verify: compile_check, EditMode tests, play-mode screenshots (mouse paws) en + ja.

## Next
Editor section (lock key ui): urecompile → ImportSettingsBuilder.Run() (new sprites) → LocalizationSeeder (CSV rows already
appended) → FontAssetsBuilder.Run() (CJK glyphs) → UiViewsBuilder.RunRewardAndHud() (only RewardView + GameplayHud;
Run() rebuilds all views with fileID churn) → utests RewardPawPickerTests + UiLayoutContractTests → commit.
Then play-mode screenshots: scripted IPawPointer via run_script (same path as the mouse), en + ja.

## API
(filled as implemented)

## Requests
(none yet)
