from PIL import Image
im = Image.open('out/large/plan-proto-f07.png').convert('RGB')
print(im.size)
# scan a horizontal line crossing the diamond and the ring
y = 900
row = [im.getpixel((x, y)) for x in range(600, 1400, 20)]
print(' '.join('%d:%s' % (600 + i * 20, c) for i, c in enumerate(row)))
