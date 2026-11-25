from PIL import Image
base = r"D:/workspace/ESCAPE FROM WORK/docs/3d-rebuild/screenshots/"
jobs = [
    ("m1-floor3d-corridor.png", (0, 0, 960, 620), "probe-corr-left.png", 2),
    ("m1-floor3d-corridor.png", (700, 250, 1500, 720), "probe-corr-mid.png", 2),
    ("m1-floor3d-core.png", (0, 60, 760, 700), "probe-core-left.png", 2),
]
for src, box, out, scale in jobs:
    im = Image.open(base + src).crop(box)
    im = im.resize((im.width * scale, im.height * scale), Image.LANCZOS)
    im.save(base + out)
    print(out, im.size)
