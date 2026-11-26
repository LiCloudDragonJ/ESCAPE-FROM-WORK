#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ESCAPE FROM WORK -- HUD / UI icon set generator.

Pure-Pillow vector-style drawing (lines / arcs / beziers / polygons), rendered at
4x supersampling and LANCZOS-downsampled.  All output is white (255,255,255) with
a transparent background so Unity can tint it via Image.color / vertex colour.

Run:
    python generate_ui_icons.py

Idempotent: re-running overwrites the generated PNGs, the contact sheet and README.md.
Outputs stay inside Art/UI/ :
    Crosshair/  5 x 512x512
    Icons/     17 x 256x256
    Map/        9 x 256x256 (compass is 128x128)
    _contact_sheet.png
    README.md
"""

from __future__ import annotations

import math
import os
from dataclasses import dataclass
from typing import Callable, Iterable

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
SS = 4                       # supersampling factor
WHITE = (255, 255, 255, 255)
DETAIL = 6.0                 # secondary / inner detail stroke width @256

_FONT_CJK = r"C:\Windows\Fonts\msyh.ttc"
_FONT_CJK_BOLD = r"C:\Windows\Fonts\msyhbd.ttc"
_FONT_MONO = r"C:\Windows\Fonts\consola.ttf"


def _font(path, size):
    try:
        return ImageFont.truetype(path, size)
    except OSError:
        return ImageFont.load_default(size=size)


# --------------------------------------------------------------------------- #
# drawing helpers
# --------------------------------------------------------------------------- #
def _cubic(p0, p1, p2, p3, n=28):
    """Sample a cubic bezier, inclusive of both ends."""
    out = []
    for i in range(n + 1):
        t = i / n
        mt = 1.0 - t
        x = (mt ** 3) * p0[0] + 3 * mt * mt * t * p1[0] + 3 * mt * t * t * p2[0] + t ** 3 * p3[0]
        y = (mt ** 3) * p0[1] + 3 * mt * mt * t * p1[1] + 3 * mt * t * t * p2[1] + t ** 3 * p3[1]
        out.append((x, y))
    return out


def _quad(p0, p1, p2, n=8):
    out = []
    for i in range(n + 1):
        t = i / n
        mt = 1.0 - t
        out.append((mt * mt * p0[0] + 2 * mt * t * p1[0] + t * t * p2[0],
                    mt * mt * p0[1] + 2 * mt * t * p1[1] + t * t * p2[1]))
    return out


def _arc_pts(cx, cy, r, a0, a1, n=56):
    """Angles in degrees, screen space (0=right, 90=down)."""
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)),
             cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


def _dist(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def _path_round(pts, r, closed=False, n=8):
    """Polyline with quadratic-rounded interior corners (r = corner radius)."""
    pts = [tuple(p) for p in pts]
    if len(pts) < 3:
        return pts
    out = []
    if closed:
        for i, p in enumerate(pts):
            prev, nxt = pts[i - 1], pts[(i + 1) % len(pts)]
            r1 = min(r, _dist(p, prev) / 2)
            r2 = min(r, _dist(p, nxt) / 2)
            d1 = ((prev[0] - p[0]) / _dist(p, prev), (prev[1] - p[1]) / _dist(p, prev))
            d2 = ((nxt[0] - p[0]) / _dist(p, nxt), (nxt[1] - p[1]) / _dist(p, nxt))
            a = (p[0] + d1[0] * r1, p[1] + d1[1] * r1)
            b = (p[0] + d2[0] * r2, p[1] + d2[1] * r2)
            out.extend(_quad(a, p, b, n))
    else:
        out.append(pts[0])
        for i in range(1, len(pts) - 1):
            p, prev, nxt = pts[i], pts[i - 1], pts[i + 1]
            r1 = min(r, _dist(p, prev) / 2)
            r2 = min(r, _dist(p, nxt) / 2)
            d1 = ((prev[0] - p[0]) / _dist(p, prev), (prev[1] - p[1]) / _dist(p, prev))
            d2 = ((nxt[0] - p[0]) / _dist(p, nxt), (nxt[1] - p[1]) / _dist(p, nxt))
            a = (p[0] + d1[0] * r1, p[1] + d1[1] * r1)
            b = (p[0] + d2[0] * r2, p[1] + d2[1] * r2)
            out.extend(_quad(a, p, b, n))
        out.append(pts[-1])
    return out


class Cv:
    """Small vector canvas: coordinates are in final-icon pixels, drawing at 4x."""

    def __init__(self, size: int, stroke: float = 8.0):
        self.size = size
        self.stroke = float(stroke)
        self.img = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)

    # -- low level -------------------------------------------------------- #
    def _pts(self, pts):
        return [(float(x) * SS, float(y) * SS) for x, y in pts]

    def line(self, pts, w=None, fill=WHITE, caps=True):
        w = self.stroke if w is None else float(w)
        pp = self._pts(pts)
        self.d.line(pp, fill=fill, width=max(1, int(round(w * SS))), joint="curve")
        if caps:
            r = w * SS / 2.0
            for x, y in (pp[0], pp[-1]):
                self.d.ellipse([x - r, y - r, x + r, y + r], fill=fill)

    def closed(self, pts, w=None, fill=WHITE):
        self.line(list(pts) + [pts[0]], w=w, fill=fill)

    def arc(self, cx, cy, r, a0, a1, w=None, fill=WHITE):
        self.line(_arc_pts(cx, cy, r, a0, a1), w=w, fill=fill)

    def circle(self, cx, cy, r, w=None, fill=WHITE):
        w = self.stroke if w is None else float(w)
        rr = r * SS
        self.d.ellipse([cx * SS - rr, cy * SS - rr, cx * SS + rr, cy * SS + rr],
                       outline=fill, width=max(1, int(round(w * SS))))

    def disk(self, cx, cy, r, fill=WHITE):
        rr = r * SS
        self.d.ellipse([cx * SS - rr, cy * SS - rr, cx * SS + rr, cy * SS + rr], fill=fill)

    def rrect(self, x0, y0, x1, y1, rad, w=None, fill=WHITE):
        w = self.stroke if w is None else float(w)
        self.d.rounded_rectangle([x0 * SS, y0 * SS, x1 * SS, y1 * SS],
                                 radius=rad * SS, outline=fill,
                                 width=max(1, int(round(w * SS))))

    def fill_poly(self, pts, r=3.0, fill=WHITE):
        """Filled polygon with slightly rounded corners."""
        pp = self._pts(pts)
        self.d.polygon(pp, fill=fill)
        lw = max(1, int(round(2.0 * r * SS)))
        self.d.line(pp + [pp[0]], fill=fill, width=lw, joint="curve")
        rr = r * SS
        for x, y in pp:
            self.d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=fill)

    def fill_path(self, path, grow=3.0, fill=WHITE):
        """Filled closed bezier path; `grow` rounds/expands the silhouette edge."""
        pp = self._pts(path)
        self.d.polygon(pp, fill=fill)
        if grow > 0:
            self.d.line(pp + [pp[0]], fill=fill,
                        width=max(1, int(round(2.0 * grow * SS))), joint="curve")

    def trim(self):
        return self.img.resize((self.size, self.size), Image.LANCZOS)


# --------------------------------------------------------------------------- #
# 1. crosshairs / hit feedback -- 512 canvas, thin 14px stroke
# --------------------------------------------------------------------------- #
def cross_dot(cv: Cv):
    c = 256.0
    cv.disk(c, c, 9)
    for a in (0, 90, 180, 270):
        dx, dy = math.cos(math.radians(a)), math.sin(math.radians(a))
        cv.line([(c + dx * 40, c + dy * 40), (c + dx * 100, c + dy * 100)], w=14)


def cross_cross(cv: Cv):
    c = 256.0
    for a in (0, 90, 180, 270):
        dx, dy = math.cos(math.radians(a)), math.sin(math.radians(a))
        cv.line([(c + dx * 36, c + dy * 36), (c + dx * 186, c + dy * 186)], w=14)


def cross_circle(cv: Cv):
    cv.circle(256, 256, 118, w=12)
    cv.disk(256, 256, 8)


def cross_chevron(cv: Cv):
    c = 256.0
    for a in (0, 90, 180, 270):
        ang = math.radians(a)
        ax, ay = math.cos(ang), math.sin(ang)
        px, py = -ay, ax
        tip = (c + ax * 52, c + ay * 52)
        b1 = (c + ax * 110 + px * 28, c + ay * 110 + py * 28)
        b2 = (c + ax * 110 - px * 28, c + ay * 110 - py * 28)
        cv.fill_poly([tip, b1, b2], r=2)


def hitmarker(cv: Cv):
    c = 256.0
    cv.disk(c, c, 7)
    for a in (45, 135, 225, 315):
        dx, dy = math.cos(math.radians(a)), math.sin(math.radians(a))
        cv.line([(c + dx * 42, c + dy * 42), (c + dx * 118, c + dy * 118)], w=13)


# --------------------------------------------------------------------------- #
# 2. survival
# --------------------------------------------------------------------------- #
def health(cv: Cv):
    p = [(128, 220)]
    p += _cubic((128, 220), (108, 202), (32, 152), (32, 92))[1:]
    p += _cubic((32, 92), (32, 56), (60, 32), (90, 32))[1:]
    p += _cubic((90, 32), (110, 32), (122, 44), (128, 56))[1:]
    p += _cubic((128, 56), (134, 44), (146, 32), (166, 32))[1:]
    p += _cubic((166, 32), (196, 32), (224, 56), (224, 92))[1:]
    p += _cubic((224, 92), (224, 152), (148, 202), (128, 220))[1:]
    cv.closed(p)


def armor(cv: Cv):
    p = [(60, 52), (196, 52), (196, 100)]
    p += _cubic((196, 100), (196, 150), (168, 186), (128, 214))[1:]
    p += _cubic((128, 214), (88, 186), (60, 150), (60, 100))[1:]
    cv.closed(p)


def stamina(cv: Cv):
    cv.fill_poly([(162, 26), (80, 142), (120, 142), (104, 230), (176, 112), (136, 112)], r=3)


def weight(cv: Cv):
    cv.arc(128, 86, 28, 180, 360)
    cv.rrect(62, 88, 194, 224, 22)
    cv.rrect(90, 150, 166, 200, 12, w=7)


# --------------------------------------------------------------------------- #
# 3. combat
# --------------------------------------------------------------------------- #
def ammo(cv: Cv):
    p = [(100, 112), (100, 196)]
    p += _cubic((100, 196), (100, 218), (156, 218), (156, 196))[1:]
    p += [(156, 112)]
    p += _cubic((156, 112), (156, 84), (142, 56), (128, 30))[1:]
    p += _cubic((128, 30), (114, 56), (100, 84), (100, 112))[1:]
    cv.fill_path(p, grow=3)
    # cartridge rim at the base
    cv.rrect(90, 202, 166, 220, 7)


def weapon(cv: Cv):
    # order matters: silhouette parts overlap into one white shape
    cv.fill_poly([(124, 144), (152, 144), (168, 208), (134, 208)], r=2.5)     # magazine
    cv.fill_poly([(100, 144), (122, 144), (110, 192), (88, 182)], r=2.5)      # grip
    cv.fill_poly([(30, 116), (86, 102), (86, 150), (36, 166)], r=2.5)         # stock
    cv.fill_poly([(84, 100), (158, 100), (158, 148), (84, 148)], r=3)         # receiver
    cv.fill_poly([(150, 106), (198, 106), (198, 134), (150, 134)], r=3)       # handguard
    cv.fill_poly([(152, 112), (232, 112), (232, 128), (152, 128)], r=2.5)     # barrel
    cv.fill_poly([(202, 88), (214, 88), (214, 114), (202, 114)], r=2)         # front sight


def damage(cv: Cv):
    p = [(64, 44), (192, 44), (192, 92)]
    p += _cubic((192, 92), (192, 142), (164, 178), (128, 208))[1:]
    p += _cubic((128, 208), (92, 178), (64, 142), (64, 92))[1:]
    cv.closed(p)
    cv.line([(128, 50), (112, 92), (142, 116), (114, 156), (130, 186)], w=7)
    cv.line([(142, 116), (166, 106)], w=7)


# --------------------------------------------------------------------------- #
# 4. loot / economy
# --------------------------------------------------------------------------- #
def loot(cv: Cv):
    cv.rrect(48, 72, 208, 220, 16)
    cv.line([(48, 128), (208, 128)], w=8)
    cv.rrect(112, 108, 144, 150, 6, w=6)


def key(cv: Cv):
    cv.circle(72, 128, 36, w=10)
    cv.line([(106, 128), (218, 128)], w=8)
    cv.line([(174, 128), (174, 162)], w=8)
    cv.line([(198, 128), (198, 152)], w=8)


def value(cv: Cv):
    cv.rrect(28, 76, 228, 180, 14)
    cv.circle(128, 128, 26)
    cv.disk(56, 128, 5)
    cv.disk(200, 128, 5)


def document(cv: Cv):
    cv.closed([(64, 32), (156, 32), (200, 76), (200, 224), (64, 224)])
    cv.line([(156, 32), (156, 76), (200, 76)], w=8)
    cv.line([(88, 116), (168, 116)], w=DETAIL)
    cv.line([(88, 148), (168, 148)], w=DETAIL)
    cv.line([(88, 180), (144, 180)], w=DETAIL)


def harddrive(cv: Cv):
    cv.rrect(36, 76, 220, 184, 16)
    cv.circle(176, 130, 26)
    cv.line([(64, 112), (120, 112)], w=DETAIL)
    cv.line([(64, 148), (120, 148)], w=DETAIL)


def medkit(cv: Cv):
    cv.rrect(36, 80, 220, 212, 18)
    cv.arc(128, 80, 30, 180, 360)
    cv.line([(128, 106), (128, 184)], w=22)
    cv.line([(96, 146), (160, 146)], w=22)


# --------------------------------------------------------------------------- #
# 5. extraction
# --------------------------------------------------------------------------- #
def extract(cv: Cv):
    frame = _path_round([(216, 84), (216, 48), (40, 48), (40, 216), (216, 216), (216, 172)], 12)
    cv.line(frame)
    cv.line([(96, 128), (222, 128)], w=8)
    cv.fill_poly([(192, 100), (192, 156), (232, 128)], r=3)


def elevator(cv: Cv):
    cv.rrect(56, 40, 200, 232, 12)
    cv.line([(56, 96), (200, 96)], w=6)
    cv.line([(128, 96), (128, 228)], w=6)
    # up / down floor indicator in the header band
    cv.line([(96, 82), (108, 62), (120, 82)], w=8)
    cv.line([(136, 62), (148, 82), (160, 62)], w=8)


def stairs(cv: Cv):
    path = _path_round([(40, 212), (88, 212), (88, 164), (136, 164),
                        (136, 116), (184, 116), (184, 68), (224, 68)], 4)
    cv.line(path)


# --------------------------------------------------------------------------- #
# 6. generic
# --------------------------------------------------------------------------- #
def interact(cv: Cv):
    cv.rrect(44, 44, 212, 212, 20)
    cv.line([(92, 82), (164, 82)], w=12)
    cv.line([(92, 82), (92, 174)], w=12)
    cv.line([(92, 128), (152, 128)], w=12)
    cv.line([(92, 174), (164, 174)], w=12)


def lock(cv: Cv):
    cv.rrect(58, 116, 198, 220, 18)
    cv.arc(128, 102, 38, 180, 360)
    cv.line([(90, 102), (90, 122)], w=8)
    cv.line([(166, 102), (166, 122)], w=8)
    cv.disk(128, 152, 11)
    cv.line([(128, 158), (128, 184)], w=8)


def check(cv: Cv):
    cv.line([(44, 136), (104, 196), (214, 64)], w=20)


def alert(cv: Cv):
    tri = _path_round([(128, 40), (228, 214), (28, 214)], 20, closed=True)
    cv.closed(tri)
    cv.line([(128, 100), (128, 156)], w=14)
    cv.disk(128, 188, 10)


# --------------------------------------------------------------------------- #
# 7. map markers
# --------------------------------------------------------------------------- #
def map_player(cv: Cv):
    cv.fill_poly([(128, 30), (202, 224), (128, 180), (54, 224)], r=5)


def map_extract(cv: Cv):
    cv.circle(128, 128, 94)
    cv.line([(74, 128), (170, 128)], w=8)
    cv.fill_poly([(142, 102), (142, 154), (184, 128)], r=3)


def map_spawn(cv: Cv):
    cv.circle(128, 98, 58)
    cv.line([(87, 139), (128, 226)], w=8)
    cv.line([(169, 139), (128, 226)], w=8)
    cv.disk(128, 98, 16)


def map_unknown(cv: Cv):
    p = _arc_pts(128, 90, 48, 180, 360)
    p += _cubic((176, 90), (176, 126), (128, 134), (128, 166))[1:]
    cv.line(p, w=13)
    cv.disk(128, 198, 12)


def map_stairs_up(cv: Cv):
    path = _path_round([(40, 216), (84, 216), (84, 172), (128, 172),
                        (128, 128), (172, 128), (172, 84)], 5)
    cv.line(path)
    cv.line([(208, 196), (208, 60)], w=8)
    cv.line([(184, 84), (208, 58), (232, 84)], w=8)


def map_stairs_down(cv: Cv):
    path = _path_round([(40, 40), (84, 40), (84, 84), (128, 84),
                        (128, 128), (172, 128), (172, 172)], 5)
    cv.line(path)
    cv.line([(208, 60), (208, 196)], w=8)
    cv.line([(184, 172), (208, 198), (232, 172)], w=8)


# --------------------------------------------------------------------------- #
# 8. compass letters (128 canvas)
# --------------------------------------------------------------------------- #
def compass_n(cv: Cv):
    cv.line([(26, 102), (26, 26), (102, 102), (102, 26)], w=12)


def compass_e(cv: Cv):
    cv.line([(26, 26), (26, 102)], w=12)
    cv.line([(26, 26), (101, 26)], w=12)
    cv.line([(26, 64), (88, 64)], w=12)
    cv.line([(26, 102), (101, 102)], w=12)


def compass_s(cv: Cv):
    p = _cubic((98, 42), (98, 26), (30, 26), (30, 50))
    p += _cubic((30, 50), (30, 72), (98, 64), (98, 88))[1:]
    p += _cubic((98, 88), (98, 104), (38, 104), (30, 90))[1:]
    cv.line(p, w=12)


def compass_w(cv: Cv):
    cv.line([(26, 28), (42, 100), (64, 52), (86, 100), (102, 28)], w=12)


# --------------------------------------------------------------------------- #
# registry
# --------------------------------------------------------------------------- #
@dataclass
class Spec:
    group: str
    name: str
    size: int
    fn: Callable[[Cv], None]
    usage: str
    stroke: float = 8.0


CROSSHAIR = "Crosshair"
ICONS = "Icons"
MAP = "Map"

SPECS = [
    # -- crosshair --------------------------------------------------------- #
    Spec(CROSSHAIR, "I_Cross_Dot.png", 512, cross_dot, "默认 FPS 准星：中心点 + 四短刻线", 14),
    Spec(CROSSHAIR, "I_Cross_Cross.png", 512, cross_cross, "四向开口十字准星（无中心点）", 14),
    Spec(CROSSHAIR, "I_Cross_Circle.png", 512, cross_circle, "圆环 + 中心点准星（霰弹/蓄力）", 12),
    Spec(CROSSHAIR, "I_Cross_Chevron.png", 512, cross_chevron, "四向小三角准星（精确瞄准）", 14),
    Spec(CROSSHAIR, "I_Hitmarker.png", 512, hitmarker, "命中反馈：四斜线 + 中心点，可放大闪一下", 13),
    # -- survival ---------------------------------------------------------- #
    Spec(ICONS, "I_Health.png", 256, health, "生命值（心形）"),
    Spec(ICONS, "I_Armor.png", 256, armor, "护甲值（盾牌）"),
    Spec(ICONS, "I_Stamina.png", 256, stamina, "耐力/体力（闪电）"),
    Spec(ICONS, "I_Weight.png", 256, weight, "负重/背包容量"),
    # -- combat ------------------------------------------------------------ #
    Spec(ICONS, "I_Ammo.png", 256, ammo, "弹药（子弹）"),
    Spec(ICONS, "I_Weapon.png", 256, weapon, "当前武器（步枪剪影）"),
    Spec(ICONS, "I_Damage.png", 256, damage, "护甲破损/受伤（裂盾）"),
    # -- loot -------------------------------------------------------------- #
    Spec(ICONS, "I_Loot.png", 256, loot, "可搜刮容器/物资箱"),
    Spec(ICONS, "I_Key.png", 256, key, "钥匙/门禁卡（钥匙）"),
    Spec(ICONS, "I_Value.png", 256, value, "战利品价值（钞票）"),
    Spec(ICONS, "I_Document.png", 256, document, "情报文件/合同"),
    Spec(ICONS, "I_HardDrive.png", 256, harddrive, "硬盘/数据载体"),
    Spec(ICONS, "I_Medkit.png", 256, medkit, "医疗包/治疗"),
    # -- extract ----------------------------------------------------------- #
    Spec(ICONS, "I_Extract.png", 256, extract, "撤离点（门 + 向外箭头）"),
    Spec(ICONS, "I_Elevator.png", 256, elevator, "电梯"),
    Spec(ICONS, "I_Stairs.png", 256, stairs, "楼梯/楼层通行"),
    # -- generic ----------------------------------------------------------- #
    Spec(ICONS, "I_Interact.png", 256, interact, "交互提示（E 键方格）"),
    Spec(ICONS, "I_Lock.png", 256, lock, "上锁/需要钥匙"),
    Spec(ICONS, "I_Check.png", 256, check, "完成/已确认"),
    Spec(ICONS, "I_Alert.png", 256, alert, "警告/危险"),
    # -- map --------------------------------------------------------------- #
    Spec(MAP, "I_Map_Player.png", 256, map_player, "地图玩家标记，向上 = 0°"),
    Spec(MAP, "I_Map_Extract.png", 256, map_extract, "地图撤离点标记"),
    Spec(MAP, "I_Map_Spawn.png", 256, map_spawn, "地图出生点标记"),
    Spec(MAP, "I_Map_Unknown.png", 256, map_unknown, "地图未知区域/未探索"),
    Spec(MAP, "I_Map_StairsUp.png", 256, map_stairs_up, "地图上行楼梯"),
    Spec(MAP, "I_Map_StairsDown.png", 256, map_stairs_down, "地图下行楼梯"),
    Spec(MAP, "I_Compass_N.png", 128, compass_n, "罗盘北", 12),
    Spec(MAP, "I_Compass_E.png", 128, compass_e, "罗盘东", 12),
    Spec(MAP, "I_Compass_S.png", 128, compass_s, "罗盘南", 12),
    Spec(MAP, "I_Compass_W.png", 128, compass_w, "罗盘西", 12),
]

GROUP_DIR = {CROSSHAIR: "Crosshair", ICONS: "Icons", MAP: "Map"}

GROUP_INFO = [
    (CROSSHAIR, "准星 / 命中反馈", "512×512，细线（14px），适合直接叠在屏幕中心"),
    (ICONS, "HUD 图标", "256×256，主线宽 8px，细节 6–7px；均为白色单色"),
    (MAP, "地图 / 罗盘", "256×256；罗盘字母 128×128"),
]


# --------------------------------------------------------------------------- #
# build
# --------------------------------------------------------------------------- #
def build_icons():
    written = []
    for spec in SPECS:
        subdir = os.path.join(HERE, GROUP_DIR[spec.group])
        os.makedirs(subdir, exist_ok=True)
        cv = Cv(spec.size, spec.stroke)
        spec.fn(cv)
        img = cv.trim()
        path = os.path.join(subdir, spec.name)
        img.save(path)
        written.append((spec, path, img))
    return written


def build_contact_sheet(written):
    cols, cell_w, cell_h, pad = 7, 150, 132, 28
    header_h = 46
    rows = 0
    for group, _, _ in GROUP_INFO:
        n = sum(1 for s, _, _ in written if s.group == group)
        rows += math.ceil(n / cols)
    sheet_w = pad * 2 + cols * cell_w
    sheet_h = 120 + header_h * len(GROUP_INFO) + rows * cell_h + pad

    sheet = Image.new("RGBA", (sheet_w, sheet_h), (15, 18, 22, 255))
    d = ImageDraw.Draw(sheet)
    f_title = _font(_FONT_CJK_BOLD, 24)
    f_head = _font(_FONT_CJK_BOLD, 15)
    f_lbl = _font(_FONT_MONO, 11)
    f_small = _font(_FONT_CJK, 11)

    d.text((pad, 26), "ESCAPE FROM WORK — HUD / UI ICON SET", font=f_title, fill=(238, 242, 245, 255))
    d.text((pad, 62), f"{len(written)} icons · white · transparent · 4× supersampled · 256px grid",
           font=f_small, fill=(140, 150, 160, 255))

    y = 100
    for group, title, subtitle in GROUP_INFO:
        d.text((pad, y), title, font=f_head, fill=(228, 234, 240, 255))
        tw = d.textbbox((0, 0), title, font=f_head)[2]
        d.text((pad + tw + 14, y + 3), subtitle, font=f_small, fill=(120, 130, 140, 255))
        d.line([(pad, y + 30), (sheet_w - pad, y + 30)], fill=(48, 56, 64, 255), width=1)
        y += header_h

        items = [(s, p, im) for s, p, im in written if s.group == group]
        for idx, (spec, _, im) in enumerate(items):
            cx = pad + (idx % cols) * cell_w
            cy = y + (idx // cols) * cell_h
            d.rounded_rectangle([cx + 4, cy + 2, cx + cell_w - 4, cy + cell_h - 4],
                                radius=10, fill=(26, 31, 37, 255), outline=(40, 47, 54, 255))
            icon_px = 100 if spec.size >= 256 else 88
            icon = im.resize((icon_px, icon_px), Image.LANCZOS)
            ix = cx + (cell_w - icon_px) // 2
            iy = cy + 8 + (100 - icon_px) // 2
            sheet.paste(icon, (ix, iy), icon)
            label = spec.name
            bb = d.textbbox((0, 0), label, font=f_lbl)
            lw = bb[2] - bb[0]
            lx = cx + (cell_w - lw) // 2
            d.text((lx, cy + 108), label, font=f_lbl, fill=(185, 194, 203, 255))
        y += math.ceil(len(items) / cols) * cell_h

    out = os.path.join(HERE, "_contact_sheet.png")
    sheet.convert("RGB").save(out)
    return out, sheet.size


README = """# ESCAPE FROM WORK — HUD / UI 图标集

白色单色、透明背景、可被代码染色的矢量风格图标。全部由 `generate_ui_icons.py`
（纯 Pillow 程序化绘制，4× 超采样后 LANCZOS 缩小）生成，可重复执行，幂等。

- 生成：`python generate_ui_icons.py`
- 预览：`_contact_sheet.png`
- 画布：准星 512×512，HUD/地图 256×256，罗盘字母 128×128；内容四周留 ≥16px 安全边
- 线条：@256 主线 8px、内细节 6–7px、圆角端点/圆角连接；准星 512 画布用 14px 细线
- 颜色：纯白 `#FFFFFF`，Alpha 通道即图形；Unity 中用 `Image.color` / 顶点色染色
- 无文字、无阴影、无描边特效；所有图标统一中心 pivot
- 注：`.meta` 由 Unity 首次导入时生成，本目录未提交 `.meta`

## 建议 Unity 导入设置（全部图标一致）

| 设置项 | 值 |
| --- | --- |
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single |
| Pixels Per Unit | 100 |
| Mesh Type | Full Rect |
| Pivot | Center |
| Alpha Is Transparency | ✅ On |
| sRGB (Color Texture) | ✅ On |
| Wrap Mode | Clamp |
| Filter Mode | Bilinear |
| Generate Mip Maps | ❌ Off（HUD 不随距离缩放） |
| Compression | None（或 High Quality，避免细线糊边） |
| Max Size | ≥ 512（准星）/ 256（其余） |

## 文件清单

"""


def build_readme(written):
    lines = [README]
    for group, title, subtitle in GROUP_INFO:
        items = [s for s, _, _ in written if s.group == group]
        lines.append(f"### {title}（{GROUP_DIR[group]}/，{len(items)} 个）\n")
        lines.append(f"{subtitle}\n")
        lines.append("| 文件 | 尺寸 | 用途 |")
        lines.append("| --- | --- | --- |")
        for s in items:
            lines.append(f"| `{GROUP_DIR[group]}/{s.name}` | {s.size}×{s.size} | {s.usage} |")
        lines.append("")
    lines.append("## 建议显示尺寸（UI 里的 Image / SpriteRenderer）\n")
    lines.append("| 用途 | 贴图 | 建议显示尺寸 | 说明 |")
    lines.append("| --- | --- | --- | --- |")
    lines.append("| HUD 图标（生命/护甲/弹药等） | 256 | 32–48px | 24px 仍可辨认，低于 24px 细线会发灰 |")
    lines.append("| 背包/交互大图标 | 256 | 64–96px | 线宽随尺寸线性放大，不会糊 |")
    lines.append("| 准星 | 512 | 128–256px | 图形本体约 220px，线宽约 3.5–7px；命中反馈建议 96→160 缩放闪 |")
    lines.append("| 地图标记 | 256 | 24–40px | 玩家箭头向上 = 0°，代码旋转即可 |")
    lines.append("| 罗盘字母 | 128 | 24–40px | 小尺寸下 12px 笔画仍清晰 |")
    lines.append("")
    lines.append("## 已知说明\n")
    lines.append("- `I_Hunger.png`（饥饿）未生成：256px 下难以与其它线条图标拉开辨识度，"
                 "按任务要求跳过；后续如需可用餐具/苹果图形补做。")
    lines.append("- 罗盘字母（N/E/S/W）是图形化笔画，不是字体渲染；笔画 12px 比其它图标略粗，"
                 "用于保证小尺寸可读。")
    lines.append("- 所有图形均在 16px 安全边内；准星刻意留出大面积透明，方便叠层与动画缩放。")
    lines.append("- 准星图形本体 220×220（512 画布），所以贴图尺寸大但内容不占满；"
                 "按上表 128–256px 显示即可，线宽随尺寸线性变化。")
    lines.append("")
    out = os.path.join(HERE, "README.md")
    with open(out, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    return out


def verify(written):
    print(f"{'file':<28} {'size':>9}  {'bbox':<22} {'margins L,T,R,B':<20} {'cover':>6}")
    bad = []
    for spec, path, img in written:
        arr = np.array(img)
        alpha = arr[..., 3]
        ys, xs = np.nonzero(alpha)
        w, h = img.size
        if len(xs) == 0:
            bad.append(f"{spec.name}: EMPTY")
            continue
        bbox = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)
        l, t, r, b = bbox
        margins = (l, t, w - r, h - b)
        cover = float((alpha > 0).mean())
        flag = "  <-- CLIPPED" if min(margins) < 2 else ""
        if flag:
            bad.append(spec.name + " clipped")
        visible = alpha > 0
        bad_rgb = int((visible & ((arr[..., 0] != 255) | (arr[..., 1] != 255) | (arr[..., 2] != 255))).sum())
        if bad_rgb:
            bad.append(f"{spec.name}: {bad_rgb} non-white visible pixels")
        print(f"{spec.group + '/' + spec.name:<28} {w}x{h:>4}  {str(bbox):<22} "
              f"{str(margins):<20} {cover * 100:5.2f}%{flag}")
    print()
    print("OK" if not bad else "ISSUES: " + "; ".join(bad))
    return bad


def main():
    written = build_icons()
    sheet, sheet_size = build_contact_sheet(written)
    readme = build_readme(written)
    print(f"generated {len(written)} icons")
    print(f"contact sheet: {sheet}  {sheet_size[0]}x{sheet_size[1]}")
    print(f"readme: {readme}")
    print()
    issues = verify(written)
    return 0 if not issues else 1


if __name__ == "__main__":
    raise SystemExit(main())
