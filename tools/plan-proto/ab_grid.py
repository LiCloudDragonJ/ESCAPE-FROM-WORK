# -*- coding: utf-8 -*-
"""A/B：同种子，正交柱网 vs 倾斜柱网"""
import os, sys
import plan_proto as pp
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')

base = dict(name='A · 正交柱网（墙全横平竖直）', sub='柱网与世界轴对齐，只有主脊和地标是斜的',
            seed=7071, W=200, H=160, gestures=['spine', 'hall'], spine_deg=19,
            vcuts=[0.31, 0.70], hcuts=[0.38],
            hall_deg=45, hall_size=(52, 32), hall_label='数据大厅',
            max_room=520, min_accept=70)

tilt = dict(base)
tilt = dict(base, name='B · 倾斜柱网（整体转 14°）',
            sub='柱网整体倾斜 14°：房间、走廊全部跟着斜，外廓仍是方的',
            grid_deg=14.0)

floors = [pp.make_floor(base), pp.make_floor(tilt)]
scale = 3.0
pad = 24
plans = [pp.render_plan(fl, scale=scale, pad=pad, ss=2) for fl in floors]
pw, ph = plans[0].size
Wt = 52 * 2 + pw * 2 + 44
Ht = 150 + ph + 190
img = Image.new('RGB', (Wt, Ht), (247, 248, 252))
d = ImageDraw.Draw(img)
d.text((52, 34), '柱网方向：墙到底该不该横平竖直', font=pp.font(34, True), fill=pp.INK)
d.text((52, 84), '同种子同节目单；右图只是把柱网整体转了 14°，房间/走廊/门全部跟着转，外廓（甲方给的楼板）仍是方的',
       font=pp.font(17), fill=pp.INK_SOFT)
for i, fl in enumerate(floors):
    x = 52 + i * (pw + 44)
    img.paste(plans[i], (x, 128))
    d.text((x, 128 + ph + 8), fl['name'], font=pp.font(20, True), fill=pp.INK)
    d.text((x, 128 + ph + 38), fl['sub'], font=pp.font(14), fill=pp.INK_SOFT)
    st = pp.stats(fl)
    d.text((x, 128 + ph + 64), pp.stats_line(st), font=pp.font(15, True), fill=(150, 92, 40))
    d.text((x, 128 + ph + 92), '走廊占比 %d%%' % round(st['corridor'] * 100),
           font=pp.font(14), fill=pp.INK_SOFT)
img.save(os.path.join(OUT, 'plan-proto-tilt.png'))
print('tilt sheet', img.size)
for fl in floors:
    st = pp.stats(fl)
    print(fl['name'], st['count'], st['kinds'], 'nonrect=%.0f%%' % (st['nonrect'] * 100),
          'corr=%.1f%%' % (st['corridor'] * 100))
