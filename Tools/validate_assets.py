"""Useful source/asset checks without pretending to run Unity. Standard library only."""
import hashlib
import json
from pathlib import Path
import struct
import wave

ROOT=Path(__file__).resolve().parents[1]
checks=0
def check(ok,message):
    global checks
    if not ok: raise AssertionError(message)
    checks+=1

inventory=json.loads((ROOT/'Docs/model_inventory.json').read_text())
check(len({r['name'] for r in inventory})>=6,'six distinct imported models required')
check({r['format'] for r in inventory}=={'obj','fbx','glb'},'all three formats required')
for model in inventory:
    p=ROOT/model['file'];check(p.is_file(),str(p))
    b=p.read_bytes()
    if p.suffix=='.fbx':check(b.startswith(b'Kaydara FBX Binary'),str(p)+' invalid FBX signature')
    elif p.suffix=='.obj':
        lines=b.decode().splitlines()
        vertices=[l for l in lines if l.startswith('v ')]
        faces=[l.split()[1:] for l in lines if l.startswith('f ')]
        check(len(faces)==model['triangles'],str(p)+' triangle count mismatch')
        check(all(len(f)==3 and all(1<=int(v.split('/')[0])<=len(vertices) for v in f) for f in faces),'OBJ indices/triangulation invalid')
    else:
        magic,version,length=struct.unpack_from('<III',b)
        check(magic==0x46546c67 and version==2 and length==len(b),str(p)+' invalid GLB header')
        jl,jt=struct.unpack_from('<II',b,12);check(jt==0x4e4f534a,'missing GLB JSON')
        j=json.loads(b[20:20+jl]);check(bool(j.get('meshes')),str(p)+' no meshes')
        check(all('uri' not in buffer for buffer in j.get('buffers',[])),str(p)+' external buffer')
        triangles=sum(j['accessors'][primitive['indices']]['count']//3 for mesh in j['meshes'] for primitive in mesh['primitives'])
        check(triangles>=model['triangles'],str(p)+' triangle count smaller than source')
lod0=next(m for m in inventory if m['file'].endswith('/Chair.obj'))
lod1=next(m for m in inventory if m['file'].endswith('/Chair_LOD1.obj'))
check(.45<lod1['triangles']/lod0['triangles']<.55,'LOD1 must be about half detail')
for p in (ROOT/'Assets/Models/Chair').glob('*.mtl'):
    for line in p.read_text().splitlines():
        if line.startswith(('map_','bump ')):
            check((p.parent/line.split()[-1]).is_file(),'MTL texture missing: '+line)

credits=(ROOT/'CREDITS.txt').read_text()
wav_paths=list((ROOT/'Assets/Audio').rglob('*.wav'))
check(len(wav_paths)>=5,'five audio assets required')
for p in wav_paths:
    with wave.open(str(p)) as w:
        check(w.getnchannels()==1 and w.getsampwidth()==2 and w.getframerate()==44100,str(p)+' unsupported WAV format')
        check(w.getnframes()>1000,str(p)+' clip too short')
        data=w.readframes(w.getnframes());check(any(data),str(p)+' silent clip')
    check(str(p.relative_to(ROOT)) in credits,str(p)+' missing credit')
for p in (ROOT/'Assets/Models/Chair/Textures').glob('*.png'):
    b=p.read_bytes();check(b[:8]==b'\x89PNG\r\n\x1a\n','invalid PNG')
    width,height=struct.unpack_from('>II',b,16);check(max(width,height)<=1024,'oversized texture')
source=ROOT/'Tools/SourceAssets/Chair/SheenChair.glb'
check(hashlib.sha256(source.read_bytes()).hexdigest()=='f0af2a2b102d28d540236306ae19f8fb36842df76bd38cf76f063f9bd2853399','source model changed')
for p in [ROOT/'Packages/manifest.json',*ROOT.glob('Assets/**/*.asmdef')]:json.loads(p.read_text())
check((ROOT/'Assets/Audio/MainMixer.mixer').read_text().count('AudioMixerSnapshotController:')==2,'two snapshots required')
print(f'PASS: {checks} asset integrity checks; {len(inventory)} model imports, {len(wav_paths)} WAV clips.')
print('Not a Unity compile, scene, performance, or listening test.')
