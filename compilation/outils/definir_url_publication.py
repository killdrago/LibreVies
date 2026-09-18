"""Change la branche que le launcher interroge pour ses mises a jour.

Deux endroits doivent rester d'accord :
  * jeu/version_url.json  -> champs raw_url / game_url (le manifeste) ;
  * jeu/launcher.pyw      -> constante DEFAULT_RAW_URL (valeur de secours,
    utilisee quand aucun version_url.json n'existe encore chez le joueur).

Exemple : apres avoir fusionne le travail dans main,

    python outils\\definir_url_publication.py --branche main

Le script relit le launcher apres modification, verifie qu'il compile encore,
et revient en arriere si ce n'est pas le cas : impossible de casser le fichier
par accident.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

RACINE = Path(__file__).resolve().parents[2]
MANIFESTE = RACINE / "jeu" / "version_url.json"
LAUNCHER = RACINE / "jeu" / "launcher.pyw"
DEPOT = "killdrago/LibreVies"


def remplacer_constante(source: str, depot: str, branche: str, dossier: str) -> str:
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


def main() -> int:
    parseur = argparse.ArgumentParser(description="Branche publiee par le launcher")
    parseur.add_argument("--branche", required=True,
                         help="branche GitHub qui contient le dossier jeu/ (ex. main)")
    parseur.add_argument("--depot", default=DEPOT)
    parseur.add_argument("--dossier", default="jeu",
                         help="dossier du depot ou se trouve le manifeste")
    args = parseur.parse_args()

    branche = args.branche.strip().strip("/")
    dossier = args.dossier.strip().strip("/")
    raw = "https://raw.githubusercontent.com/%s/%s/%s" % (args.depot, branche, dossier)
    tree = "https://github.com/%s/tree/%s/%s" % (args.depot, branche, dossier)

    manifeste = json.loads(MANIFESTE.read_text(encoding="utf-8"))
    manifeste["raw_url"] = raw
    manifeste["game_url"] = tree
    MANIFESTE.write_text(json.dumps(manifeste, indent=2, ensure_ascii=False) + "\n",
                         encoding="utf-8", newline="\n")
    print("manifeste : %s" % MANIFESTE.relative_to(RACINE))
    print("  raw_url : %s" % raw)

    source = LAUNCHER.read_text(encoding="utf-8")
    nouvelle = remplacer_constante(source, args.depot, branche, dossier)

    # Garde-fou : le fichier doit rester compilable et quasi identique.
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
    print("launcher  : %s" % LAUNCHER.relative_to(RACINE))
    print("  DEFAULT_RAW_URL : %s" % raw)

    print()
    print("Pense a envoyer les deux fichiers :")
    print("  git add jeu/version_url.json jeu/launcher.pyw")
    print('  git commit -m "Publication depuis la branche %s"' % branche)
    print("  git push")
    return 0


if __name__ == "__main__":
    sys.exit(main())
