# Polish: environment kit (Tools/Blender/environment/) - revision 2

Resumes the interrupted revise pass (WIP commit e734de96: camera read from ArenaConfig, measure_previews.py,
actors mask) against the director's review (scratchpad content_review_environment.json).
Previews: scratchpad content/environment/revise2/ (act{1,2,3}_diorama/_actors/_overview.png, pieces_sheet.png,
readability_game.json, boss_backdrops_game.png).

## Done (milestone 1)
- Previews sample *_Surface parts like Unity does today (mesh UVs x _Tiling 0.5, ToonLit _WORLD_UV off);
  `render_diorama.py --world-uv` keeps the world-box comparison.
- Arena floor: acts bind the one-panel-per-cell floors 2D art is producing in parallel (RuinFloor / CryptFloor slate /
  HollowFloor, staging 06:29); make_layouts binds a variant once its texture is in Starter or staging, else
  CryptFloor. Kit UVs unchanged for Env_FloorTile (each cell samples texels 1..31 of one quadrant); Env_LaunchPad
  Top_Surface UV origin moved onto the cell corners (world x -3.5 + k, z 1.6 - k).
- Env_FloorTile_Danger: DangerFrame (new part, carved dark-red groove, never emissive) + a 3.5 cm
  DangerInlay_Emissive line inside it (was a 10 cm always-red band with corner studs).
- Env_WaterPool: mid-value water (sky 6/8, teal-8 shallows), lily pads on Water, wet-sand shoreline + reeds on Rim.
- Env_Tree_A three separated canopy clumps with flattened undersides; Env_Tree_B three tiers with dark rim bands
  (they read from the high camera); Env_Stalagmite pale tips / lit shoulders; Env_CaveFloorTile darker, fewer violet
  patches; tombstone mounds darker; Env_PavingPatch rubble halved and darker.
- New pieces (additions, no renames): Env_MossBorder (Act 1 dark moss skirt hugging the walls),
  Env_CryptGroundTile (Act 2 dark palette flagstone ground, replaces the bright CryptBrick ground surface).
- make_layouts: boss backdrop x [-2.5, 2.5] z [11.6, 14.5] and >= 1.5 m wall clearance enforced for emissive
  scenery (Act 3 crystals behind the boss replaced by rock / stalagmite silhouettes, groves set back >= 2.2 m; Act 2
  candles / bones behind the Lich removed, braziers moved to |x| 5.8); sconce lights moved above the rim, 0.45 m
  inside the wall, and lit in every act (4 per act) so they pool on the arena; 3 god rays per act with varied widths,
  each landing on a feature, fitted so they never overlay the arena on screen; Act 1 scatter halved, paving on
  StoneFloor, arch 0.72x on 0.8x stairs (fully framed); Act 2 graves in two tidy rows along the path, dirt beds and
  most pebbles removed, fewer dust motes.
- Camera: game pose from the working-tree ArenaConfig (another agent is re-framing it: pitch 52, FOV 22, aim z 5.57);
  dressing pruned against that pose AND the committed one; `requested` only while the aim is > 0.3 m off z 5.8.
- Frame note: ArenaLayout matches the TDD frame, parent under it with identity. unityNotes: UV contract, floor
  binding, danger parts, draw cost.

## Measurements (previews, game pose)
| act | arena luma (before) | band | ratio (before) | grid std (before) | min enemy dE (before) |
|---|---|---|---|---|---|
| 1 | 0.32 (0.33) | 0.30 | 1.06 (0.95) | 0.049 (0.160) | 25.7 (18.3) |
| 2 | 0.19 (0.20) | 0.23 | 0.83 (0.66) | 0.054 (0.078) | 24.3 (19.1) |
| 3 | 0.23 (0.24) | 0.18 | 1.29 (1.11) | 0.060 (0.131) | 23.3 (13.4) |

## Findings / requests
- MaterialsBuilder.Surfaces must list RuinFloor and HollowFloor (else EnvironmentPieces' URP Lit fallback) and the
  integrator must sync the 2D-art staging surfaces.
- Act 2 stays below the 1.2 ratio: the slate CryptFloor albedo is 0.27 luma (RuinFloor 0.32, HollowFloor 0.33).
- Enemy dE < 35 for the dark roster (Bomber, Totem, ShieldKnight) is a floor-value vs enemy-palette trade-off.
- EnvironmentLayout.cs summary already describes the TDD frame (no change needed).

## Before
- in-game: scratchpad/integration/playable/05_playerturn.png (floor reads as wooden crates)
- previews: scratchpad/content/environment/_baseline/

## Next
- final full rebuild + verify, commit
