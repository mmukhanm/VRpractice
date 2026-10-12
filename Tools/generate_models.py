"""Blender 4.3: create original metre-scale workshop assets and export imports.
Run: blender -b --python Tools/generate_models.py -- <project root>
Original models are CC0; Chair is derived from Eric Chadwick / Wayfair CC0.
"""
import bpy
import json
import math
import sys
from pathlib import Path
from mathutils import Vector

ROOT = Path(sys.argv[sys.argv.index('--') + 1]).resolve()
MODELS = ROOT / 'Assets/Models'
STATS = []

def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)

def material(name, rgb, metallic=0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = .35 if metallic else .6
    return m

steel = material('Steel', (.18, .23, .27), .75)
yellow = material('SafetyYellow', (.95, .55, .055), .15)
rubber = material('Rubber', (.025, .032, .04))
red = material('RedPaint', (.65, .055, .035), .4)
blue = material('BluePaint', (.035, .22, .38), .3)
glass = material('GlassBlue', (.12, .35, .46), .2)
white = material('WarmWhite', (.78, .8, .76))

def box(name, xyz, size, mat=steel, bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=1, location=xyz)
    o = bpy.context.object
    o.name = name
    o.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(mat)
    if bevel:
        b = o.modifiers.new('Edge bevel', 'BEVEL'); b.width = bevel; b.segments = 2
        bpy.ops.object.modifier_apply(modifier=b.name)
    return o

def cylinder(name, xyz, radius, depth, mat=steel, rotate=(0,0,0), vertices=24):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=xyz, rotation=rotate)
    o=bpy.context.object; o.name=name; o.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return o

def export(name, fmt, animation=False, filename=None):
    directory=MODELS/name; directory.mkdir(parents=True,exist_ok=True)
    path=directory/(filename or (name+'.'+fmt))
    bpy.ops.object.select_all(action='SELECT')
    if fmt=='fbx':
        bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, axis_forward='-Z', axis_up='Y', global_scale=1, apply_unit_scale=True, bake_anim=animation, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0, add_leaf_bones=False, path_mode='AUTO')
    elif fmt=='obj':
        bpy.ops.wm.obj_export(filepath=str(path), forward_axis='NEGATIVE_Z', up_axis='Y', export_selected_objects=True, export_materials=True, path_mode='RELATIVE', export_triangulated_mesh=True)
    else:
        bpy.ops.export_scene.gltf(filepath=str(path), export_format='GLB', use_selection=True, export_animations=animation, export_yup=True)
    points=[o.matrix_world@Vector(v) for o in bpy.context.scene.objects if o.type=='MESH' for v in o.bound_box]
    bounds=[round(max(v[i] for v in points)-min(v[i] for v in points),4) for i in range(3)]
    tris=0
    for o in bpy.context.scene.objects:
        if o.type=='MESH':
            o.data.calc_loop_triangles(); tris+=len(o.data.loop_triangles)
    STATS.append(dict(name=name,file=str(path.relative_to(ROOT)),format=fmt,dimensions_blender_xyz=bounds,dimensions_unity_xyz=[bounds[0],bounds[2],bounds[1]],triangles=tris,materials=len({m.name for o in bpy.context.scene.objects if o.type=='MESH' for m in o.data.materials if m}),scale_factor=1))

clear()
box('WorkbenchTop',(0,0,.85),(1.8,.75,.1),yellow)
for x in [-.78,.78]:
    for y in [-.27,.27]: box('Leg',(x,y,.4),(.075,.075,.8))
box('Shelf',(0,0,.25),(1.65,.6,.05))
export('Table','fbx')

clear()
bpy.ops.import_scene.gltf(filepath=str(ROOT/'Tools/SourceAssets/Chair/SheenChair.glb'))
texture_dir=MODELS/'Chair/Textures';texture_dir.mkdir(parents=True,exist_ok=True)
for image in list(bpy.data.images):
    if image.type=='IMAGE' and image.size[0]>0:
        # Embedded glTF images load lazily; has_data is false before pixel access.
        _=image.pixels[0]
        if image.name=='Image_0': image.name='Chair_Normal'
        if max(image.size)>1024:
            ratio=1024/max(image.size);image.scale(round(image.size[0]*ratio),round(image.size[1]*ratio))
        image.filepath_raw=str(texture_dir/(image.name.replace('/','_')+'.png'))
        image.file_format='PNG';image.save()
        if image.packed_file: image.unpack(method='REMOVE')
# Normalize in the source mesh/export, never by scaling a placed Unity object.
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
# Flatten glTF-only sheen/variant node graphs to standard OBJ-compatible materials.
for m in {m for o in meshes for m in o.data.materials if m}:
    kind='fabric' if m.name.startswith('fabric') else 'wood' if m.name.startswith('wood') else 'label' if m.name=='label' else 'metal'
    base={'fabric':(.883,.035,0),'wood':(.14,.07,.01),'label':(1,1,1),'metal':(.28,.26,.23)}[kind]
    m.diffuse_color=(*base,1);m.use_nodes=True;m.node_tree.nodes.clear()
    out=m.node_tree.nodes.new('ShaderNodeOutputMaterial');bsdf=m.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
    bsdf.inputs['Base Color'].default_value=(*base,1);m.node_tree.links.new(bsdf.outputs['BSDF'],out.inputs['Surface'])
    index={'fabric':'Image_2','wood':'Image_3','label':'Image_5'}.get(kind)
    if index:
        tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images[index]
        m.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
    if kind=='fabric':
        tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images['Chair_Normal'];tex.image.colorspace_settings.name='Non-Color'
        normal=m.node_tree.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.6
        m.node_tree.links.new(tex.outputs['Color'],normal.inputs['Color']);m.node_tree.links.new(normal.outputs['Normal'],bsdf.inputs['Normal'])
points=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
height=max(v.z for v in points)-min(v.z for v in points)
factor=.90/height
minz=min(v.z for v in points); centerx=(max(v.x for v in points)+min(v.x for v in points))/2; centery=(max(v.y for v in points)+min(v.y for v in points))/2
for o in meshes:
    world=o.matrix_world.copy(); o.parent=None; o.matrix_world=world
    for v in o.data.vertices:
        p=world@v.co; v.co=(p-Vector((centerx,centery,minz)))*factor
    o.matrix_world.identity()
export('Chair','obj')
for o in meshes:
    m=o.modifiers.new('Half detail','DECIMATE');m.ratio=.5
    bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=m.name)
export('Chair','obj',filename='Chair_LOD1.obj')

clear()
cylinder('Base',(0,0,.025),.13,.05)
cylinder('Stem',(0,0,.26),.018,.45)
bpy.ops.mesh.primitive_cone_add(vertices=32,radius1=.16,radius2=.07,depth=.18,location=(0,0,.55))
bpy.context.object.data.materials.append(yellow)
export('Lamp','glb')

clear()
box('Radio',(0,0,.13),(.36,.15,.26),blue)
for x in [-.1,.1]: cylinder('Speaker',(x,-.079,.14),.063,.008,rubber,(math.pi/2,0,0))
box('Handle',(0,0,.30),(.2,.035,.025),yellow)
cylinder('TuningKnob',(.12,-.09,.235),.015,.015,yellow,(math.pi/2,0,0))
export('Radio','fbx')

clear()
cylinder('AirTank',(0,0,.43),.22,.9,red,(0,math.pi/2,0),32)
box('Motor',(0,0,.75),(.38,.28,.26))
for x in [-.32,.32]:
    cylinder('Wheel',(x,0,.13),.13,.08,rubber,(math.pi/2,0,0))
export('Compressor','glb')

clear()
box('Chassis',(0,0,.62),(1.78,4.05,.6),blue,.12)
box('Cabin',(0,.08,1.12),(1.53,1.9,.66),glass,.15)
box('Hood',(0,-1.38,.91),(1.68,1.05,.1),blue,.05)
for x in [-.9,.9]:
    for y in [-1.25,1.25]:
        cylinder('Tyre',(x,y,.35),.35,.22,rubber,(0,math.pi/2,0),40)
        cylinder('Hub',(x*1.1,y,.35),.21,.025,steel,(0,math.pi/2,0))
for x in [-.6,.6]: box('Headlight',(x,-2.035,.73),(.3,.04,.14),white)
export('Car','fbx')

clear()
box('Handle',(0,0,.013),(.045,.24,.026),steel,.006)
# Open jaw, not a solid block; ideal for demonstrating impact pitch and volume.
box('JawLeft',(-.038,.14,.013),(.028,.10,.026),steel,.004)
box('JawRight',(.038,.14,.013),(.028,.10,.026),steel,.004)
box('JawBase',(0,.11,.013),(.075,.035,.026),steel,.004)
cylinder('Ring',(0,-.15,.013),.035,.026,steel,vertices=12)
export('Wrench','fbx')

clear()
box('FanBase',(0,0,.04),(.5,.4,.08),yellow)
cylinder('FanStand',(0,0,.43),.045,.74)
cylinder('Housing',(0,0,.90),.28,.13,steel,(math.pi/2,0,0),48)
hub=cylinder('Rotor',(0,-.075,.90),.06,.035,yellow,(math.pi/2,0,0))
for angle in [0,2*math.pi/3,4*math.pi/3]:
    blade=box('Blade',(0,0,0),(.09,.025,.19),yellow,.025)
    blade.parent=hub; blade.location=(math.sin(angle)*.14,math.cos(angle)*.14,0);blade.rotation_euler.z=-angle
hub.rotation_mode='XYZ'
bpy.context.scene.frame_start=1;bpy.context.scene.frame_end=61
hub.rotation_euler.y=0;hub.keyframe_insert(data_path='rotation_euler',frame=1)
hub.rotation_euler.y=2*math.pi;hub.keyframe_insert(data_path='rotation_euler',frame=61)
for fc in hub.animation_data.action.fcurves:
    for k in fc.keyframe_points:k.interpolation='LINEAR'
export('Fan','fbx',animation=True)

clear()
box('DroneBody',(0,0,.09),(.35,.22,.18),yellow)
for x in [-.28,.28]:
    for y in [-.22,.22]:
        cylinder('Rotor',(x,y,.17),.13,.02,steel)
        box('Arm',(x*.5,y*.5,.10),(.3,.04,.04))
export('Drone','glb')

(ROOT/'Docs/model_inventory.json').write_text(json.dumps(STATS,indent=2),encoding='utf8')
print('EXPORTED',len(STATS),'model assets')
