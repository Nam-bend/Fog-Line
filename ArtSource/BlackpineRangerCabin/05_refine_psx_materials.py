import bpy,numpy as np,math
out=r"C:/Users/namphung/FPS/ArtSource/BlackpineRangerCabin/Optimized"
asset=bpy.data.collections["CABIN optimized"]
rng=np.random.default_rng(47)
Y,X=np.mgrid[0:128,0:128]
fine=rng.normal(0,1,(128,128))
# Desaturated, broken rust edges instead of large square colour patches.
ridge=np.where(X%8<2,-.055,np.where(X%8==3,.038,0))
roof=np.array([.28,.295,.285])+(.012*fine+ridge)[:,:,None]
patch=np.sin(X*.145+np.sin(Y*.07)*2)+np.cos(Y*.23+X*.03)+np.sin(X*.4-Y*.13)*.3
strength=np.clip((patch-1.22)*1.7,0,.85)
roof=roof*(1-strength[:,:,None])+np.array([.245,.175,.118])*strength[:,:,None]
def update(name,rgb):
 im=bpy.data.images[name];arr=np.ones((*rgb.shape[:2],4),np.float32);arr[:,:,:3]=np.clip(rgb,0,1)
 im.pixels.foreach_set(arr.ravel());im.filepath_raw=out+"/"+name+".png";im.save();im.pack()
update("BP_Roof_128",roof)
# Posts and frames use uninterrupted grain, not horizontal plank seams.
grain=np.sin(X*.8+np.sin(Y*.06)*.5)+np.sin(X*.28)*.5
rgb=np.array([.27,.25,.207])+(.013*fine+.016*grain)[:,:,None]
im=bpy.data.images.new("BP_Trim_128",width=128,height=128,alpha=True)
update("BP_Trim_128",rgb)
m=bpy.data.materials.new("BP_Trim_128")
p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
p.inputs['Roughness'].default_value=.95;p.inputs['Specular IOR Level'].default_value=.12
t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=im;t.interpolation='Closest'
m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color'])
for ob in asset.objects:
 if ob.type!='MESH':continue
 if ob.name.startswith(("PSX front wall","PSX back wall","PSX left wall","PSX right wall","PSX solid gable","PSX interior floor","PSX porch deck")):continue
 for i,mat in enumerate(ob.data.materials):
  if mat.name=='BP_Wood_256':ob.data.materials[i]=m
s=bpy.context.scene;s.render.filepath=out+"/BlackpineCabin_PSX.png";s.render.resolution_percentage=100
bpy.ops.wm.save_as_mainfile(filepath=out+"/BlackpineRangerCabin_PSX.blend")
bpy.app.timers.register(lambda:(bpy.ops.render.render(write_still=True) and None),first_interval=1)
print("Refined rust edges and unbroken grain on structural trim")

