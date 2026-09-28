# Polish: enemy & boss models (Tools/Blender/enemies) - revise2

Owner: enemy/boss model revise agent. Scope: `Tools/Blender/enemies/**` only (outputs go to the gitignored
`Tools/Staging/Assets/...`; the integrator syncs staging -> `Starter/Assets` and re-runs the builders).
Previews (R = `/private/tmp/claude-501/-Users-simonbut-project-VibeProject3/78b50d75-0262-494a-a64b-9a4cc3dfcbc6/scratchpad/content/enemies/revise2`):
`R/before/` (in-game captures), `R/before_game/` (old models, in-game look emulation), `R/iterN/` (iterations).

## Done
- Resumed from the WIP commit e734de96: the previous revise agent's edits already cover the director review
  (content_review_enemies.json) majors: golem ShieldCrystal in its own gold hue in the outer band + floating aegis
  shard (visible on all 4 faces), bone-wall rework (bone crib + 2 big skulls, dark sockets), palette-driven emissive
  rule written into the manifest (`emissive_rule`, per-part `emissive_faces`), plus footprint check, bat/beetle value
  breaks, shorter totem/skeleton, imp mage horns/ears, icon framing (`icon_crop`, BANDS4). Rebuilding that code gave
  byte-identical FBX to what `Starter/Assets` already has (so the in-game captures show exactly that state).
- In-game look emulation (`game_look.py`, `bl_preview.py` data passes, `compose_previews.py`): the old previews
  capped light at x1.0, had no rim / torch fill / grading and a made-up camera, so they flattered the models. Now:
  real ArenaConfig camera (29 px per cell mid-arena), the act's staged floor surface, raw passes (albedo, emission,
  N.L x shadow, normal, placement id) re-shaded with the ToonLit formula + Act_<n>.asset lighting + Volume_Act<n>
  post (bloom, white balance, contrast, split toning, saturation, Neutral) read-only from the Unity project.
  Calibrated on capture 05/07: floor (132,88,59) game vs (132,107,88) emulated; the old lime slime turns lemon-yellow
  exactly like in game.
- `measure_readability.py`: per model and act, silhouette px / width and floor contrast (luma dL, Lab dE, pop =
  share of pixels with dE > 40) in the emulated frame; `readability.json/.txt` per preview dir. Baseline (old models)
  in `R/base_work/readability.json`.
- Readability pass (all 12 models; part names, hierarchy and pivots unchanged, all tri budgets / footprint / FBX
  round-trip checks pass):
  - Eyes: cute pupils 0.64 -> 0.68 of the white, bigger glint; bigger eyes on slime (0.092x0.118 -> 0.125x0.155,
    higher, tilted 46 deg), bat, shroom, beetle (moved up the bigger head, 50 deg), King Slime (0.15x0.19 ->
    0.185x0.225), mage glow eyes, totem glow eyes, skeleton / bone-wall sockets.
  - Faces toward the 58 deg camera: new `Model.tilt_up(part, deg)` (pivot kept): skeleton Head 18, knight Head 12,
    mage Head 10; bone-wall skulls tipped 18.
  - Colour blocks picked with a palette-vs-floor contrast check across the three acts: slime + King Slime lime ->
    green (lime clipped to flat lemon in Act 1), shroom pink -> orange toadstool, beetle dark teal -> yellow "bomb
    bug" shell with black spots, totem all-brown -> dark pole + red face mask + teal thunderbird, knight silver helm
    + gold-rimmed blue shield, golem slate rock (darker, eyes / core / shield pop), bat light lavender with dark wing
    rim / bones and pink face (dark / crimson variants measured worse on the Act 2-3 floors).
  - Bloom: large surfaces kept under ~shade 12-13 (in game the warm sun lights tops x1.3-1.5 and bloom starts at
    1.05); eye whites gray 8 -> gray 4 emissive because M_Palette multiplies emission by 2.2 (gray 8 was ~2.0).

## Next
- Final before/after composites, icons check, manifest, report.

## Findings
- In-game capture 05/07: slimes read as flat yellow lemons with small low eyes; skeleton / shroom wash out to pastel;
  totem is brown on the brown-lit floor; beetle eyes are hidden under the shell.
- WorldPrefabsBuilder already gives every enemy M_Palette with `_EmissionMap = Palette_Emission` (x2.2), so the
  palette-half glow works; only `EnemyStatusVisuals.emissiveRenderers` (status pulse) is name-based
  (`WorldPrefabModels.EmissiveTokens`). TDD 14.1's text still describes the name rule -> Request below.
