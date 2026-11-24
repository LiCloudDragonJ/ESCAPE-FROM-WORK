from PIL import Image, ImageDraw, ImageFont

def font(sz, bold=False):
    return ImageFont.truetype("C:/Windows/Fonts/msyhbd.ttc" if bold else "C:/Windows/Fonts/msyh.ttc", sz)

src = Image.open("docs/3d-rebuild/screenshots/m1-floor-hires.png").convert("RGB")
# 2400x1920 覆盖 200x160m -> 12px/m
S = 12.0
x0, y0, x1, y1 = 38, 46, 118, 106
c = src.crop((int(x0*S), int(y0*S), int(x1*S), int(y1*S)))
scale = 2
c = c.resize((c.width*scale, c.height*scale), Image.NEAREST)

W, H = c.width + 40, c.height + 150
img = Image.new("RGB", (W, H), (13,16,21))
img.paste(c, (20, 108))
d = ImageDraw.Draw(img)
d.text((20, 18), "走廊掩体实测：%d×%d m 区域（第 1 层，seed 1，4.8m 主走廊 + 3.6m 次走廊）" % (x1-x0, y1-y0), font=font(22, True), fill=(232,238,246))
d.text((20, 52), "琥珀=大件（1.4–2.2m，挡视线）｜浅蓝灰=中件（1.2–1.8m，蹲姿掩体）｜薄荷绿=小件（0.4–0.6m）", font=font(13), fill=(150,162,178))
d.text((20, 74), "两侧都有、贴墙、避开门口 1.2m 与走廊交叉口；中间留 2.4m 以上通行", font=font(13), fill=(110,200,190))
img.save("docs/3d-rebuild/screenshots/m1-cover-zoom.png")
print("ok", img.size, src.size)
