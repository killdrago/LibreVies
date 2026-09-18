"""Publie une compilation du jeu pour le launcher LibreVies.

Etapes (tout est automatique) :
  1. met le dossier du jeu (release/game) dans UNE archive .zip ;
  2. envoie l'archive dans la release GitHub « derniere » (gh CLI) ;
  3. met a jour jeu/version_url.json : url, taille et md5 de l'archive.

Le joueur n'utilise jamais ce script : il ne recoit que le launcher, qui lit
le manifeste et telecharge l'archive ici publiee.

Exemple (Windows, depuis le dossier compilation) :

    python outils\\publier_jeu.py --jeu release\\game --version 0.5.0 ^
        --notes "Village Unity + camera corrigee" --exe release\\LibreVies.exe

Sans gh installe, le script fabrique quand meme l'archive et met le manifeste
a jour : il suffit alors d'envoyer le .zip a la main a l'URL indiquee.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

RACINE = Path(__file__).resolve().parents[2]      # racine du depot
MANIFESTE = RACINE / "jeu" / "version_url.json"
LAUNCHER = RACINE / "jeu" / "launcher.pyw"
DOSSIER_BUILD = RACINE / "compilation" / "build"
# Le jeu est une compilation Unity : son executable s'appelle LibreViesGame.exe.
NOMS_EXE = ("LibreViesGame.exe",)


def md5_fichier(chemin: Path) -> str:
    digest = hashlib.md5()
    with chemin.open("rb") as flux:
        for bloc in iter(lambda: flux.read(1024 * 1024), b""):
            digest.update(bloc)
    return digest.hexdigest()


def lire_version_launcher() -> str:
    try:
        texte = LAUNCHER.read_text(encoding="utf-8")
        trouve = re.search(r'^LAUNCHER_VERSION\s*=\s*"([^"]+)"', texte, re.M)
        return trouve.group(1) if trouve else ""
    except OSError:
        return ""


def trouver_exe(dossier: Path) -> Path | None:
    """Cherche l'executable du jeu, a la racine ou un niveau en dessous."""
    for nom in NOMS_EXE:
        candidat = dossier / nom
        if candidat.is_file():
            return candidat
    for sous in sorted(dossier.iterdir()):
        if sous.is_dir():
            for nom in NOMS_EXE:
                candidat = sous / nom
                if candidat.is_file():
                    return candidat
    return None


def creer_archive(dossier_jeu: Path, destination: Path) -> tuple[int, str]:
    """Compresse le CONTENU du dossier (les fichiers sont a la racine du zip)."""
    fichiers = [p for p in sorted(dossier_jeu.rglob("*")) if p.is_file()]
    if not fichiers:
        raise SystemExit("Dossier de jeu vide : %s" % dossier_jeu)
    destination.parent.mkdir(parents=True, exist_ok=True)
    total = 0
    with zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for chemin in fichiers:
            z.write(chemin, chemin.relative_to(dossier_jeu).as_posix())
            total += 1
    return total, md5_fichier(destination)


def gh_disponible() -> bool:
    return shutil.which("gh") is not None


def envoyer_release(fichier: Path, tag: str, depot: str) -> bool:
    """Envoie (ou remplace) un fichier dans la release GitHub."""
    creation = subprocess.run(
        ["gh", "release", "view", tag, "--repo", depot],
        capture_output=True, text=True)
    if creation.returncode != 0:
        print("  release « %s » absente : creation..." % tag)
        subprocess.run([
            "gh", "release", "create", tag, "--repo", depot,
            "--title", "LibreVies — compilation la plus recente",
            "--notes", "Compilations Windows publiees automatiquement. "
                       "Le launcher les telecharge et les installe tout seul.",
        ], check=False)
    envoi = subprocess.run(
        ["gh", "release", "upload", tag, str(fichier), "--repo", depot, "--clobber"],
        text=True)
    return envoi.returncode == 0


def main() -> int:
    parseur = argparse.ArgumentParser(description="Publier une compilation LibreVies")
    parseur.add_argument("--jeu", type=Path, required=True,
                         help="dossier du jeu exporte (ex. release/game)")
    parseur.add_argument("--version", required=True, help="version du jeu (ex. 0.5.0)")
    parseur.add_argument("--notes", default="", help="texte affiche dans le launcher")
    parseur.add_argument("--exe", type=Path, default=None,
                         help="launcher compile (release/LibreVies.exe) a publier aussi")
    parseur.add_argument("--tag", default="derniere", help="release GitHub (defaut : derniere)")
    parseur.add_argument("--depot", default="killdrago/LibreVies", help="depot GitHub")
    parseur.add_argument("--sans-upload", action="store_true",
                         help="fabriquer l'archive et le manifeste sans rien envoyer")
    parseur.add_argument("--pousser", action="store_true",
                         help="git add/commit/push du manifeste a la fin")
    args = parseur.parse_args()

    dossier_jeu = args.jeu.resolve()
    if not dossier_jeu.is_dir():
        print("ERREUR : dossier de jeu introuvable : %s" % dossier_jeu)
        return 1
    exe = trouver_exe(dossier_jeu)
    if not exe:
        print("ERREUR : aucun LibreViesGame.exe dans %s (export Unity manquant ?)"
              % dossier_jeu)
        return 1
    moteur = "unity"

    print("== 1. archive du jeu ==")
    provisoire = DOSSIER_BUILD / "jeu_tmp.zip"
    nb, _ = creer_archive(dossier_jeu, provisoire)
    somme = md5_fichier(provisoire)
    nom_archive = "LibreVies_jeu_%s.zip" % somme[:8]
    archive = DOSSIER_BUILD / nom_archive
    provisoire.replace(archive)
    taille = archive.stat().st_size
    print("   %d fichiers, %s Mo, md5 %s" % (nb, "%.1f" % (taille / 1048576.0), somme))
    print("   archive : %s" % archive)

    url_archive = ("https://github.com/%s/releases/download/%s/%s"
                   % (args.depot, args.tag, nom_archive))

    envoye = False
    if args.sans_upload:
        print("== 2. envoi ignore (--sans-upload) ==")
    elif gh_disponible():
        print("== 2. envoi dans la release « %s » ==" % args.tag)
        envoye = envoyer_release(archive, args.tag, args.depot)
        if envoye:
            print("   archive envoyee : %s" % url_archive)
        else:
            print("   ATTENTION : envoi refuse par gh. Envoie le fichier a la main.")
    else:
        print("== 2. gh (GitHub CLI) absent : envoi manuel necessaire ==")
        print("   Envoie ce fichier dans la release « %s » de %s :" % (args.tag, args.depot))
        print("     %s" % archive)
        print("   Il doit s'appeler exactement : %s" % nom_archive)

    print("== 3. manifeste du launcher ==")
    manifeste = json.loads(MANIFESTE.read_text(encoding="utf-8"))
    manifeste["game_version"] = args.version
    if args.notes:
        manifeste["notes"] = args.notes
    manifeste["launcher_version"] = lire_version_launcher() or manifeste.get("launcher_version", "")
    manifeste["game_build"] = {
        "moteur": moteur,
        "version": args.version,
        "dossier": "game",
        "exe": str(exe.relative_to(dossier_jeu)).replace("\\", "/"),
        "url": url_archive,
        "size": taille,
        "hash": somme,
    }
    fichiers = manifeste.get("files")
    if not isinstance(fichiers, dict):
        fichiers = {}
        manifeste["files"] = fichiers

    if args.exe:
        exe_launcher = args.exe.resolve()
        if not exe_launcher.is_file():
            print("ERREUR : launcher introuvable : %s" % exe_launcher)
            return 1
        nom_asset = "LibreVies_%s.exe" % (lire_version_launcher() or args.version)
        url_launcher = ("https://github.com/%s/releases/download/%s/%s"
                        % (args.depot, args.tag, nom_asset))
        fichiers["LibreVies.exe"] = {
            "url": url_launcher,
            "hash": md5_fichier(exe_launcher),
            "size": exe_launcher.stat().st_size,
        }
        print("   launcher publie : %s (%s)" % (nom_asset, url_launcher))
        if not args.sans_upload and gh_disponible():
            envoyer_release(exe_launcher, args.tag, args.depot)

    MANIFESTE.write_text(json.dumps(manifeste, indent=2, ensure_ascii=False) + "\n",
                         encoding="utf-8", newline="\n")
    print("   %s mis a jour" % MANIFESTE.relative_to(RACINE))

    print()
    print("== resume ==")
    print("   jeu     : %s (Unity, %s Mo)" % (args.version, "%.1f" % (taille / 1048576.0)))
    print("   archive : %s" % nom_archive)
    print("   url     : %s" % url_archive)
    if not envoye:
        print()
        print("   A FAIRE : deposer cette archive a l'URL ci-dessus")
        print("   (release « %s » du depot %s), nom exact : %s"
              % (args.tag, args.depot, nom_archive))
        print("   Sinon les joueurs ne pourront pas telecharger la nouvelle version.")

    if args.pousser:
        print("== 4. envoi du manifeste dans git ==")
        for commande in (["git", "add", "jeu/version_url.json"],
                         ["git", "commit", "-m",
                          "Publication du jeu %s : manifeste du launcher mis a jour" % args.version],
                         ["git", "push"]):
            resultat = subprocess.run(commande, cwd=str(RACINE), text=True)
            if resultat.returncode != 0:
                print("   ATTENTION : « %s » a echoue" % " ".join(commande))
                return 1
    else:
        print("   Pense a envoyer le manifeste : git add jeu/version_url.json"
              " && git commit -m \"Publication %s\" && git push" % args.version)
    print("TERMINE")
    return 0


if __name__ == "__main__":
    sys.exit(main())
