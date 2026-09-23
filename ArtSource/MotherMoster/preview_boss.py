import bpy, math, json
from pathlib import Path
from mathutils import Vector

folder=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(folder/'MotherMoster_Animated.blend'))
rig=bpy.data.objects['MotherMoster_Rig']
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers))
scene=bpy.context.scene
assert len(bpy.data.actions)==9
print('REOPENED_ACTIONS', [(a.name,list(a.frame_range)) for a in bpy.data.actions], flush=True)
scene.render.engine='BLENDER_WORKBENCH'
scene.display.shading.light='STUDIO'
scene.display.shading.color_type='TEXTURE'
scene.display.shading.show_shadows=True
scene.display.shading.show_cavity=True
scene.display.shading.background_type='WORLD'
scene.world.color=(.045,.05,.06)
scene.render.resolution_x=640
scene.render.resolution_y=480
scene.render.resolution_percentage=100
data=bpy.data.cameras.new('BossPreview')
camera=bpy.data.objects.new('BossPreview',data)
scene.collection.objects.link(camera)
scene.camera=camera
data.type='ORTHO'
out=folder/'BossPreviews'
out.mkdir(exist_ok=True)
clips={}
for name in ['Run','Idle','Walk','Attack','Attack_Sweep','Attack_Combo','Attack_Slam','Stagger','Death']:
    action=bpy.data.actions['MotherMoster_'+name]
    rig.animation_data.action=action
    length=int(action.frame_range[1])-1
    direction=Vector((2,-3,.65)).normalized()
    rotation=(-direction).to_track_quat('-Z','Y')
    axes=[rotation@Vector((1,0,0)),rotation@Vector((0,1,0)),direction]
    bounds=[]
    for frame in range(1,length+2):
        scene.frame_set(frame)
        evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        geometry=evaluated.to_mesh()
        for v in geometry.vertices:
            point=mesh.matrix_world@v.co
            bounds.append([point.dot(a) for a in axes])
        evaluated.to_mesh_clear()
    low=[min(v[i] for v in bounds) for i in range(3)]
    high=[max(v[i] for v in bounds) for i in range(3)]
    center=sum((a*((low[i]+high[i])/2) for i,a in enumerate(axes)),Vector())
    camera.location=center+direction*5
    camera.rotation_euler=rotation.to_euler()
    data.ortho_scale=max(high[0]-low[0],(high[1]-low[1])*4/3)*1.17
    frames=list(range(1,length+1,2))
    if name=='Death': frames.append(length+1)
    clips[name]=frames
    for frame in frames:
        scene.frame_set(frame)
        scene.render.filepath=str(out/f'{name}_{frame:03}.png')
        bpy.ops.render.render(write_still=True)
    print('PREVIEW_DONE',name,flush=True)
html='''<!doctype html><meta charset="utf-8"><title>MotherMoster Boss Actions</title>
<style>body{background:#11151b;color:#eee;font:16px system-ui;max-width:960px;margin:24px auto}img{width:100%;max-width:800px}button,select,input{margin:8px;padding:8px}p{color:#b9c5d5}</style>
<h1>MotherMoster — Boss animation preview</h1><p>Actual saved Blender actions · 30 fps · in-place movement</p>
<select id="clip"></select><button id="play">Pause</button><label>Speed <select id="speed"><option value="1">1×</option><option value="0.5">0.5×</option><option value="0.25">0.25×</option></select></label>
<div><img id="view"></div><input id="scrub" type="range" min="0" value="0"><span id="counter"></span>
<script>const clips=CLIP_DATA;const selector=document.querySelector('#clip'),view=document.querySelector('#view'),scrub=document.querySelector('#scrub');let index=0,playing=true;
for(const name of Object.keys(clips)){selector.add(new Option(name,name));for(const f of clips[name]){const im=new Image();im.src=`${name}_${String(f).padStart(3,'0')}.png`;}}
function show(){const frames=clips[selector.value];scrub.max=frames.length-1;scrub.value=index;view.src=`${selector.value}_${String(frames[index]).padStart(3,'0')}.png`;document.querySelector('#counter').textContent=`Frame ${frames[index]}`;}
selector.onchange=()=>{index=0;show();};scrub.oninput=()=>{index=+scrub.value;show();};document.querySelector('#play').onclick=()=>{playing=!playing;document.querySelector('#play').textContent=playing?'Pause':'Play';};
function tick(){if(playing){index=(index+1)%clips[selector.value].length;show();}setTimeout(tick,1000/(15*Number(document.querySelector('#speed').value)));}show();tick();</script>'''
(out/'preview.html').write_text(html.replace('CLIP_DATA',json.dumps(clips)),encoding='utf-8')
