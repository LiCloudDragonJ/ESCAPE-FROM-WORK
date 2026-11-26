# -*- coding: utf-8 -*-
"""设计示意图 v2：门数 / 走廊断点 / 复杂但不迷宫 / 设施类型学"""
import math
from PIL import Image, ImageDraw
import plan_proto as pp

SS = 2
INK = (33, 39, 51)
SOFT = (122, 130, 145)
PAPER = (252, 253, 255)
CORR = (214, 221, 232)
ROOM = (255, 255, 255)
ACC = (196, 120, 62)
RED = (176, 68, 60)
BLUE = (58, 108, 178)
GREEN = (70, 128, 86)
GLASS = (127, 168, 217)
VCORR = (198, 220, 202)


def F(sz, bold=False):
    return pp.font(int(round(sz * SS)), bold=bold)


def T(d, xy, s, sz, fill=INK, anchor='la', bold=False):
    d.text((xy[0] * SS, xy[1] * SS), s, font=F(sz, bold), fill=fill, anchor=anchor)


def seg(d, p0, p1, w=3, fill=INK):
    d.line([(p0[0] * SS, p0[1] * SS), (p1[0] * SS, p1[1] * SS)], fill=fill,
           width=max(1, int(round(w * SS))))


def rect(d, x, y, w, h, fill=None, outline=None, ow=3, rad=0):
    box = [x * SS, y * SS, (x + w) * SS, (y + h) * SS]
    if rad:
        d.rounded_rectangle(box, radius=rad * SS, fill=fill, outline=outline,
                            width=max(1, int(round(ow * SS))))
    else:
        d.rectangle(box, fill=fill, outline=outline, width=max(1, int(round(ow * SS))))


def wline(d, p0, p1, w=5, fill=INK, gaps=()):
    x0, y0 = p0
    x1, y1 = p1
    L = math.hypot(x1 - x0, y1 - y0)
    if L < 1e-6:
        return
    ux, uy = (x1 - x0) / L, (y1 - y0) / L
    cuts = []
    for (t, gw) in gaps:
        c = t * L
        cuts.append((max(0.0, c - gw / 2), min(L, c + gw / 2)))
    cuts.sort()
    run = 0.0
    for (a, b) in cuts:
        if a > run:
            seg(d, (x0 + ux * run, y0 + uy * run), (x0 + ux * a, y0 + uy * a), w, fill)
        run = max(run, b)
    if run < L:
        seg(d, (x0 + ux * run, y0 + uy * run), (x0 + ux * L, y0 + uy * L), w, fill)


def arrow(d, p0, p1, fill=BLUE, w=2.2, head=10):
    seg(d, p0, p1, w, fill)
    ang = math.atan2(p1[1] - p0[1], p1[0] - p0[0])
    hp = [(p1[0] - head * math.cos(ang - 0.42), p1[1] - head * math.sin(ang - 0.42)),
          (p1[0], p1[1]),
          (p1[0] - head * math.cos(ang + 0.42), p1[1] - head * math.sin(ang + 0.42))]
    d.polygon([(p[0] * SS, p[1] * SS) for p in hp], fill=fill)


def door_leaf(d, base, dirvec, length=12, w=3.6, color=ACC):
    bx, by = base
    dx, dy = dirvec
    nx, ny = -dy, dx
    seg(d, (bx, by), (bx + dx * length + nx * length * 0.7, by + dy * length + ny * length * 0.7),
        w, color)


def panel(d, x, y, w, h, title, sub):
    rect(d, x, y, w, h, fill=PAPER, outline=(224, 229, 238), ow=2, rad=8)
    T(d, (x + 30, y + 20), title, 25, INK, bold=True)
    T(d, (x + 30, y + 58), sub, 13.5, SOFT)


def bullets(d, x, y, items, gap=31, sz=14.5):
    for i, s in enumerate(items):
        T(d, (x, y + i * gap), s, sz, INK)


W, H = 1790, 2560
sheet = Image.new('RGB', (W * SS, H * SS), PAPER)
d = ImageDraw.Draw(sheet)
T(d, (54, 34), '新生成规则示意：门 / 走廊 / 复杂度 / 设施类型', 38, INK, bold=True)
T(d, (54, 86), '用来替换当前「单门 + 200m 直廊 + 同质大房间」的三条规则；第四格是去掉办公室题材后的设施类型学', 16, SOFT)

PW, PH = 812, 1180
X1, X2 = 54, 54 + PW + 46
Y1, Y2 = 132, 132 + PH + 36

# ===================== A · 门 =====================
ax, ay = X1, Y1
panel(d, ax, ay, PW, PH, 'A · 门数：没有单门房间', '普通房 ≥2 门且不在同一面墙；≥100㎡ 目标 3 门；保险库留「一道焊死」')

S = 9.4
ox, oy = ax + 40, ay + 128
T(d, (ox, oy - 28), '现状：单门房间（93–100%）', 17, RED, bold=True)
cw = 2.6 * S
rect(d, ox, oy, cw, 28 * S, fill=CORR)
rx, ry, rw, rh = ox + cw, oy + 4 * S, 15 * S, 20 * S
rect(d, rx, ry, rw, rh, fill=ROOM)
wline(d, (ox, oy), (ox, oy + 28 * S), 4)
wline(d, (ox + cw, oy), (ox + cw, ry), 4)
wline(d, (ox + cw, ry + rh), (ox + cw, oy + 28 * S), 4)
wline(d, (rx, ry), (rx, ry + rh), 4)
wline(d, (rx + rw, ry), (rx + rw, ry + rh), 4)
wline(d, (rx, ry + rh), (rx + rw, ry + rh), 4)
wline(d, (rx, ry), (rx + rw, ry), 4)
wline(d, (ox + cw, ry), (ox + cw, ry + rh), 4, INK, gaps=[(0.5, 1.1 * S)])
door_leaf(d, (ox + cw, ry + rh * 0.5), (1, 0), 12, 3.6)
seg(d, (rx + rw + 16, ry + 8), (rx + rw + 16, ry + rh - 8), 2, RED)
arrow(d, (rx + rw + 34, ry + rh * 0.5), (rx + cw + 24, ry + rh * 0.5), RED, 1.9)
T(d, (rx + rw + 44, ry + rh * 0.5 - 34), '唯一进路', 15.5, RED, bold=True)
T(d, (rx + rw + 44, ry + rh * 0.5 - 12), '被堵住 = 死局', 15.5, RED, bold=True)
T(d, (rx + 0.7 * S, ry + rh * 0.5 - 22), '无第二出口', 14, RED)
T(d, (ox - 2, oy + 28 * S + 10), '走廊', 13, SOFT)

ox2, oy2 = ax + 40, ay + 468
T(d, (ox2, oy2 - 30), '新规则：≥2 门、不同面墙 + 2 条独立退路', 17, GREEN, bold=True)
rect(d, ox2, oy2, 2.8 * S, 24 * S, fill=CORR)
rect(d, ox2, oy2 + 24 * S - 2.8 * S, 28 * S, 2.8 * S, fill=CORR)
rx2, ry2 = ox2 + 2.8 * S, oy2
rw2, rh2 = 18 * S, 21.2 * S
rect(d, rx2, ry2, rw2, rh2, fill=ROOM)
wline(d, (rx2, ry2), (rx2, ry2 + rh2), 4, INK, gaps=[(0.3, 1.1 * S)])
wline(d, (rx2, ry2 + rh2), (rx2 + rw2, ry2 + rh2), 4, INK, gaps=[(0.62, 1.1 * S)])
wline(d, (rx2 + rw2, ry2), (rx2 + rw2, ry2 + rh2), 4)
wline(d, (rx2, ry2), (rx2 + rw2, ry2), 4)
door_leaf(d, (rx2, ry2 + 0.3 * rh2), (1, 0), 12, 3.6)
door_leaf(d, (rx2 + 0.62 * rw2, ry2 + rh2), (0, 1), 12, 3.6)
arrow(d, (ox2 + 1.3 * S, oy2 + 21.4 * S), (ox2 + 1.3 * S, oy2 + 2 * S), BLUE, 2.0)
arrow(d, (ox2 + 24 * S, oy2 + 25.5 * S), (ox2 + 4 * S, oy2 + 25.5 * S), BLUE, 2.0)
T(d, (rx2 + rw2 + 12, ry2 + 4), '两条独立', 14.5, BLUE, bold=True)
T(d, (rx2 + rw2 + 12, ry2 + 24), '退路', 14.5, BLUE, bold=True)

# 保险库
vx, vy = ax + 470, ay + 470
T(d, (vx - 4, vy - 30), '保险库 / 钥匙房（≤8% 面积）', 15, INK, bold=True)
rect(d, vx, vy, 8.6 * S, 7.4 * S, fill=(251, 246, 239))
wline(d, (vx, vy), (vx, vy + 7.4 * S), 4, INK, gaps=[(0.5, 1.1 * S)])
wline(d, (vx, vy + 7.4 * S), (vx + 8.6 * S, vy + 7.4 * S), 4, INK, gaps=[(0.62, 1.1 * S)])
wline(d, (vx + 8.6 * S, vy), (vx + 8.6 * S, vy + 7.4 * S), 4)
wline(d, (vx, vy), (vx + 8.6 * S, vy), 4)
door_leaf(d, (vx, vy + 3.7 * S), (1, 0), 11, 3.4)
mx = vx + 0.62 * 8.6 * S
my = vy + 7.4 * S
seg(d, (mx - 8, my - 8), (mx + 8, my + 8), 2.6, RED)
seg(d, (mx - 8, my + 8), (mx + 8, my - 8), 2.6, RED)
T(d, (vx, vy + 7.4 * S + 14), '物理 2 门，1 道焊死 + 进入即报警', 12.5, SOFT)
T(d, (vx, vy + 7.4 * S + 34), '→ 保留「进去了要想怎么出来」，但不再是几何死局', 12.5, SOFT)

bullets(d, ax + 30, ay + 790, [
    '· 普通房：≥2 门，且不能开在同一面墙上（= 一条走廊两个口，仍是单点）',
    '· ≥100㎡：目标 3 门；两条路径不能出门就汇到同一条走廊',
    '· 大厅 >400㎡：≥3 门，且至少 2 个开在长边两端',
    '· 死路走廊 0 条 —— 走廊尽头必须是门，不是墙',
    '· 门位不居中：距墙角 0.15–0.5m 或 0.9–1.2m；对门错位 ≥1.10m',
])
T(d, (ax + 30, ay + 1010), '收益：单门 93–100% → 0%', 16, RED, bold=True)
T(d, (ax + 30, ay + 1040), '每个房间至少有一条跑得掉的路，交火可以从「被堵」变成「拉扯」', 13.5, SOFT)

# ===================== B · 走廊断点 =====================
bx, by = X2, Y1
panel(d, bx, by, PW, PH, 'B · 走廊断点：把 200m 视线切成 ≤45m', '断点不是砌墙挡视线，是「给第二条路」——只砌墙等于把屠杀点往前挪 30m')

S2 = 2.95
Lm = 236
ox, oy = bx + 44, by + 150
T(d, (ox, oy - 46), '现状：一条直廊贯穿全层', 17, RED, bold=True)
rect(d, ox, oy, Lm * S2, 4.6 * S2, fill=CORR)
wline(d, (ox, oy), (ox + Lm * S2, oy), 4)
wline(d, (ox, oy + 4.6 * S2), (ox + Lm * S2, oy + 4.6 * S2), 4)
wline(d, (ox, oy), (ox, oy + 4.6 * S2), 4)
wline(d, (ox + Lm * S2, oy), (ox + Lm * S2, oy + 4.6 * S2), 4)
arrow(d, (ox + 12, oy + 2.3 * S2), (ox + Lm * S2 - 12, oy + 2.3 * S2), RED, 1.9)
T(d, (ox + Lm * S2 * 0.5, oy - 22), '160–200m 一条直线看穿', 15, RED, anchor='ma', bold=True)
T(d, (ox + Lm * S2 * 0.5, oy + 4.6 * S2 + 12), '中间零断点、零侧向出口；被压制时无处可进', 12.5, SOFT, anchor='ma')

oy2 = oy + 176
T(d, (ox, oy2 - 58), '新规则：三种合法断点，每 26–32m 一个', 17, GREEN, bold=True)
rect(d, ox, oy2, Lm * S2, 4.6 * S2, fill=CORR)
wline(d, (ox, oy2), (ox + Lm * S2, oy2), 4)
wline(d, (ox, oy2 + 4.6 * S2), (ox + Lm * S2, oy2 + 4.6 * S2), 4)
wline(d, (ox, oy2), (ox, oy2 + 4.6 * S2), 4)
wline(d, (ox + Lm * S2, oy2), (ox + Lm * S2, oy2 + 4.6 * S2), 4)
gaps_m = [56, 104, 152, 200]
names = ['交叉口', '凹位 / 门斗', '可穿行房间', '交叉口']
for k, gm in enumerate(gaps_m):
    px = ox + gm * S2
    if k in (0, 3):
        hw = 2.3 * S2
        rect(d, px - hw, oy2 - 3.6 * S2, 2 * hw, 4.6 * S2 + 7.2 * S2, fill=CORR)
        wline(d, (px - hw, oy2 - 3.6 * S2), (px + hw, oy2 - 3.6 * S2), 3.5)
        wline(d, (px - hw, oy2 + 4.6 * S2 + 3.6 * S2), (px + hw, oy2 + 4.6 * S2 + 3.6 * S2), 3.5)
        wline(d, (px - hw, oy2 - 3.6 * S2), (px - hw, oy2), 3.5)
        wline(d, (px + hw, oy2 - 3.6 * S2), (px + hw, oy2), 3.5)
        wline(d, (px - hw, oy2 + 4.6 * S2), (px - hw, oy2 + 4.6 * S2 + 3.6 * S2), 3.5)
        wline(d, (px + hw, oy2 + 4.6 * S2), (px + hw, oy2 + 4.6 * S2 + 3.6 * S2), 3.5)
    elif k == 1:
        hw = 1.9 * S2
        rect(d, px - hw, oy2 + 4.6 * S2, 2 * hw, 2.6 * S2, fill=CORR)
        wline(d, (px - hw, oy2 + 4.6 * S2), (px - hw, oy2 + 4.6 * S2 + 2.6 * S2), 3.5)
        wline(d, (px + hw, oy2 + 4.6 * S2), (px + hw, oy2 + 4.6 * S2 + 2.6 * S2), 3.5)
        wline(d, (px - hw, oy2 + 4.6 * S2 + 2.6 * S2), (px + hw, oy2 + 4.6 * S2 + 2.6 * S2), 3.5)
        wline(d, (px - hw, oy2 + 4.6 * S2), (px + hw, oy2 + 4.6 * S2), 3.5, INK, gaps=[(0.5, 2.4 * S2)])
    else:
        hw = 3.3 * S2
        rect(d, px - hw, oy2 - 4.4 * S2, 2 * hw, 4.6 * S2 + 8.8 * S2, fill=ROOM)
        wline(d, (px - hw, oy2 - 4.4 * S2), (px - hw, oy2 + 4.6 * S2 + 4.4 * S2), 3.5)
        wline(d, (px + hw, oy2 - 4.4 * S2), (px + hw, oy2 + 4.6 * S2 + 4.4 * S2), 3.5)
        wline(d, (px - hw, oy2 - 4.4 * S2), (px + hw, oy2 - 4.4 * S2), 3.5)
        wline(d, (px - hw, oy2 + 4.6 * S2 + 4.4 * S2), (px + hw, oy2 + 4.6 * S2 + 4.4 * S2), 3.5)
        wline(d, (px - hw, oy2 + 4.6 * S2), (px - hw, oy2 + 4.6 * S2 + 4.4 * S2), 3.5, INK,
              gaps=[(0.5, 1.1 * S2)])
    T(d, (px, oy2 + 4.6 * S2 + 6.6 * S2), names[k], 12.5, SOFT, anchor='ma')
for k in range(3):
    a = ox + (gaps_m[k] + 2.3) * S2
    b = ox + (gaps_m[k + 1] - 2.3) * S2
    yy = oy2 - 22
    seg(d, (a + 4, yy), (b - 4, yy), 1.3, BLUE)
    seg(d, (a + 4, yy - 5), (a + 4, yy + 5), 1.3, BLUE)
    seg(d, (b - 4, yy - 5), (b - 4, yy + 5), 1.3, BLUE)
    T(d, ((a + b) * 0.5, yy - 22), '26–32m', 12, BLUE, anchor='ma')
T(d, (ox + Lm * S2 * 0.5, oy2 - 92), '任何一段的视线 ≤ 45m', 14, BLUE, anchor='ma', bold=True)

# 并行服务走廊
oy3 = oy2 + 150
T(d, (ox, oy3 - 34), '并行服务走廊：给「被压制的人」一条替代路线', 15.5, INK, bold=True)
rect(d, ox, oy3, Lm * S2 * 0.86, 4.6 * S2, fill=CORR)
wline(d, (ox, oy3), (ox + Lm * S2 * 0.86, oy3), 4)
wline(d, (ox, oy3 + 4.6 * S2), (ox + Lm * S2 * 0.86, oy3 + 4.6 * S2), 4)
svx = ox + 6
svw = 2.0 * S2
rect(d, svx, oy3 + 8 * S2, Lm * S2 * 0.8, svw, fill=VCORR)
wline(d, (svx, oy3 + 8 * S2), (svx + Lm * S2 * 0.8, oy3 + 8 * S2), 3)
wline(d, (svx, oy3 + 8 * S2 + svw), (svx + Lm * S2 * 0.8, oy3 + 8 * S2 + svw), 3)
for k in range(1, 4):
    lx = svx + k * (Lm * S2 * 0.8) / 4.0
    seg(d, (lx, oy3 + 4.6 * S2), (lx, oy3 + 8 * S2), 3, ACC)
T(d, (svx + 4, oy3 + 8 * S2 + svw + 10), '服务廊 1.8–2.1m，每 30–40m 一个横向连通口（橙）', 12.5, SOFT)
T(d, (ox + Lm * S2 * 0.86 + 8, oy3 + 2), '主廊', 12.5, SOFT)

# 四级宽度
oy4 = oy3 + 132
T(d, (ox, oy4 - 30), '四级走廊宽度：宽度本身就在讲「该走哪」', 15.5, INK, bold=True)
levels = [('主廊 4.8m', 4.8, INK), ('次廊 3.6m', 3.6, (70, 78, 92)),
          ('三级 2.7m', 2.7, (110, 118, 132)), ('服务 1.8–2.1m', 2.0, (150, 158, 172))]
for i, (nm, wm, col) in enumerate(levels):
    yy = oy4 + 4 + i * 30
    px = wm * 17
    rect(d, ox, yy, px, 13, fill=(226, 231, 240))
    seg(d, (ox, yy), (ox + px, yy), 2, col)
    seg(d, (ox, yy + 13), (ox + px, yy + 13), 2, col)
    T(d, (ox + px + 12, yy - 1), nm, 12.5, INK)

bullets(d, bx + 30, by + 830, [
    '· 断点三种：① 交叉口 ② 凹位/门斗（2.6m 进深）③ 可穿行房间',
    '· fatal funnel：门内外各 2.5m 不放主掩体与高价值容器',
    '· 交叉口净空 ≥4.8×4.8m 且内部不放掩体；T/Y 开口 ≥3.0m',
    '· 最长直段 30m；每 20–30m 必须有门或凹位，否则隔断只是挪动屠杀点',
])
T(d, (bx + 30, by + 1000), '收益：最长视线 160–200m → ≤45m，且每 30m 至少一条活路', 15, RED, bold=True)

# ===================== C · 复杂 ≠ 迷宫 =====================
cx, cy = X1, Y2
panel(d, cx, cy, PW, PH, 'C · 复杂 ≠ 迷宫：回环 + 地标 + 层级隔断', '迷宫感来自「不可预测 + 无回环 + 无地标 + 全同质」，不是来自路多')

nodes = [(i, j) for i in range(4) for j in (-1, 0, 1)]
edges_tree = [((0, 0), (0, -1)), ((0, -1), (1, -1)), ((1, -1), (2, -1)), ((2, -1), (3, -1)),
              ((0, 0), (1, 0)), ((1, 0), (1, 1)), ((0, 0), (0, 1)), ((1, 0), (2, 0)),
              ((2, 0), (2, 1)), ((2, 0), (3, 0)), ((3, 0), (3, 1))]
SP3 = 60
gx_l, gy_l = cx + 62, cy + 214
pos = {(i, j): (gx_l + i * SP3, gy_l + j * SP3) for (i, j) in nodes}
T(d, (cx + 40, cy + 132), '迷宫：树状拓扑，每次深入都要原路返回', 15.5, RED, bold=True)
for (a, b) in edges_tree:
    seg(d, pos[a], pos[b], 16, CORR)
for (a, b) in edges_tree:
    seg(d, pos[a], pos[b], 2, (150, 158, 172))
for k in pos:
    d.ellipse([pos[k][0] * SS - 5, pos[k][1] * SS - 5, pos[k][0] * SS + 5, pos[k][1] * SS + 5],
              fill=(150, 158, 172))
T(d, (cx + 40, cy + 390), '11 个尽头，全部只有一条路 —— 走进去必须原路退出', 13, RED)

gx_r = cx + 470
pos2 = {(i, j): (gx_r + i * SP3, gy_l + j * SP3) for (i, j) in nodes}
T(d, (cx + 400, cy + 132), '复杂：同样的房间数，加 4 条回环', 15.5, GREEN, bold=True)
loops = [((0, -1), (0, 1)), ((1, 1), (2, 1)), ((3, -1), (3, 1)), ((1, -1), (1, 0))]
for (a, b) in edges_tree:
    seg(d, pos2[a], pos2[b], 16, CORR)
for (a, b) in loops:
    seg(d, pos2[a], pos2[b], 16, VCORR)
for (a, b) in edges_tree:
    seg(d, pos2[a], pos2[b], 2, (150, 158, 172))
for (a, b) in loops:
    seg(d, pos2[a], pos2[b], 2, GREEN)
for k in pos2:
    d.ellipse([pos2[k][0] * SS - 5, pos2[k][1] * SS - 5, pos2[k][0] * SS + 5, pos2[k][1] * SS + 5],
              fill=(150, 158, 172))
lm = pos2[(2, 0)]
d.ellipse([lm[0] * SS - 17, lm[1] * SS - 17, lm[0] * SS + 17, lm[1] * SS + 17],
          outline=ACC, width=int(3 * SS))
T(d, (lm[0], lm[1] + 28), '地标', 12.5, ACC, anchor='ma')
T(d, (cx + 400, cy + 390), '4 条回环 → 任何深处都有第二条路；环数 ≈ 房间数/12', 13, GREEN)

ty = cy + 470
T(d, (cx + 34, ty), '五种边界 —— 这才是「区域隔断」，也顺手做出信息通道', 15.5, INK, bold=True)
samples = [('实体墙', 'solid', '断视断弹'), ('玻璃隔断', 'glass', '断弹不断视'),
           ('金属格栅', 'grille', '断人不断视'), ('卷帘门', 'shutter', '可开合'),
           ('封锁带', 'tape', '不可通行但可看')]
for i, (nm, kind, note) in enumerate(samples):
    yy = ty + 40 + i * 36
    if kind == 'solid':
        seg(d, (cx + 40, yy), (cx + 150, yy), 7, INK)
    elif kind == 'glass':
        seg(d, (cx + 40, yy), (cx + 150, yy), 4, GLASS)
        seg(d, (cx + 40, yy - 4), (cx + 150, yy - 4), 1.2, (192, 212, 234))
    elif kind == 'grille':
        for k in range(0, 111, 9):
            seg(d, (cx + 40 + k, yy - 5), (cx + 40 + k, yy + 5), 1.6, (108, 116, 130))
        seg(d, (cx + 40, yy - 5), (cx + 150, yy - 5), 1.6, (108, 116, 130))
        seg(d, (cx + 40, yy + 5), (cx + 150, yy + 5), 1.6, (108, 116, 130))
    elif kind == 'shutter':
        for k in range(0, 111, 8):
            seg(d, (cx + 40 + k, yy - 5), (cx + 40 + k + 4, yy + 5), 1.7, (162, 132, 94))
    else:
        for k in range(0, 111, 17):
            seg(d, (cx + 40 + k, yy - 5), (cx + 40 + k + 9, yy + 5), 2.3, RED)
    T(d, (cx + 168, yy - 9), nm, 13.5, INK)
    T(d, (cx + 276, yy - 9), note, 12.5, SOFT)

ty2 = ty + 246
T(d, (cx + 34, ty2), '空旷区 vs 大厅：区别不是面积，是掩体', 15.5, INK, bold=True)
T(d, (cx + 34, ty2 + 32), '· 大厅 200–800㎡：高掩体多、视线 ≤45m —— 是「打起来」的地方', 13.5, INK)
T(d, (cx + 34, ty2 + 60), '· 空旷区 100–400㎡：低/无掩体、多入口 —— 是「跑过去」的地方', 13.5, INK)
T(d, (cx + 34, ty2 + 88), '· 空旷区里放高掩体 = 变成第二个大厅，节奏就平了', 13.5, INK)

bullets(d, cx + 30, cy + 830, [
    '· 硬约束：外环 1 + 内环 1 + 局部环 3–6；决策点间距 40–60m；每 4,000㎡ 一个地标',
    '· 视线 ≤45m，但每 30m 至少一个「能看出去」的开口（玻璃/格栅/高差）',
    '· 同层任意两个相邻区的层高 / 地面材质 / 主色，至少两项不同',
])
T(d, (cx + 30, cy + 960), '目标：走进去靠「那个大罐子 / 那段玻璃」定位，而不是靠数门', 15, GREEN, bold=True)
T(d, (cx + 30, cy + 1000), '复杂度来自层级（高度、材质、隔断类型、通视关系），不是来自岔路数量', 13.5, SOFT)

# ===================== D · 设施类型学 =====================
dx, dy = X2, Y2
panel(d, dx, dy, PW, PH, 'D · 设施类型学：同一台生成器，不同题材', '每层选 3–4 种设施做主题；房间尺寸天然分化，不用靠随机数制造差异')

cells = [('机房 / 服务器排', '25–60㎡', '2 门', 'rack'),
         ('冷库', '60–150㎡', '2 门 · 1 卷帘', 'cold'),
         ('管廊 / 检修夹层', '2.4–3.6m 宽', '两端开口', 'duct'),
         ('车间 / 装配线', '200–600㎡', '2–3 门', 'shop'),
         ('中控室 / 调度台', '80–160㎡', '2 门', 'ctrl'),
         ('变电 / 水泵房', '40–120㎡', '2 门', 'power'),
         ('储罐 / 气瓶间', '100–300㎡', '2 门', 'tank'),
         ('医疗隔离区', '60–120㎡', '2 门 · 1 气闸', 'med'),
         ('装卸 / 货架区', '300–900㎡', '3+ 门', 'load'),
         ('休息 / 生活舱', '30–90㎡', '2 门', 'rest'),
         ('档案 / 备份库', '100–400㎡', '2 门', 'rack'),
         ('实验 / 洁净间', '60–120㎡', '2 门 · 1 风淋', 'med')]
CW, CH, GUT = 172, 156, 14
gx0, gy0 = dx + 32, dy + 128
for i, (nm, area, doors, kind) in enumerate(cells):
    col, row = i % 4, i // 4
    x = gx0 + col * (CW + GUT)
    y = gy0 + row * (CH + 58)
    bx0, by0 = x, y
    bw, bh = CW - 8, CH - 50
    rect(d, bx0, by0, bw, bh, fill=(250, 251, 253), outline=(204, 210, 220), ow=2, rad=4)
    if kind == 'rack':
        for k in range(1, 5):
            yy = by0 + 8 + k * (bh - 16) / 5.0
            seg(d, (bx0 + 8, yy), (bx0 + bw - 26, yy), 2.6, (172, 180, 195))
        for k in range(1, 3):
            xx = bx0 + 8 + k * (bw - 34) / 3.0
            seg(d, (xx, by0 + 8), (xx, by0 + bh - 8), 1.6, (196, 204, 216))
    elif kind == 'cold':
        for k in range(0, bw - 6, 9):
            seg(d, (bx0 + k, by0 + 7), (bx0 + k, by0 + bh - 7), 1.2, (206, 226, 232))
        seg(d, (bx0 + bw, by0 + bh * 0.34), (bx0 + bw, by0 + bh * 0.66), 4, ACC)
    elif kind == 'duct':
        seg(d, (bx0, by0 + bh * 0.42), (bx0 + bw, by0 + bh * 0.42), 6, INK)
        seg(d, (bx0, by0 + bh * 0.62), (bx0 + bw, by0 + bh * 0.62), 6, INK)
        for k in range(4, int(bw) - 8, 20):
            seg(d, (bx0 + k, by0 + bh * 0.42), (bx0 + k, by0 + bh * 0.62), 1.3, (172, 180, 195))
    elif kind == 'shop':
        for k in range(2):
            rect(d, bx0 + 12 + k * 46, by0 + 12, 30, 20, fill=(226, 231, 239), outline=(196, 202, 214), ow=1.5)
            rect(d, bx0 + 12 + k * 46, by0 + bh - 34, 30, 20, fill=(226, 231, 239), outline=(196, 202, 214), ow=1.5)
        seg(d, (bx0 + 8, by0 + bh * 0.5), (bx0 + bw - 8, by0 + bh * 0.5), 3, (176, 184, 198))
    elif kind == 'ctrl':
        seg(d, (bx0 + bw * 0.6, by0), (bx0 + bw * 0.6, by0 + bh), 3.5, GLASS)
        for k in range(3):
            rect(d, bx0 + 8 + k * 22, by0 + 10, 17, 9, fill=(214, 222, 234), outline=(190, 198, 212), ow=1)
    elif kind == 'power':
        for k in range(2):
            rect(d, bx0 + 10 + k * 44, by0 + 12, 34, 26, fill=(232, 236, 243), outline=(188, 195, 208), ow=1.5)
        seg(d, (bx0 + 12, by0 + bh - 22), (bx0 + bw - 12, by0 + bh - 22), 2, (176, 184, 198))
    elif kind == 'tank':
        for k in range(3):
            cxx, cyy = bx0 + 24 + k * 30, by0 + bh * 0.5
            d.ellipse([(cxx - 11) * SS, (cyy - 11) * SS, (cxx + 11) * SS, (cyy + 11) * SS],
                      fill=(236, 233, 227), outline=(176, 170, 160), width=int(1.6 * SS))
    elif kind == 'med':
        rect(d, bx0 + 10, by0 + bh * 0.32, 24, bh * 0.36, fill=(238, 244, 240), outline=(146, 188, 162), ow=1.6)
        seg(d, (bx0 + 34, by0 + bh * 0.32), (bx0 + 34, by0 + bh * 0.68), 3, ACC)
        for k in range(2):
            rect(d, bx0 + 56 + k * 30, by0 + 12, 21, 18, fill=(236, 240, 246), outline=(194, 200, 213), ow=1)
    elif kind == 'load':
        for k in range(4):
            seg(d, (bx0 + 8, by0 + 12 + k * 14), (bx0 + bw - 34, by0 + 12 + k * 14), 4, (200, 194, 182))
        for k in range(3):
            seg(d, (bx0 + bw - 14, by0 + 10 + k * 16), (bx0 + bw - 14, by0 + 22 + k * 16), 3.5, ACC)
    else:
        for k in range(3):
            rect(d, bx0 + 8 + k * 24, by0 + 12 + (k % 2) * 22, 18, 16, fill=(240, 238, 244), outline=(200, 196, 214), ow=1.4)
        rect(d, bx0 + 8, by0 + bh - 30, bw - 20, 18, fill=(244, 240, 236), outline=(202, 196, 188), ow=1.2)
    T(d, (bx0, y + CH - 44), nm, 13, INK, bold=True)
    T(d, (bx0, y + CH - 24), area + ' · ' + doors, 11, SOFT)

bullets(d, dx + 30, dy + 790, [
    '· 每层选 3–4 种设施当主题，其余用「通用办公 / 生活」填充 —— 这是「楼层身份」的来源',
    '· 设施自带尺寸分布：机房密、车间大、管廊窄、储罐空 —— 尺度差异不用随机数造',
    '· 同一种设施在不同层比例不同 → 楼层可辨识但不重复，也不会像迷宫一样无规律',
])
T(d, (dx + 30, dy + 910), '换题材只换这张表：', 15, INK, bold=True)
themes = [('医院楼', '隔离区 · 手术室 · 药房 · 太平间 · 设备层'),
          ('实验楼', '超净间 · 冷库 · 动物房 · 试剂库 · 气瓶间'),
          ('数据楼', '机房 · 配电 · 冷却塔 · 光缆竖井 · 备份库'),
          ('行政楼', '开放办公 · 会议 · 档案 · 茶水间 · 机房')]
for i, (nm, lst) in enumerate(themes):
    yy = dy + 944 + i * 32
    T(d, (dx + 34, yy), '· ' + nm, 13.5, BLUE, bold=True)
    T(d, (dx + 130, yy), lst, 13, SOFT)

out = 'D:/workspace/ESCAPE FROM WORK/tools/plan-proto/out/design-sketches.png'
sheet.save(out)
print('saved', out, sheet.size)
