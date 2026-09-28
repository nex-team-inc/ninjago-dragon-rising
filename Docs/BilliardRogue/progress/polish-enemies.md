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

- Final pass: skeleton Head `scale_part` 1.06 (new helper), mage hat brim 0.28 -> 0.31; final build is
  deterministic (second run: fbx_changed=False for all 12), tri budgets 452-600 / 992-1148, footprint + FBX round-trip
  checks pass; part names / hierarchy unchanged (only the Eyes pivots of Slime/Bat/Healer/KingSlime moved with the
  bigger, higher eyes, and the Mage Hat pivot moved with the head tilt). compile_check green (no C# touched).

## Results (R/final, R/compare_*.png)
- Before/after: `R/compare_act1_x4.png`, `R/compare_rows_acts.png`; in-game before: `R/before/game_05_crop.png`,
  `R/before/game_07_crop.png`; final previews `R/final/{arena_act*,context_act*_x3,contact_sheet,icons}.png`.
- Eye size at game scale (29 px/cell): slime 5.4 -> 7.3 px wide, King Slime 8.7 -> 10.7, skeleton sockets 4.7 -> 6.8,
  beetle 3.9 -> 5.0, shroom 4.0 -> 4.9, totem 4.1 -> 4.9, mage 3.3 -> 4.2, bat 4.6 -> 5.3.
- Floor contrast (R/final/readability_vs_before.txt, mean Lab dE Act1/2/3): beetle 44/39/41 -> 62/62/66, shroom
  42/48/43 -> 53/55/60, totem 35/38/35 -> 42/42/42, mage 43/49/47 -> 47/51/52, knight 50/47/45 -> 52/50/50; slime
  62/70/69 -> 60/63/66 and bat 57/45/48 -> 54/41/46 about level (now green / lavender instead of lemon / pastel);
  skeleton 44/53/44 -> 42/49/40 and bone wall 51/55/51 -> 49/51/47 slightly lower (bone tops held under the bloom
  threshold); every model stays >= 40 in every act.

## Next (not in this module)
- Integrator: `Tools/sync_staging.sh`, then re-run WorldPrefabsBuilder (labelHeight/centerHeight come from bounds).
- Requests below (TDD 14.1 emission text; optional outline pass; Act 1 exposure).

## Findings
- In-game capture 05/07: slimes read as flat yellow lemons with small low eyes; skeleton / shroom wash out to pastel;
  totem is brown on the brown-lit floor; beetle eyes are hidden under the shell.
- WorldPrefabsBuilder already gives every enemy M_Palette with `_EmissionMap = Palette_Emission` (x2.2), so the
  palette-half glow works; only `EnemyStatusVisuals.emissiveRenderers` (status pulse) is name-based
  (`WorldPrefabModels.EmissiveTokens`). TDD 14.1's text still describes the name rule -> Request below.
- Bloom in game starts at 1.05 on the gamma-space colour; with Act 1 sun 1.35 x (1, .78, .5) plus rim 0.35, lit
  albedo above shade ~12 blooms / clips to pastel. Kept large surfaces <= 12-13; highlights only as small specks.
- The mean-colour floor-contrast metric does not capture the internal reads (eyes, faces); those are checked visually
  in the x4 crops.

## Requests (TDD 15)
1. TDD 14.1 last paragraph -> "palette-mapped models use M_Palette (_EMISSION always on, _EmissionMap =
   Palette_Emission x PaletteEmission); the palette's right half decides what glows; name-based flags
   (WorldPrefabModels.EmissiveTokens) only pick the renderers the status pulse drives." (The builder already works
   this way; only the text is stale.)
2. Rendering (optional): the TDD 10 `BilliardRogue/Outline` inverted-hull pass for enemy renderers would be the
   strongest remaining silhouette boost on the busy floors; the model side can bake smoothed normals into UV2 on
   request.
3. Presentation: HP labels must not cover the head of the enemy in the row behind (tall knight / mage / totem).
