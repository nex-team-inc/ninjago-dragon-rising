# Polish wave — UI/UX (all views) — progress checkpoint

Owner: UI/UX polish agent. Resume from here: read this file + `git log --oneline -15 -- Starter/Assets/Scripts/BilliardRogue/UI Starter/Assets/Scripts/BilliardRogue/Flow Starter/Assets/Scripts/BilliardRogue/Editor`.
Scratch: `/private/tmp/claude-501/-Users-simonbut-project-VibeProject3/78b50d75-0262-494a-a64b-9a4cc3dfcbc6/scratchpad/polish-ui/`
(`before/` = copies of the playable-pass shots, `after/` = new 1920x1080 play-mode shots, `small/` = 960 copies + locale
sheets `loc_{title,calib,hud,reward,summary}.png`, `ui.sh` = helpers (shot, key <Up|Down|Left|Right|Enter|Escape> via
KeyboardNavigationController.OnKey, top), `sess_*.sh` = locked Editor sessions, `cs/*.cs` = eval snippets).

## Done (milestone 1)
1. HUD left column (`UiHudBuilder`): 448-wide columns at 32 from both edges, 16 gutters, centre band x 480..1440 free.
   Camera panel (y 32..360: glyph + "Camera" title + P1/P2 shooter chips, 384x216 screen with camera glyph + "Show both
   paws" tip) → stage/turn (376..504) → HP (520..664) → tracking warnings. The GameplayPip overlay now holds only the
   feed, placed on `UiHudBuilder.PipFeedScreenPosition/PipFeedSize` (64,-112 / 384x216); the separate Shooter panel is gone
   (its P2 chip overflowed). Feed RawImage colour white (was black = black feed on device).
2. Ball queue: counter at 64 px, NEXT slot (104, icon 5x) with name (48) + level, grid 6 columns x up to 2 rows of 64
   slots (panel 320 → 388 tall as the turn needs), bonus shots shown as extra "+" slots, power pickup tints the NEXT slot +
   "Power ready!" chip under the panel; right column is a vertical layout so the boss bar collapses.
   HudBinder re-pushes the bag at each stage start (a level-up reward left the HUD at the old level).
3. Stage intro: GameplayView hides the HUD columns + PiP before the band and slides them in after it (theme
   hudRevealDuration/Slide); the PiP feed also hides under Reward/Pause/StageIntro (it drew over them), stays for
   TrackingLost. Run start: calibration fades to the theme curtain colour, GameplayView raises the same opaque curtain at
   Initialize (covers the calibration pop and the board build), fades it once the intro band is on top.
4. Title record panel: 768 wide, sizes to its lines; "No runs yet" only with no runs, "Best run: none yet" + runs counter
   when runs exist but none finished (new key br.ui.title.noRecordYet); focus follows Continue appearing/vanishing (was
   lost after a run ended).
5. Calibration (`FlowViewPrefabsBuilder`, now drawn with the UI kit): banner, step pips 1-4, parchment "How to play" card
   (animated paws: cue paw thrusts into the ball paw, burst, ball shoots up; rows ball / cue / strike), camera panel
   (camera placeholder → starter setup previews → P1/P2 cards with Waiting/Ready), prompt on a navy strip, left-handed
   hint line. Starter PreviewsManager was invisible when nested (its root canvas stores scale 0 and size 0): fixed in the
   builder. New keys br.ui.calibration.controlsHeader/controlBall/controlCue/controlStrike (+ Left/RightPawTag constants).
6. TrackingLost panel 960 wide (inside the centre band, header 64); Summary record stamp beside the title; PlayerMode
   gets the "← → choose · OK to confirm" hint like Reward/Settings.
7. Keyboard nav verified by raising KeyboardNavigationController.OnKey: Title (Up/Down/Enter, focus on Continue/New
   Run), Settings (Up/Down, Escape back), PlayerMode (Left/Right/Enter/Escape), Calibration (Escape → PlayerMode), Pause
   (Escape opens, Up/Down, Enter resumes), Reward (Left/Right/Enter), Summary (Right, Enter → Title).
- Tests: `Editor/Tests/UiLayoutContractTests.cs` (4: HUD column/gutter contract, PiP feed on the HUD screen, pixel font
  sizes multiple of 16 in every view + HUD, key responder on every interactive view). EditMode: Assembly-CSharp-Editor
  17/18 (RenderingContractTests.MaterialsUseContractShaders fails on another agent's material change), Simulation 52/52,
  InputCore 16/16. compile_check --warnings: clean.
- Locale pass (en, fr-CA, zh-Hans, zh-Hant, ja) on Title / Calibration / HUD / Reward / Summary: no truncation; fixed the
  French NEXT name touching the frame (hero slot 120 → 104).

## Screenshots (1920x1080, play mode, unfocused Editor)
- Before: `polish-ui/before/{01_title,02_playermode,03_calibration,04_stageintro,05_playerturn,09_reward,12_pause,
  13_title_continue,16_summary,18_calibration_2p,19_2p_p1_turn}.png` (copies of the playable pass).
- After (same names, driven with simulated keys): `polish-ui/final/` + `03b_calibration_strike`, `14_continued`,
  `17_title_after_summary`, `18b_calibration_2p_strike`, `20_2p_p2_turn`. Side by side: `polish-ui/small/ba1.png`, `ba2.png`.
- Extra: `after/k_calib_2p_p1ready.png` (2P ready/waiting cards), `after/v2_hud_bag.png` (12-ball bag, 2 rows),
  `after/b_title_noruns.png` / `b_title_norecord.png` (record states), `small/b_boss_sheet.png` (boss intro → HUD slide-in),
  `small/g_burst_sheet.png`, `small/c_burst_sheet.png` (calibration → curtain → band → board), `small/loc_*.png` (5 locales).

## Next
- Nothing pending in scope.

## Findings (not fixed / for others)
- EditMode `run_tests` leaves an Untitled scene open (Main.unity must be reopened: `ucmd open_scene --path Scenes/BilliardRogue/Main`).
- `ObjectPooler.OnDestroy` NullReferenceException x12 on every play-mode exit (starter pooler, not UI).
- Summary "Turns 0" after a forced defeat in turn 1-2 (gameplay stat counts completed turns?).
- 10 fps unfocused Editor: the curtain stays up ~1 s while the board builds; expect far less on device.
