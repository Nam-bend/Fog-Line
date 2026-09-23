import bpy, json, math, os
from mathutils import Vector

OUT=r'C:\Users\namphung\FPS\ArtSource\MotherMoster'
os.makedirs(OUT,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=r'C:\Blender\MotherMoster.blend')
if bpy.context.object and bpy.context.object.mode!='OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True)
bpy.context.view_layer.objects.active=mesh
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
arm=bpy.data.armatures.new('MotherMoster_Skeleton')
rig=bpy.data.objects.new('MotherMoster_Rig',arm)
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig
mesh.select_set(False)
rig.select_set(True)
rig.show_in_front=True
arm.display_type='OCTAHEDRAL'
bpy.ops.object.mode_set(mode='EDIT')
def bone(name,head,tail,parent=None,deform=True):
    b=arm.edit_bones.new(name)
    b.head=head; b.tail=tail; b.use_deform=deform
    if parent:
        b.parent=arm.edit_bones[parent]
        b.use_connect=(b.head-b.parent.tail).length<0.0001
    return name
def chain(prefix,points,parent):
    for i in range(len(points)-1):
        parent=bone(f'{prefix}.{i+1:02}',points[i],points[i+1],parent)
    return parent
bone('root',(0,0,0),(0,0,.10),deform=False)
bone('pelvis',(.005,.035,.385),(.005,.04,.47),'root')
bone('spine',(.005,.04,.47),(.005,.035,.565),'pelvis')
bone('chest',(.005,.035,.565),(.005,.045,.68),'spine')
bone('neck',(.005,.045,.68),(.005,.022,.83),'chest')
bone('head',(.005,.022,.83),(.005,.026,.943),'neck')
for sign,side in [(1,'L'),(-1,'R')]:
    p=lambda x,y,z:(.005+sign*x,y,z)
    bone('thigh.'+side,p(.063,.047,.405),p(.145,.001,.222),'pelvis')
    bone('shin.'+side,p(.145,.001,.222),p(.188,.011,.047),'thigh.'+side)
    bone('foot.'+side,p(.188,.011,.047),p(.219,-.075,.011),'shin.'+side)
    bone('clavicle.'+side,p(.018,.037,.671),p(.113,.027,.668),'chest')
    if sign==1:
        points=[p(.113,.027,.668),p(.19,.049,.591),p(.242,.061,.529),p(.249,.024,.461),p(.222,-.018,.375),p(.215,.012,.282),p(.258,.056,.192),p(.314,.077,.143),p(.338,.085,.091)]
    else:
        points=[p(.113,.027,.668),p(.174,.042,.59),p(.242,.051,.528),p(.258,.012,.453),p(.251,-.004,.364),p(.235,.02,.272),p(.21,.059,.171),p(.223,.079,.111),p(.279,.088,.05),p(.324,.09,.022)]
    chain('arm_tentacle.'+side,points,'clavicle.'+side)
# The upper and lower dorsal tentacles are asymmetric in the source mesh.
chain('dorsal_upper.L',[(.06,.097,.718),(.117,.144,.735),(.154,.188,.775),(.164,.232,.835),(.192,.272,.908),(.228,.27,.946)],'chest')
chain('dorsal_upper.R',[(-.05,.102,.716),(-.095,.164,.753),(-.073,.241,.741),(-.014,.293,.77),(.033,.293,.815),(.056,.279,.858),(.1,.249,.881)],'chest')
chain('dorsal_lower.L',[(.062,.093,.555),(.093,.166,.53),(.105,.229,.465),(.148,.267,.421),(.19,.259,.386),(.205,.258,.336),(.205,.266,.27)],'spine')
chain('dorsal_lower.R',[(-.058,.095,.556),(-.094,.166,.52),(-.129,.208,.451),(-.142,.217,.385),(-.134,.234,.31),(-.144,.225,.224),(-.135,.205,.159)],'spine')
bpy.ops.object.mode_set(mode='OBJECT')
mesh.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
rig['Rig_notes']='FK deform rig. Root moves the entire character. Separate chains for arms and four dorsal tentacles. Rest pose matches original mesh.'
mesh['Source_file']=r'C:\Blender\MotherMoster.blend'
# Normalize weights and ensure every vertex is bound.
deform={b.name for b in arm.bones if b.use_deform}
segments=[(mesh.vertex_groups.get(b.name),b.head_local.copy(),b.tail_local.copy()) for b in arm.bones if b.use_deform]
missing=[]
for v in mesh.data.vertices:
    if sum(g.weight for g in v.groups if mesh.vertex_groups[g.group].name in deform)<1e-7:
        missing.append(v.index)
        pos=mesh.matrix_world@v.co
        def distance(s):
            group,a,b=s; d=b-a
            return (pos-(a+d*max(0,min(1,(pos-a).dot(d)/d.length_squared)))).length
        group,a,b=min(segments,key=distance)
        group.add([v.index],1,'REPLACE')
bpy.context.view_layer.objects.active=mesh
rig.select_set(False)
bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
bpy.ops.object.vertex_group_normalize_all(lock_active=False)
bpy.ops.object.mode_set(mode='OBJECT')
for m in mesh.modifiers:
    if m.type=='ARMATURE': m.use_deform_preserve_volume=True
bpy.ops.file.pack_all()
# Save a useful opening view with the armature selected.
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active=rig
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_location=(0,.07,.48)
            area.spaces.active.region_3d.view_distance=1.7
            from mathutils import Quaternion
            area.spaces.active.region_3d.view_rotation=Quaternion((1,0,0),math.radians(90))
            area.spaces.active.clip_end=100
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'MotherMoster_Rigged.blend'))
mesh.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'MotherMoster_Rigged.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
report={'vertices':len(mesh.data.vertices),'bones':len(arm.bones),'deform_bones':len(deform),'fallback_weight_vertices':len(missing),'unweighted_vertices':sum(not any(g.weight>1e-7 for g in v.groups) for v in mesh.data.vertices),'packed_images':[im.name for im in bpy.data.images if im.packed_file]}
with open(os.path.join(OUT,'rig_report.json'),'w') as f: json.dump(report,f,indent=2)
print('RIG_REPORT',json.dumps(report))
