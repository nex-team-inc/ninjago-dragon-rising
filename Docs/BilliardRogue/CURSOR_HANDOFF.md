# Cursor handoff brief — continue while Claude Code is rate-limited

You are the Cursor agent, started automatically by `Tools/handoff/claude_limit_watchdog.py` because the Claude Code session that drives this build hit its usage limit. **Claude resumes at the reset time given in your prompt; the watchdog stops you then.** Stop starting new work 10 minutes before that time, commit, and write your notes.

## Read first (in this order)
1. This file (the **Active queue** below is maintained by Claude and is your work list).
2. `Docs/BilliardRogue/GDD-v2-Changes.md` (latest design decisions) → `GDD.md` → `TDD.md` (contracts, §0a decisions, §15 ownership, §17 asset paths).
3. `Docs/BilliardRogue/HANDOFF.md` (simulation semantics) and the progress notes in `Docs/BilliardRogue/progress/` for the items you pick up (each killed Claude agent left one — resume from it, don't redo finished work).
4. Project rules `.cursor/rules/**` (binding) and skills `.cursor/skills/unity-cli`, `.cursor/skills/unity-cli-troubleshooting`, plus `Docs/BilliardRogue/research/unity-cli-cookbook.md`.

## Rules
- Never hand-write or hand-edit `.meta`, `.unity`, `.prefab`, `.asset`. Change builder scripts (`Assets/Scripts/BilliardRogue/Editor/*Builder.cs`) or configs through Editor APIs, then run the builder via the Unity CLI: `unity command eval 'return Nex.BilliardRogue.Editor.<Builder>.Run();' --project-path /Users/simonbut/project/VibeProject3/Starter`.
- Always pass `--project-path /Users/simonbut/project/VibeProject3/Starter` (other Unity editors run on this Mac). Wrap Editor sequences in `Tools/editor_lock.sh cursor bash -c '...'`. Stop play mode in the same section you started it. Never pass `save_path` to capture commands.
- Keep `python3 Tools/compile_check.py` green; run EditMode tests after code changes (`unity command run_tests ...`, see the cookbook).
- Commit after every milestone that compiles, pathspec only your files:
  `git add <paths> && git commit -m "<intent>" -m "Co-authored-by: Cursor <cursoragent@cursor.com>" -- <paths>`.
- Log what you did in `Docs/BilliardRogue/progress/cursor-session.md` (append a dated section: items worked, commits, verification, what is left) and commit it before you stop.
- Don't start large refactors; prefer finishing queue items. Don't delete other agents' work. Don't install packages or change ProjectSettings unless the queue item says so.
- Device: a Nex Playground is reachable with `adb connect 10.4.6.137` (Android 11, Mali-G52). The APK builder is `Nex.BilliardRogue.Editor.ControlDemoBuild.Run()` → `Builds/Android/BilliardRogue_ControlDemo.apk`; install with `adb -s 10.4.6.137:5555 install -r <apk>`. Only install when a queue item asks for it.

## Active queue (maintained by Claude — top item first)
| # | Item | Status | Brief / progress note |
|---|---|---|---|
| 1 | Control lab (practice mode, control readout, live tuning, rebuild demo APK) | done (installed on device) | `progress/control-demo.md` |
| 2 | v2-A Simulation + Gameplay: balls-only rewards, stage-clear heal, Hype → ball speed/damage, hit-stop scaling + cap, analytics | done (4c718930) | `GDD-v2-Changes.md` §1, §3; `progress/v2-sim-gameplay.md` |
| 3 | v2-B Input: easier strike, body motion energy (`IMotionEnergy`), paw pointer (`IPawPointer`) | done (4403da60, e3bfe5a6) | `GDD-v2-Changes.md` §2–§4; `progress/v2-input.md` |
| 4 | v2-C UI: hand-controlled RewardView (two cat arms, dual-paw hold to pick), short ball labels (5 locales), HUD Hype meter + MOVE prompt | done (b42c1a6b, 0b2a7d5f) | `GDD-v2-Changes.md` §1, §3, §4; `progress/v2-ui.md` |
| 5 | v2-D Presentation/VFX: Hype juice (ball glow/size/trail, hit VFX scale, shake, damage numbers, aura), dancing cat | done (08268071, 56dafa71) | `GDD-v2-Changes.md` §3; `progress/v2-presentation.md` |
| 5b | **v2-E Integration + device**: apply the v2 agents' Requests, Build All, EditMode tests, play-mode smoke (Hype, reward pick with both paws / remote Enter, 2P banner), rebuild the demo APK, install + launch on 10.4.6.137 (leave on title), update `ControlDemo.md` | done (26bb1159, 742242f7; v2 APK installed on the device) | `progress/v2-integration.md` (known gaps listed there) |
| 6 | Perf (60 fps on Mali-G52; title measured ~40 fps in the dev build) — Editor-side work only; do NOT install on the device while the user is testing | in progress (Claude agent) | `progress/final-perf.md` |

If an item's progress note says another agent is mid-way, continue it from the note. If every item is done or blocked, write your notes and stop.
