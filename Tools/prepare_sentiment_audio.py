"""Build the 12 actor-specific emotion soundtracks (numpy/scipy; no downloads).

Physical/animal/voice sounds use the bundled CC0 recordings, except s5_5 and s5_9,
which use the user's supplied video recordings. Summoning and beautiful
chimes are original sound designs.
"""
from pathlib import Path
import json
import uuid
import wave

import numpy as np
from scipy.signal import butter, sosfilt, sosfiltfilt, resample_poly

ROOT = Path(__file__).resolve().parents[1]
SOURCES = ROOT / 'Tools/AudioSources/Sentiments'
OUT = ROOT / 'Assets/Resources/Audio/Sentiments'
RATE = 22050
rng = np.random.default_rng(20261127)


def read(name):
    with wave.open(str(SOURCES / (name + '.wav')), 'rb') as stream:
        assert stream.getframerate() == RATE and stream.getnchannels() == 1
        return np.frombuffer(stream.readframes(stream.getnframes()), '<i2').astype(float) / 32768


def fade(x, attack=.004, release=.04):
    x = x.copy()
    a = min(len(x), max(2, round(attack * RATE)))
    b = min(len(x), max(2, round(release * RATE)))
    x[:a] *= np.linspace(0, 1, a)
    x[-b:] *= np.linspace(1, 0, b)
    return x


def normal(x, peak=.7):
    return x * peak / max(np.max(np.abs(x)), 1e-8)


def cut(name, start, end, length=None, trim=False):
    x = read(name)[round(start * RATE):round(end * RATE)].copy()
    if trim:
        onset = np.flatnonzero(np.abs(x) > np.max(np.abs(x)) * .09)[0]
        x = x[max(0, onset - round(.001 * RATE)):]
    if length:
        x = x[:round(length * RATE)]
    x = sosfilt(butter(2, 80, btype='highpass', fs=RATE, output='sos'), x)
    return normal(fade(x, .001, .05))


def mix(destination, sound, time, gain=1):
    start = round(time * RATE)
    count = min(len(sound), len(destination) - start)
    if count > 0:
        destination[start:start + count] += sound[:count] * gain


def tone(frequency, length, decay=3, attack=.015):
    t = np.arange(round(length * RATE)) / RATE
    x = (np.sin(2 * np.pi * frequency * t) + .22 * np.sin(2 * np.pi * frequency * 2.001 * t)
         + .07 * np.sin(2 * np.pi * frequency * 3.997 * t))
    return fade(x * np.minimum(t / attack, 1) * np.exp(-decay * t), .002, .1)


def heavy_explosion():
    # Recorded blast supplies the initial detonation; the larger explosion adds
    # a sustained pressure/debris tail. No distortion or synthetic sine thump.
    blast = read('bomb_blast')
    onset = np.flatnonzero(np.abs(blast) > np.max(np.abs(blast)) * .16)[0]
    blast = blast[max(0, onset - round(.003 * RATE)):][:round(.9 * RATE)]
    blast = sosfilt(butter(3, [70, 3800], btype='bandpass', fs=RATE, output='sos'), blast)
    blast = normal(fade(blast, .002, .16), .7)
    body = read('bomb_heavy')[:round(2.35 * RATE)]
    body = sosfilt(butter(3, [45, 2300], btype='bandpass', fs=RATE, output='sos'), body)
    body = normal(fade(body, .003, .6), .7)
    sound = np.zeros(round(2.4 * RATE))
    mix(sound, blast, 0, .95)
    mix(sound, body, .005, .7)
    return fade(sound, .001, .12)


def heavy_wall_hit(start, end):
    sound = read('hammer_heavy')[round(start * RATE):round(end * RATE)].copy()
    onset = np.flatnonzero(np.abs(sound) > np.max(np.abs(sound)) * .12)[0]
    sound = sound[max(0, onset - round(.002 * RATE)):]
    # Slightly lower the recorded sledgehammer's pitch, retaining its masonry crack.
    sound = resample_poly(sound, 9, 8)[:round(.54 * RATE)]
    sound = sosfilt(butter(3, [45, 5500], btype='bandpass', fs=RATE, output='sos'), sound)
    sound = normal(fade(sound, .0015, .09), .7)
    result = np.zeros(round(.63 * RATE))
    for delay, gain in [(0, 1), (.024, .18), (.057, .10), (.089, .06)]:
        mix(result, sound, delay, gain)
    return fade(result, .001, .08)


def ooo(length, dark=False):
    t = np.arange(round(length * RATE)) / RATE
    # General soft vowel, with no copied recording or identifiable voice.
    pitch = np.interp(t, [0, .35, 1.4, 1.65, length], [150, 145, 119, 126, 104])
    if dark:
        pitch *= .52
    phase = np.cumsum(pitch * (1 + .012 * np.sin(2 * np.pi * 4.1 * t))) * 2 * np.pi / RATE
    x = np.zeros_like(t)
    for harmonic in range(1, 30):
        frequency = pitch * harmonic
        vowel = .15 + np.exp(-((frequency - 330) / 135) ** 2) + .6 * np.exp(-((frequency - 760) / 190) ** 2)
        x += np.sin(phase * harmonic) * vowel / harmonic
    envelope = np.minimum(t / .22, 1) * np.minimum((length - t) / .4, 1)
    envelope *= 1 - .55 * np.exp(-((t - 1.48) / .09) ** 2)
    return normal(fade(x * envelope, .04, .16), .5)


def chimes(length, notes):
    x = np.zeros(round(length * RATE))
    for index, frequency in enumerate(notes):
        mix(x, tone(frequency, min(1.7, length - .12 * index), 3.1), .12 * index, .25)
    return fade(x, .01, .3)


def meta(path, body):
    sidecar = Path(str(path) + '.meta')
    if not sidecar.exists():
        sidecar.write_text('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex + '\n' + body, encoding='utf-8')


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    meta(OUT, 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
    manifest = []

    def save(code, label, sound, events, kind, peak=.7):
        sound = normal(fade(sound, .0005, .025), peak)
        path = OUT / (code + '.wav')
        with wave.open(str(path), 'wb') as stream:
            stream.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
            stream.writeframes(np.round(np.clip(sound, -1, 1) * 32767).astype('<i2').tobytes())
        meta(path, '''AudioImporter:
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 22050
    compressionFormat: 0
    quality: 1
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
        manifest.append({'code': code, 'label': label, 'duration': len(sound) / RATE,
                         'events': events, 'kind': kind})

    bomb = heavy_explosion()
    x = np.zeros(round(3.85 * RATE))
    for time, gain in [(.65, .9), (1.35, 1)]:
        mix(x, bomb, time, gain)
    save('s1_4', '폭탄 2발 폭발', x, [.65, 1.35], 'CC0 recorded blast layered with a large explosion tail', peak=.7)

    x = np.zeros(round(2.5 * RATE))
    for time, gain, start, end in [(.35, .94, 3.18, 3.80), (1, .98, 8.60, 9.22), (1.65, 1, 9.55, 10.17)]:
        mix(x, heavy_wall_hit(start, end), time, gain)
    save('s5_10', '큰 망치로 벽을 강하게 3번 타격', x, [.35, 1, 1.65], 'CC0 sledgehammer wall-demolition recording edit', peak=.72)

    cry = cut('child_cry', 33.4, 37.2, trim=True)
    cry = sosfilt(butter(3, 120, btype='highpass', fs=RATE, output='sos'), cry)
    x = np.zeros(round(1.6 * RATE)); mix(x, cry, .12)
    save('s5_11', '유아 울음 (5살 정도 느낌)', x, [.12], 'CC0 preschool child crying edit; source describes approximately four years old')

    x = np.zeros(round(1.25 * RATE)); mix(x, cut('crow', 0, .633), .2)
    save('s5_4', '까마귀 까아', x, [.2], 'CC0 recording edit')
    save('s5_5', '쓸쓸한 우우우~우~', read('user_s5_5')[:round(3.5 * RATE)], [0], 'First 3.5 seconds of user-provided video audio')

    gun = cut('gun', 0, .9, .42, True); glass = cut('glass', 0, 1.6, trim=True)
    x = np.zeros(round(3.25 * RATE))
    mix(x, gun, 0, .65); mix(x, gun, .5, .7); mix(x, glass, 1.5, .9)
    save('s5_6', '탕! 탕! 후 유리창 깨짐', x, [0, .5, 1.5], 'CC0 recording montage')

    t = np.arange(round(2.8 * RATE)) / RATE
    wind = sosfilt(butter(2, [1400, 8500], btype='bandpass', fs=RATE, output='sos'), rng.normal(0, 1, len(t)))
    wind *= np.sin(np.pi * t / 2.8) ** 1.8 * np.exp(-t * .65)
    for time, frequency in [(.12, 3200), (.28, 4200), (.47, 5100), (.7, 6200), (.96, 7100)]:
        mix(wind, tone(frequency, .28, 24, .001), time, .18)
    blizzard = fade(cut('blizzard', 1, 4.6), .16, .4)
    save('s5_7', '눈보라 강풍', blizzard, [0], 'CC0 recorded snowstorm wind edit')

    x = np.zeros(round(4.8 * RATE)); bed = read('storm')[:len(x)]
    rain = sosfilt(butter(2, 1600, btype='highpass', fs=RATE, output='sos'), bed)
    rain = normal(rain, .2); rain = fade(rain, .65, .7)
    x += rain
    thunder = cut('storm', 1.18, 2.15, .7)
    for time, gain in [(1.35, .7), (1.95, .85), (2.65, 1)]:
        mix(x, thunder, time, gain)
    save('s5_8', '번개·천둥과 비', x, [1.35, 1.95, 2.65], 'CC0 storm/rain recording montage')

    x = np.zeros(round(4.4 * RATE)); mix(x, ooo(2.9, True), .05, .25)
    t = np.arange(round(2.4 * RATE)) / RATE
    rush = sosfilt(butter(2, [100, 2400], btype='bandpass', fs=RATE, output='sos'), rng.normal(0, 1, len(t)))
    rush *= (t / 2.4) ** 2
    mix(x, fade(rush, .15, .02), .05, .24)
    mix(x, tone(73.42, 1.9, 2.8, .002), 2.45, .7)
    mix(x, tone(110, 1.9, 3, .002), 2.45, .25)
    save('s5_9', '악마소환', read('user_s5_9'), [0], 'User-provided video audio recording')

    save('s6_5', '아름다운 음악', chimes(3.2, [523.25, 659.25, 783.99, 987.77, 1046.5]), [0], 'Original celesta/chime composition')
    save('s8_4', '깔깔 웃음', cut('laugh', .32, 2.9), [0], 'CC0 recorded laughter edit')
    x = chimes(3.8, [659.25, 783.99, 987.77, 1318.51, 1567.98])
    mix(x, chimes(2.3, [1046.5, 1318.51, 1567.98]), 1.4, .32)
    save('s8_5', '아름다운 빛·후광', x, [0, 1.4], 'Original luminous chime composition')
    (OUT / 'manifest.json').write_text(json.dumps({'items': manifest}, ensure_ascii=False, indent=2), encoding='utf-8')
    meta(OUT / 'manifest.json', 'TextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n')
    # Separate from the electronic result-screen counter and coin-flight cue.
    coin = read('user_score_coin')
    # Keep the original bell's fundamental and a softer metallic overtone;
    # reject broadband hiss, low rumble and harsh upper harmonics.
    body = sosfiltfilt(butter(3, [1000, 1700], btype='bandpass', fs=RATE, output='sos'), coin)
    sparkle = sosfiltfilt(butter(3, [3600, 4300], btype='bandpass', fs=RATE, output='sos'), coin)
    coin = normal(fade(body + .4 * sparkle, .004, .05), .28)
    path = OUT.parent / 'ScoreCoin.wav'
    with wave.open(str(path), 'wb') as stream:
        stream.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        stream.writeframes(np.round(coin * 32767).astype('<i2').tobytes())
    meta(path, 'AudioImporter:' + (OUT / 's5_7.wav.meta').read_text(encoding='utf-8').split('AudioImporter:', 1)[1])
    print('Prepared 12 actor-specific soundtracks and their timing manifest.')


if __name__ == '__main__':
    main()
