import bpy, json
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=r'C:\Blender\MotherMoster.blend')
for o in bpy.data.objects:
    print('OBJECT', json.dumps({'name':o.name,'type':o.type,'location':list(o.location),'rotation':list(o.rotation_euler),'scale':list(o.scale),'dimensions':list(o.dimensions),'vertices':len(o.data.vertices) if o.type=='MESH' else None,'modifiers':[(m.name,m.type) for m in o.modifiers],'groups':[g.name for g in o.vertex_groups] if o.type=='MESH' else [],'bones':[b.name for b in o.data.bones] if o.type=='ARMATURE' else []}))
meshes=[o for o in bpy.data.objects if o.type=='MESH']
coords=[o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
lo=Vector(tuple(min(v[i] for v in coords) for i in range(3)))
hi=Vector(tuple(max(v[i] for v in coords) for i in range(3)))
center=(lo+hi)/2
size=max(hi-lo)
print('BOUNDS',list(lo),list(hi))
scene=bpy.context.scene
scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='STUDIO'
scene.display.shading.color_type='MATERIAL'
scene.display.shading.show_shadows=True
scene.display.shading.show_cavity=True
scene.render.resolution_x=900
scene.render.resolution_y=900
scene.render.resolution_percentage=100
camera_data=bpy.data.cameras.new('InspectionCamera')
camera=bpy.data.objects.new('InspectionCamera',camera_data)
scene.collection.objects.link(camera)
scene.camera=camera
camera_data.type='ORTHO'
camera_data.ortho_scale=size*1.25
for name,direction in [('front',(0,-1,0)),('side',(1,0,0)),('back',(0,1,0))]:
    camera.location=center+Vector(direction)*size*3
    camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=r'C:\Users\namphung\FPS\ArtSource\mother_'+name+'.png'
    bpy.ops.render.render(write_still=True)
