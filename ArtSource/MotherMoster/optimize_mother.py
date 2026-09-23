import bpy, os, json, math
from mathutils import Vector
OUT=r'C:\Users\namphung\FPS\ArtSource\MotherMoster'
ASSET=r'C:\Users\namphung\FPS\Assets\_Project\Art\Enemies\MotherMosterPSX'
os.makedirs(ASSET,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT,'MotherMoster_Rigged.blend'))
if bpy.context.object and bpy.context.object.mode!='OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
snapshot={b.name: {'head':list(b.head_local),'tail':list(b.tail_local),'matrix':[list(row) for row in b.matrix_local],'parent':b.parent.name if b.parent else None} for b in rig.data.bones}
mesh.data.calc_loop_triangles()
original_tris=len(mesh.data.loop_triangles)
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True); bpy.context.view_layer.objects.active=mesh
rig.data.pose_position='REST'
dec=mesh.modifiers.new('PSX polygon reduction','DECIMATE')
dec.ratio=1200/original_tris
dec.use_collapse_triangulate=True
bpy.ops.object.modifier_move_up(modifier=dec.name)
bpy.ops.object.modifier_apply(modifier=dec.name)
mesh.name='MotherMosterPSX'; mesh.data.name='MotherMosterPSX_Mesh'
for face in mesh.data.polygons: face.use_smooth=False
# Rebind to the user's edited skeleton, including newly added bones.
mesh.vertex_groups.clear()
for mod in list(mesh.modifiers):
    if mod.type=='ARMATURE': mesh.modifiers.remove(mod)
rig.select_set(True); bpy.context.view_layer.objects.active=rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
deform={b.name for b in rig.data.bones if b.use_deform}
for b in rig.data.bones:
    if b.use_deform and not mesh.vertex_groups.get(b.name): mesh.vertex_groups.new(name=b.name)
missing=[]
for v in mesh.data.vertices:
    if not any(mesh.vertex_groups[g.group].name in deform and g.weight>1e-7 for g in v.groups):
        missing.append(v.index)
        p=rig.matrix_world.inverted()@mesh.matrix_world@v.co
        def dist(b):
            d=b.tail_local-b.head_local
            return (p-b.head_local-d*max(0,min(1,(p-b.head_local).dot(d)/d.length_squared))).length
        nearest=min((b for b in rig.data.bones if b.use_deform),key=dist)
        mesh.vertex_groups[nearest.name].add([v.index],1,'REPLACE')
bpy.context.view_layer.objects.active=mesh
rig.select_set(False)
bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
bpy.ops.object.vertex_group_limit_total(limit=4)
bpy.ops.object.vertex_group_normalize_all(lock_active=False)
bpy.ops.object.mode_set(mode='OBJECT')
for mod in mesh.modifiers:
    if mod.type=='ARMATURE': mod.use_deform_preserve_volume=False
# Keep original UV placement and reduce the source albedo to the reference resolution.
texture=bpy.data.images['Color.jpg'].copy()
texture.name='MotherMosterPSX_Diffuse'
texture.scale(256,256)
texture.filepath_raw=os.path.join(ASSET,'MotherMosterPSX_Diffuse.png')
texture.file_format='PNG'
texture.save()
mat=bpy.data.materials.new('M_MotherMosterPSX'); mat.use_nodes=True
nodes=mat.node_tree.nodes
bsdf=nodes.get('Principled BSDF')
bsdf.inputs['Metallic'].default_value=0
bsdf.inputs['Roughness'].default_value=.7
tex=nodes.new('ShaderNodeTexImage'); tex.image=texture; tex.interpolation='Closest'
mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
mesh.data.materials.clear(); mesh.data.materials.append(mat)
for old in list(bpy.data.materials):
    if old!=mat and old.users==0: bpy.data.materials.remove(old)
for old in list(bpy.data.images):
    if old!=texture and old.users==0: bpy.data.images.remove(old)
texture.pack()
rig.data.pose_position='POSE'
mesh.data.calc_loop_triangles()
assert snapshot=={b.name: {'head':list(b.head_local),'tail':list(b.tail_local),'matrix':[list(row) for row in b.matrix_local],'parent':b.parent.name if b.parent else None} for b in rig.data.bones}
assert all(any(g.weight>1e-7 and mesh.vertex_groups[g.group].name in deform for g in v.groups) for v in mesh.data.vertices)
report={'source_triangles':original_tris,'triangles':len(mesh.data.loop_triangles),'vertices':len(mesh.data.vertices),'bones':len(rig.data.bones),'bone_positions_preserved':True,'texture_size':list(texture.size),'fallback_weight_vertices':len(missing),'max_influences':max(sum(g.weight>0 for g in v.groups) for v in mesh.data.vertices)}
rig['Rig_notes']='User-edited FK skeleton preserved. PSX mesh with 256px point-filtered albedo, weights rebound to edited bones, maximum four influences.'
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D': area.spaces.active.shading.type='MATERIAL'
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'MotherMoster_PSX.blend'))
mesh.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(ASSET,'MotherMosterPSX.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
# Texture previews use nearest sampling in the actual material.
scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.samples=16
scene.world.color=(.2,.2,.2)
scene.render.resolution_x=900; scene.render.resolution_y=900; scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'
data=bpy.data.cameras.new('PreviewCamera'); camera=bpy.data.objects.new('PreviewCamera',data)
scene.collection.objects.link(camera); scene.camera=camera
data.type='ORTHO'; data.ortho_scale=1.22
center=Vector((.005,.078,.488))
for name,position,power,size in [('Key',(-1,-2,3),170,2),('Fill',(2,-1,1),70,2),('Rim',(0,2,2),110,1.5)]:
    ld=bpy.data.lights.new(name,'AREA'); ld.energy=power; ld.shape='DISK'; ld.size=size
    ob=bpy.data.objects.new(name,ld); scene.collection.objects.link(ob); ob.location=position
    ob.rotation_euler=(center-ob.location).to_track_quat('-Z','Y').to_euler()
for name,direction in [('front',(0,-3,0)),('back',(0,3,0))]:
    camera.location=center+Vector(direction); camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=os.path.join(OUT,'psx_'+name+'.png'); bpy.ops.render.render(write_still=True)
for name,angles in {'chest':(0,0,5),'head':(0,10,0),'thigh.L':(15,0,-8),'shin.L':(-20,0,0),'arm_tentacle.L.03':(0,0,-18),'arm_tentacle.R.03':(0,0,18),'dorsal_upper.L.03':(15,0,12),'dorsal_lower.R.03':(10,0,-15)}.items():
    pb=rig.pose.bones.get(name)
    if pb: pb.rotation_mode='XYZ'; pb.rotation_euler=[math.radians(a) for a in angles]
bpy.context.view_layer.update()
evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get()); em=evaluated.to_mesh()
assert all(math.isfinite(c) for v in em.vertices for c in v.co)
ratios=[]
for e in mesh.data.edges:
    a,b=e.vertices; length=(mesh.data.vertices[a].co-mesh.data.vertices[b].co).length
    if length>1e-6: ratios.append((em.vertices[a].co-em.vertices[b].co).length/length)
report['test_pose_max_edge_stretch']=max(ratios); report['test_pose_edges_over_2x']=sum(r>2 for r in ratios)
evaluated.to_mesh_clear()
camera.location=center+Vector((0,-3,0)); camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=os.path.join(OUT,'psx_pose.png'); bpy.ops.render.render(write_still=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(ASSET,'MotherMosterPSX.fbx'))
arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
model=next(o for o in bpy.context.scene.objects if o.type=='MESH')
model.data.calc_loop_triangles()
assert set(arm.data.bones.keys())==set(snapshot)
assert len(model.data.loop_triangles)==report['triangles']
assert all(any(g.weight>0 for g in v.groups) for v in model.data.vertices)
report['fbx_roundtrip_passed']=True
with open(os.path.join(OUT,'psx_report.json'),'w') as f: json.dump(report,f,indent=2)
print('PSX_REPORT',json.dumps(report))
