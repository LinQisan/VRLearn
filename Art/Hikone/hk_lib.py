"""Shared helpers for the Hikone castle-town asset generator (run inside Blender).

Authoring convention: every helper takes *Unity* coordinates (x east, y up, z north).
Blender -> FBX (-Z forward, Y up) -> Unity mirrors X, so vertices are written to Blender as
(-x, -z, y) and every face's winding is reversed to keep normals outward after the mirror.
Verified in Unity: guide-post arm (+x), lamp arm (+z) and terrain bounds land where authored.
"""
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix

ROOT = "/Users/xinyu/VRLearn"
MODEL_DIR = ROOT + "/Assets/_Project/Hikone/Models"
TEX_DIR = ROOT + "/Assets/_Project/Hikone/Textures"


def U(x, y, z):
    return Vector((-x, -z, y))


# ---------------------------------------------------------------- materials
MAT_COLORS = {
    "HK_Plaster": (0.92, 0.91, 0.87), "HK_PlasterWarm": (0.88, 0.84, 0.76),
    "HK_WoodDark": (0.16, 0.11, 0.08), "HK_WoodMid": (0.36, 0.25, 0.16),
    "HK_WoodLight": (0.62, 0.48, 0.32), "HK_RoofTile": (0.24, 0.25, 0.27),
    "HK_RoofRidge": (0.18, 0.19, 0.21), "HK_Stone": (0.56, 0.54, 0.50),
    "HK_StoneDark": (0.40, 0.39, 0.37), "HK_Asphalt": (0.30, 0.30, 0.31),
    "HK_Sidewalk": (0.70, 0.66, 0.58), "HK_Curb": (0.66, 0.64, 0.60),
    "HK_WhitePaint": (0.94, 0.94, 0.92), "HK_Water": (0.16, 0.28, 0.27),
    "HK_Grass": (0.34, 0.44, 0.22), "HK_Soil": (0.36, 0.30, 0.22),
    "HK_Leaf": (0.26, 0.40, 0.18), "HK_LeafDark": (0.15, 0.28, 0.15),
    "HK_Sakura": (0.93, 0.74, 0.80), "HK_Bark": (0.28, 0.21, 0.16),
    "HK_Metal": (0.20, 0.20, 0.21), "HK_MetalLight": (0.62, 0.63, 0.64),
    "HK_Glass": (0.12, 0.14, 0.15), "HK_Shoji": (0.95, 0.92, 0.82),
    "HK_Noren": (0.14, 0.19, 0.34), "HK_NorenRed": (0.55, 0.14, 0.12),
    "HK_Lamp": (1.0, 0.86, 0.62), "HK_Gold": (0.78, 0.62, 0.28),
    "HK_Namako": (0.35, 0.36, 0.38), "HK_SignRed": (0.78, 0.10, 0.10),
    "HK_SignBlue": (0.08, 0.30, 0.66), "HK_SignYellow": (0.95, 0.78, 0.10),
    "HK_Guard": (0.30, 0.22, 0.16),
}


def mat(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        c = MAT_COLORS.get(name, (0.8, 0.8, 0.8))
        m.diffuse_color = (*c, 1.0)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (*c, 1.0)
            bsdf.inputs["Roughness"].default_value = 0.8
    return m


# ---------------------------------------------------------------- mesh builder
class MB:
    """Accumulates quads/tris per material and box-projects UVs (1 UV unit = 1 m)."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def mi(self, m):
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def face(self, pts, m, uv_scale=1.0):
        vs = [self.bm.verts.new(U(*p)) for p in reversed(pts)]
        f = self.bm.faces.new(vs)
        f.material_index = self.mi(m)
        f.normal_update()
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for loop in f.loops:
            co = loop.vert.co
            if ax == 0:
                u, v = co.y, co.z
            elif ax == 1:
                u, v = co.x, co.z
            else:
                u, v = co.x, co.y
            loop[self.uv].uv = (u * uv_scale, v * uv_scale)
        return f

    def box(self, c, s, m, top=True, bottom=False, rot_y=0.0):
        """Axis box centred at c (unity) with size s; optional yaw (degrees)."""
        cx, cy, cz = c
        hx, hy, hz = s[0] / 2, s[1] / 2, s[2] / 2
        ca, sa = math.cos(math.radians(rot_y)), math.sin(math.radians(rot_y))

        def P(x, y, z):
            return (cx + x * ca + z * sa, cy + y, cz - x * sa + z * ca)

        v = [P(x, y, z) for x in (-hx, hx) for y in (-hy, hy) for z in (-hz, hz)]
        # index = xi*4 + yi*2 + zi
        faces = [
            (0, 1, 3, 2), (4, 6, 7, 5),  # -x, +x
            (1, 5, 7, 3), (0, 2, 6, 4),  # +z, -z
        ]
        if top:
            faces.append((2, 3, 7, 6))
        if bottom:
            faces.append((0, 4, 5, 1))
        for fi in faces:
            self.face([v[i] for i in fi], m)

    def prism(self, pts2d, y0, y1, m, top=True, bottom=False, m_top=None):
        """Extrude a polygon given CLOCKWISE on a north-up (x right, z up) map, y0..y1."""
        n = len(pts2d)
        for i in range(n):
            a, b = pts2d[i], pts2d[(i + 1) % n]
            self.face([(a[0], y0, a[1]), (b[0], y0, b[1]), (b[0], y1, b[1]), (a[0], y1, a[1])], m)
        if top:
            self.face([(p[0], y1, p[1]) for p in pts2d], m_top or m)
        if bottom:
            self.face([(p[0], y0, p[1]) for p in reversed(pts2d)], m)

    def quad_up(self, pts2d, y, m):
        """Flat upward-facing polygon (clockwise on north-up map) at height y."""
        self.face([(p[0], y, p[1]) for p in pts2d], m)

    def cyl(self, c, r, h, m, seg=8, r_top=None, top=True):
        cx, cy, cz = c
        rt = r if r_top is None else r_top
        ring0 = [(cx + r * math.cos(2 * math.pi * i / seg), cz + r * math.sin(2 * math.pi * i / seg)) for i in range(seg)]
        ring1 = [(cx + rt * math.cos(2 * math.pi * i / seg), cz + rt * math.sin(2 * math.pi * i / seg)) for i in range(seg)]
        for i in range(seg):
            j = (i + 1) % seg
            self.face([(ring0[j][0], cy, ring0[j][1]), (ring0[i][0], cy, ring0[i][1]),
                       (ring1[i][0], cy + h, ring1[i][1]), (ring1[j][0], cy + h, ring1[j][1])], m)
        if top and rt > 0.001:
            self.face([(p[0], cy + h, p[1]) for p in reversed(ring1)], m)

    def blob(self, c, r, m, sy=1.0, seed=0, subdiv=2, jitter=0.18):
        """Low-poly icosphere foliage blob."""
        rnd = random.Random(seed)
        tmp = bmesh.new()
        bmesh.ops.create_icosphere(tmp, subdivisions=subdiv, radius=1.0)
        for v in tmp.verts:
            k = 1.0 + rnd.uniform(-jitter, jitter)
            v.co = Vector((v.co.x * r * k, v.co.y * r * k, v.co.z * r * sy * k))
        for f in tmp.faces:
            pts = []
            for v in f.verts:
                # tmp is a local blender-space sphere: proper rotation into unity axes
                ux, uz, uy = v.co.x, -v.co.y, v.co.z
                pts.append((c[0] + ux, c[1] + uy, c[2] + uz))
            self.face(pts, m)
        tmp.free()

    def finish(self, origin_note=""):
        me = bpy.data.meshes.new(self.name)
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=0.0005)
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(mat(m))
        ob = bpy.data.objects.new(self.name, me)
        col = bpy.data.collections.get("HK_Assets") or bpy.data.collections.new("HK_Assets")
        if col.name not in bpy.context.scene.collection.children:
            bpy.context.scene.collection.children.link(col)
        col.objects.link(ob)
        return ob


def clear_asset(name):
    ob = bpy.data.objects.get(name)
    if ob:
        me = ob.data
        bpy.data.objects.remove(ob)
        if me and me.users == 0:
            bpy.data.meshes.remove(me)


def export(ob, spacing_index=0):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    loc = ob.location.copy()
    ob.location = (0, 0, 0)
    bpy.ops.export_scene.fbx(
        filepath=f"{MODEL_DIR}/{ob.name}.fbx", use_selection=True,
        axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='FACE',
        add_leaf_bones=False, use_mesh_modifiers=True, path_mode='STRIP')
    # lay assets out in a grid in the .blend so they can be inspected
    ob.location = loc if spacing_index < 0 else Vector(((spacing_index % 8) * 30.0, (spacing_index // 8) * 30.0, 0))


# ---------------------------------------------------------------- textures
def save_tex(name, arr):
    """arr: numpy float array HxWx3 in 0..1 -> PNG in TEX_DIR."""
    import numpy as np
    h, w, _ = arr.shape
    img = bpy.data.images.get(name) or bpy.data.images.new(name, w, h, alpha=False)
    if img.size[0] != w:
        img.scale(w, h)
    rgba = np.ones((h, w, 4), dtype=np.float32)
    rgba[:, :, :3] = np.clip(arr, 0, 1)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = f"{TEX_DIR}/{name}.png"
    img.file_format = 'PNG'
    img.save()
