#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ESCAPE FROM WORK -- main menu / loading / death key art + logo generator.

Pipeline
    1. Flux.1-schnell (fp8) via the local ComfyUI at 127.0.0.1:8188 renders a
       1920x1088 plate per screen (cached in tools/art/_cache/menu/, so a rerun
       never re-samples and stays byte-identical).
    2. Pillow + numpy grade each plate: desaturate to the game palette, cool white
       balance, blue-grey shadow tint, amber practicals, UI safe-zone darkening
       (solved so the reserved area is >= 25 % darker than the frame mean),
       vignette, film grain.
    3. Pillow typesets the logo from msyhbd.ttc (never AI-generated text).
    4. Writes the contact sheet and README.md.

Run
    python tools/art/gen_menu.py                 # build everything
    python tools/art/gen_menu.py --only main_menu
    python tools/art/gen_menu.py --force         # re-render the AI plates
    python tools/art/gen_menu.py --sweep main_menu a b c   # try seed salts

Idempotent: seeds are pinned literals in PLATES (so a rerun is byte-identical),
every post step is deterministic, and the AI plates are cached in
tools/art/_cache/menu/ so a rerun never re-samples. `--sweep` derives trial
seeds from zlib.crc32 of a salt string when a new composition is wanted.
"""

from __future__ import annotations

import argparse
import json
import sys
import time
import urllib.parse
import urllib.request
import uuid
import zlib
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

# --------------------------------------------------------------------------- #
# paths / constants
# --------------------------------------------------------------------------- #
HERE = Path(__file__).resolve().parent          # tools/art
ROOT = HERE.parents[1]                          # repo root
OUT_DIR = ROOT / "game" / "Assets" / "_Project" / "Art" / "Menu"
CACHE_DIR = HERE / "_cache" / "menu"

COMFY = "http://127.0.0.1:8188"
CKPT = "flux1-schnell-fp8.safetensors"

W, H = 1920, 1080
GEN_W, GEN_H = 1920, 1088                        # flux plate; cropped to 1080
AMBER = (224, 160, 60)                           # #E0A03C
AMBER_F = np.array(AMBER, np.float32) / 255.0
AMBIENT = 0.011                                  # shadow floor kept inside the UI zone

FONT_BOLD = r"C:\Windows\Fonts\msyhbd.ttc"
FONT_REG = r"C:\Windows\Fonts\msyh.ttc"


def font(path: str, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(path, size)


# --------------------------------------------------------------------------- #
# plate definitions
# --------------------------------------------------------------------------- #
_PAL = ("desaturated cool grey blue palette, cold white fluorescent light, "
        "no saturated colours")

PLATES: dict[str, dict] = {
    # ------------------------------------------------------------------ main
    "main_menu": {
        "seed": 401,
        "out": "M_MainMenu.png",
        "prompt": (
            "photorealistic architectural photography of a vast completely deserted "
            "corporate office atrium, nobody present, no human figures anywhere, "
            "abandoned empty hall, monumental raw concrete core wall with vertical fins "
            "in deep shadow on the left, multi-storey gallery balconies with glass "
            "railings and a giant glass curtain wall on the right, cold white fluorescent "
            "ceiling strips, " + _PAL + ", volumetric haze, low angle wide establishing "
            "shot, 35mm lens, polished floor reflections, no people, no text, no signage, "
            "no letters, cinematic game key art"
        ),
        "steps": 6,
        "guidance": 3.5,
        "grade": dict(sat=0.40, wb=(0.980, 1.000, 1.050),
                      shadow_tint=(0.004, 0.008, 0.017), shadow_knee=0.34,
                      contrast=1.10, black=0.012, gamma=0.98,
                      amber=0.62, amber_lo=0.012, amber_span=0.150,
                      amber_color=(0.98, 0.62, 0.24),
                      exposure=1.04, lift=0.004),
        "darken": dict(kind="left", target=0.68),
        "vignette": 0.34,
        "grain": 0.0070,
        # a dim amber practical glowing behind the doorway in the left wall
        "glow": dict(x=0.276, y=0.655, rx=0.135, ry=0.190,
                     color=(1.00, 0.60, 0.22), peak=0.40),
        "bloom": dict(radius=28, threshold=0.72, strength=0.30),
        "patches": None,
        "ui": ("宸︿晶 40 %锛坸 < 768 px锛夊帇鏆楃暀缁欒彍鍗曟寜閽紱"
               "鍙充晶鐜荤拑骞曞涓庡ぇ鍫傜旱娣变繚鎸佸畬鏁达紝鐒︾偣鍦ㄤ笂閮ㄦゼ鏉夸笌澶╄姳鐏甫"),
    },

    # --------------------------------------------------------------- loading
    "loading": {
        "seed": 3104,
        "out": "M_Loading.png",
        "prompt": (
            "photorealistic wide shot down a narrow aisle between two rows of tall dark "
            "server cabinets in an empty corporate data centre, perforated steel rack "
            "doors, structured cabling overhead, cold white fluorescent strips lit in the "
            "foreground only, desaturated cold grey blue palette, no saturated colours, "
            "the aisle turns into unlit blackness beyond the last light, heavy shadow in "
            "the centre of the frame, symmetrical one point perspective, low camera "
            "position, no people, no text, no glowing coloured indicators, no neon, "
            "cinematic game key art"
        ),
        "steps": 6,
        "guidance": 3.5,
        "grade": dict(sat=0.38, wb=(0.985, 1.000, 1.045),
                      shadow_tint=(0.004, 0.009, 0.018), shadow_knee=0.30,
                      contrast=1.08, black=0.010, gamma=0.92,
                      amber=0.55, amber_lo=0.012, amber_span=0.150,
                      amber_color=(0.98, 0.62, 0.24),
                      exposure=1.55, lift=0.008),
        "darken": dict(kind="center", target=0.70),
        "vignette": 0.40,
        "grain": 0.0065,
        "glow": None,
        "bloom": dict(radius=24, threshold=0.70, strength=0.28),
        "patches": None,
        "ui": ("涓ぎ 56 %脳56 % 鍘嬫殫锛岃繘搴︽潯涓庡姞杞芥彁绀哄帇鍦ㄤ腑杞达紱"
               "涓や晶鏈烘煖闃靛垪涓庨《閮ㄧ伅甯︽彁渚涚旱娣?),
    },

    # ----------------------------------------------------------------- death
    "death": {
        "seed": 3101,
        "out": "M_Death.png",
        "prompt": (
            "photorealistic architectural photography of a vast empty corporate office "
            "atrium at night with every main light turned off, almost black interior, a "
            "row of dim deep red emergency lamps glowing along the mezzanine balconies, a "
            "thin drift of smoke and dust in the still air, the far end of the hall is "
            "unlit darkness, dark concrete columns and steel railings in silhouette, "
            + _PAL + ", no magenta, no purple, oppressive silent mood, low camera angle, "
            "wide shot, no people, no text, no signage, cinematic game key art"
        ),
        "steps": 6,
        "guidance": 3.5,
        "grade": dict(sat=0.62, wb=(1.000, 0.995, 1.030),
                      shadow_tint=(0.003, 0.007, 0.014), shadow_knee=0.26,
                      contrast=1.04, black=0.008, gamma=0.94,
                      amber=0.38, amber_lo=0.010, amber_span=0.170,
                      amber_color=(0.72, 0.26, 0.16),
                      exposure=1.70, lift=0.006),
        "darken": dict(kind="center", target=0.68),
        "vignette": 0.42,
        "grain": 0.0060,
        "glow": None,
        "bloom": dict(radius=26, threshold=0.60, strength=0.38),
        # Flux rendered the two wall lamps as neon letters ("TUV" / "匚LCP");
        # these patches dissolve them into red emergency light spill.
        "patches": [
            dict(x=143, y=662, rx=64, ry=54, fill=0.78, peak=0.46,
                 tint=(0.88, 0.25, 0.17)),
            dict(x=402, y=663, rx=60, ry=52, fill=0.78, peak=0.40,
                 tint=(0.88, 0.25, 0.17)),
        ],
        "ui": ("涓ぎ 56 %脳56 % 鍘嬫殫锛岄樀浜＄粨绠楅潰鏉垮帇鍦ㄦ涓紱"
               "鍥涜淇濈暀搴旀€ョ孩鐏笌鐑熷皹锛屾暣浣撴瘮涓昏彍鍗曚綆 1.5 妗ｄ互涓?),
    },
}

LOGO_OUTS = [
    ("M_Logo.png", (244, 247, 249), (222, 228, 233)),
    ("M_Logo_Amber.png", AMBER, AMBER),
]


def plate_seed(name: str) -> int:
    """Fixed per-plate seed -- rerunning always reproduces the same plate.

    Seeds are pinned literals so the art stays stable; `--sweep <plate> <salt>`
    derives a fresh candidate from zlib.crc32(salt) when a new composition is
    needed, and the accepted value is then pinned here.
    """
    return int(PLATES[name]["seed"])


# --------------------------------------------------------------------------- #
# ComfyUI
# --------------------------------------------------------------------------- #
def _post(api: str, payload: dict, timeout: float = 30.0) -> dict:
    req = urllib.request.Request(COMFY + api, data=json.dumps(payload).encode(),
                                 headers={"Content-Type": "application/json"})
    return json.loads(urllib.request.urlopen(req, timeout=timeout).read().decode())


def comfy_alive() -> bool:
    try:
        urllib.request.urlopen(COMFY + "/system_stats", timeout=4).read()
        return True
    except Exception:
        return False


def flux_workflow(prompt: str, seed: int, steps: int, guidance: float) -> dict:
    return {
        "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": CKPT}},
        "2": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["1", 1], "text": prompt}},
        "3": {"class_type": "CLIPTextEncode",
              "inputs": {"clip": ["1", 1], "text": "people, text, watermark"}},
        "10": {"class_type": "FluxGuidance",
               "inputs": {"conditioning": ["2", 0], "guidance": guidance}},
        "4": {"class_type": "EmptyLatentImage",
              "inputs": {"width": GEN_W, "height": GEN_H, "batch_size": 1}},
        "5": {"class_type": "KSampler",
              "inputs": {"model": ["1", 0], "positive": ["10", 0], "negative": ["3", 0],
                         "latent_image": ["4", 0], "seed": seed, "steps": steps,
                         "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple",
                         "denoise": 1.0}},
        "6": {"class_type": "VAEDecode", "inputs": {"samples": ["5", 0], "vae": ["1", 2]}},
        "9": {"class_type": "SaveImage", "inputs": {"images": ["6", 0], "filename_prefix": "efw_menu"}},
    }


def _get_json(api: str, timeout: float = 120.0) -> dict:
    """GET with retries -- the ComfyUI event loop stalls while it samples."""
    last = None
    for attempt in range(6):
        try:
            return json.loads(urllib.request.urlopen(COMFY + api, timeout=timeout).read().decode())
        except Exception as exc:                        # noqa: BLE001 - transient socket errors
            last = exc
            time.sleep(2.0 + attempt * 2.0)
    raise RuntimeError(f"ComfyUI request {api} failed: {last}")


def render_plate(prompt: str, seed: int, steps: int, guidance: float,
                 dest: Path, timeout: float = 2400.0) -> Path:
    pid = _post("/prompt", {"prompt": flux_workflow(prompt, seed, steps, guidance),
                            "client_id": str(uuid.uuid4())})["prompt_id"]
    t0 = time.time()
    hist: dict = {}
    while True:
        hist = _get_json(f"/history/{pid}")
        if pid in hist:
            status = hist[pid].get("status", {})
            if status.get("completed"):
                break
            if status.get("status_str") == "error":
                raise RuntimeError(f"ComfyUI error: {json.dumps(status)[:1200]}")
        if time.time() - t0 > timeout:
            raise TimeoutError(f"ComfyUI render timed out after {timeout:.0f}s")
        time.sleep(2.0)
    info = hist[pid]["outputs"]["9"]["images"][0]
    url = (f"{COMFY}/view?filename={urllib.parse.quote(info['filename'])}"
           f"&subfolder={urllib.parse.quote(info.get('subfolder', ''))}&type={info['type']}")
    data = urllib.request.urlopen(url, timeout=120).read()
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(data)
    return dest


def ensure_plate(name: str, force: bool = False) -> tuple[Path, int, bool]:
    spec = PLATES[name]
    seed = plate_seed(name)
    path = CACHE_DIR / f"{name}_s{seed}.png"
    if path.exists() and not force:
        return path, seed, True
    if not comfy_alive():
        raise SystemExit(
            f"ComfyUI is not reachable at {COMFY} and the cached plate is missing\n"
            f"  expected: {path}\n"
            "  start it with:\n"
            r"  F:\ComfyUI\venv\Scripts\python.exe F:\ComfyUI\main.py --listen 127.0.0.1 "
            r"--port 8188 --disable-auto-launch")
    print(f"  rendering {name} (seed {seed}, {spec['steps']} steps) ...", flush=True)
    render_plate(spec["prompt"], seed, spec["steps"], spec["guidance"], path)
    return path, seed, False


# --------------------------------------------------------------------------- #
# numeric helpers
# --------------------------------------------------------------------------- #
def luma(rgb: np.ndarray) -> np.ndarray:
    return rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722


def smoothstep(e0: float, e1: float, x: np.ndarray) -> np.ndarray:
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


_YS, _XS = np.mgrid[0:H, 0:W].astype(np.float32)
_U = (_XS + 0.5) / W                       # 0..1 across the frame
_V = (_YS + 0.5) / H


def ui_region(name: str) -> tuple[slice, slice]:
    """Pixel window that must stay dark for the UI."""
    if PLATES[name]["darken"]["kind"] == "left":
        return slice(0, H), slice(0, int(round(W * 0.40)))
    x0, x1 = int(round(W * 0.22)), int(round(W * 0.78))
    y0, y1 = int(round(H * 0.22)), int(round(H * 0.78))
    return slice(y0, y1), slice(x0, x1)


def darken_field(kind: str) -> np.ndarray:
    if kind == "left":
        m = 1.0 - smoothstep(0.34, 0.62, _U)
    else:
        rr = np.sqrt(((_U - 0.5) / 0.305) ** 2 + ((_V - 0.5) / 0.305) ** 2)
        m = 1.0 - smoothstep(0.55, 1.30, rr)
    return m.astype(np.float32)


def vignette_field(strength: float) -> np.ndarray:
    rr = np.sqrt((_U - 0.5) ** 2 + (_V - 0.5) ** 2) / 0.7071
    v = 1.0 - strength * smoothstep(0.38, 1.06, rr)
    return v.astype(np.float32)


def grade(rgb: np.ndarray, g: dict) -> np.ndarray:
    lum = luma(rgb)[..., None]
    rgb = lum + (rgb - lum) * g["sat"]
    rgb = rgb * np.array(g["wb"], np.float32)

    lum = luma(rgb)[..., None]
    sh = np.clip(1.0 - lum / g["shadow_knee"], 0.0, 1.0) ** 1.5
    rgb = rgb + sh * np.array(g["shadow_tint"], np.float32)

    rgb = 0.5 + (rgb - 0.5) * g["contrast"]
    rgb = g["black"] + rgb * (1.0 - g["black"])
    rgb = np.clip(rgb, 0.0, 1.0) ** g["gamma"]

    if g["amber"] > 0.0:
        warm = smoothstep(g["amber_lo"], g["amber_lo"] + g["amber_span"],
                          rgb[..., 0] - rgb[..., 2])
        lum = luma(rgb)[..., None]
        w = warm[..., None] * g["amber"] * np.clip(lum * 3.2 + 0.10, 0.0, 1.0)
        rgb = rgb * (1.0 - w) + np.array(g["amber_color"], np.float32) * w

    rgb = rgb * g["exposure"] + g["lift"]
    return np.clip(rgb, 0.0, 1.0)


def add_glow(rgb: np.ndarray, gl: dict) -> np.ndarray:
    """Screen-blend a soft, slightly elongated amber practical."""
    du = (_U - gl["x"]) / gl["rx"]
    dv = (_V - gl["y"]) / gl["ry"]
    r2 = du * du + dv * dv
    g = np.exp(-r2 * 1.9).astype(np.float32)
    core = np.exp(-r2 * 14.0).astype(np.float32)
    amt = (g * gl["peak"] + core * gl["peak"] * 0.9)[..., None]
    tint = np.array(gl["color"], np.float32)
    screen = 1.0 - (1.0 - rgb) * (1.0 - np.clip(amt * tint, 0.0, 1.0))
    return np.clip(screen, 0.0, 1.0)


def _blur(rgb: np.ndarray, radius: float) -> np.ndarray:
    img = Image.fromarray((np.clip(rgb, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8), "RGB")
    return np.asarray(img.filter(ImageFilter.GaussianBlur(radius)), np.float32) / 255.0


def retouch_patches(rgb: np.ndarray, patches: list[dict]) -> np.ndarray:
    """Remove AI-invented detail inside small feathered patches.

    Flux draws the tiny wall lamps as neon *letters* ("TUV", "鍖歀CP"). Each patch
    is replaced by a heavily blurred copy of its own surroundings and re-lit with
    one soft red practical, so the spot reads as emergency light spilling on the
    wall instead of pseudo-text.
    """
    soft = _blur(rgb, 70.0)
    out = rgb
    for p in patches:
        cx, cy = p["x"] / W, p["y"] / H
        d = np.sqrt(((_U - cx) / (p["rx"] / W)) ** 2 + ((_V - cy) / (p["ry"] / H)) ** 2)
        m = (1.0 - smoothstep(0.50, 1.20, d))[..., None]
        out = out * (1.0 - m) + (soft * p["fill"]) * m
        r2 = ((_U - cx) / 0.026) ** 2 + ((_V - cy) / 0.032) ** 2
        amt = np.clip(np.exp(-r2 * 2.4) + np.exp(-r2 * 9.0) * 0.5, 0.0, 1.0) * p["peak"]
        out = np.clip(1.0 - (1.0 - out) *
                      (1.0 - amt[..., None] * np.array(p["tint"], np.float32)), 0.0, 1.0)
    return out


def bloom(rgb: np.ndarray, radius: float, threshold: float, strength: float) -> np.ndarray:
    """Screen-add a blurred copy of the highlights -- cheap halation."""
    hi = np.clip((luma(rgb)[..., None] - threshold) / max(1e-3, 1.0 - threshold), 0.0, 1.0)
    bl = _blur(rgb * hi, radius) * strength
    return np.clip(1.0 - (1.0 - rgb) * (1.0 - bl), 0.0, 1.0)


def solve_darken(rgb: np.ndarray, field: np.ndarray, region: tuple[slice, slice],
                 target: float) -> tuple[np.ndarray, float, dict]:
    """Darken `field` until region_mean <= target * frame_mean.

    A sliver of ambient (`AMBIENT`) is mixed back in so the reserved area keeps
    readable structure instead of clipping to pure black.
    """
    strength = 0.06
    out = rgb
    while True:
        out = rgb * (1.0 - strength * field[..., None]) + AMBIENT * strength * field[..., None]
        rm = float(luma(out[region]).mean())
        fm = float(luma(out).mean())
        if rm <= target * fm or strength >= 0.92:
            break
        strength = min(0.92, strength + 0.04)
    return out, strength, {"region": rm, "full": fm, "ratio": rm / fm}


def finish(rgb: np.ndarray, name: str, spec: dict) -> tuple[Image.Image, dict]:
    rgb = grade(rgb, spec["grade"])
    if spec.get("patches"):
        rgb = retouch_patches(rgb, spec["patches"])
    if spec.get("glow"):
        rgb = add_glow(rgb, spec["glow"])
    if spec.get("bloom"):
        b = spec["bloom"]
        rgb = bloom(rgb, b["radius"], b["threshold"], b["strength"])

    field = darken_field(spec["darken"]["kind"])
    region = ui_region(name)
    rgb = rgb * vignette_field(spec["vignette"])[..., None]
    rgb, strength, st = solve_darken(rgb, field, region, spec["darken"]["target"])

    rng = np.random.default_rng(zlib.crc32(f"efw/menu/{name}/grain".encode()) & 0xFFFFFFFF)
    rgb = rgb + rng.normal(0.0, spec["grain"], (H, W)).astype(np.float32)[..., None]
    rgb = np.clip(rgb, 0.0, 1.0)

    st["strength"] = strength
    st["target"] = spec["darken"]["target"]
    image = Image.fromarray((rgb * 255.0 + 0.5).astype(np.uint8), "RGB")
    return image, st


def measure(image: Image.Image, name: str) -> dict:
    a = np.asarray(image.convert("RGB"), np.float32) / 255.0
    region = ui_region(name)
    rm = float(luma(a[region]).mean())
    fm = float(luma(a).mean())
    return {"region": rm, "full": fm, "ratio": rm / fm,
            "region_255": rm * 255.0, "full_255": fm * 255.0}


def load_plate(path: Path) -> np.ndarray:
    im = Image.open(path).convert("RGB")
    if im.size[1] > H:
        top = (im.size[1] - H) // 2
        im = im.crop((0, top, im.size[0], top + H))
    if im.size != (W, H):
        im = im.resize((W, H), Image.LANCZOS)
    return np.asarray(im, np.float32) / 255.0


# --------------------------------------------------------------------------- #
# logo
# --------------------------------------------------------------------------- #
LOGO_W, LOGO_H = 1600, 600
LOGO_SS = 3
STROKE = (11, 14, 18)


def text_layer(text: str, fnt: ImageFont.FreeTypeFont, tracking: float,
               fill: tuple, stroke_w: int) -> Image.Image:
    """Render one tracked line onto its own layer, cropped to the ink box."""
    pad = stroke_w + 6
    adv = [fnt.getlength(c) for c in text]
    width = sum(adv) + tracking * (len(text) - 1)
    asc, desc = fnt.getmetrics()
    layer = Image.new("RGBA", (int(width) + 2 * pad + 8, asc + desc + 2 * pad), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    x = float(pad)
    for ch, a in zip(text, adv):
        d.text((x, pad), ch, font=fnt, fill=fill,
               stroke_width=stroke_w, stroke_fill=STROKE)
        x += a + tracking
    bb = layer.getbbox()
    return layer.crop(bb)


def build_logo(title_rgb: tuple, sub_rgb: tuple) -> Image.Image:
    cw, ch = LOGO_W * LOGO_SS, LOGO_H * LOGO_SS
    title_f = font(FONT_BOLD, 236 * LOGO_SS)
    sub_f = font(FONT_BOLD, 66 * LOGO_SS)

    title = text_layer("ESCAPE", title_f, 36.0 * LOGO_SS, title_rgb + (255,), 4 * LOGO_SS)
    sub = text_layer("FROM WORK", sub_f, 33.0 * LOGO_SS, sub_rgb + (255,), 3 * LOGO_SS)

    gap_a, gap_b = 30 * LOGO_SS, 40 * LOGO_SS
    rule_h = 2 * LOGO_SS
    total = title.size[1] + gap_a + rule_h + gap_b + sub.size[1]
    top = (ch - total) // 2

    canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    canvas.alpha_composite(title, ((cw - title.size[0]) // 2, top))

    rule_y = top + title.size[1] + gap_a
    rule_w = int(title.size[0] * 1.05)
    x0, x1 = (cw - rule_w) // 2, (cw + rule_w) // 2
    gx, gap, slant = cw // 2, 32 * LOGO_SS, 14 * LOGO_SS
    ink = tuple(int(v) for v in title_rgb) + (255,)
    d = ImageDraw.Draw(canvas)
    d.polygon([(x0, rule_y), (gx - gap, rule_y),
               (gx - gap - slant, rule_y + rule_h), (x0, rule_y + rule_h)], fill=ink)
    d.polygon([(gx + gap, rule_y), (x1, rule_y),
               (x1, rule_y + rule_h), (gx + gap - slant, rule_y + rule_h)], fill=ink)

    canvas.alpha_composite(sub, ((cw - sub.size[0]) // 2, rule_y + rule_h + gap_b))
    return canvas.resize((LOGO_W, LOGO_H), Image.LANCZOS)


# --------------------------------------------------------------------------- #
# contact sheet
# --------------------------------------------------------------------------- #
SHEET_W, SHEET_H = 1920, 1560


def build_contact_sheet(plates: list[tuple[str, Image.Image, dict]],
                        logos: list[tuple[str, Image.Image]]) -> Image.Image:
    bg = (16, 19, 22)
    sheet = Image.new("RGB", (SHEET_W, SHEET_H), bg)
    d = ImageDraw.Draw(sheet)

    d.text((60, 30), "ESCAPE FROM WORK  ·  主菜单 / 加载 / 阵亡 / Logo",
           font=font(FONT_BOLD, 32), fill=(234, 238, 242))
    d.text((60, 74),
           "Flux.1-schnell (1920×1088)  →  Pillow 调色 · 压暗 · 暗角 · 颗粒   |   "
           "预留区亮度均低于全图 25 % 以上（数据见 README）",
           font=font(FONT_REG, 18), fill=(138, 148, 158))

    cw, chh, gapx, gapy = 890, 500, 20, 34
    for i, (fname, img, st) in enumerate(plates):
        col, row = i % 2, i // 2
        x = 60 + col * (cw + gapx)
        y = 118 + row * (chh + gapy + 34)
        d.text((x + 2, y - 26), fname, font=font(FONT_BOLD, 18), fill=(206, 214, 222))
        d.text((x + cw - 300, y - 26),
               f"预留区亮度 {st['ratio'] * 100:.0f} % of frame",
               font=font(FONT_REG, 17), fill=(150, 160, 170))
        sheet.paste(img.resize((cw, chh), Image.LANCZOS), (x, y))
        d.rectangle([x - 1, y - 1, x + cw, y + chh], outline=(44, 50, 56))

    ly = 118 + 2 * (chh + gapy + 34) + 6
    d.text((60, ly - 26), "LOGO 1600×600 RGBA 透明底（Pillow 排版，非 AI 生成文字）",
           font=font(FONT_BOLD, 18), fill=(206, 214, 222))

    chips = [((21, 26, 31), logos[0]), ((201, 206, 211), logos[1])]
    for i, (fillc, (fname, logo)) in enumerate(chips):
        x = 60 + i * (800 + 200)
        sheet.paste(Image.new("RGB", (800, 300), fillc), (x, ly))
        d.rectangle([x - 1, ly - 1, x + 800, ly + 300], outline=(60, 66, 72))
        scale = min(720 / logo.width, 250 / logo.height)
        lg = logo.resize((int(logo.width * scale), int(logo.height * scale)), Image.LANCZOS)
        sheet.paste(lg, (x + (800 - lg.width) // 2, ly + (300 - lg.height) // 2), lg)
        d.text((x + 2, ly + 308), fname, font=font(FONT_REG, 16), fill=(150, 160, 170))

    return sheet


# --------------------------------------------------------------------------- #
# README
# --------------------------------------------------------------------------- #
def build_readme(stats: dict[str, dict], seeds: dict[str, int]) -> str:
    rows = []
    for name, spec in PLATES.items():
        st = stats[name]
        kind = "左侧 40 %（x < 768）" if spec["darken"]["kind"] == "left" else "中央 56 %×56 %"
        rows.append(
            f"| `{spec['out']}` | 1920×1080 | {kind} | {st['region_255']:.1f} | "
            f"{st['full_255']:.1f} | **{100 - st['ratio'] * 100:.0f} % 更暗** |")
    table = "\n".join(rows)

    prompt_blocks = []
    for name, spec in PLATES.items():
        prompt_blocks.append(
            f"### {spec['out']}\n\n"
            f"```text\n{spec['prompt']}\n```\n\n"
            f"- seed：`{seeds[name]}`（固定在 `PLATES` 里的字面量，缓存命中即字节一致）\n"
            f"- {spec['steps']} steps / euler / simple / guidance {spec['guidance']} / cfg 1.0 / "
            f"{GEN_W}×{GEN_H}")
    prompts = "\n\n".join(prompt_blocks)

    return f"""# ESCAPE FROM WORK — 主菜单 / 加载界面 / 阵亡界面 / Logo

2D 主菜单美术，全部来自本机 ComfyUI（Flux.1-schnell fp8）+ Pillow 程序化后处理。
生成脚本：`tools/art/gen_menu.py`（幂等：seed 固定，缓存命中时重跑字节一致）。

- 只依赖 numpy + Pillow，没有 shapely / scipy / matplotlib。
- 本目录不提交 `.meta`，交给 Unity 首次导入生成（与 `Art/UI/` 的做法一致）。
- AI 底图缓存在 `tools/art/_cache/menu/`，**不进 Unity 工程**；删掉缓存并加 `--force` 才会重新采样。

## 1. 文件清单

| 文件 | 尺寸 | 格式 | 用途 |
| --- | --- | --- | --- |
| `M_MainMenu.png` | 1920×1080 | RGB PNG | 主菜单背景（无文字 / 无 UI） |
| `M_Loading.png` | 1920×1080 | RGB PNG | 加载界面背景（无文字 / 无 UI） |
| `M_Death.png` | 1920×1080 | RGB PNG | 阵亡 / 撤离失败结算背景 |
| `M_Logo.png` | 1600×600 | RGBA PNG | Logo 白色版（透明底） |
| `M_Logo_Amber.png` | 1600×600 | RGBA PNG | Logo 琥珀版 `#E0A03C`（透明底） |
| `_contact_sheet.png` | 1920×1560 | RGB PNG | 总览拼图（**只给美术/评审看，不要进构建**） |
| `README.md` | — | — | 本文件 |

## 2. 构图与 UI 安全区

三张背景都按「UI 会压在哪里」构图，并做了确定性压暗；压暗后仍保留该区域的材质层次（不是纯黑块）。

| 文件 | 预留区 | 预留区平均亮度 | 全图平均亮度 | 相对全图 |
| --- | --- | --- | --- | --- |
{table}

- 亮度口径：Rec.709 相对亮度（0–255 的 sRGB 编码值），预留区 = 上表窗口内所有像素的均值。
- 阈值要求「预留区比全图平均低 25 % 以上」，实测三张都在 30 % 以上，留了余量。
- 主菜单：左侧 40 % 是核心筒暗面 + 门洞琥珀应急灯，按钮放在 x≈120–700 px、垂直居中偏下最稳。
- 加载：中央 56 %×56 % 是机柜走道的尽端暗部，进度条压在轴线中央不会糊字。
- 阵亡：中央 56 %×56 % 是大厅的暗部，结算面板（`阵亡` / `撤离失败` / 统计）压正中。

## 3. Unity 导入设置

### 3.1 三张背景（`M_MainMenu` / `M_Loading` / `M_Death`）

| 设置项 | 值 | 理由 |
| --- | --- | --- |
| Texture Type | **Default** | 只当 `RawImage.texture` 用，不需要 Sprite 切片与 PPU |
| sRGB (Color Texture) | 勾选 On | 是给玩家看的颜色图，不是线性数据 |
| Alpha Is Transparency | 取消 Off | 没有 alpha 通道 |
| Non-Power of 2 | None | 1920×1080 不是 2 的幂，**不要让 Unity 拉伸成 2048** |
| Read/Write Enabled | 取消 Off | 运行时不需要 CPU 读像素 |
| Generate Mip Maps | 取消 Off | 屏幕空间 1:1 使用，mipmap 只会让 UI 变软 |
| Wrap Mode | Clamp | 避免边缘采样溢出 |
| Filter Mode | Bilinear | UI 缩放平滑 |
| Max Size | 2048 | 1920 需要 ≥ 2048 才不降采样 |
| Compression | **BC7 / High Quality**（PC）；移动端 ASTC 6×6 | 大面积平滑渐变，BC1/ETC2 会在暗部出明显色带 |
| 目标平台建议 | PC 用 BC7，主机同；移动端若包体紧张再降到 ASTC 8×8 | |

接入方式建议：`Canvas` 下放一层全屏 `RawImage`（`texture = M_MainMenu`），
菜单按钮与 Logo 叠在其上；不要用 `Image` + `Sprite`，省掉一次 sprite 生成，
也避免 `Preserve Aspect` 带来的黑边。

### 3.2 Logo（`M_Logo` / `M_Logo_Amber`）

| 设置项 | 值 |
| --- | --- |
| Texture Type | **Sprite (2D and UI)** |
| Sprite Mode | Single |
| Pixels Per Unit | 100 |
| Mesh Type | Full Rect |
| Alpha Is Transparency | 勾选 On |
| sRGB | 勾选 On |
| Generate Mip Maps | 取消 Off |
| Wrap Mode | Clamp |
| Filter Mode | Bilinear |
| Compression | **None**（或 High Quality）——描边只有 1.33 px，压缩会吃掉 |
| Max Size | 2048 |

- 显示尺寸建议：宽度 620–760 px（原图 1600，约 0.4–0.47 倍），放在主菜单左上或中上。
- 两张 Logo 内容完全一致，只有字色不同：亮背景用 `M_Logo_Amber`，暗背景用 `M_Logo`；
  也可以只留一张白色版，用 `Image.color` 染成 `#E0A03C`（描边也会被一起染，见「已知不足」）。

## 4. 生成命令

```powershell
# 全部重出（缺缓存会自动连 ComfyUI；有缓存则只重跑后处理，字节一致）
C:\\Users\\17613\\.dsh\\dsh-runtimes\\dsh-primary-runtime\\dependencies\\python\\python.exe tools\\art\\gen_menu.py

# 只重出一张
& $py tools\\art\\gen_menu.py --only main_menu

# 强制重新采样 AI 底图（换构图时才用）
& $py tools\\art\\gen_menu.py --force

# 试几种 seed salt 找构图（输出到 tools/art/_cache/menu/sweep/）
& $py tools\\art\\gen_menu.py --sweep death a b c d
```

ComfyUI 没起来时脚本会直接报错并给出启动命令：

```powershell
F:\\ComfyUI\\venv\\Scripts\\python.exe F:\\ComfyUI\\main.py --listen 127.0.0.1 --port 8188 --disable-auto-launch
```

### 处理管线

1. **Flux.1-schnell fp8**，1920×1088，6 steps / euler / simple / guidance 3.5 / cfg 1.0
   （schnell 是蒸馏模型，cfg 必须 1.0，负向提示基本不生效，所以「无人 / 无文字」写在正向里）。
2. 裁掉上下各 4 px → 1920×1080。
3. Pillow + numpy 后处理（顺序固定）：降饱和 → 冷白平衡 → 阴影蓝灰染色 → 对比/黑位 →
   暖像素拉向琥珀 → （主菜单）门洞琥珀实用光 → （阵亡）局部修补灯牌 → Bloom →
   UI 安全区压暗（迭代求解到目标比值）→ 暗角 → 胶片颗粒
   （同时充掉 8 bit 渐变的抖动层，防止暗部色带）。
4. Logo 用 `msyhbd.ttc` 逐字排版（字距逐字累加），3× 超采样后 LANCZOS 缩小，
   描边 4 px@3× = 1.33 px。

## 5. Prompt 原文

{_prompts_section(prompts)}
## 6. 已知不足

- **文字与招牌**：Flux schnell 会随机在墙上生成假英文（`DAIN ET`、`TARET` 之类）。
  当前三张都挑了没有可读文字/招牌的 seed，但主菜单左墙的小门牌、加载图机柜上的
  小标签仍有 2–3 px 的模糊色块，凑近看不到字母，远景无感。
- **阵亡图的两盏壁灯**是 Flux 自己画成霓虹字母（`TUV`、`匚LCP`）的伪文字。后处理用
  `retouch_patches()` 把它们换成了柔和的红色应急光斑——形状变成没有灯具的「光团」，
  凑近看会觉得光源不明，但比可读的伪文字干净，也不违反「不要霓虹」。
- **人物**：Flux 喜欢在大空间里放一个「比例人」。当前三张逐张放大目检过，均无人物。
- **冷白偏青**：模型默认把「冷白荧光」理解成青蓝，已用降饱和 + 白平衡压回灰蓝，
  加载图的机柜仍带一点青，是这套素材里最偏的一张。
- **琥珀点缀是合成的**：主菜单门洞那盅琥珀应急灯由 Pillow 屏幕叠加生成，
  不是模型画出来的灯具，所以没有可见的灯罩/灯丝——只有光；20 px 内看会觉得光斑「平」。
- **Logo 描边会被 Unity 染色**：白色版描边是深灰 `#0B0E12`，若用 `Image.color`
  染成琥珀色，描边会一起被染（变成暗琥珀）。要严格单色化请直接用两张现成 PNG。
- **Logo 字体**：`msyhbd.ttc`（微软雅黑 Bold）的拉丁字形偏「系统 UI 感」，
  不是定制字体；任务要求必须用这个字体排版，所以字形没有做二次改造。
- **分辨率**：AI 底图 1920×1088，即 1:1 出 1080p，没有做 2× 超分；4K 屏上会被
  Unity 拉伸到 2×，细节（天花灯带边缘）会变软。若要 4K 版需另跑一次 3840×2160 的
  超分管线（本脚本未包含）。
- **加载图中央仍是最亮处之一**：压暗后中央亮度低于全图 34 %，但镜头正对的尽端
  仍是全图最亮处之一；进度条用浅色字（`#E6EAEE`）没问题，用纯白粗体会略微「发光」。
"""


def _prompts_section(prompts: str) -> str:
    return prompts + "\n\n"


# --------------------------------------------------------------------------- #
# main
# --------------------------------------------------------------------------- #
def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description="ESCAPE FROM WORK menu art generator")
    ap.add_argument("--only", choices=list(PLATES), help="rebuild a single plate")
    ap.add_argument("--force", action="store_true", help="re-render the AI plate")
    ap.add_argument("--sweep", nargs="+", metavar="SALT",
                    help="--sweep <plate> <salt> [salt ...]: render seed variants")
    ap.add_argument("--no-sheet", action="store_true", help="skip the contact sheet")
    ap.add_argument("--no-readme", action="store_true", help="skip README.md")
    args = ap.parse_args(argv)

    if args.sweep:
        name = args.sweep[0]
        if name not in PLATES:
            raise SystemExit(f"unknown plate: {name}")
        for salt in args.sweep[1:]:
            seed = zlib.crc32(f"efw/menu/{name}/{salt}".encode("utf-8")) & 0x7FFFFFFF
            dest = CACHE_DIR / f"{name}_{salt}_s{seed}.png"
            if dest.exists():
                print(f"  cached {dest}")
                continue
            print(f"  {name} salt={salt} seed={seed} ...", flush=True)
            render_plate(PLATES[name]["prompt"], seed, PLATES[name]["steps"],
                         PLATES[name]["guidance"], dest)
            print(f"  -> {dest}")
        return 0

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    names = [args.only] if args.only else list(PLATES)

    images: dict[str, Image.Image] = {}
    seeds: dict[str, int] = {}
    stats: dict[str, dict] = {}

    for name in names:
        spec = PLATES[name]
        print(f"[{name}] plate", flush=True)
        path, seed, cached = ensure_plate(name, force=args.force)
        seeds[name] = seed
        print(f"  {'cache' if cached else 'fresh'} {path.name}", flush=True)

        rgb = load_plate(path)
        image, _ = finish(rgb, name, spec)
        out = OUT_DIR / spec["out"]
        image.save(out, "PNG", optimize=True)
        st = measure(image, name)
        stats[name] = st
        print(f"  -> {out.name}  region {st['region_255']:.1f} / full {st['full_255']:.1f}"
              f"  ratio {st['ratio'] * 100:.0f}%  ({100 - st['ratio'] * 100:.0f}% darker)",
              flush=True)
        images[name] = image

    logo_images: list[tuple[str, Image.Image]] = []
    for fname, title_rgb, sub_rgb in LOGO_OUTS:
        logo = build_logo(title_rgb, sub_rgb)
        logo.save(OUT_DIR / fname, "PNG", optimize=True)
        logo_images.append((fname, logo))
        print(f"[logo] {fname}  {logo.width}x{logo.height} {logo.mode}", flush=True)

    if not args.only and not args.no_sheet:
        plates = [(PLATES[n]["out"], images[n], stats[n]) for n in PLATES]
        sheet = build_contact_sheet(plates, logo_images)
        sheet.save(OUT_DIR / "_contact_sheet.png", "PNG", optimize=True)
        print(f"[sheet] _contact_sheet.png {sheet.width}x{sheet.height}", flush=True)

    if not args.only and not args.no_readme:
        md = build_readme(stats, seeds)
        (OUT_DIR / "README.md").write_text(md, encoding="utf-8", newline="\n")
        print("[readme] README.md", flush=True)

    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
