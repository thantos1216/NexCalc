import zlib, struct, math, os

# Renders the NexCalc icon: dark rounded tile, blue sine curve, amber "N".
# Encodes PNG from scratch (zlib + CRC32), then packages an ICO with embedded PNGs.

W = 48

def stamp(px, cx, cy, rad, col):
    lo_y = int(cy - rad) - 1
    hi_y = int(cy + rad) + 2
    lo_x = int(cx - rad) - 1
    hi_x = int(cx + rad) + 2
    for yy in range(lo_y, hi_y):
        for xx in range(lo_x, hi_x):
            if 0 <= xx < W and 0 <= yy < W:
                dx = xx - cx
                dy = yy - cy
                if dx * dx + dy * dy <= rad * rad:
                    px[yy][xx] = col

def make_base():
    px = [[(0, 0, 0, 0) for _ in range(W)] for _ in range(W)]
    r = 10
    for y in range(W):
        for x in range(W):
            inside = True
            if x < r and y < r:
                inside = (x - r) ** 2 + (y - r) ** 2 <= r * r
            elif x >= W - r and y < r:
                inside = (x - (W - r - 1)) ** 2 + (y - r) ** 2 <= r * r
            elif x < r and y >= W - r:
                inside = (x - r) ** 2 + (y - (W - r - 1)) ** 2 <= r * r
            elif x >= W - r and y >= W - r:
                inside = (x - (W - r - 1)) ** 2 + (y - (W - r - 1)) ** 2 <= r * r
            if inside:
                px[y][x] = (24, 24, 42, 255)
    # blue sine curve across the tile
    for x in range(W):
        t = x / (W - 1)
        y = 24 - 15 * math.sin(2 * math.pi * t)
        stamp(px, x, y, 2.2, (91, 141, 239, 255))
    # N glyph with dark halo: left vertical, diagonal, right vertical
    halo = (10, 10, 20, 255)
    amber = (255, 196, 105, 255)
    for y in range(12, 37):
        stamp(px, 14, y, 3.6, halo)
        stamp(px, 34, y, 3.6, halo)
    for t in range(1001):
        f = t / 1000.0
        stamp(px, 14 + 20 * f, 12 + 24 * f, 3.6, halo)
    for y in range(12, 37):
        stamp(px, 14, y, 2.5, amber)
        stamp(px, 34, y, 2.5, amber)
    for t in range(1001):
        f = t / 1000.0
        stamp(px, 14 + 20 * f, 12 + 24 * f, 2.5, amber)
    return px

def scale_nearest(px, n):
    out = [[(0, 0, 0, 0) for _ in range(n)] for _ in range(n)]
    for y in range(n):
        for x in range(n):
            sx = int((x + 0.5) * W / n)
            sy = int((y + 0.5) * W / n)
            if sx >= W: sx = W - 1
            if sy >= W: sy = W - 1
            out[y][x] = px[sy][sx]
    return out

def chunk(t, d):
    return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xFFFFFFFF)

def png_bytes(px):
    n = len(px)
    raw = b''
    for row in px:
        raw += b'\x00' + b''.join(bytes(c) for c in row)
    body = struct.pack('>II5B', n, n, 8, 6, 0, 0, 0)
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', body) + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b'')

def ico_bytes(entries):
    cnt = len(entries)
    header = struct.pack('<HHH', 0, 1, cnt)
    dir_ = b''
    offset = 6 + 16 * cnt
    for data, n in entries:
        w = n if n < 256 else 0
        h = n if n < 256 else 0
        dir_ += struct.pack('<4B2H2I', w, h, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    return header + dir_ + b''.join(d for d, _ in entries)

def bmp_dib(px):
    # Classic BMP-format DIB for ICO entries (csc's resource builder needs BMP, not PNG).
    h = len(px)
    w = len(px[0])
    mask_row = ((w + 31) // 32) * 4
    xor = b''
    for y in range(h - 1, -1, -1):
        for x in range(w):
            r, g, b, a = px[y][x]
            xor += bytes((b, g, r, a))
    hdr = struct.pack('<IiiHHIIiiII', 40, w, h * 2, 1, 32, 0, len(xor), 0, 0, 0, 0)
    mask = b'\x00' * (mask_row * h)
    return hdr + xor + mask

base = make_base()
png48 = png_bytes(base)
png32 = png_bytes(scale_nearest(base, 32))
png16 = png_bytes(scale_nearest(base, 16))

assets = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'src', 'NexCalc', 'Assets')
assets = os.path.normpath(assets)
os.makedirs(assets, exist_ok=True)
with open(os.path.join(assets, 'nexcalc.png'), 'wb') as f:
    f.write(png48)
with open(os.path.join(assets, 'nexcalc.ico'), 'wb') as f:
    f.write(ico_bytes([(bmp_dib(base), 48), (bmp_dib(scale_nearest(base, 32)), 32), (bmp_dib(scale_nearest(base, 16)), 16)]))
print('wrote', os.path.join(assets, 'nexcalc.ico'))
