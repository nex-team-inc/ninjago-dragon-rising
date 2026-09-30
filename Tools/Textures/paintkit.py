"""Paint kit: Billiard Rogue pixel art painted the way a pixel artist paints it, in explicit passes.

Run:  Tools/.venv/bin/python Tools/Textures/paintkit.py <file.paint> [...] [--out-dir DIR] [--zoom N]
      Renders each painting, prints its lint report and writes <name>.png + <name>_process.png (draft | flats |
      shaded | final, and the current asset for comparison) into DIR (default $BR_PREVIEW_DIR/paint).
      --all lints every painting under Tools/Textures/paint/.

Why: the old sprites were computed from masks and maths (a Lambert term cut into bands, dilated outlines, noise and
dither), which reads as generated: pillow-shaded spheres, doubled outline corners, speckle, colours that differ by a
few units. A painting here is authored in the order a person works, and every pass is a separate, reviewable layer:

  1. draft    The sketch / line art. Clean 1 px lines placed by hand on a grid (`draft`) or as pixel-perfect strokes
              (`strokes`: line, curve, arc, ellipse, rect, dot). The draft decides the silhouette and every region.
  2. flats    Bucket fill. A key char dropped inside a closed region (`flats` grid or `seeds`) floods it,
              4-connected, stopping at the draft lines. Two different colours reaching the same region is an error
              (a gap in the line art), exactly the leak a painter would see.
  3. shade    Shadows, highlights and detail placed by hand in each material's own ramp: `ops` (band = the region
              worn back from one side, like a brush dragged along its edge; blob; poly; px) and `shade` grids
              (overlay: '.' keeps, '_' erases, a key paints).
  4. lines    Line colour (`ops`: lines K = sel-out, interior lines take the darker line colour of the region they
              sit in; edge K P dx dy = recolour the outline where it faces a direction, e.g. the lit top-left).
  5. cleanup  Lint: leaks, unseeded holes, doubled corners in the draft, stray single pixels, colour count.

Every pixel of the result is one of the painting's declared key colours, so the palette stays hand-picked.

.paint format (plain text; '#' starts a comment; indented lines belong to the section above them)
  size W H                    frame size in pixels (required, first)
  tile                        wrap-around: flood fill, bands and neighbours wrap (seamless textures)
  alpha soft                  key colours may have partial alpha (glows, shadows); default is hard alpha only
  symmetric x                 draw the left half only: the right half of the draft and flats is replaced by the
                              mirrored left half before filling (shading stays free, the light is not symmetric)
  key                         one colour per line:  C #rrggbb[aa] [line=L] [h=0.5] [description]
                                C = one char; line= (on a fill colour) the sel-out colour that interior ink
                                lines take where they border this region;
                                h= height 0..1 for surfaces (normal / cavity maps); '.' and '_' are reserved.
  draft [at X Y]              H rows of W chars: '.' empty, a key char = an ink pixel of that colour; with
                              'at X Y' a smaller grid placed with its top-left at (X, Y) (flats / shade too)
  strokes                     one stroke per line (integer pixel coordinates, x right, y down):
                                line C x0 y0 x1 y1 [x2 y2 ...]    pixel-perfect polyline
                                curve C x0 y0 c1x c1y c2x c2y x1 y1   cubic Bezier
                                arc C cx cy rx ry a0 a1           ellipse arc, degrees, 0 = +x, 90 = down
                                ellipse C cx cy rx ry             closed, mirror-symmetric ellipse; centre and radii
                                                                  are continuous (pixel x spans x..x+1): a 30 px
                                                                  ball on pixels 1..30 is 16 16 15 15
                                rect C x0 y0 x1 y1                rectangle outline (inclusive corners)
                                dot C x y [x y ...]
  flats                       H rows: '.' no seed, a key char = seed for the region under it
  seeds                       C x y [x y ...]                    one or more seeds of colour C
  ops                         painting commands, run in order after the fill:
                                band R P dx dy [n]     pixels of region R within n steps of its edge towards (dx,dy)
                                blob R P cx cy rx ry   filled ellipse clipped to region R ('*' = any painted pixel)
                                poly R P x y x y ...   filled polygon clipped to region R
                                px P x y [x y ...]     single pixels (glints, fixes)
                                recolor A B            every pixel of colour A becomes B
                                lines K                sel-out interior ink K (see key line=)
                                edge K P dx dy         ink K whose neighbour at (dx,dy) is transparent becomes P
  shade                       H rows overlay: '.' keep, '_' erase, key char = paint (several allowed, in order)
  frame                       starts the next frame of a horizontal strip (particles); sections after it belong to
                              that frame. `key`/`size`/`tile`/`alpha` before the first frame are shared.

Python: load(path) -> [Painting]; render(path) -> RGBA strip; painting.passes() -> {draft, flats, shaded, final};
override(out_dir_parts, name, img) returns the painted image when Tools/Textures/paint/<folder>/<name>.paint exists,
else img (the generators call it so a painting replaces the procedural sprite of the same name). Deterministic.
"""
import argparse
import os
import sys

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pixelkit as pk  # noqa: E402

PAINT_ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "paint")
EMPTY, ERASE = ".", "_"
N4 = ((1, 0), (-1, 0), (0, 1), (0, -1))
N8 = N4 + ((1, 1), (1, -1), (-1, 1), (-1, -1))


class PaintError(Exception):
    pass


# ----------------------------------------------------------------------------------------------
# Pixel-perfect rasterisation (the "pixel perfect" brush: no doubled corners in 1 px lines)
# ----------------------------------------------------------------------------------------------

def _bresenham(x0, y0, x1, y1):
    pts = []
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    while True:
        pts.append((x0, y0))
        if x0 == x1 and y0 == y1:
            return pts
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy


def _join(samples):
    """Rounded float samples -> an 8-connected integer path (gaps bridged, repeats dropped)."""
    out = []
    for x, y in samples:
        p = (int(np.floor(x + 0.5)), int(np.floor(y + 0.5)))
        if not out:
            out.append(p)
        elif p != out[-1]:
            if max(abs(p[0] - out[-1][0]), abs(p[1] - out[-1][1])) > 1:
                out += _bresenham(*out[-1], *p)[1:]
            else:
                out.append(p)
    return out


def pixel_perfect(path, closed=False):
    """Drops the corner pixel of every L (two orthogonal steps that should be one diagonal step)."""
    pts = list(path)
    if len(pts) < 3:
        return pts

    def is_l(a, b, c):
        return (a[0] == b[0] or a[1] == b[1]) and (b[0] == c[0] or b[1] == c[1]) and \
            abs(a[0] - c[0]) == 1 and abs(a[1] - c[1]) == 1
    out = [pts[0]]
    for i in range(1, len(pts) - 1):
        if is_l(out[-1], pts[i], pts[i + 1]):
            continue
        out.append(pts[i])
    out.append(pts[-1])
    if closed and len(out) > 3:
        if is_l(out[-2], out[-1], out[0]):
            out.pop()
        if len(out) > 3 and is_l(out[-1], out[0], out[1]):
            out.pop(0)
    return out


def ellipse_outline(cx, cy, rx, ry, a0=None, a1=None):
    """Pixels of a 1 px ellipse outline, mirror-symmetric like a hand-drawn one. Continuous coordinates: pixel x
    covers x..x+1, so a 30 px ball on pixels 1..30 is (16, 16, 15, 15). The outline is the disc's inner boundary
    (disc pixels with a 4-neighbour outside), which is always a clean 8-connected line. a0/a1 (degrees, 0 = +x,
    90 = down) keep only an arc."""
    x0, x1 = int(np.floor(cx - rx - 1)), int(np.ceil(cx + rx + 1))
    y0, y1 = int(np.floor(cy - ry - 1)), int(np.ceil(cy + ry + 1))
    ys, xs = np.mgrid[y0:y1 + 1, x0:x1 + 1]
    inside = ((xs + 0.5 - cx) / rx) ** 2 + ((ys + 0.5 - cy) / ry) ** 2 <= 1.0
    pad = np.pad(inside, 1)
    edge = inside & ~(pad[:-2, 1:-1] & pad[2:, 1:-1] & pad[1:-1, :-2] & pad[1:-1, 2:])
    pts = list(zip(xs[edge].tolist(), ys[edge].tolist()))
    if a0 is not None:
        lo, hi = sorted((a0, a1))
        def ang(p):
            a = np.degrees(np.arctan2(p[1] + 0.5 - cy, p[0] + 0.5 - cx)) % 360
            return any(lo <= a + k <= hi for k in (-360, 0, 360))
        pts = [p for p in pts if ang(p)]
    return pts


def stroke_points(kind, args):
    """Integer pixel path of one stroke (before pixel-perfect cleanup) and whether it is closed."""
    f = [float(v) for v in args]
    if kind == "line":
        if len(f) < 4 or len(f) % 2:
            raise PaintError("line needs x0 y0 x1 y1 [...]")
        pts = []
        for i in range(0, len(f) - 2, 2):
            seg = _bresenham(int(f[i]), int(f[i + 1]), int(f[i + 2]), int(f[i + 3]))
            pts += seg if not pts else seg[1:]
        return pts, None  # Bresenham segments are clean; joints stay as drawn
    if kind == "curve":
        if len(f) != 8:
            raise PaintError("curve needs x0 y0 c1x c1y c2x c2y x1 y1")
        p = np.array(f, np.float64).reshape(4, 2)
        length = sum(np.linalg.norm(p[i + 1] - p[i]) for i in range(3))
        t = np.linspace(0, 1, max(8, int(length * 4)))[:, None]
        s = ((1 - t) ** 3) * p[0] + 3 * ((1 - t) ** 2) * t * p[1] + 3 * (1 - t) * t * t * p[2] + t ** 3 * p[3]
        return _join(s), False
    if kind in ("arc", "ellipse"):
        if kind == "arc" and len(f) != 6:
            raise PaintError("arc needs cx cy rx ry a0 a1")
        if kind == "ellipse" and len(f) != 4:
            raise PaintError("ellipse needs cx cy rx ry")
        return ellipse_outline(*f[:4], *(f[4:6] if kind == "arc" else (None, None))), None
    if kind == "rect":
        if len(f) != 4:
            raise PaintError("rect needs x0 y0 x1 y1")
        x0, y0, x1, y1 = (int(v) for v in f)
        pts = _bresenham(x0, y0, x1, y0) + _bresenham(x1, y0, x1, y1)[1:] + \
            _bresenham(x1, y1, x0, y1)[1:] + _bresenham(x0, y1, x0, y0)[1:-1]
        return pts, None  # corners stay square
    if kind == "dot":
        if len(f) < 2 or len(f) % 2:
            raise PaintError("dot needs x y [x y ...]")
        return [(int(f[i]), int(f[i + 1])) for i in range(0, len(f), 2)], None
    raise PaintError(f"unknown stroke '{kind}'")


# ----------------------------------------------------------------------------------------------
# Painting
# ----------------------------------------------------------------------------------------------

def _parse_color(tok):
    h = tok.lstrip("#")
    if len(h) not in (6, 8):
        raise PaintError(f"bad colour '{tok}'")
    rgb = tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))
    return rgb + ((int(h[6:8], 16),) if len(h) == 8 else (255,))


class Painting:
    def __init__(self, w, h, keys=None, tile=False, soft_alpha=False, name="", source=""):
        self.w, self.h, self.tile, self.soft_alpha = w, h, tile, soft_alpha
        self.name, self.source = name, source
        self.keys = dict(keys or {})  # char -> {"rgba", "line", "h", "desc"}
        self.ink = np.full((h, w), EMPTY, "<U1")
        self.seed = np.full((h, w), EMPTY, "<U1")
        self.symmetric = None
        self.paint_steps = []  # ("ops", [(lineno, tokens)]) | ("shade", rows, lineno)
        self.warnings, self.errors = [], []
        self._passes = None

    # -- authoring API (the .paint parser uses the same calls) ---------------------------------
    def key(self, char, color, line=None, h=None, desc=""):
        if len(char) != 1 or char in (EMPTY, ERASE, " "):
            raise PaintError(f"bad key char '{char}'")
        self.keys[char] = {"rgba": _parse_color(color) if isinstance(color, str) else tuple(color),
                           "line": line, "h": h, "desc": desc}

    def draft_rows(self, rows, where="", at=(0, 0)):
        for x, y, c in self._grid(rows, where, at):
            if c != EMPTY:
                self._need_key(c, where)
                self.ink[y, x] = c

    def stroke(self, kind, char, args, where=""):
        self._need_key(char, where)
        pts, closed = stroke_points(kind, args)
        if closed is not None:
            pts = pixel_perfect(pts, closed=closed)
        for x, y in pts:
            if self.tile:
                self.ink[y % self.h, x % self.w] = char
            elif 0 <= x < self.w and 0 <= y < self.h:
                self.ink[y, x] = char

    def flats_rows(self, rows, where="", at=(0, 0)):
        for x, y, c in self._grid(rows, where, at):
            if c != EMPTY:
                self._need_key(c, where)
                self.seed[y, x] = c

    def seeds(self, char, coords, where=""):
        self._need_key(char, where)
        for i in range(0, len(coords), 2):
            x, y = int(coords[i]), int(coords[i + 1])
            if not (0 <= x < self.w and 0 <= y < self.h):
                raise PaintError(f"{where}: seed ({x},{y}) outside the canvas")
            self.seed[y, x] = char

    def ops(self, lines):
        self.paint_steps.append(("ops", lines))

    def shade_rows(self, rows, where="", at=(0, 0)):
        cells = self._grid(rows, where, at)
        for _, _, c in cells:
            if c not in (EMPTY, ERASE):
                self._need_key(c, where)
        self.paint_steps.append(("shade", cells, where))

    # -- helpers ---------------------------------------------------------------------------------
    def _grid(self, rows, where, at=(0, 0)):
        """(x, y, char) cells of a grid placed with its top-left at `at`; a full-canvas grid by default."""
        ax, ay = at
        full = at == (0, 0) and (len(rows) == self.h or not rows)
        if full and len(rows) != self.h:
            raise PaintError(f"{where}: {len(rows)} rows, expected {self.h}")
        width = self.w if full else (len(rows[0]) if rows else 0)
        cells = []
        for i, r in enumerate(rows):
            if len(r) != width:
                raise PaintError(f"{where}: row {i} has {len(r)} chars, expected {width}: '{r}'")
            for j, c in enumerate(r):
                x, y = ax + j, ay + i
                if self.tile:
                    x, y = x % self.w, y % self.h
                elif not (0 <= x < self.w and 0 <= y < self.h):
                    if c != EMPTY:
                        raise PaintError(f"{where}: '{c}' at ({x},{y}) is outside the canvas")
                    continue
                cells.append((x, y, c))
        return cells

    def _need_key(self, c, where):
        if c not in self.keys:
            raise PaintError(f"{where}: char '{c}' is not in the key")

    def _nb(self, mask, dx, dy, fill=False):
        """mask shifted so out[y, x] = mask[y + dy, x + dx] (wraps when tiled; outside = fill)."""
        if self.tile:
            return np.roll(np.roll(mask, -dy, axis=0), -dx, axis=1)
        out = np.full_like(mask, fill)
        h, w = mask.shape
        ys = slice(max(0, dy), h + min(0, dy))
        yd = slice(max(0, -dy), h + min(0, -dy))
        xs = slice(max(0, dx), w + min(0, dx))
        xd = slice(max(0, -dx), w + min(0, -dx))
        out[yd, xd] = mask[ys, xs]
        return out

    # -- passes ------------------------------------------------------------------------------------
    def _mirror(self):
        if self.symmetric == "x":
            half = self.w // 2
            for layer in (self.ink, self.seed):
                layer[:, self.w - half:] = layer[:, :half][:, ::-1]
        elif self.symmetric == "y":
            half = self.h // 2
            for layer in (self.ink, self.seed):
                layer[self.h - half:, :] = layer[:half, :][::-1, :]

    def _fill(self):
        """Bucket fill: every seed floods its 4-connected region of non-ink pixels."""
        free = self.ink == EMPTY
        structure = ndimage.generate_binary_structure(2, 1)
        labels, n = ndimage.label(free, structure=structure)
        if self.tile and n:
            # merge labels that touch across the wrap
            parent = list(range(n + 1))

            def find(a):
                while parent[a] != a:
                    parent[a] = parent[parent[a]]
                    a = parent[a]
                return a
            for a_row, b_row in ((labels[0, :], labels[-1, :]), (labels[:, 0], labels[:, -1])):
                for a, b in zip(a_row, b_row):
                    if a and b:
                        ra, rb = find(a), find(b)
                        if ra != rb:
                            parent[ra] = rb
            labels = np.vectorize(lambda v: find(v) if v else 0)(labels)
        region = np.full((self.h, self.w), EMPTY, "<U1")
        owner = {}
        for y, x in zip(*np.nonzero(self.seed != EMPTY)):
            c = self.seed[y, x]
            lab = labels[y, x]
            if lab == 0:
                self.warnings.append(f"seed '{c}' at ({x},{y}) sits on a line pixel; ignored")
                continue
            if lab in owner and owner[lab][0] != c:
                ox, oy = owner[lab][1]
                self.errors.append(f"leak: seeds '{owner[lab][0]}' at ({ox},{oy}) and '{c}' at ({x},{y}) share one "
                                   f"region (gap in the draft)")
                continue
            owner[lab] = (c, (x, y))
        for lab, (c, _) in owner.items():
            region[labels == lab] = c
        # enclosed regions nobody filled (not touching the canvas border) = holes; worth a look
        if not self.tile:
            border = set(np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))) - {0}
            for lab in range(1, labels.max() + 1):
                if lab not in owner and lab not in border:
                    ys, xs = np.nonzero(labels == lab)
                    if len(ys):
                        self.warnings.append(f"unfilled enclosed region of {len(ys)} px near ({xs[0]},{ys[0]})")
        return region

    def _draft_doubles(self):
        """Doubled corners in 1 px draft lines: a line pixel whose only two neighbours are one horizontal and one
        vertical step away (an L that should be a single diagonal step). A deliberate square corner, where both
        arms carry on straight for another pixel, is not a double."""
        ink = self.ink != EMPTY
        hits = []
        for y, x in zip(*np.nonzero(ink)):
            nbs = [(dx, dy) for dx, dy in N8 if self._at(ink, x + dx, y + dy)]
            if len(nbs) != 2:
                continue
            (ax, ay), (bx, by) = nbs
            if abs(ax) + abs(ay) != 1 or abs(bx) + abs(by) != 1 or ax * bx + ay * by != 0:
                continue
            if self._at(ink, x + 2 * ax, y + 2 * ay) and self._at(ink, x + 2 * bx, y + 2 * by):
                continue
            hits.append((int(x), int(y)))
        return hits

    def _at(self, mask, x, y):
        if self.tile:
            return bool(mask[y % self.h, x % self.w])
        return 0 <= x < self.w and 0 <= y < self.h and bool(mask[y, x])

    def _run_ops(self, paint, region, is_ink, lines):
        for lineno, tok in lines:
            where = f"{self.source}:{lineno}"
            cmd, a = tok[0], tok[1:]
            if cmd in ("lines", "edge") and self._shaded is None:
                self._shaded = paint.copy()
            try:
                if cmd == "band":
                    r, p, dx, dy = a[0], a[1], int(a[2]), int(a[3])
                    n = int(a[4]) if len(a) > 4 else 1
                    self._need_key(p, where)
                    inside = self._region_mask(r, region, is_ink, paint, where)
                    keep = inside.copy()
                    for k in range(1, n + 1):
                        keep &= self._nb(inside, dx * k, dy * k, fill=False)
                    paint[inside & ~keep] = p
                elif cmd == "blob":
                    r, p = a[0], a[1]
                    cx, cy, rx, ry = (float(v) for v in a[2:6])
                    self._need_key(p, where)
                    m = pk.ellipse(self.w, self.h, cx + 0.5, cy + 0.5, rx, ry)
                    paint[m & self._region_mask(r, region, is_ink, paint, where)] = p
                elif cmd == "poly":
                    r, p = a[0], a[1]
                    pts = [(float(a[i]), float(a[i + 1])) for i in range(2, len(a), 2)]
                    self._need_key(p, where)
                    im = Image.new("L", (self.w, self.h), 0)
                    ImageDraw.Draw(im).polygon(pts, fill=255, outline=255)
                    m = np.asarray(im) > 0
                    paint[m & self._region_mask(r, region, is_ink, paint, where)] = p
                elif cmd == "px":
                    p = a[0]
                    if p != ERASE:
                        self._need_key(p, where)
                    for i in range(1, len(a), 2):
                        x, y = int(a[i]), int(a[i + 1])
                        if self.tile:
                            x, y = x % self.w, y % self.h
                        if 0 <= x < self.w and 0 <= y < self.h:
                            paint[y, x] = EMPTY if p == ERASE else p
                elif cmd == "recolor":
                    self._need_key(a[1], where)
                    paint[paint == a[0]] = a[1]
                elif cmd == "lines":
                    self._selout(paint, region, is_ink, a[0], where)
                elif cmd == "edge":
                    k, p, dx, dy = a[0], a[1], int(a[2]), int(a[3])
                    self._need_key(p, where)
                    transparent = paint == EMPTY
                    m = is_ink & (paint == k) & self._nb(transparent, dx, dy, fill=True)
                    paint[m] = p
                else:
                    raise PaintError(f"unknown op '{cmd}'")
            except (IndexError, ValueError) as e:
                raise PaintError(f"{where}: bad arguments for '{cmd}': {e}")

    def _region_mask(self, r, region, is_ink, paint, where):
        if r == "*":
            return paint != EMPTY
        if r.startswith("="):  # "=C": every pixel currently painted C
            return paint == r[1:]
        self._need_key(r, where)
        return (region == r) & ~is_ink

    def _selout(self, paint, region, is_ink, k, where):
        transparent = paint == EMPTY
        exterior = np.zeros_like(transparent)
        for dx, dy in N8:
            exterior |= self._nb(transparent, dx, dy, fill=True)
        target = is_ink & (paint == k) & ~exterior
        out = paint.copy()
        for y, x in zip(*np.nonzero(target)):
            best, best_l = None, 1e9
            for dx, dy in N4:
                nx, ny = x + dx, y + dy
                if self.tile:
                    nx, ny = nx % self.w, ny % self.h
                if not (0 <= nx < self.w and 0 <= ny < self.h) or is_ink[ny, nx]:
                    continue
                reg = region[ny, nx]
                line = self.keys.get(reg, {}).get("line") if reg != EMPTY else None
                if line:
                    self._need_key(line, where)
                    r_, g_, b_ = self.keys[line]["rgba"][:3]
                    lum = 0.299 * r_ + 0.587 * g_ + 0.114 * b_
                    if lum < best_l:
                        best, best_l = line, lum
            if best:
                out[y, x] = best
        paint[:] = out

    def _to_rgba(self, paint):
        img = np.zeros((self.h, self.w, 4), np.uint8)
        for c, k in self.keys.items():
            img[paint == c] = k["rgba"]
        return img

    def passes(self):
        """{draft, flats, shaded, final} RGBA arrays + 'keys' (final key chars) and lint lists."""
        if self._passes is not None:
            return self._passes
        self._mirror()
        draft = self.ink.copy()
        doubles = self._draft_doubles()
        region = self._fill()
        is_ink = self.ink != EMPTY
        paint = np.where(is_ink, self.ink, region)
        flats = paint.copy()
        self._shaded = None
        for step in self.paint_steps:
            if step[0] == "ops":
                self._run_ops(paint, region, is_ink, step[1])
            else:
                for x, y, c in step[1]:
                    if c == ERASE:
                        paint[y, x] = EMPTY
                    elif c != EMPTY:
                        paint[y, x] = c
        shaded = self._shaded if self._shaded is not None else paint.copy()
        if doubles:
            self.warnings.append(f"{len(doubles)} doubled corner(s) in the draft lines at "
                                 + " ".join(f"({x},{y})" for x, y in doubles[:8]) + (" ..." if len(doubles) > 8 else ""))
        final = self._to_rgba(paint)
        if not self.soft_alpha and ((final[..., 3] > 0) & (final[..., 3] < 255)).any():
            self.errors.append("partial alpha in a hard-alpha painting (add 'alpha soft' if intended)")
        self._passes = {"draft": self._to_rgba(np.where(draft != EMPTY, draft, EMPTY)),
                        "flats": self._to_rgba(flats), "shaded": self._to_rgba(shaded), "final": final,
                        "keys": paint}
        return self._passes

    def height(self, default=0.5):
        """Per-pixel height from the key h= values of the final pixels (surfaces)."""
        keys = self.passes()["keys"]
        out = np.full((self.h, self.w), default, np.float32)
        for c, k in self.keys.items():
            if k["h"] is not None:
                out[keys == c] = k["h"]
        return out


# ----------------------------------------------------------------------------------------------
# .paint parser
# ----------------------------------------------------------------------------------------------

SECTIONS = ("key", "draft", "strokes", "flats", "seeds", "ops", "shade")


def _key_entry(tok, where):
    if len(tok) < 2:
        raise PaintError(f"{where}: key line needs 'C #rrggbb'")
    if len(tok[0]) != 1 or tok[0] in (EMPTY, ERASE, "#"):
        raise PaintError(f"{where}: bad key char '{tok[0]}' ('.', '_' and '#' are reserved)")
    entry = {"rgba": _parse_color(tok[1]), "line": None, "h": None, "desc": ""}
    desc = []
    for t in tok[2:]:
        if t.startswith("line="):
            entry["line"] = t[5:]
        elif t.startswith("h="):
            entry["h"] = float(t[2:])
        else:
            desc.append(t)
    entry["desc"] = " ".join(desc)
    return tok[0], entry


def parse(text, source="<paint>"):
    """Returns a list of Paintings (one per frame). '#' starts a comment (in key lines: whitespace + '# ')."""
    header = {"size": None, "tile": False, "soft": False, "symmetric": None}
    shared_keys = {}
    frames = []   # each: list of [section, lineno, body]
    section = None
    for i, raw in enumerate(text.splitlines(), 1):
        if not raw.strip() or raw.strip().startswith("#"):
            continue
        if raw[:1] in (" ", "\t"):
            if section is None:
                raise PaintError(f"{source}:{i}: indented line outside a section")
            content = raw.strip()
            if section[0] == "key":  # colours are '#rrggbb'; a comment is ' # ...'
                idx = content.find(" # ")
                content = content[:idx].rstrip() if idx >= 0 else content
            else:
                content = content.split("#", 1)[0].rstrip()
            if content:
                section[2].append((i, content))
            continue
        tok = raw.split("#", 1)[0].split()
        if not tok:
            continue
        head, section = tok[0], None
        if head in ("size", "tile", "alpha", "symmetric"):
            if frames:
                raise PaintError(f"{source}:{i}: '{head}' must come before the first frame / section")
            if head == "size":
                header["size"] = (int(tok[1]), int(tok[2]))
            elif head == "tile":
                header["tile"] = True
            elif head == "alpha":
                header["soft"] = len(tok) > 1 and tok[1] == "soft"
            else:
                header["symmetric"] = tok[1]
        elif head == "frame":
            frames.append([])
        elif head in SECTIONS:
            at = (0, 0)
            if len(tok) >= 4 and tok[1] == "at" and head in ("draft", "flats", "shade"):
                at = (int(tok[2]), int(tok[3]))
            elif len(tok) > 1:
                raise PaintError(f"{source}:{i}: '{head}' takes no arguments (grids: '{head} at X Y')")
            section = [head, i, [], at]
            if head == "key" and not frames:  # keys before the first frame are shared by every frame
                shared_keys.setdefault("_blocks", []).append(section)
            else:
                if not frames:
                    frames.append([])
                frames[-1].append(section)
        else:
            raise PaintError(f"{source}:{i}: unknown directive '{head}'")
    if header["size"] is None:
        raise PaintError(f"{source}: missing 'size W H'")
    keys = {}
    for blk in shared_keys.get("_blocks", []):
        for n, content in blk[2]:
            c, e = _key_entry(content.split(), f"{source}:{n}")
            keys[c] = e
    if not frames:
        frames.append([])
    out = []
    w, h = header["size"]
    for blocks in frames:
        p = Painting(w, h, keys={k: dict(v) for k, v in keys.items()}, tile=header["tile"],
                     soft_alpha=header["soft"], name=os.path.splitext(os.path.basename(source))[0], source=source)
        p.symmetric = header["symmetric"]
        for sec, ln, body, at in blocks:
            where = f"{source}:{ln}"
            rows = [c for _, c in body]
            if sec == "key":
                for n, content in body:
                    c, e = _key_entry(content.split(), f"{source}:{n}")
                    p.keys[c] = e
            elif sec == "draft":
                p.draft_rows(rows, where, at)
            elif sec == "flats":
                p.flats_rows(rows, where, at)
            elif sec == "shade":
                p.shade_rows(rows, where, at)
            elif sec == "strokes":
                for n, content in body:
                    t = content.split()
                    if len(t) < 3:
                        raise PaintError(f"{source}:{n}: stroke needs 'kind C args'")
                    try:
                        p.stroke(t[0], t[1], t[2:], f"{source}:{n}")
                    except PaintError as e:
                        raise PaintError(f"{source}:{n}: {e}")
            elif sec == "seeds":
                for n, content in body:
                    t = content.split()
                    p.seeds(t[0], t[1:], f"{source}:{n}")
            elif sec == "ops":
                p.ops([(n, content.split()) for n, content in body])
        out.append(p)
    return out


def load(path):
    with open(path) as f:
        return parse(f.read(), source=os.path.relpath(path, pk.REPO) if os.path.isabs(path) else path)


def strip(frames, key="final"):
    return np.concatenate([fr.passes()[key] for fr in frames], axis=1)


def render(path):
    frames = load(path)
    img = strip(frames)
    problems = [e for fr in frames for e in fr.errors]
    if problems:
        raise PaintError(f"{path}: " + "; ".join(problems))
    return img


def paint_path(folder, name):
    return os.path.join(PAINT_ROOT, folder, name + ".paint")


def override(out_dir_parts, name, img):
    """The painted sprite when Tools/Textures/paint/<last out folder>/<name>.paint exists, else img unchanged.
    The painting must keep the procedural sprite's size (9-slice borders, pivots and .meta stay valid)."""
    path = paint_path(out_dir_parts[-1], name)
    if not os.path.exists(path):
        return img
    painted = render(path)
    if img is not None and painted.shape != img.shape:
        raise PaintError(f"{path}: painted {painted.shape[1]}x{painted.shape[0]} but the sprite is "
                         f"{img.shape[1]}x{img.shape[0]}; keep the size")
    return painted


# ----------------------------------------------------------------------------------------------
# Lint of finished PNGs (the artefact report; also used for before / after tables)
# ----------------------------------------------------------------------------------------------

def _lab(rgb):
    c = np.asarray(rgb, np.float64) / 255.0
    c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = c @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def lint_image(img, tile=False, frames=1):
    """Artefact metrics of an RGBA sprite (per frame for strips, summed)."""
    img = np.asarray(img)
    if img.ndim == 2:
        img = np.dstack([img, img, img, np.full(img.shape, 255, np.uint8)])
    if img.shape[2] == 3:
        img = np.dstack([img, np.full(img.shape[:2], 255, np.uint8)])
    fw = img.shape[1] // frames
    total = {"colors": 0, "near_dupes": 0, "stray": 0, "noise": 0.0, "soft_alpha": 0, "outline_doubles": 0,
             "opaque": 0}
    all_cols = set()
    for f in range(frames):
        fr = img[:, f * fw:(f + 1) * fw]
        a = fr[..., 3]
        opaque = a > 0
        n_op = int(opaque.sum())
        total["opaque"] += n_op
        total["soft_alpha"] += int(((a > 0) & (a < 255)).sum())
        key = (fr[..., 0].astype(np.int64) << 24) | (fr[..., 1].astype(np.int64) << 16) | \
              (fr[..., 2].astype(np.int64) << 8) | a.astype(np.int64)
        key = np.where(opaque, key, -1)
        cols = set(np.unique(key[opaque]).tolist())
        all_cols |= cols

        def nb(arr, dx, dy, fill):
            if tile:
                return np.roll(np.roll(arr, -dy, 0), -dx, 1)
            p = np.pad(arr, 1, constant_values=fill)
            return p[1 + dy:1 + dy + arr.shape[0], 1 + dx:1 + dx + arr.shape[1]]
        same8 = np.zeros(key.shape, np.int32)
        diff4 = np.zeros(key.shape, np.int32)
        for dx, dy in N8:
            s = nb(key, dx, dy, -2) == key
            same8 += s
            if (dx, dy) in N4:
                diff4 += (nb(key, dx, dy, -2) != key) & (nb(key, dx, dy, -2) >= 0)
        # stray: a pixel that matches none of its 8 neighbours while most neighbours share one other colour
        stray = opaque & (same8 == 0)
        if stray.any():
            ys, xs = np.nonzero(stray)
            cnt = 0
            for y, x in zip(ys, xs):
                vals = [nb_v for nb_v in (key[(y + dy) % key.shape[0], (x + dx) % key.shape[1]] if tile else
                                          (key[y + dy, x + dx] if 0 <= y + dy < key.shape[0] and 0 <= x + dx < key.shape[1] else -2)
                                          for dx, dy in N8) if nb_v >= 0]
                if len(vals) >= 6 and max(vals.count(v) for v in set(vals)) >= 5:
                    cnt += 1
            total["stray"] += cnt
        total["noise"] += float(((diff4 >= 3) & opaque).sum())
        # doubled outline corners: outline-coloured pixels touching transparency only diagonally
        transparent = ~opaque
        t4 = np.zeros_like(opaque)
        t8 = np.zeros_like(opaque)
        for dx, dy in N8:
            t = nb(transparent, dx, dy, True)
            t8 |= t
            if (dx, dy) in N4:
                t4 |= t
        edge = opaque & t4
        if edge.any():
            vals, counts = np.unique(key[edge], return_counts=True)
            outline = vals[np.argmax(counts)]
            total["outline_doubles"] += int((opaque & ~t4 & t8 & (key == outline)).sum())
    cols = sorted(all_cols)
    rgb = np.array([[(c >> 24) & 255, (c >> 16) & 255, (c >> 8) & 255] for c in cols], np.float64)
    total["colors"] = len(cols)
    if len(cols) > 1:
        lab = _lab(rgb)
        d = np.sqrt(((lab[:, None, :] - lab[None, :, :]) ** 2).sum(-1))
        iu = np.triu_indices(len(cols), 1)
        total["near_dupes"] = int((d[iu] < 5.0).sum())
    total["noise"] = round(total["noise"] / max(1, total["opaque"]), 3)
    return total


def lint_file(path, frames=1, tile=False):
    with Image.open(path) as im:
        return lint_image(np.asarray(im.convert("RGBA")), tile=tile, frames=frames)


# ----------------------------------------------------------------------------------------------
# Previews
# ----------------------------------------------------------------------------------------------

def current_asset(folder, name):
    """The committed sprite this painting replaces (for the process sheet), or None."""
    roots = [os.path.join(pk.REPO, "Starter", "Assets", "Sprites", "BilliardRogue", folder),
             os.path.join(pk.REPO, "Starter", "Assets", "Textures", "BilliardRogue", folder)]
    for r in roots:
        for suffix in (".png", "_Albedo.png"):
            p = os.path.join(r, name + suffix)
            if os.path.exists(p):
                with Image.open(p) as im:
                    return np.asarray(im.convert("RGBA"))
    return None


PAPER = (232, 226, 212)
CONTEXT_BG = (("navy panel", (23, 34, 70)), ("parchment card", (242, 226, 180)), ("crypt floor", (70, 66, 84)),
              ("grass", (86, 150, 60)))


def _on(img, bg):
    return pk.on_background(img, bg=bg)[..., :3]


def process_sheet(frames, before=None, zoom=6):
    """draft (on paper, like a pencil sketch) | flats | shaded | final | current asset, then the final at the
    game's 3x UI scale over the backgrounds it sits on."""
    draft = strip(frames, "draft")
    items = [("1 draft", np.dstack([_on(draft, PAPER), np.full(draft.shape[:2], 255, np.uint8)])),
             ("2 flats", strip(frames, "flats")), ("3 shaded", strip(frames, "shaded")),
             ("4 final", strip(frames, "final"))]
    if before is not None:
        items.append(("before (current asset)", before))
    wide = items[0][1].shape[1] > 160
    cols = 1 if wide else len(items)
    if wide:
        zoom = max(1, min(zoom, 1200 // items[0][1].shape[1]))
    top = pk.contact_sheet(items, scale=zoom, cols=cols)
    final = strip(frames, "final")
    ctx = [(f"3x on {n}", _on(final, bg)) for n, bg in CONTEXT_BG]
    ctx = [(n, np.dstack([c, np.full(c.shape[:2], 255, np.uint8)])) for n, c in ctx]
    bottom = pk.contact_sheet(ctx, scale=3, cols=1 if wide else len(ctx), checker_bg=False)
    w = max(top.shape[1], bottom.shape[1])
    pad = lambda a: np.pad(a, ((0, 0), (0, w - a.shape[1]), (0, 0)), constant_values=28)
    return np.concatenate([pad(top), pad(bottom)], axis=0)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("files", nargs="*")
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--out-dir", default=None)
    ap.add_argument("--zoom", type=int, default=6)
    a = ap.parse_args()
    files = list(a.files)
    if a.all:
        for root, _, names in os.walk(PAINT_ROOT):
            files += [os.path.join(root, n) for n in sorted(names) if n.endswith(".paint")]
    out = a.out_dir or pk.default_preview_dir("paint")
    os.makedirs(out, exist_ok=True)
    bad = 0
    for path in files:
        name = os.path.splitext(os.path.basename(path))[0]
        folder = os.path.basename(os.path.dirname(os.path.abspath(path)))
        try:
            frames = load(path)
            img = strip(frames)
        except PaintError as e:
            print(f"ERROR {e}")
            bad += 1
            continue
        errs = [e for fr in frames for e in fr.errors]
        warns = [w for fr in frames for w in fr.warnings]
        pk.save_rgba(os.path.join(out, name + ".png"), img)
        pk.save_rgb(os.path.join(out, name + "_process.png"),
                    process_sheet(frames, current_asset(folder, name), a.zoom))
        m = lint_image(img, tile=frames[0].tile, frames=len(frames))
        status = "FAIL" if errs else "ok"
        bad += bool(errs)
        print(f"{status:4} {name:28} {img.shape[1]}x{img.shape[0]} frames={len(frames)} {m}")
        for e in errs:
            print(f"     error: {e}")
        for w in warns:
            print(f"     warn:  {w}")
    print("PAINT", {"files": len(files), "failed": bad, "out": out})
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
