"""Rebuild actions on the actual artist file, preserving a pre-edit backup."""
from pathlib import Path
import shutil

folder = Path(__file__).resolve().parent
source = folder / 'MotherMoster_Animated.blend'
backup = folder / 'MotherMoster_BeforeBoss.blend'
if not backup.exists():
    shutil.copy2(source, backup)
revision_backup = folder / 'MotherMoster_BeforePostureFix.blend'
if not revision_backup.exists():
    shutil.copy2(source, revision_backup)

code = (folder / 'animate_mother.py').read_text(encoding='utf-8')
code = code.replace("'MotherMoster_PSX.blend'", "'MotherMoster_Animated.blend'")
code = code.replace('rig.animation_data_create()', '''rig.animation_data_create()
for old_action in list(bpy.data.actions):
    if old_action.name.startswith('MotherMoster_'):
        bpy.data.actions.remove(old_action)''')
code = code.replace("('Run',20,True)", "('Run',18,True)")
code = code.replace('if charging: ready=[67,48,30,40,65,95,125,150]',
                    'if charging: ready=[22,10,2,16,40,72,112,145]')
code = code.replace('crouch=.058,lean=1', 'crouch=.025,lean=.35')
code = code.replace('monster_arms(p,.9)', 'monster_arms(p,.45)')
code = code.replace('tentacles(p,1.4)', 'tentacles(p,.9)')
code = code.replace("phase_offset=(.35 if side=='L' else 2.0)-i*.52",
                    "phase_offset=(.35 if side=='L' else 1.05)-i*.72")
code = code.replace('angle+=stride*math.sin(phase+(0 if side==\'L\' else math.pi)-i*.23)*math.exp(-i*.16)',
                    '''angle+=stride*math.sin(phase+(0 if side=='L' else .65)-i*.55)*math.exp(-i*.10)
            # Distal segments follow the shoulder with a travelling bend.
            flexibility=(i/7)**1.25
            side_gain=1.0 if side=='L' else .82
            side_lag=.0 if side=='L' else .19
            angle+=strength*side_gain*flexibility*(13*math.sin(phase-i*.72+phase_offset*.15-side_lag)+5*math.sin(2*phase-i*.9))
            angle+=side_gain*(2.8*math.sin(phase*1.5+side_lag)+1.2*math.sin(i*.8+phase))''')
# Force loop endpoint keys to match frame one after adding asymmetric motion.
code = code.replace("    seam=max((Vector(a)-Vector(b)).length for a,b in zip(samples[0],samples[-1]))",
'''    if loop:
        scene.frame_set(1)
        for pb in rig.pose.bones:
            pb.keyframe_insert('location',frame=length+1,group=pb.name)
            pb.keyframe_insert('rotation_quaternion',frame=length+1,group=pb.name)
            pb.keyframe_insert('scale',frame=length+1,group=pb.name)
        samples[-1]=samples[0]
    seam=max((Vector(a)-Vector(b)).length for a,b in zip(samples[0],samples[-1]))''')
start = code.index("            stride=.32 if running else .15")
end = code.index("        elif name in ['Attack_Sweep'", start)
code = code[:start] + '''            stride=.38 if running else .18
            lift=.10 if running else .038
            # A grounded charge: stable gaze, broad stance, short loaded contact,
            # rapid foot recovery and delayed appendages instead of a human jog.
            contact=.58 if running else .70
            step=(2*t)%1
            load=envelope(step,[(0,.45),(.13,1),(.40,.65),(.72,0),(1,.45)])
            hunting_posture(p,.055 if running else .030,.45)
            move('pelvis',((.009 if running else .012)*math.cos(p),
                           -.025 if running else .01,
                           -(.055 if running else .030)-(.018 if running else .009)*load))
            rotate('pelvis',7 if running else 2,1.6*math.cos(p),3*math.sin(p))
            rotate('spine',(8 if running else 3)+2*load,-1.5*math.cos(p),-4*math.sin(p))
            rotate('chest',(2 if running else 1)+math.sin(2*p-.55),0,3*math.sin(p-.25))
            rotate('neck',-5 if running else -2,0,0)
            rotate('head',-9-2*load if running else -3,0,-2*math.sin(p-.25))
            tentacles(p,1.8 if running else 1.1)
            monster_arms(p,1.25 if running else .85,11 if running else 5,charging=running)
            if running: charging_dorsal(p)
            for side,offset in [('L',0),('R',.46 if running else .5)]:
                u=(t+offset)%1
                ankle=rig.data.bones['foot.'+side].head_local.copy()
                ankle.x*=1.08 if running else 1.03
                if u<contact:
                    ankle.y+=-stride/2+stride*u/contact
                    pitch=0
                else:
                    swing=(u-contact)/(1-contact)
                    ankle.y+=stride/2-stride*smooth(swing)
                    ankle.z+=lift*math.sin(math.pi*swing)**2
                    ankle.x+=(.009 if side=='L' else -.009)*math.sin(math.pi*swing)
                    pitch=0
                solve_leg(side,ankle,pitch)
''' + code[end:]
# Distinct silhouettes: upright watchful idle, loose hanging walk arms,
# forward-reaching charge, and visibly different attack anticipation.
code=code.replace("if name=='Idle':\n            hunting_posture(p)", """if name=='Idle':
            # Neutral standing silhouette; no hunting crouch in the default pose.
            hunting_posture(p,0,0)
            move('pelvis',(0,0,-.002*(1+math.sin(p))))
            rotate('pelvis',0,0,0)
            rotate('spine',.6*math.sin(p),0,0)
            rotate('chest',-.4*math.sin(p),0,0)
            rotate('neck',0,0,0)""")
code=code.replace("rotate('head',-14+2*math.sin(p)", "rotate('head',.5*math.sin(p)")
code=code.replace("ready=[37,22,6,12,38,76,115,148]", "ready=[18,6,0,10,32,64,104,140]")
code=code.replace('if charging: ready=[22,10,2,16,40,72,112,145]',
                  'if charging: ready=[62,48,30,16,28,54,95,132]')
# Reduce accumulated pelvis/spine/chest pitch during attacks and recovery.
# Keep the Slam's large downstroke and the Death fall intentional.
start=code.index("        elif name in ['Attack_Sweep'")
end=code.index('        else:\n            fall=', start)
section=code[start:end]
for old,new in [
    ("rotate('pelvis',7", "rotate('pelvis',2"),
    ("rotate('spine',15", "rotate('spine',4"),
    ("rotate('chest',11", "rotate('chest',2"),
    ("rotate('neck',-9", "rotate('neck',-2"),
    ("rotate('head',-14", "rotate('head',-4"),
    ('-.058', '-.030'),
]:
    section=section.replace(old,new)
code=code[:start]+section+code[end:]
# Make the stagger a brief interruption with a deliberate, resistant recovery.
code = code.replace('(.16,1),(.35,.8),(.7,.2)', '(.12,1),(.27,.65),(.62,.12)')
code = code.replace('15-22*recoil', '15-15*recoil')
code = code.replace('11-18*recoil', '11-12*recoil')
# Save only the requested Blender asset. Exporting to Unity is a separate step.
start = code.index("rig.animation_data.action=actions['Attack']; scene.frame_start")
code = code[:start] + '''
rig.animation_data.action=actions['Idle']
scene.frame_start=1; scene.frame_end=97; scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); bpy.context.view_layer.objects.active=rig
rig['Animation_notes']='Nine actions, upright boss posture. Select a scene named Preview - ACTION for correct playback range. Run: 18 frames at 30 fps. Main scene: Idle.'
# Independent preview scenes prevent the previous 18-frame Run range from
# truncating every other action when the user switches animations.
for old_scene in list(bpy.data.scenes):
    if old_scene.name.startswith('Preview - '):
        bpy.data.scenes.remove(old_scene)
for name,length,loop in clips:
    preview=bpy.data.scenes.new('Preview - '+name)
    preview.world=scene.world
    preview.render.fps=30
    preview.frame_start=1
    preview.frame_end=length if loop else length+1
    preview_rig=rig.copy()
    preview_rig.name='PreviewRig_'+name
    preview_rig.animation_data_clear()
    preview_rig.animation_data_create()
    preview_rig.animation_data.action=actions[name]
    preview.collection.objects.link(preview_rig)
    preview_mesh=mesh.copy()
    preview_mesh.name='PreviewMesh_'+name
    if mesh.parent==rig:
        preview_mesh.parent=preview_rig
    for modifier in preview_mesh.modifiers:
        if modifier.type=='ARMATURE' and modifier.object==rig:
            modifier.object=preview_rig
    preview.collection.objects.link(preview_mesh)
    preview.frame_set(1)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'DOPESHEET_EDITOR':
            area.spaces.active.mode='ACTION'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'MotherMoster_Animated.blend'))
with open(os.path.join(OUT,'boss_animation_validation.json'),'w') as f:
    json.dump(report,f,indent=2)
print('BOSS_ACTIONS_SAVED',json.dumps(report),flush=True)
'''
exec(compile(code, str(folder / 'animate_mother.py'), 'exec'))
