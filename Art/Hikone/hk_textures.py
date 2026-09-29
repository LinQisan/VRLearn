"""Procedural, tileable 512px textures for the Hikone set (numpy only)."""
import numpy as np
from hk_lib import save_tex

N = 512
rng = np.random.default_rng(7)


def value_noise(n, cells, seed):
    """Tileable smooth value noise in 0..1."""
    r = np.random.default_rng(seed)
    g = r.random((cells, cells))
    x = np.linspace(0, cells, n, endpoint=False)
    i0 = np.floor(x).astype(int)
    f = x - i0
    f = f * f * (3 - 2 * f)
    i1 = (i0 + 1) % cells
    a = g[np.ix_(i0, i0)]; b = g[np.ix_(i0, i1)]
    c = g[np.ix_(i1, i0)]; d = g[np.ix_(i1, i1)]
    fy = f[:, None]; fx = f[None, :]
    return a * (1 - fx) * (1 - fy) + b * fx * (1 - fy) + c * (1 - fx) * fy + d * fx * fy


def fbm(n, seed, octaves=(4, 8, 16, 32, 64), amps=(0.4, 0.25, 0.15, 0.12, 0.08)):
    out = np.zeros((n, n))
    for k, (c, a) in enumerate(zip(octaves, amps)):
        out += a * value_noise(n, c, seed + k)
    return out / sum(amps)


def tint(gray, col, var=0.12):
    col = np.array(col)[None, None, :]
    if var == 0:
        return col * gray[:, :, None]
    return col * (1 - var + 2 * var * gray[:, :, None])


def stone_wall():
    # irregular ishigaki blocks: jittered voronoi cells (tileable via wrapped distance)
    pts = rng.random((46, 2))
    yy, xx = np.mgrid[0:N, 0:N] / N
    d1 = np.full((N, N), 9.0); d2 = np.full((N, N), 9.0); idx = np.zeros((N, N), int)
    for k, (px, py) in enumerate(pts):
        dx = np.abs(xx - px); dx = np.minimum(dx, 1 - dx) * 1.0
        dy = np.abs(yy - py); dy = np.minimum(dy, 1 - dy) * 1.35   # wider-than-tall stones
        d = np.sqrt(dx * dx + dy * dy)
        closer = d < d1
        d2 = np.where(closer, d1, np.minimum(d2, d)); idx = np.where(closer, k, idx); d1 = np.where(closer, d, d1)
    edge = np.clip((d2 - d1) * 40, 0, 1)
    shade = rng.random(len(pts))[idx]
    n = fbm(N, 11)
    base = 0.55 + 0.25 * shade + 0.25 * (n - 0.5)
    face = base * (0.35 + 0.65 * edge ** 0.6)
    rgb = tint(face, (0.62, 0.60, 0.56), 0.0) * 1.0
    rgb *= (0.92 + 0.12 * (value_noise(N, 6, 5)[:, :, None]))
    save_tex("HK_T_StoneWall", rgb)


def pavers():
    # 0.3 x 0.6 m staggered stone pavers over a 2.4 m tile -> 8 rows x 4 cols
    yy, xx = np.mgrid[0:N, 0:N] / N
    rows = 8
    r = np.floor(yy * rows).astype(int)
    off = (r % 2) * 0.125
    cols = 4
    c = np.floor(((xx + off) % 1.0) * cols).astype(int)
    fx = ((xx + off) % 1.0) * cols - c
    fy = yy * rows - r
    joint = np.minimum.reduce([fx, 1 - fx, fy * 0.5 + 0.0, (1 - fy) * 0.5]) * 1.0
    j = np.clip(joint * 28, 0, 1)
    cell_rand = np.random.default_rng(3).random((rows, cols))[r, c]
    n = fbm(N, 21)
    g = 0.72 + 0.18 * cell_rand + 0.2 * (n - 0.5)
    g = g * (0.55 + 0.45 * j)
    save_tex("HK_T_Pavers", tint(g, (0.78, 0.74, 0.66), 0.0))


def asphalt():
    n = fbm(N, 31, octaves=(8, 16, 64, 128), amps=(0.3, 0.3, 0.25, 0.15))
    speck = (np.random.default_rng(9).random((N, N)) > 0.985) * 0.25
    g = 0.78 + 0.35 * (n - 0.5) + speck
    save_tex("HK_T_Asphalt", tint(g, (0.36, 0.36, 0.37), 0.0))


def roof_tile():
    # kawara: 8 tile columns x 8 rows over 2 m; rounded column shading + row lap shadow
    yy, xx = np.mgrid[0:N, 0:N] / N
    cx = (xx * 8) % 1.0
    col = 0.55 + 0.45 * np.sin(cx * np.pi)          # convex tile profile
    ry = (yy * 8) % 1.0
    lap = 0.65 + 0.35 * np.clip(ry * 3, 0, 1)        # darker at the lap under the tile above
    n = fbm(N, 41)
    g = col * lap * (0.9 + 0.2 * n)
    save_tex("HK_T_RoofTile", tint(g, (0.34, 0.35, 0.38), 0.0))


def plaster():
    n = fbm(N, 51, octaves=(4, 16, 64, 128), amps=(0.3, 0.3, 0.25, 0.15))
    g = 0.94 + 0.10 * (n - 0.5)
    save_tex("HK_T_Plaster", tint(g, (0.96, 0.95, 0.91), 0.0))


def wood():
    yy, xx = np.mgrid[0:N, 0:N] / N
    n = value_noise(N, 8, 61)
    grain = 0.5 + 0.5 * np.sin((xx * 40 + n * 6) * np.pi)
    fine = fbm(N, 62, octaves=(16, 64, 128), amps=(0.4, 0.4, 0.2))
    g = 0.75 + 0.15 * grain + 0.2 * (fine - 0.5)
    # vertical board joints every 1/6
    bj = np.abs(((xx * 6) % 1.0) - 0.5) > 0.485
    g = np.where(bj, g * 0.55, g)
    save_tex("HK_T_Wood", tint(g, (0.42, 0.30, 0.20), 0.0))


def namako():
    # square grey tiles set diagonally with raised white plaster joints, 3 tiles per repeat
    yy, xx = np.mgrid[0:N, 0:N] / N
    u = (xx + yy) * 3 / np.sqrt(2) * np.sqrt(2)
    v = (xx - yy) * 3 / np.sqrt(2) * np.sqrt(2)
    fu = u % 1.0; fv = v % 1.0
    joint = np.minimum.reduce([fu, 1 - fu, fv, 1 - fv])
    white = joint < 0.07
    n = fbm(N, 71)
    tile = tint(0.8 + 0.2 * n, (0.33, 0.34, 0.37), 0.0)
    rgb = np.where(white[:, :, None], np.array([0.93, 0.92, 0.88]), tile)
    save_tex("HK_T_Namako", rgb)


def grass():
    n = fbm(N, 81, octaves=(4, 8, 32, 128), amps=(0.35, 0.25, 0.25, 0.15))
    rgb = np.stack([0.30 + 0.12 * n, 0.40 + 0.14 * n, 0.18 + 0.06 * n], -1)
    save_tex("HK_T_Grass", rgb)


def water_normal():
    # tangent-space normal map of gentle ripples
    h = fbm(N, 91, octaves=(4, 8, 16, 32), amps=(0.4, 0.3, 0.2, 0.1))
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 6
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 6
    nz = np.ones_like(h)
    l = np.sqrt(gx * gx + gy * gy + nz * nz)
    rgb = np.stack([(-gx / l) * 0.5 + 0.5, (-gy / l) * 0.5 + 0.5, (nz / l) * 0.5 + 0.5], -1)
    save_tex("HK_T_WaterNormal", rgb)


def run():
    stone_wall(); pavers(); asphalt(); roof_tile(); plaster(); wood(); namako(); grass(); water_normal()
    return "textures ok"
