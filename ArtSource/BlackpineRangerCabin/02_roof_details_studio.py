import bpy, math, random, os
from mathutils import Vector
base=r"C:/Users/namphung/FPS/ArtSource/BlackpineRangerCabin"
scene=bpy.context.scene
asset=bpy.data.collections["CABIN - 6m x 8m + 2m porch"]
studio=bpy.data.collections["STUDIO - exclude from game export"]
src=open(base+"/01_structure.py",encoding="utf-8-sig").read()
exec(src[src.index("def material"):src.index("woods=")])
exec(src[src.index("def mesh"):src.index("# Real raised")])
woods=[bpy.data.materials["Weathered brown %02d"%i] for i in range(7)]
trim=bpy.data.materials["Worn grey timber"]; dark=bpy.data.materials["Cracks and iron"]
moss=bpy.data.materials["Sparse base moss"]; rust=bpy.data.materials["Restrained oxide rust"]
roofm=[bpy.data.materials["Old zinc %02d"%i] for i in range(4)]
stone=[bpy.data.materials["Chimney stone %d"%i] for i in range(4)]
glass=bpy.data.materials["Dusty blue grey glass"]; endgrain=bpy.data.materials["Cut firewood"]; cream=bpy.data.materials["Faded sign lettering"]
random.seed(91)
# Low-poly corrugation follows the fall line. Geometry stays light.
for side in [-1,1]:
 verts=[]; faces=[]
 for i in range(177):
  y=-4.3+8.6*i/176
  ripple=[0,.028,0,-.008][i%4]
  verts.extend([(0,y,5.84+ripple),(side*3.4,y,3.64+ripple)])
 for i in range(176):
  q=(2*i,2*i+1,2*i+3,2*i+2)
  faces.append(q if side==1 else tuple(reversed(q)))
 ob=mesh("Corrugated main roof "+str(side),verts,faces,roofm[0])
 for m in roofm[1:]+[rust]: ob.data.materials.append(m)
 for p in ob.data.polygons: p.material_index=4 if random.random()<.09 else random.randrange(4)
for y in [-4.32,4.32]:
 for side in [-1,1]: beam("Roof verge fascia",(0,y,5.84),(side*3.4,y,3.64),.15,.16,trim)
for side in [-1,1]: box("Main eave fascia",(side*3.4,0,3.62),(.14,8.7,.17),trim)
mesh("Folded metal ridge cap",[(-.16,-4.35,5.75),(0,-4.35,5.9),(.16,-4.35,5.75),(-.16,4.35,5.75),(0,4.35,5.9),(.16,4.35,5.75)],[(0,1,4,3),(1,2,5,4)],roofm[2])
verts=[];faces=[]
for i in range(129):
 x=-3.23+6.46*i/128; dz=[0,.023,0,-.005][i%4]
 verts.extend([(x,-6.22,3.54+dz),(x,-3.86,4.09+dz)])
for i in range(128): faces.append((2*i,2*i+2,2*i+3,2*i+1))
ob=mesh("Corrugated porch canopy",verts,faces,roofm[1])
for m in roofm[2:]+[rust]: ob.data.materials.append(m)
for p in ob.data.polygons: p.material_index=3 if random.random()<.07 else random.randrange(3)
for x in [-3.23,3.23]: beam("Canopy verge",(x,-6.22,3.52),(x,-3.86,4.07),.1,.13,trim)
box("Canopy drip edge",(0,-6.22,3.5),(6.55,.10,.10),roofm[0])
# A single short stone chimney, open dark flue and stone coping.
for j in range(7):
 z=4.67+j*.22
 for k in range(3):
  box("Chimney masonry",(-1.75+(k-1)*.24,1.5,z),(.23,.67,.21),random.choice(stone))
box("Chimney coping",(-1.75,1.5,6.17),(.9,.85,.12),stone[1])
box("Dark flue opening",(-1.75,1.5,6.234),(.47,.4,.01),dark)
# Sign: legible text, simple weathered wood, above the entrance at porch header.
box("Cabin name sign",(0,-6.01,3.22),(3.48,.10,.34),woods[0])
cv=bpy.data.curves.new("BLACKPINE RANGER CABIN lettering",'FONT')
cv.body="BLACKPINE RANGER CABIN"; cv.size=.208; cv.resolution_u=1
cv.extrude=.0005
text=bpy.data.objects.new("Sign lettering",cv); asset.objects.link(text); cv.materials.append(cream)
text.rotation_euler=(math.pi/2,0,0)
bpy.context.view_layer.update()
text.location=(-text.dimensions.x/2,-6.068,3.15)
# One compact toolbox and a small stack of firewood, kept away from the doorway.
box("Toolbox body",(-2.25,-4.65,.84),(.78,.43,.48),material("Toolbox faded green",(.10,.135,.10)))
box("Toolbox lid",(-2.25,-4.65,1.1),(.82,.46,.09),trim)
for x in [-2.50,-2.00]: box("Toolbox band",(x,-4.65,.89),(.055,.48,.54),dark)
box("Toolbox handle",(-2.25,-4.65,1.20),(.26,.04,.045),dark)
for x in [-2.38,-2.12]: box("Handle stand",(x,-4.65,1.16),(.035,.04,.09),dark)
for row,count in [(0,4),(1,3),(2,2)]:
 for i in range(count):
  x=1.75+i*.24+row*.12; y=-4.65
  ob=cylinder("Stacked firewood",(x,y,.73+row*.20),.125,.7,woods[1],7)
  ob.rotation_euler.x=math.pi/2
  ob.data.materials.append(endgrain); ob.data.polygons[0].material_index=1; ob.data.polygons[1].material_index=1
# Unlit hurricane lantern beside the door.
beam("Lantern wall bracket",(.98,-4.10,2.57),(.98,-4.45,2.57),.035,.035,dark)
cylinder("Lantern base",(.98,-4.43,2.12),.115,.07,dark,8)
cylinder("Lantern glass",(.98,-4.43,2.30),.085,.28,glass,8)
cylinder("Lantern cap",(.98,-4.43,2.47),.115,.07,dark,8)
for x in [.88,1.08]: beam("Lantern guard",(x,-4.43,2.13),(x,-4.43,2.48),.018,.018,dark)
beam("Lantern hanging loop",(.98,-4.43,2.5),(.98,-4.43,2.58),.02,.02,dark)
# Weathering is sparse flat geometry: grain cracks and moss at the plinth.
crackv=[];crackf=[]; mossv=[];mossf=[]
for o in list(asset.objects):
 if not any(o.name.startswith(t) for t in ["Front cladding","Side cladding","Rear cladding","Gable board"]): continue
 side=o.name.startswith("Side")
 for n in range(2):
  along=random.uniform(-.42,.42)*(o.dimensions.y if side else o.dimensions.x)
  length=min(random.uniform(.12,.65),(o.dimensions.y if side else o.dimensions.x)*.7)
  z=o.location.z+random.uniform(-.07,.07)
  if side:
   x=o.location.x+(.071 if o.location.x>0 else -.071); y=o.location.y+along
   vs=[(x,y-length/2,z),(x,y+length/2,z+.007),(x,y+length*.17,z-.008)]
  else:
   y=o.location.y+(-.071 if o.location.y<0 else .071); x=o.location.x+along
   vs=[(x-length/2,y,z),(x+length/2,y,z+.007),(x+length*.17,y,z-.008)]
  a=len(crackv);crackv.extend(vs);crackf.append((a,a+1,a+2))
 if o.location.z<1.0 and random.random()<.75:
  if side:
   x=o.location.x+(.073 if o.location.x>0 else -.073); y=o.location.y
   vs=[(x,y-.8,.62),(x,y+.65,.62),(x,y+.4,.8),(x,y-.1,.7),(x,y-.5,.78)]
  else:
   y=o.location.y+(-.073 if o.location.y<0 else .073); x=o.location.x
   vs=[(x-.6,y,.62),(x+.5,y,.62),(x+.36,y,.76),(x-.2,y,.70),(x-.5,y,.82)]
  a=len(mossv);mossv.extend(vs);mossf.append(tuple(range(a,a+5)))
mesh("Subtle split wood grain",crackv,crackf,dark)
mesh("Sparse moss along lower boards",mossv,mossf,moss)
# Camera and neutral studio backdrop.
floor=material("Studio light grey",(.64,.66,.67))
box("Studio ground",(0,0,-.08),(200,200,.12),floor,studio)
camdata=bpy.data.cameras.new("Three quarter reference")
cam=bpy.data.objects.new("Three quarter reference",camdata);studio.objects.link(cam)
cam.location=(12,-19,10); target=Vector((0,-.9,2.5))
cam.rotation_euler=(target-Vector(cam.location)).to_track_quat('-Z','Y').to_euler()
camdata.type='ORTHO';camdata.ortho_scale=16.5;scene.camera=cam
for name,pos,power,size in [("Large daylight",(-6,-10,14),2100,8),("Soft fill",(8,-4,9),1500,7),("Roof soft light",(0,7,12),1800,6)]:
 ld=bpy.data.lights.new(name,'AREA');ld.energy=power;ld.shape=next(i.identifier for i in ld.bl_rna.properties['shape'].enum_items if i.identifier=='DISK');ld.size=size
 lo=bpy.data.objects.new(name,ld);studio.objects.link(lo);lo.location=pos
 lo.rotation_euler=(Vector((0,0,2))-lo.location).to_track_quat('-Z','Y').to_euler()
scene.world=bpy.data.worlds.new("Neutral daylight world")
scene.world.use_nodes=True
bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND')
bg.inputs[0].default_value=(.7,.73,.76,1);bg.inputs[1].default_value=.45
scene.render.resolution_x=1600;scene.render.resolution_y=1400;scene.render.resolution_percentage=70
scene.render.image_settings.file_format='PNG'
scene.render.filepath=base+"/BlackpineCabin_preview.png"
# Keep source parts editable. Export batching is handled separately.
bpy.context.view_layer.update()
print("Asset objects",len(asset.objects))
print("Mesh triangles",sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in asset.objects if o.type=='MESH'))
bpy.ops.wm.save_as_mainfile(filepath=base+"/BlackpineRangerCabin.blend")
