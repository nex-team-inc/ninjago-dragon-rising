"""Nearest-neighbour upscale for visual checks: python preview.py in.png out.png [scale] [bg]"""
import sys
from PIL import Image
src, dst = sys.argv[1], sys.argv[2]
scale = int(sys.argv[3]) if len(sys.argv) > 3 else 8
bg = sys.argv[4] if len(sys.argv) > 4 else "#3a3a44"
im = Image.open(src).convert("RGBA")
im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
out = Image.new("RGBA", im.size, bg)
out.alpha_composite(im)
out.convert("RGB").save(dst)
