"""Build the original Billiard Pixel fonts (TTF) from the bitmap glyph sources in this folder.

Run:  Tools/.venv/bin/python Tools/Fonts/build_pixel_font.py [--preview-dir DIR]
In:   Tools/Fonts/glyphs_regular.txt, Tools/Fonts/glyphs_bold.txt (edit these to change glyphs)
Out:  Tools/Staging/Assets/Fonts/BilliardRogue/BilliardPixel.ttf        (Regular, body text)
      Tools/Staging/Assets/Fonts/BilliardRogue/BilliardPixel-Bold.ttf   (Bold / display, headlines)
      Tools/Staging/Assets/Fonts/BilliardRogue/BilliardPixel_charset.txt (every code point, for TMP static atlases)
      sample sheets (English, French, digits) at 16/32/48 px in DIR.

Grid: 16 px em (UPM 1024, 1 design px = 64 units). Ascender 14 px, descender 4 px (line height 18 px at size 16);
(ascender + descender) / 2 = 5 = the cap-height centre, so TMP "Middle" alignment centres capitals exactly.
Outlines are traced pixel-edge polygons (outer contours clockwise, holes counter-clockwise, diagonal-touching
pixels kept separate) so FreeType/TMP rasterise them pixel-exactly at 16 x n px. The build renders every glyph
with FreeType mono (Pillow) at 16 and 32 px and fails if any pixel differs from the source bitmap.
Deterministic: fixed head timestamps; re-runs are byte-identical.
"""
import argparse
import array
import os
import sys

import numpy as np
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import newTable
from fontTools.ttLib.tables import ttProgram
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT_DIR = os.path.join(REPO, "Tools", "Staging", "Assets", "Fonts", "BilliardRogue")
UPM, PX = 1024, 64
ASC, DESC = 14, 4
FIXED_TIME = 3_880_000_000  # seconds since 1904 (2026), keeps the binary deterministic

ACCENTS = {
    "regular": {
        "grave": ["##.", ".##"], "acute": [".##", "##."], "circumflex": [".##.", "#..#"],
        "tilde": [".##.#", "#.##."], "diaeresis": ["##.##"], "ring": [".##.", "#..#", ".##."],
        "cedilla": [".#.", "..#", "##."], "macron": ["####"],
    },
    "bold": {
        "grave": ["###.", ".###"], "acute": [".###", "###."], "circumflex": [".####.", "##..##"],
        "tilde": [".###.##", "##.###."], "diaeresis": ["##..##", "##..##"], "ring": [".###.", "##.##", ".###."],
        "cedilla": [".##.", "..##", "###."], "macron": ["#####", "#####"],
    },
}

_VOWELS = {
    "A": "ÀÁÂÃÄÅ", "a": "àáâãäå",
    "E": "ÈÉÊË", "e": "èéêë",
    "I": "ÌÍÎÏ", "ı": "ìíîï",
    "O": "ÒÓÔÕÖ", "o": "òóôõö",
    "U": "ÙÚÛÜ", "u": "ùúûü",
}
_ORDER6 = ["grave", "acute", "circumflex", "tilde", "diaeresis", "ring"]
_ORDER4 = ["grave", "acute", "circumflex", "diaeresis"]
COMPOSITES = {}
for base, chars in _VOWELS.items():
    order = _ORDER6 if len(chars) == 6 else _ORDER6[:5] if len(chars) == 5 else _ORDER4
    for ch, acc in zip(chars, order):
        COMPOSITES[ch] = (base, acc)
COMPOSITES.update({
    "Ç": ("C", "cedilla"), "ç": ("c", "cedilla"), "Ñ": ("N", "tilde"), "ñ": ("n", "tilde"),
    "Ý": ("Y", "acute"), "ý": ("y", "acute"), "Ÿ": ("Y", "diaeresis"), "ÿ": ("y", "diaeresis"),
})

SPACES = {0x20: 4, 0xA0: 4, 0x2009: 2, 0x202F: 2}  # regular advances (bold +1)
REQUIRED = ([chr(c) for c in range(0x20, 0x7F)] + [chr(c) for c in range(0xA0, 0x100)] +
            list("ŒœŸ‘’‚“”„…–—€•"
                 "‹›←↑→↓♥★ı  "))


# ----------------------------------------------------------------------------------------------
# glyph sources
# ----------------------------------------------------------------------------------------------

def parse(path):
    """-> {codepoint: set((x, y))} with y = rows above the baseline (0 = the row sitting on it)."""
    glyphs, cur, rows, top = {}, None, [], 0

    def flush():
        if cur is not None:
            px = set()
            for i, r in enumerate(rows):
                for x, c in enumerate(r):
                    if c == "#":
                        px.add((x, top - 1 - i))
            width = max((len(r.rstrip(".")) for r in rows), default=0)
            width = max(width, max((len(r) for r in rows), default=0))
            glyphs[cur] = (px, width)

    with open(path, encoding="utf-8") as f:
        for raw in f:
            line = raw.rstrip("\n")
            if not line.strip() or line.startswith(";"):
                continue
            if line.startswith("@ "):
                flush()
                tok = line.split()
                ch = tok[1]
                cur = int(ch[2:], 16) if ch.startswith("U+") else ord(ch)
                top = int(tok[2])
                rows = []
            else:
                rows.append(line.strip())
    flush()
    return glyphs


def bitmap_rows(rows, top):
    return {(x, top - 1 - i) for i, r in enumerate(rows) for x, c in enumerate(r) if c == "#"}


def compose(glyphs, accents):
    out = dict(glyphs)
    for ch, (base, acc) in COMPOSITES.items():
        cp = ord(ch)
        if cp in out:
            continue
        bpx, bw = glyphs[ord(base)]
        rows = accents[acc]
        aw, ah = max(len(r) for r in rows), len(rows)
        w = max(bw, aw)
        boff = (w - bw) // 2
        diff = w - aw - 2 * ((w - aw) // 2)
        aoff = (w - aw) // 2 + (1 if acc == "acute" and diff else 0)
        top_y = max(y for _, y in bpx)
        bottom_y = min(y for _, y in bpx)
        px = {(x + boff, y) for x, y in bpx}
        if acc == "cedilla":
            # hang under the bottom row, centred on the letter's lower bowl
            px |= {(x + aoff, y) for x, y in bitmap_rows(rows, bottom_y)}
        else:
            px |= {(x + aoff, y) for x, y in bitmap_rows(rows, top_y + 2 + ah)}  # 1 empty row between
        out[cp] = (px, w)
    return out


def derived(glyphs, accents, style):
    out = dict(glyphs)
    lower_top = max(y for _, y in glyphs[ord("x")][0])
    if 0xAD not in out:
        out[0xAD] = glyphs[ord("-")]
    if 0xA8 not in out:
        rows = accents["diaeresis"]
        out[0xA8] = (bitmap_rows(rows, lower_top + 2 + len(rows)), max(len(r) for r in rows))
    for cp, adv in SPACES.items():
        out[cp] = (set(), adv - 1 + (1 if style == "bold" else 0))
    return out


# ----------------------------------------------------------------------------------------------
# outline tracing
# ----------------------------------------------------------------------------------------------

def trace(px):
    """Pixel set -> list of closed contours (lists of (x, y) in pixel units), TrueType orientation."""
    edges = {}

    def add(a, b):
        edges.setdefault(a, []).append(b)

    for x, y in px:
        if (x, y + 1) not in px:
            add((x, y + 1), (x + 1, y + 1))      # top: left -> right
        if (x + 1, y) not in px:
            add((x + 1, y + 1), (x + 1, y))      # right: top -> bottom
        if (x, y - 1) not in px:
            add((x + 1, y), (x, y))              # bottom: right -> left
        if (x - 1, y) not in px:
            add((x, y), (x, y + 1))              # left: bottom -> top
    contours = []
    while edges:
        start = min(edges)
        pts = [start]
        cur, prev_dir = start, None
        while True:
            outs = edges[cur]
            if len(outs) == 1 or prev_dir is None:
                nxt = outs[0]
            else:
                dx, dy = prev_dir
                right, straight, left = (dy, -dx), (dx, dy), (-dy, dx)
                cand = {(o[0] - cur[0], o[1] - cur[1]): o for o in outs}
                nxt = next(cand[d] for d in (right, straight, left) if d in cand)
            outs.remove(nxt)
            if not outs:
                del edges[cur]
            prev_dir = (nxt[0] - cur[0], nxt[1] - cur[1])
            cur = nxt
            if cur == start:
                break
            pts.append(cur)
        contours.append(simplify(pts))
    contours.sort()
    return contours


def simplify(pts):
    out = []
    n = len(pts)
    for i in range(n):
        a, b, c = pts[i - 1], pts[i], pts[(i + 1) % n]
        if (b[0] - a[0]) * (c[1] - b[1]) - (b[1] - a[1]) * (c[0] - b[0]) != 0:
            out.append(b)
    # canonical start point (deterministic output)
    k = out.index(min(out))
    return out[k:] + out[:k]


def ttglyph(px):
    pen = TTGlyphPen(None)
    for contour in trace(px):
        pen.moveTo((contour[0][0] * PX, contour[0][1] * PX))
        for x, y in contour[1:]:
            pen.lineTo((x * PX, y * PX))
        pen.closePath()
    g = pen.glyph()
    # FreeType auto-hints TrueType fonts whose maxp.maxSizeOfInstructions is 0; a 1-byte no-op program per glyph
    # routes them through the bytecode interpreter instead, which keeps the grid-aligned outlines untouched.
    g.program = ttProgram.Program()
    g.program.fromAssembly(["SVTCA[0]"])
    return g


def glyph_name(cp):
    if cp == 0x20:
        return "space"
    return "uni%04X" % cp


# ----------------------------------------------------------------------------------------------
# build
# ----------------------------------------------------------------------------------------------

def build(style, glyphs, path):
    names = [".notdef"] + [glyph_name(cp) for cp in sorted(glyphs)]
    fb = FontBuilder(UPM, isTTF=True)
    fb.setupGlyphOrder(names)
    fb.setupCharacterMap({cp: glyph_name(cp) for cp in glyphs})
    glyf, hmtx = {}, {}
    nw = 7 if style == "bold" else 6
    notdef = {(x, y) for x in range(nw) for y in range(10) if x in (0, nw - 1) or y in (0, 9)}
    glyf[".notdef"] = ttglyph(notdef)
    hmtx[".notdef"] = ((nw + 1) * PX, 0)
    for cp, (px, width) in sorted(glyphs.items()):
        name = glyph_name(cp)
        glyf[name] = ttglyph(px) if px else TTGlyphPen(None).glyph()
        lsb = min(x for x, _ in px) * PX if px else 0
        hmtx[name] = ((width + 1) * PX, lsb)
    fb.setupGlyf(glyf)
    fb.setupHorizontalMetrics(hmtx)
    fb.setupHorizontalHeader(ascent=ASC * PX, descent=-DESC * PX, lineGap=0)
    bold = style == "bold"
    sub = "Bold" if bold else "Regular"
    fb.setupNameTable({
        "familyName": "Billiard Pixel", "styleName": sub,
        "uniqueFontIdentifier": f"Nex:BilliardPixel-{sub}:1.000",
        "fullName": f"Billiard Pixel {sub}", "psName": f"BilliardPixel-{sub}", "version": "Version 1.000",
        "copyright": "Copyright (c) 2026 Nex. Original pixel font for Billiard Rogue.",
        "designer": "Billiard Rogue art pipeline (Tools/Fonts/build_pixel_font.py)",
    })
    fb.setupOS2(sTypoAscender=ASC * PX, sTypoDescender=-DESC * PX, sTypoLineGap=0,
                usWinAscent=ASC * PX, usWinDescent=DESC * PX, sxHeight=(8 if bold else 7) * PX, sCapHeight=10 * PX,
                usWeightClass=700 if bold else 400, fsSelection=(0x20 if bold else 0x40) | 0x80, fsType=0,
                achVendID="NEX ", ulUnicodeRange1=0b11, usDefaultChar=0, usBreakChar=0x20, version=4)
    fb.setupPost(isFixedPitch=0, underlinePosition=-2 * PX, underlineThickness=PX)
    # no-op fpgm/prep/cvt + per-glyph programs (see ttglyph): FreeType treats the font as natively hinted and runs
    # the bytecode interpreter (which moves nothing) instead of the auto-hinter; verified pixel-exact below.
    for tag in ("fpgm", "prep"):
        table = newTable(tag)
        table.program = ttProgram.Program()
        table.program.fromAssembly(["SVTCA[0]"])
        fb.font[tag] = table
    cvt = newTable("cvt ")
    cvt.values = array.array("h", [0])
    fb.font["cvt "] = cvt
    gasp = newTable("gasp")
    gasp.gaspRange = {0xFFFF: 0x0001}  # grid-fit only, no grey-scale smoothing
    fb.font["gasp"] = gasp
    maxp = fb.font["maxp"]
    maxp.maxSizeOfInstructions = 1  # fontTools does not recalc this; FreeType reads it to decide on auto-hinting
    maxp.maxZones = 2
    maxp.maxStackElements = max(getattr(maxp, "maxStackElements", 0), 1)
    head = fb.font["head"]
    head.created = head.modified = FIXED_TIME
    head.macStyle = 1 if bold else 0
    head.lowestRecPPEM = 16
    fb.font.recalcTimestamp = False
    os.makedirs(os.path.dirname(path), exist_ok=True)
    fb.save(path)


def verify(path, glyphs):
    """Render every glyph with FreeType mono (hinting on, as TMP RASTER_HINTED) at 16 and 32 px and
    compare with the source bitmap scaled by 1 and 2. Returns the (scale, char) pairs that differ."""
    bad = []
    for scale in (1, 2):
        font = ImageFont.truetype(path, 16 * scale)
        asc = font.getmetrics()[0]
        for cp, (px, _) in glyphs.items():
            if not px or cp == 0xAD:  # soft hyphen is default-ignorable: FreeType layout draws nothing
                continue
            mask, (ox, oy) = font.getmask2(chr(cp), mode="1")
            w, h = mask.size
            got = {(x + ox, asc - (y + oy) - 1) for y in range(h) for x in range(w) if mask.getpixel((x, y))}
            want = {(x * scale + i, y * scale + j) for x, y in px for i in range(scale) for j in range(scale)}
            if got != want:
                bad.append((scale, chr(cp)))
    return bad


def charset(glyphs):
    return "".join(chr(cp) for cp in sorted(glyphs) if cp >= 0x20)


# ----------------------------------------------------------------------------------------------
# sample sheets
# ----------------------------------------------------------------------------------------------

SAMPLES = [
    "BILLIARD ROGUE — Act 1 · Stage 2 — Mossy Ruins",
    "The quick brown fox jumps over the lazy dog.",
    "THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG!",
    "Épreuve réussie ! Œuvre, cœur, « À bientôt » — l’été…",
    "Ça va ? Où êtes-vous ? Noël, naïf, français, garçon, à côté.",
    "CHOISISSEZ UNE RÉCOMPENSE · À L’ATTAQUE ! ÉTÉ À ÎÎLE",
    "0123456789 x7 COMBO! +12 HP 30/30 Balls 6/8",
    "Tour 3 → PV 24/30 ♥ ★ CRIT! BLOCK POWER! « Continuer »",
]


def render_lines(path, size, lines, fg=(255, 244, 214), bg=(22, 28, 52), pad=8):
    font = ImageFont.truetype(path, size)
    asc, desc = font.getmetrics()
    lh = asc + desc
    width = max(int(font.getlength(t)) for t in lines) + 2 * pad
    img = Image.new("RGB", (width, lh * len(lines) + 2 * pad), bg)
    d = ImageDraw.Draw(img)
    d.fontmode = "1"
    for i, t in enumerate(lines):
        d.text((pad, pad + i * lh), t, font=font, fill=fg)
    return img


def charset_grid(path, glyphs, size=16, cols=32):
    chars = [chr(cp) for cp in sorted(glyphs) if cp > 0x20 and cp not in SPACES]
    font = ImageFont.truetype(path, size)
    cell = size + 4
    rows = (len(chars) + cols - 1) // cols
    img = Image.new("RGB", (cols * cell + 8, rows * (cell + 4) + 8), (22, 28, 52))
    d = ImageDraw.Draw(img)
    d.fontmode = "1"
    for i, ch in enumerate(chars):
        x, y = 4 + (i % cols) * cell, 4 + (i // cols) * (cell + 4)
        d.rectangle([x, y + 2, x + cell - 3, y + cell + 1], outline=(40, 48, 80))
        d.text((x + 2, y), ch, font=font, fill=(255, 244, 214))
    return img


def stack(images, gap=6, bg=(10, 12, 22)):
    w = max(i.width for i in images)
    h = sum(i.height for i in images) + gap * (len(images) - 1)
    out = Image.new("RGB", (w, h), bg)
    y = 0
    for im in images:
        out.paste(im, (0, y))
        y += im.height + gap
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview-dir", default=None)
    a = ap.parse_args()
    preview = a.preview_dir or os.path.join(os.environ.get("BR_PREVIEW_DIR", "/tmp/billiard_rogue_previews"), "art2d")
    os.makedirs(preview, exist_ok=True)
    regular_src = parse(os.path.join(HERE, "glyphs_regular.txt"))
    bold_src = parse(os.path.join(HERE, "glyphs_bold.txt"))
    for cp, g in regular_src.items():  # bold falls back to regular bitmaps for rare symbols
        bold_src.setdefault(cp, g)
    report = {}
    for style, src, fname in (("regular", regular_src, "BilliardPixel.ttf"), ("bold", bold_src, "BilliardPixel-Bold.ttf")):
        glyphs = derived(compose(src, ACCENTS[style]), ACCENTS[style], style)
        missing = [c for c in REQUIRED if ord(c) not in glyphs]
        if missing:
            sys.exit(f"{style}: missing glyphs {missing!r}")
        path = os.path.join(OUT_DIR, fname)
        build(style, glyphs, path)
        bad = verify(path, glyphs)
        if bad:
            sys.exit(f"{style}: FreeType render differs from bitmap for {bad[:20]}")
        report[style] = {"glyphs": len(glyphs) + 1, "bytes": os.path.getsize(path)}
        if style == "regular":
            with open(os.path.join(OUT_DIR, "BilliardPixel_charset.txt"), "w", encoding="utf-8") as f:
                f.write(charset(glyphs))
        sheets = [render_lines(path, s, SAMPLES) for s in (16, 32, 48)]
        stack(sheets).save(os.path.join(preview, f"font_{style}_samples.png"))
        grid = charset_grid(path, glyphs)
        grid.resize((grid.width * 2, grid.height * 2), Image.NEAREST).save(os.path.join(preview, f"font_{style}_charset.png"))
    print("FONTS", report)


if __name__ == "__main__":
    main()
