---
name: asset-prototype
description: Use when prototyping a game and completely new game assets has to be created for the game from scratch.
---

# Workflow
1. Read [`references/asset-index.md`](references/asset-index.md) and route the user's keywords to the closest asset type and theme.
2. Open the linked source catalog and search its complete inventory. Do not use any paid assets.
3. Before downloading or importing an external asset, read [`SECURITY.md`](SECURITY.md) and follow its inspection workflow.
4. If it is from an external website, download the assets. After downloading, security check the asset to ensure it is safe to use.
5. If it is from the local repository, inspect the candidate asset and its dependencies before copying or exporting it into the current project.
6. For external catalogs, use the reference links first and make only the minimum number of page requests needed. Do not crawl aggressively, enumerate download URLs, or send parallel/high-rate requests.
7. Report the names and the website of asset pack you have sourced.
8. Prompt the user to update the catalog if the list is outdated by at least three months.

# Usable Asset Catalog
- [`references/asset-index.md`](references/asset-index.md): type-first routing index for 2D, 3D, UI, VFX, animation, and audio
- [`references/music-cell-shared-assets.md`](references/music-cell-shared-assets.md): internal team-owned audio, VFX, and environment asset index
- [`references/kenney-free-assets.md`](references/kenney-free-assets.md): current free Kenney asset-page index; mainly 2D, 3D, UI, textures, and VFX, CC0 licensed
- [`references/quaternius-free-assets.md`](references/quaternius-free-assets.md): current free Quaternius pack index; mainly 3D models, environments, characters, and animation, CC0 licensed

## External asset safety
- Read [`SECURITY.md`](SECURITY.md) before downloading or importing external assets.
- Use only the free downloads and licenses identified in the references. Do not catalog or download paid bundles, paid tiers, or paid source/engine integrations.
- Verify the license file included with each downloaded archive and inspect the archive before importing it into Unity.
- Treat external downloads as untrusted: do not execute installers, scripts, or binaries from an asset archive.
- If any suspicious or executable file appears, stop using the entire asset pack immediately and warn the user before taking further action.
- Let Unity generate the `.meta` files for anything you import. Never hand-write or copy one, even when the Unity CLI is blocked; see [`unity/asset-editing.mdc`](../../rules/unity/asset-editing.mdc).
