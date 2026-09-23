import bpy
from pathlib import Path

root=Path(__file__).resolve().parent
asset=root.parent.parent/'Assets'/'_Project'/'Art'/'Enemies'/'MotherMosterPSX'/'MotherMosterPSX.fbx'
bpy.ops.wm.open_mainfile(filepath=str(root/'MotherMoster_Animated.blend'))
scene=bpy.context.scene
scene.render.fps=30
rig=bpy.data.objects['MotherMoster_Rig']
mesh=next(o for o in bpy.data.objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers))
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); mesh.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(asset), use_selection=True,
    object_types={'ARMATURE','MESH'}, add_leaf_bones=False, bake_anim=True,
    bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0, bake_anim_step=1, axis_forward='-Z',
    axis_up='Y', path_mode='COPY', embed_textures=True)
print('EXPORTED_MOTHER_TO_UNITY', asset, flush=True)
