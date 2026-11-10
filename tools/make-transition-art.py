#!/usr/bin/env python3
"""화면 이동 연출용 그림 — 종이를 오려 겹친 꼴의 층(layer) PNG를 짓는다.

다섯 장면(정거장 · 기둥과 노을 · 바닷속 숲 · 나선 나무 · 행성 위)마다 뒤에서 앞으로 겹치는 층과 작은 조각들.
전부 도형으로 그린다(무대 1920×1080 기준, 투명 배경, 종이 결 + 아래로 떨어지는 그림자).
쓰기: python3 tools/make-transition-art.py <출력 폴더>   (장면마다 하위 폴더, preview.png는 겹쳐 본 것)
"""
import math
import os
import random
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

W, H, SS = 1920, 1080, 2


def mask():
    return Image.new("L", (W * SS, H * SS), 0)


def poly(m, pts, v=255):
    ImageDraw.Draw(m).polygon([(x * SS, y * SS) for x, y in pts], fill=v)


def oval(m, cx, cy, rx, ry, v=255):
    ImageDraw.Draw(m).ellipse([(cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS], fill=v)


def line(m, pts, width, v=255):
    d = ImageDraw.Draw(m)
    d.line([(x * SS, y * SS) for x, y in pts], fill=v, width=int(width * SS), joint="curve")
    r = width / 2
    for x, y in (pts[0], pts[-1]):
        oval(m, x, y, r, r, v)


def ring(m, cx, cy, r, width):
    oval(m, cx, cy, r, r)
    oval(m, cx, cy, r - width, r - width, 0)


def hexrgb(c):
    c = c.lstrip("#")
    return tuple(int(c[i:i + 2], 16) for i in (0, 2, 4))


def shift(image, dx, dy):
    """밀기 — 넘친 것은 버린다(ImageChops.offset은 반대편으로 돌아 나와, 아래 끝의 그림자가 위 끝에 띠로 찍힌다)."""
    out = Image.new(image.mode, image.size, 0)
    out.paste(image, (dx, dy))
    return out


_grain = None


def grain():
    global _grain
    if _grain is None:
        rnd = random.Random(7)
        small = Image.new("L", (W // 2, H // 2))
        small.putdata([int(max(0, min(255, rnd.gauss(128, 26)))) for _ in range((W // 2) * (H // 2))])
        _grain = small.resize((W, H), Image.BICUBIC).filter(ImageFilter.GaussianBlur(0.6))
    return _grain


def finish(m, color, shadow=0.34, crop=False, rim=True):
    """마스크 → 종이 한 장: 결, 위쪽 모서리의 밝은 테(두께), 아래로 떨어지는 그림자."""
    a = m.resize((W, H), Image.LANCZOS)
    r, g, b = hexrgb(color)
    body = Image.new("RGB", (W, H), (r, g, b))
    # 종이 결 — 밝기를 ±5%쯤 흔든다.
    gr = grain().point(lambda v: 128 + (v - 128) * 0.35)
    body = ImageChops.overlay(body, Image.merge("RGB", (gr, gr, gr)))
    if rim:
        # 위 모서리의 밝은 테: 마스크에서 아래로 2px 민 것을 뺀 띠.
        top = ImageChops.subtract(a, shift(a, 0, 3))
        light = Image.new("RGB", (W, H), tuple(min(255, int(c + (255 - c) * 0.45)) for c in (r, g, b)))
        body = Image.composite(light, body, top.filter(ImageFilter.GaussianBlur(0.8)))
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    if shadow > 0:
        sh = shift(a, 7, 12).filter(ImageFilter.GaussianBlur(9)).point(lambda v: int(v * shadow))
        out = Image.merge("RGBA", (Image.new("L", (W, H), 12), Image.new("L", (W, H), 14), Image.new("L", (W, H), 24), sh))
    out.alpha_composite(Image.merge("RGBA", (*body.split(), a)))
    if crop:
        box = out.getbbox()
        if box:
            pad = 4
            out = out.crop((max(0, box[0] - pad), max(0, box[1] - pad), min(W, box[2] + pad), min(H, box[3] + pad)))
    return out


def glow(cx, cy, r, color, strength=1.0):
    """부드럽게 번지는 빛 — 종이가 아니라 뒤에서 비치는 빛(그림자·결 없음)."""
    m = Image.new("L", (W, H), 0)
    ImageDraw.Draw(m).ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)
    m = m.filter(ImageFilter.GaussianBlur(r * 0.45)).point(lambda v: int(v * strength))
    rr, gg, bb = hexrgb(color)
    out = Image.merge("RGBA", (Image.new("L", (W, H), rr), Image.new("L", (W, H), gg), Image.new("L", (W, H), bb), m))
    box = out.getbbox()
    return out.crop(box) if box else out


def hills(m, base, amp, seed, y1=H + 40):
    rnd = random.Random(seed)
    ph = [rnd.uniform(0, 6.28) for _ in range(3)]
    fr = [rnd.uniform(0.6, 1.4) * k for k in (1.0, 2.3, 4.1)]
    pts = [(-40, y1)]
    for x in range(-40, W + 41, 16):
        t = x / W * 6.28
        y = base - amp * (0.6 * math.sin(fr[0] * t + ph[0]) + 0.3 * math.sin(fr[1] * t + ph[1]) + 0.1 * math.sin(fr[2] * t + ph[2]))
        pts.append((x, y))
    pts.append((W + 40, y1))
    poly(m, pts)


def skyline(m, base, seed, hmin, hmax, wmin=40, wmax=110, x0=-20, x1=W + 20, tilt=0.0):
    rnd = random.Random(seed)
    x = x0
    while x < x1:
        w = rnd.randint(wmin, wmax)
        h = rnd.randint(hmin, hmax)
        b = base + tilt * x
        top = b - h
        poly(m, [(x, b + 60), (x, top), (x + w, top), (x + w, b + 60)])
        if rnd.random() < 0.35:  # 뾰족 지붕·안테나
            poly(m, [(x + w * 0.3, top), (x + w * 0.5, top - rnd.randint(14, 46)), (x + w * 0.7, top)])
        if rnd.random() < 0.25:
            poly(m, [(x + w * 0.45, top), (x + w * 0.45, top - 60), (x + w * 0.52, top - 60), (x + w * 0.52, top)])
        x += w + rnd.randint(-6, 14)
    poly(m, [(x0, base + 30), (x1, base + 30 + tilt * x1), (x1, H + 60), (x0, H + 60)])


def cloud(m, cx, cy, w, h, seed):
    rnd = random.Random(seed)
    oval(m, cx, cy, w / 2, h / 3)
    for _ in range(7):
        ox = rnd.uniform(-w / 2.4, w / 2.4)
        oval(m, cx + ox, cy - rnd.uniform(0, h / 3), rnd.uniform(w / 7, w / 4), rnd.uniform(h / 4, h / 2.2))
    # 바닥을 반듯하게 자른다 — 종이 구름의 평평한 밑변(양옆으로 넉넉히 잘라 갈고리가 안 남게).
    poly(m, [(cx - w, cy + h * 0.06), (cx + w, cy + h * 0.06), (cx + w, cy + h), (cx - w, cy + h)], 0)


def fish(m, cx, cy, s, flip=1, v=255):
    oval(m, cx, cy, s, s * 0.42, v)
    poly(m, [(cx - flip * s * 0.8, cy), (cx - flip * s * 1.5, cy - s * 0.5), (cx - flip * s * 1.5, cy + s * 0.5)], v)


def whale(m, cx, cy, s, flip=1):
    pts = []
    for i in range(41):  # 몸통 — 앞이 둥글고 뒤로 가늘어진다
        t = i / 40
        x = cx + flip * (s * (1 - 2 * t))
        y = cy - s * 0.34 * math.sin(math.pi * t) ** 0.7 * (1 - 0.25 * t)
        pts.append((x, y))
    for i in range(41):
        t = 1 - i / 40
        x = cx + flip * (s * (1 - 2 * t))
        y = cy + s * 0.22 * math.sin(math.pi * t) ** 0.8
        pts.append((x, y))
    poly(m, pts)
    tx = cx - flip * s
    poly(m, [(tx + flip * s * 0.12, cy), (tx - flip * s * 0.28, cy - s * 0.3), (tx - flip * s * 0.12, cy), (tx - flip * s * 0.28, cy + s * 0.3)])
    poly(m, [(cx + flip * s * 0.2, cy + s * 0.1), (cx - flip * s * 0.05, cy + s * 0.5), (cx - flip * s * 0.15, cy + s * 0.12)])


def trumpet(m, cx, base, h, w, flare, lean=0.0):
    """위가 나팔처럼 벌어지는 기둥."""
    left, right = [], []
    for i in range(31):
        t = i / 30
        y = base - h * t
        half = w / 2 * (0.75 + 0.5 * (1 - t) ** 2) + flare * max(0.0, t - 0.62) ** 2 * 7
        x = cx + lean * h * t
        left.append((x - half, y))
        right.append((x + half, y))
    top = base - h
    cap = [(cx + lean * h + flare * 1.02 * math.cos(a), top - 14 * math.sin(a)) for a in [math.pi * k / 12 for k in range(13)]]
    poly(m, left + cap[::-1] + right[::-1])


def leaf(m, cx, cy, s, ang):
    pts = []
    for i in range(25):
        t = i / 24 * 2 * math.pi
        x, y = s * math.cos(t), s * 0.32 * math.sin(t) * (1 - 0.35 * math.cos(t))
        pts.append((cx + x * math.cos(ang) - y * math.sin(ang), cy + x * math.sin(ang) + y * math.cos(ang)))
    poly(m, pts)


def star4(m, cx, cy, s):
    pts = []
    for i in range(8):
        r = s if i % 2 == 0 else s * 0.22
        a = math.pi / 4 * i - math.pi / 2
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    poly(m, pts)


# ── 장면들 ────────────────────────────────────────────────────────────────────
# 각 장면: (이름, 바탕색, [(파일 이름, 그림, 놓는 자리 x, y — 겹쳐 볼 때)])


def scene_station():
    """지하 정거장 — 소실점으로 모이는 기둥과 아치, 끝의 빛, 떠다니는 빛나는 미생물."""
    c = dict(bg="#10262B", far="#1C3D3F", mid="#16302F", near="#0B1B1E", sign="#24343C", warm="#E9A15B", neon1="#9BE36A", neon2="#5CC8E8", neon3="#F2D45C")
    L = []
    vx, vy = 960, 620
    m = mask()  # 아치 — 소실점 둘레의 겹친 고리
    for r, wd in ((520, 60), (380, 44), (270, 32), (190, 22)):
        ring(m, vx, vy, r, wd)
    poly(m, [(0, vy + 40), (W, vy + 40), (W, H), (0, H)], 0)
    L.append(("arches", finish(m, c["far"]), 0, 0))
    m = mask()  # 바닥 — 소실점에서 벌어지는 승강장
    poly(m, [(vx - 60, vy + 30), (vx + 60, vy + 30), (W + 200, H + 20), (-200, H + 20)])
    L.append(("floor", finish(m, c["mid"], shadow=0.2), 0, 0))
    for side, name in ((-1, "pillars_left"), (1, "pillars_right")):
        m = mask()
        for k, (d, wd) in enumerate(((880, 150), (600, 96), (410, 62), (285, 40), (200, 26))):
            x = vx + side * d
            top = vy - d * 0.78
            bot = vy + d * 0.52
            poly(m, [(x - wd / 2, top), (x + wd / 2, top), (x + wd / 2, bot), (x - wd / 2, bot)])
            poly(m, [(x - wd * 0.9, top), (x + wd * 0.9, top), (x + wd * 0.7, top + wd * 0.35), (x - wd * 0.7, top + wd * 0.35)])
        # 기둥을 잇는 들보 — 소실점으로 모인다
        poly(m, [(vx + side * 960, vy - 960 * 0.80), (vx + side * 960, vy - 960 * 0.70), (vx + side * 180, vy - 180 * 0.70), (vx + side * 180, vy - 180 * 0.80)])
        L.append((name, finish(m, c["near"]), 0, 0))
    m = mask()  # 열차 — 왼쪽에 서 있다
    poly(m, [(-40, 520), (430, 600), (470, 640), (470, 800), (-40, 900)])
    for i in range(4):
        x0 = 20 + i * 105
        poly(m, [(x0, 590 + i * 14), (x0 + 70, 602 + i * 14), (x0 + 70, 700 + i * 6), (x0, 700 + i * 2)], 0)
    L.append(("train", finish(m, "#22484A"), 0, 0))
    m = mask()  # 매달린 안내판
    poly(m, [(560, 150), (1400, 150), (1400, 262), (560, 262)])
    poly(m, [(640, 60), (652, 60), (652, 150), (640, 150)])
    poly(m, [(1308, 60), (1320, 60), (1320, 150), (1308, 150)])
    L.append(("sign", finish(m, c["sign"], crop=True), 556, 56))
    L.append(("light", glow(vx, vy - 20, 230, c["warm"], 0.95), vx - 230 - 100, vy - 20 - 230 - 100))
    # 미생물 — 가는 선으로 그린 빛나는 것들
    m = mask()
    ring(m, 200, 200, 70, 7)
    for k in range(14):
        a = 2 * math.pi * k / 14
        line(m, [(200 + 74 * math.cos(a), 200 + 74 * math.sin(a)), (200 + 112 * math.cos(a), 200 + 112 * math.sin(a))], 4)
    oval(m, 180, 190, 16, 16)
    L.append(("microbe_sun", finish(m, c["neon2"], shadow=0.0, crop=True, rim=False), 1180, 170))
    m = mask()
    pts = [(200 + 46 * math.cos(t) * (1 + 0.15 * math.sin(3 * t)), 300 + 150 * math.sin(t)) for t in [2 * math.pi * k / 60 for k in range(61)]]
    line(m, pts, 6)
    line(m, [(200, 190), (200, 410)], 3)
    for yy in (230, 280, 330, 380):
        line(m, [(172, yy), (228, yy - 16)], 3)
    L.append(("microbe_leaf", finish(m, c["neon1"], shadow=0.0, crop=True, rim=False), 430, 60))
    m = mask()
    pts = [(200 + 60 * math.cos(t), 200 + 44 * math.sin(t) + 10 * math.sin(2 * t)) for t in [2 * math.pi * k / 50 for k in range(51)]]
    line(m, pts, 6)
    oval(m, 184, 196, 9, 9)
    oval(m, 214, 206, 6, 6)
    line(m, [(258, 200), (300, 180), (330, 204), (360, 186)], 4)
    L.append(("microbe_cell", finish(m, c["neon3"], shadow=0.0, crop=True, rim=False), 320, 720))
    return "station", c["bg"], L


def scene_pillar():
    """기울어진 기둥과 노을 — 큰 하늘, 낮은 해, 멀리 도시, 날리는 꽃잎."""
    c = dict(bg="#243B6B", cloud1="#3A4E86", cloud2="#7B5C8E", haze="#E8A46B", city1="#8A5A6A", city2="#4A3552", hill="#2A2238", pillar="#3B2F3E", sun="#FFE2A6", petal="#F4C06A")
    L = []
    m = mask()
    cloud(m, 520, 330, 900, 240, 1)
    cloud(m, 1500, 240, 700, 200, 2)
    L.append(("clouds_far", finish(m, c["cloud1"], shadow=0.18), 0, 0))
    m = mask()
    cloud(m, 1250, 520, 1100, 280, 3)
    cloud(m, 300, 620, 700, 200, 4)
    L.append(("clouds_near", finish(m, c["cloud2"], shadow=0.22), 0, 0))
    m = mask()  # 지평선의 노을 띠
    hills(m, 730, 22, 11)
    L.append(("haze", finish(m, c["haze"], shadow=0.0), 0, 0))
    L.append(("sun_glow", glow(1560, 690, 320, c["sun"], 0.95), 1560 - 320 - 144, 690 - 320 - 144))
    m = mask()
    oval(m, 200, 200, 92, 92)
    L.append(("sun", finish(m, c["sun"], shadow=0.0, crop=True), 1560 - 96, 690 - 96))
    m = mask()
    skyline(m, 850, 21, 30, 120, tilt=-0.03)
    L.append(("city_far", finish(m, c["city1"]), 0, 0))
    m = mask()
    skyline(m, 930, 22, 50, 190, 60, 150, tilt=-0.04)
    L.append(("city_near", finish(m, c["city2"]), 0, 0))
    m = mask()
    hills(m, 990, 34, 23)
    L.append(("hill", finish(m, c["hill"]), 0, 0))
    m = mask()  # 기둥 — 오른쪽으로 기울어 서 있다. 머리가 넓다.
    poly(m, [(150, 330), (330, 300), (480, 1010), (330, 1040)])
    poly(m, [(60, 250), (420, 190), (470, 300), (120, 360)])
    poly(m, [(110, 190), (380, 140), (430, 205), (90, 262)])
    poly(m, [(300, 150), (520, 210), (500, 262), (330, 232)])
    L.append(("pillar", finish(m, c["pillar"], crop=True), 56, 136))
    for i, (s, a) in enumerate(((60, 0.5), (40, -0.7), (28, 1.1))):
        m = mask()
        leaf(m, 200, 200, s, a)
        L.append((f"petal_{i + 1}", finish(m, c["petal"], shadow=0.2, crop=True), (760, 1180, 1420)[i], (300, 470, 180)[i]))
    return "pillar", c["bg"], L


def scene_lapis():
    """바닷속 숲 — 나팔 기둥이 줄지어 선 푸른 바닥, 물고기 떼, 위의 별."""
    c = dict(bg="#0E1E4A", far="#2B4E9A", mid="#1E3B80", near="#0F2358", bed1="#2A5BB0", bed2="#16357A", band="#BFE3F2", fish="#0B1A40", star="#9FE8F0", orb="#EAF7FF")
    L = []
    m = mask()  # 수면 쪽의 밝은 띠
    hills(m, 400, 18, 31, y1=470)
    L.append(("light_band", finish(m, c["band"], shadow=0.0), 0, 0))
    m = mask()
    for x, h in ((260, 330), (560, 300), (840, 320), (1100, 290), (1380, 325), (1660, 300)):
        trumpet(m, x, 720, h, 46, 34)
    L.append(("pillars_far", finish(m, c["far"], shadow=0.2), 0, 0))
    m = mask()
    hills(m, 700, 40, 32)
    L.append(("seabed_far", finish(m, c["bed1"], shadow=0.2), 0, 0))
    m = mask()
    for x, h, w in ((170, 500, 86), (620, 470, 80), (1240, 520, 92), (1720, 480, 84)):
        trumpet(m, x, 900, h, w, 60)
    L.append(("pillars_mid", finish(m, c["mid"]), 0, 0))
    m = mask()
    hills(m, 880, 60, 33)
    for x in (430, 980, 1500):  # 뾰족한 바위
        poly(m, [(x - 90, 900), (x, 640), (x + 100, 900)])
    L.append(("seabed_near", finish(m, c["bed2"]), 0, 0))
    m = mask()
    trumpet(m, 70, 1100, 900, 190, 120, lean=0.02)
    L.append(("trunk_left", finish(m, c["near"], crop=True), 0, 150))
    m = mask()
    trumpet(m, 1830, 1100, 820, 170, 110, lean=-0.03)
    L.append(("trunk_right", finish(m, c["near"], crop=True), 1620, 230))
    m = mask()
    rnd = random.Random(35)
    for _ in range(26):
        fish(m, rnd.uniform(60, 640), rnd.uniform(40, 300), rnd.uniform(9, 20), 1)
    L.append(("fish_school", finish(m, c["fish"], shadow=0.15, crop=True), 1150, 300))
    m = mask()  # 큰 것 — 가오리
    wing = [(120 + 300 * t, 200 - 90 * math.sin(math.pi * t) ** 0.8) for t in [k / 24 for k in range(25)]]
    belly = [(420 - 300 * t, 200 + 62 * math.sin(math.pi * t) ** 0.9) for t in [k / 24 for k in range(25)]]
    poly(m, wing + belly)
    line(m, [(410, 202), (520, 208), (620, 200)], 6)
    L.append(("ray", finish(m, "#7F8FC8", crop=True), 760, 560))
    m = mask()
    rnd = random.Random(36)
    for _ in range(46):
        r = rnd.uniform(2, 6)
        oval(m, rnd.uniform(0, W), rnd.uniform(20, 330), r, r)
    L.append(("stars", finish(m, c["star"], shadow=0.0, rim=False), 0, 0))
    m = mask()
    ring(m, 200, 200, 86, 20)
    poly(m, [(200, 200), (330, 120), (330, 290)], 0)
    oval(m, 208, 204, 34, 34)
    L.append(("orb", finish(m, c["orb"], shadow=0.2, crop=True), 250, 400))
    return "lapis", c["bg"], L


def scene_heredity():
    """나선 나무 — 큰 달 앞의 소용돌이 가지, 한가운데의 구슬, 기운 수정, 떠 있는 섬."""
    c = dict(bg="#0C1A33", moon="#9CC6E8", ring="#141E33", orb="#3E86D8", orbin="#BFE6FF", crystal="#7E7BD6", crystal2="#B7B2F0", isle="#3C5A2E", isle2="#1A2A1C", canopy="#16284A", leaf="#3A5C96", water="#8FE0E8")
    L = []
    m = mask()
    oval(m, 430, 430, 420, 420)
    L.append(("moon", finish(m, c["moon"], shadow=0.0, crop=True), 960 - 424, 440 - 424))
    m = mask()  # 나선 가지 — 바깥에서 안으로 감겨 든다
    pts = []
    for k in range(141):
        t = k / 140
        a = -0.4 + t * 4.6
        r = 380 - 250 * t
        pts.append((960 + r * math.cos(a) * 1.05, 470 - r * math.sin(a)))
    for k in range(len(pts) - 1):
        line(m, [pts[k], pts[k + 1]], 46 - 30 * k / len(pts))
    for dx in (-190, -120, 60, 150):  # 다리 — 뿌리
        line(m, [(960 + dx * 0.5, 520), (960 + dx * 1.1, 700), (960 + dx * 1.5, 860)], 26)
    L.append(("spiral", finish(m, c["ring"]), 0, 0))
    m = mask()
    oval(m, 130, 130, 120, 120)
    L.append(("orb", finish(m, c["orb"], crop=True), 960 - 60 - 124, 420 - 124))
    m = mask()
    pts = [(130 + (70 - 60 * k / 60) * math.cos(k / 60 * 7.5), 130 + (70 - 60 * k / 60) * math.sin(k / 60 * 7.5)) for k in range(61)]
    line(m, pts, 9)
    L.append(("orb_swirl", finish(m, c["orbin"], shadow=0.0, crop=True, rim=False), 960 - 60 - 80, 420 - 80))
    m = mask()  # 수정 — 한쪽으로 기운 기둥들
    for i, (x, h, w) in enumerate(((60, 300, 60), (140, 420, 70), (230, 520, 80), (330, 360, 64), (410, 250, 54))):
        base = 560
        poly(m, [(x, base), (x + w, base), (x + w + h * 0.55, base - h), (x + h * 0.55, base - h)])
    L.append(("crystals", finish(m, c["crystal"], crop=True), 1180, 330))
    m = mask()
    for i, (x, h, w) in enumerate(((100, 240, 30), (200, 400, 34), (300, 300, 28))):
        base = 560
        poly(m, [(x, base), (x + w, base), (x + w + h * 0.55, base - h), (x + h * 0.55, base - h)])
    L.append(("crystals_light", finish(m, c["crystal2"], shadow=0.15, crop=True), 1230, 380))
    m = mask()  # 가운데 섬
    poly(m, [(420, 800), (1560, 780), (1420, 880), (1100, 1010), (760, 960), (520, 880)])
    oval(m, 990, 800, 580, 60)
    L.append(("island", finish(m, c["isle"]), 0, 0))
    m = mask()
    for cx, cy, rx in ((230, 820, 210), (1700, 900, 190), (1480, 660, 120)):
        oval(m, cx, cy, rx, 34)
        poly(m, [(cx - rx * 0.8, cy), (cx + rx * 0.8, cy), (cx + rx * 0.2, cy + 110), (cx - rx * 0.3, cy + 90)])
    L.append(("islets", finish(m, c["isle2"]), 0, 0))
    m = mask()  # 위 두 모서리의 나뭇잎 지붕
    cloud(m, 120, 60, 760, 330, 41)
    cloud(m, 1820, 50, 720, 300, 42)
    L.append(("canopy", finish(m, c["canopy"]), 0, 0))
    for i, (s, a) in enumerate(((70, 0.6), (46, -0.4), (34, 1.3))):
        m = mask()
        leaf(m, 200, 200, s, a)
        L.append((f"leaf_{i + 1}", finish(m, c["leaf"], shadow=0.2, crop=True), (480, 300, 1240)[i], (130, 330, 250)[i]))
    return "heredity", c["bg"], L


def scene_mercury():
    """행성 위 — 비스듬한 지평의 큰 둥근 땅, 대기의 띠, 헤엄치는 고래와 물고기 떼, 먼 별빛."""
    c = dict(bg="#0A1740", atmo="#7FD4E8", atmo2="#CFF3F7", land="#1E5C6A", land2="#2F7A5E", cloud="#EAF4F6", whale="#2B4A8C", whale2="#16306A", fish="#9DB7F2", star="#DFF6FF", sun="#BFF2FF")
    L = []
    cx, cy, R = 300, 2300, 2000  # 화면 밖에 중심을 둔 큰 원 — 오른쪽으로 기운 지평
    m = mask()
    oval(m, cx, cy, R + 70, R + 70)
    L.append(("atmosphere", finish(m, c["atmo"], shadow=0.0), 0, 0))
    m = mask()
    oval(m, cx, cy, R + 26, R + 26)
    L.append(("atmosphere_inner", finish(m, c["atmo2"], shadow=0.0), 0, 0))
    m = mask()
    oval(m, cx, cy, R, R)
    L.append(("planet", finish(m, c["land"], shadow=0.0), 0, 0))
    m = mask()  # 땅의 무늬 — 둥근 면을 따라 흐르는 띠
    for k, (dr, w) in enumerate(((160, 70), (360, 110), (620, 90))):
        ring(m, cx, cy, R - dr, w)
    poly(m, [(0, 0), (W, 0), (W, 380), (0, 380)], 0)
    L.append(("planet_bands", finish(m, c["land2"], shadow=0.12), 0, 0))
    m = mask()
    for x, y, w in ((420, 560, 520), (980, 760, 620), (1500, 930, 520), (160, 900, 460)):
        cloud(m, x, y, w, 150, 50 + x)
    L.append(("clouds", finish(m, c["cloud"], shadow=0.25), 0, 0))
    L.append(("star_glow", glow(1560, 210, 200, c["sun"], 0.8), 1560 - 200 - 90, 210 - 200 - 90))
    m = mask()
    star4(m, 100, 100, 70)
    L.append(("star", finish(m, c["star"], shadow=0.0, crop=True, rim=False), 1560 - 74, 210 - 74))
    m = mask()
    rnd = random.Random(52)
    for _ in range(60):
        r = rnd.uniform(1.5, 4.5)
        oval(m, rnd.uniform(700, W), rnd.uniform(10, 520), r, r)
    L.append(("stars", finish(m, c["star"], shadow=0.0, rim=False), 0, 0))
    m = mask()
    whale(m, 480, 260, 420, 1)
    L.append(("whale_big", finish(m, c["whale"], crop=True), 420, 40))
    m = mask()
    whale(m, 260, 160, 220, -1)
    L.append(("whale_small", finish(m, c["whale2"], crop=True), 1380, 480))
    m = mask()
    rnd = random.Random(53)
    for _ in range(22):
        fish(m, rnd.uniform(40, 560), rnd.uniform(40, 330), rnd.uniform(10, 24), -1)
    L.append(("fish_school", finish(m, c["fish"], shadow=0.18, crop=True), 1180, 700))
    return "mercury", c["bg"], L


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "transition-art"
    for build in (scene_station, scene_pillar, scene_lapis, scene_heredity, scene_mercury):
        name, bg, layers = build()
        folder = os.path.join(out, name)
        os.makedirs(folder, exist_ok=True)
        preview = Image.new("RGBA", (W, H), hexrgb(bg) + (255,))
        rows = []
        for i, (lname, image, x, y) in enumerate(layers):
            image.save(os.path.join(folder, f"{i + 1:02d}_{lname}.png"))
            preview.alpha_composite(image, (int(x), int(y)))
            rows.append(f"{i + 1:02d}_{lname}.png\t{int(x)}\t{int(y)}\t{image.size[0]}\t{image.size[1]}")
        preview.convert("RGB").save(os.path.join(folder, "preview.png"))
        with open(os.path.join(folder, "layers.tsv"), "w", encoding="utf-8") as f:
            f.write(f"# 바탕색 {bg} — 파일\t왼쪽\t위\t너비\t높이 (무대 1920×1080, 왼쪽 위 기준)\n" + "\n".join(rows) + "\n")
        print(name, len(layers), "layers")


if __name__ == "__main__":
    main()
