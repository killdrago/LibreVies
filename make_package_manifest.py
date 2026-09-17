"""Génère le manifeste du dossier release de LibreVies.

Utilisé uniquement lors de la fabrication d'une distribution Windows. Le
joueur ne lance jamais ce script et n'installe aucun composant supplémentaire.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from urllib.parse import quote


def md5(path: Path) -> str:
    digest = hashlib.md5()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main() -> None:
    parser = argparse.ArgumentParser(description="Manifeste de distribution LibreVies")
    parser.add_argument("release", type=Path)
    parser.add_argument("--config", type=Path, default=Path("version_url.json"))
    parser.add_argument("--output", type=Path, default=None)
    parser.add_argument(
        "--asset-base-url",
        default="",
        help="URL publique du dossier contenant LibreVies.exe et game/",
    )
    args = parser.parse_args()

    config = json.loads(args.config.read_text(encoding="utf-8"))
    game_relative = "game/LibreViesGame.exe"
    required = ["LibreVies.exe", game_relative]
    asset_base = args.asset_base_url.rstrip("/")
    package_files = {}

    # Unity Windows produit plusieurs fichiers indispensables au runtime :
    # UnityPlayer.dll, *_Data/ et parfois des fichiers de configuration. Ils
    # doivent tous suivre le même cycle de mise à jour que l'exécutable.
    for path in sorted(args.release.rglob("*")):
        if not path.is_file() or path.name == "version_url.json":
            continue
        relative = path.relative_to(args.release).as_posix()
        info = {"hash": md5(path), "size": path.stat().st_size}
        if asset_base:
            info["url"] = f"{asset_base}/{quote(relative, safe='/')}"
        package_files[relative] = info

    for relative in required:
        if relative not in package_files:
            raise SystemExit(f"Fichier exporté absent : {args.release / relative}")

    config["package"] = {
        "game_executable": game_relative,
        "files": package_files,
    }
    output = args.output or args.release / "version_url.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        json.dumps(config, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(f"Manifeste écrit : {output}")
    print(f"  {len(package_files)} fichiers runtime suivis")
    for name, info in package_files.items():
        print(f"  {name}: {info['hash']} ({info['size']} octets)")


if __name__ == "__main__":
    main()
