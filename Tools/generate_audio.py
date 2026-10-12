"""Create deterministic original CC0 sound assets. No unlicensed samples."""
from pathlib import Path
import math
import random
import struct
import wave

ROOT=Path(__file__).resolve().parents[1]
RATE=44100
rng=random.Random(6)

def save(folder,name,seconds,signal):
    p=ROOT/'Assets/Audio'/folder/(name+'.wav'); p.parent.mkdir(parents=True,exist_ok=True)
    with wave.open(str(p),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE)
        w.writeframes(b''.join(struct.pack('<h',int(max(-1,min(1,signal(i/RATE,seconds)))*26000)) for i in range(int(seconds*RATE))))

def music(t,d):
    # Four bars in A minor; all oscillators have integral cycles over the loop.
    notes=[220,262,330,294,220,330,392,330]
    phase=t%1; f=notes[int(t)%len(notes)]
    return .18*math.sin(2*math.pi*f*t)*math.sin(math.pi*phase)**2+.10*math.sin(2*math.pi*110*t)

save('Music','WorkshopMusic',8,music)
save('Ambient','RadioLoop',8,lambda t,d:.65*music(t,d))
save('Ambient','FanLoop',2,lambda t,d:.16*math.sin(2*math.pi*90*t)+.08*math.sin(2*math.pi*180*t)*(1+.3*math.sin(2*math.pi*8*t)))
save('Ambient','MotorLoop',2,lambda t,d:.15*math.sin(2*math.pi*110*t)+.09*math.sin(2*math.pi*220*t)+.06*math.sin(2*math.pi*440*t))
save('Ambient','EngineLoop',2,lambda t,d:.22*math.sin(2*math.pi*45*t)*(1+.5*math.sin(2*math.pi*12*t))+.07*math.sin(2*math.pi*135*t))
save('Ambient','CompressorLoop',2,lambda t,d:.12*math.sin(2*math.pi*60*t)+.07*math.sin(2*math.pi*300*t))
for n,f in enumerate([660,790,920],1):
    save('SFX','Impact'+str(n),.32,lambda t,d,f=f:(.55*math.sin(2*math.pi*f*t)+.2*rng.uniform(-1,1))*math.exp(-t*24)*min(1,t*1000))
save('SFX','Grab',.12,lambda t,d:.32*math.sin(2*math.pi*880*t)*math.sin(math.pi*t/d)**2)
save('SFX','Release',.16,lambda t,d:.25*math.sin(2*math.pi*440*t)*math.sin(math.pi*t/d)**2)
save('SFX','Switch',.08,lambda t,d:.3*math.sin(2*math.pi*1200*t)*math.sin(math.pi*t/d)**2)
print('Generated 12 mono WAV files (44.1 kHz PCM16)')
