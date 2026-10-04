# Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

import base64
import json
import pathlib
import subprocess
import tempfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
PROJECT = ROOT / "src" / "FoxIPTV" / "FoxIPTV.csproj"
OUTPUT = ROOT / "flatpak" / "nuget-sources.json"
SKIPPED = ("microsoft.netcore.app.", "microsoft.aspnetcore.app.", "microsoft.net.illink.tasks", "microsoft.windowsdesktop.app.")

with tempfile.TemporaryDirectory() as work:
    packages = pathlib.Path(work) / "packages"

    for rid in ("linux-x64", "linux-arm64"):
        subprocess.run(["dotnet", "restore", str(PROJECT), "-r", rid, "-p:SelfContained=true", "--packages", str(packages), f"-p:MSBuildProjectExtensionsPath={work}/obj-{rid}/"], check=True)

    sources = []

    for checksum in sorted(packages.glob("*/*/*.nupkg.sha512")):
        name, version = checksum.parent.parent.name, checksum.parent.name

        if name.startswith(SKIPPED):
            continue

        sources.append({
            "type": "file",
            "url": f"https://api.nuget.org/v3-flatcontainer/{name}/{version}/{name}.{version}.nupkg",
            "sha512": base64.b64decode(checksum.read_text().strip()).hex(),
            "dest": "nuget-sources",
            "dest-filename": f"{name}.{version}.nupkg"
        })

OUTPUT.write_text(json.dumps(sources, indent=4) + "\n", encoding="utf-8")

print(f"{len(sources)} packages written to {OUTPUT}")
