"""Kyobashi signal heads ported from hikone-traffic-sim/tools/abstract_props.py.

Run in Blender: hk_signals.build_all(). Render-only parts retain independent
red/yellow/green lenses; placement and original colliders belong to the scene.
"""
import math
import random
from hk_lib import clear_asset, export
from hk_web_props import (PropMesh, COLORS, cylinder, beveled_box, loft)

MODEL_NAMES = ('HK_Signal_Car', 'HK_Signal_Pedestrian')
SIGNAL_COLORS = {
    'ART_SignalHousing': (0.55, 0.57, 0.57),
    'ART_SignalHood': (0.50, 0.52, 0.53),
    'ART_SignalHousingDark': (0.25, 0.25, 0.27),
    'ART_SignalHoodDark': (0.22, 0.23, 0.26),
    'ART_SignalGreen': (0.17, 0.76, 0.46),
    'ART_SignalGlyph': (1.0, 0.96, 0.80),
}
COLORS.update(SIGNAL_COLORS)


class SignalMesh(PropMesh):
    def __init__(self, name):
        super().__init__(name)
        self.rng = random.Random(name.replace('HK_', 'ART_', 1))
        self.channel = 'RedLight'

    def polygon(self, points, material, pigment=None):
        if material in ('ART_SignalRed', 'ART_SignalYellow', 'ART_SignalGreen'):
            self.channel = {'ART_SignalRed': 'RedLight', 'ART_SignalYellow': 'YellowLight',
                            'ART_SignalGreen': 'GreenLight'}[material]
            self.part = self.channel
            pigment = COLORS[material]
        elif material == 'ART_SignalGlyph':
            self.part = self.channel
            pigment = COLORS[material]
        else:
            self.part = 'Housing'
        # The browser reflects depth after building its signal heads: the lenses
        # face the preserved source head's local -Z, with winding corrected.
        reflected = [type(p)((p.x, -p.y, p.z)) for p in reversed(points)]
        super().polygon(reflected, material, pigment)

def signal_lens(b, x, y, radius, material, front=0.16, hood=0.15, arc=(18, 162), hood_material='ART_Frame'):
    cylinder(b, (x, y, front - 0.03), (x, y, front + 0.024), radius + 0.026,
             'ART_Frame', segments=24, bevel=0.012)
    cylinder(b, (x, y, front + 0.020), (x, y, front + 0.049), radius,
             material, segments=24, bevel=0.006)
    # Closed sun visor (hood) over the lens with front/back and end walls; `arc` in degrees from +X.
    steps = max(6, round((arc[1] - arc[0]) / 12))
    angles = [math.radians(arc[0] + (arc[1] - arc[0]) * i / steps) for i in range(steps + 1)]
    outer = [(x + (radius + 0.052) * math.cos(t), y + (radius + 0.052) * math.sin(t)) for t in angles]
    inner = [(x + (radius + 0.023) * math.cos(t), y + (radius + 0.023) * math.sin(t)) for t in reversed(angles)]
    polygon = outer + inner
    if sum(polygon[i][0] * polygon[(i + 1) % len(polygon)][1] - polygon[(i + 1) % len(polygon)][0] * polygon[i][1]
           for i in range(len(polygon))) > 0:
        polygon.reverse()
    loft(b, [[(xx, yy, front + zz) for xx, yy in polygon] for zz in (-0.012, hood)], hood_material)

def square_lamp(b, y, material, walking, front):
    """A Japanese pedestrian lamp: square lens in a dark frame under a three-sided hood."""
    beveled_box(b, (0, y, front - 0.012), (0.31, 0.31, 0.03), 'ART_Frame', 0.01)
    beveled_box(b, (0, y, front + 0.012), (0.27, 0.27, 0.024), material, 0.012)
    person(b, 0, y, walking, front + 0.026)
    for center, size in (((0, y + 0.165, front + 0.08), (0.34, 0.022, 0.18)),
                         ((-0.163, y + 0.02, front + 0.08), (0.022, 0.31, 0.18)),
                         ((0.163, y + 0.02, front + 0.08), (0.022, 0.31, 0.18))):
        beveled_box(b, center, size, 'ART_SignalHoodDark', 0.006)

def person(b, x, y, walking, front):
    cylinder(b, (x, y + 0.046, front), (x, y + 0.046, front + 0.006), 0.013,
             'ART_SignalGlyph', segments=12)
    # Five thick painted gestures; all are capped solids.
    limbs = [((x, y + 0.025), (x, y - 0.018))]
    if walking:
        limbs += [((x, y + 0.008), (x - 0.028, y - 0.009)), ((x, y + 0.008), (x + 0.026, y + 0.020)),
                  ((x, y - 0.014), (x - 0.032, y - 0.052)), ((x, y - 0.014), (x + 0.027, y - 0.047))]
    else:
        limbs += [((x, y + 0.015), (x - 0.023, y - 0.014)), ((x, y + 0.015), (x + 0.023, y - 0.014)),
                  ((x, y - 0.014), (x - 0.014, y - 0.055)), ((x, y - 0.014), (x + 0.014, y - 0.055))]
    for (ax, ay), (cx, cy) in limbs:
        cylinder(b, (ax, ay, front + 0.005), (cx, cy, front + 0.005), 0.008,
                 'ART_SignalGlyph', segments=6)

def signals(b, name):
    """Kyobashi's signals as photographed: a grey three-lamp head (green, yellow, red from the
    driver's left) with deep hoods, hung by two short brackets from the mast arm above it; a dark
    pedestrian head with two square hooded lamps (red standing figure over green walking one)."""
    if name == 'ART_Signal_Support':
        cylinder(b, (0, -1, 0), (0, 1, 0), 0.5, 'ART_Frame', segments=12, end_radius=0.43)
    elif name == 'ART_Signal_Car':
        beveled_box(b, (0, 0, -0.05), (1.52, 0.40, 0.12), 'ART_SignalHousing', 0.05)
        # Authored X ends up mirrored in the GLB, and the lenses face the GLB's -Z: authored -X is
        # the left of whoever the lamps face. Japanese order from that side: green, yellow, red.
        for x, color in ((-0.52, 'ART_SignalGreen'), (0, 'ART_SignalYellow'), (0.52, 'ART_SignalRed')):
            cylinder(b, (x, 0, -0.09), (x, 0, 0.075), 0.235, 'ART_SignalHousing', segments=28, bevel=0.02)
            signal_lens(b, x, 0, 0.165, color, front=0.08, hood=0.26, arc=(-28, 208), hood_material='ART_SignalHood')
        for x in (-0.46, 0.46):     # brackets up to the mast arm (0.43 m above the head's centre)
            cylinder(b, (x, 0.19, -0.05), (x, 0.47, -0.05), 0.028, 'ART_Frame', segments=8)
    else:
        beveled_box(b, (0, 0, 0), (0.40, 0.74, 0.20), 'ART_SignalHousingDark', 0.04)
        square_lamp(b, 0.175, 'ART_SignalRed', False, 0.112)
        square_lamp(b, -0.175, 'ART_SignalGreen', True, 0.112)


def build(name):
    clear_asset(name)
    builder = SignalMesh(name)
    signals(builder, name.replace('HK_', 'ART_', 1))
    return builder.finish()


def build_all():
    for name in MODEL_NAMES:
        export(build(name), -1)
