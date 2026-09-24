# music-cell-shared-assets catalog

Use this catalog to choose a source folder before searching for a specific asset. Search the
repository with the user's keywords, then inspect the matching asset and its dependencies before
copying or exporting it into the current project.

The Unity project is nested one level inside the repository, so every `Assets/...` path below is
rooted at `<repo>/music-cell-shared-assets/` — for a default clone,
`~/Documents/music-cell-shared-assets/music-cell-shared-assets/`.

## Categories

### Audio

- `Assets/NEX/BGM/` — NEX background music.
- `Assets/NEX/Sound effect/` — NEX sound effects, including `Lego/`.
- `Assets/Universal Sound FX/` — broad SFX library organized by action and sound type, including
  `8BIT/`, `AMBIENCES/`, `ANIMALS/`, `BUTTONS/`, `CARTOON/`, `IMPACTS/`, and `WEAPONS/`.
- `Assets/Cute_Game_Sounds/WAV/` — cute and casual sound effects.
- `Assets/Cute UI _ Interact Sound Effects Pack/AUDIO/` — UI interaction sounds organized by
  actions such as `Button`, `Collect`, `Pop`, `Powerup`, and `Swoosh`.
- `Assets/Farm Music Pack/Music/` and `Stingers/` — farm-themed music and stingers.
- `Assets/Ultimate Game Music Collection/` — music organized by mood and use, including
  `Ambience/`, `Combat/`, `Dungeons/`, `Locations/`, `Platform/`, `Puzzles/`, `Seasonal/`, and
  `Short Cues/`.
- `Assets/Merge Games Sound Effects and Music Pack/AUDIO/` — mixed pack containing `MUSIC/`,
  `SFX/`, and `STINGER_SUCCESS/`.

### VFX and shaders

- `Assets/Epic Toon FX/` — toon VFX, prefabs, materials, textures, shaders, and demo content.
- `Assets/Ultimate VFX/` — particle systems, scripting, shaders, editor extensions, and docs.

### 3D art and environment

- `Assets/SimpleTown/` — low-poly environment models, materials, prefabs, textures, and scenes.
- `Assets/CartoonTown-LowPolyAssets/` — environment models, prefabs, animations, materials,
  textures, and demo scenes.

## Search hints

- Music, soundtrack, ambience, combat music, or cues → `Ultimate Game Music Collection`, `Farm Music
  Pack`, `NEX/BGM`, or the mixed Merge Games pack.
- UI click, button, notification, pop, collect, or menu sound → `Cute UI _ Interact Sound Effects
  Pack`, then `Universal Sound FX`.
- Generic gameplay SFX → `Universal Sound FX`, `Cute_Game_Sounds`, or `NEX/Sound effect`.
- Toon particles, explosions, magic, or elemental effects → `Epic Toon FX` or `Ultimate VFX`.
- Town, building, prop, farming, or low-poly environment assets → `CartoonTown-LowPolyAssets` or
  `SimpleTown`.
