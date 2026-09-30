#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Met a jour automatiquement l'atelier de vetements depuis GitHub.

Le script ne touche jamais aux exports et projets de l'utilisateur. Il ne
telecharge que les fichiers declares dans update_manifest.json, puis verifie
leur SHA-256 avant de les installer de facon atomique.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys
import tempfile
import urllib.error
import urllib.request
from pathlib import Path
from typing import Dict, Iterable, Tuple

REMOTE_BASE = os.environ.get(
    "LIBREVIES_VETEMENT_UPDATE_URL",
    "https://raw.githubusercontent.com/killdrago/LibreVies/"
    "arena/01a0b32c-librevies/vetement",
).rstrip("/")
MANIFEST_NAME = "update_manifest.json"
USER_AGENT = "LibreVies-Vetement-Updater/1.0"
MAX_FILE_SIZE = 50 * 1024 * 1024


def log(message: str, quiet: bool = False) -> None:
    if not quiet:
        print(message, flush=True)


def remote_url(relative_path: str) -> str:
    # Les chemins du manifeste sont relatifs au dossier vetement du depot.
    return REMOTE_BASE + "/" + "/".join(relative_path.split("/"))


def read_url(url: str, timeout: int = 30) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=timeout) as response:
        content_length = response.headers.get("Content-Length")
        if content_length and int(content_length) > MAX_FILE_SIZE:
            raise ValueError("fichier distant trop volumineux")
        data = response.read(MAX_FILE_SIZE + 1)
    if len(data) > MAX_FILE_SIZE:
        raise ValueError("fichier distant trop volumineux")
    return data


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def local_sha256(path: Path) -> str | None:
    if not path.is_file():
        return None
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def safe_relative_path(value: object) -> str:
    if not isinstance(value, str) or not value or "\\" in value:
        raise ValueError("chemin de manifeste invalide")
    path = Path(value)
    if path.is_absolute() or ".." in path.parts or path.name != value.split("/")[-1]:
        raise ValueError("chemin de manifeste invalide")
    return value


def load_manifest(data: bytes) -> Dict:
    manifest = json.loads(data.decode("utf-8"))
    if not isinstance(manifest, dict) or not isinstance(manifest.get("files"), list):
        raise ValueError("manifeste distant invalide")
    checked = []
    for item in manifest["files"]:
        if not isinstance(item, dict):
            raise ValueError("entree de manifeste invalide")
        path = safe_relative_path(item.get("path"))
        digest = item.get("sha256")
        if not isinstance(digest, str) or len(digest) != 64:
            raise ValueError("empreinte SHA-256 invalide")
        int(digest, 16)
        checked.append({"path": path, "sha256": digest.lower()})
    manifest["files"] = checked
    manifest.setdefault("schema", 1)
    manifest.setdefault("version", "inconnue")
    return manifest


def target_path(root: Path, relative_path: str) -> Path:
    destination = (root / Path(*relative_path.split("/"))).resolve()
    if destination != root.resolve() and root.resolve() not in destination.parents:
        raise ValueError("destination hors du dossier vetement")
    return destination


def find_updates(root: Path, manifest: Dict) -> list[dict]:
    updates = []
    for item in manifest["files"]:
        destination = target_path(root, item["path"])
        current = local_sha256(destination)
        if current != item["sha256"]:
            updates.append({**item, "destination": destination, "current": current})
    return updates


def install_file(destination: Path, content: bytes) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary_name = tempfile.mkstemp(
        prefix="." + destination.name + ".", suffix=".download", dir=str(destination.parent)
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(handle, "wb") as output:
            output.write(content)
            output.flush()
            os.fsync(output.fileno())
        os.replace(temporary, destination)
    finally:
        temporary.unlink(missing_ok=True)


def write_local_manifest(root: Path, raw_manifest: bytes) -> None:
    # Le manifeste local sert d'information et permet de voir la version
    # installee hors connexion. Sa propre empreinte n'est pas geree comme un
    # fichier de code, sinon elle se referencerait elle-meme.
    install_file(root / MANIFEST_NAME, raw_manifest)


def update(root: Path, check_only: bool = False, quiet: bool = False) -> int:
    try:
        raw_manifest = read_url(remote_url(MANIFEST_NAME))
        manifest = load_manifest(raw_manifest)
    except (OSError, ValueError, urllib.error.URLError, json.JSONDecodeError) as exc:
        log("[atelier] Mise a jour indisponible : " + str(exc), quiet)
        return 0

    updates = find_updates(root, manifest)
    if not updates:
        log("[atelier] Atelier deja a jour (" + str(manifest["version"]) + ").", quiet)
        if not check_only:
            try:
                write_local_manifest(root, raw_manifest)
            except OSError:
                pass
        return 0

    log("[atelier] " + str(len(updates)) + " fichier(s) a mettre a jour.", quiet)
    if check_only:
        for item in updates:
            log("  - " + item["path"], quiet)
        return 0

    failures = 0
    for item in updates:
        try:
            content = read_url(remote_url(item["path"]), timeout=60)
            if sha256(content) != item["sha256"]:
                raise ValueError("empreinte distante differente du manifeste")
            install_file(item["destination"], content)
            log("  + " + item["path"], quiet)
        except (OSError, ValueError, urllib.error.URLError) as exc:
            failures += 1
            log("  ! " + item["path"] + " : " + str(exc), quiet)

    if failures == 0:
        try:
            write_local_manifest(root, raw_manifest)
        except OSError as exc:
            log("[atelier] Impossible d'enregistrer le manifeste local : " + str(exc), quiet)
        log("[atelier] Mise a jour terminee vers " + str(manifest["version"]) + ".", quiet)
        return 0
    log("[atelier] Mise a jour incomplete : l'atelier actuel est conserve fichier par fichier.", quiet)
    return 1


def main() -> int:
    parser = argparse.ArgumentParser(description="Mettre a jour l'atelier de vetements LibreVies")
    parser.add_argument("--check", action="store_true", help="afficher les fichiers a mettre a jour sans les installer")
    parser.add_argument("--quiet", action="store_true", help="ne pas afficher les messages normaux")
    args = parser.parse_args()
    return update(Path(__file__).resolve().parent, args.check, args.quiet)


if __name__ == "__main__":
    raise SystemExit(main())
