from PIL import Image, ImageDraw, ImageFont

SRC = "docs/3d-rebuild/screenshots/"
OUT = "docs/3d-rebuild/screenshots/m1-corridor-compare.png"

def font(sz, bold=False):
    return ImageFont.truetype("C:/Windows/Fonts/msyhbd.ttc" if bold else "C:/Windows/Fonts/msyh.ttc", sz)

f_title = font(26, True)
f_head  = font(20, True)
f_body  = font(14)
f_small = font(12)

# 裁一块包含核心筒 + 主走廊 + 二三级走廊的区域（米 -> 像素，900x720 覆盖 200x160m）
X0, X1 = 52, 152   # m
Y0, Y1 = 32, 92    # m
S = 900 / 200.0
crop = (int(X0*S), int(Y0*S), int(X1*S), int(Y1*S))

panels = [
    ("A", "两级：主 4.8m + 次 3.6m", "121 条 / 3527m / 327 房", "层A", "长视线、穿办公室多、几何最省"),
    ("B", "三级：4.8 / 3.6 / 2.7m（2.7 起于 190㎡ 分区）", "223 条 / 4943m / 355 房", "层B", "侧翼路线多、走廊都能摆掩体"),
    ("C", "三级收窄：4.8 / 3.6 / 2.7m（2.7 起于 260㎡ 分区）", "170 条 / 4224m / 327 房", "层C", "细走廊只在整片办公区出现"),
]

scale = 2
w = (crop[2] - crop[0]) * scale
h = (crop[3] - crop[1]) * scale
head_h = 62
pad = 18
W = w + pad * 2
H = 92 + (h + head_h + pad) * 3 + 60

img = Image.new("RGB", (W, H), (13, 16, 21))
d = ImageDraw.Draw(img)
d.text((pad, 22), "走廊分级对比 · 第 1 层（200×160m，开放办公）", font=f_title, fill=(232, 238, 246))
d.text((pad, 58), "同一区域裁切放大 2×：灰=房间，亮=主走廊 4.8m，中=次走廊 3.6m，暗=三级走廊 2.7m，黄点=门，青点=套间/环路门", font=f_small, fill=(140, 152, 168))

y = 92
for tag, sub, stats, name, note in panels:
    src = Image.open(SRC + "m1-corridor-" + tag + ".png").convert("RGB").crop(crop)
    src = src.resize((w, h), Image.NEAREST)
    d.text((pad, y), tag + " · " + name, font=f_head, fill=(232, 238, 246))
    d.text((pad + 150, y + 4), sub, font=f_body, fill=(170, 182, 200))
    d.text((pad + 150, y + 26), stats, font=f_body, fill=(110, 200, 190))
    d.text((pad + 150, y + 46), note, font=f_small, fill=(130, 142, 158))
    img.paste(src, (pad, y + head_h))
    d.rectangle([pad - 1, y + head_h - 1, pad + w, y + head_h + h], outline=(48, 58, 72))
    y += head_h + h + pad

img.save(OUT)
print("saved", OUT, img.size)
