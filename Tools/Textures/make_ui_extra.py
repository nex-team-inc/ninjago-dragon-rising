"""Extra UI glyphs for the view polish pass (camera placeholder, calibration controls illustration).

Run:  Tools/.venv/bin/python Tools/Textures/make_ui_extra.py [--preview-dir DIR]
Out:  Tools/Staging/Assets/Sprites/BilliardRogue/UI/{Icon_Camera,Icon_Paw,Icon_Cue,Icon_Strike}.png (16x16, no 9-slice,
      shown at an integer 3x/4x like the rest of the kit) + ui_extra.png contact sheet in DIR.
Kept apart from make_ui.py so the kit script stays owned by the 2D-art pass; same palette and emblem helpers.
Deterministic.
"""
import argparse
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(os.path.dirname(HERE), "Fonts"))
import make_icons as mi  # noqa: E402
import make_ui as mu  # noqa: E402
import paintkit  # noqa: E402
import pixelkit as pk  # noqa: E402

CAMERA = ["...####.......",
          "..######......",
          "##############",
          "#.....oooo..r#",
          "#....oggggo..#",
          "#...oggggggo.#",
          "#...oghggggo.#",
          "#...oggggggo.#",
          "#...oggggggo.#",
          "#....oggggo..#",
          "#.....oooo...#",
          "##############"]

STRIKE = ["......#.......",
          "......##....#.",
          ".#....###..##.",
          ".##..######...",
          "..##########..",
          "...########...",
          "############..",
          "..#########...",
          "...########...",
          "..###########.",
          ".##..####..##.",
          "#....###....#.",
          ".....##.......",
          ".....#........"]


def icon_camera():
    img = pk.canvas(16, 16)
    body = mi.art(CAMERA, "#.orgh")
    top = np.zeros_like(body)
    top[:3] = body[:3]
    x0, y0 = mi.centered(img, body, 0, 1)
    mi.emblem(img, body, "#a8aec0", mu.INK, x0, y0, shade="#7a8298",
              extra={"#d2d6e0": top, mu.GOLD["lt"]: mi.art(CAMERA, "o"), "#1a2a4a": mi.art(CAMERA, "g"),
                     "#9fd0ff": mi.art(CAMERA, "h"), "#ff5a4a": mi.art(CAMERA, "r")})
    return img


def icon_paw():
    img = pk.canvas(16, 16)
    paw = mi.art(mi.PAW)
    x0, y0 = mi.centered(img, paw, 0, 1)
    mi.emblem(img, paw, "#fff1dc", "#1a0c14", x0, y0, shade="#e8c8a8", extra={"#ff9ab4": _pad_mask(paw)})
    return img


def _pad_mask(paw):
    """Toe beans + palm pad inside the paw silhouette (pink)."""
    m = np.zeros_like(paw)
    m[1:3, 4:6] = True
    m[1:3, 9:11] = True
    m[5:7, 1:3] = True
    m[5:7, 11:13] = True
    m[8:11, 4:10] = True
    return m & paw


def icon_cue():
    img = pk.canvas(16, 16)
    shaft = pk.line(16, 16, [(2.5, 13.5), (12.5, 3.5)], width=2)
    tip = pk.line(16, 16, [(12.0, 4.0), (13.5, 2.5)], width=2)
    butt = pk.line(16, 16, [(2.0, 14.0), (5.0, 11.0)], width=2)
    pk.put(img, shaft, "#e8b070")
    pk.put(img, butt, "#7a3a1a")
    pk.put(img, tip, "#5aa0ff")
    pk.put(img, pk.dilate(shaft | tip | butt, False) & ~(shaft | tip | butt), mu.INK)
    return img


def icon_strike():
    img = pk.canvas(16, 16)
    burst = mi.art(STRIKE)
    x0, y0 = mi.centered(img, burst, 0, 0)
    core = pk.erode(burst, True)
    mi.emblem(img, burst, mu.GOLD["md"], "#1a1008", x0, y0, shade=mu.GOLD["dk"],
              extra={mu.GOLD["lt"]: core, mu.GOLD["hi"]: pk.erode(core, True)})
    return img


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview-dir", default=None)
    a = ap.parse_args()
    preview_dir = a.preview_dir or pk.default_preview_dir("art2d")
    sprites = {
        "Icon_Camera": icon_camera(),
        "Icon_Paw": icon_paw(),
        "Icon_Cue": icon_cue(),
        "Icon_Strike": icon_strike(),
    }
    sprites = {k: paintkit.override(mu.OUT_DIR, k, img) for k, img in sprites.items()}
    for name, img in sprites.items():
        h, w = img.shape[:2]
        assert w % 4 == 0 and h % 4 == 0, (name, w, h)
        pk.save_rgba(pk.staging(*mu.OUT_DIR, name + ".png"), img)
    pk.save_rgb(os.path.join(preview_dir, "ui_extra.png"), pk.contact_sheet(list(sprites.items()), scale=6, cols=4))
    print("UI extra", {"sprites": list(sprites), "preview": preview_dir})


if __name__ == "__main__":
    main()
