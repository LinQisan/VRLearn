"""Modular Hikone castle-town assets. Unity coordinates throughout (see hk_lib.U).

Buildings: origin at the front-bottom-centre of the lot, facade faces +Z (street side),
lot extends to -Z. Roads: origin at tile centre, top of carriageway ~= y 0.
"""
import math, random
import bpy
from hk_lib import MB, clear_asset, export, mat

PAINT_Y = 0.012


# =============================================================== building parts
def gable_roof(b, W, z_front, z_back, y_eave, pitch_deg, over_side=0.35, thick=0.16,
               tile="HK_RoofTile", ridge_m="HK_RoofRidge", ridge_size=0.34, gable_wall=None,
               wall_front=None, wall_back=None, y_wall=None):
    """Gable roof with ridge parallel to X. Eaves at z_front/z_back (world, including overhang)."""
    t = math.tan(math.radians(pitch_deg))
    zr = (z_front + z_back) / 2
    yr = y_eave + (z_front - zr) * t
    x0, x1 = -W / 2 - over_side, W / 2 + over_side
    # top surfaces
    b.face([(x0, y_eave, z_front), (x1, y_eave, z_front), (x1, yr, zr), (x0, yr, zr)], tile)
    b.face([(x1, y_eave, z_back), (x0, y_eave, z_back), (x0, yr, zr), (x1, yr, zr)], tile)
    # undersides
    yu = y_eave - thick
    yru = yr - thick
    b.face([(x1, yu, z_front), (x0, yu, z_front), (x0, yru, zr), (x1, yru, zr)], "HK_WoodDark")
    b.face([(x0, yu, z_back), (x1, yu, z_back), (x1, yru, zr), (x0, yru, zr)], "HK_WoodDark")
    # fascia (front/back edges)
    b.face([(x0, yu, z_front), (x1, yu, z_front), (x1, y_eave, z_front), (x0, y_eave, z_front)], "HK_WoodDark")
    b.face([(x1, yu, z_back), (x0, yu, z_back), (x0, y_eave, z_back), (x1, y_eave, z_back)], "HK_WoodDark")
    # verge boards (side edges)
    for x, s in ((x0, -1), (x1, 1)):
        pts = [(x, yu, z_front), (x, y_eave, z_front), (x, yr, zr), (x, y_eave, z_back), (x, yu, z_back), (x, yru, zr)]
        if s < 0:
            b.face([pts[0], pts[5], pts[2], pts[1]], "HK_WoodDark")
            b.face([pts[5], pts[4], pts[3], pts[2]], "HK_WoodDark")
        else:
            b.face([pts[0], pts[1], pts[2], pts[5]], "HK_WoodDark")
            b.face([pts[5], pts[2], pts[3], pts[4]], "HK_WoodDark")
    # ridge cap + onigawara
    b.box((0, yr + ridge_size * 0.35, zr), (x1 - x0 + 0.1, ridge_size, ridge_size), ridge_m)
    for x in (x0 + 0.05, x1 - 0.05):
        b.box((x, yr + ridge_size * 0.6, zr), (0.18, ridge_size * 1.7, ridge_size * 1.5), ridge_m)
    # gable wall triangles
    if gable_wall and wall_front is not None:
        wf, wb, yw = wall_front, wall_back, y_wall
        ytop = yru
        for x, s in ((-W / 2, -1), (W / 2, 1)):
            tri = [(x, yw, wb), (x, yw, wf), (x, ytop, zr)] if s > 0 else [(x, yw, wf), (x, yw, wb), (x, ytop, zr)]
            b.face(tri, gable_wall)
    return yr


def lean_to(b, W, z_wall, depth, y_top, drop, over_side=0.15, thick=0.1, tile="HK_RoofTile"):
    """Hisashi (pent roof) projecting towards +Z from a wall at z_wall."""
    x0, x1 = -W / 2 - over_side, W / 2 + over_side
    zf = z_wall + depth
    yb = y_top - drop
    b.face([(x0, yb, zf), (x1, yb, zf), (x1, y_top, z_wall), (x0, y_top, z_wall)], tile)
    b.face([(x1, yb - thick, zf), (x0, yb - thick, zf), (x0, y_top - thick, z_wall), (x1, y_top - thick, z_wall)], "HK_WoodDark")
    b.face([(x0, yb - thick, zf), (x1, yb - thick, zf), (x1, yb, zf), (x0, yb, zf)], "HK_WoodDark")
    for x, s in ((x0, -1), (x1, 1)):
        q = [(x, yb - thick, zf), (x, yb, zf), (x, y_top, z_wall), (x, y_top - thick, z_wall)]
        b.face(q if s > 0 else list(reversed(q)), "HK_WoodDark")
    # ridge bead where it meets the wall
    b.box((0, y_top + 0.06, z_wall + 0.08), (x1 - x0, 0.14, 0.18), "HK_RoofRidge")


def lattice(b, x0, x1, y0, y1, z, pitch=0.12, slat=0.045, depth=0.06, m="HK_WoodMid"):
    n = max(1, int((x1 - x0) / pitch))
    step = (x1 - x0) / n
    for i in range(n + 1):
        b.box((x0 + i * step, (y0 + y1) / 2, z), (slat, y1 - y0, depth), m)
    b.box(((x0 + x1) / 2, y0 + 0.04, z), (x1 - x0 + slat, 0.08, depth + 0.02), m)
    b.box(((x0 + x1) / 2, y1 - 0.04, z), (x1 - x0 + slat, 0.08, depth + 0.02), m)


def mushiko(b, cx, y0, w, h, z):
    b.box((cx, y0 + h / 2, z - 0.02), (w, h, 0.04), "HK_WoodDark")
    n = 5
    for i in range(n):
        x = cx - w / 2 + (i + 0.5) * w / n
        b.box((x, y0 + h / 2, z + 0.03), (0.09, h, 0.06), "HK_Plaster")
    b.box((cx, y0 - 0.04, z + 0.03), (w + 0.2, 0.08, 0.08), "HK_Plaster")
    b.box((cx, y0 + h + 0.04, z + 0.03), (w + 0.2, 0.08, 0.08), "HK_Plaster")


def shoji_window(b, cx, y0, w, h, z, cols=4):
    b.box((cx, y0 + h / 2, z - 0.02), (w, h, 0.04), "HK_Shoji")
    for i in range(cols + 1):
        b.box((cx - w / 2 + i * w / cols, y0 + h / 2, z + 0.02), (0.05, h, 0.05), "HK_WoodDark")
    for yy in (y0, y0 + h / 2, y0 + h):
        b.box((cx, yy, z + 0.02), (w + 0.05, 0.05, 0.05), "HK_WoodDark")


def body(b, W, D, H, z_front=-0.5):
    zc = (z_front - D) / 2
    b.box((0, H / 2, zc), (W, H, D + z_front), "HK_Plaster")
    # stone plinth
    b.box((0, 0.12, zc), (W + 0.06, 0.24, D + z_front + 0.06), "HK_StoneDark")
    # dark corner posts & floor beam on front
    for x in (-W / 2 + 0.11, W / 2 - 0.11):
        b.box((x, H / 2, z_front + 0.03), (0.22, H, 0.12), "HK_WoodDark")
    # side timber frame lines (sides visible at gaps)
    for x in (-W / 2 - 0.02, W / 2 + 0.02):
        b.box((x, 3.25, zc), (0.05, 0.22, D + z_front), "HK_WoodDark")


def machiya(name, W=7.2, D=9.0, variant="A", seed=0):
    clear_asset(name)
    b = MB(name)
    rnd = random.Random(seed)
    zf = -0.5
    H = {"A": 5.5, "B": 6.1, "C": 6.5}[variant]
    body(b, W, D, H, zf)
    # ground floor front band
    b.box((0, 3.2, zf + 0.06), (W, 0.28, 0.14), "HK_WoodDark")
    door_w = 1.7
    door_x = W / 2 - 0.4 - door_w / 2 if variant != "B" else 0.0
    if variant in ("A", "C"):
        # dark backing + fine lattice (koshi) either side of the entrance
        b.box((0, 1.65, zf + 0.02), (W - 0.3, 2.9, 0.04), "HK_WoodDark")
        lattice(b, -W / 2 + 0.3, door_x - door_w / 2 - 0.1, 0.35, 2.95, zf + 0.09)
        if door_x + door_w / 2 + 0.1 < W / 2 - 0.3:
            lattice(b, door_x + door_w / 2 + 0.1, W / 2 - 0.3, 0.35, 2.95, zf + 0.09)
        # entrance: recessed sliding doors + noren
        b.box((door_x, 1.2, zf + 0.0), (door_w, 2.4, 0.05), "HK_Glass")
        b.box((door_x, 2.5, zf + 0.12), (door_w + 0.2, 0.9, 0.03), "HK_Noren" if rnd.random() < 0.6 else "HK_NorenRed")
        # inuyarai (curved bamboo fence) as a low sloped rail
        b.box((-W / 4 - 0.3, 0.45, zf + 0.45), (W / 2 - 0.8, 0.5, 0.06), "HK_WoodDark")
    else:
        # shop front: posts, glass panes, kick boards, centre door
        b.box((0, 0.3, zf + 0.06), (W - 0.2, 0.6, 0.1), "HK_WoodMid")
        for x in (-W / 2 + 0.3, -1.0, 1.0, W / 2 - 0.3):
            b.box((x, 1.7, zf + 0.1), (0.14, 2.8, 0.14), "HK_WoodDark")
        b.box((-(W / 2 + 1.0) / 2 + 0.15, 1.6, zf + 0.04), (W / 2 - 1.3, 2.0, 0.04), "HK_Glass")
        b.box(((W / 2 + 1.0) / 2 - 0.15, 1.6, zf + 0.04), (W / 2 - 1.3, 2.0, 0.04), "HK_Glass")
        b.box((0, 1.25, zf + 0.0), (1.9, 2.5, 0.04), "HK_Glass")
        b.box((0, 2.55, zf + 0.12), (2.1, 0.8, 0.03), "HK_Noren")
        # hanging lantern
        b.cyl((1.35, 2.2, zf + 0.5), 0.22, 0.55, "HK_NorenRed", seg=8)
        b.box((1.35, 2.85, zf + 0.5), (0.03, 0.3, 0.03), "HK_WoodDark")
    # hisashi over the ground floor
    lean_to(b, W, zf, 1.15, 3.55, 0.42)
    # upper floor
    if variant == "A":
        for cx in (-W / 4, W / 4):
            mushiko(b, cx, 4.05, min(1.6, W / 2 - 0.6), 0.75, zf)
    elif variant == "B":
        shoji_window(b, 0, 4.15, min(2.8, W - 1.6), 1.1, zf)
        # kanban (shop sign board) on the upper wall
        b.box((0, 3.85, zf + 0.12), (2.6, 0.45, 0.08), "HK_WoodMid")
        b.box((0, 3.85, zf + 0.17), (2.3, 0.3, 0.02), "HK_WoodDark")
    else:
        shoji_window(b, -W / 4, 4.1, W / 2 - 0.8, 1.35, zf, cols=4)
        shoji_window(b, W / 4, 4.1, W / 2 - 0.8, 1.35, zf, cols=4)
        # balcony rail (koran)
        b.box((0, 4.25, zf + 0.55), (W - 0.2, 0.08, 0.08), "HK_WoodDark")
        b.box((0, 3.9, zf + 0.55), (W - 0.2, 0.06, 0.06), "HK_WoodDark")
        for i in range(9):
            b.box((-W / 2 + 0.1 + i * (W - 0.2) / 8, 4.0, zf + 0.55), (0.07, 0.5, 0.07), "HK_WoodDark")
        b.box((0, 3.7, zf + 0.3), (W - 0.2, 0.08, 0.6), "HK_WoodDark")
    # main roof (hira-iri: ridge parallel to the street)
    pitch = {"A": 23, "B": 24, "C": 25}[variant]
    gable_roof(b, W, zf + 0.85, -D - 0.6, H + 0.05, pitch, gable_wall="HK_Plaster",
               wall_front=zf, wall_back=-D, y_wall=H)
    ob = b.finish()
    return ob


def kura(name, W=6.0, D=8.0, seed=0):
    clear_asset(name)
    b = MB(name)
    zf = -0.4
    H = 6.2
    body(b, W, D, H, zf)
    # namako walls on the lower part (front and both sides)
    b.box((0, 0.8, zf + 0.03), (W - 0.1, 1.3, 0.04), "HK_Namako")
    for x in (-W / 2 - 0.03, W / 2 + 0.03):
        b.box((x, 0.8, (zf - D) / 2), (0.04, 1.3, D + zf - 0.1), "HK_Namako")
    # heavy door with plaster frame and small pent roof
    b.box((0, 1.25, zf + 0.12), (2.2, 2.5, 0.22), "HK_Plaster")
    b.box((0, 1.1, zf + 0.25), (1.5, 2.2, 0.06), "HK_WoodDark")
    b.box((0, 2.75, zf + 0.4), (2.6, 0.12, 0.7), "HK_RoofTile")
    # upper windows with shutters
    for cx in (-W / 4 + 0.2, W / 4 - 0.2):
        b.box((cx, 4.4, zf + 0.06), (0.9, 0.9, 0.12), "HK_WoodDark")
        b.box((cx, 4.4, zf + 0.14), (1.3, 1.1, 0.1), "HK_Plaster")
        b.box((cx, 4.4, zf + 0.2), (0.85, 0.85, 0.04), "HK_Metal")
    # dark band under eaves
    b.box((0, H - 0.2, zf + 0.04), (W, 0.4, 0.06), "HK_WoodDark")
    gable_roof(b, W, zf + 0.7, -D - 0.7, H + 0.05, 26, over_side=0.45, thick=0.22, ridge_size=0.46,
               gable_wall="HK_Plaster", wall_front=zf, wall_back=-D, y_wall=H)
    # family crest disk on gable side
    for x, s in ((-W / 2 - 0.05, -1), (W / 2 + 0.05, 1)):
        b.cyl((x, H + 1.2, (zf - D) / 2), 0.35, 0.02, "HK_WoodDark", seg=10)
    return b.finish()


def dobei(name, L=5.0):
    clear_asset(name)
    b = MB(name)
    b.box((0, 0.25, 0), (L, 0.5, 0.62), "HK_Stone")
    b.box((0, 1.35, 0), (L, 1.7, 0.42), "HK_Plaster")
    b.box((0, 2.12, 0), (L, 0.16, 0.46), "HK_WoodDark")
    # coping roof
    y0, y1 = 2.2, 2.55
    for s in (1, -1):
        q = [(-L / 2, y0, 0.55 * s), (L / 2, y0, 0.55 * s), (L / 2, y1, 0), (-L / 2, y1, 0)]
        b.face(q if s > 0 else [q[1], q[0], q[3], q[2]], "HK_RoofTile")
        b.face([(-L / 2, y0 - 0.06, 0.55 * s), (L / 2, y0 - 0.06, 0.55 * s), (L / 2, y0, 0.55 * s), (-L / 2, y0, 0.55 * s)] if s > 0 else
               [(L / 2, y0 - 0.06, -0.55), (-L / 2, y0 - 0.06, -0.55), (-L / 2, y0, -0.55), (L / 2, y0, -0.55)], "HK_RoofTile")
    for x, s in ((-L / 2, -1), (L / 2, 1)):
        tri = [(x, y0, -0.55), (x, y0, 0.55), (x, y1, 0)]
        b.face(tri if s > 0 else list(reversed(tri)), "HK_RoofTile")
    b.box((0, y1 + 0.06, 0), (L, 0.14, 0.18), "HK_RoofRidge")
    return b.finish()


def gate(name, span=4.6, H=3.6):
    clear_asset(name)
    b = MB(name)
    for x in (-span / 2, span / 2):
        b.box((x, 0.2, 0), (0.7, 0.4, 0.7), "HK_Stone")
        b.box((x, H / 2, 0), (0.36, H, 0.36), "HK_WoodDark")
        b.box((x, 1.3, -0.9), (0.24, 2.6, 0.24), "HK_WoodDark")
        b.box((x, 2.55, -0.45), (0.2, 0.2, 1.1), "HK_WoodDark")
    b.box((0, H - 0.25, 0), (span + 0.9, 0.34, 0.34), "HK_WoodDark")
    b.box((0, H - 0.8, 0), (span, 0.22, 0.25), "HK_WoodDark")
    gable_roof(b, span + 0.4, 1.0, -1.4, H + 0.1, 28, over_side=0.4, thick=0.12, ridge_size=0.3)
    return b.finish()


# =============================================================== roads
def _road_paint_strip(b, pts, w, y=PAINT_Y):
    """Paint a polyline strip (x,z) of width w."""
    for i in range(len(pts) - 1):
        (ax, az), (bx, bz) = pts[i], pts[i + 1]
        dx, dz = bx - ax, bz - az
        l = math.hypot(dx, dz) or 1
        nx, nz = -dz / l * w / 2, dx / l * w / 2
        q = [(ax - nx, az - nz), (bx - nx, bz - nz), (bx + nx, bz + nz), (ax + nx, az + nz)]
        # ensure upward facing (clockwise on north-up map)
        area = sum(q[k][0] * q[(k + 1) % 4][1] - q[(k + 1) % 4][0] * q[k][1] for k in range(4))
        if area > 0:
            q = list(reversed(q))
        b.quad_up(q, y, "HK_WhitePaint")


def _rect(b, x0, x1, z0, z1, y, m):
    b.quad_up([(x0, z0), (x0, z1), (x1, z1), (x1, z0)], y, m)


# The original road colliders put the carriageway at y 0.01 and sidewalks at y 0.41, so the
# modules are placed at y 0.01 and sidewalks rise WALK_H above the carriageway.
WALK_H = 0.40


def _sidewalk_poly(b, pts, curb_edge=None):
    b.prism(pts, -0.05, WALK_H + 0.004, "HK_Sidewalk", m_top="HK_Sidewalk")


def _curb_line(b, pts):
    """Granite curb along a polyline (x,z), 0.18 wide, flush with the sidewalk top."""
    for i in range(len(pts) - 1):
        (ax, az), (bx, bz) = pts[i], pts[i + 1]
        cx, cz = (ax + bx) / 2, (az + bz) / 2
        l = math.hypot(bx - ax, bz - az)
        yaw = math.degrees(math.atan2(bz - az, bx - ax))
        b.box((cx, (WALK_H + 0.02) / 2, cz), (l + 0.02, WALK_H + 0.02, 0.18), "HK_Curb", rot_y=-yaw)


def _arc(cx, cz, r, a0, a1, n):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cz + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


def _zebra(b, axis, center, half_len=1.875, across=4.0):
    """axis 'x': bars run along x (crossing a road that runs along x)."""
    k = -across + 0.4
    while k <= across - 0.35:
        if axis == "x":
            _rect(b, center - half_len, center + half_len, k, k + 0.45, PAINT_Y, "HK_WhitePaint")
        else:
            _rect(b, k, k + 0.45, center - half_len, center + half_len, PAINT_Y, "HK_WhitePaint")
        k += 0.9


def road_straight(name="HK_Road_Straight", surface_only=False):
    """surface_only: asphalt + markings over the whole tile; the raised sidewalks/curbs come
    from the original road meshes (HK_RoadRaised) so they match the colliders exactly."""
    clear_asset(name)
    b = MB(name)
    _rect(b, -5, 5, -10 if surface_only else -4, 10 if surface_only else 4, 0.004, "HK_Asphalt")
    for s in (1, -1):
        z0, z1 = (4, 10) if s > 0 else (-10, -4)
        if not surface_only:
            _sidewalk_poly(b, [(-5, z0), (-5, z1), (5, z1), (5, z0)])
            _curb_line(b, [(-5, 4.09 * s), (5, 4.09 * s)])
        _rect(b, -5, 5, 3.55 * s - 0.075, 3.55 * s + 0.075, PAINT_Y, "HK_WhitePaint")
    _rect(b, -2.5, 2.5, -0.075, 0.075, PAINT_Y, "HK_WhitePaint")
    return b.finish()


def _corner(b, sx, sz, r=4.5, c=8.5, n=8):
    """Rounded sidewalk corner in quadrant (sx,sz) of a 20x20 junction tile."""
    cx, cz = c * sx, c * sz
    base_a = math.degrees(math.atan2(-sz, -sx))  # direction from arc centre to junction centre
    arc = _arc(cx, cz, r, base_a - 45, base_a + 45, n)
    pts = [(10 * sx, 10 * sz)]
    # walk: outer corner -> along x edge -> arc -> along z edge
    pts += [(10 * sx, 4 * sz), (c * sx, 4 * sz)]
    a_pts = arc if math.hypot(arc[0][0] - c * sx, arc[0][1] - 4 * sz) < math.hypot(arc[-1][0] - c * sx, arc[-1][1] - 4 * sz) else list(reversed(arc))
    pts += a_pts[1:-1]
    pts += [(4 * sx, c * sz), (4 * sx, 10 * sz)]
    area = sum(pts[k][0] * pts[(k + 1) % len(pts)][1] - pts[(k + 1) % len(pts)][0] * pts[k][1] for k in range(len(pts)))
    if area > 0:
        pts = list(reversed(pts))
    _sidewalk_poly(b, pts)
    curb = [(10 * sx, 4.09 * sz), (c * sx, 4.09 * sz)] + [(cx + (p[0] - cx) * (r - 0.09) / r, cz + (p[1] - cz) * (r - 0.09) / r) for p in a_pts[1:-1]] + [(4.09 * sx, c * sz), (4.09 * sx, 10 * sz)]
    _curb_line(b, curb)


def road_cross(name="HK_Road_Cross", tee=False, surface_only=False, east_crosswalk=True):
    """east_crosswalk=False leaves out the crosswalk on the +x edge (the Honmachi T lies 2 m west of
    the Kyobashi junction, whose own crosswalk already crosses that street: two side by side is wrong)."""
    clear_asset(name)
    b = MB(name)
    _rect(b, -10, 10, -10, 10, 0.004, "HK_Asphalt")
    if not surface_only:
        for sx in (1, -1):
            for sz in (1, -1):
                if tee and sz > 0:
                    continue
                _corner(b, sx, sz)
    if tee:
        if not surface_only:
            _sidewalk_poly(b, [(-10, 4), (-10, 10), (10, 10), (10, 4)])
            _curb_line(b, [(-10, 4.09), (10, 4.09)])
        _rect(b, -6, 6, 3.475, 3.625, PAINT_Y, "HK_WhitePaint")
    # crosswalks at the tile edges (6.25..10 from centre), matching the original layout
    _zebra(b, "x", -8.125)
    if east_crosswalk:
        _zebra(b, "x", 8.125)
    _zebra(b, "z", -8.125)
    if not tee:
        _zebra(b, "z", 8.125)
    return b.finish()


def road_turn(name="HK_Road_Turn", surface_only=False, outer_line=True):
    """Connects the -X edge and the -Z edge (arc centred on the (-10,-10) corner)."""
    clear_asset(name)
    b = MB(name)
    _rect(b, -10, 10, -10, 10, 0.004, "HK_Asphalt")
    if not surface_only:
        inner = [(-10, -10)] + _arc(-10, -10, 6, 90, 0, 10)
        _sidewalk_poly(b, inner if sum(inner[k][0] * inner[(k + 1) % len(inner)][1] - inner[(k + 1) % len(inner)][0] * inner[k][1] for k in range(len(inner))) < 0 else list(reversed(inner)))
        outer = [(-10, 10), (10, 10), (10, -10)] + _arc(-10, -10, 14, 0, 90, 14)
        outer = outer if sum(outer[k][0] * outer[(k + 1) % len(outer)][1] - outer[(k + 1) % len(outer)][0] * outer[k][1] for k in range(len(outer))) < 0 else list(reversed(outer))
        _sidewalk_poly(b, outer)
        _curb_line(b, _arc(-10, -10, 6.09, 0, 90, 10))
        _curb_line(b, _arc(-10, -10, 13.91, 0, 90, 14))
    _road_paint_strip(b, _arc(-10, -10, 6.45, 0, 90, 12), 0.15)
    if outer_line:
        _road_paint_strip(b, _arc(-10, -10, 13.55, 0, 90, 16), 0.15)
    arc = _arc(-10, -10, 10, 0, 90, 16)
    for i in range(0, 16, 2):
        _road_paint_strip(b, arc[i:i + 2], 0.15)
    return b.finish()


def road_raised(name, tiles, geometry_path, drop_z_above=None, drop_tiles=(), corridor=None, clip_moat=False):
    """Sidewalks, curbs and corner ramps copied from the original road meshes (world space),
    re-skinned with Hikone paving. The flat carriageway (y <= 0.05) is left to the surface
    modules. corridor=(tile, x0, x1): cut a straight road corridor through one tile."""
    import json
    import bmesh as _bm
    from mathutils import Vector as _V
    clear_asset(name)
    b = MB(name)
    data = json.load(open(geometry_path))
    for tile in tiles:
        for tri in data[tile]:
            pts = [tuple(tri[i:i + 3]) for i in (0, 3, 6)]
            if max(p[1] for p in pts) <= 0.05:
                continue
            cz = sum(p[2] for p in pts) / 3
            if tile in drop_tiles and drop_z_above is not None and cz > drop_z_above:
                continue
            ax, ay, az = (pts[1][i] - pts[0][i] for i in range(3))
            bx, by, bz = (pts[2][i] - pts[0][i] for i in range(3))
            nx, ny, nz = ay * bz - az * by, az * bx - ax * bz, ax * by - ay * bx
            ln = math.sqrt(nx * nx + ny * ny + nz * nz) or 1.0
            flat_top = ny / ln > 0.85          # sidewalks and corner ramps; steep faces are curbs
            b.face(pts, "HK_Sidewalk" if flat_top else "HK_Curb")
    if clip_moat:
        # original sidewalk triangles that straddle the bank line would stick out over the water
        # (the bridge has its own sidewalks): cut at both banks and drop what lies over the moat
        from hk_terrain import TOWN_Z as _tz, PEN as _pen
        bm = b.bm
        for z in (_tz, _pen[2]):      # unity z -> blender -y
            geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
            _bm.ops.bisect_plane(bm, geom=geom, dist=1e-4, plane_co=_V((0, -z, 0)), plane_no=_V((0, 1, 0)))
        dead = [f for f in bm.faces if _tz + 1e-3 < -f.calc_center_median().y < _pen[2] - 1e-3]
        _bm.ops.delete(bm, geom=dead, context='FACES')
    if corridor:
        x0, x1 = corridor
        bm = b.bm
        for x in (x0, x1):   # unity x -> blender -x
            geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
            _bm.ops.bisect_plane(bm, geom=geom, dist=1e-4, plane_co=_V((-x, 0, 0)), plane_no=_V((1, 0, 0)))
        dead = [f for f in bm.faces if x0 + 1e-3 < -f.calc_center_median().x < x1 - 1e-3]
        _bm.ops.delete(bm, geom=dead, context='FACES')
        # curb faces along the cut, facing into the corridor
        uvl = b.uv
        for e in list(bm.edges):
            if not e.is_boundary:
                continue
            v0, v1 = e.verts
            for x, facing in ((x0, 1), (x1, -1)):
                if abs(-v0.co.x - x) < 2e-3 and abs(-v1.co.x - x) < 2e-3 and max(v0.co.z, v1.co.z) > 0.2:
                    p0 = (x, v0.co.z, -v0.co.y); p1 = (x, v1.co.z, -v1.co.y)
                    q = [p0, p1, (x, 0.0, p1[2]), (x, 0.0, p0[2])]
                    # orient: normal.x must equal facing
                    ux, uy, uz = (q[1][i] - q[0][i] for i in range(3))
                    wx, wy, wz = (q[2][i] - q[0][i] for i in range(3))
                    if (uy * wz - uz * wy) * facing < 0:
                        q = list(reversed(q))
                    b.face(q, "HK_Curb")
    return b.finish()


# =============================================================== props
def street_lamp(name="HK_StreetLamp"):
    clear_asset(name)
    b = MB(name)
    b.box((0, 0.15, 0), (0.36, 0.3, 0.36), "HK_StoneDark")
    b.box((0, 2.5, 0), (0.15, 4.8, 0.15), "HK_Metal")
    b.box((0, 4.85, 0.6), (0.08, 0.08, 1.3), "HK_Metal")
    b.box((0, 4.62, 0.25), (0.05, 0.4, 0.05), "HK_Metal")
    # lantern (andon-like box with a hipped cap)
    lz = 1.2
    b.box((0, 4.55, lz), (0.44, 0.55, 0.44), "HK_Lamp")
    for dx in (-0.22, 0.22):
        for dz in (-0.22, 0.22):
            b.box((dx, 4.55, lz + dz), (0.05, 0.6, 0.05), "HK_Metal")
    b.face([(-0.34, 4.85, lz - 0.34), (0.34, 4.85, lz - 0.34), (0.0, 5.05, lz)], "HK_Metal")
    b.face([(0.34, 4.85, lz - 0.34), (0.34, 4.85, lz + 0.34), (0.0, 5.05, lz)], "HK_Metal")
    b.face([(0.34, 4.85, lz + 0.34), (-0.34, 4.85, lz + 0.34), (0.0, 5.05, lz)], "HK_Metal")
    b.face([(-0.34, 4.85, lz + 0.34), (-0.34, 4.85, lz - 0.34), (0.0, 5.05, lz)], "HK_Metal")
    b.face([(-0.34, 4.85, lz + 0.34), (0.34, 4.85, lz + 0.34), (0.34, 4.85, lz - 0.34), (-0.34, 4.85, lz - 0.34)], "HK_Metal")
    return b.finish()


def bollard(name="HK_Bollard"):
    clear_asset(name)
    b = MB(name)
    b.box((0, 0.4, 0), (0.2, 0.8, 0.2), "HK_Stone")
    b.box((0, 0.84, 0), (0.26, 0.08, 0.26), "HK_StoneDark")
    return b.finish()


def guardrail(name="HK_Guardrail", L=2.0):
    clear_asset(name)
    b = MB(name)
    for x in (-L / 2 + 0.05, L / 2 - 0.05):
        b.cyl((x, 0, 0), 0.04, 0.95, "HK_Guard", seg=6)
    for y in (0.45, 0.85):
        b.box((0, y, 0), (L, 0.06, 0.06), "HK_Guard")
    return b.finish()


def _sign_pole(b, h=2.9):
    b.cyl((0, 0, 0), 0.035, h, "HK_MetalLight", seg=6)


def sign_stop(name="HK_Sign_Stop"):
    clear_asset(name)
    b = MB(name)
    _sign_pole(b)
    y = 2.45; z = 0.06
    s = 0.44
    tri = lambda k: [(-k, y + k * 0.577, z), (k, y + k * 0.577, z), (0, y - k * 1.155, z)]
    t = tri(s + 0.05); b.face([t[1], t[0], t[2]], "HK_WhitePaint")
    t = tri(s); t = [(p[0], p[1], p[2] + 0.01) for p in t]; b.face([t[1], t[0], t[2]], "HK_SignRed")
    b.box((0, y, z - 0.03), (0.06, 0.5, 0.03), "HK_MetalLight")
    return b.finish()


def sign_square(name, face_m, glyph):
    clear_asset(name)
    b = MB(name)
    _sign_pole(b)
    y = 2.5; z = 0.06
    b.box((0, y, z), (0.62, 0.62, 0.02), "HK_WhitePaint")
    b.box((0, y, z + 0.012), (0.56, 0.56, 0.01), face_m)
    zz = z + 0.02
    if glyph == "crosswalk":
        b.face([(-0.2, y - 0.17, zz), (0.2, y - 0.17, zz), (0.0, y + 0.2, zz)], "HK_WhitePaint")
        b.box((0, y - 0.03, zz + 0.005), (0.05, 0.16, 0.005), "HK_Metal")
        b.box((0, y + 0.07, zz + 0.005), (0.06, 0.06, 0.005), "HK_Metal")
        for i in range(4):
            b.box((-0.15 + i * 0.1, y - 0.2, zz), (0.05, 0.03, 0.005), "HK_WhitePaint")
    return b.finish()


def sign_school(name="HK_Sign_School"):
    clear_asset(name)
    b = MB(name)
    _sign_pole(b)
    y = 2.45; z = 0.06; r = 0.42
    b.face([(0, y - r - 0.04, z), (r + 0.04, y, z), (0, y + r + 0.04, z), (-r - 0.04, y, z)], "HK_Metal")
    b.face([(0, y - r, z + 0.01), (r, y, z + 0.01), (0, y + r, z + 0.01), (-r, y, z + 0.01)], "HK_SignYellow")
    # two child silhouettes (big + small)
    for cx, s in ((-0.08, 1.0), (0.12, 0.75)):
        b.box((cx, y - 0.02 * s, z + 0.02), (0.08 * s, 0.22 * s, 0.005), "HK_Metal")
        b.box((cx, y + 0.13 * s, z + 0.02), (0.07 * s, 0.07 * s, 0.005), "HK_Metal")
        b.box((cx - 0.03 * s, y - 0.18 * s, z + 0.02), (0.03 * s, 0.14 * s, 0.005), "HK_Metal")
        b.box((cx + 0.03 * s, y - 0.18 * s, z + 0.02), (0.03 * s, 0.14 * s, 0.005), "HK_Metal")
    return b.finish()


def guide_post(name="HK_GuidePost"):
    """Wooden direction post (michishirube) pointing along +X."""
    clear_asset(name)
    b = MB(name)
    b.box((0, 1.2, 0), (0.18, 2.4, 0.18), "HK_WoodMid")
    b.box((0.55, 2.0, 0), (1.1, 0.26, 0.05), "HK_WoodLight")
    b.face([(1.1, 1.87, 0.026), (1.3, 2.0, 0.026), (1.1, 2.13, 0.026)], "HK_WoodLight")
    b.box((0.55, 1.6, 0), (1.0, 0.22, 0.05), "HK_WoodLight")
    b.box((0, 2.45, 0), (0.26, 0.1, 0.26), "HK_RoofTile")
    return b.finish()


def bench(name="HK_Bench"):
    clear_asset(name)
    b = MB(name)
    for x in (-0.75, 0.75):
        b.box((x, 0.2, 0), (0.12, 0.4, 0.45), "HK_StoneDark")
    for i in range(4):
        b.box((0, 0.43, -0.16 + i * 0.105), (1.8, 0.05, 0.08), "HK_WoodLight")
    return b.finish()


def planter(name="HK_Planter"):
    clear_asset(name)
    b = MB(name)
    b.box((0, 0.25, 0), (1.3, 0.5, 0.55), "HK_WoodMid", top=False)
    b.box((0, 0.45, 0), (1.2, 0.02, 0.45), "HK_Soil")
    b.blob((-0.3, 0.75, 0), 0.38, "HK_Leaf", sy=0.8, seed=3)
    b.blob((0.3, 0.72, 0), 0.34, "HK_LeafDark", sy=0.8, seed=4)
    return b.finish()


# =============================================================== nature
def tree_round(name="HK_Tree_Round", leaf="HK_Leaf", seed=1, h=7.0, subdiv=2):
    clear_asset(name)
    b = MB(name)
    rnd = random.Random(seed)
    b.cyl((0, 0, 0), 0.2, h * 0.5, "HK_Bark", seg=6, r_top=0.12)
    b.cyl((0, h * 0.42, 0), 0.1, h * 0.2, "HK_Bark", seg=5, r_top=0.05)
    for i in range(4):
        a = rnd.uniform(0, 2 * math.pi)
        rr = 0.0 if i == 0 else rnd.uniform(0.8, 1.3)
        b.blob((math.cos(a) * rr, h * (0.68 if i == 0 else rnd.uniform(0.55, 0.72)), math.sin(a) * rr),
               h * (0.3 if i == 0 else rnd.uniform(0.2, 0.25)), leaf, sy=0.85, seed=seed * 10 + i, subdiv=subdiv)
    return b.finish()


def tree_pine(name="HK_Tree_Pine", seed=2):
    """Japanese black pine: leaning trunk with layered, flattened needle pads."""
    clear_asset(name)
    b = MB(name)
    rnd = random.Random(seed)
    pts = [(0, 0, 0), (0.5, 2.2, 0.2), (0.2, 4.2, 0.6), (0.9, 6.0, 0.3)]
    for i in range(3):
        (x0, y0, z0), (x1, y1, z1) = pts[i], pts[i + 1]
        r0 = 0.24 - 0.06 * i
        seg = 6
        ring = lambda cx, cy, cz, r: [(cx + r * math.cos(2 * math.pi * k / seg), cy, cz + r * math.sin(2 * math.pi * k / seg)) for k in range(seg)]
        ra, rb = ring(x0, y0, z0, r0), ring(x1, y1, z1, r0 - 0.06)
        for k in range(seg):
            j = (k + 1) % seg
            b.face([ra[j], ra[k], rb[k], rb[j]], "HK_Bark")
    pads = [(1.0, 6.1, 0.3, 1.6), (-0.9, 4.8, 0.4, 1.3), (1.6, 4.4, -0.5, 1.2), (0.0, 3.4, 1.2, 1.0), (0.3, 6.9, 0.2, 1.0)]
    for i, (x, y, z, r) in enumerate(pads):
        b.blob((x, y, z), r, "HK_LeafDark", sy=0.38, seed=seed * 7 + i, jitter=0.12, subdiv=1)
    for (x, y, z, r) in pads[:3]:
        b.box(((x * 0.5), y - 0.25, z * 0.5), (abs(x) + 0.2, 0.1, 0.1), "HK_Bark")
    return b.finish()


def tree_cedar(name="HK_Tree_Cedar", seed=3, h=11.0):
    clear_asset(name)
    b = MB(name)
    b.cyl((0, 0, 0), 0.22, h * 0.35, "HK_Bark", seg=6, r_top=0.16)
    for i, (y, r) in enumerate(((0.25, 2.2), (0.48, 1.7), (0.68, 1.15))):
        b.cyl((0, h * y, 0), r, h * (0.45 - 0.08 * i), "HK_LeafDark", seg=7, r_top=0.0, top=False)
        b.face([(r * math.cos(2 * math.pi * k / 7), h * y, r * math.sin(2 * math.pi * k / 7)) for k in range(7)], "HK_LeafDark")
    return b.finish()


def shrub(name="HK_Shrub", seed=5):
    clear_asset(name)
    b = MB(name)
    b.blob((0, 0.45, 0), 0.75, "HK_Leaf", sy=0.65, seed=seed)
    b.blob((0.5, 0.35, 0.2), 0.5, "HK_LeafDark", sy=0.7, seed=seed + 1)
    return b.finish()


# =============================================================== moat & castle
def ishigaki(name="HK_Ishigaki", L=10.0, H=3.4, batter=1.1, coping=True, fill=True):
    """Battered stone wall facing +Z. Top edge at y=0,z=0; foot at y=-H, z=+batter."""
    clear_asset(name)
    b = MB(name)
    n = 4
    # slight concave curve (ogi-no-kobai) by splitting the face in n rows
    prof = []
    for i in range(n + 1):
        t = i / n
        y = -H * (1 - t)
        z = batter * (1 - t) ** 1.6
        prof.append((y, z))
    for i in range(n):
        (ya, za), (yb, zb) = prof[i], prof[i + 1]
        b.face([(-L / 2, ya, za), (L / 2, ya, za), (L / 2, yb, zb), (-L / 2, yb, zb)], "HK_Stone")
    if coping:
        b.box((0, 0.08, -0.25), (L, 0.16, 0.6), "HK_StoneDark")
    if fill:
        # grass strip on the bank behind the coping
        _rect(b, -L / 2, L / 2, -2.0, -0.55, 0.0, "HK_Grass")
    return b.finish()


def water(name="HK_Water", size=40.0):
    clear_asset(name)
    b = MB(name)
    s = size / 2
    _rect(b, -s, s, -s, s, 0.0, "HK_Water")
    return b.finish()


def bridge(name="HK_Kyobashi", L=12.4, road=8.0, walk=3.0):
    """Kyobashi: road bridge along +Z from the town bank (z=0) to the castle side (z=L).
    Asphalt carriageway with markings, sidewalks, dark timber-style railing with stone
    newel posts (oyabashira) at the four ends. Carriageway at y=0 (placed at the original
    collider height 0.01); sidewalks rise WALK_H like the road tiles underneath."""
    clear_asset(name)
    b = MB(name)
    hw = road / 2 + walk
    # deck slab + stone fascia
    b.box((0, -0.45, L / 2), (2 * hw + 0.4, 0.9, L), "HK_StoneDark", top=False, bottom=True)
    _rect(b, -road / 2, road / 2, 0, L, 0.004, "HK_Asphalt")
    for sgn in (-1, 1):
        x0, x1 = sorted((sgn * road / 2, sgn * hw))
        b.prism([(x0, 0), (x0, L), (x1, L), (x1, 0)], -0.05, WALK_H + 0.004, "HK_Sidewalk")
        _curb_line(b, [(sgn * (road / 2 + 0.09), 0), (sgn * (road / 2 + 0.09), L)])
        _rect(b, sgn * (road / 2 - 0.45) - 0.075, sgn * (road / 2 - 0.45) + 0.075, 0, L, PAINT_Y, "HK_WhitePaint")
    for k in range(3):
        z0 = 0.8 + k * 4.0
        _rect(b, -0.075, 0.075, z0, min(L, z0 + 2.5), PAINT_Y, "HK_WhitePaint")
    # piers and girders under the deck
    for z in (L * 0.5,):
        for x in (-hw * 0.55, hw * 0.55):
            b.box((x, -2.4, z), (1.6, 3.2, 1.2), "HK_StoneDark")
    for x in (-hw + 0.6, 0.0, hw - 0.6):
        b.box((x, -1.1, L / 2), (0.4, 0.5, L), "HK_Metal", top=False)
    # railings: dark timber-look top rail, mid rail and balusters (on the raised sidewalk)
    ry = WALK_H - 0.1
    for sgn in (-1, 1):
        x = sgn * (hw - 0.15)
        b.box((x, ry + 1.05, L / 2), (0.16, 0.12, L), "HK_WoodDark")
        b.box((x, ry + 0.62, L / 2), (0.1, 0.08, L), "HK_WoodDark")
        b.box((x, ry + 0.16, L / 2), (0.22, 0.14, L), "HK_WoodDark")
        n = int(L / 1.55)
        for i in range(1, n):
            z = i * L / n
            b.box((x, ry + 0.6, z), (0.14, 0.95, 0.14), "HK_WoodDark")
        # stone newel posts with rounded caps at both ends
        for z in (0.0, L):
            b.box((x, ry + 0.75, z), (0.5, 1.5, 0.5), "HK_Stone")
            b.cyl((x, ry + 1.5, z), 0.24, 0.22, "HK_Stone", seg=8, r_top=0.08)
    return b.finish()


def gatehouse(name, width=16.0, depth=4.6, open_w=9.0, open_h=4.2, body_h=5.4, pitch=26, turret=False):
    """Gate building the road passes through (nagaya-mon / yagura-mon).
    Passage runs along local Z; facades face +Z (front) and -Z. Interior is dark timber so a
    black occluder at the back of the passage reads as the unlit tunnel."""
    clear_asset(name)
    b = MB(name)
    hw, hd = width / 2, depth / 2
    ow = open_w / 2
    # two solid piers beside the passage
    for sgn in (-1, 1):
        cx = sgn * (ow + (hw - ow) / 2)
        b.box((cx, open_h / 2, 0), (hw - ow, open_h, depth), "HK_Plaster")
        b.box((cx, 0.5, 0), (hw - ow + 0.04, 1.0, depth + 0.04), "HK_Namako")
        b.box((cx, open_h * 0.55, hd + 0.02), (1.2, 0.9, 0.05), "HK_WoodDark")
        b.box((cx, open_h * 0.55, -hd - 0.02), (1.2, 0.9, 0.05), "HK_WoodDark")
    # body over the passage (closed box so anything placed inside it is hidden)
    b.box((0, (open_h + body_h) / 2, 0), (width, body_h - open_h, depth), "HK_Plaster", bottom=True)
    # dark passage lining + massive gate beams and posts
    b.face([(-ow, open_h - 0.01, -hd), (ow, open_h - 0.01, -hd), (ow, open_h - 0.01, hd), (-ow, open_h - 0.01, hd)], "HK_WoodDark")
    for sgn in (-1, 1):
        x = sgn * (ow - 0.01)
        q = [(x, 0, -hd), (x, 0, hd), (x, open_h, hd), (x, open_h, -hd)]
        b.face(q if sgn > 0 else list(reversed(q)), "HK_WoodDark")
        for z in (hd - 0.2, -hd + 0.2):
            b.box((sgn * (ow + 0.2), open_h / 2, z), (0.45, open_h, 0.45), "HK_WoodDark")
    for z in (hd, -hd):
        b.box((0, open_h + 0.25, z), (open_w + 1.2, 0.5, 0.35), "HK_WoodDark")
    # upper floor windows (renji-mado)
    for sgn in (-1, 1):
        for z, s in ((hd + 0.02, 1), (-hd - 0.02, -1)):
            b.box((sgn * width * 0.28, open_h + (body_h - open_h) * 0.55, z), (1.6, 0.7, 0.05), "HK_WoodDark")
    # roof: hipped skirt + gable, like the castle turrets
    _hip_skirt(b, hw + 1.0, hd + 1.0, body_h, hw - 0.4, hd - 0.4, body_h + 1.4)
    gable_roof(b, width - 0.8, hd - 0.3, -hd + 0.3, body_h + 1.4, pitch + 6, over_side=0.3, thick=0.2, ridge_size=0.4)
    if turret:
        b.box((0, body_h + 2.4, 0), (width * 0.45, 2.0, depth - 1.0), "HK_Plaster")
        gable_roof(b, width * 0.45, hd - 0.2, -hd + 0.2, body_h + 3.4, pitch + 8, over_side=0.5, thick=0.2, ridge_size=0.4)
    return b.finish()


def chain_fence(name="HK_ChainFence", L=2.0):
    """Low stone posts with a sagging chain (moat-side edge, from the Kyobashi photo)."""
    clear_asset(name)
    b = MB(name)
    b.box((-L / 2, 0.35, 0), (0.18, 0.7, 0.18), "HK_Stone")
    for (ax, az), (bx, bz) in (((0.09, -0.09), (-0.09, -0.09)), ((-0.09, 0.09), (0.09, 0.09)),
                               ((-0.09, -0.09), (-0.09, 0.09)), ((0.09, 0.09), (0.09, -0.09))):
        b.face([(-L / 2 + ax, 0.7, az), (-L / 2 + bx, 0.7, bz), (-L / 2, 0.8, 0)], "HK_Stone")
    pts = [(-L / 2 + L * i / 3, 0.6 - 0.16 * math.sin(math.pi * i / 3)) for i in range(4)]
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        b.box((cx, cy, 0), (x1 - x0 + 0.02, 0.03 + abs(y1 - y0), 0.03), "HK_Metal")
    return b.finish()


def tree_zelkova(name="HK_Tree_Zelkova", seed=21, h=8.5):
    """Keyaki street tree, pruned as Japanese street trees are: straight trunk, vase-shaped
    canopy about 8.5 m tall and 5.5 m across (the 11 m / 10 m crown was far too big)."""
    clear_asset(name)
    b = MB(name)
    rnd = random.Random(seed)
    b.cyl((0, 0, 0), 0.28, h * 0.38, "HK_Bark", seg=6, r_top=0.2)
    for a in range(3):
        ang = a * 2 * math.pi / 3 + 0.4
        b.box((math.cos(ang) * 0.6, h * 0.46, math.sin(ang) * 0.6), (0.16, h * 0.2, 0.16), "HK_Bark")
    # vase-shaped crown: a ring of wide lobes low, a narrower ring above
    for i in range(7):
        ang = i * 2 * math.pi / 7 + rnd.uniform(-0.25, 0.25)
        rr = rnd.uniform(1.3, 1.6)
        b.blob((math.cos(ang) * rr, h * rnd.uniform(0.62, 0.7), math.sin(ang) * rr), h * rnd.uniform(0.15, 0.17),
               "HK_Leaf", sy=0.72, seed=seed * 10 + i, subdiv=1)
    for i in range(4):
        ang = i * 2 * math.pi / 4 + 0.5
        b.blob((math.cos(ang) * 0.8, h * 0.84, math.sin(ang) * 0.8), h * 0.15, "HK_Leaf", sy=0.7, seed=seed * 10 + 20 + i, subdiv=1)
    return b.finish()


def _wheel(b, cx, cy, cz, r, w, m="HK_Rubber", seg=12):
    """Wheel with its axle along z (a vertical cyl turned 90 deg about x, so windings stay outward)."""
    ring = [(math.cos(2 * math.pi * i / seg), math.sin(2 * math.pi * i / seg)) for i in range(seg)]
    P = lambda c, sn, h: (cx + r * c, cy - r * sn, cz + h)
    h0, h1 = -w / 2, w / 2
    for i in range(seg):
        j = (i + 1) % seg
        b.face([P(*ring[j], h0), P(*ring[i], h0), P(*ring[i], h1), P(*ring[j], h1)], m)
    b.face([P(c, sn, h1) for (c, sn) in reversed(ring)], m)
    b.face([P(c, sn, h0) for (c, sn) in ring], m)
    # hub caps
    for sgn in (-1, 1):
        rr = r * 0.52
        hub = [(cx + rr * c, cy - rr * sn, cz + sgn * (w / 2 + 0.005)) for (c, sn) in ring]
        b.face(list(reversed(hub)) if sgn > 0 else hub, "HK_MetalLight")


def box_truck(name, L, W, H, cab_len, cab_h, wheel_r, axles, rear_dual=True):
    """Japanese cab-over box truck (aluminium van body), as parked on town streets.
    Cab at -x, origin at the centre of the footprint on the ground. Sized to the scene's truck
    colliders so the occlusion of the parked-truck scenarios stays the same."""
    clear_asset(name)
    b = MB(name)
    xf, xr = -L / 2, L / 2
    # chassis, wheels, side guards, fuel tank
    b.box((0.25, wheel_r + 0.12, 0), (L - 0.9, 0.26, W * 0.55), "HK_Metal", bottom=True)
    for k, ax in enumerate(axles):
        dual = rear_dual and k > 0
        for sgn in (-1, 1):
            if dual:
                for off in (0.0, 0.34):
                    _wheel(b, ax, wheel_r, sgn * (W / 2 - 0.2 - off), wheel_r, 0.3)
            else:
                _wheel(b, ax, wheel_r, sgn * (W / 2 - 0.2), wheel_r, 0.3)
    span0, span1 = axles[0] + wheel_r + 0.35, axles[1] - wheel_r - 0.3
    for sgn in (-1, 1):
        for y in (0.45, 0.72):
            b.box(((span0 + span1) / 2, y, sgn * (W / 2 - 0.06)), (span1 - span0, 0.09, 0.04), "HK_MetalLight")
    b.box((xf + cab_len + 0.9, 0.78, -(W / 2 - 0.4)), (1.1, 0.48, 0.5), "HK_MetalLight")
    # cab (flat cab-over front, white), windscreen, grille, bumper, lights, mirrors, green plate
    cx = xf + cab_len / 2
    b.box((cx, (0.6 + cab_h) / 2, 0), (cab_len, cab_h - 0.6, W), "HK_TruckCab", bottom=True)
    b.box((xf + 0.55, cab_h + 0.12, 0), (1.0, 0.24, W - 0.25), "HK_TruckCab")          # sun visor / roof lip
    b.box((xf - 0.015, cab_h - 0.72, 0), (0.04, 1.0, W - 0.32), "HK_Glass")
    for sgn in (-1, 1):
        b.box((xf + cab_len * 0.42, cab_h - 0.75, sgn * (W / 2 + 0.012)), (cab_len * 0.5, 0.82, 0.03), "HK_Glass")
        b.box((xf + cab_len * 0.8, (0.9 + cab_h - 1.25) / 2 + 0.2, sgn * (W / 2 + 0.012)), (0.04, cab_h - 1.4, 0.03), "HK_Metal")  # door line
        b.box((xf + 0.12, cab_h - 0.55, sgn * (W / 2 + 0.22)), (0.07, 0.48, 0.14), "HK_Metal")   # mirror
        b.box((xf + 0.12, cab_h - 0.3, sgn * (W / 2 + 0.1)), (0.04, 0.04, 0.26), "HK_Metal")    # mirror arm
        b.box((xf - 0.03, 0.9, sgn * (W / 2 - 0.36)), (0.05, 0.2, 0.38), "HK_Lamp")           # headlight
        b.box((xf - 0.03, 0.9, sgn * (W / 2 - 0.1)), (0.05, 0.16, 0.12), "HK_SignYellow")     # indicator
    b.box((xf - 0.02, 1.2, 0), (0.04, 0.38, W * 0.62), "HK_Metal")                      # grille
    b.box((xf - 0.06, 0.62, 0), (0.16, 0.3, W + 0.02), "HK_Metal", bottom=True)         # bumper
    b.box((xf - 0.15, 0.62, 0), (0.02, 0.2, 0.33), "HK_PlateGreen")
    # aluminium van body with ribs, rails, rear doors, tail lights
    bx0 = xf + cab_len + 0.12
    y0 = wheel_r * 2 + 0.05
    bl = xr - bx0
    b.box(((bx0 + xr) / 2, (y0 + H) / 2, 0), (bl, H - y0, W), "HK_VanBody", bottom=True)
    n = max(4, int(bl / 0.62))
    for i in range(1, n):
        x = bx0 + i * bl / n
        for sgn in (-1, 1):
            b.box((x, (y0 + H) / 2, sgn * (W / 2 + 0.012)), (0.05, H - y0 - 0.1, 0.02), "HK_VanRib")
    for y in (y0 + 0.05, H - 0.06):
        for sgn in (-1, 1):
            b.box(((bx0 + xr) / 2, y, sgn * (W / 2 + 0.02)), (bl, 0.1, 0.03), "HK_VanRib")
    b.box((xr + 0.012, (y0 + H) / 2, 0), (0.02, H - y0 - 0.1, 0.05), "HK_VanRib")           # door split
    for sgn in (-1, 1):
        b.box((xr + 0.012, (y0 + H) / 2, sgn * (W / 2 - 0.05)), (0.02, H - y0 - 0.1, 0.06), "HK_VanRib")
        b.box((xr + 0.03, 0.75, sgn * (W / 2 - 0.3)), (0.04, 0.16, 0.36), "HK_SignRed")      # tail light
    b.box((xr - 0.05, 0.5, 0), (0.12, 0.14, W - 0.1), "HK_Metal")                       # rear underrun bar
    return b.finish()


def _oface(b, pts, m, inside):
    """Face whose normal points away from the point `inside` (winding picked automatically)."""
    nx = ny = nz = 0.0
    for i in range(len(pts)):
        (x0, y0, z0), (x1, y1, z1) = pts[i], pts[(i + 1) % len(pts)]
        nx += (y0 - y1) * (z0 + z1); ny += (z0 - z1) * (x0 + x1); nz += (x0 - x1) * (y0 + y1)
    c = [sum(p[k] for p in pts) / len(pts) for k in range(3)]
    if nx * (c[0] - inside[0]) + ny * (c[1] - inside[1]) + nz * (c[2] - inside[2]) < 0:
        pts = list(reversed(pts))
    b.face(pts, m)


def _extrude_x(b, profile, x0, x1, m):
    """A side profile [(z, y), ...] (star-shaped around its centroid) extruded from x0 to x1."""
    cz = sum(p[0] for p in profile) / len(profile)
    cy = sum(p[1] for p in profile) / len(profile)
    inside = ((x0 + x1) / 2, cy, cz)
    n = len(profile)
    for i in range(n):
        (za, ya), (zb, yb) = profile[i], profile[(i + 1) % n]
        _oface(b, [(x0, ya, za), (x0, yb, zb), (x1, yb, zb), (x1, ya, za)], m, inside)
        for x in (x0, x1):   # side caps as a fan from the centroid
            _oface(b, [(x, cy, cz), (x, ya, za), (x, yb, zb)], m, inside)


def _wheel_x(b, x, y, z, r, w, seg=12):
    """Car wheel, axle along x: tyre and a silver wheel cover on the outer face."""
    ring = [(math.cos(2 * math.pi * i / seg), math.sin(2 * math.pi * i / seg)) for i in range(seg)]
    x0, x1 = x - w / 2, x + w / 2
    c = (x, y, z)
    for i in range(seg):
        (ca, sa), (cb, sb) = ring[i], ring[(i + 1) % seg]
        _oface(b, [(x0, y + r * sa, z + r * ca), (x0, y + r * sb, z + r * cb), (x1, y + r * sb, z + r * cb), (x1, y + r * sa, z + r * ca)], "HK_Rubber", c)
    for xx in (x0, x1):
        _oface(b, [(xx, y + r * sn, z + r * cs) for (cs, sn) in ring], "HK_Rubber", c)
    out = x1 + 0.004 if x > 0 else x0 - 0.004
    _oface(b, [(out, y + 0.62 * r * sn, z + 0.62 * r * cs) for (cs, sn) in ring], "HK_MetalLight", (x, y, z))


def kei_car(name, profile, H, windshield, side_window, rear_window, pillars, door_lines):
    """Japanese kei car (3.395 x 1.475 m, the kei limits): painted body (HK_KeiPaint, tinted per car
    at runtime), glass, lights, bumpers, yellow kei plates, wheels. Front at +z, origin on the
    ground at the centre of the footprint, like the scene's car prefabs."""
    clear_asset(name)
    b = MB(name)
    L, W, r, wb = 3.395, 1.475, 0.28, 2.52
    hw = W / 2
    _extrude_x(b, profile, -hw, hw, "HK_KeiPaint")
    # glass, just proud of the body (side windows on both sides; windscreen and rear window as quads)
    for x in (-hw - 0.004, hw + 0.004):
        _oface(b, [(x, y, z) for (z, y) in side_window], "HK_Glass", (0.0, H * 0.7, 0.0))
        for (z0, z1) in pillars:     # body-coloured pillars over the glass
            _oface(b, [(x * 1.001, side_window[0][1], z0), (x * 1.001, side_window[0][1], z1),
                       (x * 1.001, side_window[-1][1], z1), (x * 1.001, side_window[-1][1], z0)], "HK_KeiPaint", (0.0, H * 0.7, 0.0))
        for z in door_lines:
            _oface(b, [(x * 1.002, 0.38, z - 0.01), (x * 1.002, 0.38, z + 0.01), (x * 1.002, side_window[0][1] - 0.02, z + 0.01),
                       (x * 1.002, side_window[0][1] - 0.02, z - 0.01)], "HK_Metal", (0.0, H * 0.7, 0.0))
    (zb, yb), (zt, yt) = windshield
    for (pts, ref) in (([(-hw + 0.1, yb, zb + 0.004), (hw - 0.1, yb, zb + 0.004), (hw - 0.14, yt, zt + 0.004), (-hw + 0.14, yt, zt + 0.004)], (0, 0.5, 0)),):
        _oface(b, pts, "HK_Glass", ref)
    (zr, yr0, yr1) = rear_window
    _oface(b, [(-hw + 0.14, yr0, zr - 0.004), (hw - 0.14, yr0, zr - 0.004), (hw - 0.16, yr1, zr - 0.004), (-hw + 0.16, yr1, zr - 0.004)], "HK_Glass", (0, 0.5, 0))
    zf = max(p[0] for p in profile); zrr = min(p[0] for p in profile)
    # lights, bumpers, plates, mirrors
    for sx in (-1, 1):
        b.box((sx * (hw - 0.25), 0.8, zf - 0.035), (0.34, 0.15, 0.06), "HK_Lamp")
        b.box((sx * (hw - 0.08), 0.95, zrr + 0.018), (0.12, 0.42, 0.03), "HK_SignRed")
        b.box((sx * (hw + 0.08), side_window[0][1] + 0.05, windshield[0][0] - 0.12), (0.16, 0.11, 0.08), "HK_KeiPaint")
    # bumpers, grille and plates flush with the body: the whole car stays within 3.395 m
    b.box((0, 0.36, zf - 0.055), (W - 0.02, 0.22, 0.1), "HK_KeiPaint", bottom=True)
    b.box((0, 0.44, zf - 0.004), (0.9, 0.1, 0.008), "HK_Metal")                        # lower grille
    b.box((0, 0.36, zrr + 0.055), (W - 0.02, 0.22, 0.1), "HK_KeiPaint", bottom=True)
    b.box((0, 0.58, zf - 0.004), (0.33, 0.165, 0.008), "HK_SignYellow")                # kei plates are yellow
    b.box((0, 0.72, zrr + 0.004), (0.33, 0.165, 0.008), "HK_SignYellow")
    # wheels and a dark underbody between them
    b.box((0, 0.22, 0), (W - 0.3, 0.14, L - 0.5), "HK_Metal", bottom=True)
    for sx in (-1, 1):
        for zz in (wb / 2, -wb / 2):
            _wheel_x(b, sx * (hw - 0.09), r, zz, r, 0.16)
    return b.finish()


def kei_tall():
    """Tall-wagon kei (N-BOX / Tanto type): box body 1.79 m high, short nose, sliding rear door."""
    profile = [(-1.69, 0.3), (1.66, 0.3), (1.697, 0.55), (1.68, 0.86), (1.14, 1.02), (0.62, 1.74),
               (0.48, 1.79), (-1.58, 1.79), (-1.697, 1.68)]
    return kei_car("HK_Kei_Tall", profile, 1.79, windshield=((1.1, 1.08), (0.66, 1.7)),
                   side_window=[(-1.5, 1.14), (0.99, 1.14), (0.64, 1.68), (-1.5, 1.68)],
                   rear_window=(-1.697, 1.16, 1.62), pillars=[(0.02, 0.1), (-0.95, -0.85)], door_lines=[0.06, -0.9])


def kei_hatch():
    """Two-box kei hatchback (Alto / Mira type), 1.525 m high."""
    profile = [(-1.69, 0.3), (1.66, 0.3), (1.697, 0.55), (1.66, 0.78), (0.95, 0.92), (0.28, 1.49),
               (0.05, 1.525), (-1.22, 1.525), (-1.62, 1.34), (-1.697, 0.96)]
    return kei_car("HK_Kei_Hatch", profile, 1.525, windshield=((0.92, 0.97), (0.33, 1.46)),
                   side_window=[(-1.3, 1.0), (0.84, 1.0), (0.3, 1.44), (-1.18, 1.44)],
                   rear_window=(-1.64, 1.02, 1.3), pillars=[(-0.2, -0.12)], door_lines=[-0.16])


def hedge(name="HK_Hedge", L=3.0):
    clear_asset(name)
    b = MB(name)
    b.box((0, 0.1, 0), (L, 0.2, 0.9), "HK_Curb")
    b.box((0, 0.5, 0), (L - 0.1, 0.6, 0.7), "HK_LeafDark")
    for i in range(3):
        b.blob((-L / 3 + i * L / 3, 0.78, 0), 0.5, "HK_Leaf", sy=0.5, seed=40 + i, subdiv=1)
    return b.finish()


def sign_guide(name="HK_Sign_Guide"):
    """Overhead blue direction sign on a brown landscape-colour pole; board faces +Z."""
    clear_asset(name)
    b = MB(name)
    b.cyl((0, 0, 0), 0.14, 5.6, "HK_Guard", seg=8)
    b.box((1.4, 5.3, 0), (2.8, 0.14, 0.14), "HK_Guard")
    cx, cy, z = 2.2, 4.4, 0.12
    b.box((cx, cy, z), (2.3, 1.7, 0.06), "HK_SignBlue")
    zz = z + 0.04
    b.box((cx, cy - 0.3, zz), (1.7, 0.1, 0.01), "HK_WhitePaint")        # cross street arrow
    b.face([(cx - 0.85, cy - 0.45, zz), (cx - 0.85, cy - 0.15, zz), (cx - 1.05, cy - 0.3, zz)], "HK_WhitePaint")
    b.face([(cx + 0.85, cy - 0.45, zz), (cx + 1.05, cy - 0.3, zz), (cx + 0.85, cy - 0.15, zz)], "HK_WhitePaint")
    b.box((cx, cy + 0.15, zz), (0.1, 0.9, 0.01), "HK_WhitePaint")      # ahead arrow (to the castle)
    b.face([(cx - 0.18, cy + 0.6, zz), (cx + 0.18, cy + 0.6, zz), (cx, cy + 0.82, zz)], "HK_WhitePaint")
    for dx in (-0.75, 0.55):
        b.box((cx + dx, cy + 0.45, zz), (0.5, 0.12, 0.01), "HK_WhitePaint")
    return b.finish()


def sign_speed(name="HK_Sign_Speed30"):
    clear_asset(name)
    b = MB(name)
    _sign_pole(b)
    y, z = 2.45, 0.06
    ring = [(0.3 * math.cos(2 * math.pi * k / 16), y + 0.3 * math.sin(2 * math.pi * k / 16)) for k in range(16)]
    inner = [(0.23 * math.cos(2 * math.pi * k / 16), y + 0.23 * math.sin(2 * math.pi * k / 16)) for k in range(16)]
    for k in range(16):
        j = (k + 1) % 16
        b.face([(ring[k][0], ring[k][1], z), (ring[j][0], ring[j][1], z), (inner[j][0], inner[j][1], z), (inner[k][0], inner[k][1], z)], "HK_SignRed")
    b.face([(p[0], p[1], z) for p in inner], "HK_WhitePaint")
    zz = z + 0.01
    # "30" in blue seven-segment strokes
    def seg(x0, y0, x1, y1):
        b.box(((x0 + x1) / 2, (y0 + y1) / 2, zz), (abs(x1 - x0) + 0.03, abs(y1 - y0) + 0.03, 0.005), "HK_SignBlue")
    for ox in (-0.08, 0.08):
        if ox < 0:
            seg(ox - 0.05, y + 0.12, ox + 0.05, y + 0.12); seg(ox - 0.05, y, ox + 0.05, y); seg(ox - 0.05, y - 0.12, ox + 0.05, y - 0.12)
            seg(ox + 0.05, y - 0.12, ox + 0.05, y + 0.12)
        else:
            seg(ox - 0.05, y + 0.12, ox + 0.05, y + 0.12); seg(ox - 0.05, y - 0.12, ox + 0.05, y - 0.12)
            seg(ox - 0.05, y - 0.12, ox - 0.05, y + 0.12); seg(ox + 0.05, y - 0.12, ox + 0.05, y + 0.12)
    return b.finish()


def _hip_skirt(b, x_out, z_out, y_out, x_in, z_in, y_in, m="HK_RoofTile"):
    """Four-sided sloping roof skirt between an outer eave rectangle and an inner wall rectangle."""
    O = [(-x_out, -z_out), (-x_out, z_out), (x_out, z_out), (x_out, -z_out)]
    I = [(-x_in, -z_in), (-x_in, z_in), (x_in, z_in), (x_in, -z_in)]
    for k in range(4):
        j = (k + 1) % 4
        b.face([(O[k][0], y_out, O[k][1]), (O[j][0], y_out, O[j][1]), (I[j][0], y_in, I[j][1]), (I[k][0], y_in, I[k][1])], m)
        b.face([(I[k][0], y_in - 0.25, I[k][1]), (I[j][0], y_in - 0.25, I[j][1]), (O[j][0], y_out - 0.25, O[j][1]), (O[k][0], y_out - 0.25, O[k][1])], "HK_WoodDark")
        b.face([(O[j][0], y_out - 0.25, O[j][1]), (O[j][0], y_out, O[j][1]), (O[k][0], y_out, O[k][1]), (O[k][0], y_out - 0.25, O[k][1])], "HK_WoodDark")


def _chidori(b, cx, cz, y_base, w, h, depth, facing=1):
    """Small triangular gable (chidori-hafu) on a roof skirt, facing +Z (facing=1) or -Z."""
    zf = cz + depth * facing
    zb = cz
    tri = [(cx - w / 2, y_base, zf), (cx + w / 2, y_base, zf), (cx, y_base + h, zf)]
    b.face(tri if facing > 0 else [tri[1], tri[0], tri[2]], "HK_Plaster")
    # roof slabs
    for s in (-1, 1):
        q = [(cx + s * (w / 2 + 0.3), y_base - 0.2, zf + 0.35 * facing), (cx, y_base + h + 0.1, zf + 0.35 * facing), (cx, y_base + h + 0.1, zb), (cx + s * (w / 2 + 0.3), y_base - 0.2, zb)]
        b.face(q if s * facing > 0 else list(reversed(q)), "HK_RoofTile")
    b.box((cx, y_base + h * 0.55, zf + 0.02 * facing), (0.5, 0.5, 0.04), "HK_Gold")


def castle_keep(name="HK_HikoneCastle"):
    clear_asset(name)
    b = MB(name)
    # tenshudai stone base (battered)
    bw0, bd0, bw1, bd1, bh = 11.5, 8.5, 10.0, 7.2, 4.5
    b.face([(-bw0, 0, -bd0), (-bw0, 0, bd0), (-bw1, bh, bd1), (-bw1, bh, -bd1)], "HK_Stone")
    b.face([(bw0, 0, bd0), (bw0, 0, -bd0), (bw1, bh, -bd1), (bw1, bh, bd1)], "HK_Stone")
    b.face([(-bw0, 0, bd0), (bw0, 0, bd0), (bw1, bh, bd1), (-bw1, bh, bd1)], "HK_Stone")
    b.face([(bw0, 0, -bd0), (-bw0, 0, -bd0), (-bw1, bh, -bd1), (bw1, bh, -bd1)], "HK_Stone")
    _rect(b, -bw1, bw1, -bd1, bd1, bh, "HK_StoneDark")
    # tier 1
    y = bh
    b.box((0, y + 2.5, 0), (18, 5, 12), "HK_Plaster")
    b.box((0, y + 0.6, 0), (18.05, 1.2, 12.05), "HK_WoodDark")      # dark lower boards
    for x in (-6, -2, 2, 6):
        b.box((x, y + 3.0, 6.02), (1.0, 0.8, 0.06), "HK_WoodDark")
        b.box((x, y + 3.0, -6.02), (1.0, 0.8, 0.06), "HK_WoodDark")
    _hip_skirt(b, 10.4, 7.4, y + 5.0, 7.0, 4.5, y + 6.5)
    _chidori(b, 0, 4.5, y + 5.6, 6.0, 2.4, 2.4, 1)
    _chidori(b, 0, -4.5, y + 5.6, 6.0, 2.4, 2.4, -1)
    # tier 2
    y2 = y + 6.3
    b.box((0, y2 + 2.0, 0), (14, 4.0, 9), "HK_Plaster")
    for x in (-4.5, -1.5, 1.5, 4.5):
        for s in (1, -1):
            b.box((x, y2 + 2.2, 4.52 * s), (0.9, 1.1, 0.06), "HK_WoodDark")
            b.box((x, y2 + 2.8, 4.55 * s), (1.0, 0.12, 0.06), "HK_Gold")
    _hip_skirt(b, 8.4, 5.8, y2 + 4.0, 5.2, 3.6, y2 + 5.3)
    _chidori(b, -3.6, 3.6, y2 + 4.4, 3.6, 1.8, 1.8, 1)
    _chidori(b, 3.6, 3.6, y2 + 4.4, 3.6, 1.8, 1.8, 1)
    _chidori(b, 0, -3.6, y2 + 4.4, 4.2, 2.0, 1.8, -1)
    # tier 3 with balcony (koran) and katomado windows
    y3 = y2 + 5.0
    b.box((0, y3 + 1.8, 0), (10, 3.6, 7), "HK_Plaster")
    for x in (-3.0, 0.0, 3.0):
        for s in (1, -1):
            b.box((x, y3 + 1.9, 3.52 * s), (1.0, 1.5, 0.06), "HK_WoodDark")
            b.box((x, y3 + 2.72, 3.55 * s), (0.7, 0.2, 0.06), "HK_Gold")
    b.box((0, y3 + 0.35, 0), (11.6, 0.12, 8.6), "HK_WoodDark", top=True)
    for s in (1, -1):
        b.box((0, y3 + 0.95, 4.25 * s), (11.6, 0.08, 0.08), "HK_WoodDark")
        b.box((5.75 * s, y3 + 0.95, 0), (0.08, 0.08, 8.6), "HK_WoodDark")
    # top roof: irimoya (hip base + gable ends)
    yt = y3 + 3.6
    _hip_skirt(b, 6.6, 5.0, yt, 3.6, 1.6, yt + 2.2)
    ridge_y = yt + 3.4
    for s in (1, -1):
        q = [(-3.8, yt + 2.2, 1.9 * s), (3.8, yt + 2.2, 1.9 * s), (3.8, ridge_y, 0), (-3.8, ridge_y, 0)]
        b.face(q if s > 0 else [q[1], q[0], q[3], q[2]], "HK_RoofTile")
    for x, s in ((-3.6, -1), (3.6, 1)):
        tri = [(x, yt + 2.2, -1.6), (x, yt + 2.2, 1.6), (x, ridge_y - 0.1, 0)]
        b.face(tri if s > 0 else list(reversed(tri)), "HK_Plaster")
        b.cyl((x + 0.03 * s, yt + 2.6, 0), 0.3, 0.03, "HK_Gold", seg=8)
    b.box((0, ridge_y + 0.15, 0), (7.8, 0.35, 0.4), "HK_RoofRidge")
    # shachihoko
    for x in (-3.7, 3.7):
        b.box((x, ridge_y + 0.7, 0), (0.35, 0.9, 0.3), "HK_Gold")
        b.box((x - 0.12 * (1 if x > 0 else -1), ridge_y + 1.15, 0), (0.25, 0.35, 0.5), "HK_Gold")
    # kara-hafu (curved front gable) approximated by a low arched board on tier 3 front
    for s in (1, -1):
        pts = _arc(0, 0, 2.2, 20, 160, 8)
        for i in range(len(pts) - 1):
            (x0, h0), (x1, h1) = pts[i], pts[i + 1]
            q = [(x0, yt - 0.4 + h0 * 0.5, 5.2 * s), (x1, yt - 0.4 + h1 * 0.5, 5.2 * s), (x1, yt - 0.4 + h1 * 0.5 + 0.25, 5.2 * s), (x0, yt - 0.4 + h0 * 0.5 + 0.25, 5.2 * s)]
            b.face(q if s > 0 else list(reversed(q)), "HK_WoodDark")
    return b.finish()


def yagura(name="HK_Yagura", W=10.0, D=6.5):
    clear_asset(name)
    b = MB(name)
    b.box((0, 2.2, 0), (W, 4.4, D), "HK_Plaster")
    b.box((0, 0.5, 0), (W + 0.04, 1.0, D + 0.04), "HK_WoodDark")
    for x in (-W / 3, 0, W / 3):
        b.box((x, 2.6, D / 2 + 0.02), (0.9, 0.9, 0.05), "HK_WoodDark")
    _hip_skirt(b, W / 2 + 1.2, D / 2 + 1.2, 4.3, W / 2 - 1.0, D / 2 - 1.0, 5.4)
    b.box((0, 6.0, 0), (W - 1.6, 1.4, D - 1.6), "HK_Plaster")
    gable_roof(b, W - 1.6, D / 2 + 0.3, -D / 2 - 0.3, 7.0, 32, over_side=0.6, thick=0.2, ridge_size=0.4)
    return b.finish()


def castle_wall(name="HK_CastleWall", L=10.0):
    """Castle perimeter dobei: taller white wall with loopholes (sama)."""
    clear_asset(name)
    b = MB(name)
    b.box((0, 1.5, 0), (L, 3.0, 0.6), "HK_Plaster")
    for i in range(4):
        x = -L / 2 + (i + 0.5) * L / 4
        b.box((x, 1.7, 0.31), (0.25, 0.25, 0.03), "HK_WoodDark") if i % 2 == 0 else b.face([(x - 0.14, 1.6, 0.31), (x + 0.14, 1.6, 0.31), (x, 1.85, 0.31)], "HK_WoodDark")
    y0, y1 = 3.0, 3.45
    for s in (1, -1):
        q = [(-L / 2, y0, 0.75 * s), (L / 2, y0, 0.75 * s), (L / 2, y1, 0), (-L / 2, y1, 0)]
        b.face(q if s > 0 else [q[1], q[0], q[3], q[2]], "HK_RoofTile")
    b.box((0, y1 + 0.05, 0), (L, 0.14, 0.2), "HK_RoofRidge")
    return b.finish()


# =============================================================== terrain
from hk_terrain import (PLATEAU, WALL_H, terrain_height, TOWN_Z, BREAK_X, BREAK_Z)


def terrain(name="HK_Terrain_Castle", x0=-160, x1=224, z0=TOWN_Z, z1=300, step=4.0):
    clear_asset(name)
    b = MB(name)
    xs = sorted(set([x0 + i * step for i in range(int((x1 - x0) / step) + 1)] + list(BREAK_X)))
    zs = sorted(set([z0] + [z0 + j * step for j in range(1, int((z1 - z0) / step) + 1)] + list(BREAK_Z)))
    H = {(x, z): terrain_height(x, z) for x in xs for z in zs}
    for i in range(len(xs) - 1):
        for j in range(len(zs) - 1):
            xa, xb, za, zb = xs[i], xs[i + 1], zs[j], zs[j + 1]
            hs = [H[(xa, za)], H[(xa, zb)], H[(xb, zb)], H[(xb, za)]]
            m = "HK_Soil" if max(hs) < 0.0 else "HK_Grass"
            b.face([(xa, hs[0], za), (xa, hs[1], zb), (xb, hs[2], zb), (xb, hs[3], za)], m)
    return b.finish()


def ground_slab(name="HK_GroundPaved", x0=-136, x1=112, z0=-152, z1=TOWN_Z):
    """Paved town ground at the pavement collider height, with the road tiles (and the visual
    Castle Road corridor) left open so the lower carriageway shows through."""
    import json
    clear_asset(name)
    b = MB(name)
    tiles = [t for t in json.load(open("/Users/xinyu/VRLearn/Art/Hikone/scene_constraints.json"))["tiles"] if t["active"]]
    holes = [(t["min"][0], t["min"][1], t["max"][0], t["max"][1]) for t in tiles]
    holes.append((22.0, -152.0, 42.0, -16.0))
    cell = 1.0
    z = float(z0)
    while z < z1 - 1e-6:
        zt = min(z + cell, z1)
        zc = (z + zt) / 2
        run = None
        x = float(x0)
        while x < x1 + 1e-6:
            inside = x < x1 and not any(h[0] < x + cell / 2 < h[2] and h[1] < zc < h[3] for h in holes)
            if inside and run is None:
                run = x
            if (not inside) and run is not None:
                _rect(b, run, x, z, zt, 0.0, "HK_Sidewalk")
                run = None
            x += cell
        z = zt
    return b.finish()


def far_ground(name="HK_GroundFar"):
    """Grass ring around the paved town and the castle terrain (leaves both holes open)."""
    clear_asset(name)
    b = MB(name)
    _rect(b, -700, 700, -700, -152, 0.0, "HK_Grass")
    _rect(b, -700, -136, -152, TOWN_Z, 0.0, "HK_Grass")
    _rect(b, 112, 700, -152, TOWN_Z, 0.0, "HK_Grass")
    _rect(b, -700, -160, TOWN_Z, 700, 0.05, "HK_Grass")
    _rect(b, 224, 700, TOWN_Z, 700, 0.05, "HK_Grass")
    _rect(b, -160, 224, 300, 700, 0.05, "HK_Grass")
    return b.finish()


# =============================================================== build all
def build_all():
    obs = []
    obs.append(machiya("HK_Machiya_A", 7.2, 9.0, "A", seed=1))
    obs.append(machiya("HK_Machiya_A_Narrow", 5.4, 9.0, "A", seed=2))
    obs.append(machiya("HK_Machiya_B", 6.0, 9.0, "B", seed=3))
    obs.append(machiya("HK_Machiya_C", 8.4, 10.0, "C", seed=4))
    obs.append(kura("HK_Kura"))
    obs.append(dobei("HK_Dobei_5m"))
    obs.append(gate("HK_Gate"))
    obs.append(road_straight())                                   # visual-only Castle Road extension
    obs.append(road_straight("HK_RoadSurf_Straight", surface_only=True))
    obs.append(road_cross("HK_RoadSurf_Cross", tee=False, surface_only=True))
    obs.append(road_cross("HK_RoadSurf_Tee", tee=True, surface_only=True, east_crosswalk=False))
    obs.append(road_turn("HK_RoadSurf_Turn", surface_only=True))
    obs.append(road_turn("HK_RoadSurf_TurnOpen", surface_only=True, outer_line=False))
    geo = "/Users/xinyu/VRLearn/Art/Hikone/road_tiles_geometry.json"
    import json as _json
    all_tiles = list(_json.load(open(geo)).keys())
    honmachi = "Road_1_line_turn (1)"
    obs.append(road_raised("HK_RoadRaised", [t for t in all_tiles if t != honmachi], geo,
                           drop_z_above=TOWN_Z - 0.05, drop_tiles=("Road_1_line (23)", "Road_1_line (24)"),
                           clip_moat=True))
    obs.append(road_raised("HK_RoadRaised_Honmachi", [honmachi], geo, corridor=(28.0, 36.0)))
    obs.append(ground_slab())
    obs.append(far_ground())
    obs.append(street_lamp())
    obs.append(bollard())
    obs.append(guardrail())
    obs.append(sign_stop())
    obs.append(sign_square("HK_Sign_Crosswalk", "HK_SignBlue", "crosswalk"))
    obs.append(sign_school())
    obs.append(guide_post())
    obs.append(bench())
    obs.append(planter())
    obs.append(tree_round())
    obs.append(tree_round("HK_Tree_Sakura", leaf="HK_Sakura", seed=7, h=6.0))
    obs.append(tree_round("HK_Tree_RoundLow", seed=11, h=8.0, subdiv=1))
    obs.append(tree_pine())
    obs.append(tree_cedar())
    obs.append(shrub())
    obs.append(ishigaki())
    obs.append(ishigaki("HK_Ishigaki_Tall", L=10.0, H=WALL_H + 0.5, batter=2.2))
    obs.append(ishigaki("HK_Ishigaki_Low", fill=False))
    obs.append(gatehouse("HK_GateHouse_Nagaya"))
    obs.append(gatehouse("HK_GateHouse_Yagura", width=20.7, depth=6.4, open_w=9.0, open_h=5.5, body_h=10.9, turret=True))
    obs.append(chain_fence())
    obs.append(tree_zelkova())
    obs.append(hedge())
    # kei cars mixed into the background traffic (VehicleBody)
    obs.append(kei_tall())
    obs.append(kei_hatch())
    # parked trucks of the truck scenarios (10 t and 4 t class), sized to their colliders
    obs.append(box_truck("HK_Truck_Large", L=11.8, W=2.49, H=3.62, cab_len=2.3, cab_h=3.05, wheel_r=0.52,
                         axles=(-11.8 / 2 + 1.35, 11.8 / 2 - 3.1, 11.8 / 2 - 1.8)))
    obs.append(box_truck("HK_Truck_Medium", L=8.6, W=2.3, H=3.62, cab_len=1.95, cab_h=2.8, wheel_r=0.46,
                         axles=(-8.6 / 2 + 1.15, 8.6 / 2 - 2.3)))
    obs.append(sign_guide())
    obs.append(sign_speed())
    obs.append(water())
    obs.append(bridge())
    obs.append(castle_keep())
    obs.append(yagura())
    obs.append(castle_wall())
    obs.append(terrain())
    for i, ob in enumerate(obs):
        export(ob, i)
    tris = {ob.name: sum(len(p.vertices) - 2 for p in ob.data.polygons) for ob in obs}
    return tris
