import bpy, math, json, os
from mathutils import Vector
OUT=r'C:\Users\namphung\FPS\ArtSource\MotherMoster'
bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT,'MotherMoster_Rigged.blend'))
rig=bpy.data.objects['MotherMoster_Rig']
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='STUDIO'
scene.display.shading.color_type='MATERIAL'
scene.display.shading.show_shadows=True
scene.display.shading.show_cavity=True
scene.render.resolution_x=1000
scene.render.resolution_y=1000
scene.render.resolution_percentage=100
data=bpy.data.cameras.new('ValidationCamera')
camera=bpy.data.objects.new('ValidationCamera',data)
scene.collection.objects.link(camera)
scene.camera=camera
data.type='ORTHO'; data.ortho_scale=1.3
target=Vector((.005,.078,.488))
camera.location=target+Vector((0,-3,0))
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=os.path.join(OUT,'rest_preview.png')
bpy.ops.render.render(write_still=True)
for name,angles in {'chest':(0,0,5),'head':(0,10,0),'thigh.L':(15,0,-8),'shin.L':(-20,0,0),'arm_tentacle.L.03':(0,0,-18),'arm_tentacle.R.03':(0,0,18),'dorsal_upper.L.03':(15,0,12),'dorsal_lower.R.03':(10,0,-15)}.items():
    pb=rig.pose.bones[name]; pb.rotation_mode='XYZ'; pb.rotation_euler=[math.radians(a) for a in angles]
bpy.context.view_layer.update()
dg=bpy.context.evaluated_depsgraph_get()
evaluated=mesh.evaluated_get(dg)
em=evaluated.to_mesh()
assert all(math.isfinite(c) for v in em.vertices for c in v.co)
ratios=[]
for edge in mesh.data.edges:
    a,b=edge.vertices
    before=(mesh.data.vertices[a].co-mesh.data.vertices[b].co).length
    after=(em.vertices[a].co-em.vertices[b].co).length
    if before>1e-6: ratios.append(after/before)
evaluated.to_mesh_clear()
print('POSE_CHECK',json.dumps({'max_edge_stretch':max(ratios),'edges_over_2x':sum(r>2 for r in ratios),'edge_count':len(ratios)}))
scene.render.filepath=os.path.join(OUT,'pose_preview.png')
bpy.ops.render.render(write_still=True)
# Check the actual exported file can be imported with its skeleton and skin weights.
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(OUT,'MotherMoster_Rigged.fbx'))
arms=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(arms)==1 and len(arms[0].data.bones)==54
assert meshes and all(any(m.type=='ARMATURE' for m in o.modifiers) for o in meshes)
assert all(any(g.weight>0 for g in v.groups) for o in meshes for v in o.data.vertices)
print('FBX_ROUNDTRIP_OK')
