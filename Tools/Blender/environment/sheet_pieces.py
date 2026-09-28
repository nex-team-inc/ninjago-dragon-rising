"""Montage the per-piece review renders into one labelled sheet.

    Tools/.venv/bin/python Tools/Blender/environment/sheet_pieces.py <pieces dir> <out.png>
"""
from PIL import Image, ImageDraw
import glob, os, sys
src, out = sys.argv[1], sys.argv[2]
fs = sorted(glob.glob(src + "/*.png"))
cols = 8; sz = 300
rows = (len(fs) + cols - 1) // cols
W = Image.new("RGB", (cols * sz, rows * (sz + 16)), (10, 10, 14))
d = ImageDraw.Draw(W)
for i, f in enumerate(fs):
    x, y = (i % cols) * sz, (i // cols) * (sz + 16)
    W.paste(Image.open(f).convert("RGB"), (x, y + 16))
    d.text((x + 4, y + 2), os.path.basename(f)[4:-4], fill=(255, 230, 180))
W.save(out)
