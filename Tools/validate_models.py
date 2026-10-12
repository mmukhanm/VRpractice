"""Blender round-trip import validation. Run with --python-exit-code 1."""
import bpy
import json
from pathlib import Path
import sys
from mathutils import Vector

ROOT=Path(sys.argv[sys.argv.index('--')+1]).resolve()
models=json.loads((ROOT/'Docs/model_inventory.json').read_text())
for model in models:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    path=str(ROOT/model['file'])
    if model['format']=='fbx':bpy.ops.import_scene.fbx(filepath=path)
    elif model['format']=='obj':bpy.ops.wm.obj_import(filepath=path,forward_axis='NEGATIVE_Z',up_axis='Y')
    else:bpy.ops.import_scene.gltf(filepath=path)
    bpy.context.scene.frame_set(1)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    assert meshes,model['file']+' has no imported meshes'
    points=[o.matrix_world@Vector(v) for o in meshes for v in o.bound_box]
    size=[max(v[i] for v in points)-min(v[i] for v in points) for i in range(3)]
    expected=model['dimensions_blender_xyz']
    assert all(abs(a-b)<max(.003,b*.02) for a,b in zip(size,expected)),(model['file'],size,expected)
    if model['name']=='Fan':
        animated=[o for o in bpy.context.scene.objects if o.animation_data and o.animation_data.action]
        assert animated,'FBX fan lost animation'
        # FBX importer adds a frame offset; use the imported action's actual range.
        first,last=animated[0].animation_data.action.frame_range
        bpy.context.scene.frame_set(round(first))
        start={o.name:o.matrix_world.copy() for o in meshes}
        bpy.context.scene.frame_set(round(last))
        for o in meshes:
            assert max(abs(o.matrix_world[i][j]-start[o.name][i][j]) for i in range(4) for j in range(4))<.01,'Fan loop seam: '+o.name
    print('PASS round-trip:',model['file'],[round(v,4) for v in size])
print('PASS: all 10 model imports; metre-scale bounds; fan loop endpoints.')
