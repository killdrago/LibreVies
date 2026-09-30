#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Genere le manifeste SHA-256 publie avec l'atelier de vetements.

Usage depuis le dossier vetement :
    python generer_manifest.py 2026.09.30.2
"""

from __future__ import annotations

import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent
MANIFEST = ROOT / "update_manifest.json"
FILES = (
    "atelier3d.py",
    "vetement.py",
    "lancer_vetement.bat",
    "requirements.txt",
    "README.md",
    "update_vetement.py",
    "generer_manifest.py",
)


def digest(path: Path) -> str:
    sha = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            sha.update(block)
    return sha.hexdigest()


def main() -> int:
    version = sys.argv[1] if len(sys.argv) > 1 else datetime.now(timezone.utc).strftime("%Y.%m.%d.%H%M")
    files = []
    for relative in FILES:
        path = ROOT / relative
        if not path.is_file():
            raise SystemExit("Fichier introuvable : " + relative)
        files.append({"path": relative, "sha256": digest(path)})
    manifest = {
        "schema": 1,
        "version": version,
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "files": files,
    }
    MANIFEST.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print("Manifeste ecrit :", MANIFEST)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
