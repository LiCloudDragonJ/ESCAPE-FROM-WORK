"""
楼层平面原型（纯 2D，不碰 Unity）
====================================
目的：验证"形状来自少数强结构动作传播"这条路线能不能产出好看的平面——
每层先放 2-4 个非正交动作（斜向主脊 / 旋转大厅 / 环形核心筒 / 中庭），
再用 BSP 填充剩余自由空间；紧邻这些动作的房间被边界夹住，自然长成梯形、
扇形、五边形；远处保持矩形。

输出：单层大图 + 六层对照图 + 与"现状（纯矩形 BSP）"的对比图。

用法：
    python plan_proto.py --out <目录> [--seed 7]
"""
import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

CELL = 0.5  # 栅格（米）

FONT_REG = r"C:\Windows\Fonts\msyh.ttc"
FONT_BOLD = r"C:\Windows\Fonts\msyhbd.ttc"

# 配色（平面图语汇）
PAPER = (243, 245, 249)
OUTSIDE = (228, 231, 237)
INK = (46, 52, 64)
INK_SOFT = (120, 128, 144)
FILL_RECT = (255, 255, 255)
FILL_TRAP = (223, 238, 224)     # 梯形 / 三角形：被斜边夹出来的
FILL_FAN = (247, 226, 184)      # 扇形 / 旋转矩形：地标体量
FILL_POLY = (233, 226, 244)     # 五边形以上 / 异形
FILL_CORR = (216, 222, 232)
FILL_SPINE = (203, 211, 225)
FILL_VOID = (58, 63, 72)
FILL_SOLID = (201, 207, 217)
ACCENT = (196, 120, 62)


def font(size, bold=False):
    path = FONT_BOLD if bold else FONT_REG
    try:
        return ImageFont.truetype(path, size)
    except Exception:
        return ImageFont.load_default()


# ---------------------------------------------------------------- 基础几何

def poly_area(poly):
    a = 0.0
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        a += x1 * y2 - x2 * y1
    return a * 0.5


def poly_mask(poly, w_m, h_m, cell=CELL):
    cols = int(round(w_m / cell))
    rows = int(round(h_m / cell))
    xs = (np.arange(cols) + 0.5) * cell
    ys = (np.arange(rows) + 0.5) * cell
    X, Y = np.meshgrid(xs, ys)
    inside = np.zeros((rows, cols), bool)
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        if y1 == y2:
            continue
        cond = (y1 > Y) != (y2 > Y)
        xint = (x2 - x1) * (Y - y1) / (y2 - y1) + x1
        inside ^= cond & (X < xint)
    return inside


def regular_poly(cx, cy, r, n, phase=0.0):
    return [(cx + r * math.cos(phase + 2 * math.pi * k / n),
             cy + r * math.sin(phase + 2 * math.pi * k / n)) for k in range(n)]


def rot_rect(cx, cy, w, h, ang):
    ca, sa = math.cos(ang), math.sin(ang)
    pts = [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]
    return [(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in pts]


def band_poly(p0, p1, half):
    dx, dy = p1[0] - p0[0], p1[1] - p0[1]
    L = math.hypot(dx, dy) or 1.0
    nx, ny = -dy / L * half, dx / L * half
    return [(p0[0] + nx, p0[1] + ny), (p1[0] + nx, p1[1] + ny),
            (p1[0] - nx, p1[1] - ny), (p0[0] - nx, p0[1] - ny)]


def seg_dist(p, a, b):
    ax, ay = a
    bx, by = b
    px, py = p
    dx, dy = bx - ax, by - ay
    L2 = dx * dx + dy * dy
    t = 0.0 if L2 == 0 else max(0.0, min(1.0, ((px - ax) * dx + (py - ay) * dy) / L2))
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy))


# ---------------------------------------------------------------- 栅格轮廓

def trace_polys(mask, ox, oy):
    """把格点掩码描成多边形环（外环 CCW，单位：格）"""
    h, w = mask.shape
    edges = {}
    idx = np.argwhere(mask)
    for i, j in idx:
        x, y = ox + j, oy + i
        if i == 0 or not mask[i - 1, j]:
            edges[(x, y)] = (x + 1, y)
        if j == w - 1 or not mask[i, j + 1]:
            edges[(x + 1, y)] = (x + 1, y + 1)
        if i == h - 1 or not mask[i + 1, j]:
            edges[(x + 1, y + 1)] = (x, y + 1)
        if j == 0 or not mask[i, j - 1]:
            edges[(x, y + 1)] = (x, y)
    loops = []
    while edges:
        start = next(iter(edges))
        cur = start
        loop = [start]
        while True:
            nxt = edges.pop(cur, None)
            if nxt is None or nxt == start:
                break
            loop.append(nxt)
            cur = nxt
        if len(loop) >= 4:
            loops.append(loop)
    return loops


def dp_open(pts, eps):
    if len(pts) < 3:
        return list(pts)
    ax, ay = pts[0]
    bx, by = pts[-1]
    best, bi = -1.0, 0
    for i in range(1, len(pts) - 1):
        d = seg_dist(pts[i], (ax, ay), (bx, by))
        if d > best:
            best, bi = d, i
    if best <= eps:
        return [pts[0], pts[-1]]
    left = dp_open(pts[:bi + 1], eps)
    right = dp_open(pts[bi:], eps)
    return left[:-1] + right


def dp_closed(pts, eps):
    if len(pts) < 5:
        return list(pts)
    p0 = pts[0]
    far, fi = -1.0, 0
    for i, p in enumerate(pts):
        d = (p[0] - p0[0]) ** 2 + (p[1] - p0[1]) ** 2
        if d > far:
            far, fi = d, i
    a = dp_open(pts[:fi + 1], eps)
    b = dp_open(pts[fi:] + [pts[0]], eps)
    return a[:-1] + b[:-1]


def components(mask):
    h, w = mask.shape
    out = []
    seen = np.zeros((h, w), bool)
    for i, j in np.argwhere(mask):
        if seen[i, j]:
            continue
        stack = [(i, j)]
        seen[i, j] = True
        cells = []
        while stack:
            y, x = stack.pop()
            cells.append((y, x))
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ny, nx = y + dy, x + dx
                if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not seen[ny, nx]:
                    seen[ny, nx] = True
                    stack.append((ny, nx))
        sub = np.zeros((h, w), bool)
        for y, x in cells:
            sub[y, x] = True
        out.append(sub)
    return out


def crop_to(mask):
    ys, xs = np.where(mask)
    y0, y1 = ys.min(), ys.max() + 1
    x0, x1 = xs.min(), xs.max() + 1
    return mask[y0:y1, x0:x1], int(x0), int(y0)


# ---------------------------------------------------------------- 外廓

def build_envelope(W, H, rng):
    """矩形板 → 2-4 个 45° 截角 + 0-2 个边缺口，返回 CCW 多边形"""
    poly = [(0.0, 0.0), (W, 0.0), (W, H), (0.0, H)]
    corners = rng.sample([0, 1, 2, 3], rng.choice([2, 3, 4]))
    # 逐个替换角点：角点 = 两端沿边内缩 s
    for k in sorted(corners, reverse=True):
        s = rng.uniform(11.0, 21.0)
        p_prev = poly[(k - 1) % 4]
        p_cur = poly[k]
        p_next = poly[(k + 1) % 4]
        a = unit_toward(p_cur, p_prev, s)
        b = unit_toward(p_cur, p_next, s)
        poly[k:k + 1] = [a, b]
    # 缺口
    for _ in range(rng.choice([0, 1, 1, 2])):
        poly = add_notch(poly, W, H, rng)
    return poly


def unit_toward(p, q, s):
    dx, dy = q[0] - p[0], q[1] - p[1]
    L = math.hypot(dx, dy) or 1.0
    return (p[0] + dx / L * s, p[1] + dy / L * s)


def add_notch(poly, W, H, rng):
    n = len(poly)
    cand = []
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        if L > 34:
            cand.append((i, L))
    if not cand:
        return poly
    i, L = rng.choice(cand)
    a, b = poly[i], poly[(i + 1) % n]
    dx, dy = (b[0] - a[0]) / L, (b[1] - a[1]) / L
    inward = (-dy, dx) if poly_area(poly) > 0 else (dy, -dx)
    wn = min(rng.uniform(12, 26), L * 0.45)
    dn = rng.uniform(6, 15)
    u1 = rng.uniform(0.25, 0.75 - wn / L)
    u2 = u1 + wn / L
    p1 = (a[0] + dx * L * u1, a[1] + dy * L * u1)
    p2 = (a[0] + dx * L * u1 + inward[0] * dn, a[1] + dy * L * u1 + inward[1] * dn)
    p3 = (a[0] + dx * L * u2 + inward[0] * dn, a[1] + dy * L * u2 + inward[1] * dn)
    p4 = (a[0] + dx * L * u2, a[1] + dy * L * u2)
    return poly[:i + 1] + [p1, p2, p3, p4] + poly[i + 1:]


def ensure_ccw(poly):
    return poly if poly_area(poly) > 0 else list(reversed(poly))


# ---------------------------------------------------------------- 生成参数

MIN_ROOM = 55.0        # 小于这个面积当碎块处理
MAX_ROOM = 780.0       # 大于这个面积继续切
MAX_DEPTH = 9
BAND_W = 4.6           # BSP 主走廊宽
SPINE_HALF = 2.8


def band_rect(axis, cut, band, ox, oy, hh, ww, spine):
    """分裂带对应的绘制多边形（世界坐标）"""
    x0, y0 = ox * CELL, oy * CELL
    x1, y1 = (ox + ww) * CELL, (oy + hh) * CELL
    if axis == 'x':
        return [(cut, y0), (cut + band, y0), (cut + band, y1), (cut, y1)]
    if axis == 'y':
        return [(x0, cut), (x1, cut), (x1, cut + band), (x0, cut + band)]
    p0, p1, half, d, n, _, _ = spine
    corners = [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
    ts = [(c[0] - p0[0]) * d[0] + (c[1] - p0[1]) * d[1] for c in corners]
    if axis == 'perp':
        lo, hi = min(ts), max(ts)
        return [add(p0, d, lo, n, cut), add(p0, d, hi, n, cut),
                add(p0, d, hi, n, cut + band), add(p0, d, lo, n, cut + band)]
    ss = [(c[0] - p0[0]) * n[0] + (c[1] - p0[1]) * n[1] for c in corners]
    lo, hi = min(ss), max(ss)
    return [add(p0, d, lo, n, cut), add(p0, d, hi, n, cut),
            add(p0, d, hi, n, cut + band), add(p0, d, lo, n, cut + band)]


def add(p, d, t, n, s):
    return (p[0] + d[0] * t + n[0] * s, p[1] + d[1] * t + n[1] * s)


def corridor_at(ctx, ci, cj):
    c = ctx['corridor']
    return 0 <= cj < c.shape[0] and 0 <= ci < c.shape[1] and bool(c[cj, ci])


def block_bbox(mask, ox, oy):
    ys, xs = np.where(mask)
    return (int(ox + xs.min()), int(oy + ys.min()),
            int(ox + xs.max() + 1), int(oy + ys.max() + 1))


def try_local_corridor(mask, ox, oy, ctx):
    """块内局部走廊：沿长轴插一条；两端都必须接到已有走廊，否则不插（不留死胡同）"""
    hh, ww = mask.shape
    w_m, h_m = ww * CELL, hh * CELL
    x0, y0, x1, y1 = block_bbox(mask, ox, oy)
    rng = ctx['rng']
    band = ctx.get('local_band', 2.8)
    ys = (oy + np.arange(hh) + 0.5) * CELL
    xs = (ox + np.arange(ww) + 0.5) * CELL
    if w_m >= h_m:
        cut = y0 * CELL + h_m * rng.uniform(0.36, 0.64)
        cc = int(cut // CELL)
        if not (corridor_at(ctx, x0 - 1, cc) and corridor_at(ctx, x1, cc)):
            return None
        sel = (ys >= cut) & (ys < cut + band)
        bm = mask & sel[:, None]
        a = mask & ~bm & (ys < cut)[:, None]
        b = mask & ~bm & (ys >= cut + band)[:, None]
        poly = [(x0 * CELL, cut), (x1 * CELL, cut),
                (x1 * CELL, cut + band), (x0 * CELL, cut + band)]
    else:
        cut = x0 * CELL + w_m * rng.uniform(0.36, 0.64)
        cc = int(cut // CELL)
        if not (corridor_at(ctx, cc, y0 - 1) and corridor_at(ctx, cc, y1)):
            return None
        sel = (xs >= cut) & (xs < cut + band)
        bm = mask & sel[None, :]
        a = mask & ~bm & (xs < cut)[None, :]
        b = mask & ~bm & (xs >= cut + band)[None, :]
        poly = [(cut, y0 * CELL), (cut, y1 * CELL),
                (cut + band, y1 * CELL), (cut + band, y0 * CELL)]
    if float(bm.sum()) * CELL * CELL < 10.0:
        return None
    ctx['corridor'][oy:oy + hh, ox:ox + ww] |= bm
    ctx['corridor_polys'].append(('local', poly, None))
    return a, b


def pick_grammar(grammars, used, rng):
    """按权重抽空间语法；已经用过的降权，保证一块楼板里语法不重复堆叠"""
    choices = []
    for g in grammars:
        w = float(g.get('weight', 1.0)) / (1.0 + 2.0 * used.get(g['name'], 0))
        if w > 0.0:
            choices.append((g, w))
    if not choices:
        choices = [(g, 1.0) for g in grammars]
    tot = sum(w for _, w in choices)
    r = rng.random() * tot
    acc = 0.0
    for g, w in choices:
        acc += w
        if r <= acc:
            return g
    return choices[-1][0]


def partition(mask, ox, oy, depth, ctx, out, leftover):
    """二分切分；切分轴可以是世界轴，也可以是旋转后的柱网轴（形状仍由边界给）"""
    area = float(mask.sum()) * CELL * CELL
    if area <= 0.0:
        return
    if area < MIN_ROOM * 0.55:
        leftover.append((mask, ox, oy))
        return
    hh, ww = mask.shape
    rng = ctx['rng']
    ax = ctx.get('ax', (1.0, 0.0))
    ay = ctx.get('ay', (0.0, 1.0))
    rotated = abs(ax[0] - 1.0) > 1e-9
    if ((not rotated) and (not ctx.get('no_local')) and depth < 2
            and area > 820.0 and min(ww, hh) * CELL > 23.0):
        halves = try_local_corridor(mask, ox, oy, ctx)
        if halves is not None:
            for half in halves:
                for part in components(half):
                    sub, sx, sy = crop_to(part)
                    partition(sub, ox + sx, oy + sy, depth + 1, ctx, out, leftover)
            return
    if depth >= MAX_DEPTH:
        out.append((mask, ox, oy))
        return
    gx = (ox + np.arange(ww) + 0.5) * CELL
    gy = (oy + np.arange(hh) + 0.5) * CELL
    GX, GY = np.meshgrid(gx, gy)
    px = GX * ax[0] + GY * ax[1]
    py = GX * ay[0] + GY * ay[1]
    lo_x, hi_x = float(px[mask].min()), float(px[mask].max())
    lo_y, hi_y = float(py[mask].min()), float(py[mask].max())
    ext_x, ext_y = hi_x - lo_x, hi_y - lo_y
    aspect = max(ext_x, ext_y) / max(1e-3, min(ext_x, ext_y))
    if area <= ctx['max_room'] and aspect <= ctx['aspect_max']:
        out.append((mask, ox, oy))
        return
    if area <= ctx['min_accept'] and aspect <= ctx['aspect_max'] * 1.6:
        out.append((mask, ox, oy))
        return

    target = None
    if ctx.get('strip_axis') and min(ext_x, ext_y) > ctx.get('strip_w', 11.0) * 1.5:
        axis = 'y' if ctx['strip_axis'] == 'x' else 'x'
        target = ctx.get('strip_w', 11.0)
    elif ext_x > ext_y * 1.2:
        axis = 'x'
    elif ext_y > ext_x * 1.2:
        axis = 'y'
    else:
        axis = 'x' if rng.random() < 0.5 else 'y'

    val = px if axis == 'x' else py
    lo = lo_x if axis == 'x' else lo_y
    hi = hi_x if axis == 'x' else hi_y
    if hi - lo < 6.0:
        out.append((mask, ox, oy))
        return
    if target is not None:
        cut = lo + target
        if cut > hi - 4.0:
            cut = lo + (hi - lo) * 0.5
    else:
        cut = lo + (hi - lo) * rng.uniform(0.4, 0.6)
    a = mask & (val < cut)
    b = mask & (val >= cut)
    if (float(a.sum()) * CELL * CELL < MIN_ROOM * 0.6
            or float(b.sum()) * CELL * CELL < MIN_ROOM * 0.6):
        cut = lo + (hi - lo) * 0.5
        a = mask & (val < cut)
        b = mask & (val >= cut)
        if (float(a.sum()) * CELL * CELL < MIN_ROOM * 0.5
                or float(b.sum()) * CELL * CELL < MIN_ROOM * 0.5):
            out.append((mask, ox, oy))
            return
    for part in components(a):
        sub, sx, sy = crop_to(part)
        partition(sub, ox + sx, oy + sy, depth + 1, ctx, out, leftover)
    for part in components(b):
        sub, sx, sy = crop_to(part)
        partition(sub, ox + sx, oy + sy, depth + 1, ctx, out, leftover)


def mask_center(mask, ox, oy):
    ys, xs = np.where(mask)
    return ((ox + xs.mean() + 0.5) * CELL, (oy + ys.mean() + 0.5) * CELL)


def room_doors(poly, mask, walk_other, cell=CELL):
    """沿房间边采样，朝外一侧是"别人"就记一道门。返回 [(edge, t_mid, w)]"""
    out = []
    n = len(poly)
    cx = sum(p[0] for p in poly) / n
    cy = sum(p[1] for p in poly) / n
    for ei in range(n):
        ax, ay = poly[ei]
        bx, by = poly[(ei + 1) % n]
        ex, ey = bx - ax, by - ay
        L = math.hypot(ex, ey)
        if L < 1.4:
            continue
        ux, uy = ex / L, ey / L
        nx, ny = -uy, ux
        # 让法线朝外
        mx, my = (ax + bx) * 0.5, (ay + by) * 0.5
        if (mx - cx) * nx + (my - cy) * ny < 0:
            nx, ny = -nx, -ny
        step = 0.6
        run_start = None
        s = 0.3
        while s < L - 0.3:
            px, py = ax + ux * s, ay + uy * s
            qx, qy = px + nx * 1.1, py + ny * 1.1
            cj, ci = int(qy // cell), int(qx // cell)
            ok = (0 <= cj < walk_other.shape[0] and 0 <= ci < walk_other.shape[1]
                  and walk_other[cj, ci] and not mask[cj, ci])
            if ok and run_start is None:
                run_start = s
            if (not ok) and run_start is not None:
                if s - run_start >= 1.6:
                    out.append((ei, (run_start + s) * 0.5, min(2.4, s - run_start)))
                run_start = None
            s += step
        if run_start is not None and L - 0.3 - run_start >= 1.6:
            out.append((ei, (run_start + L - 0.3) * 0.5, min(2.4, L - 0.3 - run_start)))
    # 每间房最多留 2 道门，且彼此拉开
    out.sort(key=lambda t: -t[2])
    keep = []
    for d in out:
        if all(abs(d[1] - k[1]) > 3.5 or d[0] != k[0] for k in keep):
            keep.append(d)
        if len(keep) >= 2:
            break
    return keep


def classify(poly, landmark=None):
    if landmark:
        return landmark
    n = len(poly)
    axis_aligned = True
    for i in range(n):
        dx = poly[(i + 1) % n][0] - poly[i][0]
        dy = poly[(i + 1) % n][1] - poly[i][1]
        if abs(dx) > 0.3 and abs(dy) > 0.3:
            axis_aligned = False
            break
    if n == 4 and axis_aligned:
        return 'rect'
    if n == 4:
        return 'trap'
    return 'poly'


def make_floor(cfg):
    W, H = cfg['W'], cfg['H']
    rng = random.Random(cfg['seed'])
    fl = {'W': W, 'H': H, 'cfg': cfg, 'rooms': [], 'corridors': [], 'voids': [],
          'solids': [], 'cores': [], 'zones': [], 'spine': None, 'name': cfg.get('name', ''), 'sub': cfg.get('sub', '')}
    fl['envelope'] = ensure_ccw(build_envelope(W, H, rng)) if cfg.get('envelope', True) \
        else [(0.0, 0.0), (float(W), 0.0), (float(W), float(H)), (0.0, float(H))]
    inside = poly_mask(fl['envelope'], W, H)
    corridor = np.zeros_like(inside)
    assigned = np.zeros_like(inside)
    void = np.zeros_like(inside)
    near = np.zeros_like(inside)

    cx = W * 0.5 + rng.uniform(-10, 10)
    cy = H * 0.5 + rng.uniform(-8, 8)

    gdeg = math.radians(cfg.get('grid_deg', 0.0))
    gax = (math.cos(gdeg), math.sin(gdeg))
    gay = (-gax[1], gax[0])

    def add_corridor(poly, kind, label=None):
        nonlocal corridor
        m = poly_mask(poly, W, H) & inside
        corridor = corridor | m
        fl['corridors'].append((kind, poly, label))
        return m

    # ---- 1) 斜向主脊
    spine_mask = np.zeros_like(inside)
    if 'spine' in cfg['gestures']:
        ang = math.radians(cfg.get('spine_deg', 24.0))
        d = (math.cos(ang), math.sin(ang))
        nrm = (-d[1], d[0])
        L = 0.62 * math.hypot(W, H)
        off = rng.uniform(-8, 8)
        p0 = (cx - d[0] * L * 0.5 + nrm[0] * off, cy - d[1] * L * 0.5 + nrm[1] * off)
        p1 = (p0[0] + d[0] * L, p0[1] + d[1] * L)
        spine_mask = add_corridor(band_poly(p0, p1, SPINE_HALF), 'spine',
                                  cfg.get('spine_label', '主脊'))
        fl['spine'] = (p0, p1, SPINE_HALF, d, nrm, cx, cy)

    SL = 0.0
    if fl['spine'] is not None:
        SL = math.hypot(fl['spine'][1][0] - fl['spine'][0][0],
                        fl['spine'][1][1] - fl['spine'][0][1])

    def spine_base(t, side, dist):
        p0, p1, half, d, nrm, _, _ = fl['spine']
        bx = p0[0] + d[0] * (t * SL)
        by = p0[1] + d[1] * (t * SL)
        return (bx + nrm[0] * side * dist, by + nrm[1] * side * dist)

    def nearest_spine(p):
        p0, p1, half, d, nrm, _, _ = fl['spine']
        t = (p[0] - p0[0]) * d[0] + (p[1] - p0[1]) * d[1]
        t = max(0.0, min(SL, t))
        return (p0[0] + d[0] * t, p0[1] + d[1] * t)

    def link_to_spine(c, reach):
        """地标环廊外缘 → 主脊，拉一条 4.6m 支廊，保证环廊不是孤岛"""
        sp = nearest_spine(c)
        vx, vy = sp[0] - c[0], sp[1] - c[1]
        vlen = math.hypot(vx, vy) or 1.0
        ux, uy = vx / vlen, vy / vlen
        start = (c[0] + ux * reach, c[1] + uy * reach)
        end = (sp[0] + ux * (SPINE_HALF + 0.6), sp[1] + uy * (SPINE_HALF + 0.6))
        add_corridor(band_poly(start, end, 2.3), 'link', None)

    # ---- 2) 旋转大厅：自带环廊（ring_w 默认 5m，3.4m 太窄看不出是走廊），贴在主脊一侧
    if 'hall' in cfg['gestures'] and fl['spine'] is not None:
        bw, bh = cfg.get('hall_size', (46.0, 30.0))
        ang = math.radians(cfg.get('hall_deg', 45.0))
        pad = cfg.get('ring_w', 5.0)
        reach = max(bw, bh) * 0.5 + pad
        best = None
        for _ in range(90):
            t = rng.uniform(0.3, 0.74)
            side = rng.choice([-1, 1])
            c = spine_base(t, side, reach + SPINE_HALF + rng.uniform(0.8, 3.0))
            inner = rot_rect(c[0], c[1], bw, bh, ang)
            outer = rot_rect(c[0], c[1], bw + 2 * pad, bh + 2 * pad, ang)
            m_in = poly_mask(inner, W, H)
            m_out = poly_mask(outer, W, H)
            if (m_out & ~inside).sum() > m_out.sum() * 0.02:
                continue
            if (m_out & (assigned | void)).any():
                continue
            if (m_in & spine_mask).any():
                continue
            best = (c, inner, outer, m_in, m_out)
            break
        if best:
            c, inner, outer, m_in, m_out = best
            add_corridor(outer, 'ring', None)
            assigned = assigned | m_in
            fl['rooms'].append({'poly': inner, 'mask': m_in, 'kind': 'hall', 'landmark': True,
                                'label': cfg.get('hall_label', '旋转大厅'),
                                'center': c, 'area': abs(poly_area(inner))})
            link_to_spine(c, reach)

    # ---- 3) 环形核心筒：八边形环廊 + 八条扇形
    if 'hub' in cfg['gestures'] and fl['spine'] is not None:
        r_out = cfg.get('hub_r', 21.0)
        r_core = 6.4
        r_ring = r_core + cfg.get('ring_w', 5.0)
        best = None
        for _ in range(90):
            t = rng.uniform(0.58, 0.86)
            side = rng.choice([-1, 1])
            c = spine_base(t, side, r_out + SPINE_HALF + rng.uniform(1.5, 4.0))
            outer = regular_poly(c[0], c[1], r_out, 8, math.pi / 8)
            m_out = poly_mask(outer, W, H)
            if (m_out & ~inside).sum() > m_out.sum() * 0.02:
                continue
            if (m_out & (assigned | void | spine_mask)).any():
                continue
            best = (c, m_out)
            break
        if best:
            c, m_out = best
            oct_core = regular_poly(c[0], c[1], r_core, 8, math.pi / 8)
            oct_ring = regular_poly(c[0], c[1], r_ring, 8, math.pi / 8)
            m_core = poly_mask(oct_core, W, H)
            add_corridor(oct_ring, 'ring', None)
            corridor = corridor & ~m_core
            for k in range(8):
                a0 = math.pi / 8 + 2 * math.pi * k / 8
                a1 = math.pi / 8 + 2 * math.pi * (k + 1) / 8
                quad = [(c[0] + r_ring * math.cos(a0), c[1] + r_ring * math.sin(a0)),
                        (c[0] + r_out * math.cos(a0), c[1] + r_out * math.sin(a0)),
                        (c[0] + r_out * math.cos(a1), c[1] + r_out * math.sin(a1)),
                        (c[0] + r_ring * math.cos(a1), c[1] + r_ring * math.sin(a1))]
                qm = poly_mask(quad, W, H) & ~poly_mask(oct_ring, W, H)
                assigned = assigned | qm
                cc = (sum(q[0] for q in quad) / 4.0, sum(q[1] for q in quad) / 4.0)
                fl['rooms'].append({'poly': quad, 'mask': qm, 'kind': 'fan', 'landmark': False,
                                    'label': None, 'center': cc, 'area': abs(poly_area(quad))})
            assigned = assigned | m_core
            fl['rooms'].append({'poly': oct_core, 'mask': m_core, 'kind': 'hall', 'landmark': True,
                                'label': cfg.get('hub_label', '环形大厅'),
                                'center': c, 'area': abs(poly_area(oct_core))})
            link_to_spine(c, r_ring)

    # ---- 4) 中庭 / 挑空：八角挑空 + 环廊（ring_w）
    if 'atrium' in cfg['gestures'] and fl['spine'] is not None:
        r = cfg.get('atrium_r', 16.0)
        arw = cfg.get('ring_w', 5.0)
        best = None
        for _ in range(90):
            t = rng.uniform(0.28, 0.7)
            side = rng.choice([-1, 1])
            c = spine_base(t, side, r + arw + SPINE_HALF + rng.uniform(1.0, 4.0))
            inner = regular_poly(c[0], c[1], r, 8, math.pi / 8)
            outer = regular_poly(c[0], c[1], r + arw, 8, math.pi / 8)
            m_in = poly_mask(inner, W, H)
            m_out = poly_mask(outer, W, H)
            if (m_out & ~inside).sum() > m_out.sum() * 0.02:
                continue
            if (m_out & (assigned | spine_mask)).any():
                continue
            best = (c, inner, outer, m_in)
            break
        if best:
            c, inner, outer, m_in = best
            add_corridor(outer, 'ring', None)
            corridor = corridor & ~m_in
            void = void | m_in
            fl['voids'].append((inner, cfg.get('atrium_label', '中庭（挑空）'), c))
            link_to_spine(c, r + arw)

    # ---- 4b) 主廊：沿柱网方向贯穿全层（柱网可为世界轴，也可整体倾斜）
    def corridor_fits(poly):
        m = poly_mask(poly, W, H)
        tot = float(m.sum()) or 1.0
        return float((m & (assigned | void)).sum()) <= tot * 0.06

    half = BAND_W * 0.5
    span = 0.78 * math.hypot(W, H)
    ocx, ocy = W * 0.5, H * 0.5
    for f in cfg.get('vcuts', []):
        for delta in (0.0, 0.025, -0.025, 0.05, -0.05, 0.08, -0.08, 0.11, -0.11):
            off = (f + delta - 0.5) * W
            if abs(off) > W * 0.5 - 14.0:
                continue
            c0 = (ocx + gax[0] * off, ocy + gax[1] * off)
            p0 = (c0[0] - gay[0] * span, c0[1] - gay[1] * span)
            p1 = (c0[0] + gay[0] * span, c0[1] + gay[1] * span)
            poly = band_poly(p0, p1, half)
            if corridor_fits(poly):
                add_corridor(poly, 'main', None)
                break
    for f in cfg.get('hcuts', []):
        for delta in (0.0, 0.025, -0.025, 0.05, -0.05, 0.08, -0.08, 0.11, -0.11):
            off = (f + delta - 0.5) * H
            if abs(off) > H * 0.5 - 14.0:
                continue
            c0 = (ocx + gay[0] * off, ocy + gay[1] * off)
            p0 = (c0[0] - gax[0] * span, c0[1] - gax[1] * span)
            p1 = (c0[0] + gax[0] * span, c0[1] + gax[1] * span)
            poly = band_poly(p0, p1, half)
            if corridor_fits(poly):
                add_corridor(poly, 'main', None)
                break

    # ---- 4c) 电梯/楼梯核心筒：贴主脊的实心体量
    if cfg.get('lifts', True) and fl['spine'] is not None:
        lang = math.radians(cfg.get('spine_deg', 24.0))
        for t in cfg.get('lift_t', (0.14, 0.5, 0.86)):
            placed = False
            for side in (1, -1):
                if placed:
                    break
                for k in range(4):
                    lh = cfg.get('lift_h', 9.0)
                    # 核心筒近边必须正好贴住主脊边，否则中间会留一条 0.4m 宽的白色缝
                    c = spine_base(t + rng.uniform(-0.025, 0.025), side,
                                   SPINE_HALF + lh * 0.5 + k * 0.5)
                    core = rot_rect(c[0], c[1], cfg.get('lift_w', 13.0), lh, lang)
                    m = poly_mask(core, W, H)
                    if (m & ~inside).sum() > m.sum() * 0.02:
                        continue
                    if (m & (assigned | void | spine_mask)).any():
                        continue
                    assigned = assigned | m
                    fl['cores'].append((core, c))
                    placed = True
                    break

    # ---- 5) 分区：每块自由空间按自己的空间语法填充
    free = inside & ~corridor & ~assigned & ~void
    grammars = cfg.get('grammars') or [
        dict(name='阵列', grid_deg=0.0, max_room=cfg.get('max_room', MAX_ROOM),
             aspect_max=cfg.get('aspect_max', 5.0), strip_axis=cfg.get('strip_axis'),
             strip_w=cfg.get('strip_w', 11.0))]
    ctx_base = {'corridor': corridor, 'corridor_polys': fl['corridors'], 'rng': rng}
    used = {}
    leaves = []
    leftover = []
    blocks = [c for c in components(free) if float(c.sum()) * CELL * CELL > 60.0]
    blocks.sort(key=lambda c: -float(c.sum()))
    for part in blocks:
        g = pick_grammar(grammars, used, rng)
        used[g['name']] = used.get(g['name'], 0) + 1
        gdeg_b = math.radians(g.get('grid_deg', 0.0))
        cax = (math.cos(gdeg_b), math.sin(gdeg_b))
        cay = (-cax[1], cax[0])
        ctx = dict(ctx_base)
        ctx.update({'ax': cax, 'ay': cay,
                    'max_room': g.get('max_room', cfg.get('max_room', MAX_ROOM)),
                    'aspect_max': g.get('aspect_max', cfg.get('aspect_max', 5.0)),
                    'min_accept': g.get('min_accept', cfg.get('min_accept', MIN_ROOM * 1.6)),
                    'strip_axis': g.get('strip_axis'), 'strip_w': g.get('strip_w', 11.0),
                    'local_band': g.get('local_band', 2.8),
                    'no_local': g.get('no_local', False)})
        sub, sx, sy = crop_to(part)
        partition(sub, sx, sy, 0, ctx, leaves, leftover)
        fl['zones'].append({'mask': part, 'grammar': g['name'],
                            'center': mask_center(sub, sx, sy),
                            'deg': g.get('grid_deg', 0.0),
                            'area': float(part.sum()) * CELL * CELL})

    covered = np.zeros_like(inside)
    for mask, ox, oy in leaves:
        covered[oy:oy + mask.shape[0], ox:ox + mask.shape[1]] |= mask
    for mask, ox, oy in leftover:
        covered[oy:oy + mask.shape[0], ox:ox + mask.shape[1]] |= mask

    # 房间多边形
    for mask, ox, oy in leaves:
        room_area = float(mask.sum()) * CELL * CELL
        loops = trace_polys(mask, ox, oy)
        if not loops:
            continue
        loops.sort(key=lambda L: -abs(poly_area([(p[0] * CELL, p[1] * CELL) for p in L])))
        poly = [(p[0] * CELL, p[1] * CELL) for p in loops[0]]
        poly = dp_closed(poly, 0.62)
        if len(poly) < 4:
            continue
        area = abs(poly_area(poly))
        if area < 26.0:
            leftover.append((mask, ox, oy))
            continue
        full = np.zeros_like(inside)
        full[oy:oy + mask.shape[0], ox:ox + mask.shape[1]] = mask
        fl['rooms'].append({'poly': poly, 'mask': full, 'kind': classify(poly),
                            'landmark': False, 'label': None,
                            'center': mask_center(mask, ox, oy), 'area': area})

    # 碎块 → 实心（设备井/结构）
    rest = free & ~covered & ~corridor
    for comp in components(rest):
        sub, sx, sy = crop_to(comp)
        if sub.sum() < 6:
            continue
        loops = trace_polys(sub, sx, sy)
        for L in loops:
            poly = dp_closed([(p[0] * CELL, p[1] * CELL) for p in L], 0.62)
            if abs(poly_area(poly)) > 3.0:
                fl['solids'].append(poly)

    # ---- 6) 门：先开向走廊；没有走廊邻接才开一道套间门；都没有就降级成实心
    rooms_mask = np.zeros_like(inside)
    for room in fl['rooms']:
        rooms_mask |= room['mask']
    walk = corridor | rooms_mask
    for room in fl['rooms']:
        m = room['mask']
        room['doors'] = room_doors(room['poly'], m, corridor)
        if room['area'] < 380.0:
            room['doors'] = room['doors'][:1]
        if not room['doors']:
            room['doors'] = room_doors(room['poly'], m, walk & ~m)[:1]
        if not room['doors'] and not room.get('landmark'):
            room['kind'] = 'solid_room'

    # 没有门的普通房间降级成实心
    keep = []
    for room in fl['rooms']:
        if room['kind'] == 'solid_room':
            loops = trace_polys(room['mask'], 0, 0)
            if loops:
                loops.sort(key=lambda L: -abs(poly_area([(p[0] * CELL, p[1] * CELL) for p in L])))
                fl['solids'].append([(p[0] * CELL, p[1] * CELL) for p in loops[0]])
            continue
        keep.append(room)
    fl['rooms'] = keep

    fl['inside'] = inside
    fl['corridor_mask'] = corridor
    return fl


def stats(fl):
    rooms = fl['rooms']
    kinds = {}
    area_kind = {}
    for r in rooms:
        kinds[r['kind']] = kinds.get(r['kind'], 0) + 1
        area_kind[r['kind']] = area_kind.get(r['kind'], 0.0) + r['area']
    total_area = sum(r['area'] for r in rooms) or 1.0
    nonrect = sum(a for k, a in area_kind.items() if k not in ('rect',)) / total_area
    env_area = abs(poly_area(fl['envelope']))
    corr = float(fl['corridor_mask'].sum()) * CELL * CELL
    return {'count': len(rooms), 'kinds': kinds, 'nonrect': nonrect,
            'env_area': env_area, 'corridor': corr / max(1.0, env_area),
            'rooms_per_1000': len(rooms) / max(1e-6, env_area / 1000.0)}


# ---------------------------------------------------------------- 渲染

def fill_for(kind):
    if kind == 'rect':
        return FILL_RECT
    if kind == 'trap':
        return FILL_TRAP
    if kind == 'fan':
        return FILL_FAN
    if kind == 'hall':
        return FILL_FAN
    return FILL_POLY


def _seg(d, a, b, l0, l1, P, wpx):
    p0 = (a[0] + (b[0] - a[0]) * l0, a[1] + (b[1] - a[1]) * l0)
    p1 = (a[0] + (b[0] - a[0]) * l1, a[1] + (b[1] - a[1]) * l1)
    d.line([P(p0), P(p1)], fill=INK, width=wpx)


def draw_walls(d, room, P, wpx):
    poly = room['poly']
    n = len(poly)
    doors = room.get('doors', [])
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        if L < 0.05:
            continue
        spans = sorted((max(0.0, (t - w / 2) / L), min(1.0, (t + w / 2) / L))
                       for ei, t, w in doors if ei == i)
        cur = 0.0
        for l0, l1 in spans:
            if l0 > cur + 0.004:
                _seg(d, a, b, cur, l0, P, wpx)
            cur = max(cur, l1)
        if cur < 1.0 - 0.004:
            _seg(d, a, b, cur, 1.0, P, wpx)
    # 门扇（朝房间内）
    cx = sum(p[0] for p in poly) / n
    cy = sum(p[1] for p in poly) / n
    for ei, t, w in doors:
        a, b = poly[ei], poly[(ei + 1) % n]
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        ux, uy = (b[0] - a[0]) / L, (b[1] - a[1]) / L
        nx, ny = -uy, ux
        px, py = a[0] + ux * t, a[1] + uy * t
        if (px - cx) * nx + (py - cy) * ny > 0:
            nx, ny = -nx, -ny
        d.line([P((px, py)), P((px + nx * w, py + ny * w))], fill=ACCENT,
               width=max(1, int(wpx * 0.6)))


def hatch_polygon(img, pts, color, spacing=8, width=1):
    from PIL import ImageChops
    pm = Image.new('L', img.size, 0)
    ImageDraw.Draw(pm).polygon(pts, fill=255)
    lm = Image.new('L', img.size, 0)
    ld = ImageDraw.Draw(lm)
    w, h = img.size
    for x in range(-h, w + h, spacing):
        ld.line([(x, 0), (x + h, h)], fill=255, width=width)
    img.paste(Image.new('RGB', img.size, color), mask=ImageChops.multiply(pm, lm))


def render_plan(fl, scale=2.9, pad=30, ss=2):
    W, H = fl['W'], fl['H']
    S = scale * ss
    pw, ph = int(round(W * S)), int(round(H * S))
    PO = int(round(pad * ss))
    img = Image.new('RGB', (pw + 2 * PO, ph + 2 * PO), OUTSIDE)
    d = ImageDraw.Draw(img)

    def P(p):
        return (PO + p[0] * S, PO + (H - p[1]) * S)

    d.rectangle([PO, PO, PO + pw, PO + ph], fill=PAPER)
    d.polygon([P(p) for p in fl['envelope']], fill=(252, 253, 255))

    for kind, poly, label in fl['corridors']:
        d.polygon([P(p) for p in poly], fill=FILL_SPINE if kind == 'spine' else FILL_CORR)
    for room in fl['rooms']:
        d.polygon([P(p) for p in room['poly']], fill=fill_for(room['kind']))
    for poly, label, c in fl['voids']:
        d.polygon([P(p) for p in poly], fill=FILL_VOID)
    for poly in fl['solids']:
        d.polygon([P(p) for p in poly], fill=FILL_SOLID)

    for core, c in fl.get('cores', []):
        d.polygon([P(p) for p in core], fill=(226, 230, 237))

    # 外廓以外清成页面底色
    em = Image.new('L', img.size, 0)
    ImageDraw.Draw(em).polygon([P(p) for p in fl['envelope']], fill=255)
    img.paste(OUTSIDE, mask=Image.eval(em, lambda v: 255 - v))

    # 挑空斜纹
    for poly, label, c in fl['voids']:
        hatch_polygon(img, [P(p) for p in poly], (96, 102, 114), spacing=int(7 * ss), width=max(1, ss))
    d = ImageDraw.Draw(img)
    for core, c in fl.get('cores', []):
        hatch_polygon(img, [P(p) for p in core], (152, 160, 174), spacing=int(6 * ss), width=max(1, ss))
        d.line([P(p) for p in core] + [P(core[0])], fill=(92, 99, 114),
               width=max(2, int(round(0.28 * S))))

    wpx = max(3, int(round(0.42 * S)))
    for room in fl['rooms']:
        draw_walls(d, room, P, wpx)
    for room in fl['rooms']:
        if room.get('landmark'):
            d.line([P(p) for p in room['poly']] + [P(room['poly'][0])],
                   fill=INK, width=wpx + ss, joint='curve')
    d.line([P(p) for p in fl['envelope']] + [P(fl['envelope'][0])],
           fill=INK, width=wpx + ss + 1, joint='curve')

    out_w = int(round(W * scale)) + 2 * pad
    out_h = int(round(H * scale)) + 2 * pad
    return img.resize((out_w, out_h), Image.LANCZOS)


def inside_pt(fl, p):
    ci = int(p[0] // CELL)
    cj = int(p[1] // CELL)
    m = fl['inside']
    return 0 <= cj < m.shape[0] and 0 <= ci < m.shape[1] and bool(m[cj, ci])


def in_corridor(fl, p):
    cm = fl.get('corridor_mask')
    if cm is None:
        return True
    ci = int(p[0] / CELL)
    cj = int(p[1] / CELL)
    if 0 <= cj < cm.shape[0] and 0 <= ci < cm.shape[1]:
        return bool(cm[cj, ci])
    return False


def decorate(img, fl, scale, pad, big=False):
    """地标文字、主脊虚线、比例尺、指北针"""
    d = ImageDraw.Draw(img)
    W, H = fl['W'], fl['H']

    def P(p):
        return (pad + p[0] * scale, pad + (H - p[1]) * scale)

    fs = max(12, int(10.5 * scale / 3.0))
    fb = font(fs, bold=True)
    fs2 = max(10, int(8.0 * scale / 3.0))
    f2 = font(fs2)

    # 主脊中线（虚线）+ 名字
    if fl['spine']:
        p0, p1, half, dd, nn, cx, cy = fl['spine']
        L = math.hypot(p1[0] - p0[0], p1[1] - p0[1])
        t = 0.0
        while t < 1.0:
            q0 = (p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t)
            q1 = (p0[0] + (p1[0] - p0[0]) * min(1.0, t + 0.018), p0[1] + (p1[1] - p0[1]) * min(1.0, t + 0.018))
            if inside_pt(fl, q0) and in_corridor(fl, q0):
                d.line([P(q0), P(q1)], fill=(150, 160, 178), width=max(1, int(1.4 * scale / 3)))
            t += 0.03
        # 标签放在脊线中段
        for t in (0.5, 0.42, 0.58, 0.35, 0.65):
            q = (p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t)
            if inside_pt(fl, q):
                label = fl['corridors'][0][2] if fl['corridors'] and fl['corridors'][0][0] == 'spine' else '主脊'
                if label:
                    tw = d.textlength(label, font=f2)
                    x, y = P(q)
                    d.rectangle([x - tw / 2 - 3, y - fs2 / 2 - 2, x + tw / 2 + 3, y + fs2 / 2 + 2], fill=(238, 241, 247))
                    d.text((x - tw / 2, y - fs2 / 2 - 1), label, font=f2, fill=(84, 92, 110))
                break

    # 地标
    for room in fl['rooms']:
        if not room.get('label'):
            continue
        x, y = P(room['center'])
        tw = d.textlength(room['label'], font=fb)
        d.rounded_rectangle([x - tw / 2 - 5, y - fs * 0.8, x + tw / 2 + 5, y + fs * 0.8],
                            radius=4, fill=(255, 250, 238), outline=(206, 168, 96))
        d.text((x - tw / 2, y - fs * 0.62), room['label'], font=fb, fill=(122, 74, 18))

    # 分区语法标签
    fz = font(max(11, int(7.6 * scale / 3.0)))
    for z in fl.get('zones', []):
        if z['area'] < 1100.0:
            continue
        x, y = P(z['center'])
        label = z['grammar']
        tw = d.textlength(label, font=fz)
        d.text((x - tw / 2, y), label, font=fz, fill=(158, 165, 178))
    for poly, label, c in fl['voids']:
        x, y = P(c)
        tw = d.textlength(label, font=fb)
        d.text((x - tw / 2, y - fs * 0.62), label, font=fb, fill=(226, 230, 238))

    # 比例尺
    bar = 20.0
    x0, y0 = pad + 6, H * scale + pad - 20
    d.line([(x0, y0), (x0 + bar * scale, y0)], fill=INK, width=3)
    for k in range(0, int(bar) + 1, 10):
        d.line([(x0 + k * scale, y0 - 4), (x0 + k * scale, y0 + 4)], fill=INK, width=2)
    d.text((x0, y0 + 8), '20 m', font=f2, fill=INK_SOFT)

    # 指北针
    ax, ay = W * scale + pad - 26, pad + 22
    d.line([(ax, ay + 14), (ax, ay - 12)], fill=INK, width=2)
    d.polygon([(ax, ay - 18), (ax - 5, ay - 8), (ax + 5, ay - 8)], fill=INK)
    d.text((ax - 4, ay + 16), 'N', font=f2, fill=INK)


def kind_label(kind):
    return {'rect': '矩形', 'trap': '梯形', 'fan': '扇形', 'hall': '地标体量',
            'poly': '多边形'}.get(kind, kind)


def stats_line(st, compact=False):
    k = st['kinds']
    if compact:
        return '房间 %d · 非矩形面积 %d%%' % (st['count'], round(st['nonrect'] * 100))
    order = ['rect', 'trap', 'poly', 'fan']
    seg = ' / '.join('%s %d' % (kind_label(o), k[o]) for o in order if k.get(o))
    return '房间 %d · %s · 非矩形面积 %d%%' % (st['count'], seg, round(st['nonrect'] * 100))


def compose_sheet(floors, out_path, title, subtitle):
    scale, pad = 3.5, 30
    plans = [render_plan(fl, scale=scale, pad=pad, ss=2) for fl in floors]
    for i, fl in enumerate(floors):
        decorate(plans[i], fl, scale, pad)
    pw, ph = plans[0].size
    cols, rows = 2, 3
    margin, head = 52, 136
    panel_w, panel_h = pw + 36, ph + 104
    gap = 22
    Wt = margin * 2 + panel_w * cols + gap * (cols - 1)
    Ht = head + margin + panel_h * rows + gap * (rows - 1) + 84
    sheet = Image.new('RGB', (Wt, Ht), (247, 248, 252))
    d = ImageDraw.Draw(sheet)
    d.text((margin, 40), title, font=font(38, True), fill=INK)
    d.text((margin, 92), subtitle, font=font(18), fill=INK_SOFT)
    for i, fl in enumerate(floors):
        r, c = divmod(i, cols)
        x = margin + c * (panel_w + gap)
        y = head + r * (panel_h + gap)
        d.rounded_rectangle([x, y, x + panel_w, y + panel_h], radius=10,
                            fill=(255, 255, 255), outline=(224, 228, 236))
        sheet.paste(plans[i], (x + 18, y + 14))
        st = stats(fl)
        d.text((x + 20, y + ph + 26), fl['name'], font=font(21, True), fill=INK)
        d.text((x + 20, y + ph + 54), fl['sub'], font=font(14), fill=INK_SOFT)
        d.text((x + 20, y + ph + 76), stats_line(st), font=font(14, True), fill=(150, 92, 40))
    # 图例
    ly = Ht - 56
    items = [('矩形', FILL_RECT), ('梯形 / 三角形', FILL_TRAP), ('扇形', FILL_FAN),
             ('多边形', FILL_POLY), ('走廊', FILL_CORR), ('挑空', FILL_VOID), ('实心 / 结构', FILL_SOLID)]
    lx = margin
    for label, col in items:
        d.rectangle([lx, ly, lx + 16, ly + 16], fill=col, outline=(180, 186, 198))
        d.text((lx + 22, ly - 1), label, font=font(14), fill=INK_SOFT)
        lx += 24 + d.textlength(label, font=font(14)) + 22
    sheet.save(out_path)
    return sheet.size


def mix_bars(img, d, x, y, w, h, fl, title):
    st = stats(fl)
    total = sum(st['kinds'].get(k, 0) for k in st['kinds']) or 1
    d.text((x, y), title, font=font(15, True), fill=INK)
    yy = y + 26
    for kind in ['rect', 'trap', 'poly', 'fan']:
        n = st['kinds'].get(kind, 0)
        if not n:
            continue
        bw = int(w * n / total)
        d.rectangle([x, yy, x + max(1, bw), yy + 18], fill=fill_for(kind))
        d.rectangle([x, yy, x + max(1, bw), yy + 18], outline=(190, 195, 206))
        d.text((x + 6, yy + 1), '%s %d' % (kind_label(kind), n), font=font(12), fill=INK)
        yy += 22
    d.text((x, yy + 2), '非矩形面积 %d%%' % round(st['nonrect'] * 100), font=font(13, True), fill=(150, 92, 40))


def compose_compare(base, new, out_path):
    scale, pad = 3.1, 26
    pa = render_plan(base, scale=scale, pad=pad, ss=2)
    pb = render_plan(new, scale=scale, pad=pad, ss=2)
    pw, ph = pa.size
    Wt = 52 * 2 + pw * 2 + 40
    Ht = 150 + ph + 210
    img = Image.new('RGB', (Wt, Ht), (247, 248, 252))
    d = ImageDraw.Draw(img)
    d.text((52, 36), '同一层尺寸（200×160m）对照：现状 vs 结构动作传播', font=font(34, True), fill=INK)
    d.text((52, 86), '左：纯矩形 BSP，走廊是唯一变化；右：2 个结构动作（斜向主脊 + 45° 旋转大厅），形状由边界夹出来',
           font=font(17), fill=INK_SOFT)
    img.paste(pa, (52, 128))
    img.paste(pb, (52 + pw + 40, 128))
    d.text((52, 128 + ph + 8), base['name'], font=font(20, True), fill=INK)
    d.text((52 + pw + 40, 128 + ph + 8), new['name'], font=font(20, True), fill=INK)
    mix_bars(img, d, 52, 128 + ph + 46, 320, 120, base, '房间形状构成')
    mix_bars(img, d, 52 + pw + 40, 128 + ph + 46, 320, 120, new, '房间形状构成')
    img.save(out_path)
    return img.size


# ---------------------------------------------------------------- 程序单

GRAMMAR_LIBRARY = {
    '阵列': dict(name='阵列网格', grid_deg=0.0, max_room=330, aspect_max=1.6, weight=3),
    '长条': dict(name='长条仓区', grid_deg=0.0, strip_axis='x', strip_w=10.0,
               max_room=1200, aspect_max=9.0, weight=2),
    '大跨': dict(name='无柱大跨', grid_deg=0.0, max_room=2400, aspect_max=3.4,
               no_local=True, weight=1),
    '斜网': dict(name='斜网 +14°', grid_deg=14.0, max_room=430, aspect_max=4.0, weight=2),
    '斜网2': dict(name='斜网 -9°', grid_deg=-9.0, max_room=520, aspect_max=4.5, weight=2),
    '管廊': dict(name='管廊窄间', grid_deg=0.0, max_room=250, aspect_max=2.2,
               local_band=2.2, weight=2),
    '展厅': dict(name='展厅', grid_deg=0.0, max_room=700, aspect_max=2.4,
               no_local=True, weight=2),
    '小间': dict(name='小隔间', grid_deg=0.0, max_room=210, aspect_max=1.4, weight=3),
}


def gl(names):
    return [GRAMMAR_LIBRARY[n] for n in names]


def floor_programs():
    return [
        dict(name='F02 · 物流层', sub='长条仓区 + 阵列网格 + 管廊窄间，三段语法并置；斜脊只切一刀',
             seed=2027, W=200, H=160, gestures=['spine'], spine_deg=23,
             vcuts=[0.30, 0.72], hcuts=[0.45],
             grammars=gl(['长条', '阵列', '管廊']), max_room=1300, aspect_max=9,
             spine_label='货运主脊'),
        dict(name='F07 · 数据层', sub='斜脊 + 45° 旋转大厅；厅四周被斜边夹成梯形，其余按语法分三区',
             seed=7071, W=200, H=160, gestures=['spine', 'hall'], spine_deg=19,
             vcuts=[0.31, 0.70], hcuts=[0.38],
             hall_deg=45, hall_size=(52, 32), hall_label='数据大厅',
             grammars=gl(['阵列', '斜网', '管廊', '大跨']), max_room=520, min_accept=70),
        dict(name='F11 · 居住层', sub='斜脊 + 八角中庭；环廊外一圈暗房间，两翼是阵列网格与小隔间',
             seed=1113, W=200, H=160, gestures=['spine', 'atrium'], spine_deg=31,
             vcuts=[0.26, 0.74], hcuts=[],
             atrium_r=15.5, atrium_label='采光中庭',
             grammars=gl(['小间', '阵列', '管廊']), max_room=420, min_accept=64),
        dict(name='F15 · 医疗层', sub='斜脊 + 环形核心筒；八条扇形病区，外圈长条病房与阵列护理单元',
             seed=1511, W=200, H=160, gestures=['spine', 'hub'], spine_deg=16,
             vcuts=[0.34, 0.66], hcuts=[],
             hub_r=21, aspect_max=7.0, max_room=900,
             grammars=gl(['阵列', '管廊', '长条'])),
        dict(name='F18 · 中转层', sub='两个地标叠在一起；三套柱网方向并置（正交 / +14° / -9°），形状最杂',
             seed=1819, W=200, H=160, gestures=['spine', 'hub', 'hall'], spine_deg=27,
             vcuts=[0.28, 0.73], hcuts=[0.68],
             hub_r=19, hall_size=(44, 28), hall_label='换乘大厅',
             grammars=gl(['斜网', '斜网2', '阵列', '大跨']), max_room=560),
        dict(name='F21 · 观景层', sub='斜脊 + 大中庭；无柱大跨与展厅为主，房间少而大',
             seed=2129, W=200, H=160, gestures=['spine', 'atrium'], spine_deg=36,
             vcuts=[0.42], hcuts=[0.30, 0.70],
             atrium_r=19.5, atrium_label='挑空中庭',
             grammars=gl(['大跨', '展厅', '阵列']), max_room=1150, aspect_max=6.0,
             min_accept=90),
    ]


def baseline_program():
    return dict(name='现状 · 矩形 BSP（seed 7071）', sub='无外廓、无结构动作、只切矩形',
                seed=7071, W=200, H=160, gestures=[], envelope=False,
                vcuts=[0.31, 0.70], hcuts=[0.38], max_room=520, min_accept=70)

def main(argv):
    out = None
    i = 1
    while i < len(argv):
        if argv[i] == '--out':
            out = argv[i + 1]
            i += 2
        else:
            i += 1
    if out is None:
        out = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')
    os.makedirs(out, exist_ok=True)
    os.makedirs(os.path.join(out, 'large'), exist_ok=True)

    floors = []
    for cfg in floor_programs():
        fl = make_floor(cfg)
        floors.append(fl)
        st = stats(fl)
        print('[%s] rooms=%d kinds=%s nonrect=%.0f%% corridor=%.1f%%'
              % (fl['name'], st['count'], st['kinds'], st['nonrect'] * 100, st['corridor'] * 100))

    compose_sheet(floors, os.path.join(out, 'plan-proto-sheet.png'),
                  '楼层平面原型：形状从结构动作传播，而不是给矩形撒斜线',
                  '每层 200×160m · 先落 2–3 个非正交结构动作（斜向主脊 / 旋转大厅 / 环形核心筒 / 中庭），再把剩余楼板按区分给空间语法（阵列 / 长条 / 大跨 / 斜网 / 管廊）')

    for fl in floors:
        if fl['name'].startswith(('F07', 'F18')):
            big = render_plan(fl, scale=8.6, pad=56, ss=2)
            decorate(big, fl, 8.6, 56, big=True)
            path = os.path.join(out, 'large', 'plan-proto-%s.png' % fl['name'][:3].lower())
            big.save(path)
            print('wrote', path, big.size)

    base = make_floor(baseline_program())
    print('[baseline] rooms=%d kinds=%s nonrect=%.0f%%'
          % (stats(base)['count'], stats(base)['kinds'], stats(base)['nonrect'] * 100))
    compose_compare(base, floors[1], os.path.join(out, 'plan-proto-compare.png'))
    print('done ->', out)


if __name__ == '__main__':
    main(sys.argv)