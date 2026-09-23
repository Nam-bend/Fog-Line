import bpy,os,json,math
from mathutils import Vector
OUT=r'C:\Users\namphung\FPS\ArtSource\MotherMoster'
bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT,'MotherMoster_Animated.blend'))
rig=bpy.data.objects['MotherMoster_Rig']; scene=bpy.context.scene
rig.animation_data.action=bpy.data.actions['MotherMoster_Attack']
scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='STUDIO'
scene.display.shading.color_type='TEXTURE'
scene.display.shading.show_shadows=True
scene.display.shading.show_cavity=True
scene.display.shading.background_type='WORLD'
scene.world.color=(.055,.065,.075)
scene.render.resolution_x=800;scene.render.resolution_y=600;scene.render.resolution_percentage=100
camera_data=bpy.data.cameras.new('AttackCamera'); camera=bpy.data.objects.new('AttackCamera',camera_data)
scene.collection.objects.link(camera);scene.camera=camera;camera_data.type='ORTHO';camera_data.ortho_scale=2.25
center=Vector((0,-.20,.45)); camera.location=center+Vector((3,-1,.5));camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.003))
floor=bpy.context.object;floor.color=(.16,.18,.21,1)
data=[]
for frame in [1,10,13,16,20,27,37]:
    scene.frame_set(frame)
    positions={side:{'shoulder':list(rig.pose.bones['arm_tentacle.'+side+'.01'].head),'tip':list(rig.pose.bones['arm_tentacle.'+side+'.08'].tail)} for side in ['L','R']}
    data.append({'frame':frame,'arms':positions})
    scene.render.filepath=os.path.join(OUT,'AnimationPreviews',f'Attack_side_{frame:02}.png')
    bpy.ops.render.render(write_still=True)
with open(os.path.join(OUT,'attack_motion_check.json'),'w') as f:json.dump(data,f,indent=2)
coil=next(d['arms'] for d in data if d['frame']==10)
strike=next(d['arms'] for d in data if d['frame']==16)
for side in ['L','R']:
    assert coil[side]['tip'][1]>coil[side]['shoulder'][1]+.4,side+' did not wind up behind the shoulder'
    assert strike[side]['tip'][1]<strike[side]['shoulder'][1]-.6,side+' did not strike forward'
    assert coil[side]['tip'][1]-strike[side]['tip'][1]>1.3,side+' forward sweep is too small'
print('ATTACK_MOTION',json.dumps(data))
# A flipbook can be played directly in the browser; no extra viewer is needed.
scene.render.resolution_x=640;scene.render.resolution_y=480
for name,length in [('Attack',36),('Idle',96),('Walk',36),('Run',20),('Attack_Sweep',33),('Attack_Combo',48),('Attack_Slam',45)]:
    rig.animation_data.action=bpy.data.actions['MotherMoster_'+name]
    view_direction=Vector((.8,-3,.4) if name=='Run' else (2,-3,.5))
    orientation=(-view_direction).to_track_quat('-Z','Y')
    right=orientation@Vector((1,0,0));up=orientation@Vector((0,1,0));depth=view_direction.normalized()
    mesh=bpy.data.objects['MotherMosterPSX']
    bounds=[]
    for frame in range(1,length+1):
        scene.frame_set(frame)
        ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());em=ev.to_mesh()
        for vertex in em.vertices:
            point=mesh.matrix_world@vertex.co
            bounds.append((point.dot(right),point.dot(up),point.dot(depth)))
        ev.to_mesh_clear()
    lower=[min(v[i] for v in bounds) for i in range(3)];upper=[max(v[i] for v in bounds) for i in range(3)]
    center=sum((axis*((lower[i]+upper[i])/2) for i,axis in enumerate([right,up,depth])),Vector())
    camera.location=center+view_direction.normalized()*4;camera.rotation_euler=orientation.to_euler()
    camera_data.ortho_scale=max(upper[0]-lower[0],(upper[1]-lower[1])*4/3)*1.15
    frames_dir=os.path.join(OUT,'AnimationPreviews',name+'Frames');os.makedirs(frames_dir,exist_ok=True)
    for frame in range(1,length+1):
        scene.frame_set(frame);scene.render.filepath=os.path.join(frames_dir,f'{frame:03}.png');bpy.ops.render.render(write_still=True)
