
from PIL import Image, ImageDraw
import os

S = 1024
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# 圆角方块 + 渐变（靛蓝 -> 紫）
r = int(S * 0.23)
c1 = (79, 70, 229)
c2 = (139, 92, 246)
mask = Image.new("L", (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle([0, 0, S - 1, S - 1], radius=r, fill=255)
grad = Image.new("RGBA", (S, S))
gd = ImageDraw.Draw(grad)
for y in range(S):
    t = y / (S - 1)
    col = (
        int(c1[0] + (c2[0] - c1[0]) * t),
        int(c1[1] + (c2[1] - c1[1]) * t),
        int(c1[2] + (c2[2] - c1[2]) * t),
        255,
    )
    gd.line([(0, y), (S, y)], fill=col)
img.paste(grad, (0, 0), mask)

# 白色放大镜
ring_c = (S * 0.435, S * 0.415)
ring_r = S * 0.205
w = int(S * 0.083)
d.ellipse([ring_c[0] - ring_r, ring_c[1] - ring_r, ring_c[0] + ring_r, ring_c[1] + ring_r],
          outline=(255, 255, 255, 255), width=w)
# 手柄
import math
ang = math.radians(45)
x1 = ring_c[0] + (ring_r + w * 0.35) * math.cos(ang)
y1 = ring_c[1] + (ring_r + w * 0.35) * math.sin(ang)
x2 = S * 0.795
y2 = S * 0.775
d.line([(x1, y1), (x2, y2)], fill=(255, 255, 255, 255), width=w)
d.ellipse([x2 - w / 2, y2 - w / 2, x2 + w / 2, y2 + w / 2], fill=(255, 255, 255, 255))
d.ellipse([x1 - w / 2, y1 - w / 2, x1 + w / 2, y1 + w / 2], fill=(255, 255, 255, 255))

# 镜片里一道高光
d.arc([ring_c[0] - ring_r * 0.62, ring_c[1] - ring_r * 0.62, ring_c[0] + ring_r * 0.62, ring_c[1] + ring_r * 0.62],
      200, 290, fill=(199, 210, 254, 255), width=int(w * 0.42))

out = os.path.join("icons", "lenspop.ico")
os.makedirs("icons", exist_ok=True)
sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (24, 24), (16, 16)]
img.save(out, format="ICO", sizes=sizes)
img.resize((256, 256), Image.LANCZOS).save(os.path.join("icons", "icon256.png"))
print("icon written", os.path.getsize(out), "bytes")
