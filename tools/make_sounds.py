# Generates the game's sound effects as 16-bit mono WAV files (no external assets, no licence issues).
import math, struct, wave, random, os
SR = 22050
out = "Resources/Sounds"
os.makedirs(out, exist_ok=True)

def tone(freq, dur, vol=0.5, kind="sine", decay=4.0):
    n = int(SR*dur); s = []
    for i in range(n):
        t = i/SR
        ph = 2*math.pi*freq*t
        v = math.sin(ph) if kind=="sine" else (1 if math.sin(ph)>=0 else -1)*0.6 if kind=="square" else 2*((freq*t)%1)-1
        env = math.exp(-decay*t/dur) * min(1, i/(SR*0.005))
        s.append(v*env*vol)
    return s

def noise(dur, vol=0.4, decay=3.0):
    n = int(SR*dur)
    return [(random.uniform(-1,1))*math.exp(-decay*i/n)*vol for i in range(n)]

def seq(*parts): 
    r=[]
    for p in parts: r+=p
    return r

def mix(a,b):
    n=max(len(a),len(b)); a=a+[0]*(n-len(a)); b=b+[0]*(n-len(b))
    return [x+y for x,y in zip(a,b)]

def save(name, samples):
    m = max(1e-9, max(abs(x) for x in samples))
    k = min(1.0, 0.9/m)
    with wave.open(f"{out}/{name}.wav","w") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(x*k*32767)) for x in samples))

random.seed(7)
save("click",   tone(880,0.06,0.5,"sine",6))
save("back",    tone(520,0.08,0.5,"sine",6))
save("buy",     seq(tone(660,0.09,0.5), tone(880,0.09,0.5), tone(1320,0.18,0.5)))
save("error",   seq(tone(220,0.12,0.5,"square",3), tone(165,0.2,0.5,"square",3)))
save("reveal_bronze", tone(440,0.25,0.5,"sine",5))
save("reveal_silver", seq(tone(523,0.12,0.5), tone(784,0.25,0.5)))
save("reveal_gold",   seq(tone(523,0.1,0.5), tone(659,0.1,0.5), tone(784,0.1,0.5), tone(1047,0.35,0.55)))
save("reveal_elite",  mix(seq(tone(523,0.09), tone(659,0.09), tone(784,0.09), tone(1047,0.09), tone(1319,0.09), tone(1568,0.6,0.6,"sine",3)),
                          seq([0]*int(SR*0.45), noise(0.5,0.12,4))))
save("pack_shake", seq(*[noise(0.05,0.35,2) + [0]*int(SR*0.03) for _ in range(8)]))
save("whistle", mix(tone(2800,0.5,0.4,"sine",1.5), tone(3000,0.5,0.3,"sine",1.5)))
save("goal", mix(noise(1.4,0.35,1.2), seq(tone(392,0.15,0.4), tone(523,0.15,0.4), tone(659,0.15,0.4), tone(784,0.7,0.5,"sine",2.5))))
save("win",  seq(tone(523,0.15,0.5), tone(659,0.15,0.5), tone(784,0.15,0.5), tone(1047,0.5,0.55,"sine",2)))
save("lose", seq(tone(392,0.2,0.5), tone(330,0.2,0.5), tone(262,0.5,0.5,"sine",2)))
save("reward", seq(tone(988,0.07,0.5), tone(1319,0.25,0.5)))
# soft looping menu music: slow chord pads
pad=[]
for chord in [(262,330,392),(220,262,330),(175,220,262),(196,247,294)]:
    seg=[0]*int(SR*2.0)
    for f in chord:
        seg=mix(seg,[0.18*math.sin(2*math.pi*f*i/SR)*(0.5-0.5*math.cos(2*math.pi*i/(SR*2.0))) for i in range(int(SR*2.0))])
    pad+=seg
save("music_menu", pad)
print(sorted(os.listdir(out)))
