"""Moat / castle-grounds / castle-hill height function shared by the Blender mesh and the layout.

Real-world reference (Kyobashi intersection, Hikone):
  Route 25 (Nakabori-dori) runs along the middle moat; Yume-Kyobashi Castle Road meets it from
  the south; Kyobashi bridge crosses the moat to the north and ends at the Kyobashi-guchi
  masugata (stone-walled gate court) where the road turns.
Mapped onto the existing gameplay road network:
  main E-W road (z 16..36)        -> Route 25, moat directly north of it
  north leg x 22..42, z 36..52    -> Kyobashi bridge (z 39.6..52)
  road z 52..72 east to x 62      -> road inside the masugata, closed by the stone wall at z 76
  south leg + southern road       -> Castle Road (continues south, visual only) / Honmachi street
"""
import math

TOWN_Z = 39.6                      # town-side moat bank (top edge of the stone wall)
MOAT_X0, MOAT_X1 = -150.0, 200.0
PEN = (21.0, 63.0, 51.5, 76.0)     # masugata peninsula: x0, x1, south bank z, north wall z
CASTLE_Z = 76.0                    # castle-side bank (outside the peninsula)
CASTLE_GROUND = 4.4                # raised castle grounds behind the stone walls
PEN_GROUND = -0.05         # below the carriageway (0.01) so the masugata road shows
TOWN_GROUND = 0.40
WATER_BED = -3.2
WATER_Y = -1.35
PLATEAU = (-40.0, 205.0, 30.0, 22.0, 50.0)     # cx, cz, half-x, half-z, height (honmaru)
WALL_H = 6.0
INSET = 1.4                        # moat floor starts this far inside the wall tops

BREAK_X = [MOAT_X0, MOAT_X0 + INSET, PEN[0] - INSET, PEN[0], PEN[1], PEN[1] + INSET, MOAT_X1 - INSET, MOAT_X1]
BREAK_Z = [TOWN_Z, TOWN_Z + INSET, PEN[2] - INSET, PEN[2], CASTLE_Z - INSET, CASTLE_Z]


def in_moat(x, z, inset=0.0):
    if not (MOAT_X0 + inset < x < MOAT_X1 - inset) or z <= TOWN_Z + inset:
        return False
    if PEN[0] - inset <= x <= PEN[1] + inset:
        return z < PEN[2] - inset
    return z < CASTLE_Z - inset


def on_peninsula(x, z):
    return PEN[0] <= x <= PEN[1] and PEN[2] <= z < PEN[3]


def terrain_height(x, z):
    if z <= TOWN_Z + 1e-6:
        return TOWN_GROUND
    if in_moat(x, z, INSET - 1e-3):
        return WATER_BED
    if in_moat(x, z):
        return TOWN_GROUND          # vertices under the battered walls; hidden by the stone
    if on_peninsula(x, z):
        return PEN_GROUND
    if z < CASTLE_Z and not (MOAT_X0 < x < MOAT_X1):
        return 0.45                 # beyond the moat ends
    cx, cz, hx, hz, top = PLATEAU
    base = CASTLE_GROUND
    dx = max(0.0, abs(x - cx) - hx)
    dz = max(0.0, abs(z - cz) - hz)
    r = math.hypot(dx, dz)
    if r <= 0.0:
        return top
    R = 64.0
    t = min(1.0, r / R)
    s = 1 - (3 * t * t - 2 * t * t * t)
    h = base + (top - WALL_H - base) * s ** 1.15
    if r < 1.2:
        h = top - WALL_H
    n = math.sin(x * 0.11) * math.cos(z * 0.09) * 1.3 + math.sin(x * 0.037 + z * 0.05) * 1.0
    h += n * min(1.0, s * 3) * (1 - s) * 2.2
    return max(h, base)
