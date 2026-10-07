import bpy, math, random, os, json, numpy as np
from mathutils import Vector
base=r"C:/Users/namphung/FPS/ArtSource/BlackpineRangerCabin"
out=base+"/Optimized"
os.makedirs(out,exist_ok=True)
old=bpy.context.scene
source=bpy.data.collections["CABIN - 6m x 8m + 2m porch"]
scene=bpy.data.scenes.new("Blackpine Cabin - PSX optimized")
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
asset=bpy.data.collections.new("CABIN optimized");scene.collection.children.link(asset)
studio=bpy.data.collections.new("PSX reference studio");scene.collection.children.link(studio)
src=open(base+"/01_structure.py",encoding="utf-8-sig").read()
exec(src[src.index("def material"):src.index("woods=")])
exec(src[src.index("def mesh"):src.index("# Real raised")])
rng=np.random.default_rng(28)
def texture(name,rgb):
 h,w,_=rgb.shape
 arr=np.ones((h,w,4),dtype=np.float32);arr[:,:,:3]=np.clip(rgb,0,1)
 im=bpy.data.images.new(name,width=w,height=h,alpha=True)
 im.pixels.foreach_set(arr.ravel());im.filepath_raw=out+"/"+name+".png";im.file_format='PNG';im.save();im.pack()
 m=material(name,(.18,.16,.13),.95)
 p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
 p.inputs['Specular IOR Level'].default_value=.12
 t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=im;t.interpolation='Closest'
 m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color'])
 return m
# Small, deliberately coarse hand-authored-style surface maps; no costly normal shader.
Y,X=np.mgrid[0:256,0:256]
coarse=rng.normal(0,1,(32,32)).repeat(8,0).repeat(8,1)
fine=rng.normal(0,1,(256,256))
row=rng.normal(0,1,(256,1))
board=rng.normal(0,1,(16,1)).repeat(16,0)
grain=np.sin(X*.13+np.sin(Y*.57)*4)+np.sin(X*.41+Y*.8)
wood=np.array([.29,.265,.218])+(.022*coarse+.009*fine+.012*row+.018*board+.008*grain)[:,:,None]
wood[(Y%16)==0]*=.42;wood[(Y%16)==1]*=.74;wood[(Y%16)==15]*=1.09
for i in range(95):
 y=int(rng.integers(3,252));x=int(rng.integers(0,220));length=int(rng.integers(5,32))
 wood[y,x:x+length]*=rng.uniform(.42,.72)
# Limited damp staining on bottom courses.
wet=np.clip((35-Y)/35,0,1)*(np.sin(X*.06)**2)
wood=wood*(1-wet[:,:,None]*.25)+wet[:,:,None]*np.array([.005,.017,.003])
woodmat=texture("BP_Wood_256",wood)
Y,X=np.mgrid[0:128,0:128]
noise=rng.normal(0,1,(128,128))
coarse=rng.random((16,16)).repeat(8,0).repeat(8,1)
ridge=np.where(X%8<2,-.085,np.where(X%8==3,.075,0))
roof=np.array([.33,.35,.34])+(.018*noise+ridge)[:,:,None]
mask=(coarse>.79)&(np.sin(Y*.09+X*.06)>.05)
roof[mask]=np.array([.30,.20,.125])+noise[mask,None]*.018
roofmat=texture("BP_Roof_128",roof)
stone=np.zeros((128,128,3))
for y in range(128):
 for x in range(128):
  seam=y%24<2 or (x+(12 if (y//24)%2 else 0))%32<2
  c=.18 if seam else .36+.025*math.sin((x//32)*14+(y//24)*7)
  stone[y,x]=np.array([c,c*.99,c*.94])+noise[y,x]*.02
stonemat=texture("BP_Stone_128",stone)
# A painted 5x7 stencil replaces the 872-triangle extruded lettering.
glyphs={
'B':['11110','10001','10001','11110','10001','10001','11110'],
'L':['10000','10000','10000','10000','10000','10000','11111'],
'A':['01110','10001','10001','11111','10001','10001','10001'],
'C':['01111','10000','10000','10000','10000','10000','01111'],
'K':['10001','10010','10100','11000','10100','10010','10001'],
'P':['11110','10001','10001','11110','10000','10000','10000'],
'I':['111','010','010','010','010','010','111'],
'N':['10001','11001','11001','10101','10011','10011','10001'],
'E':['11111','10000','10000','11110','10000','10000','11111'],
'R':['11110','10001','10001','11110','10100','10010','10001'],
'G':['01111','10000','10000','10111','10001','10001','01111'],
' ':['000']*7}
sign=np.tile(np.array([.15,.155,.125]),(32,256,1))+rng.normal(0,.01,(32,256,1))
word="BLACKPINE RANGER CABIN"
width=sum(len(glyphs[c][0])+1 for c in word)*2;x=(256-width)//2
for c in word:
 rows=glyphs[c]
 for j,line in enumerate(rows):
  for i,v in enumerate(line):
   if v=='1':sign[9+(6-j)*2:11+(6-j)*2,x+i*2:x+i*2+2]=[.64,.62,.48]
 x+=(len(rows[0])+1)*2
signmat=texture("BP_Sign_256x32",sign)
# Keep silhouette-bearing details from the editable source.
omit=("Front cladding","Rear cladding","Side cladding","Gable board","Interior floor plank","Porch deck plank","Corrugated","Chimney masonry","Subtle split","Sparse moss","Sign lettering")
for o in source.objects:
 if o.name.startswith(omit):continue
 ob=o.copy();ob.data=o.data.copy();ob.parent=None;ob.matrix_world=o.matrix_world.copy();asset.objects.link(ob)
 for i,m in enumerate(ob.data.materials):
  if m and (m.name.startswith("Weathered brown") or m.name.startswith("Worn grey timber")):ob.data.materials[i]=woodmat
  elif m and m.name.startswith("Chimney stone"):ob.data.materials[i]=stonemat
# Continuous walls retain real holes, with a few large rectangles instead of many board boxes.
def panels(front):
 holes=[(-.64,.64,.6,2.85),(-2.52,-1.38,1.65,2.75),(1.38,2.52,1.65,2.75)] if front else []
 ys=[.6,1.65,2.75,2.85,3.8] if front else [.6,3.8]
 for lo,hi in zip(ys,ys[1:]):
  spans=[(-3,3)]
  for a,b,c,d in holes:
   if (lo+hi)/2<c or (lo+hi)/2>d:continue
   result=[]
   for l,r in spans:
    if b<=l or a>=r:result.append((l,r))
    else:
     if l<a:result.append((l,a))
     if r>b:result.append((b,r))
   spans=result
  for a,b in spans:box("PSX front wall" if front else "PSX back wall",((a+b)/2,-4 if front else 4,(lo+hi)/2),(b-a,.14,hi-lo),woodmat)
panels(True);panels(False)
box("PSX left wall",(-3,0,2.2),(.14,8,3.2),woodmat)
for a,b,lo,hi in [(-4,4,.6,1.65),(-4,-.65,1.65,2.75),(.65,4,1.65,2.75),(-4,4,2.75,3.8)]:
 box("PSX right wall",(3,(a+b)/2,(lo+hi)/2),(.14,b-a,hi-lo),woodmat)
for y in [-4,4]:
 verts=[(-3,y-.07,3.8),(3,y-.07,3.8),(0,y-.07,5.8),(-3,y+.07,3.8),(3,y+.07,3.8),(0,y+.07,5.8)]
 mesh("PSX solid gable",verts,[(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)],woodmat)
box("PSX interior floor",(0,0,.55),(6,8,.1),woodmat)
box("PSX porch deck",(0,-5,.55),(6,2,.1),woodmat)
for side in [-1,1]:
 a=Vector((0,0,5.84));b=Vector((side*3.4,0,3.64))
 # Four sided slab, no corrugated geometry.
 verts=[(0,-4.3,5.84),(side*3.4,-4.3,3.64),(side*3.4,4.3,3.64),(0,4.3,5.84)]
 verts+= [(x,y,z-.055) for x,y,z in verts]
 faces=[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)]
 if side<0:faces=[tuple(reversed(f)) for f in faces]
 mesh("PSX main roof "+str(side),verts,faces,roofmat)
verts=[(-3.23,-6.22,3.54),(3.23,-6.22,3.54),(3.23,-3.86,4.09),(-3.23,-3.86,4.09)]
verts +=[(x,y,z-.04) for x,y,z in verts]
mesh("PSX porch roof",verts,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],roofmat)
box("PSX chimney",(-1.75,1.5,5.33),(.71,.67,1.54),stonemat)
# Planar metre-based UVs keep texel density predictable on structural faces.
for ob in asset.objects:
 if ob.type!='MESH':continue
 uv=ob.data.uv_layers.new(name="UVMap")
 ob.data.update()
 for p in ob.data.polygons:
  normal=(ob.matrix_world.to_3x3()@p.normal).normalized()
  axis=max(range(3),key=lambda i:abs(normal[i]))
  for li in p.loop_indices:
   co=ob.matrix_world@ob.data.vertices[ob.data.loops[li].vertex_index].co
   if axis==0:u,v=(co.y+4)/8,(co.z-.6)/3.2
   elif axis==1:u,v=(co.x+3)/6,(co.z-.6)/3.2
   else:u,v=(co.x+3)/6,(co.y+6)/10
   if ob.name.startswith("PSX main roof"):u,v=(co.y+4.3)/8.6,abs(co.x)/3.4
   elif ob.name.startswith("PSX porch roof"):u,v=(co.x+3.23)/6.46,(co.y+6.22)/2.36
   elif ob.name.startswith("PSX chimney"):u,v=((co.x+2.105)/.71 if axis==1 else (co.y-1.165)/.67),(co.z-4.56)/1.54
   uv.data[li].uv=(u,v)
# Paint on the front face of the existing sign board.
signboard=next(o for o in asset.objects if o.name.startswith("Cabin name sign"))
signboard.data.materials.append(signmat)
for p in signboard.data.polygons:
 if p.normal.y<-.9:
  p.material_index=len(signboard.data.materials)-1
  for li in p.loop_indices:
   co=signboard.data.vertices[signboard.data.loops[li].vertex_index].co
   signboard.data.uv_layers.active.data[li].uv=((co.x+1.74)/3.48,(co.z+.17)/.34)
for ob in bpy.data.collections["STUDIO - exclude from game export"].objects:
 copy=ob.copy();studio.objects.link(copy)
 if ob==old.camera:scene.camera=copy
scene.world=old.world
scene.render.resolution_x=1600;scene.render.resolution_y=1400;scene.render.resolution_percentage=70
scene.render.image_settings.file_format='PNG';scene.render.filepath=out+"/BlackpineCabin_PSX_preview.png"
print("Optimized triangles",sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in asset.objects if o.type=='MESH'))
bpy.ops.wm.save_as_mainfile(filepath=out+"/BlackpineRangerCabin_PSX.blend")
bpy.app.timers.register(lambda:(bpy.ops.render.render(write_still=True) and None),first_interval=1)

