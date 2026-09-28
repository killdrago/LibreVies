"""Met a jour la branche publiee par le launcher LibreVies.

Le projet Unreal n'est pas telecharge automatiquement par le joueur : seul le
package de jeu contenu dans jeu/game est publie.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "jeu" / "version_url.json"
LAUNCHER = ROOT / "jeu" / "launcher.pyw"
REPOSITORY = "killdrago/LibreVies"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--branche", required=True)
    parser.add_argument("--depot", default=REPOSITORY)
    args = parser.parse_args()

    branch = args.branche.strip().strip("/")
    raw = f"https://raw.githubusercontent.com/{args.depot}/{branch}/jeu"
    tree = f"https://github.com/{args.depot}/tree/{branch}/jeu"

    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    manifest["raw_url"] = raw
    manifest["game_url"] = tree
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    source = LAUNCHER.read_text(encoding="utf-8")
    marker = "DEFAULT_RAW_URL = "
    start = source.find(marker)
    if start < 0:
        raise SystemExit("DEFAULT_RAW_URL introuvable dans launcher.pyw")
    end = source.find("\n", start)
    while end >= 0 and source[start:end].count("(") > source[start:end].count(")"):
        end = source.find("\n", end + 1)
    replacement = (
        f'DEFAULT_RAW_URL = ("https://raw.githubusercontent.com/{args.depot}/"\n'
        f'                   "{branch}/jeu")'
    )
    LAUNCHER.write_text(source[:start] + replacement + source[end:], encoding="utf-8")
    print(f"Branche de publication : {branch}")
    print(f"Package Unreal : {tree}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
