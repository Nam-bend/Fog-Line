import bpy, math, random, os
from mathutils import Vector
random.seed(28)
base=r"C:/Users/namphung/FPS/ArtSource/BlackpineRangerCabin"
scene=bpy.data.scenes.new("Blackpine Cabin - Low Poly Reference")
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1
asset=bpy.data.collections.new("CABIN - 6m x 8m + 2m porch")
scene.collection.children.link(asset)
studio=bpy.data.collections.new("STUDIO - exclude from game export")
scene.collection.children.link(studio)
def material(name,c,rough=.8,metal=0):
 m=bpy.data.materials.new(name); m.diffuse_color=(*c,1)
 n=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
 n.inputs['Base Color'].default_value=(*c,1); n.inputs['Roughness'].default_value=rough; n.inputs['Metallic'].default_value=metal
 return m
woods=[material("Weathered brown %02d"%i,(.105+i*.013,.073+i*.010,.046+i*.008)) for i in range(7)]
trim=material("Worn grey timber",(.18,.16,.125))
dark=material("Cracks and iron",(.032,.031,.027),.85,.25)
moss=material("Sparse base moss",(.075,.105,.044))
roofm=[material("Old zinc %02d"%i,(.18+i*.017,.20+i*.017,.21+i*.017),.72,.35) for i in range(4)]
rust=material("Restrained oxide rust",(.23,.095,.041),.96)
stone=[material("Chimney stone %d"%i,(.22+i*.025,.225+i*.025,.21+i*.025)) for i in range(4)]
glass=material("Dusty blue grey glass",(.055,.105,.12),.22,.4)
endgrain=material("Cut firewood",(.34,.23,.12))
cream=material("Faded sign lettering",(.72,.68,.52))
def mesh(name,verts,faces,mat,coll=asset):
 me=bpy.data.meshes.new(name); me.from_pydata(verts,[],faces); me.update()
 ob=bpy.data.objects.new(name,me); coll.objects.link(ob)
 if mat: me.materials.append(mat)
 return ob
def box(name,loc,size,mat,coll=asset):
 x,y,z=[s*.5 for s in size]
 v=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
 o=mesh(name,v,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat,coll); o.location=loc
 return o
def beam(name,a,b,width,depth,mat):
 a,b=Vector(a),Vector(b); o=box(name,(a+b)/2,(width,depth,(b-a).length),mat)
 o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler(); return o
def cylinder(name,loc,radius,depth,mat,n=8):
 verts=[(radius*math.cos(i*2*math.pi/n),radius*math.sin(i*2*math.pi/n),z) for z in [-depth/2,depth/2] for i in range(n)]
 faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
 o=mesh(name,verts,faces,mat); o.location=loc; return o
# Real raised framing; hollow building and plank floor.
for x in [-2.65,0,2.65]:
 for y in [-5.65,-3.65,0,3.65]:
  box("Stone footing",(x,y,.18),(.48,.48,.36),random.choice(stone))
for x in [-2.8,0,2.8]: box("Floor bearer",(x,-1,.39),(.18,10,.2),woods[0])
for i in range(30):
 x=-2.9+i*.2
 box("Interior floor plank",(x,0,.55),(.193,8,.10),random.choice(woods))
 box("Porch deck plank",(x,-5,.55),(.193,2,.10),random.choice(woods))
for y in [-6,-4,4]: box("Deck fascia",(0,y,.44),(6.12,.15,.25),trim)
# Openings are omitted from wall courses, not painted onto solid walls.
open_front=[(-.64,.64,.60,2.85),(-2.52,-1.38,1.65,2.75),(1.38,2.52,1.65,2.75)]
def subtract(interval,holes):
 out=[interval]
 for lo,hi in holes:
  result=[]
  for a,b in out:
   if hi<=a or lo>=b: result.append((a,b))
   else:
    if a<lo: result.append((a,lo))
    if b>hi: result.append((hi,b))
  out=result
 return out
for j in range(16):
 z=.7+j*.2; zlo=z-.097; zhi=z+.097
 for front in [True,False]:
  y=-4 if front else 4
  holes=[(a,b) for a,b,c,d in open_front if zhi>c and zlo<d] if front else []
  for a,b in subtract((-3,3),holes):
   box("Front cladding" if front else "Rear cladding",((a+b)/2,y,z),(b-a,.14,.194),random.choice(woods))
 for side in [-1,1]:
  holes=[(-.65,.65)] if side==1 and zhi>1.65 and zlo<2.75 else []
  for a,b in subtract((-4,4),holes):
   box("Side cladding",(side*3,(a+b)/2,z),(.14,b-a,.194),random.choice(woods))
for x in [-3,3]:
 for y in [-4,4]: box("Corner post",(x,y,2.18),(.2,.2,3.16),trim)
# Gables built from horizontal courses tapering to the ridge.
for y in [-4,4]:
 for i in range(10):
  z=3.9+i*.2; width=max(.08,6*(5.8-z)/2)
  box("Gable board",(0,y,z),(width,.14,.192),random.choice(woods))
# Door with origin at left hinge, closed but editable.
door=box("DOOR - hinge pivot", (0,-4.08,1.70),(1.20,.13,2.18),woods[1])
for v in door.data.vertices: v.co.x+=.60
door.location.x=-.60
for x in [-.4,-.2,0,.2,.4]: box("Door board joint",(x,-4.151,1.70),(.009,.008,2.1),dark)
for z in [.94,2.46]:
 box("Iron door strap",(-.32,-4.165,z),(.47,.025,.055),dark)
box("Door latch",(.42,-4.19,1.58),(.035,.06,.19),dark)
for x in [-.69,.69]: box("Door jamb",(x,-4.08,1.72),(.12,.2,2.32),trim)
box("Door lintel",(0,-4.08,2.91),(1.5,.2,.15),trim)
box("Door threshold",(0,-4.14,.63),(1.4,.38,.09),trim)
def window(name,center,side=False):
 def part(label,u,d,z,w,t,h,mat):
  loc=(center[0]+d,center[1]+u,z) if side else (center[0]+u,center[1]-d,z)
  size=(t,w,h) if side else (w,t,h)
  return box(name+" "+label,loc,size,mat)
 part("glass",0,0,2.2,1.06,.045,1.0,glass)
 for u in [-.59,.59]: part("jamb",u,.025,2.2,.11,.18,1.26,trim)
 for z in [1.61,2.79]: part("frame",0,.025,z,1.28,.18,.11,trim)
 part("mullion",0,.07,2.2,.045,.16,1.1,trim)
 part("crossbar",0,.07,2.2,1.1,.16,.045,trim)
 part("sill",0,.04,1.55,1.4,.32,.12,woods[5])
window("Front left window",(-1.95,-4.1))
window("Front right window",(1.95,-4.1))
window("Right side window",(3.10,0),True)
# Exactly four full-height square porch posts and functional braces.
for i,x in enumerate([-2.85,-1.0,1.0,2.85]):
 box("Porch square post %d"%(i+1),(x,-5.85,1.98),(.18,.18,2.76),trim)
 for sign in [-1,1]:
  beam("Porch knee brace",(x,-5.85,2.8),(x+sign*.36,-5.85,3.38),.09,.10,woods[3])
box("Porch front header",(0,-5.85,3.42),(6.25,.20,.23),trim)
box("Porch wall ledger",(0,-4.05,3.91),(6.25,.2,.18),trim)
for x in [-2.95,-1.5,0,1.5,2.95]:
 beam("Porch rafter",(x,-6.15,3.48),(x,-3.85,4.03),.12,.16,woods[2])
for i in range(3):
 box("Entrance step %d"%(i+1),(0,-6.9+i*.30,.10+i*.17),(1.7,.34,.12),woods[4])
for x in [-.65,.65]: beam("Stair stringer",(x,-7.05,.04),(x,-6.05,.49),.10,.16,woods[0])
print("Structure created",len(asset.objects))
