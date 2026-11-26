#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ESCAPE FROM WORK —— 贴花 (decal) 美术资源生成器.

设计约定
--------
* 输出 ``game/Assets/_Project/Art/Decals/D_<Name>.png``，512x512 RGBA，透明底。
  Alpha = 贴花形状与不透明度，游戏里用一张 quad + alpha blend 贴在墙面/地面/玻璃上。
* 颜色语义：RGB 是「污渍本体色」，不是「覆盖后的墙面」。Alpha 低 => 墙面透出来。
  所以水渍/油渍/污痕的 alpha 峰值只有 0.4~0.75，只有弹孔这种「洞」才接近 1.0。
* 混合方案：整个流程用 **预乘 alpha** 从底(最软)到顶(最硬)叠加，最后
  1) 高斯羽化 alpha（feather 1.6~5px） 2) 边框 2~8px 渐隐（前 2px 强制 alpha=0，
  不会出现「贴纸硬矩形边」）。

AI / 程序化的分工
----------------
* AI（本机 ComfyUI / SDXL base 1.0，1024²，dpmpp_2m + karras）只负责**材质颗粒**：
  混凝土灰浆颗粒、锈迹鳞片、霉斑孢子、油膜、干涸液体的细纹等。它出不了透明通道，
  也出不了「放射裂纹 / 蛛网 / 杯圈 / 垂痕」这类结构。
* 程序化（numpy + Pillow）负责**形状与结构**：fBm 域扭曲的团块、径向衰减、
  分叉裂纹树、同心蛛网、杯圈环带、重力垂痕、鞋底拖痕、潮线、粉尘散点。

幂等
----
* ``random.seed(20261126)`` + 每张贴花独立的 ``np.random.default_rng``
  （种子 = 20261126 + crc32(名字) % 100000，用 zlib.crc32 而不是 hash()，
  跨进程稳定，且增删贴花不会打乱其它贴花的种子）。
* AI 底图带缓存（``F:\\ComfyUI\\output\\decals\\base_<key>.png``），固定 seed 生成一次，
  之后重跑直接复用 => 同参数重跑像素字节一致，不需要 GPU。
* 每张图打印 RGBA 缓冲的 zlib.crc32，可与上次输出比对。

用法
----
    python tools/art/gen_decals.py                  # 全量（缺底图时自动问 ComfyUI 要）
    python tools/art/gen_decals.py --bases-only     # 只生成 AI 底图缓存
    python tools/art/gen_decals.py --only D_Oil_Splat D_Rust_Run
    python tools/art/gen_decals.py --no-ai          # 没有 ComfyUI 时用占位噪声调形状
    python tools/art/gen_decals.py --regen-ai       # 强制重出 AI 底图

不修改仓库里任何已有文件；除了这个脚本，只写 ``Art/Decals/`` 下的 PNG/README/contact sheet。

自检口径（每次运行都会打印）
----------------------------
* ``cov%``  = ``alpha > 0.06`` 的像素占比（视觉比重，不是全透明面积）。
* ``clear`` = 最外 2px 的 alpha 最大值，必须为 0（保证 quad 四边不出现硬边）。
* ``amax``  = alpha 峰值。低于 0.35 视为「太淡」，会被标 ``!FAINT``。
"""

from __future__ import annotations

import argparse
import json
import math
import os
import random
import sys
import time
import urllib.parse
import urllib.request
import zlib
from dataclasses import dataclass, field
from typing import Callable, Dict, List, Tuple

import numpy as np
from PIL import Image, ImageDraw, ImageFont

# ---------------------------------------------------------------- paths / const

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(REPO, "game", "Assets", "_Project", "Art", "Decals")
README_PATH = os.path.join(OUT_DIR, "README.md")
SHEET_PATH = os.path.join(OUT_DIR, "_contact_sheet.png")

SIZE = 512
SEED = 20261126
BORDER_CLEAR_PX = 2.0     # 前 2px 强制 alpha=0
BORDER_FADE_PX = 8.0      # 8px 之外不受边框影响

COMFY = os.environ.get("EFW_COMFY", "http://127.0.0.1:8188")
COMFY_OUT = os.environ.get("EFW_COMFY_OUT", r"F:\ComfyUI\output")
BASE_CACHE = os.path.join(COMFY_OUT, "decals")

COV_THRESHOLD = 0.06      # 覆盖率统计阈值

try:  # Windows 控制台默认 codepage 可能不是 UTF-8，统一输出编码
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

random.seed(SEED)  # 仅作兜底；所有随机数都走 per-decal default_rng

YY, XX = np.mgrid[0:SIZE, 0:SIZE]
XX = (XX.astype(np.float32) + 0.5)
YY = (YY.astype(np.float32) + 0.5)


# ------------------------------------------------------------------ math utils

def smoothstep(e0: float, e1: float, x) -> np.ndarray:
    t = np.clip((x - e0) / (e1 - e0 + 1e-9), 0.0, 1.0)
    return (t * t * (3.0 - 2.0 * t)).astype(np.float32)


def _box1d(a: np.ndarray, r: int, axis: int) -> np.ndarray:
    """Separable box filter (edge padding), float32 in/out."""
    if r <= 0:
        return a
    n = a.shape[axis]
    pad = [(0, 0), (0, 0)]
    pad[axis] = (r, r)
    p = np.pad(a.astype(np.float32), pad, mode="edge")
    c = np.cumsum(p, axis=axis, dtype=np.float64)
    head = list(p.shape)
    head[axis] = 1
    c = np.concatenate([np.zeros(head, np.float64), c], axis=axis)
    hi = [slice(None), slice(None)]
    lo = [slice(None), slice(None)]
    hi[axis] = slice(2 * r + 1, 2 * r + 1 + n)
    lo[axis] = slice(0, n)
    return ((c[tuple(hi)] - c[tuple(lo)]) / (2 * r + 1)).astype(np.float32)


def blur(a: np.ndarray, sigma: float) -> np.ndarray:
    """3-pass box blur ≈ Gaussian; PIL's GaussianBlur refuses mode 'F'."""
    if sigma <= 0.05:
        return a.astype(np.float32)
    r = max(1, int(round(sigma)))
    for _ in range(3):
        a = _box1d(_box1d(a, r, 0), r, 1)
    return a


def _resize_f(a: np.ndarray, w: int, h: int, resample=Image.Resampling.BICUBIC) -> np.ndarray:
    im = Image.fromarray(np.ascontiguousarray(a, dtype=np.float32), mode="F")
    return np.asarray(im.resize((w, h), resample), dtype=np.float32)


def _norm01(a: np.ndarray) -> np.ndarray:
    lo, hi = float(a.min()), float(a.max())
    return ((a - lo) / (hi - lo + 1e-9)).astype(np.float32)


def fbm(rng, h: int, w: int, cells: int = 4, octaves: int = 4,
        persistence: float = 0.5) -> np.ndarray:
    """值噪声 fBm，返回 [0,1]，双三次上采样，无周期要求。"""
    total = np.zeros((h, w), np.float32)
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        c = max(2, int(round(cells * (2 ** o))))
        g = rng.random((c, c), dtype=np.float32)
        total += np.float32(amp) * _resize_f(g, w, h)
        norm += amp
        amp *= persistence
    return _norm01(total / norm)


def fbm_aniso(rng, h: int, w: int, cells_x: int, cells_y: int,
              octaves: int = 3, persistence: float = 0.5) -> np.ndarray:
    """各向异性 fBm：cells_y >> cells_x 时沿 X 拉丝（cells_x 小 => 竖向条纹）。"""
    total = np.zeros((h, w), np.float32)
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        cx = max(2, int(round(cells_x * (2 ** o))))
        cy = max(2, int(round(cells_y * (2 ** o))))
        g = rng.random((cy, cx), dtype=np.float32)
        total += np.float32(amp) * _resize_f(g, w, h)
        norm += amp
        amp *= persistence
    return _norm01(total / norm)


def prof1d(rng, n: int = 64, smooth: int = 5,
           lo: float = 0.35, hi: float = 1.0) -> np.ndarray:
    """一维平滑起伏，用来调制笔刷沿程的浓淡（避免每段一个固定 alpha 而变成串珠）。"""
    raw = rng.random(n, dtype=np.float32)
    k = max(1, int(smooth))
    raw = np.convolve(raw, np.ones(k, np.float32) / k, mode="same")
    return (lo + (hi - lo) * _norm01(raw)).astype(np.float32)


def alpha_curve(prof: np.ndarray):
    """把 prof1d 的结果变成 stroke_path 需要的 alpha_fn(u)。"""
    xs = np.linspace(0.0, 1.0, len(prof))
    return lambda u: float(np.interp(u, xs, prof))


def lum(rgb: np.ndarray) -> np.ndarray:
    return (0.2126 * rgb[..., 0] + 0.7152 * rgb[..., 1] + 0.0722 * rgb[..., 2]).astype(np.float32)


def tint(base: np.ndarray, color, detail: float = 0.45) -> np.ndarray:
    """把 AI 底图重染成目标色，只保留亮度细节（detail=0 -> 纯平色）。"""
    l = lum(base)
    m = float(l.mean()) + 1e-6
    l = l / m
    l = 1.0 + (l - 1.0) * detail
    col = np.asarray(color, np.float32).reshape(1, 1, 3)
    return np.clip(col * l[..., None], 0.0, 1.0)


def blob_field(rng, cx: float, cy: float, rx: float, ry: float, rot: float = 0.0,
               warp: float = 0.12, cells: int = 5, octaves: int = 4,
               extra: float = 0.0) -> np.ndarray:
    """域扭曲的椭圆半径场 q（q≈1 为边界，<1 在内）。"""
    ca, sa = math.cos(rot), math.sin(rot)
    u = ((XX - cx) * ca + (YY - cy) * sa) / max(rx, 1e-3)
    v = (-(XX - cx) * sa + (YY - cy) * ca) / max(ry, 1e-3)
    q = np.sqrt(u * u + v * v).astype(np.float32)
    n1 = fbm(rng, SIZE, SIZE, cells=cells, octaves=octaves)
    n2 = fbm(rng, SIZE, SIZE, cells=cells + 3, octaves=3)
    q = q + (n1 - 0.5) * 2.0 * warp + (n2 - 0.5) * warp * 0.75
    if extra:
        q = q + (fbm(rng, SIZE, SIZE, cells=2, octaves=2) - 0.5) * extra
    return q


def radial_fade(cx: float, cy: float, r0: float, r1: float) -> np.ndarray:
    d = np.sqrt((XX - cx) ** 2 + (YY - cy) ** 2)
    return 1.0 - smoothstep(r0, r1, d)


def edge_band(mask: np.ndarray, width: float = 1.0) -> np.ndarray:
    """从任意 [0,1] 掩膜提取「边界环带」：4*m*(1-m) 在 m=0.5 处取 1。"""
    b = 4.0 * np.clip(mask, 0.0, 1.0) * (1.0 - np.clip(mask, 0.0, 1.0))
    return np.clip(b * width, 0.0, 1.0).astype(np.float32)


# --------------------------------------------------------------- canvas / brush

class Canvas:
    """预乘 alpha 画布，从软到硬叠加。"""

    def __init__(self, h: int = SIZE, w: int = SIZE):
        self.c = np.zeros((h, w, 3), np.float32)
        self.a = np.zeros((h, w), np.float32)

    def over(self, rgb, alpha) -> None:
        rgb = np.asarray(rgb, np.float32)
        if rgb.ndim == 1:
            rgb = rgb.reshape(1, 1, 3)
        a = np.clip(np.asarray(alpha, np.float32), 0.0, 1.0)
        if a.ndim == 0:
            a = np.full(self.a.shape, float(a), np.float32)
        self.c = rgb * a[..., None] + self.c * (1.0 - a[..., None])
        self.a = a + self.a * (1.0 - a)

    def scale(self, m) -> None:
        """整体衰减不透明度（保持颜色不变）。"""
        m = np.clip(np.asarray(m, np.float32), 0.0, 1.0)
        self.c *= m[..., None]
        self.a *= m

    def result(self) -> Tuple[np.ndarray, np.ndarray]:
        a = self.a
        rgb = self.c / np.maximum(a, 1e-6)[..., None]
        rgb = np.where(a[..., None] > 1e-4, rgb, 0.0)
        return rgb.astype(np.float32), a.astype(np.float32)


SUB = 2  # 每个像素 2×2 亚采样（等效 2× 超采样抗锯齿，不需要整幅渲染到 4×）


def stamp_stroke(cv: Canvas, p0, p1, r0: float, r1: float, color,
                 alpha: float = 1.0, soft: float = 1.15) -> None:
    """粗细渐变的胶囊笔刷：解析距离场 + 2×2 亚采样，边缘永远是软的、没有直角。

    注意颜色只写到「本次 alpha 超过原 alpha」的像素上 —— 早期版本在整个包围盒里
    写 cv.c，重叠绘制时会把矩形包围盒里没被笔刷覆盖的像素也染成该笔刷的颜色，
    在贴花上表现为一块块硬边方形色斑。
    """
    h, w = cv.a.shape
    x0, y0 = float(p0[0]), float(p0[1])
    x1, y1 = float(p1[0]), float(p1[1])
    if alpha <= 0.002:
        return
    rmax = max(r0, r1)
    pad = rmax + 3.0
    xa = int(max(0, math.floor(min(x0, x1) - pad)))
    xb = int(min(w, math.ceil(max(x0, x1) + pad)))
    ya = int(max(0, math.floor(min(y0, y1) - pad)))
    yb = int(min(h, math.ceil(max(y0, y1) + pad)))
    if xb <= xa or yb <= ya:
        return
    xs = np.arange(xa, xb, dtype=np.float32) + 0.5
    ys = np.arange(ya, yb, dtype=np.float32) + 0.5
    dx, dy = x1 - x0, y1 - y0
    L2 = dx * dx + dy * dy
    acc = np.zeros((len(ys), len(xs)), np.float32)
    off = np.linspace(0.5 / SUB - 0.5, 0.5 - 0.5 / SUB, SUB, dtype=np.float32)
    for oy in off:
        for ox in off:
            X = xs[None, :] - x0 + ox
            Y = ys[:, None] - y0 + oy
            t = np.clip((X * dx + Y * dy) / (L2 + 1e-9), 0.0, 1.0)
            d = np.sqrt((X - t * dx) ** 2 + (Y - t * dy) ** 2)
            r = r0 + (r1 - r0) * t
            acc += np.clip((r - d) / max(soft, 1e-3) + 0.5, 0.0, 1.0)
    a = (acc / (SUB * SUB)) * alpha
    sub_a = cv.a[ya:yb, xa:xb]
    old = sub_a.copy()
    take = a > old
    np.maximum(sub_a, a, out=sub_a)
    col = np.asarray(color, np.float32).reshape(3)
    sub_c = cv.c[ya:yb, xa:xb]
    sub_c[take] = col * a[take][:, None]


def stamp_blob(cv: Canvas, x: float, y: float, rx: float, ry: float, rot: float,
               color, alpha: float = 1.0) -> None:
    """软椭圆斑：径向衰减 (1 - (d/r)^2)^2，可任意旋转 —— 撒点/孢子/锈珠都用它。"""
    h, w = cv.a.shape
    R = max(rx, ry) + 2.0
    xa = int(max(0, math.floor(x - R)))
    xb = int(min(w, math.ceil(x + R)))
    ya = int(max(0, math.floor(y - R)))
    yb = int(min(h, math.ceil(y + R)))
    if xb <= xa or yb <= ya:
        return
    xs = np.arange(xa, xb, dtype=np.float32) + 0.5 - x
    ys = np.arange(ya, yb, dtype=np.float32) + 0.5 - y
    ca, sa = math.cos(rot), math.sin(rot)
    u = (xs[None, :] * ca + ys[:, None] * sa) / max(rx, 1e-3)
    v = (-xs[None, :] * sa + ys[:, None] * ca) / max(ry, 1e-3)
    a = np.clip(1.0 - (u * u + v * v), 0.0, 1.0) ** 2 * alpha
    sub_a = cv.a[ya:yb, xa:xb]
    old = sub_a.copy()
    take = a > old
    np.maximum(sub_a, a, out=sub_a)
    col = np.asarray(color, np.float32).reshape(3)
    sub_c = cv.c[ya:yb, xa:xb]
    sub_c[take] = col * a[take][:, None]


def stamp_dot(cv: Canvas, x: float, y: float, r: float, color, alpha: float = 1.0) -> None:
    stamp_blob(cv, x, y, r, r, 0.0, color, alpha)


def stroke_path(cv: Canvas, pts, w0: float, w1: float, color, alpha: float = 1.0,
                soft: float = 1.0, alpha_fn: Callable[[float], float] = None,
                taper: float = 0.0) -> None:
    """沿折线画粗细渐变的笔刷；taper 让中段略粗（像真的裂纹）。"""
    n = len(pts) - 1
    if n < 1:
        return
    for i in range(n):
        u = i / n
        u2 = min(1.0, (i + 1) / n)
        wa = w0 + (w1 - w0) * u
        wb = w0 + (w1 - w0) * u2
        if taper:
            wa *= 1.0 + taper * (math.sin(math.pi * u) - 0.5)
            wb *= 1.0 + taper * (math.sin(math.pi * u2) - 0.5)
        al = alpha if alpha_fn is None else alpha * float(alpha_fn(u))
        if al <= 0.002:
            continue
        stamp_stroke(cv, pts[i], pts[i + 1], max(0.22, wa), max(0.22, wb), color, al, soft)


def crack(cv: Canvas, rng, x: float, y: float, ang: float, length: float,
          w0: float, w1: float, color, alpha: float = 1.0, steps: int = 8,
          wander: float = 0.16, depth: int = 1, branch_p: float = 0.45,
          halo=None, halo_alpha: float = 0.0,
          glow=None, glow_alpha: float = 0.0, soft: float = 1.0) -> None:
    """分叉裂纹：主走向随机游走 + 三层（晕/唇口高光/暗芯），子裂纹在中段分叉。"""
    a = float(ang)
    pts = [(float(x), float(y))]
    seg = length / max(1, steps)
    px, py = float(x), float(y)
    for _ in range(steps):
        a += float(rng.normal(0.0, wander))
        px += math.cos(a) * seg
        py += math.sin(a) * seg
        pts.append((px, py))
    if halo is not None and halo_alpha > 0.0:
        stroke_path(cv, pts, w0 * 3.0 + 1.2, w1 * 3.0 + 1.0, halo, halo_alpha,
                    soft=max(1.3, w0 * 0.9), taper=0.7)
    if glow is not None and glow_alpha > 0.0:
        gp = [(p[0] + 1.2, p[1] + 1.6) for p in pts]
        stroke_path(cv, gp, max(0.4, w0 * 0.55), max(0.3, w1 * 0.5), glow,
                    glow_alpha, soft=0.9)
    stroke_path(cv, pts, w0, w1, color, alpha, soft=max(0.9, soft),
                alpha_fn=lambda u: 1.0 - 0.5 * u, taper=0.45)
    if depth > 0:
        for i in range(1, steps):
            if rng.random() < branch_p * (1.0 - 0.55 * i / steps):
                ba = a + (1.0 if rng.random() < 0.5 else -1.0) * float(rng.uniform(0.5, 1.25))
                crack(cv, rng, pts[i][0], pts[i][1], ba, length * 0.45,
                      max(0.7, w0 * 0.6), 0.22, color, alpha * 0.78, steps=4,
                      wander=0.30, depth=depth - 1, branch_p=0.18,
                      halo=halo, halo_alpha=halo_alpha * 0.75,
                      glow=glow, glow_alpha=glow_alpha * 0.7)


def mask_streaks(fn: Callable[[Canvas], None]) -> np.ndarray:
    """在临时画布上把笔刷叠成一张 [0,1] 掩膜，之后可以用噪声整体调制再一次性合成。

    直接逐段 stamp 会在段与段之间出现 alpha 跳变（像串珠）；先做掩膜、
    再乘一个平滑噪声场，才能得到连续的浓淡过渡。
    """
    tmp = Canvas()
    fn(tmp)
    return tmp.a


def composite(cv: Canvas, mask, rgb, alpha: float = 1.0) -> None:
    """把掩膜乘 alpha 后一次性叠加到画布（rgb 可以是 (3,) 也可以是 HxWx3 数组）。"""
    a = np.clip(np.asarray(mask, np.float32) * alpha, 0.0, 1.0)
    cv.over(rgb, a)


def specks(cv: Canvas, rng, n: int, cx: float, cy: float, spread: float,
           rmin: float, rmax: float, color, alpha: float = 0.6,
           jitter: float = 1.0) -> None:
    """散点（粉尘/碎屑/孢子）：软椭圆 + 随机旋转，尺寸 1–4px 抖动，没有直角边。"""
    for _ in range(n):
        ang = float(rng.random()) * math.tau
        rad = spread * math.sqrt(float(rng.random()))
        x = cx + math.cos(ang) * rad * jitter
        y = cy + math.sin(ang) * rad
        r = rmin + (rmax - rmin) * float(rng.random()) ** 2
        ry = r * float(rng.uniform(0.55, 1.45))
        stamp_blob(cv, x, y, r, ry, float(rng.random()) * math.pi, color,
                   alpha * (0.35 + 0.65 * float(rng.random())))


def vein_paths(rng, x: float, y: float, ang: float, seg: float, n: int,
               wander: float) -> List[Tuple[float, float]]:
    """生成一条随机游走的折线（可复用给多层绘制）。"""
    pts = [(float(x), float(y))]
    a = float(ang)
    px, py = float(x), float(y)
    for _ in range(n):
        a += float(rng.normal(0.0, wander))
        px += math.cos(a) * seg
        py += math.sin(a) * seg
        pts.append((px, py))
    return pts


def finalize(cv: Canvas, sigma: float) -> Tuple[np.ndarray, np.ndarray]:
    rgb, a = cv.result()
    a = blur(a, sigma)
    d = np.minimum(np.minimum(XX, YY), np.minimum(SIZE - XX, SIZE - YY))
    a = a * smoothstep(BORDER_CLEAR_PX, BORDER_FADE_PX, d)
    return np.clip(rgb, 0.0, 1.0), np.clip(a, 0.0, 1.0)


# ------------------------------------------------------------- ComfyUI (SDXL)

MAT_SUFFIX = ("extreme close-up macro photograph of the bare material surface, "
              "high frequency fine detail, matte, dead flat even diffuse lighting, "
              "photorealistic PBR albedo map, orthographic top view, "
              "no shadows, no objects, no geometry")

BASE_PROMPTS: Dict[str, Tuple[str, str]] = {
    "concrete": (
        "raw trowelled light grey concrete surface, fine cement grain, pitted air "
        "pores, dust film mottling, faint trowel swirl marks, chalky, " + MAT_SUFFIX,
        "cracks, stains, debris, 3d scene, perspective, shadow, vignette, text, "
        "watermark, border, frame, blurry, objects, tiles, pattern, repeat"),
    "metal": (
        "matte grey painted steel sheet, fine orange-peel paint texture, faint "
        "abrasion dust, tiny scratch haze, uniform tone, " + MAT_SUFFIX,
        "rust, holes, reflections, glare, 3d scene, perspective, shadow, vignette, "
        "text, watermark, border, frame, objects, rivets, seams, tiles"),
    "glass": (
        "frosted glass pane surface, ultrafine etched grain, faint dried water "
        "spots, uniform pale cool grey, dull satin finish, " + MAT_SUFFIX,
        "cracks, reflections, glare, light streaks, window frame, mullion, "
        "rectangle, door, handle, 3d scene, perspective, shadow, vignette, text, "
        "watermark, border, frame, objects, tiles, pattern"),
    "water": (
        "dried water damaged pale plaster, chalky mineral tide marks, powdery "
        "grey brown deposits, patchy mottling, " + MAT_SUFFIX,
        "3d scene, perspective, shadow, hard lighting, vignette, text, watermark, "
        "border, frame, objects, furniture, tiles, pattern, black mold, cracks"),
    "mold": (
        "dense colony of tiny black and dark green mold spots on damp pale grey "
        "plaster wall, speckled clusters of different sizes, fuzzy diffuse edges, "
        "flat powdery growth on the surface, low saturation, " + MAT_SUFFIX,
        "3d scene, perspective, shadow, bright colors, text, watermark, border, "
        "frame, objects, mushroom, moss, lichen, rock, stone, boulder, pebbles, "
        "sculpture, rounded lumps, 3d bumps, blurry blobs, tiles, pattern"),
    "plaster": (
        "chalky off white plaster skim coat, fine hairline crazing, dusty matte "
        "finish, faint trowel texture, " + MAT_SUFFIX,
        "3d scene, perspective, shadow, hard lighting, vignette, text, watermark, "
        "border, frame, objects, holes, deep cracks, tiles, pattern"),
    "rust": (
        "heavily rusted steel, orange brown iron oxide flakes, layered powdery "
        "scale, desaturated earth tones, " + MAT_SUFFIX,
        "3d scene, perspective, shadow, hard lighting, vignette, text, watermark, "
        "border, frame, objects, bolts, plates, holes, tiles, pattern"),
    "oil": (
        "thin dark used oil film on grey concrete, matte sludge grain, faint thin "
        "sheen patches, sooty sediment, " + MAT_SUFFIX,
        "3d scene, perspective, shadow, hard lighting, vignette, text, watermark, "
        "border, frame, objects, puddle reflection, sky, colorful, tiles, pattern"),
    "dirt": (
        "dry brown mud and dust smeared on pale grey floor, fine grit particles, "
        "powdery deposit, low saturation earth tones, " + MAT_SUFFIX,
        "3d scene, perspective, shadow, hard lighting, vignette, text, watermark, "
        "border, frame, objects, footprints, shoes, tiles, pattern"),
    "stain": (
        "dried dark brown coffee residue on light surface, ring sediment, fine "
        "dried film with tide lines, " + MAT_SUFFIX,
        "3d scene, perspective, shadow, hard lighting, vignette, text, watermark, "
        "border, frame, objects, cup, table, coffee beans, tiles, pattern"),
    "soot": (
        "soot and smoke deposit on pale wall, black grey powdery gradient, fine "
        "ash particles, soft edges, " + MAT_SUFFIX,
        "3d scene, perspective, shadow, fire, flame, hard lighting, vignette, "
        "text, watermark, border, frame, objects, tiles, pattern"),
    "paint": (
        "peeling white paint film on grey substrate, chipped flake edges, crazed "
        "cracked paint surface, matte, " + MAT_SUFFIX,
        "3d scene, perspective, shadow, hard lighting, vignette, text, watermark, "
        "border, frame, objects, brush, wall, tiles, pattern"),
    "scuff": (
        "fine dark grey rubber dust and dry abrasion smudge on pale grey painted "
        "surface, soft powdery deposit, faint thin smear, uncorrelated grain, "
        + MAT_SUFFIX,
        "3d scene, perspective, shadow, hard lighting, vignette, text, watermark, "
        "border, frame, objects, shoes, foot, stripes, waves, flowing lines, "
        "repeated pattern, high contrast, tiles"),
}


def _post(path: str, data=None, timeout: float = 900.0):
    req = urllib.request.Request(
        COMFY + path, data=data,
        headers={"Content-Type": "application/json"} if data else {})
    with urllib.request.urlopen(req, timeout=timeout) as f:
        return json.load(f)


def _build_workflow(positive: str, negative: str, seed: int, prefix: str) -> dict:
    return {
        "1": {"class_type": "CheckpointLoaderSimple",
              "inputs": {"ckpt_name": "sd_xl_base_1.0.safetensors"}},
        "3": {"class_type": "CLIPTextEncode", "inputs": {"text": positive, "clip": ["1", 1]}},
        "4": {"class_type": "CLIPTextEncode", "inputs": {"text": negative, "clip": ["1", 1]}},
        "5": {"class_type": "EmptyLatentImage",
              "inputs": {"width": 1024, "height": 1024, "batch_size": 1}},
        "6": {"class_type": "KSampler",
              "inputs": {"model": ["1", 0], "positive": ["3", 0], "negative": ["4", 0],
                         "latent_image": ["5", 0], "seed": seed, "steps": 24, "cfg": 6.5,
                         "sampler_name": "dpmpp_2m", "scheduler": "karras", "denoise": 1.0}},
        "8": {"class_type": "VAEDecode", "inputs": {"samples": ["6", 0], "vae": ["1", 2]}},
        "9": {"class_type": "SaveImage",
              "inputs": {"images": ["8", 0], "filename_prefix": prefix}},
    }


def _comfy_alive() -> bool:
    try:
        with urllib.request.urlopen(COMFY + "/system_stats", timeout=4) as f:
            return f.status == 200
    except Exception:
        return False


def generate_base(key: str, force: bool = False) -> str:
    """生成（或复用缓存）一张 AI 材质底图，返回本地缓存路径。"""
    os.makedirs(BASE_CACHE, exist_ok=True)
    dst = os.path.join(BASE_CACHE, "base_%s.png" % key)
    if os.path.exists(dst) and not force:
        return dst
    if not _comfy_alive():
        raise RuntimeError(
            "ComfyUI 未运行且缺少底图缓存 %s\n启动：\n"
            r"  F:\ComfyUI\venv\Scripts\python.exe F:\ComfyUI\main.py --listen 127.0.0.1 "
            r"--port 8188 --disable-auto-launch" % dst)
    pos, neg = BASE_PROMPTS[key]
    seed = SEED + zlib.crc32(("base:" + key).encode("utf-8")) % 100000
    wf = _build_workflow(pos, neg, seed, "decals/base_%s" % key)
    print("  [AI] %-9s seed=%-8d ..." % (key, seed), end="", flush=True)
    t0 = time.time()
    pid = _post("/prompt", json.dumps({"prompt": wf}).encode())["prompt_id"]
    while time.time() - t0 < 900:
        time.sleep(1.5)
        with urllib.request.urlopen("%s/history/%s" % (COMFY, pid), timeout=60) as f:
            hist = json.load(f)
        e = hist.get(pid)
        if not e:
            continue
        st = e.get("status", {})
        if st.get("status_str") == "error":
            raise RuntimeError("ComfyUI 报错 %s: %s" % (key, st.get("messages")))
        if st.get("completed"):
            img = None
            for node in e.get("outputs", {}).values():
                if node.get("images"):
                    img = node["images"][0]
                    break
            if img is None:
                raise RuntimeError("ComfyUI 没有输出图片: %s" % key)
            q = urllib.parse.urlencode({"filename": img["filename"],
                                        "subfolder": img.get("subfolder", ""),
                                        "type": img.get("type", "output")})
            with urllib.request.urlopen("%s/view?%s" % (COMFY, q), timeout=120) as f:
                raw = f.read()
            with open(dst, "wb") as fh:
                fh.write(raw)
            print(" ok %.1fs" % (time.time() - t0))
            return dst
    raise RuntimeError("ComfyUI 超时: %s" % key)


def placeholder_base(key: str) -> np.ndarray:
    rng = np.random.default_rng(SEED + zlib.crc32(("ph:" + key).encode()) % 100000)
    n = fbm(rng, SIZE, SIZE, cells=3, octaves=5)
    g = 0.5 + (n - 0.5) * 0.5
    return np.repeat(g[..., None], 3, axis=2).astype(np.float32)


def load_bases(keys, force=False, no_ai=False, verbose=True) -> Dict[str, np.ndarray]:
    out: Dict[str, np.ndarray] = {}
    for k in keys:
        dst = os.path.join(BASE_CACHE, "base_%s.png" % k)
        if no_ai or (not force and not os.path.exists(dst) and not _comfy_alive()):
            if verbose:
                print("  [--] %-9s 用占位噪声（无 AI 底图）" % k)
            out[k] = placeholder_base(k)
            continue
        path = generate_base(k, force=force)
        im = Image.open(path).convert("RGB").resize((SIZE, SIZE), Image.Resampling.LANCZOS)
        out[k] = np.asarray(im, dtype=np.float32) / 255.0
    return out


# ------------------------------------------------------------------- builders
# 每个 builder 返回 Canvas；调用方负责 finalize(feather)。
# 约定：形状从软到硬叠加，颜色只表达「污渍本体」，不烤死墙面颜色。

def b_hole_concrete(B, rng) -> Canvas:
    """混凝土弹孔：粉尘晕 -> 崩边亮环 -> 孔唇暗环 -> 孔芯 -> 放射裂纹 -> 碎屑。"""
    cv = Canvas()
    cx, cy = 250.0, 258.0
    base = B["concrete"]
    mot = fbm(rng, SIZE, SIZE, cells=4, octaves=4)
    fine = fbm(rng, SIZE, SIZE, cells=14, octaves=3)

    # 1) 粉尘光晕（最软，由中心向外衰减，峰很淡）
    q = blob_field(rng, cx, cy, 152, 146, warp=0.30, cells=4, octaves=3)
    dust = (1.0 - smoothstep(0.22, 1.0, q)) * (0.30 + 0.70 * mot)
    cv.over(tint(base, (0.72, 0.71, 0.68), 0.55), dust * 0.22)

    # 2) 崩边环（新茬混凝土，偏亮、边缘破碎；高频只做轻微浓淡，不要切成碎片）
    qs = blob_field(rng, cx, cy, 88, 82, warp=0.34, cells=6)
    spall = ((1.0 - smoothstep(0.46, 1.00, qs)) * smoothstep(0.08, 0.40, qs)
             * (0.70 + 0.30 * fine))
    cv.over(tint(base, (0.74, 0.73, 0.69), 0.60), spall * 0.92)

    # 3) 孔唇：外亮内暗的立体边
    q1 = blob_field(rng, cx, cy, 56, 52, warp=0.20, cells=7)
    rim_hi = np.exp(-((q1 - 0.88) / 0.11) ** 2)
    cv.over(tint(base, (0.82, 0.81, 0.77), 0.55), rim_hi * 0.78)
    q2 = blob_field(rng, cx, cy, 41, 39, warp=0.22, cells=7)
    lip = np.exp(-((q2 - 0.97) / 0.11) ** 2)
    cv.over((0.135, 0.130, 0.122), lip * 0.92)

    # 4) 孔芯（近黑，内壁一点点冷反光）
    hole = 1.0 - smoothstep(0.78, 1.02, q2)
    cv.over((0.032, 0.031, 0.029), hole)
    cv.over((0.085, 0.083, 0.080), hole * np.exp(-((q2 - 0.60) / 0.24) ** 2) * 0.55)

    # 5) 放射裂纹（主 + 分叉，带粉化晕与高光唇）
    n_cr = 7
    for i in range(n_cr):
        ang = (i / n_cr) * math.tau + float(rng.uniform(-0.24, 0.24))
        ln = float(rng.uniform(58, 148))
        crack(cv, rng, cx + math.cos(ang) * 38, cy + math.sin(ang) * 38, ang, ln,
              float(rng.uniform(2.4, 3.4)), 0.30, (0.195, 0.190, 0.180),
              alpha=0.94, steps=9, wander=0.055, depth=1, branch_p=0.32,
              halo=(0.74, 0.73, 0.69), halo_alpha=0.10,
              glow=(0.86, 0.85, 0.81), glow_alpha=0.10)
    # 细碎发丝裂纹（更淡、更抖）
    for _ in range(16):
        ang = float(rng.random()) * math.tau
        r0 = float(rng.uniform(34, 96))
        crack(cv, rng, cx + math.cos(ang) * r0, cy + math.sin(ang) * r0, ang,
              float(rng.uniform(16, 52)), 1.0, 0.18, (0.30, 0.29, 0.27),
              alpha=0.40, steps=3, wander=0.36, depth=0)

    # 6) 碎屑与粉尘散点
    specks(cv, rng, 60, cx, cy, 118, 0.5, 2.0, (0.74, 0.73, 0.69), 0.26)
    specks(cv, rng, 34, cx, cy, 76, 0.9, 3.2, (0.30, 0.29, 0.27), 0.46)
    return cv


def b_hole_metal(B, rng) -> Canvas:
    """金属弹孔：漆面崩落 -> 塑性凹坑（单向明暗）-> 孔 + 金属翻边高光 -> 擦痕。"""
    cv = Canvas()
    cx, cy = 252.0, 256.0
    base = B["metal"]
    mot = fbm(rng, SIZE, SIZE, cells=5, octaves=4)
    fine = fbm(rng, SIZE, SIZE, cells=22, octaves=3)
    ang = np.arctan2(YY - cy, XX - cx)

    # 1) 漆面崩落（不规则灰白区，比凹坑大一圈；外缘快点收掉，别拖出一大片淡雾）
    qp = blob_field(rng, cx, cy, 98, 92, rot=0.25, warp=0.30, cells=5)
    chip = (1.0 - smoothstep(0.28, 0.88, qp)) * (0.55 + 0.45 * mot)
    chip = np.clip(chip * (0.80 + 0.28 * fine), 0.0, 1.0)
    cv.over(tint(base, (0.68, 0.68, 0.67), 0.50), chip * 0.68)

    # 2) 塑性凹坑：环状明暗，光假设在左上（cos(ang-3.9)）
    qd = blob_field(rng, cx - 3, cy + 5, 64, 57, rot=0.25, warp=0.22, cells=6)
    lit = np.clip(0.5 + 0.5 * np.cos(ang - 3.95), 0.0, 1.0)
    shade = np.exp(-((qd - 0.95) / 0.14) ** 2) * lit
    cv.over(tint(base, (0.40, 0.40, 0.41), 0.45), shade * 0.62)
    hi = np.exp(-((qd - 0.88) / 0.10) ** 2) * (1.0 - lit)
    cv.over(tint(base, (0.72, 0.72, 0.71), 0.40), hi * 0.45)

    # 3) 孔芯 + 亮金属翻边
    qh = blob_field(rng, cx, cy, 27, 23, rot=0.4, warp=0.30, cells=7)
    hole = 1.0 - smoothstep(0.80, 1.04, qh)
    cv.over((0.022, 0.022, 0.024), hole)
    rim = np.exp(-((qh - 0.99) / 0.15) ** 2)
    cv.over((0.87, 0.87, 0.85), rim * 0.78 * (0.35 + 0.65 * lit))
    cv.over((0.95, 0.95, 0.93), rim * 0.48 * np.clip(0.5 + 0.5 * np.cos(ang - 2.1), 0, 1))

    # 4) 放射细裂 + 弹道擦痕（方向性很强，只向弹道出口一侧偏）
    for i in range(6):
        a = (i / 6) * math.tau + float(rng.uniform(-0.32, 0.32))
        crack(cv, rng, cx + math.cos(a) * 24, cy + math.sin(a) * 24, a,
              float(rng.uniform(30, 68)), 1.5, 0.22, (0.42, 0.42, 0.41),
              alpha=0.56, steps=5, wander=0.20, depth=1, branch_p=0.28,
              halo=(0.62, 0.62, 0.61), halo_alpha=0.13)
    for _ in range(7):
        a = float(rng.uniform(0.15, 1.15))
        r0 = float(rng.uniform(26, 58))
        stamp_stroke(cv, (cx + math.cos(a) * r0, cy + math.sin(a) * r0),
                     (cx + math.cos(a) * (r0 + float(rng.uniform(34, 62))),
                      cy + math.sin(a) * (r0 + float(rng.uniform(34, 62)))),
                     1.2, 0.14, (0.74, 0.74, 0.72), 0.16, soft=0.9)

    specks(cv, rng, 40, cx, cy, 92, 0.4, 1.7, (0.68, 0.68, 0.66), 0.28)
    return cv


def b_hole_glass(B, rng) -> Canvas:
    """玻璃弹孔：近直线放射裂纹 + 同心蛛网弧 + 发白粉碎核心。"""
    cv = Canvas()
    cx, cy = 253.0, 257.0
    base = B["glass"]
    mot = fbm(rng, SIZE, SIZE, cells=6, octaves=4)

    # 1) 玻璃表面的脏污盘（极淡，只为了不浮在玻璃上）
    qg = blob_field(rng, cx, cy, 168, 164, warp=0.26, cells=4, octaves=3)
    cv.over(tint(base, (0.80, 0.82, 0.84), 0.30),
            (1.0 - smoothstep(0.18, 1.0, qg)) * (0.06 + 0.05 * mot))

    # 2) 径向裂纹：低抖动 => 真正的放射状；每条配一条暗线做蚀刻层次
    n = 11
    angles = []
    for i in range(n):
        a = (i / n) * math.tau + float(rng.uniform(-0.26, 0.26))
        angles.append(a)
        # 长度必须参差：整圈等长就会像绘制的蛛网
        ln = float(rng.uniform(48, 190)) if rng.random() < 0.45 else float(rng.uniform(120, 196))
        w0 = float(rng.uniform(2.0, 3.6))
        pts = vein_paths(rng, cx + math.cos(a) * 20, cy + math.sin(a) * 20,
                         a, ln / 12.0, 12, 0.055)
        if rng.random() < 0.32:
            # 贯穿裂纹：透出玻璃后面的暗色，不是一条白线
            stroke_path(cv, [(p[0] + 1.1, p[1] + 0.8) for p in pts],
                        w0 * 1.5, 0.34, (0.74, 0.77, 0.80), 0.22, soft=1.3)
            stroke_path(cv, pts, w0 * 0.9, 0.16, (0.30, 0.33, 0.37), 0.62, soft=0.85,
                        alpha_fn=lambda u: 1.0 - 0.60 * u, taper=0.32)
        else:
            stroke_path(cv, [(p[0] + 1.3, p[1] + 0.9) for p in pts],
                        w0 * 1.7, 0.34, (0.50, 0.54, 0.58), 0.26, soft=1.3)
            stroke_path(cv, pts, w0, 0.18, (0.93, 0.95, 0.965), 0.93, soft=0.85,
                        alpha_fn=lambda u: 1.0 - 0.50 * u, taper=0.32)
        # 短支裂纹（只在靠近核心处）
        if rng.random() < 0.6:
            k = int(rng.uniform(2, 5))
            if k < len(pts):
                ba = a + (1.0 if rng.random() < 0.5 else -1.0) * float(rng.uniform(0.5, 0.95))
                crack(cv, rng, pts[k][0], pts[k][1], ba, float(rng.uniform(12, 30)),
                      1.0, 0.16, (0.90, 0.93, 0.95), alpha=0.50, steps=3,
                      wander=0.16, depth=0)

    # 3) 同心蛛网弧：半径抖动 + 随机断口（断裂概率高一些，别成完整同心圆）
    for ring, rr in enumerate((40.0, 74.0, 108.0, 142.0)):
        rr += float(rng.uniform(-7, 7))
        for i in range(n):
            a0, a1 = angles[i], angles[(i + 1) % n]
            if a1 < a0:
                a1 += math.tau
            if rng.random() < 0.40:
                continue
            sub = 6
            prev = None
            for s in range(sub + 1):
                t = s / sub
                a = a0 + (a1 - a0) * t
                r = rr + float(rng.normal(0, 2.4)) + math.sin(t * math.pi) * 3.0
                p = (cx + math.cos(a) * r, cy + math.sin(a) * r)
                if prev is not None:
                    stamp_stroke(cv, prev, p, 0.58, 0.58, (0.90, 0.93, 0.95),
                                 0.44, soft=0.85)
                prev = p

    # 4) 粉碎核心（发白玻璃屑）+ 中心穿透小孔
    qc = blob_field(rng, cx, cy, 35, 31, warp=0.34, cells=7)
    core = (1.0 - smoothstep(0.45, 1.0, qc)) * (0.6 + 0.4 * mot)
    cv.over(tint(base, (0.94, 0.96, 0.97), 0.30), core * 0.95)
    qh = blob_field(rng, cx, cy, 8, 7, warp=0.38, cells=8)
    cv.over((0.055, 0.065, 0.075), (1.0 - smoothstep(0.45, 1.0, qh)) * 0.95)

    # 5) 玻璃屑亮点
    specks(cv, rng, 70, cx, cy, 105, 0.35, 1.5, (0.97, 0.985, 1.0), 0.50)
    specks(cv, rng, 26, cx, cy, 60, 0.5, 1.6, (0.52, 0.55, 0.60), 0.28)
    return cv


def b_water_stain(B, rng) -> Canvas:
    """渗水痕：三个团块的并集形状 + 潮线 + 竖向流挂 + 水碱白霜。"""
    cv = Canvas()
    cx, cy = 250.0, 246.0
    base = B["water"]
    mot = fbm(rng, SIZE, SIZE, cells=5, octaves=5)
    fine = fbm(rng, SIZE, SIZE, cells=16, octaves=3)

    # 1) 形状：竖向拉长的主团 + 下方两个小凸起（真实渗水不是一个椭圆）
    q1 = blob_field(rng, cx + 2, cy - 24, 124, 136, warp=0.58, cells=5, octaves=3)
    q2 = blob_field(rng, cx + 40, cy + 92, 66, 62, warp=0.54, cells=5, octaves=3)
    q3 = blob_field(rng, cx - 56, cy + 48, 54, 68, warp=0.56, cells=5, octaves=3)
    m1 = 1.0 - smoothstep(0.52, 1.02, q1)
    m2 = 1.0 - smoothstep(0.48, 1.0, q2)
    m3 = 1.0 - smoothstep(0.48, 1.0, q3)
    body = np.maximum.reduce([m1, m2, m3]).astype(np.float32)
    body = np.clip(body * (0.62 + 0.55 * fine), 0.0, 1.0)

    # 1b) 上缘更深（水是从上方渗下来的，顶部积得最厚）
    body *= np.clip(1.22 - (YY - cy) / 260.0, 0.0, 1.0)

    # 2) 外圈极淡的潮气晕（边缘外侧的扩散）
    halo = np.clip(blur(body, 9.0) - body, 0.0, 1.0) * 3.0
    cv.over(tint(base, (0.50, 0.48, 0.43), 0.40), halo * (0.10 + 0.10 * mot))

    # 3) 主体（中等浓度）+ 内芯加深
    cv.over(tint(base, (0.42, 0.40, 0.35), 0.50), body * (0.26 + 0.18 * mot))
    core = blur(body, 11.0)
    cv.over(tint(base, (0.35, 0.33, 0.29), 0.45), np.clip(core - 0.58, 0, 1) * 0.45)

    # 4) 潮线：沿边界的深色环带（干涸后矿物质堆积），要断续、不能是一条黑线圈
    tide = edge_band(body, 1.0) * (0.24 + 0.92 * mot)
    cv.over(tint(base, (0.30, 0.28, 0.24), 0.38), tide * 0.55)
    inner_tide = edge_band(np.clip(core * 1.25, 0, 1), 1.0) * 0.45
    cv.over(tint(base, (0.33, 0.31, 0.27), 0.30), inner_tide * 0.32)

    # 5) 指状竖向拖尾：从形状下缘往下流，越往下越细越淡（不是纯高斯模糊的圆团）
    def _tails(t: Canvas) -> None:
        for _ in range(7):
            xx = cx + float(rng.uniform(-104, 104))
            yy = cy + float(rng.uniform(48, 128))
            ln = float(rng.uniform(70, 200))
            w0 = float(rng.uniform(6.0, 13.0))
            pts = [(xx + math.sin(s / 12.0 * 3.2 + xx * 0.05) * 5.5, yy + ln * s / 12.0)
                   for s in range(13)]
            stroke_path(t, pts, w0, 0.5, (1.0, 1.0, 1.0), 1.0, soft=2.2,
                        alpha_fn=lambda u: (1.0 - u) ** 1.2)
    tails = mask_streaks(_tails)
    an = fbm_aniso(rng, SIZE, SIZE, 40, 5, octaves=3)
    composite(cv, tails * (0.22 + 0.36 * an), tint(base, (0.40, 0.38, 0.33), 0.42))

    # 5b) 大范围的潮气拖痕（更宽更淡，给"往下渗"的方向感）
    def _runs(t: Canvas) -> None:
        for _ in range(6):
            x = cx + float(rng.uniform(-96, 96))
            y0 = cy + float(rng.uniform(30, 120))
            ln = float(rng.uniform(50, 140))
            stamp_stroke(t, (x, y0), (x + float(rng.uniform(-8, 8)), y0 + ln),
                         float(rng.uniform(6, 14)), float(rng.uniform(2, 6)),
                         (1.0, 1.0, 1.0), 1.0, soft=7.0)
    runs = mask_streaks(_runs)
    composite(cv, runs * (0.07 + 0.18 * an), tint(base, (0.44, 0.42, 0.37), 0.40))

    # 6) 水碱白霜（局部发白的矿化斑）
    for _ in range(5):
        qw = blob_field(rng, cx + float(rng.uniform(-96, 96)),
                        cy + float(rng.uniform(-86, 86)),
                        float(rng.uniform(22, 50)), float(rng.uniform(18, 38)),
                        warp=0.44, cells=7)
        cv.over(tint(base, (0.68, 0.66, 0.60), 0.45),
                (1.0 - smoothstep(0.25, 1.0, qw)) * 0.13)
    return cv


def b_mold_patch(B, rng) -> Canvas:
    """霉斑：潮气晕 + 主霉簇（锯齿状边缘）+ 中/小簇 + 孢子散点。"""
    cv = Canvas()
    cx, cy = 250.0, 250.0
    base = B["water"]
    grain = B["mold"]
    mot = fbm(rng, SIZE, SIZE, cells=6, octaves=5)
    fine = fbm(rng, SIZE, SIZE, cells=22, octaves=3)

    # 1) 潮气晕（灰绿，很淡，多团并集）
    halo = np.zeros((SIZE, SIZE), np.float32)
    for _ in range(6):
        hx = cx + float(rng.uniform(-92, 92))
        hy = cy + float(rng.uniform(-92, 92))
        qh = blob_field(rng, hx, hy, float(rng.uniform(64, 118)),
                        float(rng.uniform(58, 108)), warp=0.38, cells=4, octaves=3)
        halo = np.maximum(halo, 1.0 - smoothstep(0.10, 1.0, qh))
    cv.over(tint(base, (0.42, 0.44, 0.36), 0.40), halo * (0.14 + 0.12 * mot))

    # 2) 主霉簇（3 团，边缘用高频噪声咬碎 => 绒毛感，互相叠成一片而不是几个球）
    for i in range(3):
        mx = cx + (i - 1) * 46.0 + float(rng.uniform(-18, 18))
        my = cy + (i % 2) * 40.0 - 24.0 + float(rng.uniform(-14, 14))
        r = float(rng.uniform(62, 96))
        qm = blob_field(rng, mx, my, r, r * float(rng.uniform(0.70, 0.95)),
                        rot=float(rng.uniform(0, math.pi)), warp=0.52, cells=7)
        a = (1.0 - smoothstep(0.16, 1.0, qm)) * (0.40 + 0.60 * mot)
        a = np.clip(a * (0.42 + 1.05 * fine), 0.0, 1.0)
        cv.over(tint(grain, (0.150, 0.170, 0.120), 0.55), a * 0.86)
        cv.over((0.070, 0.080, 0.058), np.clip((1.0 - smoothstep(0.08, 0.60, qm)), 0, 1) * 0.58)

    # 3) 中/小簇（沿边缘扩散，数量多才像成片的霉）
    for _ in range(62):
        mx = cx + float(rng.uniform(-186, 186))
        my = cy + float(rng.uniform(-174, 174))
        r = float(rng.uniform(2.5, 20))
        qm = blob_field(rng, mx, my, r, r * float(rng.uniform(0.60, 1.0)),
                        warp=0.56, cells=8)
        a = (1.0 - smoothstep(0.16, 1.0, qm)) * float(rng.uniform(0.50, 0.94))
        cv.over(tint(grain, (0.180, 0.200, 0.145), 0.60), a)

    # 4) 孢子散点（两种密度：簇内 + 远处零星）
    specks(cv, rng, 300, cx, cy, 184, 0.4, 1.7, (0.135, 0.150, 0.108), 0.42)
    specks(cv, rng, 110, cx, cy, 204, 0.35, 1.1, (0.33, 0.35, 0.28), 0.26)
    return cv


def b_plaster_crack(B, rng) -> Canvas:
    """石膏裂纹：粉化带（下）-> 崩边高光唇（中）-> 暗色裂缝芯（上）+ 碎屑。"""
    cv = Canvas()
    base = B["plaster"]
    mot = fbm(rng, SIZE, SIZE, cells=5, octaves=4)

    # 主裂缝路径（一次生成，三层复用同一条路径 => 层间不会错位）
    main = vein_paths(rng, 42.0, 36.0, 0.62, 17.5, 30, 0.215)
    branches = []
    for _ in range(4):
        bi = int(rng.uniform(4, 22))
        bx, by = main[bi]
        bang = math.atan2(main[bi + 1][1] - main[bi][1], main[bi + 1][0] - main[bi][0])
        bang += (1.0 if rng.random() < 0.5 else -1.0) * float(rng.uniform(0.45, 0.95))
        branches.append(vein_paths(rng, bx, by, bang, 12.0, 11,
                                   0.30 + 0.08 * float(rng.random())))

    paths = [main] + branches
    prof = prof1d(rng, 64, 7, 0.45, 1.0)

    # 1) 裂缝两侧的粉化起壳带（很淡的浅色，浓淡沿程起伏才不像刷子）
    for p in paths:
        stroke_path(cv, p, 8.0, 3.0, (0.82, 0.81, 0.77), 0.40, soft=4.0, taper=0.5,
                    alpha_fn=alpha_curve(prof))

    # 2) 崩边高光唇（偏一侧，石膏剥落露出的亮边）
    for p in paths:
        off = [(q[0] + 2.0, q[1] + 2.6) for q in p]
        stroke_path(cv, off, 1.8, 0.6, (0.92, 0.91, 0.87), 0.34, soft=1.2, taper=0.4)

    # 3) 裂缝暗芯（最上层，保证不会被高光盖住）
    for k, p in enumerate(paths):
        w0 = 2.6 if k == 0 else 2.0
        stroke_path(cv, p, w0, 0.40, (0.185, 0.180, 0.172), 0.99 if k == 0 else 0.92,
                    soft=1.1, taper=0.4,
                    alpha_fn=lambda u: 1.0 - 0.35 * u)
    # 4) 更细的二次龟裂（只在主裂缝附近）
    for _ in range(7):
        i = int(rng.uniform(2, len(main) - 2))
        a = float(rng.random()) * math.tau
        crack(cv, rng, main[i][0], main[i][1], a, float(rng.uniform(16, 38)),
              0.95, 0.2, (0.36, 0.35, 0.33), alpha=0.40, steps=3,
              wander=0.42, depth=0)

    # 5) 起皮碎屑 + 粉化散点
    specks(cv, rng, 110, 256, 256, 236, 0.4, 1.8, (0.74, 0.73, 0.69), 0.20)
    specks(cv, rng, 55, 256, 256, 236, 0.5, 2.2, (0.40, 0.39, 0.36), 0.16)
    return cv


def b_paint_peel(B, rng) -> Canvas:
    """漆皮起壳：残漆块（锯齿边）-> 露底斑块 + 内影 -> 龟裂 -> 翘边高光 -> 碎屑。"""
    cv = Canvas()
    cx, cy = 252.0, 248.0
    base = B["paint"]
    mot = fbm(rng, SIZE, SIZE, cells=5, octaves=5)
    fine = fbm(rng, SIZE, SIZE, cells=18, octaves=3)
    fine2 = fbm(rng, SIZE, SIZE, cells=40, octaves=2)

    # 1) 残漆块：大尺度域扭曲 + 高频咬边（不是光滑成型的「白纸片」）
    q = blob_field(rng, cx, cy, 172, 156, warp=0.50, cells=3, extra=0.09)
    plate = 1.0 - smoothstep(0.80, 1.00, q)
    plate = np.clip(plate * (0.55 + 0.70 * fine2), 0.0, 1.0)
    plate = np.clip(plate * 1.25, 0.0, 1.0)

    # 2) 外侧软阴影（漆皮翘起投下的影，很淡、只在外缘）
    shadow = np.clip(blur(plate, 5.0) - plate, 0.0, 1.0) * 3.2
    cv.over((0.34, 0.34, 0.31), shadow * 0.30)

    # 3) 残漆本体（偏白但压住亮度与不透明度，避免整块像贴纸）
    col_paint = tint(base, (0.75, 0.74, 0.69), 0.55)
    cv.over(col_paint, plate * (0.58 + 0.18 * mot))

    # 4) 露底斑块（深灰绿腻子/混凝土），斑块内缘有暗影
    for _ in range(15):
        bx = cx + float(rng.uniform(-116, 116))
        by = cy + float(rng.uniform(-104, 104))
        rb = float(rng.uniform(16, 54))
        qb = blob_field(rng, bx, by, rb, rb * float(rng.uniform(0.6, 1.0)),
                        rot=float(rng.uniform(0, math.pi)), warp=0.48, cells=7)
        exposed = (1.0 - smoothstep(0.50, 1.0, qb)) * plate
        exposed = np.clip(exposed * (0.6 + 0.7 * fine), 0.0, 1.0)
        cv.over(tint(B["plaster"], (0.585, 0.580, 0.535), 0.45), exposed * 0.74)
        cv.over((0.34, 0.34, 0.32), edge_band(np.clip(exposed, 0, 1), 1.0) * 0.40)
        cv.over((0.44, 0.44, 0.41), np.exp(-((qb - 0.55) / 0.35) ** 2) * exposed * 0.16)

    # 5) 漆面龟裂（细、深色、密集短裂纹 —— 真实起壳漆的 crazing）
    for _ in range(58):
        x = cx + float(rng.uniform(-146, 146))
        y = cy + float(rng.uniform(-138, 138))
        if math.hypot(x - cx, y - cy) > 156:
            continue
        if plate[int(min(SIZE - 1, max(0, y))), int(min(SIZE - 1, max(0, x)))] < 0.25:
            continue
        crack(cv, rng, x, y, float(rng.random()) * math.tau, float(rng.uniform(10, 40)),
              0.95, 0.2, (0.44, 0.43, 0.39), alpha=0.52, steps=3, wander=0.55, depth=0)

    # 6) 翘边高光（沿边界的亮线）+ 剥落碎屑
    cv.over((0.93, 0.92, 0.87), edge_band(plate, 1.0) * 0.36 * (0.5 + 0.5 * mot))
    specks(cv, rng, 70, cx, cy, 155, 0.5, 2.4, (0.80, 0.79, 0.74), 0.28)
    specks(cv, rng, 30, cx, cy, 172, 0.7, 2.8, (0.52, 0.51, 0.47), 0.26)
    return cv


def b_scuff_marks(B, rng) -> Canvas:
    """鞋底擦痕：多条弧线（弓形/倾斜/长度各异）+ 断续噪声 + 橡胶蹭点 + 干粉尘。"""
    cv = Canvas()
    base = B["scuff"]
    an = fbm_aniso(rng, SIZE, SIZE, cells_x=3, cells_y=28, octaves=3)
    mot = fbm(rng, SIZE, SIZE, cells=5, octaves=4)
    fine_s = fbm(rng, SIZE, SIZE, cells=42, octaves=2)
    col = (0.205, 0.205, 0.200)

    def arc(x0, y0, ln, tilt, bow, w, alpha0, steps=13):
        prev = None
        for s in range(steps + 1):
            t = s / steps
            x = x0 + ln * t
            y = y0 + tilt * ln * (t - 0.5) + math.sin(t * math.pi) * bow
            if prev is not None:
                fade = math.sin(math.pi * t) ** 0.45
                ww = w * (0.72 + 0.34 * fade)
                stamp_stroke(cv, prev, (x, y), ww, ww, col,
                             alpha0 * (0.30 + 0.70 * fade), soft=w * 0.85)
            prev = (x, y)

    # 4 条主擦痕：都是接近水平的方向（换一个 tilt 就会交成一个大 X，很假）
    for tilt, bow_k, yi in ((-0.08, 0.55, 0.30), (0.12, -0.85, 0.46), (-0.16, 0.30, 0.62),
                            (0.06, -1.15, 0.74)):
        ln = float(rng.uniform(150, 330))
        y0 = 512.0 * yi + float(rng.uniform(-30, 30))
        arc(float(rng.uniform(20, 140)), y0, ln,
            tilt + float(rng.uniform(-0.05, 0.05)),
            float(rng.uniform(-24, 24)) * bow_k, float(rng.uniform(4.0, 9.0)),
            float(rng.uniform(0.42, 0.62)))

    # 1 条长拖拽痕（更淡更宽，只作方向暗示，不能抢主视）
    for _ in range(1):
        x0 = float(rng.uniform(20, 130))
        y0 = float(rng.uniform(150, 400))
        ln = float(rng.uniform(280, 400))
        ang = float(rng.uniform(-0.95, 0.35))
        stamp_stroke(cv, (x0, y0), (x0 + math.cos(ang) * ln, y0 + math.sin(ang) * ln),
                     float(rng.uniform(6, 10)), float(rng.uniform(2, 4)),
                     col, 0.07, soft=9.0)

    # 短促擦点（鞋底转向/踩踏）
    for _ in range(14):
        x = float(rng.uniform(60, 450))
        y = float(rng.uniform(90, 430))
        a = float(rng.uniform(0, math.pi))
        ln = float(rng.uniform(10, 42))
        stamp_stroke(cv, (x, y), (x + math.cos(a) * ln, y + math.sin(a) * ln),
                     float(rng.uniform(3, 7)), 0.4, col,
                     float(rng.uniform(0.15, 0.35)), soft=2.0)

    # 断续/浓淡：各向异性噪声调制（先算好再乘，避免颜色错位）
    cv.scale(np.clip((0.38 + 0.95 * an) * (0.62 + 0.50 * fine_s), 0.0, 1.0))

    # 橡胶蹭点 + 干粉尘残留
    for _ in range(12):
        stamp_dot(cv, float(rng.uniform(80, 430)), float(rng.uniform(110, 410)),
                  float(rng.uniform(1.8, 6.5)), (0.14, 0.14, 0.14),
                  float(rng.uniform(0.20, 0.50)))
    specks(cv, rng, 130, 256, 256, 240, 0.4, 1.5, (0.62, 0.61, 0.58), 0.14)
    return cv


def b_coffee_ring(B, rng) -> Canvas:
    """咖啡杯圈：角向噪声的环（半径/厚度/浓淡沿环变化）+ 内薄膜 + 侧漏 + 外溅。"""
    cv = Canvas()
    cx, cy = 256.0, 256.0
    base = B["stain"]
    mot = fbm(rng, SIZE, SIZE, cells=6, octaves=4)
    ang = np.arctan2(YY - cy, XX - cx)
    r = np.sqrt((XX - cx) ** 2 + (YY - cy) ** 2)
    R = 148.0

    # 角向一维噪声：环的半径抖动 / 厚度变化 / 浓度断口都靠它
    n1 = fbm(rng, 8, 512, cells=3, octaves=3)[0]
    n2 = fbm(rng, 8, 512, cells=6, octaves=3)[0]
    idx = ((ang + math.pi) / math.tau * 511.0).astype(np.int32) % 512
    wob = n1[idx]
    den = n2[idx]

    # 1) 杯底薄膜（内部很淡的渍）
    inner = 1.0 - smoothstep(R * 0.55, R * 0.99, r)
    cv.over(tint(base, (0.52, 0.41, 0.30), 0.40), inner * (0.05 + 0.04 * mot))

    # 2) 主环：半径 R 随角向抖动 ±4%，厚度随角向 2~12px，并留一个断口
    rr = R * (1.0 + 0.040 * (wob - 0.5) * 2.0 + 0.012 * np.sin(ang * 3.0 + 0.7))
    thick = 2.4 + 10.0 * den
    ring = np.exp(-((r - rr) / thick) ** 2) * (0.55 + 0.45 * den)
    ab = float(rng.uniform(0, math.tau))
    dang = np.abs((ang - ab + math.pi) % math.tau - math.pi)
    ring = ring * (1.0 - 0.80 * np.exp(-(dang / 0.13) ** 2))
    cv.over(tint(base, (0.355, 0.240, 0.150), 0.45), ring * 0.78)

    # 3) 内沿细环（双圈，真实杯底常有两条）
    rr2 = R * 0.935 * (1.0 + 0.030 * (wob - 0.5) * 2.0)
    ring2 = np.exp(-((r - rr2) / 2.2) ** 2) * (0.45 + 0.55 * den)
    cv.over(tint(base, (0.42, 0.29, 0.18), 0.40), ring2 * 0.34)

    # 4) 环上的积垢点（浓处）
    for _ in range(14):
        a = float(rng.random()) * math.tau
        x = cx + math.cos(a) * R * float(rng.uniform(0.98, 1.02))
        y = cy + math.sin(a) * R * float(rng.uniform(0.98, 1.02))
        stamp_dot(cv, x, y, float(rng.uniform(4, 12)), (0.32, 0.21, 0.125),
                  float(rng.uniform(0.12, 0.40)))

    # 5) 侧漏的小滩 + 外溅点
    for _ in range(2):
        bx = cx + float(rng.uniform(-1, 1)) * 70
        by = cy + float(rng.uniform(0.82, 1.02)) * R
        qb = blob_field(rng, bx, by, float(rng.uniform(9, 22)), float(rng.uniform(6, 14)),
                        warp=0.42, cells=8)
        cv.over(tint(base, (0.33, 0.22, 0.135), 0.45),
                (1.0 - smoothstep(0.25, 1.0, qb)) * 0.52)
    for _ in range(16):
        a = float(rng.random()) * math.tau
        rad = R * float(rng.uniform(1.02, 1.36))
        stamp_dot(cv, cx + math.cos(a) * rad, cy + math.sin(a) * rad,
                  float(rng.uniform(0.6, 2.6)), (0.37, 0.25, 0.15),
                  float(rng.uniform(0.25, 0.60)))
    return cv


def b_dirt_track(B, rng) -> Canvas:
    """泥脚印拖痕：拖出的泥带 + 3 个鞋印（前掌/足弓/后跟 + 鞋底横纹）+ 泥点。"""
    cv = Canvas()
    base = B["dirt"]
    mot = fbm(rng, SIZE, SIZE, cells=5, octaves=5)
    fine = fbm(rng, SIZE, SIZE, cells=20, octaves=3)
    ang = -0.60
    col = (0.330, 0.278, 0.208)
    dark = (0.235, 0.196, 0.142)
    ca, sa = math.cos(ang), math.sin(ang)

    # 1) 被拖出来的泥带（很低浓度，只给一点方向感；掩膜 + 平滑噪声避免串珠）
    def _drag(t: Canvas) -> None:
        for _ in range(3):
            off = float(rng.uniform(-26, 26))
            x0 = 120.0 + off
            y0 = 432.0 - off * 0.30
            ln = float(rng.uniform(260, 360))
            stamp_stroke(t, (x0, y0), (x0 + ca * ln, y0 + sa * ln),
                         float(rng.uniform(24, 42)), float(rng.uniform(6, 16)),
                         (1.0, 1.0, 1.0), 1.0, soft=16.0)
    composite(cv, mask_streaks(_drag) * (0.05 + 0.14 * mot), tint(base, col, 0.45))

    # 2) 三个鞋印：前掌椭圆 + 足弓（窄、浅）+ 后跟椭圆，各带独立旋转
    #    鞋印长轴（趾→跟）约 100px，间距 104px：太长会糊成一根长条，认不出是脚印
    for k in range(3):
        px = 152.0 + ca * 104.0 * k + float(rng.uniform(-7, 7))
        py = 404.0 + sa * 104.0 * k + float(rng.uniform(-7, 7))
        rot = ang + float(rng.uniform(-0.12, 0.12))
        cf, sf = math.cos(rot), math.sin(rot)
        ax, ay = -cf, -sf

        qf = blob_field(rng, px, py, 20, 30, rot=rot, warp=0.16, cells=6)
        fore = 1.0 - smoothstep(0.55, 1.0, qf)
        # 脚趾：前掌前端 4 个小凸起，要「顶出去」才能和掌面分开
        toes = np.zeros((SIZE, SIZE), np.float32)
        for j in range(4):
            tp = (-0.40 + j * 0.27) * 40.0
            tx = px + cf * 26.0 - sf * tp
            ty = py + sf * 26.0 + cf * tp
            qt = blob_field(rng, tx, ty, 8.0, 6.4, rot=rot, warp=0.34, cells=8)
            toes = np.maximum(toes, (1.0 - smoothstep(0.25, 1.0, qt)) * 0.90)
        hx, hy = px + ax * 70.0, py + ay * 70.0
        qh = blob_field(rng, hx, hy, 15, 21, rot=rot, warp=0.18, cells=7)
        heel = 1.0 - smoothstep(0.52, 1.0, qh)
        qa = blob_field(rng, px + ax * 36.0, py + ay * 36.0, 7.5, 26, rot=rot,
                        warp=0.30, cells=7)
        arch = (1.0 - smoothstep(0.42, 1.0, qa)) * 0.34
        sole = np.maximum.reduce([fore, toes, heel, arch]).astype(np.float32)
        # 鞋底横纹：沿长轴等距的凹槽（把 alpha 咬掉一截，而不是叠深色线）
        uu = (XX - px) * cf + (YY - py) * sf
        groove = np.clip(np.sin(uu * (math.tau / 9.5)) * 0.5 + 0.5, 0.0, 1.0)
        sole = sole * (1.0 - 0.50 * (groove ** 2))
        sole = np.clip(sole * (0.55 + 0.62 * fine) * (0.84 + 0.26 * mot), 0.0, 1.0)
        cv.over(tint(base, col, 0.55), sole * 0.94)

        # 前掌/后跟压实处更深（着地受力最大）
        press = np.maximum(fore * 0.85, heel * 0.75).astype(np.float32)
        cv.over(tint(base, dark, 0.45),
                np.clip(press * (0.45 + 0.45 * fine), 0.0, 1.0) * 0.62)

    # 3) 泥点飞溅 + 干土残留 + 干湿不均
    specks(cv, rng, 170, 256, 300, 225, 0.5, 3.0, col, 0.32)
    specks(cv, rng, 70, 256, 300, 240, 0.4, 1.5, (0.60, 0.55, 0.45), 0.20)
    cv.scale(np.clip(0.72 + 0.42 * mot, 0.0, 1.0))
    return cv


def b_rust_run(B, rng) -> Canvas:
    """锈水垂痕：顶部锈源 + 多条重力垂痕（掩膜 + 竖向拉丝调制）+ 末端积垢 + 下方漫流。"""
    cv = Canvas()
    base = B["rust"]
    mot = fbm(rng, SIZE, SIZE, cells=5, octaves=5)
    an = fbm_aniso(rng, SIZE, SIZE, 34, 4, octaves=3)
    sx, sy = 250.0, 74.0
    bright = (0.505, 0.293, 0.163)
    deep = (0.268, 0.150, 0.082)

    # 1) 顶部锈源（锈蚀起点，最浓；做得够大才能"挂"住下面的垂痕）
    q = blob_field(rng, sx, sy, 92, 60, warp=0.46, cells=6)
    src = (1.0 - smoothstep(0.15, 1.02, q)) * (0.55 + 0.45 * mot)
    cv.over(tint(base, bright, 0.60), src * 0.92)
    cv.over(tint(base, deep, 0.50), (1.0 - smoothstep(0.10, 0.72, q)) * 0.62)

    # 2) 垂痕：先叠成掩膜（连续、无串珠），再用竖向拉丝噪声调制浓淡
    strands = []

    def _paths(t: Canvas) -> None:
        for i in range(10):
            x = sx + float(rng.uniform(-88, 88))
            y0 = sy + float(rng.uniform(-12, 52))
            ln = float(rng.uniform(130, 392)) * (1.0 - 0.45 * abs(x - sx) / 104.0)
            w0 = float(rng.uniform(4.5, 14.0))
            drift = float(rng.uniform(-16, 16))
            steps = 22
            pts = []
            for s in range(steps + 1):
                tt = s / steps
                pts.append((x + drift * tt + math.sin(tt * 5.0 + i * 1.7) * 2.6,
                            y0 + ln * tt))
            strands.append((pts, w0))
            stroke_path(t, pts, w0, w0 * 0.42, (1.0, 1.0, 1.0), 1.0,
                        soft=max(1.8, w0 * 0.30), taper=0.25,
                        alpha_fn=lambda u: (1.0 - u) ** 0.55)

    mask = mask_streaks(_paths)
    cv.over(tint(base, bright, 0.55), mask * np.clip(0.30 + 0.62 * an, 0.0, 1.0) * 0.72)

    def _cores(t: Canvas) -> None:
        for pts, w0 in strands:
            stroke_path(t, pts, w0 * 0.42, w0 * 0.20, (1.0, 1.0, 1.0), 1.0,
                        soft=max(1.0, w0 * 0.18))

    core = mask_streaks(_cores)
    cv.over(tint(base, deep, 0.45), core * np.clip(0.25 + 0.60 * an, 0.0, 1.0) * 0.55)

    # 3) 末端积垢（锈水蒸发后留下的深色珠）
    for pts, _w in strands:
        ex, ey = pts[-1]
        qb = blob_field(rng, ex, ey, float(rng.uniform(3, 9)),
                        float(rng.uniform(4, 12)), warp=0.40, cells=8)
        cv.over(tint(base, deep, 0.50), (1.0 - smoothstep(0.20, 1.0, qb)) * 0.30)

    # 4) 锈源下方的宽幅漫流（薄薄一层，带竖向拉丝）
    qw = blob_field(rng, sx, sy + 150, 84, 150, warp=0.36, cells=4)
    wash = (1.0 - smoothstep(0.25, 1.0, qw)) * (0.30 + 0.70 * an)
    cv.over(tint(base, (0.395, 0.245, 0.148), 0.50), wash * 0.20)

    # 5) 氧化铁鳞片 / 锈斑颗粒
    for _ in range(60):
        x = sx + float(rng.uniform(-92, 92))
        y = float(rng.uniform(30, 500))
        stamp_dot(cv, x, y, float(rng.uniform(0.9, 4.2)), deep,
                  float(rng.uniform(0.12, 0.42)))
    cv.scale(np.clip(0.80 + 0.34 * mot, 0.0, 1.0))
    return cv


def b_oil_splat(B, rng) -> Canvas:
    """油渍：外渗晕 + 3 瓣并集主体 + 浓黑芯 + 边缘油膜虹彩 + 溅射卫星滴。"""
    cv = Canvas()
    cx, cy = 248.0, 254.0
    base = B["oil"]
    mot = fbm(rng, SIZE, SIZE, cells=5, octaves=5)

    # 1) 外渗晕（油渗进材料的淡暗环）
    q = blob_field(rng, cx, cy, 190, 172, rot=0.4, warp=0.36, cells=4, extra=0.05)
    cv.over(tint(base, (0.34, 0.34, 0.33), 0.40),
            (1.0 - smoothstep(0.30, 1.06, q)) * (0.12 + 0.08 * mot))

    # 2) 主体：3 个瓣的并集 => 不规则、有「流出去」的方向
    qa = blob_field(rng, cx - 16, cy - 8, 128, 114, rot=0.4, warp=0.44, cells=4, extra=0.06)
    qb = blob_field(rng, cx + 80, cy + 36, 84, 70, rot=0.1, warp=0.48, cells=5, extra=0.05)
    qc = blob_field(rng, cx - 70, cy + 66, 66, 58, rot=-0.3, warp=0.50, cells=5)
    m = np.maximum.reduce([1.0 - smoothstep(0.30, 1.0, qa),
                           1.0 - smoothstep(0.28, 0.98, qb),
                           1.0 - smoothstep(0.28, 0.98, qc)]).astype(np.float32)
    body = np.clip(m * (0.78 + 0.34 * mot), 0.0, 1.0)
    cv.over(tint(base, (0.125, 0.125, 0.130), 0.55), body * 0.82)
    # 浓黑芯
    cv.over(tint(base, (0.065, 0.065, 0.070), 0.50),
            np.clip(blur(body, 8.0) - 0.45, 0, 1) * 0.95)

    # 3) 油膜虹彩（只在边缘很薄一圈，低饱和冷色）
    band = edge_band(body, 1.0) * (0.45 + 0.55 * mot)
    cv.over((0.30, 0.34, 0.40), band * 0.14)
    cv.over((0.38, 0.33, 0.31), edge_band(np.clip(blur(body, 4.0), 0, 1), 1.0) * 0.10)

    # 4) 卫星油滴（沿径向拉长，像被甩出去的）
    for _ in range(14):
        a = float(rng.random()) * math.tau
        rad = float(rng.uniform(126, 218))
        dx = cx + math.cos(a) * rad
        dy = cy + math.sin(a) * rad * 0.88
        rr = float(rng.uniform(2.5, 9.0))
        qd = blob_field(rng, dx, dy, rr, rr * float(rng.uniform(0.9, 1.6)),
                        rot=a, warp=0.32, cells=9)
        cv.over(tint(base, (0.115, 0.115, 0.125), 0.50),
                (1.0 - smoothstep(0.30, 1.0, qd)) * float(rng.uniform(0.45, 0.80)))
    cv.scale(np.clip(0.84 + 0.26 * mot, 0.0, 1.0))
    return cv


def b_grime_streak(B, rng) -> Canvas:
    """竖向污渍：顶部污带 -> 多条竖向污痕（粗细/起点/长度各异，逐段浓淡）+ 拉丝调制。"""
    cv = Canvas()
    base = B["concrete"]
    an = fbm_aniso(rng, SIZE, SIZE, cells_x=2, cells_y=34, octaves=4)
    mot = fbm(rng, SIZE, SIZE, cells=4, octaves=4)
    fine = fbm(rng, SIZE, SIZE, cells=24, octaves=2)
    col = (0.365, 0.365, 0.350)

    # 1) 顶部灰污带（墙面从上方流下来的起点）
    top = np.exp(-((YY - 34.0) / 46.0) ** 2)
    cv.over(tint(base, col, 0.45), top * (0.22 + 0.22 * mot))

    # 2) 竖向污痕（逐段浓淡，避免均匀色带）
    for i in range(8):
        x = float(rng.uniform(26, 486))
        y0 = float(rng.uniform(4, 64))
        ln = float(rng.uniform(200, 430))
        w = float(rng.uniform(4.5, 14.0))
        drift = float(rng.uniform(-12, 12))
        steps = 9
        for s in range(steps):
            t = s / steps
            t2 = (s + 1) / steps
            xa = x + drift * t + math.sin(t * 3.2 + i * 1.7) * 2.2
            xb = x + drift * t2 + math.sin(t2 * 3.2 + i * 1.7) * 2.2
            ya = y0 + ln * t
            yb = y0 + ln * t2
            wa = w * (1.0 - 0.40 * t)
            al = float(rng.uniform(0.30, 0.64)) * (1.0 - 0.35 * t)
            stamp_stroke(cv, (xa, ya), (xb, yb), wa, wa * 0.94, col, al,
                         soft=max(1.5, wa * 0.38))

    # 3) 拉丝调制（断续 + 两侧毛边）
    cv.scale(np.clip(0.42 + 0.62 * an * (0.75 + 0.35 * fine), 0.0, 1.0))
    # 4) 底部积灰慢慢消失
    cv.scale(np.clip(1.15 - YY / 520.0, 0.0, 1.0) ** 0.7)
    return cv


def b_soot_smoke(B, rng) -> Canvas:
    """烟熏：羽状烟场（各向异性，向上发散）+ 浓黑核心 + 向上舔的暗丝 + 灰烬。"""
    cv = Canvas()
    base = B["soot"]
    mot = fbm(rng, SIZE, SIZE, cells=4, octaves=5)
    an = fbm_aniso(rng, SIZE, SIZE, cells_x=5, cells_y=11, octaves=4)
    an2 = fbm_aniso(rng, SIZE, SIZE, cells_x=9, cells_y=3, octaves=3)
    cx, cy = 250.0, 128.0

    # 1) 羽状烟场（越往上越散，左右被噪声撕开）
    plume = 1.0 - smoothstep(0.05, 1.0,
                             np.sqrt(((XX - cx) / (138 + 58 * an)) ** 2 +
                                     ((YY - cy) / (238 + 46 * an)) ** 2))
    plume = np.clip(plume * (0.55 + 0.75 * an2), 0.0, 1.0)
    cv.over(tint(base, (0.215, 0.215, 0.205), 0.55),
            plume * (0.26 + 0.34 * mot) * np.clip(1.15 - YY / 470.0, 0, 1) ** 0.55)

    # 2) 浓黑核心（火源上方，略偏）
    q = blob_field(rng, cx + 8, cy + 26, 96, 78, warp=0.42, cells=5)
    cv.over(tint(base, (0.105, 0.105, 0.100), 0.55),
            (1.0 - smoothstep(0.12, 1.0, q)) * 0.66)
    cv.over((0.055, 0.055, 0.055), np.clip(1.0 - smoothstep(0.05, 0.60, q), 0, 1) * 0.58)

    # 3) 向上舔舐的暗丝（收窄、抖动、逐渐变淡）
    for _ in range(10):
        a = -math.pi * 0.5 + float(rng.uniform(-0.85, 0.85))
        r0 = float(rng.uniform(48, 112))
        x0 = cx + math.cos(a) * r0
        y0 = cy + math.sin(a) * r0 + 16
        crack(cv, rng, x0, y0, a, float(rng.uniform(46, 128)), 6.5, 0.4,
              (0.135, 0.135, 0.132), alpha=0.20, steps=7, wander=0.30, depth=0)

    # 4) 灰烬散点（黑 + 极少量灰白）
    specks(cv, rng, 170, cx, cy + 40, 205, 0.4, 2.3, (0.095, 0.095, 0.095), 0.34)
    specks(cv, rng, 60, cx, cy + 40, 225, 0.4, 1.5, (0.56, 0.55, 0.53), 0.18)
    return cv


def b_paint_drip(B, rng) -> Canvas:
    """油漆流挂：顶部不规则残漆带 + 重力流挂（渐细 + 末端漆珠）+ 点状漆滴。"""
    cv = Canvas()
    base = B["paint"]
    mot = fbm(rng, SIZE, SIZE, cells=5, octaves=4)
    fine = fbm(rng, SIZE, SIZE, cells=18, octaves=3)
    paint = (0.700, 0.690, 0.652)
    y_band = 118.0

    # 1) 顶部残漆带（下缘被噪声咬碎，不是一条直线）
    qb = blob_field(rng, 256, 26, 330, 118, warp=0.26, cells=4, extra=0.05)
    band = 1.0 - smoothstep(0.42, 1.02, qb)
    band = np.clip(band * (0.70 + 0.45 * fine), 0.0, 1.0)
    band *= smoothstep(2.0, 40.0, YY)  # 顶部不要贴边，让贴花有上边距
    cv.over(tint(base, paint, 0.40), band * 0.70)
    # 漆带下缘的厚度阴影
    low = np.clip(blur(band, 4.0) - band, 0.0, 1.0) * 3.0
    cv.over((0.50, 0.495, 0.470), low * 0.34)

    # 2) 重力流挂（7 条，宽度差异大，末端积漆珠）
    for i in range(7):
        x = 58.0 + i * 62.0 + float(rng.uniform(-18, 18))
        ln = float(rng.uniform(90, 280))
        w = float(rng.uniform(2.2, 6.4))
        y0 = y_band + float(rng.uniform(-14, 14))
        steps = 10
        prev = (x, y0)
        for s in range(1, steps + 1):
            t = s / steps
            nx = x + math.sin(t * 4.0 + i * 1.1) * 2.4
            ny = y0 + ln * t
            ww = w * (1.0 - 0.42 * t) + 0.45
            stamp_stroke(cv, prev, (nx, ny), ww, ww * 0.95, paint,
                         alpha=0.62, soft=1.05)
            prev = (nx, ny)
        stamp_dot(cv, prev[0], prev[1], w * 1.05, paint, 0.66)
        stamp_dot(cv, prev[0] + w * 0.30, prev[1] - w * 0.32, w * 0.34,
                  (0.83, 0.825, 0.790), 0.45)

    # 3) 少量点状漆滴 + 漆带上的颗粒
    for _ in range(12):
        stamp_dot(cv, float(rng.uniform(28, 484)),
                  float(rng.uniform(y_band + 24, 486)),
                  float(rng.uniform(1.2, 4.6)), paint,
                  float(rng.uniform(0.35, 0.80)))
    cv.scale(np.clip(0.90 + 0.14 * mot, 0.0, 1.0))
    return cv


# ------------------------------------------------------------------ registry

@dataclass
class Decal:
    name: str
    title: str
    group: str
    world: str
    surfaces: str
    tileable: str
    ai: str
    proc: str
    caveat: str
    feather: float
    cov: Tuple[float, float]
    build: Callable
    bases: Tuple[str, ...]
    _amax: float = field(default=0.0, repr=False)


DECALS = [
    Decal("D_Hole_Concrete", "混凝土/石膏墙弹孔", "弹孔组", "0.30 m（孔芯约 7 cm）",
          "ConcreteWall / PaintedWall / ConcreteFloor / CeilingTile 等灰质墙面、柱子",
          "否（单次贴花，不可平铺）",
          "concrete 底图：水泥灰浆颗粒、气孔、粉尘斑驳（重染到 0.70 灰，detail 0.55）",
          "三层：粉尘光晕（域扭曲椭圆 + 径向衰减）→ 崩边亮环（高频噪声咬边）→ 孔唇亮/暗双环 → "
          "近黑孔芯（带一圈内壁反光）；9 条放射主裂纹（分叉随机游走，带粉化晕 + 唇口高光 + 暗芯）、"
          "16 条发丝裂纹、碎屑散点",
          "裂纹尺度按 512px 手调，放到 0.6 m 以上会看出笔刷尺度；没有法线/视差，"
          "斜视时孔洞不「凹」。", 2.4, (5.0, 18.0), b_hole_concrete, ("concrete",)),
    Decal("D_Hole_Metal", "金属板弹孔", "弹孔组", "0.22 m（孔芯约 4.5 cm）",
          "MetalDoor / DarkMetal / DiamondPlate / ServerPanel 等涂装钢板",
          "否",
          "metal 底图：细漆面颗粒与微弱磨损（重染 0.68 灰，detail 0.50）",
          "漆面崩落区（不规则灰白）→ 塑性凹坑（单向明暗：亮边/暗边按光照方向加权）→ "
          "近黑孔芯 + 亮金属翻边（rim 高斯 + 角度加权高光弧）→ 7 条放射细裂 + 12 条弹道擦痕（方向性笔刷）",
          "翻边高光与凹坑明暗都假设主光在左上，光源方向差太多时会轻微违和。",
          2.0, (3.0, 11.0), b_hole_metal, ("metal",)),
    Decal("D_Hole_Glass", "玻璃弹孔（蛛网裂纹）", "弹孔组", "0.40 m（核心约 4 cm）",
          "GlassPanel 隔断、窗、玻璃门（叠在不透明磨砂玻璃上）",
          "否",
          "glass 底图：磨砂玻璃极细蚀刻颗粒（重染 0.80/0.82/0.84，detail 0.30）",
          "9 条近直线径向裂纹（wander 0.03，12 段，每条配一条偏移暗线做蚀刻层次 + 短支裂纹）、"
          "4 圈同心蛛网弧（半径抖动 ±5px、随机断口）、发白粉碎核心、中心穿透小孔、玻璃屑亮点",
          "裂纹靠 alpha 亮线模拟，在纯暗背景前不如真实折射亮；玻璃孔是画在表面上的，不是真通孔。",
          1.6, (7.0, 19.0), b_hole_glass, ("glass",)),
    Decal("D_Water_Stain", "渗水痕", "墙面劣化", "1.1 m",
          "ConcreteWall / PaintedWall / CeilingTile（天花板渗漏）",
          "否",
          "water 底图：受潮石膏的矿物潮痕与粉化颗粒（重染 0.42/0.40/0.35，detail 0.50）",
          "三个团块的并集形状（上大、下窄、右下溢）→ 外侧潮气晕（blur 差集）→ 主体 + 内芯（blur 加深）→ "
          "潮线（4·m·(1−m) 边界环带，双圈）→ 7 条指状拖尾（越往下越细越透明，掩膜法避免串珠）+ "
          "2 条宽拖痕 → 5 处水碱白霜",
          "本体 alpha 峰值只有 ~0.55，必须贴在同色系墙面上；贴到深色金属上会发灰。",
          5.0, (16.0, 36.0), b_water_stain, ("water",)),
    Decal("D_Mold_Patch", "霉斑", "墙面劣化", "1.0 m",
          "ConcreteWall / PaintedWall、卫生间/机房墙角、CeilingTile",
          "否",
          "water + mold 底图：孢子团颗粒、粉状边缘（mold 底图只取亮度，detail 0.55）",
          "6 团并集潮气晕（灰绿、很淡）+ 4 团主霉簇（边缘用 22 cells 高频噪声咬碎成绒毛感）+ "
          "22 团中/小簇（各自域扭曲）+ 240 个簇内孢子 + 90 个远处孢子",
          "深色霉斑在深色底材上对比会掉；建议只贴在中浅灰/米白墙面。",
          4.0, (14.0, 40.0), b_mold_patch, ("water", "mold")),
    Decal("D_Plaster_Crack", "石膏裂纹", "墙面劣化", "1.6 m",
          "PaintedWall / ConcreteWall / CeilingTile（腻子层开裂）",
          "否",
          "plaster 底图：白垩质腻子的细龟裂与粉化（重染 0.76/0.75/0.71）",
          "1 条主裂缝（24 段随机游走）+ 4 条支缝，同一路径画三层：粉化起壳带（半径 8→3px，"
          "沿程浓度起伏）→ 偏侧高光唇（+2.0/+2.6px 偏移，宽 1.8px）→ 暗色裂缝芯（半径 2.6→0.40px）；"
          "30 条二次龟裂 + 碎屑散点",
          "裂纹路径是程序化随机的，同一张不宜在相邻墙面重复使用，否则能看出同一条裂纹；"
          "裂缝芯只有 2~3px，贴到 1.6 m 以上会明显偏细。",
          0.9, (2.0, 9.0), b_plaster_crack, ("plaster",)),
    Decal("D_Paint_Peel", "漆皮起壳", "墙面劣化", "1.0 m",
          "PaintedWall / IndustrialPaint / MetalDoor（涂装面）",
          "否",
          "paint + plaster 底图：干裂漆膜与露出的腻子底材",

          "残漆块（大尺度域扭曲 + 40 cells 高频咬边，边缘不光滑）→ 外侧软阴影 → 残漆本体（0.77 灰，"
          "压住亮度）→ 15 处露底斑块（内缘暗影 + 中心微亮）→ 58 条漆面 crazing 细裂 → "
          "翘边高光 + 碎片散点",
          "露底斑块假设底材是灰绿腻子；贴到已经露混凝土的位置会重复「露底」。",
          2.0, (14.0, 36.0), b_paint_peel, ("paint", "plaster")),
    Decal("D_Scuff_Marks", "鞋底擦痕/拖拽痕", "地面", "0.9 m",
          "TileFloor / EpoxyFloor / ConcreteFloor / DiamondPlate",
          "否",
          "scuff 底图：橡胶磨耗的干粉状条痕（重染 0.205 深灰，detail 0.50）",
          "4 条近水平主擦痕（弓形 + 独立粗细，沿程 alpha 用 sin 包络淡出）+ 1 条很淡的长拖拽痕 + "
          "14 条短促擦点 + 各向异性 fBm（cells_x=3/cells_y=28）× 42 cells 高频调制 + 橡胶蹭点 + 干粉尘",
          "整体很淡（峰值 alpha 只有 0.38），是「近看才见」的细节层；需要更脏就叠两张不同旋转。",
          3.5, (4.0, 14.0), b_scuff_marks, ("scuff",)),
    Decal("D_Coffee_Ring", "咖啡杯圈渍", "地面/桌面", "0.12 m（杯底约 7 cm）",
          "EpoxyFloor / WoodPanel / VenueCarpet 桌面、会议桌、茶水间台面",
          "否",
          "stain 底图：干涸液膜的细沉积纹",
          "角向一维噪声驱动：环半径抖动 ±4%、厚度 2.6~11px、浓度沿环断口；"
          "内外双环 + 杯底薄膜 + 14 个环上积垢点 + 2 处侧漏小滩 + 16 个外溅点",
          "环半径固定（只有抖动），同一桌面上重复贴会看出同一个缺口位置。",
          2.0, (7.0, 24.0), b_coffee_ring, ("stain",)),
    Decal("D_Dirt_Track", "泥脚印/拖痕", "地面", "1.2 m",
          "ConcreteFloor / EpoxyFloor / TileFloor、装卸区/楼梯间",
          "否",
          "dirt 底图：干泥与砂砾颗粒（重染 0.325/0.275/0.205）",
          "3 个鞋印（前掌椭圆 + 4 个脚趾 + 窄足弓 + 后跟，鞋底凹槽用 alpha 咬出；各自独立旋转，"
          "边缘被高频噪声咬碎）+ 3 条斜向拖拽主痕 + 泥点 + 干湿不均噪声调制",
          "脚印是简化轮廓，不是解剖学正确的鞋底；贴近镜头仍能看出椭圆拼出来的趾/跟。",
          5.0, (6.0, 18.0), b_dirt_track, ("dirt",)),
    Decal("D_Rust_Run", "锈水垂痕", "工业", "0.9 m（竖向）",
          "IndustrialPaint / ColdMetal / MetalDoor / DiamondPlate，螺栓与焊缝下方",
          "否",
          "rust 底图：氧化铁鳞片与粉状锈（重染 0.505/0.293/0.163）",
          "顶部锈源团块 → 8 条重力垂痕（每段独立随机浓淡 + 深色芯 + 横向漂移 + 末端积垢珠）→ "
          "下方漫流（各向异性拉丝）→ 48 个氧化铁颗粒",
          "假设锈源在贴花上缘正中；要挂在螺栓正下方时需要按世界位置对齐（pivot 建议 0.5, 0.9）。",
          3.0, (8.0, 26.0), b_rust_run, ("rust",)),
    Decal("D_Oil_Splat", "油渍", "工业", "1.0 m",
          "EpoxyFloor / DiamondPlate / ConcreteFloor / ServerPanel 机房地面",
          "否",
          "oil 底图：油泥与薄膜的暗色颗粒",
          "三个瓣的并集形状（有流淌方向）→ 外渗晕 → 主体 + 浓黑芯（blur 差集加深）→ "
          "边缘油膜虹彩（边界环带，低饱和冷色）→ 14 颗沿径向拉长的卫星油滴",
          "虹彩只是固定的低饱和冷色，不是真的薄膜干涉；"
          "如果以后有 specular/clearcoat 通道，应把油渍做成 smoothness 更高的材质。",
          4.0, (16.0, 40.0), b_oil_splat, ("oil",)),
    Decal("D_Grime_Streak", "竖向污渍", "工业", "1.4 m（竖向）",
          "ConcreteWall / IndustrialPaint / CleanPanel / ServerPanel 墙与设备侧面",
          "否（如需无缝可用 2 张错位叠贴）",
          "concrete 底图：灰尘与细颗粒（重染 0.365 灰，detail 0.45）",
          "顶部灰污带（高斯，模拟从上方流下的起点）→ 8 条竖向污痕（每段独立随机浓淡、宽度渐细、"
          "横向漂移）→ 各向异性 fBm（cells_x=2/cells_y=34）断续 + 24 cells 高频毛边 → 底部按 Y 淡出",
          "为保证四边 alpha=0（不出现硬矩形）放弃了平铺；大面积墙面请错位叠 2~3 张。",
          4.0, (12.0, 38.0), b_grime_streak, ("concrete",)),
    Decal("D_Soot_Smoke", "烟熏/烧灼痕", "工业", "1.4 m",
          "ConcreteWall / PaintedWall / CeilingTile，战斗后墙角、配电箱、起火点上方",
          "否",
          "soot 底图：炭黑与灰烬颗粒（重染 0.215 灰，detail 0.55）",
          "羽状烟场（两套各向异性噪声分别控制轮廓与撕裂）→ 浓黑核心（blur 差集）→ "
          "10 条向上舔舐并收窄的暗丝 → 170 个炭粒 + 60 个灰白灰烬",
          "没有真正的「烧焦碳化」质感（缺法线/自发光）；适合中远景，近景贴脸会偏软。",
          5.0, (16.0, 40.0), b_soot_smoke, ("soot",)),
    Decal("D_Paint_Drip", "油漆流挂", "工业", "0.9 m",
          "IndustrialPaint / MetalDoor / ConcreteWall（施工未干的流挂）",
          "否",
          "paint 底图：漆膜干裂与颗粒（重染 0.795/0.785/0.745，detail 0.40）",
          "顶部残漆带（域扭曲团块 + 高频咬碎下缘 + 上边距留空）→ 下缘厚度阴影 → "
          "7 条重力流挂（渐细 + 末端积漆珠 + 珠上高光）→ 12 个点状漆滴",
          "漆色偏亮米白，只适合浅色涂装面；深色面上会像白色涂料。",
          2.0, (14.0, 36.0), b_paint_drip, ("paint",)),
]


# ------------------------------------------------------------------- pipeline

def render(decal: Decal, bases: Dict[str, np.ndarray]) -> Tuple[np.ndarray, np.ndarray]:
    seed = SEED + zlib.crc32(("decal:" + decal.name).encode("utf-8")) % 100000
    rng = np.random.default_rng(seed)
    cv = decal.build(bases, rng)
    return finalize(cv, decal.feather)


def to_rgba8(rgb: np.ndarray, a: np.ndarray) -> Image.Image:
    rgba = np.zeros((SIZE, SIZE, 4), np.uint8)
    rgba[..., :3] = np.clip(np.round(rgb * 255.0), 0, 255).astype(np.uint8)
    rgba[..., 3] = np.clip(np.round(a * 255.0), 0, 255).astype(np.uint8)
    return Image.fromarray(rgba, mode="RGBA")


def stats(a: np.ndarray) -> dict:
    cov = float((a > COV_THRESHOLD).mean() * 100.0)
    border = float(max(a[0].max(), a[-1].max(), a[:, 0].max(), a[:, -1].max()))
    clear = float(max(a[:2].max(), a[-2:].max(), a[:, :2].max(), a[:, -2:].max()))
    return {"cov": cov, "border": border, "clear": clear,
            "a_max": float(a.max()), "a_mean": float(a.mean())}


def make_contact_sheet(items, path: str) -> None:
    tile, gap, label_h, margin, cols = 300, 26, 34, 30, 4
    rows = (len(items) + cols - 1) // cols
    cw = tile + gap
    ch = tile + label_h + gap
    W = margin * 2 + cols * cw - gap
    H = margin * 2 + rows * ch - gap
    sheet = Image.new("RGB", (W, H), (128, 128, 128))   # 中灰底，alpha 合成后看真实观感
    dr = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype(r"C:\Windows\Fonts\segoeui.ttf", 19)
    except Exception:  # pragma: no cover
        font = ImageFont.load_default()
    grey = Image.new("RGB", (tile, tile), (128, 128, 128))
    for idx, (dec, im) in enumerate(items):
        r, c = divmod(idx, cols)
        x = margin + c * cw
        y = margin + r * ch
        thumb = im.resize((tile, tile), Image.Resampling.LANCZOS)
        tile_img = grey.copy()
        tile_img.paste(thumb, (0, 0), thumb)
        sheet.paste(tile_img, (x, y))
        dr.rectangle([x, y, x + tile - 1, y + tile - 1], outline=(70, 70, 70))
        name = dec.name[:-4] if dec.name.endswith(".png") else dec.name
        tw = dr.textlength(name, font=font)
        dr.text((x + (tile - tw) / 2, y + tile + 7), name,
                fill=(240, 240, 240), font=font)
    sheet.save(path, "PNG")


def write_readme(rows, path: str) -> None:
    lines = []
    lines.append("# 贴花（Decal）资源")
    lines.append("")
    lines.append("本目录由 `tools/art/gen_decals.py` 生成，**未改动仓库里任何已有文件**。")
    lines.append("")
    lines.append("- 格式：`512 × 512 RGBA`，透明底；`Alpha` = 贴花形状与不透明度，`RGB` = 污渍本体色。")
    lines.append("- 用法：一张 quad + `Alpha Blend`（或 Unity Decal Projector）贴在墙面/地面/玻璃上；"
                 "alpha 是直通（straight）的，不要按预乘处理。")
    lines.append("- 每张最外 2px 的 `alpha` 强制为 0，2~8px 之间渐隐，因此可以直接当 quad 用，"
                 "不会出现硬矩形边。")
    lines.append("- 内部 alpha 由中心向外衰减，峰值≠1（除弹孔孔芯），避免「贴纸感」；"
                 "需要更浓时在材质里叠两张不同旋转。")
    lines.append("")
    lines.append("![contact sheet](_contact_sheet.png)")
    lines.append("")
    lines.append("## 生成命令")
    lines.append("")
    lines.append("```powershell")
    lines.append("# 全量重出（ComfyUI 底图有缓存时不需要 GPU，直接复用底图）")
    lines.append(r"C:\Users\17613\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe "
                 r"F:\Workspace\ESCAPE FROM WORK\tools\art\gen_decals.py")
    lines.append("")
    lines.append("# 只重出某几张")
    lines.append(r"... gen_decals.py --only D_Oil_Splat D_Rust_Run")
    lines.append("")
    lines.append("# 重出 AI 材质底图（需要 ComfyUI 在 127.0.0.1:8188）")
    lines.append(r"... gen_decals.py --regen-ai")
    lines.append("")
    lines.append("# 没有 ComfyUI 时用占位噪声，只调程序化形状")
    lines.append(r"... gen_decals.py --no-ai")
    lines.append("```")
    lines.append("")
    lines.append("AI 底图缓存：`F:\\ComfyUI\\output\\decals\\base_<key>.png`（SDXL base 1.0，"
                 "1024²，dpmpp_2m + karras，24 steps，cfg 6.5，固定 seed）。")
    lines.append("脚本用 `zlib.crc32(名字)` 派生每张图的种子，增删贴花不会打乱其它图的随机序列，"
                 "同参数重跑像素字节一致（`--no-ai` 也一样，占位噪声也是固定种子）。")
    lines.append("")
    lines.append("## 一览表")
    lines.append("")
    lines.append("| 文件 | 用途 | 建议世界尺寸 | 贴附表面 | 可平铺 | Alpha 覆盖率 |")
    lines.append("|------|------|-------------|---------|-------|-------------|")
    for d, st in rows:
        lines.append("| `%s.png` | %s | %s | %s | %s | %.1f%% |"
                     % (d.name, d.title, d.world, d.surfaces, d.tileable, st["cov"]))
    lines.append("")
    lines.append("覆盖率 = `alpha > 0.06` 的像素占比（视觉比重，不是全透明面积），"
                 "用来判断贴花在场景里大概占多大视觉比重，以及四边是否真的留空。")
    lines.append("")
    lines.append("## 逐张说明：AI 生成 vs 程序化生成")
    lines.append("")
    for d, st in rows:
        lines.append("### `%s.png` — %s" % (d.name, d.title))
        lines.append("")
        lines.append("- **建议世界尺寸**：%s；贴附表面：%s；可平铺：%s。"
                     % (d.world, d.surfaces, d.tileable))
        lines.append("- **AI 生成**：%s。" % d.ai)
        lines.append("- **程序化生成**：%s。" % d.proc)
        lines.append("- **已知不足**：%s" % d.caveat)
        lines.append("- 实测：alpha 覆盖率 %.1f%%，峰值 %.2f，四边 2px 内 alpha = 0。"
                     % (st["cov"], st["a_max"]))
        lines.append("")
    lines.append("## 通用已知不足")
    lines.append("")
    lines.append("1. 全部是 **albedo-only** 贴花：没有法线/粗糙度/自发光通道，"
                 "孔洞的「凹陷感」靠 albedo 里的暗环与高光弧假装，斜视时说服力有限。")
    lines.append("2. 光照方向：`D_Hole_Metal` 的凹坑明暗/翻边高光、`D_Paint_Drip` 的漆带阴影"
                 "是按「主光在左上」烤的；实际场景主光方向相反时会轻微违和。")
    lines.append("3. 弹孔组没有做「穿透」版本：玻璃孔是画在磨砂玻璃表面的，"
                 "真正的通孔需要建模或额外的 refraction 贴花。")
    lines.append("4. alpha 边缘羽化 1.6~5px（@512）：贴到 2m 以上的大表面时边缘会偏软，"
                 "需要更锐利就换更大的源尺寸重出。")
    lines.append("5. 每张都是单次贴花，不可平铺；大面积墙面的竖向污渍请错位叠 2~3 张"
                 "（`D_Grime_Streak`）。")
    lines.append("6. 没有写 `.meta`：交给 Unity 首次导入自动生成（默认 Texture Type 是 Default，"
                 "需要手动改成 `Default` + `Alpha Is Transparency` + `sRGB`，并关闭 sRGB 之外的压缩"
                 "或改用 BC7 保持 alpha 质量）。")
    lines.append("7. 形状全部由解析距离场笔刷 + 2×2 亚采样画出（软椭圆斑、胶囊笔刷、"
                 "径向/边界环带），没有 `draw.rectangle` / 整块矩形覆盖，"
                 "放大到 4× 也看不到直角块；但所有形体都是「画」出来的，"
                 "极度贴近镜头（<0.15 m）时仍能看出笔刷的数学校准感。")
    lines.append("8. 最弱的三张：`D_Dirt_Track` 的鞋印仍是椭圆拼合，凑近看趾/跟分界不够硬；"
                 "`D_Rust_Run` 的垂痕是宽度渐变胶囊，端头偏「齐」；"
                 "`D_Paint_Drip` 顶部漆带下缘偏几何，近看像被切过。")
    lines.append("")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))


# ----------------------------------------------------------------------- main

def main(argv) -> int:
    ap = argparse.ArgumentParser(description="ESCAPE FROM WORK decal generator")
    ap.add_argument("--only", nargs="*", default=None, help="只出这些贴花名")
    ap.add_argument("--bases-only", action="store_true", help="只生成 AI 材质底图缓存")
    ap.add_argument("--regen-ai", action="store_true", help="强制重出 AI 底图")
    ap.add_argument("--no-ai", action="store_true", help="用占位噪声代替 AI 底图")
    ap.add_argument("--list", action="store_true", help="列出全部贴花名")
    ap.add_argument("--no-sheet", action="store_true", help="不重出 contact sheet")
    args = ap.parse_args(argv)

    if args.list:
        for d in DECALS:
            print("%-20s %-14s %s" % (d.name, d.group, d.title))
        return 0

    os.makedirs(OUT_DIR, exist_ok=True)
    keys = sorted({k for d in DECALS for k in d.bases})
    print("AI 材质底图 %d 张 -> %s" % (len(keys), BASE_CACHE))
    print("ComfyUI: %s (%s)" % (COMFY, "在线" if _comfy_alive() else "离线"))

    if args.bases_only:
        for k in keys:
            p = generate_base(k, force=args.regen_ai)
            print("  cached:", p)
        return 0

    t0 = time.time()
    bases = load_bases(keys, force=args.regen_ai, no_ai=args.no_ai)
    print("底图就绪 %.1fs" % (time.time() - t0))

    todo = DECALS
    if args.only:
        want = {n if n.startswith("D_") else "D_" + n for n in args.only}
        todo = [d for d in DECALS if d.name in want]
        missing = want - {d.name for d in todo}
        if missing:
            print("未知贴花: %s" % ", ".join(sorted(missing)))
            return 2

    rows = []
    items = []
    bad = []
    print("\n%-20s %6s %7s %7s %6s  %s" % ("name", "size", "cov%", "clear", "amax", "crc32"))
    for d in todo:
        t = time.time()
        rgb, a = render(d, bases)
        im = to_rgba8(rgb, a)
        path = os.path.join(OUT_DIR, d.name + ".png")
        im.save(path, "PNG")
        st = stats(a)
        crc = zlib.crc32(im.tobytes())
        d._amax = st["a_max"]
        rows.append((d, st))
        items.append((d, im))
        flag = ""
        lo, hi = d.cov
        if not (lo <= st["cov"] <= hi):
            flag += " !COV"
            bad.append((d.name, "coverage %.1f%% not in [%.0f,%.0f]" % (st["cov"], lo, hi)))
        if st["a_max"] < 0.35:
            flag += " !FAINT"
            bad.append((d.name, "alpha peak %.2f too faint" % st["a_max"]))
        if st["clear"] > 0.0:
            flag += " !BORDER"
            bad.append((d.name, "border 2px alpha %.4f (should be 0)" % st["clear"]))
        print("%-20s %6s %7.1f %7.4f %6.2f  %08x  %.1fs%s"
              % (d.name, "512x512", st["cov"], st["clear"], st["a_max"], crc,
                 time.time() - t, flag))

    if not args.no_sheet:
        make_contact_sheet(items, SHEET_PATH)
        print("\ncontact sheet -> %s" % SHEET_PATH)

    # README 只在全量生成时重写，避免 --only 时覆盖率表不全
    if len(todo) == len(DECALS):
        write_readme(rows, README_PATH)
        print("README -> %s" % README_PATH)

    print("\n共 %d 张，用时 %.1fs" % (len(todo), time.time() - t0))
    if bad:
        print("\n需要关注：")
        for n, why in bad:
            print("  - %s: %s" % (n, why))
        return 1
    print("自检通过：四边 2px alpha=0，覆盖率/峰值在预期区间。")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
