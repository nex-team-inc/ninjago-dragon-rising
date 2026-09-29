# Final QA + docs — checkpoint

Owner key: `qa` (editor lock label). Spec: task item 8 in `CURSOR_HANDOFF.md`. Editor only (no adb / device).
Screenshots: `<session scratchpad>/qa/*.png` (not committed; `qa/s/` holds 1100 px copies and contact sheets).
Scratch: `qa/qa.sh` (helpers on top of `integration/playable/pl.sh`: key, top, topis, clear, rewardup, flying, alog,
realerrs), `qa/QaProbe.cs` (run_script: board, prefs, settings rows, locale, TMP overflow + missing glyphs, scripted
paws, HUD/Hype probes), sections `qa/s1.sh`..`s8.sh` with logs `s*.log`. Editor save backed up to
`qa/SaveFile.backup.es3` before the first play session (restore it when QA is done).

## Plan / status
- [x] 1. Read docs + earlier smoke helpers
- [x] 2. 1P: title → player mode → calibration → act 1 (pop-ins, skip empty turn, Hype t2/max, MOVE prompt, reward by
      scripted paws and remote Enter) → act 1 boss → act 2 → act 3 boss → victory → summary → title (s1, s2, s4)
- [x] 3. Continue after Save & Quit (state identical), defeat with practice mode off, Play again → calibration (s4)
- [x] 4. 2P: shooters P1 → P2 → P1, "P1 picks!" / "P2 picks!" banners, charcoal P2 arms, P2 paw pick (s5)
- [x] 5. Settings: 5 locales on settings/title/player mode/calibration/intro/HUD/pause/reward/summary, volumes, aim guide,
      left-handed (calibration hint + PawShotInput.LeftHanded), screen shake; persisted across a play restart (s6, s7)
- [x] 6. Secret code on title and in gameplay → DebugSettingsView, back resumes (s8); analytics spot-check
- [x] 7. Bug fix: settings language row kept the previous language's name (fixed in SettingsView, verified s8)
- [x] 8. EditMode tests (Simulation 76/76, InputCore 45/45, Assembly-CSharp-Editor 48/48), Editor save restored, commit
- [ ] 9. README.md, DesignerGuide.md, QA-Report.md, root README section; commit

## Findings
- FIXED `SettingsView.HandleLanguage`: after `SelectedLocale = next`, `RefreshLanguage` returned early because the
  locale change restarts `LocalizationSettings.InitializationOperation` (IsDone false), so the Language row kept showing
  the old language ("English" while the UI was Chinese). Now it sets the label from `next` directly.
- Not bugs (Editor-only): the Editor mouse drives the reward paws once moved (arms follow the real cursor) and UGUI
  pointer hover highlights a ball; `CameraFrameProvider: Camera timeout` errors, Jazz watchdog warning and pipeline
  `Main thread operation timed out after 5000ms` are Editor/CLI noise with no camera.
- Hype meter looked empty in two screenshots: the ball had already left (flights at Hype 1 last ~1 s); the probe shows
  target 1 / tier 3 / MAX!!! while a ball flies (shot 39).
- Analytics seen: START/STOP/PAUSE/RESUME, SCREEN per view, ui_button_click (input motion/remote/debug), ui_back,
  setup_step/complete, stage_start/clear, turn_start/end, shot_fired, ball_result (hype_avg/max/damage), reward_offered/
  chosen, boss_spawn/defeated, run_end (Victory/Defeat/Abandoned), settings_changed, secret_code_entered. Not seen:
  tracking_lost (needs a tracked body).
