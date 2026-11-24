
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import random, math

random.seed(11)
S = 5; FW, FH = 200, 160
W, H = FW*S, FH*S
M = 46; TOP = 58
img = Image.new("RGB", (W+2*M, H+2*M+TOP), (11, 13, 16))
d = ImageDraw.Draw(img, "RGBA")

def X(x): return M + x*S
def Y(y): return TOP + M + (FH-y)*S
def crop(a, b):
    return (X(min(a[0], b[0])), Y(max(a[1], b[1])), X(max(a[0], b[0])), Y(min(a[1], b[1])))

def font(sz, bold=False):
    p = "C:/Windows/Fonts/msyhbd.ttc" if bold else "C:/Windows/Fonts/msyh.ttc"
    try: return ImageFont.truetype(p, sz)
    except Exception: return ImageFont.load_default()

f_big = font(23, True); f_mid = font(15, True); f_sm = font(13); f_tiny = font(10); f_zone = font(16, True)

CORR_H = (0, 78.2, 200, 81.8)
CORR_V = (98.2, 0, 101.8, 160)
CORE = (92, 128, 108, 140)
ST_W = (4, 70, 12, 90)
ST_E = (188, 70, 196, 90)
obstacles = [CORR_H, CORR_V, CORE, ST_W, ST_E]

def hits(a, b):
    return not (a[2] <= b[0] or b[2] <= a[0] or a[3] <= b[1] or b[3] <= a[1])

cells = []
x = 0
while x + 20 <= FW:
    y = 0
    while y + 20 <= FH:
        r = (x+1.2, y+1.2, x+18.8, y+18.8)
        if not any(hits(r, o) for o in obstacles):
            cells.append([x, y])
        y += 20
    x += 20

rooms = []
used = set()
for (cx, cy) in cells:
    if random.random() < 0.45 and (cx+20, cy) in cells and (cx+20, cy) not in used and (cx, cy) not in used:
        rooms.append([cx+1.2, cy+1.2, cx+38.8, cy+18.8]); used.add((cx, cy)); used.add((cx+20, cy))
for (cx, cy) in cells:
    if (cx, cy) in used: continue
    rooms.append([cx+1.2, cy+1.2, cx+18.8, cy+18.8])

def zone_of(r):
    cx = (r[0]+r[2])/2; cy = (r[1]+r[3])/2
    if cy > 105: return "会议室区"
    if cx > 120: return "行政办公"
    if cx < 60: return "研发部"
    return "综合办公"

ZONE_COL = {"会议室区": (150, 160, 96), "行政办公": (104, 148, 176), "研发部": (158, 128, 174), "综合办公": (130, 140, 150)}

path = [(20, 18), (20, 80), (100, 80), (100, 126)]
def dist_to_path(px, py):
    best = 1e9
    for i in range(len(path)-1):
        ax, ay = path[i]; bx, by = path[i+1]
        dx, dy = bx-ax, by-ay
        L2 = dx*dx + dy*dy
        t = 0 if L2 == 0 else max(0, min(1, ((px-ax)*dx + (py-ay)*dy)/L2))
        best = min(best, math.hypot(px-(ax+t*dx), py-(ay+t*dy)))
    return best

def explored(r):
    return dist_to_path((r[0]+r[2])/2, (r[1]+r[3])/2) <= 27

# base
d.rectangle(crop((0, 0), (FW, FH)), fill=(17, 21, 27, 255))

def door_gap(r, side):
    x0, y0, x1, y1 = r
    w = 2.4
    cx = (x0+x1)/2; cy = (y0+y1)/2
    if side == "S": return (cx-w/2, y0), (cx+w/2, y0)
    if side == "N": return (cx-w/2, y1), (cx+w/2, y1)
    if side == "W": return (x0, cy-w/2), (x0, cy+w/2)
    return (x1, cy-w/2), (x1, cy+w/2)

def draw_room_detail(r):
    x0, y0, x1, y1 = r
    z = zone_of(r)
    base = ZONE_COL[z]
    fill = (int(base[0]*0.30+16), int(base[1]*0.30+19), int(base[2]*0.30+24))
    d.rectangle(crop((x0, y0), (x1, y1)), fill=fill + (255,))
    # choose door on side nearest corridor
    cand = [("S", y0), ("N", y1), ("W", x0), ("E", x1)]
    def dd(side):
        px, py = door_gap(r, side)[0]
        return dist_to_path(px, py)
    side = min(cand, key=lambda c: dd(c[0]))[0]
    # walls with door gap
    d.rectangle(crop((x0, y0), (x1, y1)), outline=(206, 214, 224, 255), width=2)
    p, q = door_gap(r, side)
    d.rectangle((min(X(p[0]), X(q[0])) - 1, min(Y(p[1]), Y(q[1])) - 1,
                 max(X(p[0]), X(q[0])) + 1, max(Y(p[1]), Y(q[1])) + 1), fill=fill + (255,))
    # door leaf
    mx, my = ((p[0]+q[0])/2, (p[1]+q[1])/2)
    d.ellipse((X(mx)-3, Y(my)-3, X(mx)+3, Y(my)+3), fill=(250, 214, 120, 255))
    # cubicles
    step = 2.2
    yy = y0 + 1.6
    while yy < y1 - 1.6:
        d.line((X(x0+1.0), Y(yy), X(x1-1.0), Y(yy)), fill=(120, 130, 143, 130), width=1)
        xx = x0 + 1.0
        while xx < x1 - 1.0:
            d.rectangle(crop((xx, yy-0.6), (xx+1.5, yy+1.1)), outline=(150, 160, 172, 90))
            xx += 2.0
        yy += step

for r in rooms:
    if explored(r):
        draw_room_detail(r)

# corridors explored part
seg = 4
y0 = 0
while y0 < FH:
    if dist_to_path(100, y0+seg/2) <= 6:
        d.rectangle(crop((98.2, y0), (101.8, y0+seg)), fill=(58, 64, 73, 255))
    y0 += seg
x0 = 0
while x0 < FW:
    if dist_to_path(x0+seg/2, 80) <= 6:
        d.rectangle(crop((x0, 78.2), (x0+seg, 81.8)), fill=(58, 64, 73, 255))
    x0 += seg
# corridor edges only where explored
d.line((X(20), Y(78.2), X(100), Y(78.2)), fill=(190, 198, 208, 220), width=2)
d.line((X(20), Y(81.8), X(100), Y(81.8)), fill=(190, 198, 208, 220), width=2)
d.line((X(98.2), Y(80), X(98.2), Y(126), ), fill=(190, 198, 208, 220), width=2)
d.line((X(101.8), Y(80), X(101.8), Y(126)), fill=(190, 198, 208, 220), width=2)

# anchors
d.rectangle(crop((CORE[0], CORE[1]), (CORE[2], CORE[3])), fill=(44, 50, 59, 255), outline=(214, 222, 232, 200), width=2)
d.text((X(100), Y(134)), "核心筒", font=f_tiny, fill=(190, 200, 212), anchor="mm")
for st in (ST_W, ST_E):
    d.rectangle(crop((st[0], st[1]), (st[2], st[3])), fill=(28, 33, 40, 255), outline=(214, 222, 232, 180), width=2)

# unexplored: one zone label per function area
for z in ["会议室区", "研发部", "行政办公", "综合办公"]:
    cands = [((r[0]+r[2])/2, (r[1]+r[3])/2) for r in rooms if not explored(r) and zone_of(r) == z]
    cands = [c for c in cands if dist_to_path(c[0], c[1]) > 45 and dist_to_path(c[0], c[1] + 0) > 0]
    if not cands: continue
    mx = sum(c[0] for c in cands)/len(cands)
    my = sum(c[1] for c in cands)/len(cands)
    best = min(cands, key=lambda c: (c[0]-mx)**2 + (c[1]-my)**2)
    bx, by = best
    if abs(bx-100) < 22 and abs(by-124) < 16: by += 14
    d.text((X(bx), Y(by)), z, font=f_zone, fill=(116, 126, 140), anchor="mm")

d.rectangle(crop((0, 0), (FW, FH)), outline=(226, 234, 244, 120), width=2)

# spawn
spx, spy = 20, 18
d.ellipse((X(spx)-7, Y(spy)-7, X(spx)+7, Y(spy)+7), fill=(74, 190, 130, 255), outline=(235, 255, 245, 220))
d.text((X(spx)+14, Y(spy)-3), "出生区 A", font=f_mid, fill=(150, 240, 195))

def extraction(px, py, lab, sub, color):
    cx, cy = X(px), Y(py); r = 10
    d.polygon([(cx, cy-r), (cx+r, cy), (cx, cy+r), (cx-r, cy)], fill=color+(255,), outline=(255, 255, 255, 235))
    d.text((cx, cy+22), lab, font=f_mid, fill=color, anchor="mm")
    d.text((cx, cy+40), sub, font=f_tiny, fill=(195, 202, 212), anchor="mm")

extraction(192, 92, "撤离 · 消防梯", "20s", (232, 168, 74))
extraction(100, 124, "撤离 · 电梯", "10s · 需供电", (96, 200, 224))

# header
d.text((M, 18), "本层 32,000㎡ · 已探索 38%", font=f_big, fill=(238, 244, 250))
d.text((M, 45), "M 开图 · 开图不暂停 · 撤离点常显", font=f_tiny, fill=(140, 150, 162))

lx = W + 2*M - 206
d.rectangle((lx, 12, lx+196, 132), fill=(16, 20, 25, 215), outline=(70, 80, 92, 200))
rows = [("已探索：墙/隔间/门", (206, 214, 224)), ("未探索：只给区域名", (120, 130, 144)),
        ("撤离点", (232, 168, 74)), ("出生区", (74, 190, 130))]
for i, (name, c) in enumerate(rows):
    yy = 26 + i*26
    d.rectangle((lx+12, yy, lx+30, yy+11), fill=c+(255,))
    d.text((lx+38, yy-2), name, font=f_sm, fill=(212, 220, 230))

d.text((M, TOP+M+H+16), "已探索=真实平面（隔间/门/走廊）· 未探索=功能区名称 · 不显示战利品/敌人/其他队伍", font=f_sm, fill=(120, 130, 142))
img.save("docs/3d-rebuild/screenshots/m1-map-mock.png")
print("ok", img.size)
