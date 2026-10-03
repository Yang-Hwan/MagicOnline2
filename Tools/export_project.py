"""Create and verify a source-project checkpoint without Unity caches/builds."""
import hashlib
import json
from datetime import datetime
from pathlib import Path
import zipfile

ROOT = Path(__file__).resolve().parents[1]
INCLUDE = ("Assets", "Packages", "ProjectSettings", "Docs", "Tools")


def sources():
    return sorted(
        p for folder in INCLUDE for p in (ROOT / folder).rglob("*")
        if p.is_file() and "__pycache__" not in p.parts and p.suffix != ".pyc"
    )


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    destination = ROOT / "Exports" / f"MagicOnline2-current-{stamp}.zip"
    destination.parent.mkdir(exist_ok=True)
    pending = destination.with_suffix(".zip.partial")
    files = sources()
    manifest = {
        "created": datetime.now().astimezone().isoformat(),
        "unity": "6000.5.7f1", "included": INCLUDE,
        "excluded": ["Library", "Temp", "Logs", "Build", "Builds", "UserSettings", "Exports", "__pycache__"],
        "files": [],
    }
    with zipfile.ZipFile(pending, "x", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in files:
            relative = path.relative_to(ROOT).as_posix()
            if path.is_symlink() or not path.resolve().is_relative_to(ROOT):
                raise RuntimeError(f"External or linked source: {relative}")
            checksum = digest(path)
            archive.write(path, relative)
            manifest["files"].append({"path": relative, "bytes": path.stat().st_size, "sha256": checksum})
        archive.writestr("EXPORT-MANIFEST.json", json.dumps(manifest, ensure_ascii=False, indent=2))
    with zipfile.ZipFile(pending) as archive:
        if len(archive.namelist()) != len(files) + 1:
            raise RuntimeError("Archive entry count mismatch")
        for entry in manifest["files"]:
            with archive.open(entry["path"]) as stream:
                checksum = hashlib.file_digest(stream, "sha256").hexdigest()
            if checksum != entry["sha256"] or digest(ROOT / entry["path"]) != checksum:
                raise RuntimeError(f"Source changed or archive mismatch: {entry['path']}")
        if sources() != files:
            raise RuntimeError("Source file list changed while exporting")
    pending.rename(destination)
    checksum = digest(destination)
    destination.with_suffix(".zip.sha256").write_text(f"{checksum}  {destination.name}\n", encoding="utf-8")
    result = {"path": str(destination), "files": len(files), "bytes": destination.stat().st_size,
              "verified": True, "sha256": checksum}
    (destination.parent / "latest-checkpoint.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
