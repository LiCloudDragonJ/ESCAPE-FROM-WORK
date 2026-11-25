
from PIL import Image, ImageDraw, ImageFont
src = r"D:/workspace/ESCAPE FROM WORK/docs/3d-rebuild/screenshots/m1-floor3d-core.png"
im = Image.open(src).convert("RGB")
d = ImageDraw.Draw(im)
font = ImageFont.truetype("C:/Windows/Fonts/msyhbd.ttc", 40)
boxes = [
    (10, 380, 420, 760),    # left door / header
    (1090, 340, 1380, 720), # center pillar-like wall stub
    (1470, 300, 1920, 760), # right opening with light
    (520, 300, 760, 700),   # left-center small panel
]
for i,(x0,y0,x1,y1) in enumerate(boxes, 1):
    d.rectangle([x0,y0,x1,y1], outline=(255,70,70), width=6)
    d.text((x0+10, max(0,y0-50)), str(i), font=font, fill=(255,70,70))
im.save(r"D:/workspace/ESCAPE FROM WORK/docs/3d-rebuild/screenshots/m1-wall-annotated.png")
print("ok")
