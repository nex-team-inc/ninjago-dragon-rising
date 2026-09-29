# v2-E — Integration + device (checkpoint)

Owner key: `integration` (editor lock label). Spec: `GDD-v2-Changes.md`; inputs: `progress/v2-{sim-gameplay,input,ui,presentation}.md`
(their "Requests" sections). Scratch: `<session scratchpad>/v2/`. A Cursor agent may resume this item from here.

## Plan / status
- [x] 1. Requests applied: control readout Hype row (`UI/Debug/ControlReadoutEnergyRow`: ENERGY routed / BODY meter energy,
      nodes, in/s) + "(line)" strike log entries; `HypeJuice` tiers + hysteresis from `HypeConfig` (JuiceConfig.Hype as fallback);
      bot/debug energy already report IsTracked; CURSOR_HANDOFF queue updated; input metas were already committed.
      Not done (optional, spec keeps board Hype to flight): DebugHooks.SetHype → BoardPresenter.SetDebugHype. Kept the v2-A
      MOVE-prompt rule (needs a tracked motion source) and rewardHoldSeconds in UiTheme.
- [~] 2. (done: recompile, Build All 19/19, EditMode "Nex.BilliardRogue" 154/154) Editor recompile → Build All → EditMode tests → play smoke (1P Hype, reward paws + remote Enter, 2P banner), screenshots in scratch `v2/`.
- [ ] 3. `ControlDemoBuild.Run()` → `Builds/Android/BilliardRogue_ControlDemo.apk`.
- [ ] 4. Device 10.4.6.137: install -r, launch, logcat check, screenshot, leave on title.
- [ ] 5. `ControlDemo.md` v2 section. Commit.

## Next
Step 2 play smoke: scripts in scratch `v2/` (s1_build.sh, s2_tests.sh, s3_play.sh); reuse `v2ui/PawsVerify.cs` for scripted paws.

## Requests
(none yet)
