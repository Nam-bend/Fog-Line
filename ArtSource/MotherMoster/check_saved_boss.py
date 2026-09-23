import bpy, json, math
from pathlib import Path
from mathutils import Vector

folder=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(folder/'MotherMoster_BeforeBoss.blend'))
old_rig=bpy.data.objects['MotherMoster_Rig']
rest={b.name:(tuple(b.head_local),tuple(b.tail_local)) for b in old_rig.data.bones}
old_run=list(bpy.data.actions['MotherMoster_Run'].frame_range)
bpy.ops.wm.open_mainfile(filepath=str(folder/'MotherMoster_Animated.blend'))
rig=bpy.data.objects['MotherMoster_Rig']
assert rest=={b.name:(tuple(b.head_local),tuple(b.tail_local)) for b in rig.data.bones}
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers))
expected={'Idle','Walk','Run','Attack','Attack_Sweep','Attack_Combo','Attack_Slam','Stagger','Death'}
assert {a.name.removeprefix('MotherMoster_') for a in bpy.data.actions}==expected
report={'direct_saved_file_check':True,'rest_skeleton_unchanged':True,'original_run_frames':old_run,'actions':[]}
previews=[s for s in bpy.data.scenes if s.name.startswith('Preview - ')]
assert len(previews)==9
for preview in previews:
    preview_rig=next(o for o in preview.objects if o.type=='ARMATURE')
    action=preview_rig.animation_data.action
    assert action.name=='MotherMoster_'+preview.name.removeprefix('Preview - ')
    end=int(action.frame_range[1])
    assert preview.frame_end in (end,end-1)
    preview_mesh=next(o for o in preview.objects if o.type=='MESH')
    assert any(m.type=='ARMATURE' and m.object==preview_rig for m in preview_mesh.modifiers)
report['nine_preview_scenes_valid']=True
for action in bpy.data.actions:
    rig.animation_data.action=action
    frames=[]
    minimum=1e9
    root_drift=0
    max_step=0
    previous=None
    for frame in range(int(action.frame_range[0]),int(action.frame_range[1])+1):
        bpy.context.scene.frame_set(frame)
        root_drift=max(root_drift,rig.pose.bones['root'].location.length)
        ev=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
        geom=ev.to_mesh()
        points=[mesh.matrix_world@v.co for v in geom.vertices]
        assert all(math.isfinite(c) for v in points for c in v)
        minimum=min(minimum,min(v.z for v in points))
        if previous is not None:
            max_step=max(max_step,max((v-w).length for v,w in zip(previous,points)))
        previous=points
        frames.append(points)
        ev.to_mesh_clear()
    seam=max((v-w).length for v,w in zip(frames[0],frames[-1]))
    if action.name.removeprefix('MotherMoster_') in {'Idle','Walk','Run'}:
        assert seam<1e-5
    assert minimum>-.001,(action.name,minimum)
    assert root_drift<1e-6
    report['actions'].append({'name':action.name,'frames':list(action.frame_range),'minimum_z':minimum,'endpoint_distance':seam,'maximum_vertex_step':max_step})
(folder/'saved_boss_check.json').write_text(json.dumps(report,indent=2))
print('SAVED_BOSS_CHECK',json.dumps(report),flush=True)
