import bpy, json, os
from mathutils import Vector
out=r"C:/Users/namphung/FPS/ArtSource/BlackpineRangerCabin/Optimized"
source=bpy.context.scene;asset=bpy.data.collections["CABIN optimized"]
scene=bpy.data.scenes.new("Blackpine PSX - compact export")
groups={}
origin=Vector((-.60,-4.08,1.70))
for ob in asset.objects:
 if ob.type!='MESH':continue
 key="Door" if ob.name.startswith(("DOOR","Door board joint","Iron door strap","Door latch")) else "Cabin"
 d=groups.setdefault(key,dict(v=[],f=[],uv=[],mi=[],m=[]))
 offset=len(d['v'])
 d['v'].extend(tuple(ob.matrix_world@v.co-(origin if key=="Door" else Vector())) for v in ob.data.vertices)
 for p in ob.data.polygons:
  mat=ob.data.materials[p.material_index]
  if mat not in d['m']:d['m'].append(mat)
  d['f'].append(tuple(offset+i for i in p.vertices));d['mi'].append(d['m'].index(mat))
  d['uv'].append([tuple(ob.data.uv_layers.active.data[i].uv) for i in p.loop_indices])
for key,d in groups.items():
 me=bpy.data.meshes.new("PSX "+key);me.from_pydata(d['v'],[],d['f']);me.update()
 for m in d['m']:me.materials.append(m)
 uv=me.uv_layers.new(name="UVMap")
 for p,mi,coords in zip(me.polygons,d['mi'],d['uv']):
  p.material_index=mi
  for li,co in zip(p.loop_indices,coords):uv.data[li].uv=co
 ob=bpy.data.objects.new("Blackpine_"+key,me);scene.collection.objects.link(ob)
 if key=="Door":ob.location=origin
bpy.context.window.scene=scene
for o in scene.objects:o.select_set(True)
bpy.context.view_layer.objects.active=next(iter(scene.objects))
fbx=out+"/BlackpineRangerCabin_PSX.fbx"
bpy.ops.export_scene.fbx(filepath=fbx,use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',path_mode='RELATIVE',add_leaf_bones=False,bake_anim=False)
triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in scene.objects)
polygons=sum(len(o.data.polygons) for o in scene.objects)
assert triangles==1844
check=bpy.data.scenes.new("PSX FBX validation");bpy.context.window.scene=check
bpy.ops.import_scene.fbx(filepath=fbx)
meshes=[o for o in check.objects if o.type=='MESH']
assert len(meshes)==2
assert sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes)==triangles
assert all(o.data.uv_layers.active is not None for o in meshes)
missing=[]
textures=set()
for ob in meshes:
 for mat in ob.data.materials:
  for n in mat.node_tree.nodes:
   if n.type=='TEX_IMAGE' and n.image:
    path=bpy.path.abspath(n.image.filepath);textures.add(path)
    if not os.path.exists(path):missing.append(path)
assert not missing,missing
assert len(textures)==5,textures
report={"before_triangles":5994,"triangles":triangles,"mesh_polygons":polygons,"reduction_percent":round(100*(1-triangles/5994),2),"export_meshes":2,"texture_files":sorted(textures),"verification":"PASS: FBX reimport triangle counts, UVs, 2 meshes and 5 external texture files"}
with open(out+"/validation.json","w") as f:json.dump(report,f,indent=2)
bpy.context.window.scene=source
bpy.ops.wm.save_as_mainfile(filepath=out+"/BlackpineRangerCabin_PSX.blend")
print(json.dumps(report))

