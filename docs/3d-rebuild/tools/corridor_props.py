from PIL import Image, ImageDraw, ImageFont

def font(sz, bold=False):
    return ImageFont.truetype("C:/Windows/Fonts/msyhbd.ttc" if bold else "C:/Windows/Fonts/msyh.ttc", sz)

S = 36.0
LEN_M = 20.0
WALL = 0.2
PADX = 250
TOP = 118
GAP = 62

f_t = font(24, True); f_h = font(16, True); f_s = font(11); f_xs = font(9); f_b = font(13)

rows = [
    dict(w=4.8, top=0.9, bot=0.9, title="主走廊 4.8m", sub="双侧掩体带各 0.9m",
         note="净宽 3.0m：并排两人 + 走位 + 蹲掩体后换位",
         top_items=[(1.4,1.2,0.45,"档案柜"),(4.2,0.9,0.9,"纸箱堆"),(7.4,1.4,0.7,"办公桌"),
                    (12.6,0.9,0.9,"纸箱堆"),(15.0,0.5,0.5,"椅"),(17.0,1.2,0.45,"矮柜")],
         bot_items=[(1.8,1.2,0.45,"矮柜"),(4.6,0.9,0.9,"纸箱堆"),(8.0,1.4,0.7,"办公桌"),
                    (12.2,1.2,0.45,"档案柜"),(14.8,0.5,0.5,"饮水机"),(16.8,0.9,0.9,"纸箱堆")]),
    dict(w=3.6, top=0.0, bot=0.9, title="次走廊 3.6m", sub="单侧掩体带 0.9m",
         note="净宽 2.7m：一侧有掩护，另一侧敞开",
         top_items=[],
         bot_items=[(1.6,1.2,0.45,"档案柜"),(4.4,0.9,0.9,"纸箱堆"),(7.8,1.4,0.7,"办公桌"),
                    (12.4,0.9,0.9,"纸箱堆"),(15.2,1.2,0.45,"矮柜"),(17.4,0.5,0.5,"椅")]),
    dict(w=2.7, top=0.0, bot=0.3, title="三级走廊 2.7m", sub="只放 0.3m 浅物",
         note="净宽 2.4m：刚好一人侧身让过，不放大件",
         top_items=[],
         bot_items=[(2.0,1.0,0.3,"灭火器"),(5.2,1.0,0.3,"窄柜"),(8.2,0.5,0.3,"小箱"),
                    (11.4,1.0,0.3,"消防栓"),(14.4,1.0,0.3,"窄柜"),(17.0,0.5,0.3,"小箱")]),
]

W = int(PADX + LEN_M*S + 300)
H = int(TOP + sum((r['w']+2*WALL)*S for r in rows) + GAP*len(rows) + 54)
img = Image.new("RGB", (W, H), (13,16,21))
d = ImageDraw.Draw(img)

d.text((18, 18), "走廊宽度与掩体（横断面，俯视，1m ≈ 36px，20m 长）", font=f_t, fill=(232,238,246))
d.text((18, 58), "深灰=墙 0.2m（在走廊净宽之外）｜灰蓝=掩体带｜深蓝=通行净宽｜青色斜纹=门洞 1.2m 净空，不摆家具", font=f_s, fill=(140,152,168))

WALL_C = (56,62,72); WALL_E = (104,112,126)
BAND_C = (38,50,64); LANE_C = (22,42,56); LANE_L = (72,160,206)
PROP_C = (88,100,118); PROP_E = (170,184,206)

def prop(x, y, w, h, label):
    d.rectangle([x, y, x+w, y+h], fill=PROP_C, outline=PROP_E)
    if h >= 13 and w >= d.textlength(label, font=f_xs) + 4:
        d.text((x+w/2, y+h/2), label, font=f_xs, fill=(18,22,28), anchor="mm")

y = TOP
for r in rows:
    wm = r['w']; h = wm*S; x0 = PADX
    clear = wm - r['top'] - r['bot']
    # 墙：净宽外侧
    d.rectangle([x0-WALL*S, y, x0+LEN_M*S+WALL*S, y+h], fill=WALL_C, outline=WALL_E)
    # 走廊净空
    d.rectangle([x0, y, x0+LEN_M*S, y+h], fill=BAND_C)
    lane_y0 = y + r['top']*S
    d.rectangle([x0, lane_y0, x0+LEN_M*S, lane_y0+clear*S], fill=LANE_C)
    d.line([x0, lane_y0, x0+LEN_M*S, lane_y0], fill=LANE_L, width=2)
    d.line([x0, lane_y0+clear*S, x0+LEN_M*S, lane_y0+clear*S], fill=LANE_L, width=2)

    # 门洞净空
    dx0, dx1 = x0 + 9.2*S, x0 + 10.8*S
    for xx in range(int(dx0), int(dx1), 8):
        d.line([xx, y, xx+8, y+10], fill=(64,150,150), width=1)
        d.line([xx, y+h-10, xx+8, y+h], fill=(64,150,150), width=1)

    d.text((x0+LEN_M*S+WALL*S+16, lane_y0+clear*S/2-8), "净宽 %.1fm" % clear, font=f_b, fill=(110,200,238), anchor="lm")
    d.text((x0+LEN_M*S+WALL*S+16, lane_y0+clear*S/2+14), "走廊 %.1fm + 墙 0.2m" % wm, font=f_xs, fill=(120,132,148), anchor="lm")

    d.text((20, y+h/2-28), r['title'], font=f_h, fill=(232,238,246))
    d.text((20, y+h/2-6), r['sub'], font=f_s, fill=(150,240,195))
    d.text((20, y+h/2+14), r['note'], font=f_s, fill=(140,152,168))

    m = lambda v: x0 + v*S
    for off, pw, dep, label in r['top_items']:
        dep = min(dep, r['top'])
        prop(m(off), y+2, pw*S, dep*S-4, label)
    for off, pw, dep, label in r['bot_items']:
        dep = min(dep, r['bot'])
        prop(m(off), y+h-dep*S+2, pw*S, dep*S-4, label)
    y += h + 2*WALL*S + GAP

d.text((18, H-38), "门洞两侧 1.2m 内不放家具；同一段走廊连续掩体不超过 3 组（约 6–8m 一组），留出可冲刺直道", font=f_s, fill=(130,142,158))
img.save("docs/3d-rebuild/screenshots/m1-corridor-props.png")
print("ok", img.size)
