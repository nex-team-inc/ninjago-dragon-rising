"""Sprites that exist only as paintings (no procedural generator): enemy icons and the cat portraits.

Run:  Tools/.venv/bin/python Tools/Textures/make_painted.py [--preview-dir DIR]
Out:  Tools/Staging/Assets/Sprites/BilliardRogue/Icons/Enemy_<Type>.png (48x48) and
      Tools/Staging/Assets/Sprites/BilliardRogue/UI/Portrait_CatP<1|2>.png (128x128), rendered from
      Tools/Textures/paint/{Icons,UI}/<name>.paint (see paintkit.py), + painted.png contact sheet in DIR.
These used to be Blender renders (Tools/Blender/enemies, Tools/Blender/heroprops) downsampled to pixel size, which
left render noise and mushy detail; they are now painted by hand from the same models (colours, silhouettes and
features match the 3D enemies and cats). A name without a painting is skipped, so the Blender icon stays until it
is painted. Sizes must match the committed sprites (their .meta, pivots and UI rects assume them). Deterministic.
"""
import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import paintkit  # noqa: E402
import pixelkit as pk  # noqa: E402

ENEMIES = ["Slime", "Bat", "Skeleton", "ShieldKnight", "Mage", "Bomber", "Healer", "Totem", "BoneWall",
           "KingSlime", "BoneLich", "CrystalGolem"]
PAINTED = [(("Sprites", "BilliardRogue", "Icons"), f"Enemy_{e}", (48, 48)) for e in ENEMIES] + \
          [(("Sprites", "BilliardRogue", "UI"), f"Portrait_CatP{p}", (128, 128)) for p in (1, 2)]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview-dir", default=None)
    a = ap.parse_args()
    preview_dir = a.preview_dir or pk.default_preview_dir("art2d")
    done, items = [], []
    for out_dir, name, (w, h) in PAINTED:
        path = paintkit.paint_path(out_dir[-1], name)
        if not os.path.exists(path):
            continue
        img = paintkit.render(path)
        if img.shape[:2] != (h, w):
            sys.exit(f"{path}: painted {img.shape[1]}x{img.shape[0]}, the sprite is {w}x{h}")
        pk.save_rgba(pk.staging(*out_dir, name + ".png"), img)
        done.append(name)
        items.append((name, img))
    if items:
        pk.save_rgb(os.path.join(preview_dir, "painted.png"), pk.contact_sheet(items, scale=3, cols=7))
    print("PAINTED", {"sprites": len(done), "skipped": len(PAINTED) - len(done)})


if __name__ == "__main__":
    main()
