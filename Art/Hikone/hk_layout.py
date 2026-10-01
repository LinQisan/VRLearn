"""Generates the Hikone castle-town placement list consumed by HikoneEnvironmentBuilder.cs.

Run with plain python3 (no Blender needed):  python3 Art/Hikone/hk_layout.py
Inputs : Art/Hikone/scene_constraints.json (exported from Gameplay_Hikone: road tiles,
         waypoints, spawns, goals, triggers, invisible walls, lamp poles)
Output : Assets/_Project/Hikone/Layout/hikone_layout.json + a validation report on stdout.

World frame = Unity (x east, y up, z north). Town ground (road surface) is y = 0.40.
"""
import json, math, os, random, sys

sys.path.insert(0, os.path.dirname(__file__))
from hk_terrain import (PLATEAU, terrain_height, TOWN_Z, PEN, CASTLE_Z, MOAT_X0, MOAT_X1,
                        CASTLE_GROUND, PEN_GROUND, WATER_Y, in_moat, on_peninsula)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
C = json.load(open(os.path.join(ROOT, "Art/Hikone/scene_constraints.json")))
OUT = os.path.join(ROOT, "Assets/_Project/Hikone/Layout/hikone_layout.json")
Y0 = 0.40
rnd = random.Random(20260928)

# ------------------------------------------------------------------ asset metrics
# width (x), depth (z, behind the facade line) of building footprints
BUILD = {
    "HK_Machiya_A": (7.2, 9.0), "HK_Machiya_A_Narrow": (5.4, 9.0),
    "HK_Machiya_B": (6.0, 9.0), "HK_Machiya_C": (8.4, 10.0), "HK_Kura": (6.0, 8.0),
}
ITEMS = []
FOOT = []   # (xmin, zmin, xmax, zmax, label)


def add(asset, x, y, z, yaw=0.0, s=(1, 1, 1), group="Props", foot=None, collider=None):
    e = {"a": asset, "p": [round(x, 3), round(y, 3), round(z, 3)], "r": round(yaw % 360, 2),
         "s": [round(v, 3) for v in s], "g": group}
    if collider:
        e["c"] = collider
    ITEMS.append(e)
    if foot:
        FOOT.append((*foot, asset))
    return e


def rot(lx, lz, yaw):
    a = math.radians(yaw)
    return lx * math.cos(a) + lz * math.sin(a), -lx * math.sin(a) + lz * math.cos(a)


def footprint(x, z, yaw, w, d, front=-0.5):
    pts = [rot(px, pz, yaw) for px in (-w / 2, w / 2) for pz in (front, -d)]
    xs = [x + p[0] for p in pts]; zs = [z + p[1] for p in pts]
    return (min(xs), min(zs), max(xs), max(zs))


# ------------------------------------------------------------------ constraints
TILES = [t for t in C["tiles"] if t["active"]]
PTS = [p for p in C["points"] if p["kind"] in ("waypoint", "spawn", "goal", "accident", "factory", "destroy", "signal") and abs(p["p"][0]) + abs(p["p"][2]) > 0.01]
POLES = C["poles"]


def rect_overlap(a, b, m=0.0):
    return a[0] < b[2] + m and a[2] > b[0] - m and a[1] < b[3] + m and a[3] > b[1] - m


def blocked(fp, point_buf=2.0, tile_margin=0.05):
    for t in TILES:
        if rect_overlap(fp, (t["min"][0], t["min"][1], t["max"][0], t["max"][1]), -tile_margin):
            return "tile " + t["name"]
    for p in PTS:
        x, z = p["p"][0], p["p"][2]
        if fp[0] - point_buf < x < fp[2] + point_buf and fp[1] - point_buf < z < fp[3] + point_buf:
            return f"{p['kind']} {p['name']}"
    for f in FOOT:
        if rect_overlap(fp, f[:4], -0.02):
            return "overlap " + f[4]
    return None


# ------------------------------------------------------------------ roads (merged runs)
BRIDGE = (22.0, TOWN_Z, 42.0, 52.0)          # north leg over the moat -> Kyobashi
ROAD_Y = 0.01        # carriageway height of the original road colliders (sidewalks: 0.41)
WALK_Y = 0.41
CASTLE_ROAD_END = -150.0                      # visual continuation of Castle Road to the south


def build_roads():
    """Replace every original road tile with a module on the same footprint (colliders stay).
    Exceptions that follow the Kyobashi map: the north leg over the moat becomes the bridge,
    and the south-west turn becomes a T so Castle Road can continue south (visual only)."""
    junctions = []
    for t in TILES:
        x0, z0 = t["min"]; x1, z1 = t["max"]
        cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
        if t["kind"] == "cross":
            add("HK_RoadSurf_Cross", cx, ROAD_Y, cz, 0, group="Roads"); junctions.append((x0, z0, x1, z1))
        elif t["kind"] == "tee":
            add("HK_RoadSurf_Tee", cx, ROAD_Y, cz, 0, group="Roads"); junctions.append((x0, z0, x1, z1))
        elif t["kind"] == "turn":
            def has(dx, dz):
                px, pz = cx + dx * 10.5, cz + dz * 10.5
                return any(u["min"][0] < px < u["max"][0] and u["min"][1] < pz < u["max"][1] for u in TILES if u is not t)
            edges = {(-1, 0): has(-1, 0), (1, 0): has(1, 0), (0, -1): has(0, -1), (0, 1): has(0, 1)}
            conn = tuple(sorted(k for k, v in edges.items() if v))
            if conn == ((-1, 0), (0, 1)) and cz < 0:
                # Castle Road x Honmachi: the turn stays drivable; HK_RoadRaised_Honmachi cuts
                # the corridor that lets Castle Road continue south
                add("HK_RoadSurf_TurnOpen", cx, ROAD_Y, cz, 90, group="Roads"); junctions.append((x0, z0, x1, z1))
                continue
            yaw = {((-1, 0), (0, -1)): 0, ((-1, 0), (0, 1)): 90, ((0, -1), (1, 0)): 270, ((0, 1), (1, 0)): 180}.get(conn)
            if yaw is None:
                raise SystemExit(f"cannot orient turn {t['name']}: {conn}")
            add("HK_RoadSurf_Turn", cx, ROAD_Y, cz, yaw, group="Roads"); junctions.append((x0, z0, x1, z1))
    junctions.append(BRIDGE)
    runs = {}
    for t in TILES:
        if t["kind"] != "straight":
            continue
        x0, z0 = t["min"]; x1, z1 = t["max"]
        if (x1 - x0) < (z1 - z0):
            key = ("x", round(z0), round(z1)); rng = (x0, x1)
        else:
            key = ("z", round(x0), round(x1)); rng = (z0, z1)
        runs.setdefault(key, []).append(list(rng))
    out = []

    def emit(axis, c0, c1, a, b, asset="HK_RoadSurf_Straight"):
        L = b - a
        if L < 0.5:
            return
        n = max(1, round(L / 10.0)); seg = L / n
        for i in range(n):
            m = a + seg * (i + 0.5)
            if axis == "x":
                add(asset, m, ROAD_Y, (c0 + c1) / 2, 0, (seg / 10, 1, 1), "Roads")
            else:
                add(asset, (c0 + c1) / 2, ROAD_Y, m, 90, (seg / 10, 1, 1), "Roads")
        out.append((axis, c0, c1, a, b))

    for key, spans in runs.items():
        spans.sort()
        merged = [spans[0]]
        for a, b in spans[1:]:
            if a <= merged[-1][1] + 0.01:
                merged[-1][1] = max(merged[-1][1], b)
            else:
                merged.append([a, b])
        axis, c0, c1 = key
        for a, b in merged:
            pieces = [(a, b)]
            for (jx0, jz0, jx1, jz1) in junctions:
                new = []
                for (pa, pb) in pieces:
                    same_band = (jz0 <= c0 + 0.1 and jz1 >= c1 - 0.1) if axis == "x" else (jx0 <= c0 + 0.1 and jx1 >= c1 - 0.1)
                    ja, jb = (jx0, jx1) if axis == "x" else (jz0, jz1)
                    if not same_band or jb <= pa or ja >= pb:
                        new.append((pa, pb)); continue
                    if pa < ja: new.append((pa, ja))
                    if jb < pb: new.append((jb, pb))
                pieces = new
            for pa, pb in pieces:
                emit(axis, c0, c1, pa, pb)
    # Castle Road continues south past Honmachi (no traffic there; purely the street view)
    emit("z", 22, 42, CASTLE_ROAD_END, -16.0, asset="HK_Road_Straight")
    # raised sidewalks / curbs / corner ramps copied from the original tiles (world space)
    add("HK_RoadRaised", 0, 0, 0, 0, group="Roads")
    add("HK_RoadRaised_Honmachi", 0, 0, 0, 0, group="Roads")
    return out


def build_lamps(runs):
    """New lanterns exactly on the original pole positions (their colliders/lights stay)."""
    for (x, y, z) in POLES:
        best = None
        if BRIDGE[0] <= x <= BRIDGE[2] and BRIDGE[1] <= z <= BRIDGE[3]:
            best = 90 if x < 32 else 270
        for axis, c0, c1, a, b in runs:
            if best is not None:
                break
            if axis == "x" and a - 1 <= x <= b + 1 and c0 - 1 <= z <= c1 + 1:
                best = 0 if z < (c0 + c1) / 2 else 180
            if axis == "z" and a - 1 <= z <= b + 1 and c0 - 1 <= x <= c1 + 1:
                best = 90 if x < (c0 + c1) / 2 else 270
        add("HK_StreetLamp", x, y, z, best if best is not None else 0, group="Lamps")


# ------------------------------------------------------------------ buildings
def frontage(x0, x1, zf, yaw, weights=None, gap=0.12, allow_kura=True):
    """Fill a straight frontage. yaw 0 faces +Z, 180 faces -Z, 90 faces +X, 270 faces -X.
    For yaw 0/180 the run is along x (x0..x1 at z=zf); for 90/270 along z (x0..x1 are z values at x=zf)."""
    weights = weights or {"HK_Machiya_A": 0.32, "HK_Machiya_B": 0.26, "HK_Machiya_C": 0.18, "HK_Machiya_A_Narrow": 0.12, "HK_Kura": 0.12 if allow_kura else 0}
    names = list(weights); w = [weights[n] for n in names]
    pos = x0
    placed = 0
    prev = None
    while True:
        name = rnd.choices(names, w)[0]
        if name == prev and rnd.random() < 0.6:
            name = rnd.choices(names, w)[0]
        W, D = BUILD[name]
        if pos + W > x1 + 0.01:
            # try to finish with a narrower one
            fits = [n for n in names if BUILD[n][0] <= x1 - pos + 0.01 and weights[n] > 0]
            if not fits:
                break
            name = min(fits, key=lambda n: abs(BUILD[n][0] - (x1 - pos)))
            W, D = BUILD[name]
        c = pos + W / 2
        setback = rnd.choice((0.0, 0.0, 0.15, 0.3))
        if yaw in (0, 180):
            z = zf - setback if yaw == 0 else zf + setback
            fp = footprint(c, z, yaw, W, D, front=0.0)
            why = blocked(fp)
            if not why:
                add(name, c, Y0, z, yaw, group="Buildings", foot=fp, collider="box")
                placed += 1
            else:
                REPORT.append(f"skip {name} at x={c:.1f} z={zf}: {why}")
        else:
            x = zf + setback if yaw == 90 else zf - setback
            fp = footprint(x, c, yaw, W, D, front=0.0)
            why = blocked(fp)
            if not why:
                add(name, x, Y0, c, yaw, group="Buildings", foot=fp, collider="box")
                placed += 1
            else:
                REPORT.append(f"skip {name} at z={c:.1f} x={zf}: {why}")
        prev = name
        pos += W + gap
    return placed


def scatter_buildings(region, n, yaws=(0, 180, 90, 270), kinds=("HK_Kura", "HK_Machiya_A", "HK_Machiya_A_Narrow")):
    x0, z0, x1, z1 = region
    placed = 0
    for _ in range(n * 12):
        if placed >= n:
            break
        name = rnd.choice(kinds); W, D = BUILD[name]
        yaw = rnd.choice(yaws)
        x = rnd.uniform(x0, x1); z = rnd.uniform(z0, z1)
        fp = footprint(x, z, yaw, W, D, front=0.0)
        if fp[0] < x0 or fp[2] > x1 or fp[1] < z0 or fp[3] > z1:
            continue
        # keep a 1.5 m lane around back buildings
        grown = (fp[0] - 1.5, fp[1] - 1.5, fp[2] + 1.5, fp[3] + 1.5)
        if blocked(grown):
            continue
        add(name, x, Y0, z, yaw, group="Buildings", foot=fp, collider="box")
        placed += 1
    return placed


def tree(asset, x, z, s=1.0, y=None, group="Trees", check=True, buf=1.2):
    fp = (x - buf, z - buf, x + buf, z + buf)
    if check and blocked(fp, point_buf=2.5):
        return False
    add(asset, x, Y0 if y is None else y, z, rnd.uniform(0, 360), (s, s * rnd.uniform(0.9, 1.1), s), group, foot=fp)
    return True


# ------------------------------------------------------------------ tree clearance
# crown radius at scale 1 (after the Blender models); street trees are pruned, so they must not
# swallow the lanterns, sign posts or signals, and no crown may lie on a roof
CROWN = {"HK_Tree_Zelkova": 3.0, "HK_Tree_Round": 3.0, "HK_Tree_Pine": 2.6, "HK_Tree_Cedar": 2.2,
         "HK_Tree_Sakura": 2.8, "HK_Tree_RoundLow": 3.3}
TOWN_TREE_GROUPS = ("StreetTrees", "Trees")
BUILT = ("HK_Machiya", "HK_Kura", "HK_Dobei", "HK_Gate", "HK_CastleWall")


def _obstacles():
    """(x, z, kind) of poles and lanterns the crowns must keep clear of."""
    out = []
    for e in ITEMS:
        x, _, z = e["p"]
        if e["a"] == "HK_StreetLamp":
            a = math.radians(e["r"])
            out.append((x, z, "pole"))
            out.append((x + 1.2 * math.sin(a), z + 1.2 * math.cos(a), "lantern"))
        elif e["g"] == "Signs" or e["a"] == "HK_GuidePost":
            out.append((x, z, "pole"))
    for (x, _, z) in POLES:
        out.append((x, z, "pole"))
    for p in PTS:
        if p["kind"] == "signal":
            out.append((p["p"][0], p["p"][2], "signal"))
    return out


def tree_conflict(asset, x, z, s=1.0, obstacles=None):
    r = CROWN.get(asset, 2.5) * s
    for (ox, oz, kind) in (obstacles if obstacles is not None else _obstacles()):
        d = math.hypot(ox - x, oz - z)
        if kind == "lantern" and d < r + 0.3:
            return "crown over a lantern"
        if kind in ("pole", "signal") and d < (r if kind == "signal" else 1.5):
            return "trunk/crown at a " + kind
    for f in FOOT:
        if not str(f[4]).startswith(BUILT):
            continue
        cx = min(max(x, f[0] + 0.3), f[2] - 0.3); cz = min(max(z, f[1] + 0.3), f[3] - 0.3)
        if math.hypot(cx - x, cz - z) < r * 0.85:
            return "crown on " + f[4]
    return None


def clear_town_trees():
    """Street trees slide along their row to a clear spot; scattered town trees are dropped."""
    obstacles = _obstacles()
    moved = dropped = 0
    for e in list(ITEMS):
        if e["g"] not in TOWN_TREE_GROUPS or e["a"] not in CROWN:
            continue
        x, y, z = e["p"]
        if not tree_conflict(e["a"], x, z, e["s"][0], obstacles):
            continue
        spot = None
        if e["g"] == "StreetTrees":
            for dz in (1, -1, 2, -2, 3, -3, 4, -4, 5, -5):
                if not tree_conflict(e["a"], x, z + dz, e["s"][0], obstacles) and not any(
                        math.hypot(p["p"][0] - x, p["p"][2] - (z + dz)) < 1.2 for p in PTS):
                    spot = z + dz
                    break
        if spot is None:
            ITEMS.remove(e); dropped += 1
            REPORT.append(f"   dropped {e['a']} at ({x:.1f}, {z:.1f}): {tree_conflict(e['a'], x, z, e['s'][0], obstacles)}")
        else:
            REPORT.append(f"   moved {e['a']} ({x:.1f}, {z:.1f}) -> z {spot:.1f}: {tree_conflict(e['a'], x, z, e['s'][0], obstacles)}")
            e["p"][2] = round(spot, 3); moved += 1
    return moved, dropped


def dobei_run(x0, z0, x1, z1, group="Walls"):
    L = math.hypot(x1 - x0, z1 - z0)
    n = max(1, round(L / 5.0)); seg = L / n
    yaw = -math.degrees(math.atan2(z1 - z0, x1 - x0))
    for i in range(n):
        t = (i + 0.5) / n
        x = x0 + (x1 - x0) * t; z = z0 + (z1 - z0) * t
        fp = (min(x0, x1) - 0.3, min(z0, z1) - 0.3, max(x0, x1) + 0.3, max(z0, z1) + 0.3) if i == 0 else None
        add("HK_Dobei_5m", x, Y0, z, yaw, (seg / 5.0, 1, 1), group, foot=fp, collider="box")


REPORT = []


def carriageway_overlap(fp, t):
    """True if a footprint touches the driven part of an original tile (8 m band / junction box)."""
    x0, z0 = t["min"]; x1, z1 = t["max"]
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    if t["kind"] == "straight":
        band = (x0, cz - 4.2, x1, cz + 4.2) if (x1 - x0) < (z1 - z0) else (cx - 4.2, z0, cx + 4.2, z1)
        return rect_overlap(fp, band, -0.05)
    return rect_overlap(fp, (cx - 8.5, cz - 8.5, cx + 8.5, cz + 8.5), -0.05)


def wall_run(asset, x0, z0, x1, z1, yaw, y, group="Moat", module=10.0):
    L = math.hypot(x1 - x0, z1 - z0)
    n = max(1, round(L / module)); seg = L / n
    for i in range(n):
        t = (i + 0.5) / n
        add(asset, x0 + (x1 - x0) * t, y, z0 + (z1 - z0) * t, yaw, (seg / module, 1, 1), group)


def main():
    runs = build_roads()
    build_lamps(runs)
    # ground: paved town (south of the moat), grass ring, castle-side terrain
    add("HK_GroundPaved", 0, Y0 - 0.004, 0, 0, group="Ground")
    add("HK_GroundFar", 0, Y0 - 0.06, 0, 0, group="Ground")
    add("HK_Terrain_Castle", 0, 0, 0, 0, group="Landscape")

    # reserved areas (nothing may be scattered there)
    FOOT.append((-136, 36.0, 112, 300, "moat + castle side"))
    FOOT.append((-80.2, -5.0, -59.8, 15.95, "parking"))
    FOOT.append((22.0, -152, 42.0, 16.0, "Castle Road corridor"))
    FOOT.append((1.5, 5.8, 18.5, 11.2, "gatehouse"))

    # ------------------------------------------------------------ Kyobashi intersection
    add("HK_Kyobashi", 32.0, ROAD_Y, TOWN_Z, 0, group="Moat")
    # town bank: stone wall + chain-post fence (skipped under the bridge deck)
    for (xa, xb) in ((MOAT_X0, 25.0), (39.0, MOAT_X1)):
        wall_run("HK_Ishigaki", xa, TOWN_Z, xb, TOWN_Z, 0, Y0)
        x = xa + 1.0
        while x + 2 <= xb - 0.5:
            add("HK_ChainFence", x + 1.0, Y0, TOWN_Z - 0.45, 0, group="Moat")
            x += 2.0
    # castle-side bank (tall stone walls up to the raised grounds) incl. the masugata wall
    wall_run("HK_Ishigaki_Tall", MOAT_X0, CASTLE_Z, MOAT_X1, CASTLE_Z, 180, CASTLE_GROUND)
    # masugata court across the bridge: low banks on three sides
    wall_run("HK_Ishigaki_Low", PEN[0], PEN[2], 25.0, PEN[2], 180, WALK_Y)
    wall_run("HK_Ishigaki_Low", 39.0, PEN[2], PEN[1], PEN[2], 180, WALK_Y)
    wall_run("HK_Ishigaki_Low", PEN[0], PEN[2], PEN[0], CASTLE_Z - 2.0, 270, WALK_Y)
    wall_run("HK_Ishigaki_Low", PEN[1], PEN[2], PEN[1], CASTLE_Z - 2.0, 90, WALK_Y)
    # moat ends
    wall_run("HK_Ishigaki_Low", MOAT_X0, TOWN_Z, MOAT_X0, CASTLE_Z - 2.2, 90, 0.45)
    wall_run("HK_Ishigaki_Low", MOAT_X1, TOWN_Z, MOAT_X1, CASTLE_Z - 2.2, 270, 0.45)
    add("HK_Water", (MOAT_X0 + MOAT_X1) / 2, WATER_Y, (TOWN_Z + CASTLE_Z) / 2, 0,
        ((MOAT_X1 - MOAT_X0) / 40.0, 1, (CASTLE_Z - TOWN_Z) / 40.0), group="Moat")
    # white castle walls on the masugata banks; the inner road ends at a wall
    wall_run("HK_CastleWall", 39.6, PEN[2] + 0.45, PEN[1] - 0.4, PEN[2] + 0.45, 0, WALK_Y, group="Castle")
    wall_run("HK_CastleWall", PEN[0] + 0.2, PEN[2] + 0.45, 24.8, PEN[2] + 0.45, 0, WALK_Y, group="Castle", module=4.0)
    wall_run("HK_CastleWall", PEN[0] + 0.45, 52.3, PEN[0] + 0.45, 73.6, 90, WALK_Y, group="Castle")
    wall_run("HK_CastleWall", 62.6, 52.2, 62.6, 71.8, 90, WALK_Y, group="Castle")
    add("HK_Yagura", 68.0, CASTLE_GROUND, 82.0, 180, group="Castle")
    wall_run("HK_CastleWall", 74.0, CASTLE_Z + 1.2, 104.0, CASTLE_Z + 1.2, 0, CASTLE_GROUND, group="Castle")
    add("HK_GuidePost", 38.2, Y0, 37.6, 270, group="Props")   # 彦根城 ->

    # ------------------------------------------------------------ Route 25 (south side only)
    frontage(-121.0, -81.0, 15.6, 0)
    frontage(-57.5, -0.6, 15.6, 0)
    frontage(42.6, 81.6, 15.6, 0)
    frontage(-4.0, 35.5, 102.5, 270)
    # parking lot (scenario 6/7 car exits here) — the "P" west of Castle Road on the map
    dobei_run(-80.0, 15.3, -68.8, 15.3)
    dobei_run(-63.2, 15.3, -60.0, 15.3)
    dobei_run(-80.3, -4.0, -80.3, 15.0)
    dobei_run(-59.8, -4.0, -59.8, 15.0)
    add("HK_Gate", -66.0, Y0, 15.2, 0, group="Walls")
    add("HK_Sign_Stop", -63.0, Y0, 14.6, 180, group="Signs")
    # west end: yagura gate over Route 25 encloses the original BlackWall (spawn occluder)
    add("HK_GateHouse_Yagura", -100.2, Y0, 26.0, 90, group="Walls", collider=None)

    # ------------------------------------------------------------ Castle Road (south leg)
    frontage(CASTLE_ROAD_END + 2, -17.6, 21.6, 90)
    frontage(CASTLE_ROAD_END + 2, -17.6, 42.4, 270)
    z = -21.0
    while z > CASTLE_ROAD_END + 3:
        for x in (26.8, 37.2):
            add("HK_Tree_Zelkova", x, Y0, z + rnd.uniform(-0.6, 0.6), rnd.uniform(0, 360), (1, rnd.uniform(0.92, 1.08), 1), "StreetTrees")
            add("HK_Hedge", x + (0.1 if x < 32 else -0.1), Y0, z - 5.0, 90, group="StreetTrees")
        z -= 10.0
    for (x, zz) in ((26.8, 9.0), (37.2, 7.0)):
        add("HK_Tree_Zelkova", x, Y0, zz, rnd.uniform(0, 360), group="StreetTrees")
    z = -24.0
    side = 0
    while z > CASTLE_ROAD_END + 4:
        add("HK_StreetLamp", 22.9 if side == 0 else 41.1, Y0, z, 90 if side == 0 else 270, group="Lamps")
        side ^= 1; z -= 16.0
    add("HK_Sign_Guide", 37.6, Y0, 6.0, 180, group="Signs")
    add("HK_Sign_Speed30", 37.4, Y0, -30.0, 180, group="Signs")
    add("HK_Sign_Speed30", 26.6, Y0, -60.0, 0, group="Signs")

    # ------------------------------------------------------------ Honmachi street (Edo machiya)
    frontage(-8.0, 21.6, -16.5, 0)
    add("HK_Machiya_A", -8.4, Y0, -6.0, 90, group="Buildings", foot=footprint(-8.4, -6.0, 90, 7.2, 9.0, 0.0), collider="box")
    # nagaya gate over the side street stub encloses BlackWall (1) (spawn occluder)
    add("HK_GateHouse_Nagaya", 10.0, Y0, 8.5, 0, group="Walls")
    # SW corner of the Kyobashi intersection: storehouse + corner house + wall. They stand
    # where the original corner building stood and keep the side-street spawn (8, 9) and the
    # Honmachi despawn zone (9, -8) out of sight from the crossings, as before.
    add("HK_Kura", 18.6, Y0, 15.6, 0, (1, 1, 0.6), group="Buildings", foot=(15.6, 10.8, 21.6, 15.6), collider="box")
    add("HK_Kura", 21.6, Y0, 1.4, 90, (0.87, 1, 0.75), group="Buildings", foot=(15.6, -1.2, 21.6, 4.0), collider="box")
    dobei_run(0.5, 3.6, 15.4, 3.6)
    dobei_run(21.4, 4.3, 21.4, 10.6)

    # ------------------------------------------------------------ signs on Route 25
    add("HK_Sign_Crosswalk", 44.8, Y0, 31.3, 90, group="Signs")
    add("HK_Sign_Crosswalk", 21.0, Y0, 20.9, 270, group="Signs")
    add("HK_Sign_Crosswalk", 37.3, Y0, 37.4, 0, group="Signs")
    add("HK_Sign_Crosswalk", 26.7, Y0, 13.4, 180, group="Signs")
    add("HK_Sign_Stop", 4.6, Y0, 14.8, 180, group="Signs")
    add("HK_Sign_School", 52.0, Y0, 31.2, 90, group="Signs")
    add("HK_Sign_School", -30.0, Y0, 20.8, 270, group="Signs")
    add("HK_Sign_Speed30", 88.0, Y0, 31.2, 90, group="Signs")
    add("HK_Sign_Speed30", -20.0, Y0, 20.8, 270, group="Signs")
    for (x, z0, z1) in ((27.55, 4.5, 14.0),):
        z = z0
        while z + 2 <= z1:
            cz = z + 1.0
            if all(math.hypot(px - x, pz - cz) > 1.6 for (px, py, pz) in POLES) and not any(
                    abs(p["p"][0] - x) < 1.5 and abs(p["p"][2] - cz) < 1.5 for p in PTS):
                add("HK_Guardrail", x, Y0, cz, 90, group="Props")
            z += 2.0
    for e in list(ITEMS):
        if e["g"] == "Buildings" and e["a"] == "HK_Machiya_B" and e["r"] == 0.0 and rnd.random() < 0.55:
            x, _, z = e["p"]
            if 55 < x < 78:
                continue
            zz = z + 0.55
            if not any(abs(p["p"][0] - (x + 1.6)) < 2.0 and abs(p["p"][2] - zz) < 2.0 for p in PTS):
                add("HK_Planter", x + 1.6, Y0, zz, 0, group="Props")

    # ------------------------------------------------------------ back lots
    scatter_buildings((-121, -45, -81, 5.5), 7)
    scatter_buildings((-57, -45, -9, 5.0), 7)
    scatter_buildings((44.5, -45, 81.5, 5.3), 7)
    scatter_buildings((-8, -60, 19.5, -27), 5)
    scatter_buildings((44.5, -150, 90, -46), 10)
    scatter_buildings((-40, -150, 19.5, -61), 10)
    n = 0
    for _ in range(1500):
        x = rnd.uniform(-134, 110); z = rnd.uniform(-150, 6)
        if tree(rnd.choice(("HK_Tree_Round", "HK_Tree_Round", "HK_Tree_Pine", "HK_Tree_Cedar")), x, z, rnd.uniform(0.8, 1.25), buf=1.6):
            n += 1
        if n >= 80:
            break
    REPORT.append(f"town trees: {n}")

    # ------------------------------------------------------------ castle grounds + hill
    cx, cz, hx, hz, top = PLATEAU

    def wall_line(x0, z0, x1, z1, yaw):
        wall_run("HK_Ishigaki_Tall", x0, z0, x1, z1, yaw, top, group="Castle")
    wall_line(cx - hx, cz - hz, cx + hx, cz - hz, 180)
    wall_line(cx - hx, cz + hz, cx + hx, cz + hz, 0)
    wall_line(cx - hx, cz - hz, cx - hx, cz + hz, 270)
    wall_line(cx + hx, cz - hz, cx + hx, cz + hz, 90)
    for i in range(6):
        x = cx - hx + 5 + i * 10
        if abs(x - cx) > 6:
            add("HK_CastleWall", x, top, cz - hz + 1.0, 0, group="Castle")
    add("HK_HikoneCastle", cx - 2.0, top, cz + 4.0, 180, (1.25, 1.25, 1.25), group="Castle")
    add("HK_Yagura", cx - hx + 8.0, top, cz - hz + 5.5, 180, group="Castle")
    add("HK_Yagura", cx + hx - 8.0, top, cz - hz + 5.5, 180, (1.4, 1, 1), group="Castle")
    for (x, z) in ((cx - 20, cz + 14), (cx + 18, cz + 15), (cx + 22, cz - 4), (cx - 24, cz - 2)):
        add("HK_Tree_Pine", x, top, z, rnd.uniform(0, 360), (1.1, 1.1, 1.1), "Castle")

    count = 0
    # dense tree mass right behind the castle-side stone walls (as in the Kyobashi photo)
    x = MOAT_X0 + 2
    while x < MOAT_X1 - 2:
        if not (PEN[0] - 1 < x < PEN[1] + 1) and not (64 < x < 106):
            for zz in (CASTLE_Z + 2.5, CASTLE_Z + 7.5):
                z = zz + rnd.uniform(-1.2, 1.2)
                asset = rnd.choice(("HK_Tree_Pine", "HK_Tree_RoundLow", "HK_Tree_RoundLow", "HK_Tree_Sakura"))
                add(asset, x + rnd.uniform(-1.5, 1.5), terrain_height(x, z) - 0.1, z, rnd.uniform(0, 360),
                    (rnd.uniform(1.0, 1.35),) * 3, "Forest")
                count += 1
        x += rnd.uniform(5.5, 8.0)
    # trees behind the masugata wall
    for x in range(24, 62, 7):
        z = CASTLE_Z + 3.0 + rnd.uniform(-1, 1)
        add(rnd.choice(("HK_Tree_RoundLow", "HK_Tree_Pine")), x + rnd.uniform(-1.5, 1.5), CASTLE_GROUND - 0.1, z, rnd.uniform(0, 360), (1.2, 1.2, 1.2), "Forest")
        count += 1
    # castle grounds and the hill slopes
    for gx in range(-156, 222, 9):
        for gz in range(90, 296, 9):
            x = gx + rnd.uniform(-3, 3); z = gz + rnd.uniform(-3, 3)
            dx = max(0.0, abs(x - cx) - hx); dz = max(0.0, abs(z - cz) - hz)
            r = math.hypot(dx, dz)
            if r < 4.0 or in_moat(x, z, -1.0) or on_peninsula(x, z):
                continue
            far = r > 70
            if far and rnd.random() < (0.6 if abs(x - cx) < 120 else 0.88):
                continue
            h = terrain_height(x, z)
            k = rnd.random()
            asset = "HK_Tree_Cedar" if k < 0.45 else ("HK_Tree_RoundLow" if k < 0.88 else "HK_Tree_Pine")
            s = rnd.uniform(0.9, 1.5) if asset != "HK_Tree_Pine" else rnd.uniform(1.0, 1.4)
            add(asset, x, h - 0.2, z, rnd.uniform(0, 360), (s, s * rnd.uniform(0.9, 1.15), s), "Forest")
            count += 1
    REPORT.append(f"castle-side trees: {count}")

    moved, dropped = clear_town_trees()
    REPORT.append(f"town trees moved clear of lamps/signs/buildings: {moved}, dropped: {dropped}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    json.dump({"version": 2, "items": ITEMS}, open(OUT, "w"), ensure_ascii=False, indent=0)

    # ------------------------------------------------------------ validation
    from collections import Counter
    print("items", len(ITEMS), Counter(e["g"] for e in ITEMS))
    bad = 0
    for e in ITEMS:
        a = e["a"]
        if a in BUILD:
            fp = footprint(e["p"][0], e["p"][2], e["r"], *BUILD[a], front=0.0)
            for t in TILES:
                if carriageway_overlap(fp, t):
                    print("!! building on carriageway", a, e["p"], t["name"]); bad += 1
            for p in PTS:
                if fp[0] - 0.5 < p["p"][0] < fp[2] + 0.5 and fp[1] - 0.5 < p["p"][2] < fp[3] + 0.5:
                    print("!! building on gameplay point", a, e["p"], p["name"]); bad += 1
        if e["g"] in ("Forest", "StreetTrees", "Trees"):
            for p in PTS:
                if math.hypot(p["p"][0] - e["p"][0], p["p"][2] - e["p"][2]) < 1.2:
                    print("!! tree on gameplay point", a, e["p"], p["name"]); bad += 1
    for e in ITEMS:
        if e["g"] in TOWN_TREE_GROUPS and e["a"] in CROWN:
            why = tree_conflict(e["a"], e["p"][0], e["p"][2], e["s"][0])
            if why:
                print("!! tree", why, e["a"], e["p"]); bad += 1
    print("validation problems:", bad)
    for r in REPORT[-30:]:
        print("  ", r)


if __name__ == "__main__":
    main()
