# -*- coding: utf-8 -*-
"""按调研规则体检生成器 v3：走廊骨架像素图（欧拉数算环）、死路走廊、视线、门数"""
import math
import numpy as np
import plan_proto as pp

CELL = pp.CELL
NB = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]

def thin(mask, cap=80):
    img = mask.astype(np.uint8).copy()
    for _ in range(cap):
        changed = False
        for step in (0, 1):
            p = np.pad(img, 1, mode='constant')
            P2 = p[0:-2, 1:-1]; P3 = p[0:-2, 2:]; P4 = p[1:-1, 2:]
            P5 = p[2:, 2:]; P6 = p[2:, 1:-1]; P7 = p[2:, 0:-2]
            P8 = p[1:-1, 0:-2]; P9 = p[0:-2, 0:-2]
            B = P2 + P3 + P4 + P5 + P6 + P7 + P8 + P9
            seq = [P2, P3, P4, P5, P6, P7, P8, P9, P2]
            A = sum(((seq[i] == 0) & (seq[i + 1] == 1)).astype(np.uint8) for i in range(8))
            if step == 0:
                cond = (P2 * P4 * P6 == 0) & (P4 * P6 * P8 == 0)
            else:
                cond = (P2 * P4 * P8 == 0) & (P2 * P6 * P8 == 0)
            rm = (img == 1) & (B >= 2) & (B <= 6) & (A == 1) & cond
            if rm.any():
                img[rm] = 0
                changed = True
        if not changed:
            break
    return img.astype(bool)

def degs(pts):
    d = {}
    for p in pts:
        d[p] = sum(1 for dj, di in NB if (p[0] + dj, p[1] + di) in pts)
    return d

def comps(pts):
    seen = set(); c = 0
    for p in pts:
        if p in seen: continue
        c += 1; stack = [p]; seen.add(p)
        while stack:
            q = stack.pop()
            for dj, di in NB:
                r = (q[0] + dj, q[1] + di)
                if r in pts and r not in seen:
                    seen.add(r); stack.append(r)
    return c

def dead_end_len(start, pts, deg):
    """从端点走，返回 (长度m, 到分叉点像素)"""
    prev, cur, L = None, start, 0
    while True:
        nb = [(cur[0] + dj, cur[1] + di) for dj, di in NB]
        nb = [q for q in nb if q in pts and q != prev]
        if len(nb) != 1:
            return L * CELL, cur
        prev, cur = cur, nb[0]
        L += 1
        if deg[cur] >= 3:
            return L * CELL, cur

def audit(fl, name):
    rooms = fl['rooms']
    corr = fl['corridor_mask'].copy()
    skel = thin(corr)
    pts = set(map(tuple, np.argwhere(skel)))
    deg = degs(pts)
    V = len(pts)
    E = sum(deg.values()) // 2
    C = comps(pts) if pts else 0
    loops = E - V + C
    # 房间挂到最近骨架像素
    plist = list(pts)
    attached = 0; dcount = []
    for ri, room in enumerate(rooms):
        poly = room['poly']
        cx = sum(p[0] for p in poly) / len(poly); cy = sum(p[1] for p in poly) / len(poly)
        ds = room.get('doors', [])
        dcount.append(len(ds))
        ok = False
        for (ei, tmid, wd) in ds:
            ax, ay = poly[ei]; bx, by = poly[(ei + 1) % len(poly)]
            ex, ey = bx - ax, by - ay
            L = math.hypot(ex, ey)
            if L < 1e-6: continue
            ux, uy = ex / L, ey / L
            nx, ny = -uy, ux
            mx, my = (ax + bx) * 0.5, (ay + by) * 0.5
            if (mx - cx) * nx + (my - cy) * ny < 0: nx, ny = -nx, -ny
            qx, qy = ax + ux * tmid + nx * 0.8, ay + uy * tmid + ny * 0.8
            dj = int(qy // CELL); di = int(qx // CELL)
            hit = False
            for r in range(0, 7):
                for k in range(-r, r + 1):
                    for cand in ((dj + k, di + r - abs(k)), (dj + k, di - r + abs(k))):
                        if cand in pts:
                            hit = True; break
                    if hit: break
                if hit: break
            if hit:
                ok = True; break
        if ok: attached += 1
    ends = [p for p in pts if deg[p] == 1]
    deads = []
    for e in ends:
        L, j = dead_end_len(e, pts, deg)
        if L > 6.0:
            deads.append(L)
    best = 0
    for m in (corr, corr.T):
        for row in m:
            run = 0
            for v in row:
                run = run + 1 if v else 0
                if run > best: best = run
    areas = [abs(pp.poly_area(r['poly'])) for r in rooms]
    nr = len(rooms)
    return dict(name=name, rooms=nr, loops=loops, V=V, E=E, C=C,
                endpoints=len(ends), deadcorr=len(deads), maxdead=(max(deads) if deads else 0),
                single=sum(1 for d in dcount if d <= 1) / nr,
                unreach=nr - attached, straight=best * CELL,
                lt56=sum(1 for a in areas if a < 56) / nr,
                med=float(np.median(areas)), tot=sum(areas))

if __name__ == '__main__':
    print('%-10s %5s %5s %6s %6s %6s %7s %7s %6s %6s %6s' % (
        'floor', 'rooms', 'loop', 'V', 'C', 'deadEnd', 'deadCorr', 'sight', 'single', 'unreach', 'med'))
    for cfg in list(pp.floor_programs()) + [pp.baseline_program()]:
        fl = pp.make_floor(cfg)
        st = audit(fl, cfg.get('name', 'base')[:9])
        print('%-10s %5d %5d %6d %6d %6d %6d %6.0fm %5.0f%% %6d %6.0f' % (
            st['name'], st['rooms'], st['loops'], st['V'], st['C'], st['endpoints'],
            st['deadcorr'], st['straight'], st['single'] * 100, st['unreach'], st['med']))
