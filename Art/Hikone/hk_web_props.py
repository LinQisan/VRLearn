"""Lamp and vehicle geometry ported from hikone-traffic-sim/tools/abstract_props.py.

Source revision: 79dd465f310cc084163d5404a7f3b749a84259c8 (abstract-closed-solids-v2).
Only the lantern and five vehicles are ported; experiment signals stay unchanged.
Geometry uses Unity metres, +Z car front, -X parked truck front. The adapter sends
all vertices through hk_lib.MB.face / U and retains the web's per-face pigments.
Run in Blender: hk_assets.build_web_props() (only these six FBXs).
"""
import math
import random
import bpy
from mathutils import Vector
from hk_lib import MB, clear_asset

MODEL_NAMES = ('HK_StreetLamp', 'HK_Sedan', 'HK_Kei_Tall', 'HK_Kei_Hatch',
               'HK_Truck_Large', 'HK_Truck_Medium')

# Dedicated vertex-pigment materials keep the shared road/architecture materials intact.
MATERIAL_NAMES = {'HK_KeiPaint': 'ART_VehiclePaint', 'HK_Rubber': 'ART_Rubber',
                  'HK_WhitePaint': 'ART_WhitePaint', 'HK_PlateGreen': 'ART_PlateGreen'}
COLORS = {
    'ART_Frame': (0.27, 0.34, 0.45), 'ART_FrameLight': (0.52, 0.59, 0.65),
    'ART_LampGlow': (1.0, 0.87, 0.59), 'ART_LampBase': (0.48, 0.48, 0.57),
    'ART_SignalRed': (0.94, 0.19, 0.16), 'ART_SignalYellow': (1.0, 0.77, 0.13),
    'ART_Glass': (0.35, 0.52, 0.66), 'ART_Cargo': (0.80, 0.82, 0.82),
    'ART_Chassis': (0.17, 0.18, 0.22), 'HK_KeiPaint': (0.90, 0.91, 0.87),
    'HK_Rubber': (0.26, 0.27, 0.35), 'HK_WhitePaint': (0.98, 0.93, 0.77),
    'HK_PlateGreen': (0.23, 0.47, 0.36),
}


def linear(c):
    return c / 12.92 if c < 0.04045 else ((c + 0.055) / 1.055) ** 2.4


class PropMesh:
    """MB adapter with corner pigments and separate car-wheel geometry."""
    def __init__(self, name):
        self.name = name
        self.rng = random.Random('WEB_Sedan' if name == 'HK_Sedan' else name)
        self.moving = name in ('HK_Sedan', 'HK_Kei_Tall', 'HK_Kei_Hatch')
        self.part = 'BodyMain'
        self.parts = {}

    def pigment(self, material):
        base = COLORS[material]
        if material == 'HK_KeiPaint':
            return tuple(min(1, c + self.rng.uniform(-0.07, 0.05)) for c in base)
        accent = self.rng.choice(((0.96, 0.78, 0.49), (0.55, 0.62, 0.82),
                                  (0.55, 0.62, 0.82), (0.87, 0.65, 0.65), base))
        amount = self.rng.uniform(0, 0.12)
        shift = self.rng.uniform(-0.035, 0.035)
        return tuple(max(0, min(1, a * (1 - amount) + c * amount + shift))
                     for a, c in zip(base, accent))

    def polygon(self, points, material, pigment=None):
        part = 'Glass' if self.moving and material == 'ART_Glass' else self.part
        if part not in self.parts:
            builder = MB(self.name + '_' + part)
            builder.colors = builder.bm.loops.layers.float_color.new('Pigment')
            self.parts[part] = builder
        builder = self.parts[part]
        # abstract_props hands the web mesh builder Blender-space points. Invert once
        # here so MB remains the only owner of Unity -> Blender axis conversion.
        unity = [(-p.x, p.z, -p.y) for p in reversed(points)]
        # Keep mesh origins at zero through FBX. Blender's bake_space_transform does
        # not bake empty-node translations the same way as mesh vertices. Unity's
        # prefab generator centres the wheel pivots after importing the geometry.
        f = builder.face(unity, MATERIAL_NAMES.get(material, material))
        rgba = (*map(linear, pigment or self.pigment(material)), 1)
        for loop in f.loops:
            loop[builder.colors] = rgba

    def finish(self):
        root = bpy.data.objects.new(self.name, None)
        bpy.context.scene.collection.objects.link(root)
        for part, builder in self.parts.items():
            ob = builder.finish(weld_distance=0.00001)
            if part.startswith('Wheel_'):
                ob.name = self.name + '_Tire_' + part.removeprefix('Wheel_')
            ob.parent = root
        return root


def identity(p):
    return p


def face(b, points, material, transform=identity, pigment=None):
    pts = [Vector(transform(p)) for p in points]
    if pigment is None and material == 'HK_KeiPaint':
        n = (pts[1] - pts[0]).cross(pts[2] - pts[0]).normalized()
        # These are pigment planes, independent of screen or viewing distance.
        pigment = ((1.0, 0.97, 0.89) if n.y > 0.35 else
                   (0.52, 0.57, 0.67) if n.y < -0.35 else
                   (0.78, 0.84, 0.94) if n.x < -0.2 else (0.93, 0.89, 0.80))
    if material.startswith('ART_Signal'):
        # Signal colours stay distinct instead of receiving complementary paint.
        pigment = {
            'ART_SignalRed': (0.94, 0.19, 0.16),
            'ART_SignalYellow': (1.0, 0.77, 0.13),
            'ART_SignalGreen': (0.17, 0.76, 0.46),
            'ART_SignalGlyph': (1.0, 0.96, 0.80),
        }.get(material, pigment)
    b.polygon([Vector((-p.x, -p.z, p.y)) for p in reversed(pts)], material, pigment)


def outline(width, height, bevel):
    x, y = width / 2, height / 2
    c = min(bevel, x * 0.45, y * 0.45)
    # Clockwise in XY. All lofts have matching rings and closed end caps.
    return [(-x + c, -y), (-x, -y + c), (-x, y - c), (-x + c, y),
            (x - c, y), (x, y - c), (x, -y + c), (x - c, -y)]


def loft(b, rings, material, transform=identity):
    face(b, rings[0], material, transform)
    face(b, list(reversed(rings[-1])), material, transform)
    for a, c in zip(rings, rings[1:]):
        for i in range(len(a)):
            j = (i + 1) % len(a)
            face(b, [a[i], c[i], c[j], a[j]], material, transform)


def beveled_box(b, center, size, material, bevel=0.06, transform=identity):
    x, y, z = center
    w, h, d = size
    c = min(bevel, w * 0.2, h * 0.2, d * 0.2)
    rings = []
    for depth, inset in ((-d / 2, c), (-d / 2 + c, 0), (d / 2 - c, 0), (d / 2, c)):
        rings.append([(x + xx, y + yy, z + depth) for xx, yy in outline(w - inset * 2, h - inset * 2, c)])
    loft(b, rings, material, transform)


def cylinder(b, start, end, radius, material, segments=16, end_radius=None, bevel=0, transform=identity):
    a, c = Vector(start), Vector(end)
    axis = (c - a).normalized()
    ref = Vector((0, 1, 0)) if abs(axis.y) < 0.9 else Vector((1, 0, 0))
    u = (ref - axis * ref.dot(axis)).normalized()
    v = axis.cross(u)
    rt = radius if end_radius is None else end_radius
    length = (c - a).length
    t = min(bevel, length * 0.20, radius * 0.20)
    profiles = [(a, radius), (c, rt)] if not t else [
        (a, radius - t), (a + axis * t, radius), (c - axis * t, rt), (c, rt - t)]
    rings = [[p + r * (u * math.cos(i * math.tau / segments) + v * math.sin(i * math.tau / segments))
              for i in range(segments)] for p, r in profiles]
    face(b, list(reversed(rings[0])), material, transform)
    face(b, rings[-1], material, transform)
    for lower, upper in zip(rings, rings[1:]):
        for i in range(segments):
            j = (i + 1) % segments
            face(b, [lower[i], lower[j], upper[j], upper[i]], material, transform)


def panel(b, points, material, inside, transform=identity, pigment=None):
    pts = [Vector(p) for p in points]
    n = (pts[1] - pts[0]).cross(pts[2] - pts[0]).normalized()
    center = sum(pts, Vector()) / len(pts)
    if n.dot(center - Vector(inside)) < 0:
        pts.reverse()
        n.negate()
    back = [p + n * 0.001 for p in pts]
    front = [p + n * 0.009 for p in pts]
    face(b, list(reversed(back)), material, transform, pigment)
    face(b, front, material, transform, pigment)
    for i in range(len(pts)):
        j = (i + 1) % len(pts)
        face(b, [back[i], back[j], front[j], front[i]], material, transform, pigment)


def lantern(b):
    beveled_box(b, (0, 0.16, 0), (0.43, 0.32, 0.43), 'ART_LampBase', 0.07)
    cylinder(b, (0, 0.32, 0), (0.02, 4.85, 0.035), 0.092, 'ART_Frame',
             end_radius=0.065, segments=12)
    cylinder(b, (0.02, 4.85, 0.035), (0, 4.85, 1.2), 0.065, 'ART_Frame',
             end_radius=0.046, segments=12)
    # A closed, gently tapered eight-sided glass volume. Its warm bottom is real.
    rings = []
    for height, w, d in ((4.26, 0.40, 0.40), (4.34, 0.44, 0.44), (4.80, 0.50, 0.50)):
        # Map the cross-section's second axis to depth; keep a consistent winding.
        rings.append([(x, height, 1.2 - y) for x, y in outline(w, d, 0.075)])
    # The clockwise ring has a -Y normal: the bottom faces down, the top up.
    face(b, rings[0], 'ART_LampGlow')
    face(b, list(reversed(rings[-1])), 'ART_LampGlow')
    for low, high in zip(rings, rings[1:]):
        for i in range(8):
            j = (i + 1) % 8
            face(b, [low[i], high[i], high[j], low[j]], 'ART_LampGlow')
    beveled_box(b, (0, 4.22, 1.2), (0.49, 0.105, 0.49), 'ART_LampBase', 0.023)
    # Thick sloping cap, sealed below; its apex is an octagonal ridge, not a plane.
    cylinder(b, (0, 4.795, 1.2), (0, 5.11, 1.2), 0.405, 'ART_Frame',
             segments=8, end_radius=0.095)
    for x in (-0.18, 0.18):
        for z in (1.02, 1.38):
            cylinder(b, (x, 4.25, z), (x * 1.22, 4.81, 1.2 + (z - 1.2) * 1.22),
                     0.023, 'ART_Frame', segments=6, end_radius=0.020)


def interpolate(profile, z):
    for (a, h), (c, k) in zip(profile, profile[1:]):
        if a <= z <= c:
            return h + (k - h) * (z - a) / (c - a)
    return profile[0][1] if z < profile[0][0] else profile[-1][1]


def car_body(b, width, profile, radius, wheelbase, cabin, transform=identity, center_z=0):
    hw = width / 2
    # The shell follows the wheel arches, with a fully closed underside above tyres.
    zs = {z for z, _ in profile}
    for axle in (-wheelbase / 2, wheelbase / 2):
        for i in range(9):
            zs.add(axle + (radius + 0.045) * math.cos(math.pi * i / 8))
    zs = sorted(z for z in zs if profile[0][0] <= z <= profile[-1][0])

    def bottom(z):
        floor = 0.30
        for axle in (-wheelbase / 2, wheelbase / 2):
            dz = abs(z - axle)
            if dz < radius + 0.045:
                floor = max(floor, radius + math.sqrt((radius + 0.045) ** 2 - dz * dz))
        return floor

    def section(z):
        h = interpolate(profile, z)
        belt = min(0.92 if width > 1.6 else 1.01, h - 0.15)
        floor = min(bottom(z), belt - 0.10)
        top_width = hw * (0.78 if h > 1.15 else 0.92)
        return floor, belt, h, top_width

    rings = []
    for z in zs:
        floor, belt, h, wt = section(z)
        rings.append([(-hw * 0.90, floor, z + center_z), (-hw, floor + 0.05, z + center_z),
                      (-hw, belt, z + center_z), (-wt, h - 0.075, z + center_z),
                      (-wt * 0.90, h, z + center_z), (wt * 0.90, h, z + center_z),
                      (wt, h - 0.075, z + center_z), (hw, belt, z + center_z),
                      (hw, floor + 0.05, z + center_z), (hw * 0.90, floor, z + center_z)])
    loft(b, rings, 'HK_KeiPaint', transform)
    inside = (0, 0.8, center_z)
    rear, front, divider = cabin

    def glass_point(side, z, t):
        _, belt, h, wt = section(z)
        y = belt + (h - 0.075 - belt) * t
        x = hw + (wt - hw) * t
        return (side * x, y, z + center_z)

    for side in (-1, 1):
        for start, end in ((rear, divider - 0.045), (divider + 0.045, front)):
            breaks = sorted({start, end, *[z for z, _ in profile if start < z < end]})
            for a, c in zip(breaks, breaks[1:]):
                panel(b, [glass_point(side, a, 0.14), glass_point(side, c, 0.14),
                          glass_point(side, c, 0.83), glass_point(side, a, 0.83)],
                      'ART_Glass', inside, transform)
        # A single warm handle is enough to suggest a door.
        p = glass_point(side, divider + 0.16, 0.04)
        beveled_box(b, (p[0], p[1] - 0.07, p[2]), (0.04, 0.035, 0.20),
                    'ART_FrameLight', 0.007, transform)
    for low, high in ((front + 0.17, front - 0.30), (rear - 0.17, rear + 0.20)):
        low = max(profile[0][0] + 0.06, min(profile[-1][0] - 0.06, low))
        high = max(profile[0][0] + 0.06, min(profile[-1][0] - 0.06, high))
        # Match every bend of the actual roof surface, then offset outwards.
        # A single inset quad disappears inside a changing roof profile.
        start, end = sorted((low, high))
        breaks = sorted({start, end, *[z for z, _ in profile if start < z < end]})
        for a, c in zip(breaks, breaks[1:]):
            _, _, h0, w0 = section(a)
            _, _, h1, w1 = section(c)
            panel(b, [(-w0 * 0.81, h0 + 0.002, a + center_z), (w0 * 0.81, h0 + 0.002, a + center_z),
                      (w1 * 0.81, h1 + 0.002, c + center_z), (-w1 * 0.81, h1 + 0.002, c + center_z)],
                  'ART_Glass', inside, transform)


def wheel(b, x, y, z, radius, transform=identity, width=0.16):
    previous = b.part
    if b.moving:
        b.part = "Wheel_" + ("F" if z > 0 else "R") + ("R" if x > 0 else "L")
    cylinder(b, (x - width / 2, y, z), (x + width / 2, y, z), radius,
             'HK_Rubber', segments=16, bevel=0.025, transform=transform)
    side = 1 if x > 0 else -1
    outer = x + side * width / 2
    cylinder(b, (outer, y, z), (outer + side * 0.006, y, z), radius * 0.56,
             'ART_FrameLight', segments=12, transform=transform)
    b.part = previous


def car(b, name):
    sedan, tall = name == 'WEB_Sedan', name == 'HK_Kei_Tall'
    w, h, length, r, wb = (1.76, 1.45, 4.60, 0.31, 2.70) if sedan else (1.475, 1.79 if tall else 1.525, 3.394, 0.28, 2.52)
    if sedan:
        profile = [(-2.30, 0.70), (-2.10, 0.88), (-1.45, 1.01), (-0.78, 1.40),
                   (-0.25, h), (0.34, h), (0.82, 1.08), (1.55, 0.88), (2.15, 0.86), (2.30, 0.86)]
        cabin = (-1.24, 0.66, -0.22)
    elif tall:
        profile = [(-1.697, 1.47), (-1.48, 1.74), (-1.20, h), (0.28, h), (0.60, 1.72),
                   (1.12, 1.02), (1.55, 0.88), (1.697, 0.86)]
        cabin = (-1.42, 0.73, -0.28)
    else:
        profile = [(-1.697, 0.87), (-1.45, 1.24), (-1.12, 1.49), (-0.68, h),
                   (0.14, h), (0.50, 1.29), (0.96, 0.92), (1.55, 0.88), (1.697, 0.86)]
        cabin = (-1.26, 0.66, -0.27)
    car_body(b, w, profile, r, wb, cabin)
    hw, front, rear = w / 2, length / 2, -length / 2
    beveled_box(b, (0, 0.23, 0), (w - 0.34, 0.14, length - 0.50), 'ART_Frame', 0.025)
    for side in (-1, 1):
        for z in (-wb / 2, wb / 2):
            wheel(b, side * (hw - 0.075), r, z, r, width=0.15)
        beveled_box(b, (side * (hw + 0.08), 1.08 if sedan else 1.15, 0.66),
                    (0.16, 0.12, 0.10), 'ART_Frame', 0.025)
        beveled_box(b, (side * (hw - 0.26), 0.66, front - 0.022),
                    (0.38 if sedan else 0.29, 0.13, 0.05), 'ART_LampGlow', 0.014)
        beveled_box(b, (side * (hw - 0.23), 0.64 if sedan else 0.90, rear + 0.022),
                    (0.31 if sedan else 0.18, 0.14 if sedan else 0.28, 0.05), 'ART_SignalRed', 0.012)
    beveled_box(b, (0, 0.45, front - 0.008), (0.62, 0.09, 0.016), 'ART_Frame', 0.003)
    for z in (front - 0.010, rear + 0.010):
        beveled_box(b, (0, 0.48, z), (0.33, 0.165, 0.020),
                    'HK_WhitePaint' if sedan else 'ART_SignalYellow', 0.012)


def truck(b, name):
    """A Japanese box truck (アルミバン, like a Hino Ranger / Isuzu Forward), built so that every
    part rests on the one below it — no gaps: two black chassis rails, a sub-frame of cross members
    on them, the aluminium van box on the sub-frame. Cab-over cab with front wheel arches, grille
    band, bumper, steps and mirrors on stays; single front and dual rear tyres under black fenders
    with mud flaps; side guard rails, fuel tank, battery box; a rear underrun bar on brackets with
    triple combination lamps and the plate; rear doors with lock rods, hinges and corner posts.
    Outer extents (mirrors, bumper, plate, rear lamps, roof) stay those of the source model."""
    large = name == 'HK_Truck_Large'
    length, w, h, cab_len, cab_h, r = (11.8, 2.49, 3.62, 2.30, 3.05, 0.52) if large else (8.6, 2.30, 3.62, 1.95, 2.80, 0.46)
    half, hw = length / 2, w / 2
    # Author in the car's +Z convention, then rotate to the parked trucks' -X front.
    turn = lambda p: (-p[2], p[1], p[0])
    box = lambda c, size, m, bevel=0.02: beveled_box(b, c, size, m, bevel, turn)
    rail_top = 2 * r + 0.02                 # chassis rails
    sub_top = rail_top + 0.14               # the body's sub-frame (cross members)
    cargo_bottom = sub_top - 0.01           # the box sits on it (overlap, no seam)
    cab_bottom = 2 * r + 0.08               # just above the front tyres: the wheel arch
    front_axle = half - (1.35 if large else 1.15)
    rear_axles = (-half + 3.10, -half + 1.80) if large else (-half + 2.30,)
    cab_back = half - cab_len
    cargo_front = cab_back - 0.18
    rear = -half

    # ---- chassis: rails from behind the bumper to the rear cross beam
    rail_front, rail_back = half - 0.30, rear + 0.10
    for side in (-1, 1):
        box((side * 0.42, rail_top - 0.13, (rail_front + rail_back) / 2), (0.12, 0.26, rail_front - rail_back), 'ART_Chassis', 0.01)
    box((0, rail_top - 0.11, rear + 0.16), (w - 0.24, 0.20, 0.10), 'ART_Chassis', 0.01)        # rear cross beam
    # sub-frame: cross members across the rails, under the whole box
    z = cargo_front - 0.15
    while z > rear + 0.20:
        box((0, (rail_top + sub_top) / 2 - 0.004, z), (w - 0.10, sub_top - rail_top + 0.012, 0.09), 'ART_Chassis', 0.008)
        z -= 0.62
    for side in (-1, 1):                    # the sub-frame's long sills on the rails
        box((side * 0.42, (rail_top + sub_top) / 2, (cargo_front + rear) / 2 - 0.02), (0.14, sub_top - rail_top - 0.004, cargo_front - rear - 0.10), 'ART_Chassis', 0.008)

    # ---- the van box: aluminium, rounded edges, edge rails, posts, rear doors
    top = h - 0.002
    rings = []
    for zz in (rear, rear + 0.08, cargo_front - 0.08, cargo_front):
        inset = 0.05 if zz in (rear, cargo_front) else 0
        rings.append([(x, y + (cargo_bottom + top) / 2, zz)
                      for x, y in outline(w - 2 * inset - 0.06, top - cargo_bottom - 2 * inset, 0.08)])
    loft(b, rings, 'ART_Cargo', turn)
    body_h, mid_y, mid_z, body_l = top - cargo_bottom, (cargo_bottom + top) / 2, (cargo_front + rear) / 2, cargo_front - rear
    for side in (-1, 1):
        box((side * (hw - 0.04), top - 0.07, mid_z), (0.07, 0.13, body_l - 0.02), 'ART_FrameLight', 0.015)       # top rail
        box((side * (hw - 0.035), cargo_bottom + 0.08, mid_z), (0.07, 0.15, body_l - 0.04), 'ART_FrameLight', 0.015)   # bottom rail
        for zz in (rear + 0.06, cargo_front - 0.06):                                                          # corner posts
            box((side * (hw - 0.06), mid_y, zz), (0.11, body_h - 0.03, 0.11), 'ART_FrameLight', 0.02)
        n = 3 if large else 2                                                                                  # a few side stiffeners
        for k in range(1, n + 1):
            box((side * (hw - 0.025), mid_y, rear + body_l * k / (n + 1)), (0.035, body_h - 0.34, 0.07), 'ART_FrameLight', 0.008)
    box((0, top - 0.06, rear + 0.04), (w - 0.20, 0.12, 0.09), 'ART_FrameLight', 0.015)                   # rear door header
    for side in (-1, 1):
        box((side * (w / 4 - 0.02), mid_y - 0.02, rear - 0.006), (w / 2 - 0.15, body_h - 0.30, 0.024), 'ART_Cargo', 0.008)   # door leaf
        for x in (side * 0.24, side * (hw - 0.38)):                                                           # lock rods with handles
            cylinder(b, (x, cargo_bottom + 0.16, rear - 0.03), (x, top - 0.18, rear - 0.03), 0.018, 'ART_FrameLight', segments=8, transform=turn)
            box((x + side * 0.07, mid_y - 0.25, rear - 0.035), (0.16, 0.05, 0.026), 'ART_FrameLight', 0.008)
        for y in (cargo_bottom + 0.35, mid_y, top - 0.40):                                                    # hinges
            box((side * (hw - 0.10), y, rear - 0.02), (0.12, 0.10, 0.03), 'ART_Chassis', 0.008)

    # ---- cab-over cab: body above the wheel arch, lower panels in front of and behind it
    cab_center = half - cab_len / 2
    profile = [(-cab_len / 2, cab_h - 0.10), (-cab_len / 2 + 0.18, cab_h),
               (cab_len / 2 - 0.22, cab_h), (cab_len / 2, cab_h - 0.12)]
    rings = []
    for zz, roof in profile:
        rings.append([(x, y + (cab_bottom + roof) / 2, zz + cab_center)
                      for x, y in outline(w, roof - cab_bottom, 0.15)])
    loft(b, rings, 'HK_KeiPaint', turn)
    arch_front, arch_back = front_axle + r + 0.10, front_axle - r - 0.10
    box((0, (0.40 + cab_bottom) / 2 + 0.01, (arch_front + half) / 2 - 0.01), (w - 0.02, cab_bottom - 0.40 + 0.04, half - arch_front - 0.02), 'HK_KeiPaint', 0.05)
    box((0, (0.46 + cab_bottom) / 2 + 0.01, (cab_back + arch_back) / 2 + 0.01), (w - 0.04, cab_bottom - 0.46 + 0.04, arch_back - cab_back - 0.02), 'HK_KeiPaint', 0.04)
    for side in (-1, 1):
        box((side * (hw - 0.16), cab_bottom - 0.03, front_axle), (0.40, 0.05, 2 * r + 0.24), 'ART_Chassis', 0.01)   # arch liner
        box((side * (hw + 0.005), 0.66, (cab_back + arch_back) / 2 + 0.01), (0.05, 0.07, arch_back - cab_back - 0.16), 'ART_Chassis', 0.01)  # step
    inside = (0, 1.6, cab_center)
    panel(b, [(-hw + 0.22, 1.72, half + 0.004), (hw - 0.22, 1.72, half + 0.004),
              (hw - 0.28, cab_h - 0.29, half + 0.004), (-hw + 0.28, cab_h - 0.29, half + 0.004)],
          'ART_Glass', inside, turn)
    for side in (-1, 1):
        panel(b, [(side * hw, 1.63, half - cab_len + 0.24), (side * hw, 1.63, half - 0.27),
                  (side * hw, cab_h - 0.27, half - 0.34), (side * hw, cab_h - 0.27, half - cab_len + 0.24)],
              'ART_Glass', inside, turn)
        box((side * (hw + 0.22), cab_h - 0.54, half - 0.12), (0.14, 0.42, 0.11), 'ART_Chassis', 0.035)        # mirror
        box((side * (hw + 0.10), cab_h - 0.31, half - 0.12), (0.26, 0.035, 0.035), 'ART_FrameLight', 0.007)  # its stay
        box((side * (hw - 0.34), 0.86, half + 0.012), (0.40, 0.19, 0.05), 'ART_LampGlow', 0.02)             # headlight
        box((side * (hw - 0.08), 0.86, half + 0.010), (0.10, 0.17, 0.04), 'ART_SignalYellow', 0.015)        # indicator
    box((0, 1.22, half + 0.012), (w * 0.62, 0.34, 0.05), 'ART_Chassis', 0.02)                          # grille band
    box((0, 0.53, half + 0.06), (w + 0.02, 0.26, 0.16), 'ART_FrameLight', 0.035)                       # bumper
    box((0, 0.53, half + 0.15), (0.33, 0.17, 0.02), 'HK_PlateGreen', 0.005)                            # plate

    # ---- wheels: single front, dual rear; black fenders and mud flaps over the rear axles
    for side in (-1, 1):
        wheel(b, side * (hw - 0.16), r, front_axle, r, turn, width=0.28)
        for zz in rear_axles:
            wheel(b, side * (hw - 0.14), r, zz, r, turn, width=0.24)
            wheel(b, side * (hw - 0.41), r, zz, r, turn, width=0.24)
        span_front, span_back = max(rear_axles) + r + 0.14, min(rear_axles) - r - 0.14
        box((side * (hw - 0.28), 2 * r + 0.06, (span_front + span_back) / 2), (0.60, 0.05, span_front - span_back), 'ART_Chassis', 0.01)
        box((side * (hw - 0.28), (0.22 + 2 * r + 0.06) / 2, span_back - 0.02), (0.56, 2 * r - 0.14, 0.025), 'ART_Chassis', 0.006)

    # ---- between the axles: side guards on brackets, fuel tank, battery box
    guard_front, guard_back = cab_back - 0.10, max(rear_axles) + r + 0.24
    for side in (-1, 1):
        for y in (0.50, 0.80):
            box((side * (hw - 0.06), y, (guard_front + guard_back) / 2), (0.04, 0.09, guard_front - guard_back), 'ART_FrameLight', 0.01)
        for zz in (guard_front - 0.15, (guard_front + guard_back) / 2, guard_back + 0.15):
            box((side * (hw + 0.42) / 2, 0.83, zz), (hw - 0.44, 0.05, 0.06), 'ART_Chassis', 0.008)
            box((side * (hw - 0.09), 0.66, zz), (0.03, 0.40, 0.05), 'ART_Chassis', 0.008)
    tank_back = max(guard_back + 0.4, cab_back - 1.35)
    cylinder(b, (-(hw - 0.38), 0.74, cab_back - 0.30), (-(hw - 0.38), 0.74, tank_back), 0.24, 'ART_FrameLight', segments=16, bevel=0.03, transform=turn)
    box((hw - 0.40, 0.74, cab_back - 0.62), (0.44, 0.42, 0.62), 'ART_Chassis', 0.03)

    # ---- rear: underrun bar on two brackets, lamp bars with triple lamps, plate
    box((0, 0.50, rear + 0.02), (w - 0.14, 0.13, 0.10), 'ART_Chassis', 0.02)
    for side in (-1, 1):
        box((side * 0.42, (0.50 + rail_top) / 2, rear + 0.10), (0.08, rail_top - 0.50, 0.06), 'ART_Chassis', 0.01)
        box((side * (hw - 0.42), 0.76, rear + 0.01), (0.66, 0.17, 0.06), 'ART_Chassis', 0.015)
        for k, colour in enumerate(('ART_SignalRed', 'ART_SignalRed', 'ART_SignalYellow')):
            x = side * (hw - 0.20 - 0.21 * k)
            cylinder(b, (x, 0.76, rear - 0.015), (x, 0.76, rear - 0.05), 0.065, colour, segments=14, transform=turn)
    box((0, 0.70, rear - 0.01), (0.33, 0.17, 0.02), 'HK_PlateGreen', 0.005)


def build(name):
    clear_asset(name)
    b = PropMesh(name)
    if name == 'HK_StreetLamp':
        lantern(b)
    elif name.startswith('HK_Truck'):
        truck(b, name)
    else:
        car(b, 'WEB_Sedan' if name == 'HK_Sedan' else name)
    return b.finish()
