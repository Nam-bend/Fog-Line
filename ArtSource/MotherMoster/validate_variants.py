import bpy, json, os
OUT=r'C:\Users\namphung\FPS\ArtSource\MotherMoster'
bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT,'MotherMoster_Animated.blend'))
rig=bpy.data.objects['MotherMoster_Rig']
def sample(name,frame):
    rig.animation_data.action=bpy.data.actions['MotherMoster_'+name]
    bpy.context.scene.frame_set(frame)
    return {side:{'tip':list(rig.pose.bones['arm_tentacle.'+side+'.08'].tail),'shoulder':list(rig.pose.bones['arm_tentacle.'+side+'.01'].head)} for side in ['L','R']}
sweep_a=sample('Attack_Sweep',10);sweep_b=sample('Attack_Sweep',19)
slam_a=sample('Attack_Slam',14);slam_b=sample('Attack_Slam',25)
combo_a=sample('Attack_Combo',16);combo_b=sample('Attack_Combo',29)
result={
    'sweep_horizontal_travel':{s:abs(sweep_a[s]['tip'][0]-sweep_b[s]['tip'][0]) for s in ['L','R']},
    'slam_vertical_travel':{s:slam_a[s]['tip'][2]-slam_b[s]['tip'][2] for s in ['L','R']},
    'combo_left_first_reach':combo_a['R']['tip'][1]-combo_a['L']['tip'][1],
    'combo_right_second_reach':combo_b['L']['tip'][1]-combo_b['R']['tip'][1],
}
assert min(result['sweep_horizontal_travel'].values())>.7
assert min(result['slam_vertical_travel'].values())>.7
assert result['combo_left_first_reach']>.05 and result['combo_right_second_reach']>.05
for name in ['Idle','Walk','Run','Attack','Attack_Sweep','Attack_Combo','Attack_Slam']:
    rig.animation_data.action=bpy.data.actions['MotherMoster_'+name]
    for frame in range(1,int(rig.animation_data.action.frame_range[1])+1):
        bpy.context.scene.frame_set(frame)
        assert rig.pose.bones['root'].location.length<1e-6
result['root_motion_in_place']=True
with open(os.path.join(OUT,'attack_variants_validation.json'),'w') as f:json.dump(result,f,indent=2)
print('VARIANTS_VALIDATED',json.dumps(result))
