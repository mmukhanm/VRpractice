"""Blender: derive a 2–5k-triangle workload from the actual CC0 chair of practice 6.
No other source meshes, textures, or dependency files are overwritten.
"""
import bpy
import hashlib
import json
from pathlib import Path
import sys

root=Path(sys.argv[sys.argv.index('--')+1]).resolve()
source=root/'Assets/Models/Chair/Chair.obj'
target=root/'Assets/Models/Chair/Chair_Profiling.obj'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.wm.obj_import(filepath=str(source),forward_axis='NEGATIVE_Z',up_axis='Y')
for obj in bpy.context.scene.objects:
    if obj.type!='MESH':continue
    bpy.context.view_layer.objects.active=obj
    modifier=obj.modifiers.new('Profiling simplification','DECIMATE');modifier.ratio=.075
    bpy.ops.object.modifier_apply(modifier=modifier.name)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.wm.obj_export(filepath=str(target),forward_axis='NEGATIVE_Z',up_axis='Y',export_selected_objects=True,export_materials=False,export_triangulated_mesh=True)
triangles=sum(1 for line in target.read_text().splitlines() if line.startswith('f '))
assert 2000<=triangles<=5000,triangles
metadata={'source':str(source.relative_to(root)),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
          'file':str(target.relative_to(root)),'triangles':triangles,'decimate_ratio':.075,
          'author':'Eric Chadwick / Wayfair, LLC','license':'CC0-1.0',
          'stress_mesh_longest_edge_m':.35,
          'note':'Editor combines this derived chair into a single-renderer workload, with one shared material and 0.35 m longest edge. Original practice-6 scene is unchanged.'}
(root/'Docs/profiling-model.json').write_text(json.dumps(metadata,indent=2)+'\n')
print('PASS: profiling chair exported,',triangles,'triangles')
