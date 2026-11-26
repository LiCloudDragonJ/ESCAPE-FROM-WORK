# -*- coding: utf-8 -*-
"""A/B：同种子同地标，一套语法铺满整层 vs 分区各用一套语法"""
import os
import plan_proto as pp
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out')

base = dict(seed=7071, W=200, H=160, gestures=['spine', 'hall'], spine_deg=19,
            vcuts=[0.31, 0.70], hcuts=[0.38],
            hall_deg=45, hall_size=(52, 32), hall_label='数据大厅', min_accept=70)

one = dict(base, name='A · 一套语法铺满整层',
           sub='整层只有阵列网格一套语法：房间尺度统一，读起来就是格子间',
           grammars=pp.gl(['阵列']), max_room=380)
zoned = dict(base, name='B · 分区各用一套语法',
             sub='阵列 / 斜网 / 管廊 / 大跨 四套语法分区落地，界面都对齐在走廊上',
             grammars=pp.gl(['阵列', '斜网', '管廊', '大跨']), max_room=520)

floors = [pp.make_floor(one), pp.make_floor(zoned)]
scale = 3.0
pad = 24
plans = [pp.render_plan(fl, scale=scale, pad=pad, ss=2) for fl in floors]
for i, fl in enumerate(floors):
    pp.decorate(plans[i], fl, scale, pad)
pw, ph = plans[0].size
Wt = 52 * 2 + pw * 2 + 44
Ht = 150 + ph + 190
img = Image.new('RGB', (Wt, Ht), (247, 248, 252))
d = ImageDraw.Draw(img)
d.text((52, 34), '楼层为什么看起来一样：一套语法铺满 vs 分区多语法', font=pp.font(34, True), fill=pp.INK)
d.text((52, 84), '同种子、同外廓、同地标、同主脊；唯一变量是「分区是否各用一套空间语法」',
       font=pp.font(17), fill=pp.INK_SOFT)
for i, fl in enumerate(floors):
    x = 52 + i * (pw + 44)
    img.paste(plans[i], (x, 128))
    d.text((x, 128 + ph + 8), fl['name'], font=pp.font(20, True), fill=pp.INK)
    d.text((x, 128 + ph + 38), fl['sub'], font=pp.font(14), fill=pp.INK_SOFT)
    st = pp.stats(fl)
    d.text((x, 128 + ph + 64), pp.stats_line(st), font=pp.font(15, True), fill=(150, 92, 40))
    d.text((x, 128 + ph + 92), '走廊占比 %d%% · 分区 %d 个' % (round(st['corridor'] * 100), len(fl.get('zones', []))),
           font=pp.font(14), fill=pp.INK_SOFT)
img.save(os.path.join(OUT, 'plan-proto-zoning.png'))
print('zoning sheet', img.size)
for fl in floors:
    st = pp.stats(fl)
    print(fl['name'], st['count'], st['kinds'], 'nonrect=%.0f%%' % (st['nonrect'] * 100),
          'corr=%.1f%%' % (st['corridor'] * 100), 'zones=%d' % len(fl.get('zones', [])))
