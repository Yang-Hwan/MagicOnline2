"""Import the TableSet07 dependency closure without overwriting online assets."""
from pathlib import Path
import re, json, shutil, uuid, sys, hashlib

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path(r'D:\Make\Work\Unity\dangguu\MagicCushionSingle\MagicRenewal\MagicRenewal')
GUID = re.compile(rb'guid["\\]*\s*:\s*["\\]*([0-9a-f]{32})')

def index(root):
    result = {}
    for meta in (root / 'Assets').rglob('*.meta'):
        match = GUID.search(meta.read_bytes())
        if match:
            result[match[1].decode()] = meta.with_suffix('')
    return result

def plan():
    source, target = index(SOURCE), index(ROOT)
    pending = [SOURCE / 'Assets/Scenes/TableSet07.unity']
    pending += list((SOURCE / 'Assets/TutorialInfo/Scripts/TableSet06').rglob('*.cs'))
    seen, external = set(), set()
    while pending:
        path = pending.pop()
        if path in seen: continue
        seen.add(path)
        # Built-in packages and existing DOTween/TMP assets are shared; custom
        # art and scripts retain the source bytes under new, isolated GUIDs.
        for data in (path.read_bytes(), Path(str(path) + '.meta').read_bytes()):
            for guid in GUID.findall(data):
                g = guid.decode()
                dep = source.get(g)
                if dep and dep.is_file():
                    rel = dep.relative_to(SOURCE).as_posix()
                    if rel.startswith(('Assets/Plugins/', 'Assets/TextMesh Pro/')) and g in target:
                        continue
                    if dep not in seen: pending.append(dep)
                elif g not in target and not g.startswith('0000000000000000'):
                    external.add(g)
    return source, target, seen, external

if __name__ == '__main__':
    source, target, paths, external = plan()
    if '--apply' in sys.argv:
        output = ROOT / 'Assets/Practice'
        if output.exists(): raise SystemExit('Practice already exists; refusing to overwrite.')
        mapping = {GUID.search(Path(str(p) + '.meta').read_bytes())[1].decode(): uuid.uuid4().hex for p in paths}
        records = []
        for path in sorted(paths):
            relative = path.relative_to(SOURCE / 'Assets')
            dest = output / relative
            dest.parent.mkdir(parents=True, exist_ok=True)
            for original, copied in [(path, dest), (Path(str(path)+'.meta'), Path(str(dest)+'.meta'))]:
                data = original.read_bytes()
                if original.suffix.lower() not in ('.png', '.ttf', '.fbx'):
                    for old, new in mapping.items(): data = data.replace(old.encode(), new.encode())
                copied.write_bytes(data)
            records.append({'source': relative.as_posix(), 'destination': dest.relative_to(ROOT).as_posix(), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
        (ROOT / 'Docs').mkdir(exist_ok=True)
        (ROOT / 'Docs/practice-import-manifest.json').write_text(json.dumps({'source': str(SOURCE), 'guids': mapping, 'assets': records}, indent=2), encoding='utf-8')
        print('Imported', len(paths), 'assets.')
    print(json.dumps({'files': len(paths), 'bytes': sum(p.stat().st_size for p in paths),
        'extensions': {ext: sum(p.suffix == ext for p in paths) for ext in sorted({p.suffix for p in paths})},
        'external': sorted(external), 'scripts': [str(p.relative_to(SOURCE)) for p in sorted(paths) if p.suffix == '.cs']}, indent=2))
