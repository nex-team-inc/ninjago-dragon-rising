# Review fixes: input

Findings with area "input" from the review (scratchpad review_findings.json, indexes 1, 18, 23, 36).

## Plan
1. [x] #1 major PlayerTurnLoop: strike consumed before CanFire; cooldown stretched by hit-stop/slow-mo
   -> check CanFire first (strike stays pending, StrikeExpirySeconds still drops stale ones); cooldown ticks with the
   turn loop's unscaled time (the loop does not tick under the menu pause or the tracking-lost hold).
2. [x] #18 major PawShotInput: one missing camera frame aborts the thrust
   -> StrikeDetector.MarkMissingSample(time): drops the history only past maxSampleGapSeconds; tests.
3. [x] #23 minor PawShotInput: wall-clock frameTime stepping back freezes tracking
   -> newFrame = frameTime != last; a backward step resyncs the detector and clears the aim history; test.
4. [x] #36 minor TrackingLostView: no Back / key responder
   -> Controls Back + OnBackButton -> GameplayView pause over the overlay; builder adds a back-proxy graph;
   rebuild only TrackingLostView.prefab (run_script, not the full UiViewsBuilder).
5. [~] compile_check, lock: recompile + EditMode tests + play smoke (DebugHooks + pause/resume + TL overlay Back)
6. [ ] Commits (pathspec)

## Notes
- Views prefabs are dirty in the tree from the content integrator's Build All (fileID churn); TrackingLostView.prefab
  will also carry my rebuild.
- Lock 1 done: recompile ok, TrackingLostView.prefab rebuilt (keyResponder=GraphKeyResponder), EditMode 89/89, StrikeDetectorTests 15/15.
- The flow-ui fixer rewrote GameplayView.cs (GameplayPauseGate) and dropped my RequestPauseOverOverlay; the TL overlay
  now passes `BeginPause` (gated by pauseGate) from GameplayView.Overlays.cs. Overlays.cs also carries flow-ui's
  in-progress edits: do NOT commit it myself while theirs are uncommitted; check at the end that their commit carried it.
- Smoke script: scratchpad/input/lock2.sh + InputSmoke.cs (pause/resume, TL Back -> pause -> resume / Save & Quit).
