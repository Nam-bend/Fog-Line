"""Build an original PSX-style double-barrel shotgun and Unity-ready assets."""
from pathlib import Path
import bpy
import math
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource" / "TraditionalDoubleBarrel"
OUT = ROOT / "Assets" / "_Project" / "Art" / "Weapons" / "TraditionalDoubleBarrel"
MODEL_OUT = OUT / "Models"
TEXTURE_OUT = OUT / "Textures"
SOURCE.mkdir(parents=True, exist_ok=True)
MODEL_OUT.mkdir(parents=True, exist_ok=True)
TEXTURE_OUT.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = 900
scene.render.resolution_y = 650
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.world = bpy.data.worlds.new("PreviewWorld")
scene.world.color = (0.012, 0.009, 0.012)

collection = bpy.data.collections.new("BLOOD-MALL-INSPIRED | ORIGINAL SHOTGUN")
scene.collection.children.link(collection)
root = bpy.data.objects.new("TraditionalDoubleBarrel_Root", None)
collection.objects.link(root)

# A tiny nearest-filtered palette atlas keeps the model crisp in Unity.
palette = {
    "steel": ((0.14, 0.16, 0.15, 1), (5, 5)),
    "edge": ((0.29, 0.31, 0.27, 1), (17, 5)),
    "black": ((0.018, 0.014, 0.012, 1), (29, 5)),
    "wood": ((0.25, 0.075, 0.026, 1), (41, 5)),
    "wood_light": ((0.48, 0.18, 0.055, 1), (53, 5)),
    "brass": ((0.48, 0.31, 0.08, 1), (5, 17)),
}
size = 64
image = bpy.data.images.new("DBS_Palette64", size, size, alpha=True)
pixels = [0.035, 0.025, 0.02, 1.0] * size * size
for color, (cx, cy) in palette.values():
    for y in range(cy - 4, cy + 5):
        for x in range(cx - 4, cx + 5):
            i = (y * size + x) * 4
            pixels[i:i + 4] = color
image.pixels = pixels
image.filepath_raw = str(TEXTURE_OUT / "DBS_Palette64.png")
image.file_format = "PNG"
image.save()

mat = bpy.data.materials.new("TraditionalDBS_Pixel")
mat.use_nodes = True
mat.diffuse_color = palette["steel"][0]
nodes = mat.node_tree.nodes
for n in list(nodes):
    nodes.remove(n)
out_node = nodes.new("ShaderNodeOutputMaterial")
shader = nodes.new("ShaderNodeBsdfPrincipled")
tex = nodes.new("ShaderNodeTexImage")
tex.image = image
tex.interpolation = "Closest"
shader.inputs["Roughness"].default_value = 0.78
shader.inputs["Metallic"].default_value = 0.18
mat.node_tree.links.new(tex.outputs["Color"], shader.inputs["Base Color"])
mat.node_tree.links.new(shader.outputs["BSDF"], out_node.inputs["Surface"])

parts = []

def finish(obj, color="steel", bevel=0.025):
    obj.data.materials.append(mat)
    uv = obj.data.uv_layers.new(name="UVMap")
    cx, cy = palette[color][1]
    uv_coord = ((cx + 0.5) / size, (cy + 0.5) / size)
    for loop in uv.data:
        loop.uv = uv_coord
    if bevel:
        mod = obj.modifiers.new("Worn edges", "BEVEL")
        mod.width = bevel
        mod.segments = 1
        mod.affect = "EDGES"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.parent = root
    parts.append(obj)
    return obj

def box(name, loc, scale, color="steel", bevel=0.025, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return finish(obj, color, bevel)

def cylinder(name, loc, radius, depth, color="steel", vertices=8, axis_y=True, bevel=0.015):
    rotation = (math.pi / 2, 0, 0) if axis_y else (0, 0, 0)
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                        end_fill_type="NGON", location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish(obj, color, bevel)

def stock_mesh():
    # Extruded, angular stock profile: narrow wrist, heavy shoulder and crooked heel.
    profile = [(-2.65, 0.48), (-2.32, 0.86), (-1.34, 1.02), (-0.73, 1.04),
               (-0.66, 0.71), (-1.13, 0.57), (-1.84, 0.27), (-2.56, 0.15)]
    half = 0.22
    verts = [(x, y, z) for x in (-half, half) for y, z in profile]
    n = len(profile)
    faces = [tuple(range(n)), tuple(range(n, 2*n))[::-1]]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n+j, n+i))
    mesh = bpy.data.meshes.new("AngularStock_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("Angular_Wood_Stock", mesh)
    collection.objects.link(obj)
    return finish(obj, "wood", 0.055)

stock_mesh()
box("Stock_Highlight", (0.225, -1.70, 0.61), (0.022, 0.57, 0.105), "wood_light", 0.01,
    rotation=(math.radians(-10), 0, 0))
box("Receiver", (0, -0.18, 0.99), (0.40, 0.56, 0.37), "steel", 0.055)
box("Receiver_Top_Rib", (0, -0.12, 1.39), (0.27, 0.49, 0.055), "edge", 0.018)
box("Breech_Block", (0, 0.42, 1.02), (0.42, 0.09, 0.38), "edge", 0.025)

# Side-by-side barrels, dark bores and a crude front bead. Keep the breech end
# fixed while extending the barrels forward, so they still meet the receiver.
for x in (-0.205, 0.205):
    cylinder(f"Barrel_{'L' if x < 0 else 'R'}", (x, 2.14, 1.17), 0.225, 3.42, "steel", 10, True, 0.012)
    cylinder(f"Muzzle_Ring_{'L' if x < 0 else 'R'}", (x, 3.87, 1.17), 0.242, 0.10, "edge", 10, True, 0.009)
    cylinder(f"Bore_{'L' if x < 0 else 'R'}", (x, 3.926, 1.17), 0.168, 0.015, "black", 10, True, 0)
cylinder("Brass_Front_Bead", (0, 3.77, 1.40), 0.045, 0.07, "brass", 8, False, 0.008)
box("Underlug", (0, 0.70, 0.79), (0.19, 0.29, 0.12), "edge", 0.025)

# External hammers, break lever, trigger pair and a blocky trigger guard.
for x in (-0.215, 0.215):
    cylinder(f"Hammer_{'L' if x < 0 else 'R'}", (x, -0.64, 1.39), 0.115, 0.12, "edge", 8, False, 0.012)
    box(f"Hammer_Spur_{'L' if x < 0 else 'R'}", (x, -0.72, 1.54), (0.07, 0.14, 0.055), "edge", 0.012,
        rotation=(math.radians(-18), 0, 0))
box("Break_Lever", (0, -0.63, 1.30), (0.20, 0.19, 0.045), "edge", 0.018,
    rotation=(0, 0, math.radians(-10)))
for x, y in ((-0.10, -0.32), (0.10, -0.48)):
    box("Trigger", (x, y, 0.57), (0.035, 0.075, 0.16), "brass", 0.012,
        rotation=(math.radians(-18), 0, 0))
box("Guard_Front", (0, 0.02, 0.56), (0.22, 0.045, 0.055), "black", 0.018)
box("Guard_Rear", (0, -0.66, 0.55), (0.22, 0.045, 0.055), "black", 0.018)
for x in (-0.22, 0.22):
    box("Guard_Side", (x, -0.32, 0.55), (0.045, 0.34, 0.055), "black", 0.018)

# Fore-end and intentionally oversized hinge pin make the silhouette readable in FPS view.
box("Wood_ForeEnd", (0, 0.92, 0.72), (0.34, 0.54, 0.17), "wood", 0.07)
box("ForeEnd_Highlight", (0.26, 0.94, 0.88), (0.030, 0.40, 0.025), "wood_light", 0.008)
cylinder("Hinge_Pin", (0, 0.37, 0.77), 0.14, 0.88, "brass", 10, False, 0.012)

# Keep the complete barrel/fore-end assembly separate so the break-action can
# rotate around the hinge during reload. Everything else remains one mesh.
barrel_prefixes = ("Barrel_", "Muzzle_Ring_", "Bore_", "Wood_ForeEnd", "ForeEnd_Highlight")
barrel_names = {"Brass_Front_Bead", "Underlug"}
barrel_parts = [obj for obj in parts if obj.name.startswith(barrel_prefixes) or obj.name in barrel_names]
body_parts = [obj for obj in parts if obj not in barrel_parts]

def join_group(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    joined = bpy.context.object
    joined.name = name
    for poly in joined.data.polygons:
        poly.use_smooth = False
    return joined

weapon = join_group(body_parts, "TraditionalDoubleBarrel_Body")
weapon.parent = root

barrel_mesh = join_group(barrel_parts, "BreakAction_Barrels")
barrel_world = barrel_mesh.matrix_world.copy()
barrel_pivot = bpy.data.objects.new("BarrelPivot", None)
barrel_pivot.location = (0, 0.37, 0.77)
barrel_pivot.parent = root
collection.objects.link(barrel_pivot)
bpy.context.view_layer.update()
barrel_mesh.parent = barrel_pivot
barrel_mesh.matrix_parent_inverse = barrel_pivot.matrix_world.inverted()
barrel_mesh.matrix_basis = barrel_world
bpy.context.view_layer.update()
assert all(abs(barrel_mesh.matrix_world[r][c] - barrel_world[r][c]) < 1e-5
           for r in range(4) for c in range(4)), "Parenting moved the barrel mesh"

# Export only the asset hierarchy. +Y in Blender becomes +Z forward in Unity.
bpy.ops.object.select_all(action="DESELECT")
root.select_set(True)
weapon.select_set(True)
barrel_pivot.select_set(True)
barrel_mesh.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(filepath=str(MODEL_OUT / "TraditionalDoubleBarrel.fbx"), use_selection=True,
                         object_types={"EMPTY", "MESH"}, apply_scale_options="FBX_SCALE_ALL",
                         axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                         bake_anim=False, path_mode="AUTO", embed_textures=False)
bpy.ops.export_scene.gltf(filepath=str(SOURCE / "TraditionalDoubleBarrel.glb"), use_selection=True,
                          export_format="GLB", export_yup=True)

# Presentation render for quick review outside Blender.
floor_mat = bpy.data.materials.new("PreviewFloor")
floor_mat.diffuse_color = (0.035, 0.028, 0.03, 1)
bpy.ops.mesh.primitive_plane_add(size=20, location=(0, 0.65, 0.02))
floor = bpy.context.object
floor.data.materials.append(floor_mat)
for location, energy, color, size_light in [
    ((-3.2, -2.4, 5.5), 1100, (1.0, 0.24, 0.12), 3.0),
    ((4.0, 2.5, 4.0), 900, (0.16, 0.30, 1.0), 2.5),
    ((0.0, 4.5, 5.0), 700, (1.0, 0.72, 0.40), 2.0),
]:
    data = bpy.data.lights.new("PreviewLight", "AREA")
    data.energy, data.color, data.shape, data.size = energy, color, "DISK", size_light
    lamp = bpy.data.objects.new("PreviewLight", data)
    scene.collection.objects.link(lamp)
    lamp.location = location
    direction = Vector((0, 0.7, 0.9)) - lamp.location
    lamp.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

camera_data = bpy.data.cameras.new("PreviewCamera")
camera = bpy.data.objects.new("PreviewCamera", camera_data)
scene.collection.objects.link(camera)
camera.location = (4.9, -5.8, 3.5)
camera.data.lens = 57
camera.rotation_euler = (Vector((0, 0.55, 1.02)) - camera.location).to_track_quat("-Z", "Y").to_euler()
scene.camera = camera
scene.render.filepath = str(SOURCE / "Preview.png")
scene.render.film_transparent = False
bpy.ops.render.render(write_still=True)

bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "TraditionalDoubleBarrel.blend"))
triangles = sum(len(p.vertices) - 2 for obj in (weapon, barrel_mesh) for p in obj.data.polygons)
(SOURCE / "asset_stats.txt").write_text(
    f"TraditionalDoubleBarrel_Mesh\ntriangles={triangles}\ntexture=64x64\nforward=Unity +Z\n",
    encoding="utf-8")
print(f"ASSET_COMPLETE triangles={triangles} fbx={MODEL_OUT / 'TraditionalDoubleBarrel.fbx'}")
