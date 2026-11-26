#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""ESCAPE FROM WORK — 导视 / 标识贴图生成器 (signage decals)

纯 PIL 绘制，不依赖外部图片。幂等：重复运行直接覆盖同名 PNG。
输出目录 = 本脚本所在目录。

用法:
    python generate_signage.py
"""

from __future__ import annotations

import math
import os
import sys
import zlib
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFont

# --------------------------------------------------------------------------- #
# 基础配置
# --------------------------------------------------------------------------- #

ROOT = Path(__file__).resolve().parent

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

FONT_DIR = Path(r"C:\Windows\Fonts")
F_ZH = FONT_DIR / "msyh.ttc"        # 微软雅黑
F_ZHB = FONT_DIR / "msyhbd.ttc"     # 微软雅黑 Bold
F_EN = FONT_DIR / "arial.ttf"
F_ENB = FONT_DIR / "arialbd.ttf"

for _p in (F_ZH, F_ZHB, F_EN, F_ENB):
    if not _p.exists():
        raise SystemExit(f"缺少字体文件: {_p}")

# 调色板
GREEN = (30, 123, 77, 255)          # #1E7B4D 安全绿
GREEN_DK = (17, 86, 54, 255)
WHITE = (246, 247, 244, 255)
PAPER = (236, 237, 232, 255)
INK = (32, 35, 39, 255)
INK_SOFT = (74, 79, 84, 255)
YELLOW = (232, 179, 42, 255)
BLUE = (42, 92, 138, 255)
RED = (176, 48, 42, 255)
GREY = (128, 133, 137, 255)

OUT: list[tuple[str, str, Path]] = []   # (group, name, path)


# --------------------------------------------------------------------------- #
# 字体 / 文本工具
# --------------------------------------------------------------------------- #

_FONT_CACHE: dict[tuple[str, int], ImageFont.FreeTypeFont] = {}


def font(path: Path | str, size: int) -> ImageFont.FreeTypeFont:
    key = (str(path), int(size))
    if key not in _FONT_CACHE:
        _FONT_CACHE[key] = ImageFont.truetype(str(path), int(size))
    return _FONT_CACHE[key]


_MEASURE = ImageDraw.Draw(Image.new("L", (8, 8)))


def ink_bbox(txt: str, f: ImageFont.FreeTypeFont) -> tuple[int, int, int, int]:
    return _MEASURE.textbbox((0, 0), txt, font=f)


def ink_size(txt: str, f: ImageFont.FreeTypeFont) -> tuple[int, int]:
    b = ink_bbox(txt, f)
    return b[2] - b[0], b[3] - b[1]


def fit_font(txt: str, path: Path | str, max_w: float, max_h: float,
             start: int = 400, min_size: int = 8) -> ImageFont.FreeTypeFont:
    """从 start 往下找第一个宽高都塞得进 max_w x max_h 的字号。"""
    size = int(start)
    while size > min_size:
        f = font(path, size)
        w, h = ink_size(txt, f)
        if w <= max_w and h <= max_h:
            return f
        size -= 2
    return font(path, min_size)


def draw_text_c(d: ImageDraw.ImageDraw, xy: tuple[float, float], txt: str,
                f: ImageFont.FreeTypeFont, fill, tracking: float = 0.0) -> None:
    """按墨迹包围盒把文本光学居中到 xy。tracking = 额外字距(px)。"""
    if not tracking:
        b = ink_bbox(txt, f)
        w, h = b[2] - b[0], b[3] - b[1]
        d.text((xy[0] - w / 2 - b[0], xy[1] - h / 2 - b[1]), txt, font=f, fill=fill)
        return
    total = tracked_width(d, txt, f, tracking)
    x = xy[0] - total / 2
    b = ink_bbox(txt, f)
    y = xy[1] - (b[3] - b[1]) / 2 - b[1]
    for ch in txt:
        d.text((x, y), ch, font=f, fill=fill)
        x += d.textlength(ch, font=f) + tracking


def tracked_width(d: ImageDraw.ImageDraw, txt: str,
                  f: ImageFont.FreeTypeFont, tracking: float) -> float:
    if not txt:
        return 0.0
    return sum(d.textlength(c, font=f) for c in txt) + tracking * (len(txt) - 1)


def draw_text_l(d: ImageDraw.ImageDraw, xy: tuple[float, float], txt: str,
                f: ImageFont.FreeTypeFont, fill) -> None:
    """左对齐 + 垂直光学居中。xy = 左边中点。"""
    b = ink_bbox(txt, f)
    d.text((xy[0] - b[0], xy[1] - (b[3] - b[1]) / 2 - b[1]), txt, font=f, fill=fill)


def draw_text_r(d: ImageDraw.ImageDraw, xy: tuple[float, float], txt: str,
                f: ImageFont.FreeTypeFont, fill) -> None:
    """右对齐 + 垂直光学居中。xy = 右边中点。"""
    b = ink_bbox(txt, f)
    w = b[2] - b[0]
    d.text((xy[0] - w - b[0], xy[1] - (b[3] - b[1]) / 2 - b[1]), txt, font=f, fill=fill)


def draw_tracked_l(d: ImageDraw.ImageDraw, xy: tuple[float, float], txt: str,
                   f: ImageFont.FreeTypeFont, fill, tracking: float) -> float:
    """左对齐 + 字距的拉丁文。xy = 左边中点。返回总宽。"""
    b = ink_bbox(txt, f)
    y = xy[1] - (b[3] - b[1]) / 2 - b[1]
    x = xy[0]
    for ch in txt:
        d.text((x, y), ch, font=f, fill=fill)
        x += d.textlength(ch, font=f) + tracking
    return x - tracking - xy[0]


# --------------------------------------------------------------------------- #
# 绘图工具：超采样笔
# --------------------------------------------------------------------------- #

class Pen:
    """在 ss 倍画布上画图元，坐标仍用最终像素，最后 LANCZOS 缩回。"""

    def __init__(self, w: int, h: int, ss: int = 4):
        self.w, self.h, self.ss = w, h, ss
        self.img = Image.new("RGBA", (w * ss, h * ss), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)

    # -- 内部 --
    def _pt(self, p):
        return (p[0] * self.ss, p[1] * self.ss)

    def _w(self, width):
        return max(1, int(round(width * self.ss)))

    # -- 图元 --
    def line(self, pts, fill, width: float, caps: bool = True, joint: str = "curve"):
        w = self._w(width)
        p = [self._pt(q) for q in pts]
        if len(p) >= 2:
            self.d.line(p, fill=fill, width=w, joint=joint)
        if caps:
            r = w / 2.0
            for q in (p[0], p[-1]):
                self.d.ellipse([q[0] - r, q[1] - r, q[0] + r, q[1] + r], fill=fill)

    def poly(self, pts, fill):
        self.d.polygon([self._pt(q) for q in pts], fill=fill)

    def ellipse(self, cx: float, cy: float, rx: float, ry: float,
                fill=None, outline=None, width: float = 0):
        s = self.ss
        box = [(cx - rx) * s, (cy - ry) * s, (cx + rx) * s, (cy + ry) * s]
        self.d.ellipse(box, fill=fill, outline=outline,
                       width=self._w(width) if width else 0)

    def circle(self, cx: float, cy: float, r: float, fill=None,
               outline=None, width: float = 0):
        self.ellipse(cx, cy, r, r, fill=fill, outline=outline, width=width)

    def rect(self, x0, y0, x1, y1, fill=None, outline=None, width: float = 0):
        s = self.ss
        self.d.rectangle([x0 * s, y0 * s, x1 * s, y1 * s], fill=fill,
                         outline=outline, width=self._w(width) if width else 0)

    def rrect(self, x0, y0, x1, y1, radius: float, fill=None,
              outline=None, width: float = 0):
        s = self.ss
        self.d.rounded_rectangle([x0 * s, y0 * s, x1 * s, y1 * s],
                                 radius=radius * s, fill=fill, outline=outline,
                                 width=self._w(width) if width else 0)

    def finish(self) -> Image.Image:
        return self.img.resize((self.w, self.h), Image.LANCZOS)


def composite(base: Image.Image, pen: Pen) -> Image.Image:
    base.alpha_composite(pen.finish())
    return base


def rounded_alpha(img: Image.Image, radius: float = 14, border: int = 3) -> Image.Image:
    """加 2–4px 透明边 + 圆角，避免贴上去硬切。"""
    w, h = img.size
    ss = 4
    m = Image.new("L", (w * ss, h * ss), 0)
    ImageDraw.Draw(m).rounded_rectangle(
        [border * ss, border * ss, (w - border) * ss - 1, (h - border) * ss - 1],
        radius=radius * ss, fill=255)
    m = m.resize((w, h), Image.LANCZOS)
    img.putalpha(ImageChops.multiply(img.getchannel("A"), m))
    return img


def save(img: Image.Image, group: str, name: str) -> Path:
    assert name.startswith("S_"), name
    folder = ROOT / group
    folder.mkdir(parents=True, exist_ok=True)
    path = folder / name
    img.convert("RGBA").save(path, "PNG", optimize=True)
    OUT.append((group, name, path))
    print(f"  wrote {path.relative_to(ROOT)}  {img.size[0]}x{img.size[1]}")
    return path


# --------------------------------------------------------------------------- #
# 拉丝金属底
# --------------------------------------------------------------------------- #

def _box1d(a: np.ndarray, k: int, axis: int) -> np.ndarray:
    if k <= 1:
        return a
    a = np.moveaxis(a, axis, 0)
    lo = k // 2
    hi = k - 1 - lo
    p = np.pad(a, ((lo, hi,), (0, 0)), mode="edge")
    c = np.cumsum(p, axis=0, dtype=np.float64)
    c = np.concatenate([np.zeros((1,) + a.shape[1:]), c], axis=0)
    out = (c[k:] - c[:-k]) / float(k)
    return np.moveaxis(out.astype(np.float32), 0, axis)


def brushed_metal(w: int, h: int, base=(0.42, 0.44, 0.46),
                  seed: int = 7, vertical: bool = False) -> Image.Image:
    rng = np.random.default_rng(seed)
    # 沿拉丝方向平滑的细噪声 = 拉丝纹理
    fine = rng.normal(0, 1, (w, h)).astype(np.float32)
    fine = _box1d(fine, 21, axis=1 if vertical else 0)
    fine /= (np.abs(fine).max() + 1e-6)
    # 缓慢的明暗带
    if vertical:
        streak = _box1d(rng.normal(0, 1, (w, 1)).astype(np.float32), 7, axis=0)
    else:
        streak = _box1d(rng.normal(0, 1, (1, h)).astype(np.float32), 7, axis=1)
    streak /= (np.abs(streak).max() + 1e-6)

    lum = np.clip(1.0 + 0.055 * fine + 0.05 * streak, 0.72, 1.22).T  # -> (h, w)

    arr = np.empty((h, w, 3), dtype=np.float32)
    for i, c in enumerate(base):
        arr[:, :, i] = np.clip(c * lum * 255.0, 0, 255)
    return Image.fromarray(arr.astype(np.uint8), "RGB").convert("RGBA")


def metal_edge(img: Image.Image, inset: int, light=(255, 255, 255, 26),
               dark=(0, 0, 0, 60)) -> Image.Image:
    """给金属板加一圈很薄的倒角高光/暗边。"""
    d = ImageDraw.Draw(img)
    w, h = img.size
    d.rectangle([inset, inset, w - inset - 1, h - inset - 1], outline=light, width=2)
    d.rectangle([inset + 2, inset + 2, w - inset - 3, h - inset - 3],
                outline=dark, width=1)
    return img


# --------------------------------------------------------------------------- #
# 图标（都在 Pen 上画，坐标 = 最终像素）
# --------------------------------------------------------------------------- #

def icon_running_man(pen: Pen, x0: float, y0: float, h: float, fill):
    """ISO 7010 E001 风格奔跑小人。x0,y0 = 包围盒左上，h = 总高。"""
    W = h * 0.66
    sx, sy = W, h
    shoulder = (x0 + 0.545 * sx, y0 + 0.200 * sy)
    hip = (x0 + 0.352 * sx, y0 + 0.500 * sy)

    lw = h * 0.072
    torso_w = h * 0.128

    # 后腿 + 后臂（先画，视觉上在身体后）
    pen.line([hip, (x0 + 0.222 * sx, y0 + 0.742 * sy),
              (x0 + 0.010 * sx, y0 + 0.968 * sy)], fill, lw)
    pen.line([shoulder, (x0 + 0.310 * sx, y0 + 0.350 * sy),
              (x0 + 0.160 * sx, y0 + 0.225 * sy)], fill, lw)

    # 躯干
    pen.line([shoulder, hip], fill, torso_w)
    pen.circle(shoulder[0], shoulder[1], torso_w * 0.5, fill=fill)

    # 前腿 + 前臂
    pen.line([hip, (x0 + 0.618 * sx, y0 + 0.700 * sy),
              (x0 + 0.888 * sx, y0 + 0.872 * sy)], fill, lw)
    pen.line([shoulder, (x0 + 0.800 * sx, y0 + 0.330 * sy),
              (x0 + 0.925 * sx, y0 + 0.425 * sy)], fill, lw)

    # 颈 + 头
    pen.line([shoulder, (x0 + 0.600 * sx, y0 + 0.160 * sy)], fill, h * 0.062)
    pen.circle(x0 + 0.600 * sx, y0 + 0.092 * sy, h * 0.082, fill=fill)


def icon_arrow_right(pen: Pen, x0: float, x1: float, cy: float,
                     shaft_h: float, head_w: float, head_len: float, fill):
    head_len = min(head_len, (x1 - x0) * 0.65)
    bx = x1 - head_len
    pen.rect(x0, cy - shaft_h / 2, bx + 2, cy + shaft_h / 2, fill=fill)
    pen.poly([(bx, cy - head_w / 2), (x1, cy), (bx, cy + head_w / 2)], fill)


def icon_arrow_left(pen: Pen, x0: float, x1: float, cy: float,
                    shaft_h: float, head_w: float, head_len: float, fill):
    head_len = min(head_len, (x1 - x0) * 0.65)
    bx = x0 + head_len
    pen.rect(bx - 2, cy - shaft_h / 2, x1, cy + shaft_h / 2, fill=fill)
    pen.poly([(bx, cy - head_w / 2), (x0, cy), (bx, cy + head_w / 2)], fill=fill)


def icon_tri_up(pen: Pen, cx: float, cy: float, w: float, h: float, fill):
    pen.poly([(cx - w / 2, cy + h / 2), (cx + w / 2, cy + h / 2), (cx, cy - h / 2)], fill)


def icon_tri_down(pen: Pen, cx: float, cy: float, w: float, h: float, fill):
    pen.poly([(cx - w / 2, cy - h / 2), (cx + w / 2, cy - h / 2), (cx, cy + h / 2)], fill)


def icon_stairs(pen: Pen, x: float, y: float, w: float, h: float, fill, lw: float = 14):
    """从左下升到右上的楼梯剖面。"""
    n = 5
    sw, sh = w / n, h / n
    pts = [(x, y + h)]
    for i in range(n):
        pts.append((x + (i + 1) * sw, y + h - i * sh))
        pts.append((x + (i + 1) * sw, y + h - (i + 1) * sh))
    pen.line(pts, fill, lw)


def icon_person(pen: Pen, cx: float, top: float, h: float, fill, female: bool = False):
    head_r = h * 0.078
    pen.circle(cx, top + head_r, head_r, fill=fill)
    sh_y = top + 0.205 * h
    hip_y = top + 0.535 * h
    bw = h * 0.125
    lw = h * 0.068

    if female:
        pen.poly([(cx - bw, sh_y), (cx + bw, sh_y),
                  (cx + bw * 1.32, hip_y + h * 0.055),
                  (cx - bw * 1.32, hip_y + h * 0.055)], fill)
        pen.line([(cx - bw * 0.98, hip_y + h * 0.045),
                  (cx - bw * 0.98, top + h * 0.955)], fill, lw)
        pen.line([(cx + bw * 0.98, hip_y + h * 0.045),
                  (cx + bw * 0.98, top + h * 0.955)], fill, lw)
    else:
        pen.poly([(cx - bw, sh_y), (cx + bw, sh_y),
                  (cx + bw * 0.84, hip_y), (cx - bw * 0.84, hip_y)], fill)
        pen.line([(cx - bw * 0.52, hip_y - h * 0.02),
                  (cx - bw * 0.52, top + h * 0.955)], fill, lw)
        pen.line([(cx + bw * 0.52, hip_y - h * 0.02),
                  (cx + bw * 0.52, top + h * 0.955)], fill, lw)

    # 手臂：贴身体外侧下垂
    pen.line([(cx - bw * 0.92, sh_y + h * 0.030),
              (cx - bw * 1.76, hip_y - h * 0.020)], fill, lw * 0.82)
    pen.line([(cx + bw * 0.92, sh_y + h * 0.030),
              (cx + bw * 1.76, hip_y - h * 0.020)], fill, lw * 0.82)


def icon_bell(pen: Pen, cx: float, cy: float, w: float, h: float, fill):
    r = w / 2
    pen.d.pieslice([(cx - r) * pen.ss, (cy - h * 0.45) * pen.ss,
                    (cx + r) * pen.ss, (cy + h * 0.25) * pen.ss],
                   180, 360, fill=fill)
    pen.poly([(cx - r, cy - h * 0.10), (cx + r, cy - h * 0.10),
              (cx + r * 1.16, cy + h * 0.30), (cx - r * 1.16, cy + h * 0.30)], fill)
    pen.line([(cx - r * 1.30, cy + h * 0.31), (cx + r * 1.30, cy + h * 0.31)],
             fill, max(4, w * 0.10))
    pen.circle(cx, cy + h * 0.42, w * 0.10, fill=fill)


def icon_extinguisher(pen: Pen, cx: float, cy: float, w: float, h: float, fill):
    bw = w * 0.52
    pen.rrect(cx - bw / 2, cy - h * 0.26, cx + bw / 2, cy + h * 0.44, w * 0.08, fill=fill)
    pen.line([(cx - bw * 0.18, cy - h * 0.26), (cx - bw * 0.18, cy - h * 0.44)], fill, max(4, w * 0.10))
    pen.line([(cx - bw * 0.55, cy - h * 0.40), (cx + bw * 0.30, cy - h * 0.40)], fill, max(4, w * 0.10))
    pen.line([(cx + bw * 0.30, cy - h * 0.40), (cx + bw * 0.62, cy - h * 0.22)], fill, max(4, w * 0.09))


def icon_door_arrows(pen: Pen, cx: float, cy: float, w: float, h: float,
                     fill, opening: bool):
    """电梯 开门(◀▶ 向外) / 关门(▶◀ 向内) 图标。"""
    hy = h * 0.5
    g = w * 0.16
    o = w * 0.50
    if opening:
        pen.poly([(cx - g, cy - hy), (cx - g, cy + hy), (cx - o, cy)], fill)
        pen.poly([(cx + g, cy - hy), (cx + g, cy + hy), (cx + o, cy)], fill)
    else:
        pen.poly([(cx - o, cy - hy), (cx - o, cy + hy), (cx - g, cy)], fill)
        pen.poly([(cx + o, cy - hy), (cx + o, cy + hy), (cx + g, cy)], fill)


def icon_server(pen: Pen, cx: float, cy: float, w: float, h: float, fill, led):
    n = 3
    uh = h * 0.26
    gap = h * 0.07
    total = n * uh + (n - 1) * gap
    y = cy - total / 2
    for i in range(n):
        pen.rrect(cx - w / 2, y, cx + w / 2, y + uh, w * 0.03,
                  fill=None, outline=fill, width=max(3, w * 0.035))
        # 插槽
        for k in range(3):
            pen.rect(cx - w * 0.38, y + uh * (0.25 + k * 0.2),
                     cx - w * 0.06, y + uh * (0.31 + k * 0.2), fill=fill)
        pen.circle(cx + w * 0.34, y + uh / 2, max(3, w * 0.045), fill=led)
        y += uh + gap


# --------------------------------------------------------------------------- #
# 1. Exit — 安全出口
# --------------------------------------------------------------------------- #

def gen_exit_green():
    W, H = 1024, 512
    img = Image.new("RGBA", (W, H), GREEN)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([9, 9, W - 10, H - 10], radius=10,
                        outline=(255, 255, 255, 40), width=3)

    pen = Pen(W, H, 4)
    white = (255, 255, 255, 240)

    # 门框（右侧敞开 = 门洞）
    dx0, dx1 = 312, 438
    dy0, dy1 = 92, 396
    pen.line([(dx0, dy0), (dx0, dy1)], white, 22)
    pen.line([(dx0 - 11, dy0), (dx1, dy0)], white, 22)
    pen.line([(dx0 - 11, dy1), (dx1, dy1)], white, 22)

    # 奔跑小人
    icon_running_man(pen, 84, 52, 312, white)

    # 底部方向箭头
    icon_arrow_right(pen, 118, 458, 458, 24, 66, 74, white)

    img = composite(img, pen)

    # 文案
    f_zh = font(F_ZHB, 104)
    draw_text_c(d, (774, 186), "安全出口", f_zh, (255, 255, 255, 250), tracking=6)
    f_en = font(F_ENB, 96)
    draw_text_c(d, (774, 344), "EXIT", f_en, (255, 255, 255, 235), tracking=14)

    save(rounded_alpha(img, radius=12, border=3), "Exit", "S_Exit_Green.png")


def gen_exit_arrows():
    for direction in ("Left", "Right"):
        W = H = 512
        img = Image.new("RGBA", (W, H), GREEN)
        d = ImageDraw.Draw(img)
        d.rounded_rectangle([7, 7, W - 8, H - 8], radius=10,
                            outline=(255, 255, 255, 40), width=3)

        pen = Pen(W, H, 4)
        white = (255, 255, 255, 240)
        if direction == "Right":
            icon_arrow_right(pen, 106, 412, 194, 40, 104, 116, white)
        else:
            icon_arrow_left(pen, 100, 406, 194, 40, 104, 116, white)

        img = composite(img, pen)

        f_en = font(F_ENB, 74)
        draw_text_c(d, (256, 312), "EXIT", f_en, (255, 255, 255, 240), tracking=10)
        f_zh = font(F_ZHB, 62)
        draw_text_c(d, (256, 406), "出口", f_zh, (255, 255, 255, 225), tracking=8)

        save(rounded_alpha(img, radius=12, border=3), "Exit",
             f"S_Exit_Arrow{direction}.png")


# --------------------------------------------------------------------------- #
# 2. Floor — 楼层牌
# --------------------------------------------------------------------------- #

def gen_floor_plate(label: str, size: tuple[int, int], name: str, seed: int):
    W, H = size
    img = brushed_metal(W, H, base=(0.60, 0.615, 0.625), seed=seed)
    metal_edge(img, 12)
    d = ImageDraw.Draw(img)

    ink = (26, 29, 33, 255)
    sub = (60, 65, 70, 255)

    # 内框
    d.rectangle([26, 26, W - 27, H - 27], outline=(255, 255, 255, 70), width=3)
    d.rectangle([30, 30, W - 31, H - 31], outline=(0, 0, 0, 34), width=2)

    if W >= 900:
        # 1024x512 正式牌
        f_num = fit_font(label, F_ENB, 600, 290, start=420)
        b = ink_bbox(label, f_num)
        nx = 60 + (696 - 60) / 2
        d.text((nx - (b[2] - b[0]) / 2 - b[0], 252 - (b[3] - b[1]) / 2 - b[1]),
               label, font=f_num, fill=ink)

        # 右栏分隔
        d.line([(712, 118), (712, 394)], fill=(40, 44, 48, 90), width=3)
        f1 = font(F_ENB, 78)
        draw_tracked_l(d, (744, 184), "EFW", f1, ink, 8)
        f2 = font(F_ENB, 52)
        draw_tracked_l(d, (746, 262), "TOWER", f2, ink, 9)
        d.rectangle([746, 300, 790, 306], fill=GREEN)
        f3 = font(F_EN, 18)
        draw_tracked_l(d, (744, 338), "ESCAPE FROM WORK", f3, sub, 1.5)
    else:
        # 512x512 母版
        f_num = fit_font(label, F_ENB, 400, 250, start=300)
        b = ink_bbox(label, f_num)
        d.text((256 - (b[2] - b[0]) / 2 - b[0], 222 - (b[3] - b[1]) / 2 - b[1]),
               label, font=f_num, fill=ink)
        d.line([(196, 352), (316, 352)], fill=(40, 44, 48, 80), width=3)
        f1 = font(F_ENB, 30)
        draw_text_c(d, (256, 392), "EFW TOWER", f1, ink, tracking=4)
        f2 = font(F_EN, 15)
        draw_text_c(d, (256, 432), "ESCAPE FROM WORK", f2, sub, tracking=2)

    save(rounded_alpha(img, radius=10, border=3), "Floor", name)


def gen_floors():
    gen_floor_plate("00F", (512, 512), "S_Floor_Plate.png", seed=31)
    for i, fl in enumerate(range(40, 51)):
        gen_floor_plate(f"{fl}F", (1024, 512), f"S_Floor_{fl}F.png", seed=100 + i)


# --------------------------------------------------------------------------- #
# 3. Evac — 疏散平面图
# --------------------------------------------------------------------------- #

def gen_evac_map():
    W, H = 1024, 768
    img = Image.new("RGBA", (W, H), PAPER)
    d = ImageDraw.Draw(img)

    # ---- 页眉 ----
    d.rectangle([0, 0, W, 118], fill=INK)
    draw_text_l(d, (42, 47), "疏散平面图", font(F_ZHB, 54), WHITE)
    draw_tracked_l(d, (44, 90), "EVACUATION PLAN", font(F_ENB, 25),
                   (196, 200, 198, 255), 4.5)
    d.rectangle([44, 106, 332, 109], fill=GREEN)

    # 楼层徽标
    pen = Pen(W, H, 4)
    pen.rrect(846, 25, 976, 93, 8, fill=GREEN)
    img = composite(img, pen)
    draw_text_c(d, (911, 59), "45F", font(F_ENB, 44), WHITE, tracking=2)

    # ---- 平面图底板 ----
    PX0, PY0, PX1, PY1 = 38, 148, 686, 686
    d.rectangle([PX0, PY0, PX1, PY1], fill=(250, 250, 246),
                outline=(196, 199, 195), width=2)

    plan = Pen(W, H, 4)
    wall = (58, 62, 66, 255)
    wt = 7

    # 房间底色（奇偶交替）
    for i, (x0, x1) in enumerate([(76, 214), (214, 352), (352, 490), (490, 660)]):
        plan.rect(x0, 196, x1, 400, fill=(246, 247, 242, 255) if i % 2 == 0
                  else (238, 240, 234, 255))
        plan.rect(x0, 478, x1, 648, fill=(238, 240, 234, 255) if i % 2 == 0
                  else (246, 247, 242, 255))
    # 走道
    plan.rect(76, 400, 660, 478, fill=(226, 229, 222, 255))

    # 核心筒（电梯）：标准"井道"符号 = 方框内交叉线
    plan.rect(524, 208, 646, 350, fill=(206, 210, 203, 255))
    plan.rect(524, 208, 646, 350, fill=None, outline=wall, width=6)
    plan.line([(530, 214), (640, 344)], wall, 4, caps=False)
    plan.line([(640, 214), (530, 344)], wall, 4, caps=False)

    # 楼梯间
    plan.rect(524, 500, 646, 632, fill=(214, 217, 210, 255))
    plan.rect(524, 500, 646, 632, fill=None, outline=wall, width=6)
    icon_stairs(plan, 540, 512, 92, 66, wall, 9)

    # 外轮廓 + 内墙
    plan.rect(76, 196, 660, 648, fill=None, outline=wall, width=wt)
    for x in (214, 352, 490):
        plan.line([(x, 196), (x, 400)], wall, 5, caps=False)
        plan.line([(x, 478), (x, 648)], wall, 5, caps=False)
    plan.line([(76, 400), (660, 400)], wall, wt, caps=False)
    plan.line([(76, 478), (660, 478)], wall, wt, caps=False)

    # 两个安全出口（绿色门洞跨在墙上）
    plan.rect(646, 406, 674, 472, fill=GREEN)
    plan.rect(62, 406, 90, 472, fill=GREEN)
    plan.poly([(654, 430), (670, 439), (654, 448)], WHITE)
    plan.poly([(82, 430), (66, 439), (82, 448)], WHITE)

    # 疏散路线（绿粗箭头，指向两侧出口）
    icon_arrow_right(plan, 360, 636, 439, 18, 48, 52, GREEN)
    icon_arrow_left(plan, 84, 360, 439, 18, 48, 52, GREEN)

    # YOU ARE HERE 红点
    plan.circle(360, 439, 15, fill=WHITE)
    plan.circle(360, 439, 11, fill=RED)

    img = composite(img, plan)

    # ---- 平面标注 ----
    f_lbl = font(F_ZH, 18)
    for x in (145, 283, 421):
        draw_text_c(d, (x, 250), "办公区", f_lbl, (96, 101, 105, 255))
    for x in (145, 283):
        draw_text_c(d, (x, 566), "会议", f_lbl, (96, 101, 105, 255))
    draw_text_c(d, (421, 566), "茶水", f_lbl, (96, 101, 105, 255))
    draw_text_c(d, (585, 370), "电梯", font(F_ZH, 17), (96, 60, 55, 255))
    draw_text_c(d, (585, 612), "楼梯", font(F_ZH, 16), (70, 74, 78, 255))

    # YOU ARE HERE 说明框
    bx0, by0, bx1, by1 = 276, 296, 486, 368
    d.line([(360, by1), (360, 426)], fill=RED, width=2)
    d.rectangle([bx0, by0, bx1, by1], fill=(255, 255, 255, 255),
                outline=RED, width=3)
    draw_text_c(d, (381, 318), "您所在位置", font(F_ZHB, 30), RED)
    draw_text_c(d, (381, 348), "YOU ARE HERE", font(F_ENB, 17), RED, tracking=2.5)

    # ---- 图例 ----
    LX0, LY0, LX1, LY1 = 702, 148, 986, 500
    d.rectangle([LX0, LY0, LX1, LY1], fill=(250, 250, 246),
                outline=(196, 199, 195), width=2)
    d.rectangle([LX0, LY0, LX1, LY0 + 6], fill=INK)
    d.text((LX0 + 18, LY0 + 18), "图例", font=font(F_ZHB, 26), fill=INK)
    draw_text_l(d, (LX0 + 118, LY0 + 34), "LEGEND", font(F_ENB, 13),
                (128, 132, 135, 255))
    d.line([(LX0 + 18, LY0 + 54), (LX1 - 18, LY0 + 54)],
           fill=(206, 209, 204), width=2)

    rows = [
        ("exit", "安全出口", "EXIT"),
        ("ext", "灭火器", "EXTINGUISHER"),
        ("stairs", "疏散楼梯", "STAIRS"),
        ("lift", "电梯（禁用）", "DO NOT USE"),
    ]
    ry = LY0 + 84
    for kind, zh, en in rows:
        lpen = Pen(W, H, 4)
        if kind == "exit":
            lpen.rect(LX0 + 20, ry - 13, LX0 + 46, ry + 13, fill=GREEN)
        elif kind == "ext":
            icon_extinguisher(lpen, LX0 + 33, ry, 26, 34, RED)
        elif kind == "stairs":
            icon_stairs(lpen, LX0 + 20, ry - 14, 26, 28, INK, 6)
        else:
            lpen.rect(LX0 + 20, ry - 14, LX0 + 46, ry + 14,
                      fill=None, outline=GREY, width=3)
            lpen.line([(LX0 + 20, ry - 14), (LX0 + 46, ry + 14)], GREY, 4)
            lpen.line([(LX0 + 46, ry - 14), (LX0 + 20, ry + 14)], GREY, 4)
        img = composite(img, lpen)
        draw_text_l(d, (LX0 + 60, ry - 8), zh, font(F_ZH, 21), INK)
        draw_text_l(d, (LX0 + 60, ry + 17), en, font(F_EN, 12),
                    (110, 114, 117, 255))
        ry += 70

    # ---- 右栏：注意事项 ----
    NX0, NY0, NX1, NY1 = 702, 512, 986, 686
    d.rectangle([NX0, NY0, NX1, NY1], fill=(250, 250, 246),
                outline=(196, 199, 195), width=2)
    d.text((NX0 + 18, NY0 + 16), "注意事项", font=font(F_ZHB, 22), fill=INK)
    draw_text_l(d, (NX0 + 122, NY0 + 29), "NOTES", font(F_ENB, 12),
                (128, 132, 135, 255))
    notes = [
        "保持疏散通道畅通",
        "请勿锁闭安全出口",
        "火灾时禁止乘坐电梯",
        "集合点：一层西侧广场",
    ]
    ny = NY0 + 56
    for n in notes:
        d.ellipse([NX0 + 20, ny + 6, NX0 + 26, ny + 12], fill=GREEN)
        draw_text_l(d, (NX0 + 38, ny + 9), n, font(F_ZH, 19), (52, 56, 60, 255))
        ny += 30

    # ---- 页脚警示条 ----
    d.rectangle([0, 700, W, 750], fill=YELLOW)
    draw_text_c(d, (W / 2, 724),
                "火灾时请勿使用电梯，请走疏散楼梯　　USE STAIRS IN FIRE — DO NOT USE ELEVATORS",
                font(F_ZHB, 23), (34, 30, 20, 255))

    save(rounded_alpha(img, radius=10, border=3), "Evac", "S_Evac_Map.png")


# --------------------------------------------------------------------------- #
# 4. Elevator — 电梯面板 / 厅牌
# --------------------------------------------------------------------------- #

def gen_elevator_panel():
    W, H = 512, 1024
    img = brushed_metal(W, H, base=(0.185, 0.196, 0.212), seed=77, vertical=True)
    metal_edge(img, 14, light=(255, 255, 255, 20), dark=(0, 0, 0, 90))
    d = ImageDraw.Draw(img)

    pen = Pen(W, H, 4)
    pale = (222, 226, 224, 255)
    dim = (120, 126, 130, 255)

    # 上下行指示
    icon_tri_up(pen, 256, 118, 34, 22, (150, 158, 160, 255))
    icon_tri_down(pen, 256, 162, 34, 22, (96, 102, 106, 255))

    # 分隔线
    pen.rect(40, 196, 472, 198, fill=(70, 76, 82, 255))

    # 楼层按钮 3x4
    labels = [str(n) for n in range(40, 51)]  # 11 个
    bw = 100
    gap = 36
    x0 = (W - (3 * bw + 2 * gap)) / 2
    y0 = 236
    for idx in range(12):
        r, c = divmod(idx, 3)
        bx = x0 + c * (bw + gap)
        by = y0 + r * (bw + gap)
        cx, cy = bx + bw / 2, by + bw / 2
        if idx < len(labels):
            active = labels[idx] == "45"
            if active:
                pen.rrect(bx, by, bx + bw, by + bw, 18, fill=(24, 96, 62, 255))
                pen.rrect(bx - 3, by - 3, bx + bw + 3, by + bw + 3, 20,
                          fill=None, outline=(86, 176, 130, 200), width=3)
            else:
                pen.rrect(bx, by, bx + bw, by + bw, 18, fill=(58, 62, 68, 255))
                pen.rrect(bx + 2, by + 2, bx + bw - 2, by + bw - 2, 16,
                          fill=None, outline=(96, 102, 108, 130), width=2)
                pen.rrect(bx + 8, by + 6, bx + bw - 8, by + 22, 8,
                          fill=(86, 91, 97, 90))
        else:
            # 警铃
            pen.rrect(bx, by, bx + bw, by + bw, 18, fill=(112, 40, 36, 255))
            pen.rrect(bx + 2, by + 2, bx + bw - 2, by + bw - 2, 16,
                      fill=None, outline=(168, 78, 70, 160), width=2)
            icon_bell(pen, cx, cy + 2, 40, 46, (238, 226, 222, 245))

    # 底部功能键
    pen.rect(40, 800, 472, 802, fill=(70, 76, 82, 255))
    for i, (opening) in enumerate((True, False)):
        bx = 106 + i * 164
        by = 836
        pen.rrect(bx, by, bx + 150, by + 92, 14, fill=(52, 56, 61, 255))
        pen.rrect(bx + 2, by + 2, bx + 150 - 2, by + 92 - 2, 12,
                  fill=None, outline=(90, 96, 102, 120), width=2)
        icon_door_arrows(pen, bx + 75, by + 32, 54, 30,
                         (216, 220, 218, 255), opening)

    # 喇叭孔
    for r in range(3):
        for c in range(9):
            pen.circle(184 + c * 18, 944 + r * 16, 3.6, fill=(78, 84, 90, 255))

    img = composite(img, pen)

    # 文字
    f_brand = font(F_ENB, 26)
    draw_text_c(d, (256, 44), "EFW", f_brand, (176, 182, 186, 255), tracking=8)

    f_btn = font(F_ENB, 38)
    for idx, lab in enumerate(labels):
        r, c = divmod(idx, 3)
        bx = x0 + c * (bw + gap)
        by = y0 + r * (bw + gap)
        col = (250, 250, 248, 255) if lab != "45" else (244, 246, 240, 255)
        draw_text_c(d, (bx + bw / 2, by + bw / 2), lab, f_btn, col, tracking=1)

    f_fn = font(F_ZH, 24)
    draw_text_c(d, (181, 902), "开门", f_fn, (200, 205, 203, 255))
    draw_text_c(d, (345, 902), "关门", f_fn, (200, 205, 203, 255))

    save(rounded_alpha(img, radius=12, border=3), "Elevator", "S_Elevator_Panel.png")


def gen_elevator_hall():
    W, H = 1024, 512
    img = brushed_metal(W, H, base=(0.175, 0.186, 0.200), seed=91)
    metal_edge(img, 14, light=(255, 255, 255, 20), dark=(0, 0, 0, 90))
    d = ImageDraw.Draw(img)

    d.line([(366, 92), (366, 420)], fill=(255, 255, 255, 28), width=2)

    pen = Pen(W, H, 4)
    # 上行箭头（当前方向，绿色点亮）
    icon_tri_up(pen, 884, 190, 56, 42, (86, 178, 132, 255))
    icon_tri_down(pen, 884, 304, 56, 42, (92, 98, 104, 255))
    pen.rect(60, 296, 122, 302, fill=GREEN)
    img = composite(img, pen)

    f_zh = font(F_ZHB, 64)
    draw_text_l(d, (60, 190), "电梯", f_zh, (238, 241, 239, 255))
    f_en = font(F_ENB, 27)
    draw_tracked_l(d, (64, 262), "ELEVATOR", f_en, (168, 174, 178, 255), 9)
    draw_tracked_l(d, (64, 332), "EFW TOWER", font(F_ENB, 18), (108, 114, 118, 255), 4)

    # 当前楼层大数字
    num = "45"
    f_floor = font(F_ENB, 240)
    bn = ink_bbox(num, f_floor)
    nw, nh = bn[2] - bn[0], bn[3] - bn[1]
    ncx, ncy = 596, 246
    d.text((ncx - nw / 2 - bn[0], ncy - nh / 2 - bn[1]),
           num, font=f_floor, fill=(242, 245, 242, 255))

    # 楼层后缀 F：墨迹底与数字对齐，紧跟数字右侧
    f_f = font(F_ENB, 88)
    bf = ink_bbox("F", f_f)
    fw, fh = bf[2] - bf[0], bf[3] - bf[1]
    fx = ncx + nw / 2 + 12
    fy = ncy + nh / 2 - fh
    d.text((fx - bf[0], fy - bf[1]), "F", font=f_f, fill=(184, 190, 192, 255))

    draw_text_c(d, (596, 400), "当前楼层", font(F_ZH, 22), (150, 156, 160, 255))
    draw_text_c(d, (596, 430), "CURRENT FLOOR", font(F_ENB, 12),
                (110, 116, 120, 255), tracking=3)

    save(rounded_alpha(img, radius=12, border=3), "Elevator", "S_Elevator_Hall.png")


# --------------------------------------------------------------------------- #
# 5. Wayfinding — 指引
# --------------------------------------------------------------------------- #

def _way_plate(w: int, h: int, name: str, drawer, cjk: str, eng: str):
    img = brushed_metal(w, h, base=(0.225, 0.238, 0.252),
                        seed=zlib.crc32(name.encode("utf-8")) % 9999)
    metal_edge(img, 10, light=(255, 255, 255, 22), dark=(0, 0, 0, 70))
    d = ImageDraw.Draw(img)
    d.rectangle([18, 18, w - 19, h - 19], outline=(255, 255, 255, 26), width=2)

    pen = Pen(w, h, 4)
    drawer(pen, (255, 255, 255, 240))
    img = composite(img, pen)

    f_zh = font(F_ZHB, 54)
    draw_text_c(d, (w / 2, h - 112), cjk, f_zh, (245, 247, 244, 250), tracking=4)
    f_en = font(F_ENB, 21)
    draw_text_c(d, (w / 2, h - 58), eng, f_en, (172, 179, 182, 255), tracking=5)

    save(rounded_alpha(img, radius=10, border=3), "Wayfinding", name)


def gen_wayfinding():
    def stairs(pen, col):
        icon_stairs(pen, 148, 92, 216, 192, col, 20)

    _way_plate(512, 512, "S_Way_Stairs.png", stairs, "楼梯", "STAIRS")

    def toilet(pen, col):
        icon_person(pen, 154, 84, 210, col, female=False)
        icon_person(pen, 300, 84, 210, col, female=True)
        pen.line([(227, 108), (227, 296)], col, 5)

    _way_plate(512, 512, "S_Way_Toilet.png", toilet, "卫生间", "TOILET")

    def server(pen, col):
        icon_server(pen, 256, 196, 196, 196, col, (96, 220, 150, 255))

    _way_plate(512, 512, "S_Way_Server.png", server, "机房", "SERVER ROOM")

    # 长条方向箭头牌
    W, H = 512, 256
    img = Image.new("RGBA", (W, H), (246, 247, 243, 255))
    d = ImageDraw.Draw(img)
    d.rectangle([12, 12, W - 13, H - 13], outline=(120, 125, 128, 90), width=2)
    pen = Pen(W, H, 4)
    icon_arrow_right(pen, 68, 448, 128, 54, 138, 112, (54, 58, 63, 250))
    img = composite(img, pen)
    save(rounded_alpha(img, radius=8, border=3), "Wayfinding", "S_Way_Arrow.png")


# --------------------------------------------------------------------------- #
# 6. Room — 门牌
# --------------------------------------------------------------------------- #

DEPTS = [
    ("行政", "ADMIN"),
    ("财务", "FINANCE"),
    ("研发", "R&D"),
    ("会议 A", "MEETING A"),
    ("会议 B", "MEETING B"),
    ("档案", "ARCHIVE"),
    ("机房", "SERVER"),
    ("茶水间", "PANTRY"),
]


def gen_room_plate(number: str, dept: tuple[str, str], name: str):
    W, H = 512, 256
    img = Image.new("RGBA", (W, H), (246, 247, 244, 255))
    d = ImageDraw.Draw(img)

    # 左侧色条 + 细边
    d.rectangle([0, 0, 12, H], fill=BLUE)
    d.rectangle([12, 0, W, H], outline=(214, 217, 212, 255), width=2)
    d.line([(36, 176), (476, 176)], fill=(216, 219, 214, 255), width=2)

    f_num = font(F_ENB, 104)
    d.text((38, 56), number, font=f_num, fill=(30, 34, 38, 255))

    # 部门名：底部右对齐，中文 + 小号英文同行
    zh, en = dept
    f_zh = font(F_ZHB, 26)
    f_enz = font(F_ENB, 15)
    zw = ink_size(zh, f_zh)[0]
    ew = ink_size(en, f_enz)[0]
    gap = 13
    x = 474 - (zw + gap + ew)
    draw_text_l(d, (x, 208), zh, f_zh, (58, 63, 67, 255))
    draw_text_l(d, (x + zw + gap, 212), en, f_enz, (122, 128, 132, 255))

    f_brand = font(F_ENB, 15)
    draw_text_l(d, (40, 34), "EFW TOWER", f_brand, (140, 145, 149, 255))
    d.rectangle([148, 30, 178, 33], fill=BLUE)

    save(rounded_alpha(img, radius=8, border=3), "Room", name)


def gen_rooms():
    gen_room_plate("5001", DEPTS[0], "S_Room_Plate_Master.png")
    for i in range(12):
        num = 5001 + i
        dept = DEPTS[i % len(DEPTS)]
        gen_room_plate(str(num), dept, f"S_Room_{num}.png")


# --------------------------------------------------------------------------- #
# 7. Contact sheet
# --------------------------------------------------------------------------- #

GROUP_LABEL = {
    "Exit": "EXIT / 出口",
    "Floor": "FLOOR / 楼层",
    "Evac": "EVAC / 疏散",
    "Elevator": "ELEVATOR / 电梯",
    "Wayfinding": "WAYFINDING / 指引",
    "Room": "ROOM / 门牌",
}
GROUP_COLOR = {
    "Exit": (74, 170, 118),
    "Floor": (150, 160, 168),
    "Evac": (214, 96, 88),
    "Elevator": (92, 146, 196),
    "Wayfinding": (200, 160, 74),
    "Room": (150, 150, 210),
}


def gen_contact_sheet():
    order = ["Exit", "Floor", "Evac", "Elevator", "Wayfinding", "Room"]
    entries = sorted(OUT, key=lambda e: (order.index(e[0]), e[1]))

    cols = 6
    cell_w, cell_h = 330, 286
    pad = 22
    header = 132
    rows = math.ceil(len(entries) / cols)
    W = cols * cell_w + (cols + 1) * pad
    H = header + rows * cell_h + (rows + 1) * pad

    img = Image.new("RGBA", (W, H), (23, 25, 28, 255))
    d = ImageDraw.Draw(img)

    d.text((pad + 6, 30), "ESCAPE FROM WORK", font=font(F_ENB, 30),
           fill=(238, 241, 238, 255))
    draw_text_l(d, (pad + 6, 76), "SIGNAGE DECAL ATLAS  ·  导视贴图总览",
                font(F_ZHB, 21), (150, 156, 160, 255))
    draw_text_r(d, (W - pad - 6, 46), f"{len(entries)} TEXTS  ·  2026",
                font(F_ENB, 15), (110, 116, 120, 255))
    d.line([(pad, header - 24), (W - pad, header - 24)],
           fill=(56, 60, 64, 255), width=1)

    f_name = font(F_EN, 13)
    f_grp = font(F_ENB, 11)

    for i, (grp, name, path) in enumerate(entries):
        r, c = divmod(i, cols)
        cx = pad + c * (cell_w + pad)
        cy = header + pad + r * (cell_h + pad)
        # 格子底
        d.rounded_rectangle([cx, cy, cx + cell_w, cy + cell_h - 6], radius=8,
                            fill=(32, 35, 39, 255), outline=(52, 56, 60, 255), width=1)

        thumb = Image.open(path).convert("RGBA")
        max_w, max_h = cell_w - 30, cell_h - 62
        scale = min(max_w / thumb.width, max_h / thumb.height, 1.0)
        tw, th = max(1, int(thumb.width * scale)), max(1, int(thumb.height * scale))
        thumb = thumb.resize((tw, th), Image.LANCZOS)

        bx = cx + (cell_w - tw) // 2
        by = cy + 14 + (max_h - th) // 2
        # 棋盘格衬底，便于看透明边
        for yy in range(0, th, 12):
            for xx in range(0, tw, 12):
                col = (58, 61, 65, 255) if ((xx // 12) + (yy // 12)) % 2 else (46, 49, 53, 255)
                d.rectangle([bx + xx, by + yy,
                             bx + min(xx + 12, tw), by + min(yy + 12, th)], fill=col)
        img.alpha_composite(thumb, (bx, by))

        d.text((cx + 12, cy + cell_h - 44), GROUP_LABEL[grp].split(" / ")[0],
               font=f_grp, fill=GROUP_COLOR[grp])
        f_it = font(F_EN, 13)
        tw2 = d.textlength(name, font=f_it)
        d.text((cx + cell_w - 12 - tw2, cy + cell_h - 45), name,
               font=f_it, fill=(196, 201, 204, 255))

    out = ROOT / "_contact_sheet.png"
    img.convert("RGBA").save(out, "PNG", optimize=True)
    print(f"  wrote {out.relative_to(ROOT)}  {W}x{H}")


# --------------------------------------------------------------------------- #
# main
# --------------------------------------------------------------------------- #

def main():
    print("ESCAPE FROM WORK — signage generator")
    print(f"out: {ROOT}")
    print("[1/7] Exit")
    gen_exit_green()
    gen_exit_arrows()
    print("[2/7] Floor")
    gen_floors()
    print("[3/7] Evac")
    gen_evac_map()
    print("[4/7] Elevator")
    gen_elevator_panel()
    gen_elevator_hall()
    print("[5/7] Wayfinding")
    gen_wayfinding()
    print("[6/7] Room")
    gen_rooms()
    print("[7/7] Contact sheet")
    gen_contact_sheet()
    print(f"done: {len(OUT)} textures")


if __name__ == "__main__":
    main()
