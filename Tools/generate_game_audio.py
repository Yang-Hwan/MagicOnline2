"""Generate the game's original synthesized sound bank. Requires numpy, no downloads."""
from pathlib import Path
import uuid
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Resources/Audio'
RATE = 22050
rng = np.random.default_rng(1026)


def tone(hz, duration, decay=7, brightness=.22):
    t = np.arange(int(RATE * duration)) / RATE
    envelope = np.minimum(t / .006, 1) * np.exp(-decay * t)
    envelope *= np.minimum((duration - t) / .015, 1)
    return envelope * (np.sin(2 * np.pi * hz * t) + brightness * np.sin(2 * np.pi * hz * 2.01 * t))


def phrase(notes, step=.12, duration=.9):
    data = np.zeros(int(RATE * duration))
    for i, note in enumerate(notes):
        add(data, tone(note, min(.65, duration-i*step), 7), i*step, .32)
    return data


def add(target, sound, offset, gain=1):
    start = int(offset * RATE)
    count = min(len(sound), len(target)-start)
    if count > 0:
        target[start:start+count] += sound[:count] * gain


def meta(path, body):
    target = Path(str(path)+'.meta')
    if not target.exists():
        target.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+body, encoding='utf-8')


def save(name, samples, music=False):
    path = OUT / (name+'.wav')
    peak = np.max(np.abs(samples))
    samples *= (.34 if music else .72) / max(peak, .01)
    with wave.open(str(path), 'wb') as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(RATE)
        stream.writeframes((samples * 32767).astype('<i2').tobytes())
    meta(path, '''AudioImporter:
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 22050
    compressionFormat: 1
    quality: 0.7
    conversionMode: 0
  platformSettingOverrides: {}
  forceToMono: 1
  normalize: 0
  preloadAudioData: 1
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
''')


def impact(hz, duration, decay, noise):
    t = np.arange(int(RATE*duration))/RATE
    data = tone(hz, duration, decay, .15)
    data += rng.uniform(-1, 1, len(t)) * np.exp(-t*150) * noise
    return data


def music(name, bpm, chords, soft):
    beat = 60/bpm
    length = beat*32
    # Render two cycles and fold the tail to make a seamless loop.
    data = np.zeros(int(RATE*length)+RATE*3)
    for bar in range(8):
        chord = chords[bar % len(chords)]
        for note in chord:
            add(data, tone(note, beat*3.9, 1.5, .08), bar*beat*4, .065)
        for step in range(8):
            note = chord[(step+bar) % 3]*2
            add(data, tone(note, beat*.8, 8, .1), (bar*4+step*.5)*beat, .035 if soft else .07)
        for step in range(4):
            offset = (bar*4+step)*beat
            add(data, tone(chord[0]/2, beat*.75, 7, .15), offset, .17)
            if not soft or step % 2 == 0:
                t = np.arange(int(RATE*.18))/RATE
                kick = np.sin(2*np.pi*(48*t+1.6*(1-np.exp(-t*35))))*np.exp(-t*25)
                add(data, kick, offset, .17 if soft else .24)
            if step % 2:
                snare = impact(170, .12, 32, .6)
                add(data, snare, offset, .035 if soft else .08)
            hat = rng.uniform(-1, 1, int(RATE*.035))*np.exp(-np.arange(int(RATE*.035))/RATE*130)
            add(data, hat, offset+beat*.5, .012 if soft else .025)
    count = int(length*RATE)
    data[:len(data)-count] += data[count:]
    save(name, data[:count], True)


OUT.mkdir(parents=True, exist_ok=True)
meta(OUT, 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
notes = {
    'Click': [750], 'Open': [440, 660], 'Close': [660, 440], 'Error': [220, 185],
    'Notify': [660, 880], 'Request': [587, 784, 988], 'Accept': [523, 784],
    'Cancel': [392, 294], 'Chat': [880], 'Aim': [620], 'Turn': [440, 660, 880],
    'Start': [392, 523, 784], 'Timeout': [330, 247, 165], 'FinishReady': [523, 659, 988],
    'Score': [659, 988], 'Combo': [784, 988, 1318], 'Foul': [220, 155],
    'Victory': [523, 659, 784, 1046], 'Defeat': [392, 330, 261, 196],
    'Draw': [392, 523, 392], 'Count': [1100], 'CountDone': [784, 1046, 1568],
    'Coin': [1700, 2300], 'LevelUp': [523, 659, 784, 1046, 1318, 1568],
    'Reward': [659, 880, 1318], 'Attendance': [523, 784], 'Equip': [440, 880],
}
for name, sequence in notes.items():
    brief = name in ('Click', 'Count', 'Chat', 'Aim')
    step = .16 if name in ('Victory', 'Defeat', 'LevelUp') else .075
    save(name, phrase(sequence, step, .09 if brief else max(.45, len(sequence)*step+.4)))
save('Strike', impact(850, .16, 55, .9))
save('Pull', impact(120, .12, 25, .08))
save('Spark', impact(1900, .3, 16, .8))
save('Pulse', phrase([196, 392, 784], .04, .3))
save('Flame', impact(90, .3, 12, .9))
for variant in range(1, 4):
    save('Ball_'+str(variant), impact(1700+variant*140, .12, 65, .5))
    save('Cushion_'+str(variant), impact(140+variant*25, .18, 32, .16))
emotions = {
    'Anger': [155, 110], 'Encourage': [392, 440, 523], 'Disappoint': [523, 440, 330],
    'Grumble': [247, 233], 'Despair': [330, 247, 165], 'Joy': [659, 784, 1046],
    'Thanks': [523, 659, 784], 'Relief': [440, 392, 523],
}
for name, sequence in emotions.items():
    for variant in range(1, 4):
        save(name+'_'+str(variant), phrase([n*2**((variant-2)/12) for n in sequence], .10, .65))
music('Lobby', 96, [(130.81, 164.81, 196), (110, 130.81, 164.81), (174.61, 220, 261.63), (146.83, 196, 246.94)], False)
music('Lounge', 80, [(130.81, 155.56, 196), (116.54, 146.83, 174.61), (103.83, 130.81, 155.56), (98, 123.47, 146.83)], True)
music('Match', 88, [(110, 130.81, 164.81), (98, 123.47, 146.83), (87.31, 110, 130.81), (98, 130.81, 164.81)], True)
print(f'Generated {len(list(OUT.glob("*.wav")))} original clips in {OUT}')
