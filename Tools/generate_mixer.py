"""Write a standard Unity AudioMixer asset with exposed sliders and ducking buses."""
from pathlib import Path
import hashlib

ROOT=Path(__file__).resolve().parents[1]
def guid(s): return hashlib.md5(('VRPractice/'+s).encode()).hexdigest()

groups=['Master','Music','SFXDuck','SFX','AmbientDuck','Ambient']
ids={n:24300000+i for i,n in enumerate(groups)}
children={'Master':['Music','SFXDuck','AmbientDuck'],'SFXDuck':['SFX'],'AmbientDuck':['Ambient']}
common='  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
s='%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!241 &24100000\nAudioMixerController:\n'+common
s+='  m_Name: MainMixer\n  m_OutputGroup: {fileID: 0}\n  m_MasterGroup: {fileID: 24300000}\n  m_Snapshots:\n  - {fileID: 24500000}\n  - {fileID: 24500001}\n  m_StartSnapshot: {fileID: 24500000}\n  m_SuspendThreshold: -80\n  m_EnableSuspend: 1\n  m_UpdateMode: 0\n  m_ExposedParameters:\n'
for n in ['Music','SFX','Ambient']:s+=f'  - guid: {guid(n+"Volume")}\n    name: {n}Volume\n'
s+='  m_AudioMixerGroupViews:\n  - guids:\n'
for n in groups:s+=f'    - {guid(n+"Group")}\n'
s+='    name: All groups\n  m_CurrentViewIndex: 0\n  m_TargetSnapshot: {fileID: 24500000}\n'
for i,n in enumerate(groups):
    s+=f'--- !u!243 &{ids[n]}\nAudioMixerGroupController:\n'+common+f'  m_Name: {n}\n  m_AudioMixer: {{fileID: 24100000}}\n  m_GroupID: {guid(n+"Group")}\n'
    ch=children.get(n,[])
    s+='  m_Children:'+ ('\n'+''.join(f'  - {{fileID: {ids[c]}}}\n' for c in ch) if ch else ' []\n')
    s+=f'  m_Volume: {guid(n+"Volume")}\n  m_Pitch: {guid(n+"Pitch")}\n  m_Send: {guid(n+"Send")}\n  m_Effects:\n  - {{fileID: {24400000+i}}}\n  m_UserColorIndex: {i}\n  m_Mute: 0\n  m_Solo: 0\n  m_BypassEffects: 0\n'
    s+=f'--- !u!244 &{24400000+i}\nAudioMixerEffectController:\n'+common+f'  m_Name: \n  m_EffectID: {guid(n+"Attenuation")}\n  m_EffectName: Attenuation\n  m_MixLevel: {guid(n+"Mix")}\n  m_Parameters: []\n  m_SendTarget: {{fileID: 0}}\n  m_EnableWetMix: 0\n  m_Bypass: 0\n'
for i,n in enumerate(['Normal','Paused']):
    s+=f'--- !u!245 &{24500000+i}\nAudioMixerSnapshotController:\n'+common+f'  m_Name: {n}\n  m_AudioMixer: {{fileID: 24100000}}\n  m_SnapshotID: {guid(n+"Snapshot")}\n  m_FloatValues:\n'
    for group in groups:
        volume=-18 if n=='Paused' and group in ['SFXDuck','AmbientDuck'] else 0
        s+=f'    {guid(group+"Volume")}: {volume}\n    {guid(group+"Pitch")}: 1\n'
    s+='  m_TransitionOverrides: {}\n'
p=ROOT/'Assets/Audio/MainMixer.mixer';p.write_text(s)
print('MainMixer: 3 exposed groups, separate snapshot ducking, Normal/Paused')
