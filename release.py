import json
import os
import pathlib
import re
import shutil
import subprocess
import sys
import zipfile

PROJECT_NAME = "RemoveTheAnnoying"
OUTPUT_DIR = "out"

MOD_DESC = (
    "A configurable mod to disable Barbers, Maneaters, and "
    "certain interiors. Supports scrap QoL similar to ShipLoot, "
    "allows for v56 Artifice scrap rates, fixes players being marked 'missing' "
    "when magneted cruisers depart, and more!"
)


# Validates CLI arguments and semver structure
def get_semver() -> str:
    if len(sys.argv) != 2:
        print("Usage: python scripts/release.py semver")
        exit(1)

    semver_regex = r"^\d+\.\d+\.\d+$"
    version = sys.argv[1].strip()
    if re.match(semver_regex, version):
        return version
    print("Invalid format, expected valid semver (MAJOR.MINOR.PATCH)")
    exit(1)


# Runs dotnet build and copies the output dll to `out/`
def dotnet_build() -> str:
    result = subprocess.run(
        ["dotnet", "build", "-c", "Release"], shell=True, capture_output=True, text=True
    )

    if result.returncode != 0:
        print(result.stdout)
        print(result.stderr)
        exit(1)

    compiled_path = pathlib.Path(f"bin/Release/{PROJECT_NAME}.dll")
    if not compiled_path.exists:
        print(f"{compiled_path} does not exist")
        exit(1)

    result_path = f"{OUTPUT_DIR}/RemoveTheAnnoying.dll"
    shutil.copyfile(compiled_path, result_path)
    return result_path


# Creates a versioned manifest json file
def create_manifest(version) -> str:
    manifest_data = {
        "name": "RemoveTheAnnoying",
        "version_number": version,
        "description": MOD_DESC,
        "website_url": "https://github.com/trevorswan11/RemoveTheAnnoying.git",
        "dependencies": ["BepInEx-BepInExPack-5.4.2100"],
    }

    manifest_path = f"{OUTPUT_DIR}/manifest.json"
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest_data, f, indent=4)
    return manifest_path


if len(MOD_DESC) > 256:
    print("Mod description exceeds Thunderstore's 256 character limit")
    exit(1)

pathlib.Path(OUTPUT_DIR).mkdir(exist_ok=True)
version = get_semver()
output_zip_name = f"{OUTPUT_DIR}/Kyoshi-RemoveTheAnnoying-v{version}.zip"
dll_path = dotnet_build()
manifest_path = create_manifest(version)

with zipfile.ZipFile(output_zip_name, "w", zipfile.ZIP_DEFLATED) as zipf:
    zipf.write(manifest_path, os.path.basename(manifest_path))
    zipf.write(".github/CHANGELOG.md", "CHANGELOG.md")
    zipf.write("assets/icon.png", "icon.png")
    zipf.write("README.md", "README.md")
    zipf.write("LICENSE", "LICENSE")
    zipf.write(dll_path, os.path.basename(dll_path))
