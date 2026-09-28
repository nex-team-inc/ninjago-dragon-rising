"""In-game look emulation for the enemy previews (review only - nothing here is a game asset).

The old previews shaded with a Blender toon ramp whose light never exceeded the tint (x1.0), no rim, bloom from the
emissive half only and no grading, so they looked richer and darker than the game. This module re-shades raw
Blender data passes (albedo, emission map, sun light x shadow, world normal) with the formulas the game uses:

  * ToonLit (Starter/Assets/Shaders/BilliardRogue/ToonLitForwardPass.hlsl), gamma colour space:
      ramp     = ToonRamp(N.L x shadowAtt)                      (4 bands, softness 0.02)
      lighting = sunColor x sunIntensity x lerp(ShadowTint, 1, ramp) + trilight SH x AmbientStrength
                 (+ TORCH_FILL x additional light tint x intensity: an average torch pool, not per-light)
      colour   = albedo x lighting + RimColor x RimA x (1 - N.V)^RimPower x max(ramp, 0.2) + emission x 2.2
    with the M_Palette constants of MaterialsBuilder.BuildPalette and the act lighting of Configs/.../Act_<n>.asset.
  * URP post of Settings/.../Volume_Act<n>.asset: bloom (threshold / knee / intensity / tint), white balance (LMS),
    contrast (LogC), split toning, saturation, Neutral tonemapping.
Per-light torch pools, fog, vignette, tilt-shift and cavity maps are not emulated. The values are read (never
written) from the Unity project, so the previews follow the lighting owner's tuning.
"""
import math
import os
import re

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
ASSETS = os.path.join(ROOT, "Starter", "Assets")

# MaterialsBuilder.BuildPalette (M_Palette) - the enemy / floor material constants
PALETTE_MAT = dict(shadow_tint=(0.42, 0.4, 0.62), ambient_strength=0.35, rim=(1.0, 0.95, 0.85), rim_a=0.35,
                   rim_power=4.0, bands=4.0, softness=0.02, emission=2.2)
TORCH_FILL = 0.1


# ------------------------------------------------------------------ read the Unity assets (read-only)
def _vec(text):
    return tuple(float(v) for v in re.findall(r"[a-z]: ([-0-9.eE]+)", text))


def act_lighting(n):
    path = os.path.join(ASSETS, "Configs", "BilliardRogue", "Acts", f"Act_{n}.asset")
    txt = open(path).read()
    body = txt[txt.index("  lighting:"):]
    get = lambda key: re.search(rf"^    {key}: (.*)$", body, re.M).group(1)  # noqa: E731
    return dict(sun=_vec(get("sunColor"))[:3], intensity=float(get("sunIntensity")), euler=_vec(get("sunEuler")),
                shadow_strength=float(get("shadowStrength")), sky=_vec(get("ambientSky"))[:3],
                equator=_vec(get("ambientEquator"))[:3], ground=_vec(get("ambientGround"))[:3],
                add=tuple(c * float(get("additionalLightIntensity")) for c in _vec(get("additionalLightTint"))[:3]))


def volume(n):
    """{component: {param: value}} for the overridden params of Volume_Act<n>."""
    path = os.path.join(ASSETS, "Settings", "BilliardRogue", "Volumes", f"Volume_Act{n}.asset")
    out = {}
    for doc in open(path).read().split("--- !u!"):
        m = re.search(r"^  m_Name: (\w+)$", doc, re.M)
        if not m:
            continue
        params = {}
        for key, state, value in re.findall(r"^  (\w+):\n    m_OverrideState: (\d)\n    m_Value: (.*)$", doc, re.M):
            if state == "1":
                v = value.strip()
                params[key] = _vec(v) if v.startswith("{") else float(v) if re.match(r"^[-0-9.eE]+$", v) else v
        if re.search(r"^  active: 1$", doc, re.M):
            out[m.group(1)] = params
    return out


def sun_to_light(euler):
    """Unity Euler(x, y) of the sun -> direction TOWARDS the light, in the preview's Blender world frame
    (Blender x = Unity x, Blender y = Unity z (north, away from the camera), Blender z = Unity y)."""
    x, y = math.radians(euler[0]), math.radians(euler[1])
    fwd = (math.sin(y) * math.cos(x), -math.sin(x), math.cos(y) * math.cos(x))  # Unity forward of the light
    return np.array([-fwd[0], -fwd[2], -fwd[1]])


# ------------------------------------------------------------------ ToonLit
def toon_ramp(x, bands=4.0, soft=0.02):
    steps = max(bands, 2.0) - 1.0
    s = np.clip(x, 0.0, 1.0) * steps
    f = s - np.floor(s)
    t = np.clip((f - (0.5 - soft)) / (2 * soft), 0.0, 1.0)
    return (s - f + t * t * (3 - 2 * t)) / steps


def shade(albedo, emis, light, normal, lit, view_dir, mat=PALETTE_MAT):
    """albedo/emis (H,W,3) gamma 0..1, light (H,W) = N.L x shadow from Blender, normal (H,W,3) Blender world,
    lit = act_lighting(), view_dir = unit vector towards the camera (Blender world). Returns HDR gamma colour."""
    L = sun_to_light(lit["euler"])
    ndl = np.clip(normal @ L, 0.0, 1.0)
    shadow = np.where(ndl > 0.03, np.clip(light / np.maximum(ndl, 1e-4), 0.0, 1.0), 1.0)
    att = 1.0 - lit["shadow_strength"] * (1.0 - shadow)
    ramp = toon_ramp(ndl * att, mat["bands"], mat["softness"])[..., None]
    sun = np.array(lit["sun"]) * lit["intensity"]
    tint = np.array(mat["shadow_tint"])
    lighting = sun * (tint + (1.0 - tint) * ramp)
    up = normal[..., 2:3]  # Blender z = Unity y
    sky, eq, gr = (np.array(lit[k]) for k in ("sky", "equator", "ground"))
    amb = np.where(up >= 0, eq + (sky - eq) * up, eq + (gr - eq) * (-up))
    # torches / braziers (additional lights) as an average warm fill: calibrated so the Act 1 floor and slime match
    # the in-game capture (integration/playable/05, 07) within ~10 %
    lighting = lighting + amb * mat["ambient_strength"] + np.array(lit["add"]) * TORCH_FILL
    col = albedo * lighting
    ndv = np.clip(normal @ np.asarray(view_dir), 0.0, 1.0)[..., None]
    rim = (1.0 - ndv) ** mat["rim_power"] * mat["rim_a"]
    col = col + np.array(mat["rim"]) * rim * np.maximum(ramp, 0.2)
    return col + emis * mat["emission"]


# ------------------------------------------------------------------ URP post (approximate)
def srgb_to_lin(c):
    c = np.maximum(c, 0.0)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lin_to_srgb(c):
    c = np.maximum(c, 0.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def bloom(col, p):
    from scipy import ndimage  # compose step only (Blender imports this module without scipy)
    thr = p.get("threshold", 1.0)
    knee = thr * 0.5
    br = col.max(axis=-1, keepdims=True)
    soft = np.clip(br - thr + knee, 0.0, 2.0 * knee)
    soft = soft * soft / (4.0 * knee + 1e-4)
    pre = col * (np.maximum(br - thr, soft) / np.maximum(br, 1e-4))
    scatter = p.get("scatter", 0.7)
    acc, wsum = np.zeros_like(col), 0.0
    for i, sigma in enumerate((1.5, 3.0, 6.0, 12.0, 24.0)):  # URP mip chain from half resolution
        w = scatter ** i
        acc += w * np.stack([ndimage.gaussian_filter(pre[..., c], sigma) for c in range(3)], axis=-1)
        wsum += w
    tint = np.array(p.get("tint", (1.0, 1.0, 1.0))[:3])
    return col + acc / wsum * p.get("intensity", 0.0) * tint


_LIN2LMS = np.array([[3.90405e-1, 5.49941e-1, 8.92632e-3], [7.08416e-2, 9.63172e-1, 1.35775e-3],
                     [2.31082e-2, 1.28021e-1, 9.36245e-1]])
_LMS2LIN = np.array([[2.85847e+0, -1.62879e+0, -2.48910e-2], [-2.10182e-1, 1.15820e+0, 3.24281e-4],
                     [-4.18120e-2, -1.18169e-1, 1.06867e+0]])


def _wb_coeffs(temperature, tint):
    t1, t2 = temperature / 65.0, tint / 65.0
    x = 0.31271 - t1 * (0.1 if t1 < 0 else 0.05)
    y = 2.87 * x - 3.0 * x * x - 0.27509507 + t2 * 0.05
    X, Y, Z = x / y, 1.0, (1.0 - x - y) / y
    lms = np.array([0.7328 * X + 0.4296 * Y - 0.1624 * Z, -0.7036 * X + 1.6975 * Y + 0.0061 * Z,
                    0.0030 * X + 0.0136 * Y + 0.9834 * Z])
    return np.array([0.949237, 1.03542, 1.08728]) / lms


def _logc(x):
    return np.where(x > 0.010591, 0.247190 * np.log10(5.555556 * x + 0.052272) + 0.385537, 5.367655 * x + 0.092809)


def _logc_inv(x):
    return np.where(x > 0.1496582, (np.power(10.0, (x - 0.385537) / 0.247190) - 0.052272) / 5.555556,
                    (x - 0.092809) / 5.367655)


def _luma(c):
    return (c * np.array([0.2126729, 0.7151522, 0.0721750])).sum(axis=-1, keepdims=True)


def _soft_light(a, b):
    return np.where(b < 0.5, 2 * a * b + a * a * (1 - 2 * b), np.sqrt(np.maximum(a, 0)) * (2 * b - 1) + 2 * a * (1 - b))


def _neutral(x):
    a, b, c, d, e, f, white = 0.2, 0.29, 0.24, 0.272, 0.02, 0.3, 5.3
    curve = lambda v: ((v * (a * v + c * b) + d * e) / (v * (a * v + b) + d * f)) - e / f  # noqa: E731
    ws = 1.0 / curve(white)
    return curve(x * ws) * ws


def grade(col, vol):
    lin = srgb_to_lin(col)
    wb = vol.get("WhiteBalance")
    if wb:
        lin = ((lin @ _LIN2LMS.T) * _wb_coeffs(wb.get("temperature", 0), wb.get("tint", 0))) @ _LMS2LIN.T
    ca = vol.get("ColorAdjustments", {})
    lin = lin * 2.0 ** ca.get("postExposure", 0.0)
    con = 1.0 + ca.get("contrast", 0.0) / 100.0
    lin = _logc_inv((_logc(np.maximum(lin, 0)) - 0.4135884) * con + 0.4135884)
    st = vol.get("SplitToning")
    if st:
        g = np.power(np.maximum(lin, 0), 1 / 2.2)
        t = np.clip(_luma(np.clip(g, 0, 1)) + st.get("balance", 0) / 100.0, 0, 1)
        g = _soft_light(g, 0.5 + (np.array(st["shadows"][:3]) - 0.5) * (1 - t))
        g = _soft_light(g, 0.5 + (np.array(st["highlights"][:3]) - 0.5) * t)
        lin = np.power(np.maximum(g, 0), 2.2)
    sat = 1.0 + ca.get("saturation", 0.0) / 100.0
    luma = _luma(lin)
    lin = luma + (lin - luma) * sat
    if vol.get("Tonemapping", {}).get("mode", 0) == 1:
        lin = _neutral(np.maximum(lin, 0))
    return np.clip(lin_to_srgb(lin), 0, 1)


def post(col, vol):
    b = vol.get("Bloom")
    if b:
        col = bloom(col, b)
    return grade(col, vol)
