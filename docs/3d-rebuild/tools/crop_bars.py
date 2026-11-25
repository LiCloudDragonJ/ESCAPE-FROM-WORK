
from PIL import Image
im = Image.open(r"D:/workspace/ESCAPE FROM WORK/docs/3d-rebuild/screenshots/m1-floor3d-core.png").convert("RGB")
boxes = [(90,380,320,800),(500,360,700,800),(1100,360,1300,800),(1420,300,1620,800)]
tiles = []
for b in boxes:
    c = im.crop(b); tiles.append(c.resize((c.width*3, c.height*3), Image.LANCZOS))
w = sum(t.width for t in tiles); h = max(t.height for t in tiles)
out = Image.new("RGB", (w, h), (0,0,0))
x = 0
for t in tiles:
    out.paste(t, (x, 0)); x += t.width
out.save(r"D:/workspace/ESCAPE FROM WORK/docs/3d-rebuild/screenshots/bars.png")
print(out.size)
