"""
Packages worlds/lobotomy_corporation into dist/lobotomy_corporation.apworld.

The recommended way to build an .apworld is Archipelago's own "Build APWorlds" launcher component
(`python Launcher.py "Build APWorlds" -- "Lobotomy Corporation"` from an Archipelago source checkout that contains this
world). This script produces the same layout without needing an Archipelago checkout.
"""
import json
import pathlib
import zipfile

# Version of the APContainer packaging scheme (worlds/Files.py, "version" / "compatible_version").
APCONTAINER_VERSION = 7

ROOT = pathlib.Path(__file__).resolve().parent.parent
WORLD = ROOT / "worlds" / "lobotomy_corporation"
OUTPUT = ROOT / "dist" / "lobotomy_corporation.apworld"
EXCLUDED_PARTS = {"__pycache__", ".pytest_cache"}


def main() -> None:
    manifest = json.loads((WORLD / "archipelago.json").read_text(encoding="utf-8"))
    manifest["version"] = APCONTAINER_VERSION
    manifest["compatible_version"] = APCONTAINER_VERSION
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(OUTPUT, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in sorted(WORLD.rglob("*")):
            if path.is_dir() or EXCLUDED_PARTS & set(path.parts) or path.suffix == ".pyc":
                continue
            relative = path.relative_to(WORLD.parent).as_posix()
            if relative == "lobotomy_corporation/archipelago.json":
                continue
            archive.write(path, relative)
        archive.writestr("lobotomy_corporation/archipelago.json", json.dumps(manifest))
    print(f"Wrote {OUTPUT} (world version {manifest.get('world_version')})")


if __name__ == "__main__":
    main()
