import bpy, math, os, json
from mathutils import Vector, Quaternion, Euler, Matrix

OUT=r'C:\Users\namphung\FPS\ArtSource\MotherMoster'
ASSET=r'C:\Users\namphung\FPS\Assets\_Project\Art\Enemies\MotherMosterPSX'
bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT,'MotherMoster_PSX.blend'))
if bpy.context.object and bpy.context.object.mode!='OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
scene=bpy.context.scene
scene.render.fps=30
rig.animation_data_clear()
rig.animation_data_create()
rest={b.name:b.matrix_local.copy() for b in rig.data.bones}
snapshot={b.name:[list(b.head_local),list(b.tail_local)] for b in rig.data.bones}
for pb in rig.pose.bones: pb.rotation_mode='QUATERNION'

def rotate(name,x=0,y=0,z=0):
    pb=rig.pose.bones.get(name)
    if pb:
        q=rest[name].to_quaternion()
        pb.rotation_quaternion=q.inverted()@Euler(tuple(math.radians(a) for a in (x,y,z)),'XYZ').to_quaternion()@q

def move(name,offset):
    pb=rig.pose.bones[name]
    pb.location=rest[name].to_3x3().inverted()@Vector(offset)

def smooth(t):
    t=max(0,min(1,t)); return t*t*(3-2*t)

def envelope(t,keys):
    for (a,v),(b,w) in zip(keys,keys[1:]):
        if t<=b: return v+(w-v)*smooth((t-a)/(b-a))
    return keys[-1][1]

def reset():
    for pb in rig.pose.bones:
        pb.location=(0,0,0); pb.rotation_quaternion=(1,0,0,0); pb.scale=(1,1,1)

def solve_leg(side,ankle,foot_pitch=0):
    thigh=rig.pose.bones['thigh.'+side]; shin=rig.pose.bones['shin.'+side]; foot=rig.pose.bones['foot.'+side]
    bpy.context.view_layer.update()
    hip=thigh.head.copy()
    a=thigh.bone.length; b=shin.bone.length
    direction=ankle-hip; dist=min(direction.length,a+b-.0001); direction.normalize()
    forward=Vector((.22 if side=='L' else -.22,-1,0)); forward=(forward-direction*direction.dot(forward)).normalized()
    along=(a*a-b*b+dist*dist)/(2*dist)
    knee=hip+direction*along+forward*math.sqrt(max(0,a*a-along*along))
    for pb,start,end in [(thigh,hip,knee),(shin,knee,ankle)]:
        rb=pb.bone; delta=(rb.tail_local-rb.head_local).rotation_difference(end-start)
        pb.matrix=Matrix.LocRotScale(start,delta@rest[pb.name].to_quaternion(),Vector((1,1,1)))
        bpy.context.view_layer.update()
    foot.matrix=Matrix.LocRotScale(ankle,Quaternion((1,0,0),math.radians(foot_pitch))@rest[foot.name].to_quaternion(),Vector((1,1,1)))

def tentacles(phase,strength=1,attack=0,death=0):
    for side,offset in [('L',0),('R',math.pi)]:
        for prefix in ['arm_tentacle','dorsal_upper','dorsal_lower']:
            bones=[b for b in rig.pose.bones if b.name.startswith(prefix+'.'+side+'.')]
            for index,pb in enumerate(bones):
                lag=index*.58+offset+(1.4 if prefix.startswith('dorsal') else 0)
                wave=math.sin(phase-lag)
                amp=(2.0 if prefix=='arm_tentacle' else 3.5)*strength
                rx=amp*wave
                ry=amp*.45*math.cos(phase-lag)
                rz=amp*.65*math.sin(phase-lag+.7)
                if prefix=='arm_tentacle':
                    rx+=attack*(25 if index<2 else 7)
                    rz+=attack*(1 if side=='L' else -1)*(13 if index<2 else -4)
                elif prefix=='dorsal_upper': rx+=attack*8
                rotate(pb.name,rx*(1-death),ry*(1-death),rz*(1-death))

def aim_segment(pb,direction,min_z=None):
    bpy.context.view_layer.update()
    direction=Vector(direction).normalized()
    if min_z is not None and pb.head.z+direction.z*pb.bone.length<min_z:
        z=max(-.99,min(.99,(min_z-pb.head.z)/pb.bone.length))
        horizontal=Vector((direction.x,direction.y,0)).normalized()*math.sqrt(1-z*z)
        direction=Vector((horizontal.x,horizontal.y,z))
    current=pb.tail-pb.head
    q=current.rotation_difference(direction)
    pb.matrix=Matrix.LocRotScale(pb.head.copy(),q@pb.matrix.to_quaternion(),Vector((1,1,1)))

def monster_arms(phase,strength=1,stride=0,attack_time=None,recoil=0,charging=False):
    # Sagittal directions are authored in armature space: -Y is forward.
    # Negative shoulder rotation on a hanging limb swings it forward, not backward.
    ready=[37,22,6,12,38,76,115,148]
    if charging: ready=[67,48,30,40,65,95,125,150]
    for side,sign in [('L',1),('R',-1)]:
        bones=[b for b in rig.pose.bones if b.name.startswith('arm_tentacle.'+side+'.')]
        for i,pb in enumerate(bones):
            phase_offset=(.35 if side=='L' else 2.0)-i*.52
            angle=ready[min(i,7)]+strength*(4*math.sin(phase+phase_offset)+2*math.sin(2*phase+phase_offset))
            angle+=stride*math.sin(phase+(0 if side=='L' else math.pi)-i*.23)*math.exp(-i*.16)
            angle-=recoil*(18+2*i)
            lateral=[.65,.38,.20,.12,.06,-.08,-.18,-.25][min(i,7)]
            if attack_time is not None:
                delay=.011*i
                coil=envelope(attack_time-delay,[(0,0),(.10,.25),(.24,1),(.29,1),(.37,0),(1,0)])
                strike=envelope(attack_time-delay,[(0,0),(.27,0),(.335,1),(.385,1),(.50,.67),(.62,.30),(.85,0),(1,0)])
                settle=envelope(attack_time-delay,[(0,0),(.41,0),(.57,1),(.72,0),(1,0)])
                # Coil behind both shoulders, then unfurl a delayed wave to both tips.
                coil_angle=[-52,-78,-100,-111,-116,-112,-102,-85][min(i,7)]
                strike_angle=[89,99,101,102,105,112,121,135][min(i,7)]
                angle=angle*(1-max(coil,strike))+coil_angle*coil+strike_angle*strike
                angle-=settle*(14+2*i)
                lateral=lateral*(1-.65*strike)+.20*coil
            theta=math.radians(angle)
            aim_segment(pb,(sign*lateral,-math.sin(theta),-math.cos(theta)),min_z=.065)

def variant_arms(kind,t):
    ready=[37,22,6,12,38,76,115,148]
    for side,sign in [('L',1),('R',-1)]:
        bones=[b for b in rig.pose.bones if b.name.startswith('arm_tentacle.'+side+'.')]
        for i,pb in enumerate(bones):
            u=t-.008*i
            base=math.radians(ready[min(i,7)])
            direction=Vector((sign*[.65,.38,.20,.12,.06,-.08,-.18,-.25][min(i,7)],-math.sin(base),-math.cos(base))).normalized()
            if kind=='Attack_Sweep':
                engage=envelope(u,[(0,0),(.20,1),(.69,1),(.97,0),(1,0)])
                yaw=envelope(u,[(0,65),(.24,108),(.31,108),(.47,-58),(.63,-78),(.83,-30),(1,0)])
                theta=math.radians(83+4*math.sin(i*.7)+(5 if side=='R' else 0))
                phi=math.radians(yaw+sign*(12-i))
            elif kind=='Attack_Combo':
                offset=0 if side=='L' else .27
                u-=offset
                engage=envelope(u,[(0,0),(.09,1),(.36,1),(.58,0),(1,0)])
                theta=math.radians(envelope(u,[(0,40),(.12,143),(.17,143),(.26,77),(.34,54),(.55,37),(1,37)]))+i*.04
                phi=math.radians(sign*envelope(u,[(0,20),(.14,67),(.17,67),(.27,-30),(.39,-42),(.58,10),(1,10)]))
            else:
                engage=envelope(u,[(0,0),(.23,1),(.72,1),(.98,0),(1,0)])
                theta=math.radians(envelope(u,[(0,37),(.24,164),(.35,166),(.43,88),(.52,38),(.65,30),(.84,48),(1,37)]))+i*.025
                phi=math.radians(sign*(12-i*.8))
            attack=Vector((math.sin(theta)*math.sin(phi),-math.sin(theta)*math.cos(phi),-math.cos(theta)))
            aim_segment(pb,direction.lerp(attack,engage),min_z=.065)

def charging_dorsal(phase):
    for pb in rig.pose.bones:
        if pb.name.startswith('dorsal_'):
            bpy.context.view_layer.update()
            index=int(pb.name.split('.')[-1])
            sign=1 if '.L.' in pb.name else -1
            wave=math.sin(phase-index*.7+(0 if sign==1 else 1.8))
            target=Vector((sign*(.22+.12*wave),1,.20+.28*math.sin(phase-index*.8)))
            current=(pb.tail-pb.head).normalized()
            aim_segment(pb,current.lerp(target.normalized(),.62),min_z=.10)

def hunting_posture(phase=0,crouch=.058,lean=1):
    breath=.7*math.sin(phase)+.3*math.sin(2*phase)
    move('pelvis',(.005*math.sin(phase),.016,-crouch+.004*breath))
    rotate('pelvis',7*lean,0,1.5*math.sin(phase))
    rotate('spine',15*lean+1.6*breath,2*math.sin(phase),-2*math.sin(phase))
    rotate('chest',11*lean-1.2*breath,-2*math.sin(phase),1.2*math.sin(phase))
    rotate('neck',-9*lean,0,0)
    rotate('head',-14*lean,3*math.sin(phase+.3),0)

clips=[('Idle',96,True),('Walk',36,True),('Run',20,True),('Attack',36,False),('Stagger',24,False),('Death',72,False),('Attack_Sweep',33,False),('Attack_Combo',48,False),('Attack_Slam',45,False)]
report={'fps':30,'clips':[],'bones_preserved':True}
actions={}
for name,length,loop in clips:
    action=bpy.data.actions.new('MotherMoster_'+name)
    rig.animation_data.action=action
    action.use_fake_user=True
    actions[name]=action
    samples=[]
    for frame in range(1,length+2):
        scene.frame_set(frame); reset()
        t=(frame-1)/length; p=2*math.pi*t
        if name=='Idle':
            hunting_posture(p)
            scan=envelope(t,[(0,0),(.14,-9),(.35,-9),(.46,6),(.67,6),(.86,0),(1,0)])
            rotate('head',-14+2*math.sin(p),3*math.sin(p+.3),scan)
            tentacles(p,1.4)
            monster_arms(p,.9)
            for side in ['L','R']: solve_leg(side,rig.data.bones['foot.'+side].head_local.copy())
        elif name in ['Walk','Run']:
            running=name=='Run'
            stride=.32 if running else .15
            lift=.105 if running else .035
            impact=(.5+.5*math.cos(2*p))**3
            hunting_posture(p,.074 if running else .058,1.35 if running else 1)
            move('pelvis',((.018 if running else .009)*math.sin(p),-.025 if running else .01,-(.080 if running else .050)-(.032 if running else .013)*impact))
            rotate('pelvis',14 if running else 7,3*math.sin(p),5*math.sin(p))
            rotate('spine',(28 if running else 15)+(5 if running else 3)*math.sin(2*p-.5),-4*math.sin(p),-9*math.sin(p))
            rotate('chest',15 if running else 11,2*math.sin(p),9*math.sin(p-.35))
            rotate('neck',-14 if running else -9,0,0)
            rotate('head',-34 if running else -14,2*math.sin(p+.7),-3*math.sin(p-.4))
            tentacles(p,3 if running else 1.7)
            monster_arms(p,1.6 if running else 1.3,30 if running else 9,charging=running)
            if running: charging_dorsal(p)
            for side,offset in [('L',0),('R',.5)]:
                u=(t+offset)%1
                ankle=rig.data.bones['foot.'+side].head_local.copy()
                stance=.43 if running else .65
                if running: ankle.x*=.86
                if u<stance:
                    ankle.y+=-stride/2+stride*u/stance
                    pitch=0
                else:
                    swing=(u-stance)/(1-stance)
                    ankle.y+=stride/2-stride*smooth(swing)
                    ankle.z+=lift*math.sin(math.pi*swing)
                    ankle.x+=(.018 if side=='L' else -.018)*math.sin(math.pi*swing)
                    pitch=8*math.sin(2*math.pi*swing)
                solve_leg(side,ankle,pitch)
        elif name in ['Attack_Sweep','Attack_Combo','Attack_Slam']:
            hunting_posture(p)
            if name=='Attack_Sweep':
                twist=envelope(t,[(0,0),(.23,30),(.30,30),(.46,-32),(.64,-38),(.96,0),(1,0)])
                hit=envelope(t,[(0,0),(.30,0),(.44,1),(.65,.5),(.96,0),(1,0)])
                move('pelvis',(.028*math.sin(math.radians(twist)),.016-.045*hit,-.058-.025*hit))
                rotate('pelvis',7+5*hit,0,twist*.22)
                rotate('spine',15+8*hit,0,twist*.42)
                rotate('chest',11+8*hit,0,twist*.55)
                rotate('head',-14-8*hit,0,-twist*.3)
            elif name=='Attack_Combo':
                left=envelope(t,[(0,0),(.14,-.55),(.25,1),(.35,.7),(.45,0),(1,0)])
                right=envelope(t,[(0,0),(.41,-.55),(.52,1),(.62,.7),(.80,0),(1,0)])
                hit=max(0,left,right)
                move('pelvis',(.025*(right-left),.016-.06*hit,-.058-.018*hit))
                rotate('pelvis',7+6*hit,0,5*(right-left))
                rotate('spine',15+10*hit,0,12*(right-left))
                rotate('chest',11+10*hit,0,17*(right-left))
                rotate('head',-14-12*hit,0,-8*(right-left))
            else:
                lift=envelope(t,[(0,0),(.25,1),(.35,1),(.46,0),(1,0)])
                hit=envelope(t,[(0,0),(.35,0),(.49,1),(.60,1),(.95,0),(1,0)])
                move('pelvis',(0,.016+.012*lift-.09*hit,-.058+.025*lift-.055*hit))
                rotate('pelvis',7-4*lift+11*hit,0,0)
                rotate('spine',15-10*lift+18*hit,0,0)
                rotate('chest',11-9*lift+18*hit,0,0)
                rotate('neck',-9-8*hit,0,0)
                rotate('head',-14-8*hit,0,0)
            tentacles(p,2,attack=hit)
            variant_arms(name,t)
            for side in ['L','R']: solve_leg(side,rig.data.bones['foot.'+side].head_local.copy())
        elif name=='Attack':
            coil=envelope(t,[(0,0),(.24,1),(.29,1),(.37,0),(1,0)])
            hit=envelope(t,[(0,0),(.27,0),(.34,1),(.43,1),(.58,.4),(.85,0),(1,0)])
            hunting_posture(p)
            move('pelvis',(0,.016+.035*coil-.085*hit,-.058-.032*coil-.012*hit))
            rotate('pelvis',7+8*hit-5*coil,0,0)
            rotate('spine',15-12*coil+14*hit,0,0)
            rotate('chest',11-10*coil+13*hit,0,0)
            rotate('neck',-9-8*hit,0,0)
            rotate('head',-14-10*hit,0,0)
            tentacles(p,1.6,attack=hit)
            monster_arms(p,.35,attack_time=t)
            for side in ['L','R']: solve_leg(side,rig.data.bones['foot.'+side].head_local.copy())
        elif name=='Stagger':
            recoil=envelope(t,[(0,0),(.16,1),(.35,.8),(.7,.2),(1,0)])
            hunting_posture(p)
            move('pelvis',(0,.016+.025*recoil,-.058-.035*recoil))
            rotate('spine',15-22*recoil,0,-8*recoil)
            rotate('chest',11-18*recoil,0,14*recoil)
            rotate('head',-14+23*recoil,0,-12*recoil)
            tentacles(p,1.8,attack=-.4*recoil)
            monster_arms(p,1.2,recoil=recoil)
            for side in ['L','R']: solve_leg(side,rig.data.bones['foot.'+side].head_local.copy())
        else:
            fall=smooth((t-.12)/.56)
            recoil=envelope(t,[(0,0),(.12,1),(.4,.4),(.7,0),(1,0)])
            move('pelvis',(0,.016*(1-fall)+.12*fall,-.058*(1-fall)-.33*fall))
            rotate('pelvis',7*(1-fall)-90*fall,0,3*fall)
            rotate('spine',15*(1-fall)-12*recoil+7*fall,0,0)
            rotate('chest',11*(1-fall)-8*recoil,0,0)
            rotate('neck',-9*(1-fall),0,0)
            rotate('head',-14*(1-fall)+10*fall,0,9*fall)
            for side in ['L','R']:
                rotate('thigh.'+side,35*math.sin(math.pi*fall)+3*fall,0,0)
                rotate('shin.'+side,-65*math.sin(math.pi*fall)-6*fall,0,0)
            tentacles(p,1-t,death=fall)
            monster_arms(p,.9*(1-fall))
            # Let appendages settle along the ground instead of propping up the corpse.
            for pb in rig.pose.bones:
                if pb.name.startswith(('arm_tentacle.','dorsal_')):
                    bpy.context.view_layer.update()
                    direction=pb.tail-pb.head
                    flat=Vector((direction.x,direction.y,0))
                    if flat.length<.0001: flat=Vector((1 if '.L.' in pb.name else -1,0,0))
                    target=direction.lerp(flat.normalized()*direction.length,fall)
                    q=direction.rotation_difference(target)
                    pb.matrix=Matrix.LocRotScale(pb.head.copy(),q@pb.matrix.to_quaternion(),Vector((1,1,1)))
            bpy.context.view_layer.update()
            ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get()); em=ev.to_mesh()
            lowest=min((mesh.matrix_world@v.co).z for v in em.vertices); ev.to_mesh_clear()
            if lowest<.004:
                pb=rig.pose.bones['pelvis']; pb.location+=rest['pelvis'].to_3x3().inverted()@Vector((0,0,.004-lowest))
        bpy.context.view_layer.update()
        for pb in rig.pose.bones:
            pb.keyframe_insert('location',frame=frame,group=pb.name)
            pb.keyframe_insert('rotation_quaternion',frame=frame,group=pb.name)
            pb.keyframe_insert('scale',frame=frame,group=pb.name)
        ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get()); em=ev.to_mesh()
        vertices=[list(mesh.matrix_world@v.co) for v in em.vertices]
        assert all(math.isfinite(c) for v in vertices for c in v)
        samples.append(vertices); ev.to_mesh_clear()
    # Linear samples retain the authored motion without spline overshoot.
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    for k in fc.keyframe_points: k.interpolation='LINEAR'
    seam=max((Vector(a)-Vector(b)).length for a,b in zip(samples[0],samples[-1]))
    if loop: assert seam<1e-5,(name,seam)
    report['clips'].append({'name':name,'frames':[1,length+1],'duration':length/30,'loop':loop,'loop_seam_error':seam if loop else None,'min_ground_z':min(v[2] for sample in samples for v in sample)})
    print('AUTHORED',name,flush=True)
assert snapshot=={b.name:[list(b.head_local),list(b.tail_local)] for b in rig.data.bones}
rig.animation_data.action=actions['Attack']; scene.frame_start=1; scene.frame_end=37; scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); bpy.context.view_layer.objects.active=rig
rig['Animation_notes']='30 fps. Four attacks: Attack (double whip), Attack_Sweep, Attack_Combo (two hits), Attack_Slam. Run: 0.667 s aggressive in-place charge. Actions selectable in Dope Sheet > Action Editor.'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'MotherMoster_Animated.blend'))
mesh.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(ASSET,'MotherMosterPSX.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,bake_anim_step=1,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
with open(os.path.join(OUT,'animation_report.json'),'w') as f: json.dump(report,f,indent=2)
# Render representative poses for inspection, without saving cameras into the asset.
scene.render.engine='CYCLES'; scene.cycles.samples=12
scene.render.resolution_x=640; scene.render.resolution_y=640; scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'; scene.world.color=(.18,.18,.18)
camdata=bpy.data.cameras.new('Preview'); cam=bpy.data.objects.new('Preview',camdata); scene.collection.objects.link(cam); scene.camera=cam
center=Vector((.0,.07,.47)); camdata.type='ORTHO'; camdata.ortho_scale=1.36
cam.location=center+Vector((1,-3,.4)); cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
for name,pos,energy in [('Key',(-1,-2,3),160),('Fill',(2,-1,1),90),('Rim',(0,2,2),100)]:
    ld=bpy.data.lights.new(name,'AREA'); ld.energy=energy; ld.size=2
    ob=bpy.data.objects.new(name,ld); scene.collection.objects.link(ob); ob.location=pos; ob.rotation_euler=(center-ob.location).to_track_quat('-Z','Y').to_euler()
os.makedirs(os.path.join(OUT,'AnimationPreviews'),exist_ok=True)
for name,frame in [('Idle',25),('Walk',10),('Run',6),('Attack',17),('Stagger',6),('Death',73),('Attack_Sweep',16),('Attack_Combo',26),('Attack_Slam',23)]:
    rig.animation_data.action=actions[name]; scene.frame_set(frame)
    scene.render.filepath=os.path.join(OUT,'AnimationPreviews',name+'.png'); bpy.ops.render.render(write_still=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(ASSET,'MotherMosterPSX.fbx'))
imported=list(bpy.data.actions)
assert len(imported)==len(clips),[(a.name,list(a.frame_range)) for a in imported]
report['fbx_clips']=[{'name':a.name,'frames':list(a.frame_range)} for a in imported]
report['fbx_roundtrip_passed']=True
with open(os.path.join(OUT,'animation_report.json'),'w') as f: json.dump(report,f,indent=2)
print('ANIMATION_REPORT',json.dumps(report))
