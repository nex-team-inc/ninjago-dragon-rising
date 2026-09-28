# Polish: 2D art + fonts (revision 2)

Owner: 2D art + font (Tools/Textures/, Tools/Fonts/). Outputs go to Tools/Staging/Assets/{Textures,Sprites,Fonts}/BilliardRogue/;
the integrator syncs them into Starter. Previews: scratchpad content/art2d/revise2/.

## State on resume (revise agent 1 was cut off; its edits are in e734de96)
Already done by revise 1 (verified against staging + Starter, which match byte for byte except the logo):
- Regular/Bold `V`/`v` taper early (no longer reads as U); confusables line on EN/FR sheets.
- Outline fonts BilliardPixel-Outline.ttf / -BoldOutline.ttf (1 px 8-neighbour dilation, same metrics, checked).
- FontAssetsBuilder (now in Starter, integrator-owned): *_TMP.asset names per TDD 17, SHA-1 stamp, outline assets.
- MaterialsBuilder reads surfaces.json `tiling` 0.5.
- Moss split into small patches, CrystalRock crystals cut to 2 muted specks, Spawn/Cast telegraph hues, particle
  1 px gutter + edge assertion, logo cue/spark/2 px stroke (logo only regenerated in this pass).

## Done (this pass)
- [x] Floors calm + darker than actors + self-tiling kit quadrant (e47a068b): StoneFloor, CryptFloor (now slate
  grey-blue, sat 0.146), CrystalRock (32 px periodic, specks outside the kit quadrant) + NEW RuinFloor / HollowFloor
  (Environment's ARENA_FLOOR_REQUESTS in make_layouts.py). FLOOR_LIMITS build check; surfaces.json floorMetrics.
- [x] Frame_Panel wood grain, uniform bar fills, mock-up arrows + cursor, build_art2d manifest-based stale guard (947d1345).

- [x] Particles at game scale (particles_gamescale.png); Leaf + Debris get a 1 px dark contour (294dab07).
- [x] Final clean rebuild: build_art2d --check DETERMINISTIC, 96 files; foreign UI files (Portrait_*, make_ui_extra
  Icon_*) untouched. V/U, logo, mock-up, panel, bars re-checked on the contact sheets.

## Integrator
- Surfaces (incl. RuinFloor / HollowFloor + M_Surface_*) are already in Starter (synced at 06:33, 54917f4b).
- Still to sync from staging: Particles/Debris.png, Particles/Leaf.png, UI/Bar_Fill_Hp.png, UI/Bar_Fill_Boss.png,
  UI/Frame_Panel.png, UI/Logo_BilliardRogue.png, UI/ui_slices.json -> ImportSettingsBuilder, UiViews, VfxPrefabs.
- TDD 14.2 surface list should add RuinFloor, HollowFloor; TDD 16 still says M_Surface_* `_Tiling 1` (builder uses 0.5).

## Next
- (none in scope) Optional: MossyBrick wall-band moss; per-act Env_FloorTile bevel colour (Environment).

## Findings
- In game, every *_Surface kit piece uses its own mesh box UVs (1 UV = 1 m, offset 0.5) and M_Surface_* have
  `_WORLD_UV` off, so every 1 m floor tile / wall segment shows the SAME bottom-left 32x32 quadrant of the 64 px
  texture (the StoneFloor crack repeats in every cell in 05_playerturn.png). Textures must work under that mapping.
- Measurements (albedo luma mean / kit-quadrant luma std / pixel noise hf / % tilted normals), before -> after:
  StoneFloor 0.419/0.153/0.276/53% -> 0.313/0.057/0.093/12%; CryptFloor 0.320/0.105/0.168/53% -> 0.268/0.045/0.077/12%;
  CrystalRock 0.216/0.092/0.129/45% -> 0.250/0.023/0.041/17%; RuinFloor 0.318/0.048/0.085/12%; HollowFloor 0.334/0.032/0.037/12%.
  Game scale (kit mapping, act sun tint): floor luma 0.22-0.29 vs enemy stand-ins 0.51-0.58.
- The Env_FloorTile bevel is palette gray 4/2 in every act; on the violet CrystalRock / Act 3 floors it reads as a
  lighter grid (Environment's call).
- MossyBrick: 26 moss px sit in the 16-row band a wall segment shows, so they repeat every 1 m along the walls (minor).
- Build guard lesson: a delete-first clean build removed make_ui_extra.py's Icon_* from staging (restored
  byte-identical); the manifest approach never touches files this script did not write.

## Screenshots / previews (scratchpad content/art2d/revise2/)
- floors_before_after.png (kit mapping, act tint, enemies; top = before, bottom = after)
- run3/surfaces_gamescale*.png, run3/surfaces_lit.png, run3/ui_mockup.png, panel_before_after_8x.png
