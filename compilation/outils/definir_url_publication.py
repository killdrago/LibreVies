"""Change la branche que le launcher et le script de build utilisent.

Trois endroits doivent rester d'accord :
  * compilation/build_launcher.bat -> set "BRANCHE=..." (branche d'ou le script
    telecharge tout seul le projet quand il est absent) ;
  * jeu/version_url.json           -> champs raw_url / game_url (le manifeste) ;
  * jeu/launcher.pyw               -> constante DEFAULT_RAW_URL (valeur de
    secours, quand aucun version_url.json n'existe encore chez le joueur).

Exemple : apres avoir fusionne le travail dans main,

    python outils\\definir_url_publication.py --branche main

Le script relit chaque fichier modifie, verifie qu'il reste valide (le launcher
doit encore compiler, le .bat doit garder ses etiquettes) et revient en arriere
sinon : impossible de casser un fichier par accident.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

RACINE = Path(__file__).resolve().parents[2]
MANIFESTE = RACINE / "jeu" / "version_url.json"
LAUNCHER = RACINE / "jeu" / "launcher.pyw"
SCRIPT_BUILD = RACINE / "compilation" / "build_launcher.bat"
DEPOT = "killdrago/LibreVies"

# etiquettes que le script de build doit toujours contenir
ETIQUETTES_BUILD = (":etape_projet", ":etape_python", ":etape_pyinstaller",
                    ":etape_unity", ":echec", ":fin")


def remplacer_constante_launcher(source: str, depot: str, branche: str,
                                 dossier: str) -> str:
    """Remplace la seule instruction DEFAULT_RAW_URL (jamais le reste)."""
    lignes = source.splitlines(keepends=True)
    debut = next((i for i, ligne in enumerate(lignes)
                  if ligne.startswith("DEFAULT_RAW_URL")), None)
    if debut is None:
        raise SystemExit("DEFAULT_RAW_URL introuvable dans %s" % LAUNCHER)
    profondeur = 0
    fin = debut
    while fin < len(lignes):
        profondeur += lignes[fin].count("(") - lignes[fin].count(")")
        if profondeur <= 0:
            break
        fin += 1
    remplacement = [
        'DEFAULT_RAW_URL = ("https://raw.githubusercontent.com/%s/"\n' % depot,
        '                   "%s/%s")\n' % (branche, dossier),
    ]
    lignes[debut:fin + 1] = remplacement
    return "".join(lignes)


def remplacer_branche_build(source: str, branche: str) -> str:
    """Remplace la seule ligne set "BRANCHE=..." du script de build."""
    lignes = source.split("\n")
    motif = re.compile(r'^set "BRANCHE=[^"]*"')
    for i, ligne in enumerate(lignes):
        if motif.match(ligne):
            lignes[i] = 'set "BRANCHE=%s"' % branche
            return "\n".join(lignes)
    raise SystemExit("ligne BRANCHE introuvable dans %s" % SCRIPT_BUILD)


def main() -> int:
    parseur = argparse.ArgumentParser(description="Branche publiee par LibreVies")
    parseur.add_argument("--branche", required=True,
                         help="branche GitHub qui contient jeu/ et compilation/")
    parseur.add_argument("--depot", default=DEPOT)
    parseur.add_argument("--dossier", default="jeu",
                         help="dossier du depot ou se trouve le manifeste")
    args = parseur.parse_args()

    branche = args.branche.strip().strip("/")
    dossier = args.dossier.strip().strip("/")
    raw = "https://raw.githubusercontent.com/%s/%s/%s" % (args.depot, branche, dossier)
    tree = "https://github.com/%s/tree/%s/%s" % (args.depot, branche, dossier)

    # 1. le manifeste
    manifeste = json.loads(MANIFESTE.read_text(encoding="utf-8"))
    manifeste["raw_url"] = raw
    manifeste["game_url"] = tree
    MANIFESTE.write_text(json.dumps(manifeste, indent=2, ensure_ascii=False) + "\n",
                         encoding="utf-8", newline="\n")
    print("manifeste  : %s" % MANIFESTE.relative_to(RACINE))
    print("  raw_url  : %s" % raw)

    # 2. le launcher (valeur de secours)
    source = LAUNCHER.read_text(encoding="utf-8")
    nouvelle = remplacer_constante_launcher(source, args.depot, branche, dossier)
    if abs(nouvelle.count("\n") - source.count("\n")) > 3:
        print("ERREUR : le remplacement a touche trop de lignes, rien n'est ecrit.")
        return 1
    try:
        compile(nouvelle, str(LAUNCHER), "exec")
    except SyntaxError as erreur:
        print("ERREUR : le launcher modifie ne compile plus (%s), rien n'est ecrit."
              % erreur)
        return 1
    LAUNCHER.write_text(nouvelle, encoding="utf-8", newline="\n")
    print("launcher   : %s" % LAUNCHER.relative_to(RACINE))
    print("  DEFAULT_RAW_URL : %s" % raw)

    # 3. le script de build (telechargement automatique du projet)
    if SCRIPT_BUILD.is_file():
        source_bat = SCRIPT_BUILD.read_text(encoding="utf-8")
        nouveau_bat = remplacer_branche_build(source_bat, branche)
        if nouveau_bat.count("\n") != source_bat.count("\n"):
            print("ERREUR : le script de build a ete altere, rien n'est ecrit.")
            return 1
        perdues = [e for e in ETIQUETTES_BUILD if e not in nouveau_bat]
        if perdues:
            print("ERREUR : etiquettes perdues (%s), rien n'est ecrit."
                  % ", ".join(perdues))
            return 1
        SCRIPT_BUILD.write_text(nouveau_bat, encoding="utf-8", newline="\n")
        print("build      : %s" % SCRIPT_BUILD.relative_to(RACINE))
        print("  BRANCHE  : %s" % branche)
    else:
        print("build      : %s absent, ignore." % SCRIPT_BUILD.relative_to(RACINE))

    print()
    print("Pense a envoyer les fichiers modifies :")
    print("  git add jeu/version_url.json jeu/launcher.pyw compilation/build_launcher.bat")
    print('  git commit -m "Publication depuis la branche %s"' % branche)
    print("  git push")
    return 0


if __name__ == "__main__":
    sys.exit(main())
