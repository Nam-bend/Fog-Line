import bpy,json
SOURCE=r'C:\Users\namphung\FPS\ArtSource\MotherMoster\MotherMoster_Rigged.blend'
bpy.ops.wm.open_mainfile(filepath=SOURCE)
if bpy.context.object and bpy.context.object.mode!='OBJECT': bpy.ops.object.mode_set(mode='OBJECT')
def report(label):
    for o in bpy.context.scene.objects:
        if o.type=='MESH':
            o.data.calc_loop_triangles()
            print(label,'MESH',o.name,len(o.data.vertices),len(o.data.polygons),len(o.data.loop_triangles),'UV',len(o.data.uv_layers),'MOD',[(m.name,m.type) for m in o.modifiers])
        elif o.type=='ARMATURE': print(label,'BONES',json.dumps([(b.name,list(b.head_local),list(b.tail_local)) for b in o.data.bones]))
    for im in bpy.data.images: print(label,'IMAGE',im.name,list(im.size),im.filepath)
    for mat in bpy.data.materials:
        if mat.node_tree:
            print(label,'MAT',mat.name,[(n.name,n.type,n.image.name if n.type=='TEX_IMAGE' and n.image else '') for n in mat.node_tree.nodes])
report('MOTHER')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=r'C:\Users\namphung\FPS\Assets\_Project\Art\Enemies\MonsterPSX\MonsterPSX.fbx')
report('REFERENCE')
