"""Valide un export Mono puis prepare le launcher LOCAL dans jeu.

Aucun acces HTTP/Git/SQL, aucun changement de config.php ou d'Autolog.
--verifier-seulement sert a controler l'export temporaire avant remplacement.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import os
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REQUIRED = ("InitialiserPseudoJoueur", "CreerNomJoueur", "MettreAJourNomJoueur",
            "LibreViesCompte", "LibreViesPersonnage", "CreerPoigneesPorte")


def version_sources(root: Path = ROOT) -> str:
    source = root / "compilation/unity/Assets/Scripts/LibreViesGame.cs"
    match = re.search(r'VersionJeu\s*=\s*"([^"]+)"', source.read_text(encoding="utf-8"))
    if not match:
        raise ValueError("Version des sources introuvable.")
    return match.group(1)


def valider_export(dossier: Path, attendue: str) -> dict:
    executable = dossier / "LibreViesGame.exe"
    dll = dossier / "LibreViesGame_Data/Managed/Assembly-CSharp.dll"
    if not executable.is_file():
        raise ValueError("LibreViesGame.exe absent de l'export local.")
    try:
        infos = json.loads((dossier / "version_jeu.json").read_text(encoding="utf-8-sig"))
        contenu = dll.read_bytes()
    except (OSError, ValueError) as erreur:
        raise ValueError("Export ancien ou incomplet : version_jeu.json/assembly absente. Refaire le build.") from erreur
    if not isinstance(infos, dict) or infos.get("version") != attendue:
        raise ValueError("Export obsolete : attendu %s, trouve %s. Ne pas lancer un ancien jeu."
                         % (attendue, infos.get("version") if isinstance(infos, dict) else "inconnu"))
    digest = hashlib.sha256(contenu).hexdigest()
    if infos.get("assembly_sha256") != digest or attendue.encode("utf-16le") not in contenu:
        raise ValueError("Empreinte/version de l'assembly differente du marqueur Unity.")
    if any((nom + "\0").encode("ascii") not in contenu for nom in REQUIRED):
        raise ValueError("Pseudo/profil/poignees absents de l'assembly : export local a refaire.")
    return {"version": attendue, "assembly_sha256": digest}


def ecrire_json_atomique(path: Path, donnees: dict) -> None:
    tmp = path.with_name(path.name + ".tmp")
    tmp.write_text(json.dumps(donnees, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    os.replace(tmp, path)


def preparer(jeu: Path, attendue: str) -> dict:
    infos = valider_export(jeu / "game", attendue)
    path = jeu / "version_url.json"
    try:
        manifest = json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        manifest = {}
    if not isinstance(manifest, dict):
        manifest = {}
    launcher = jeu / "launcher.pyw"
    source = launcher.read_text(encoding="utf-8")
    match = re.search(r'^LAUNCHER_VERSION\s*=\s*"([^"]+)"', source, re.M)
    if not match:
        raise ValueError("Launcher local sans version : mettre ses sources a jour.")
    manifest.update({
        "mode_local": True,
        "launcher_version": match.group(1),
        "game_version": attendue,
        "notes": "Export local verifie, aucune mise a jour du jeu depuis HTTP.",
        "files": {},
        "game_build": {"moteur": "unity", "mode": "local", "version": attendue,
                       "dossier": "game", "exe": "LibreViesGame.exe", "hash": infos["assembly_sha256"]},
    })
    ecrire_json_atomique(path, manifest)
    ecrire_json_atomique(jeu / "etat_jeu.json", {
        "mode": "local", "version": attendue, "moteur": "unity",
        "dossier": "game", "exe": "game/LibreViesGame.exe", "hash": infos["assembly_sha256"],
    })
    return infos


def main() -> int:
    parser = argparse.ArgumentParser(description="Verifier/preparer uniquement le dossier jeu local")
    parser.add_argument("--jeu", type=Path, default=ROOT / "jeu")
    parser.add_argument("--export", type=Path)
    parser.add_argument("--verifier-seulement", action="store_true")
    args = parser.parse_args()
    try:
        attendue = version_sources()
        if args.verifier_seulement:
            valider_export(args.export or args.jeu / "game", attendue)
        else:
            preparer(args.jeu, attendue)
    except (OSError, ValueError) as erreur:
        print("ERREUR export local : " + str(erreur))
        return 1
    print("OK : export local v%s verifie (pseudo et poignees presents)." % attendue)
    if not args.verifier_seulement:
        print("Ouvrir %s : aucune publication necessaire." % (args.jeu / "LibreVies.exe"))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
