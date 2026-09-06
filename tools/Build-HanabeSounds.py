"""Render original notification cues through Audacity's local scripting pipe.

Requires an open Audacity 3.7 project with mod-script-pipe enabled.
No downloaded samples, accounts, cloud service or Python packages are required.
"""
import argparse
import array
import json
import math
from pathlib import Path
import wave

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / '.artifacts' / 'sound-production'
DEST = ROOT / 'src/HanabePhotoManager.App/Assets/Hanabe/Sounds'


class Audacity:
    def __init__(self):
        self.writer = open(r'\\.\pipe\ToSrvPipe', 'w', encoding='utf-8')
        self.reader = open(r'\\.\pipe\FromSrvPipe', encoding='utf-8')

    def command(self, command):
        self.writer.write(command + '\n\0')
        self.writer.flush()
        result = ''
        while True:
            line = self.reader.readline()
            if line == '\n' and result:
                break
            result += line
        with (WORK / 'audacity-commands.log').open('a', encoding='utf-8') as log:
            log.write(command + '\n' + result + '\n')
        if 'BatchCommand finished: OK' not in result:
            raise RuntimeError(command + '\n' + result)
        return result


def synth(path, style, notes):
    rate = 48000
    length = max(start + duration for start, duration, frequency in notes) + .035
    samples = [0.] * math.ceil(length * rate)
    for start, duration, frequency in notes:
        for i in range(int(duration * rate)):
            t = i / rate
            envelope = min(1, t / .008) * max(0, 1 - t / duration) ** 2
            fundamental = math.sin(2 * math.pi * frequency * t)
            harmonic = math.sin(2 * math.pi * frequency * 2 * t)
            click = math.sin(2 * math.pi * 2300 * t) * math.exp(-t * 180)
            tone = {'Cute': fundamental + .12 * harmonic,
                    'Camera': .45 * fundamental + .35 * harmonic + .45 * click,
                    'Mixed': .8 * fundamental + .2 * harmonic + .18 * click}[style]
            samples[int(start * rate) + i] += tone * envelope * .32
    pcm = array.array('h', (round(max(-1, min(1, x)) * 32767) for x in samples))
    with wave.open(str(path), 'wb') as output:
        output.setparams((1, 2, rate, 0, 'NONE', 'not compressed'))
        output.writeframes(pcm.tobytes())


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--probe', action='store_true')
    parser.add_argument('--offline', action='store_true')
    args = parser.parse_args()
    WORK.mkdir(parents=True, exist_ok=True)
    editor = None if args.offline else Audacity()
    if args.probe:
        print(editor.command('GetAllCommandsInfo: Format=JSON'))
        return
    cues = {
        'importing': [(0, .16, 523.25), (.11, .22, 783.99)],
        'scanning': [(0, .14, 440), (.1, .19, 659.25)],
        'checking': [(0, .14, 587.33), (.1, .19, 880)],
        'completed': [(0, .18, 523.25), (.12, .18, 659.25), (.24, .30, 1046.5)],
        'error': [(0, .22, 349.23), (.18, .29, 246.94)],
        'skipped': [(0, .07, 880), (.10, .09, 880)],
        'opened': [(0, .15, 1174.66)],
        'canceled': [(0, .15, 659.25), (.11, .19, 440)]
    }
    manifest = []
    for style in ('Camera', 'Cute', 'Mixed'):
        for name, notes in cues.items():
            source = WORK / f'{style}-{name}-source.wav'
            output = DEST / style / f'{name}.wav'
            source.parent.mkdir(parents=True, exist_ok=True)
            output.parent.mkdir(parents=True, exist_ok=True)
            synth(source, style, notes)
            if editor is not None:
                editor.command('New:')
                editor.command(f'Import2: Filename="{source.as_posix()}"')
                editor.command('SelectAll:')
                editor.command('Normalize: ApplyGain=1 PeakLevel=-6 RemoveDcOffset=1 StereoIndependent=0')
                editor.command(f'Export2: Filename="{output.as_posix()}" NumChannels=1')
                editor.command(f'SaveProject2: Filename="{(WORK / (style + "-" + name + ".aup3")).as_posix()}"')
            else:
                output.write_bytes(source.read_bytes())
            # Keep the Audacity process alive between renders; closing the only
            # project can terminate the portable editor on Windows.
            with wave.open(str(output), 'rb') as wav:
                assert wav.getsampwidth() == 2 and wav.getnchannels() == 1
                data = array.array('h', wav.readframes(wav.getnframes()))
                peak = max(abs(x) for x in data)
                assert 1000 < peak < 20000
                manifest.append(dict(style=style, event=name, seconds=len(data)/wav.getframerate(), peak=peak, sampleRate=wav.getframerate()))
            print(style, name, 'rendered and verified', flush=True)
    (WORK / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
