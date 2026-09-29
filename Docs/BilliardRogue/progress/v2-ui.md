# v2-C UI — checkpoint (key: ui)

Brief: GDD-v2-Changes.md §1 (short ball labels), §3 (Hype meter, MOVE prompt), §4 (hand-controlled RewardView).
Owns: `Assets/Scripts/BilliardRogue/UI/**`, `Editor/UiViewsBuilder.cs`, `UI/LocKeys.UI.cs`, the BilliardRogue strings CSV rows
for new keys, `Tools/Textures/make_arms.py` → `Assets/Sprites/BilliardRogue/UI/{Arm_*,Paw_*,Ring_Hold,Glow_Disc,Shadow_Ball,
Bar_Fill_Hype}.png`, and the SetPawPointer call site in `Flow/GameplayView.Overlays.cs`.
Editor lock key: `ui`. Scratch: `<session scratchpad>/v2ui/` (sess_*.sh sessions, PawsVerify.cs, shots/).

## Status: DONE (commits b42c1a6b + the follow-up "v2 UI: paws must move before a hold counts…")
- [x] 1. make_arms.py (`Tools/.venv/bin/python Tools/Textures/make_arms.py`, deterministic): Arm_P1/P2 (24x16 tiling fur
      sleeve), Paw_P1/P2_Open/Grab (36x40, palm centre = pivot), Ring_Hold, Glow_Disc, Shadow_Ball, Bar_Fill_Hype → Starter
      UI sprites + staging mirror. ImportSettingsBuilder gives them the UI sprite settings (PPU 100/3, point).
- [x] 2. RewardView v2: RewardBallOption (replaces RewardCard), PawArm, RewardPawPicker (pure logic + 5 EditMode tests),
      UiRewardViewBuilder. HUD: HypeMeterWidget + MovePromptWidget (UiHypeBuilder), GameplayHud.SetHype/ShowMovePrompt.
- [x] 3. Flow call site (GameplayView.ChooseRewardAsync): Initialize(pacing, display.WorldCamera);
      chooser = numPlayers > 1 ? stageNumber % numPlayers : 0; SetPawPointer(router.PawPointer, chooser, numPlayers).
- [x] 4. 21 CSV rows x 5 locales, LocalizationSeeder (234 rows, 0 missing), FontAssetsBuilder (CJK atlas 955 chars),
      `UiViewsBuilder.RunRewardAndHud()` (new: rebuilds only RewardView + GameplayHud; Run() still rebuilds everything).
- [x] 5. Verified: compile_check green; EditMode RewardPawPickerTests 5/5, UiLayoutContractTests 4/4; play mode (scripted
      IPawPointer via run_script = same path as the Editor mouse): rest arms, single-paw hover (enlarge + focus ring),
      both paws → ring fills → pick with grab + pull-down, analytics `ball_paws`; remote Right+Enter → `ball_remote`;
      DebugHooks.ChooseReward → `ball_debug`; 2P: "P1 picks!" (orange) after stage 1, "2Pがえらぶ！" (charcoal) after stage 2;
      HUD tier 1/2/MAX callouts + MOVE prompt in en and ja. Shots: scratch `v2ui/shots/`.

## API
- `RewardView.Initialize(PacingConfig pacing, Camera? vfxCamera = null)` (world camera for the pick VFX),
  `SetPawPointer(IPawPointer?, chooserIndex, numPlayers)` (contract), `ChooseAsync`, `Hover`, `TryChoose` unchanged.
  Picks are tracked as `RunAnalytics.UiAction("reward", "ball_paws" | "ball_remote" | "ball_debug", index)`.
- `RewardPawPicker`: `Reset(count, armDistance)`, `SetBall`, `Observe(rawLeft, rawRight)`, `Step(tracked, left, right, dt,
  hold, drain)` → picked index; a paw must move `armDistance` (UiTheme.rewardPawArmDistance = 60) after the view opens before
  a hold counts (resting paws / the Editor mouse over a ball never auto-pick).
- `GameplayHud.SetHype(hype01, tier)` → HypeMeterWidget (bar, ticks at 0.25/0.55/0.85, callouts `br.hud.hypeTier1..3`,
  stinger `UiTheme.HypeTierSfx(tier)` on a tier rise); `ShowMovePrompt(bool)` → MovePromptWidget (dancing portrait of the
  active shooter + waving paws, MOVE! + hint). Both cheap to call every frame.
- `LocKeys.Ball.Short(type)` / `Shorts` (br.ball.<type>.short), `LocKeys.Reward.PickHeader/PawHint/ChooserBanner`,
  `LocKeys.Hud.Hype/HypeTier1..3/HypeTiers/MovePrompt/MoveHint`.
- UiTheme: reward motion tuning (rewardHoldSeconds 0.8, drain 2, hit scale 1.25, sharpness 16, hover 1.18, bob, arm rise,
  grab, arm distance, hover SFX, pick VFX = LevelUpBurst), Hype tier colours/SFX, idle alpha, MOVE dance beat/angle,
  sprites `Arm(i)/PawOpen(i)/PawGrab(i)/RingHold/GlowDisc/ShadowBall/HypeFill`.

## Requests (for the integrator)
- `rewardHoldSeconds` lives in UiTheme (UI-owned config); move it to PacingConfig if you prefer it with the other reward
  pacing values.
- The first v2 UI commit's RewardCard.cs deletion was staged early and got swept into 4403da60 (v2 input); b42c1a6b makes
  HEAD compile again. Agents: always commit with a pathspec (`git commit -- <paths>`), never a bare `git commit`.
- Other cards naming balls: the HUD ball queue shows name + level only (no effect text), Summary lists no ball effects, so
  no other short-label call sites were needed.
- FontAssetsBuilder must be re-run whenever new CJK strings are seeded (done here for the v2 UI strings).

## Next
Nothing pending in scope.
