# v2-E — Integration + device (checkpoint)

Owner key: `integration` (editor lock label). Spec: `GDD-v2-Changes.md`; inputs: `progress/v2-{sim-gameplay,input,ui,presentation}.md`
(their "Requests" sections). Scratch: `<session scratchpad>/v2/`. A Cursor agent may resume this item from here.

## Plan / status
- [x] 1. Requests applied: control readout Hype row (`UI/Debug/ControlReadoutEnergyRow`: ENERGY routed / BODY meter energy,
      nodes, in/s) + "(line)" strike log entries; `HypeJuice` tiers + hysteresis from `HypeConfig` (JuiceConfig.Hype as fallback);
      bot/debug energy already report IsTracked; CURSOR_HANDOFF queue updated; input metas were already committed.
      Not done (optional, spec keeps board Hype to flight): DebugHooks.SetHype → BoardPresenter.SetDebugHype. Kept the v2-A
      MOVE-prompt rule (needs a tracked motion source) and rewardHoldSeconds in UiTheme.
- [x] 2. Recompile, Build All 19/19, EditMode "Nex.BilliardRogue" 154/154; play smoke OK (Hype 0: speed 13, 1 dmg/hit; Hype 1:
      speed 23.4, 3 dmg/hit, tier 3, MAX!!! meter, pink aura; MOVE prompt at forced 0.05; bot energy reaches tier 2; paw hold →
      `ball_paws`, Right+Enter → `ball_remote`; 2P "P2 picks!" with charcoal arms). Shots in scratch `v2/shots/`. → EditMode tests → play smoke (1P Hype, reward paws + remote Enter, 2P banner), screenshots in scratch `v2/`.
- [ ] 3. `ControlDemoBuild.Run()` → `Builds/Android/BilliardRogue_ControlDemo.apk`.
- [ ] 4. Device 10.4.6.137: install -r, launch, logcat check, screenshot, leave on title.
- [ ] 5. `ControlDemo.md` v2 section. Commit.

## Next
Step 3: `ControlDemoBuild.Run()` inside the lock, then device (step 4). Scratch scripts: `v2/s1_build.sh`, `s2_tests.sh`, `s3_play.sh`, `s4_hype.sh`, `IntegVerify.cs`.

## Requests
(none yet)
