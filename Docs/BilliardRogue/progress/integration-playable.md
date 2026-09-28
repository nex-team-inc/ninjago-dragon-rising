# Integration lead — first playable pass (progress checkpoint)

Owner: integration lead (playable pass). Resume from here after a usage-limit kill: read this file + `git log --oneline -10`.
Screenshots: `/private/tmp/claude-501/-Users-simonbut-project-VibeProject3/78b50d75-0262-494a-a64b-9a4cc3dfcbc6/scratchpad/integration/playable/`
(helper `pl.sh` there: `shot`, `st`, `hook`, `click <View> <buttonField>`, `since`, `errs`, `fps`).

## Status: DONE — full 1P + 2P flow runs in play mode with no exceptions of ours; fixes committed

### Flow walked (Main.unity, play mode, unfocused Editor at ~10 fps)
Boot (SingletonSpawner → initializer → coordinator) → Title → New Run → PlayerMode (1P) → Calibration (CameraSession
starts the MDK pipeline: dewarp, CvDetectionManager, c_mdk_detection_started) → `DebugHooks.SkipCalibration` → Gameplay
(StageIntro overlay → PlayerTurn with `DebugHooks.SetBot(true)` → balls fly, enemies take damage, HP labels, hit VFX,
SFX calls → EnemyPhase → next turns) → `ClearStage` → RewardView → `ChooseReward(0)` → stage 2 intro → Pause
(`GameplayView.RequestPause`) → Resume → Pause → Save & Quit → Title shows Continue (`Act 1 · Stage 2 · 30 HP`) →
Continue → Calibration → Skip → resumes stage 2 turn 3 → forced defeat (`Run.playerHp = 0; outcome = Defeat`) → Defeat
sequence → SummaryView (stats, New record) → Title (Best run) → New Run → 2P → Skip → shots alternate P1/P2 per ball
(`shot_fired player_index 0,1,0,1…`), HUD "Shooter P1/P2" indicator.

Buttons are driven by invoking the serialized Button.onClick of the live view (`click TitleView newRunButton`), since
`simulate_key` is unavailable; everything else through `DebugHooks`.

### Analytics coverage seen ([Analytics] + [GameAnalytics(dry-run)])
ui_button_click (title/new_run|continue, player_mode/one_player|two_players, pause/resume|save_quit, summary/title),
SCREEN + screen_exit for every view, setup_step (move_in, raise_hand), setup_complete, START/STOP (Abandoned, Defeat),
PAUSE/RESUME, stage_start, turn_start, shot_fired, ball_result, turn_end, stage_clear, reward_offered, reward_chosen,
run_end.

### Bugs fixed (commits after 26272fb3)
1. `CatView.Update` NRE every frame on the title (scene cats update before `BoardPresenter.Initialize`).
2. `FormattingException: Could not evaluate the selector "1"` on every smart-string TextLabel at OnEnable
   (LocalizeStringEvent formats the bound entry before SetKey supplies arguments): `UiPrefabKit.Label` binds layers of
   placeholder keys disabled, `TextLabel.Apply` enables them with the arguments; UI prefabs + HUD + Main.unity rebuilt.
3. `MetaProgress.runsStarted` counted every new run twice (`RunFlow.CreateNewRun` and `TurnController.BeginNewStage`
   both called `BeginRun`).
4. Run start flashed the HUD over Calibration, then the PlayerMode cards and the title logo (gameplay view instantiated
   before the pops; SimpleCanvasView shows at once; each pop fades the revealed view in): GameplayView hides canvas +
   PiP until Present, `RogueView.KeepHidden` while RunFlow unwinds, gameplay push without animation.
5. Play mode did not tick in the unfocused Editor (`Application.runInBackground` false at runtime): initializer sets it
   under `UNITY_EDITOR`.

### Observations for the polish wave (not fixed)
- Cat knight stands below the launch line and is cut off by the bottom screen edge during gameplay (only ears/head
  visible; crop `crop_11_bottom.png`) — camera pitch / `JuiceConfig.Cat.standOffset`.
- Title shows "No runs yet" + "Runs N · Wins 0" while a run is in progress (best only recorded at run end).
- Calibration with `skipCalibration` cuts straight to the HUD; a wipe or the intro band could cover it later.
- MDK in the Editor: `CameraFrameProvider: Camera timeout reached. Restarting camera` error every ~5 s (no camera
  frames for the Editor process) and `Watchdog: watchdog timed out timeout=60s`; `It is required to implement game setup
  events and StopGameSetupIfNeeded after MDK 1.10` warning at boot. Not ours.
- Unfocused Editor runs at ~10 fps (macOS throttling); `editor_focus`, `osascript` activation and `open -a` did not give
  the Editor OS focus, so a focused fps figure needs a human click on the Editor window.
