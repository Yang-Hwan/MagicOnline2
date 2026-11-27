"""Prepare only cue/ball contacts from the bundled CC0 recordings.

Requires numpy and scipy. Source provenance is in AudioSources/Billiard/LICENSE.txt.
The full audio-bank generator copies these masters instead of synthesizing impacts.
"""
from pathlib import Path
import json
import shutil
import wave

import numpy as np
from scipy.signal import butter, sosfilt, resample_poly

ROOT = Path(__file__).resolve().parents[1]
SOURCES = ROOT / 'Tools/AudioSources/Billiard'
OUTPUT = ROOT / 'Assets/Resources/Audio'
RATE = 44100


def read(name):
    with wave.open(str(SOURCES / name), 'rb') as stream:
        assert stream.getsampwidth() == 2 and stream.getframerate() == RATE
        samples = np.frombuffer(stream.readframes(stream.getnframes()), '<i2').astype(np.float64) / 32768
        return samples.reshape(-1, stream.getnchannels()).mean(axis=1)


def prepare(source, start, end, length, lowpass, peak, rate_up=1, rate_down=1):
    samples = source[round(start * RATE):round(end * RATE)].copy()
    # Remove leading room tone while retaining a small pre-attack margin.
    attack = np.flatnonzero(np.abs(samples) > np.max(np.abs(samples)) * .035)[0]
    samples = samples[max(0, attack - round(.0007 * RATE)):]
    if rate_up != rate_down:
        samples = resample_poly(samples, rate_up, rate_down)
    samples = samples[:round(length * RATE)]
    samples -= np.mean(samples)
    samples = sosfilt(butter(2, 100, btype='highpass', fs=RATE, output='sos'), samples)
    samples = sosfilt(butter(3, lowpass, fs=RATE, output='sos'), samples)
    t = np.arange(len(samples)) / RATE
    # Retain the recorded contact/body; gently shorten room noise and reverberation.
    samples *= np.exp(-np.maximum(t - .024, 0) / .065)
    fade_in = max(2, round(.0002 * RATE))
    fade_out = min(len(samples), round(.014 * RATE))
    samples[:fade_in] *= np.linspace(0, 1, fade_in)
    samples[-fade_out:] *= np.linspace(1, 0, fade_out)
    samples *= peak / max(np.max(np.abs(samples)), 1e-8)
    return samples


def write(name, samples):
    master = SOURCES / (name + '.wav')
    pcm = np.round(np.clip(samples, -1, 1) * 32767).astype('<i2')
    with wave.open(str(master), 'wb') as stream:
        stream.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        stream.writeframes(pcm.tobytes())
    shutil.copyfile(master, OUTPUT / master.name)
    meta = OUTPUT / (master.name + '.meta')
    # PCM preserves the very short impact transient; existing Unity GUIDs stay intact.
    metadata = meta.read_text(encoding='utf-8-sig').replace('compressionFormat: 1', 'compressionFormat: 0')
    metadata = metadata.replace('sampleRateOverride: 22050', 'sampleRateOverride: 44100')
    meta.write_text(metadata, encoding='utf-8')
    return {'name': name, 'durationMs': round(len(samples) / RATE * 1000, 2),
            'peak': round(float(max(abs(samples))), 4), 'sampleRate': RATE}


def main():
    strike = read('709674-source.wav')
    ball = read('539854-source.wav')
    report = [write('Strike', prepare(strike, .49, .74, .16, 8000, .68))]
    for index, (lowpass, up, down) in enumerate([(11000, 1, 1), (10200, 101, 100), (11600, 100, 101)], 1):
        report.append(write('Ball_' + str(index), prepare(ball, 0, len(ball) / RATE, .13, lowpass, .62, up, down)))
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
