# Review fixer: flow-ui (areas ui, flow, starter, editor)

Findings source: session scratchpad review_findings.json (16 findings in my areas; F-numbers are indices in that subset).

## Status: done
- [x] F0+F5 platform stop already raised at run start -> GameplayView is the single pause owner (subscribes right after
  the session starts, the replay opens the pause view); GameSession only saves on a stop
- [x] F1 RewardView waits to be the top view before PopSelf
- [x] F2 top-view safety net resumes only after a shown pause view left; session overlays wait while paused (GameplayPauseGate)
- [x] F3 GameplayView.RunAsync logs a faulted run loop and still raises RunEnded (Abandoned)
- [x] F4 tracking_lost: one emitter (overlay, real time away), skipped after run end; TurnController copy removed
- [x] F6/F13 secret_code_entered: ViewManager.SecretCodeEntered (starter) -> coordinator -> RunAnalytics.SecretCode
- [x] F7 calibration test strike: card shows "Show both paws" (LocKeys.Setup.ShowBothPaws) while IsTracking is false
- [x] F8 skipAll |= debug setting (SkipCalibration during the push animation survives)
- [x] F9 reload rule compares the last started camera session count (CameraSession.IsPlayerCountChange)
- [x] F10 RunFlow reads covered views from ViewManager.CollectStackViews (no FindObjectsByType)
- [x] F11 GameplayView [SerializeField] GameplayHud hud wired by FlowViewPrefabsBuilder; GameplayView.prefab rebuilt alone
- [x] F12 coordinator Initialize/StartMain -> WaitUntilEnabledAsync/StartMainAsync
- [x] F14 coordinator 415 -> 294 lines: PlayerShotInputFactory + CoordinatorDebugHooks
- [x] F15 braces in Flow/UI/Analytics runtime (incl. TrackingLostView after the input fixer committed it); other
  modules' ~295 cases deferred to their owners
- [x] Verification: compile_check green (tree and index); EditMode Assembly-CSharp-Editor 36/36, Simulation 56/56,
  InputCore 19/19; play smoke scratchpad/flowui/lock3.sh all scenarios pass, no game console errors

## Commits
aa6f5378 coordinator split + secret code; f493bf5c pause gate / overlays / run end / HUD ref; 85f4d9bb flow stack views,
reload rule, calibration; f3676cf0 braces; (this) TrackingLostView braces + checkpoint.
GameSession (save-only stop) and TurnController (tracking_lost removal) hunks were swept into the core fixer's 94110054,
the initializer rename into e153c23a.

## Notes
- Never stage early in the shared tree: another fixer's plain `git commit` swept my staged partial deletions.
- Mixed files were committed as HEAD + my edits via update-index (scratchpad/flowui/stage_mixed.py, index_compile.sh).
