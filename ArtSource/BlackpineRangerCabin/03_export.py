import bpy, json, os
from mathutils import Vector
base=r"C:/Users/namphung/FPS/ArtSource/BlackpineRangerCabin"
source=bpy.context.scene
asset=bpy.data.collections["CABIN - 6m x 8m + 2m porch"]
# Persist hardware transforms under the editable hinge.
door=bpy.data.objects["DOOR - hinge pivot"]
for o in asset.objects:
 if o.name.startswith(("Door board joint","Iron door strap","Door latch")):
  world=o.matrix_world.copy();o.parent=door;o.matrix_world=world
# Build a compact export scene without studio items; original source stays editable.
export_scene=bpy.data.scenes.new("Blackpine Cabin - Compact FBX")
def group(o):
 n=o.name
 if n.startswith(("DOOR","Door board joint","Iron door strap","Door latch")): return "Door"
 if "glass" in n.lower() and "Lantern" not in n: return "WindowGlass"
 if n.startswith(("Corrugated","Folded metal","Canopy drip")): return "MetalRoof"
 if n.startswith(("Chimney","Dark flue")): return "Chimney"
 if n.startswith(("Lantern","Toolbox","Handle stand","Stacked firewood")): return "PorchProps"
 if n.startswith(("Cabin name","Sign lettering")): return "Sign"
 if n.startswith(("Subtle","Sparse moss")): return "Weathering"
 if n.startswith(("Entrance step","Stair stringer")): return "Stairs"
 return "CabinStructure"
groups={}
dg=bpy.context.evaluated_depsgraph_get()
for o in asset.objects:
 if o.type not in {'MESH','FONT'}: continue
 key=group(o);d=groups.setdefault(key,dict(v=[],f=[],mi=[],mats=[]))
 ev=o.evaluated_get(dg);me=ev.to_mesh()
 offset=len(d['v']); origin=door.location if key=="Door" else Vector((0,0,0))
 d['v'].extend([tuple(o.matrix_world@v.co-origin) for v in me.vertices])
 for p in me.polygons:
  mat=me.materials[p.material_index] if len(me.materials)>p.material_index else None
  if mat not in d['mats']:d['mats'].append(mat)
  d['f'].append(tuple(offset+i for i in p.vertices));d['mi'].append(d['mats'].index(mat))
 ev.to_mesh_clear()
for key,d in groups.items():
 me=bpy.data.meshes.new("Export "+key);me.from_pydata(d['v'],[],d['f']);me.update()
 for m in d['mats']:me.materials.append(m)
 for p,idx in zip(me.polygons,d['mi']):p.material_index=idx
 ob=bpy.data.objects.new("BP_"+key,me);export_scene.collection.objects.link(ob)
 if key=="Door":ob.location=door.location
 ob["asset"]="Blackpine Ranger Cabin";ob["body_dimensions_m"]="6 x 8";ob["porch_depth_m"]=2
bpy.context.window.scene=export_scene
for o in export_scene.objects:o.select_set(True)
bpy.context.view_layer.objects.active=next(iter(export_scene.objects))
bpy.ops.export_scene.fbx(filepath=base+"/BlackpineRangerCabin.fbx",use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False)
tris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in export_scene.objects)
report={"body_m":[6,8],"porch_depth_m":2,"porch_posts":len([o for o in asset.objects if o.name.startswith("Porch square post")]),"stairs":len([o for o in asset.objects if o.name.startswith("Entrance step")]),"export_meshes":len(export_scene.objects),"triangles_including_letters":tris,"fbx_bytes":os.path.getsize(base+"/BlackpineRangerCabin.fbx"),"render":"BlackpineCabin_reference.png","note":"Reference asset. Unity integration/colliders/navigation not performed."}
assert report["porch_posts"]==4 and report["stairs"]==3
assert report["fbx_bytes"]>10000
with open(base+"/validation.json","w",encoding="utf-8") as f:json.dump(report,f,indent=2)
bpy.context.window.scene=source
source.render.resolution_percentage=100
source.render.filepath=base+"/BlackpineCabin_reference.png"
bpy.ops.wm.save_as_mainfile(filepath=base+"/BlackpineRangerCabin.blend")
print(json.dumps(report))
bpy.app.timers.register(lambda: (bpy.ops.render.render(write_still=True) and None),first_interval=1)

