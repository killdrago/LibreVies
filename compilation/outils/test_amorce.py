"""Recette de l'amorce du launcher — a lancer sur la machine de compilation.

    python compilation\\outils\\test_amorce.py

Rappel du principe : LibreVies.exe n'est qu'une petite AMORCE compilee depuis
compilation\\amorce\\amorce_launcher.py. Elle ne contient aucune logique du
launcher : elle execute le fichier launcher.pyw place a cote d'elle (qui, lui,
se met a jour tout seul par son hash). Cette recette verifie les cinq cas qui
comptent :

  1. le code du launcher est bien execute, comme s'il etait lance directement ;
  2. au premier lancement, la copie embarquee dans l'executable est recopiee ;
  3. si le code manque partout, un message clair s'affiche (pas de plantage) ;
  4. si le launcher plante, un journal exploitable est ecrit a cote de l'exe ;
  5. SystemExit traverse l'amorce (le launcher peut fermer proprement).

Aucun fichier du depot n'est modifie : tout se passe dans des dossiers
temporaires.
"""
from __future__ import annotations

import importlib.util
import os
import sys
import tempfile
from pathlib import Path

RACINE = Path(__file__).resolve().parents[2]
SOURCE = RACINE / "compilation" / "amorce" / "amorce_launcher.py"
echecs: list[str] = []


def verifier(condition, message):
    print(("  OK   " if condition else "  ECHEC ") + message)
    if not condition:
        echecs.append(message)


def charger():
    """Charge l'amorce comme si elle etait compilee dans l'executable."""
    spec = importlib.util.spec_from_file_location("amorce_test", str(SOURCE))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def ecrire(chemin, texte):
    with open(chemin, "w", encoding="utf-8", newline="\n") as f:
        f.write(texte)


def lire(chemin):
    with open(chemin, encoding="utf-8") as f:
        return f.read()


def main() -> int:
    if not SOURCE.is_file():
        print("ERREUR : amorce introuvable : %s" % SOURCE)
        return 1
    print("amorce testee : %s" % SOURCE.relative_to(RACINE))

    print("== 1. le code du launcher est bien execute ==")
    dossier = tempfile.mkdtemp(prefix="amorce1-")
    ecrire(os.path.join(dossier, "launcher.pyw"),
           "import os\n"
           "open(os.path.join(os.path.dirname(__file__), 'vu.txt'), 'w')"
           ".write(__name__ + '|' + os.path.basename(__file__))\n")
    module = charger()
    module.dossier_application = lambda: dossier
    code = module.principal()
    verifier(code == 0, "code de retour 0 (execution terminee)")
    verifier(os.path.isfile(os.path.join(dossier, "vu.txt")),
             "le launcher a bien tourne")
    verifier(lire(os.path.join(dossier, "vu.txt")) == "__main__|launcher.pyw",
             "il tourne comme un programme (__name__, __file__ corrects)")

    print("== 2. premier lancement : copie embarquee ==")
    dossier2 = tempfile.mkdtemp(prefix="amorce2-")
    embarque = tempfile.mkdtemp(prefix="amorce-embarque-")
    ecrire(os.path.join(embarque, "launcher.pyw"),
           "open(__file__ + '.ok', 'w').write('embarque')\n")
    module2 = charger()
    module2.dossier_application = lambda: dossier2
    module2.dossier_embarque = lambda: embarque
    code = module2.principal()
    verifier(code == 0, "code de retour 0 avec la copie embarquee")
    verifier(os.path.isfile(os.path.join(dossier2, "launcher.pyw")),
             "launcher.pyw est recopie a cote de l'executable")
    verifier(os.path.isfile(os.path.join(dossier2, "launcher.pyw.ok")),
             "c'est bien la copie recopiee qui a tourne")

    print("== 3. aucun code du launcher (message clair) ==")
    dossier3 = tempfile.mkdtemp(prefix="amorce3-")
    module3 = charger()
    module3.dossier_application = lambda: dossier3
    module3.dossier_embarque = lambda: tempfile.mkdtemp(prefix="vide-")
    messages = []
    module3.alerte = lambda texte: messages.append(texte)
    code = module3.principal()
    verifier(code == 2, "code de retour 2 (pas de plantage)")
    verifier(bool(messages) and "introuvable" in messages[0],
             "un message explique quoi faire")

    print("== 4. plantage du launcher : journal exploitable ==")
    dossier4 = tempfile.mkdtemp(prefix="amorce4-")
    ecrire(os.path.join(dossier4, "launcher.pyw"), "x = 1 / 0\n")
    module4 = charger()
    module4.dossier_application = lambda: dossier4
    messages4 = []
    module4.alerte = lambda texte: messages4.append(texte)
    code = module4.principal()
    journal = os.path.join(dossier4, "journal_launcher.txt")
    verifier(code == 1, "code de retour 1 en cas d'erreur")
    verifier(os.path.isfile(journal), "journal_launcher.txt est ecrit")
    contenu = lire(journal) if os.path.isfile(journal) else ""
    verifier("ZeroDivisionError" in contenu, "le journal contient la cause reelle")
    verifier(bool(messages4) and "ZeroDivisionError" in messages4[0],
             "l'alerte affiche la cause")

    print("== 5. SystemExit traverse l'amorce ==")
    dossier5 = tempfile.mkdtemp(prefix="amorce5-")
    ecrire(os.path.join(dossier5, "launcher.pyw"), "raise SystemExit(7)\n")
    module5 = charger()
    module5.dossier_application = lambda: dossier5
    try:
        module5.principal()
        verifier(False, "SystemExit remonte jusqu'a l'appelant")
    except SystemExit as sortie:
        verifier(sortie.code == 7, "SystemExit remonte jusqu'a l'appelant")

    print()
    if echecs:
        print("ECHECS (%d) :" % len(echecs))
        for message in echecs:
            print(" - %s" % message)
        return 1
    print("TOUS LES TESTS DE L'AMORCE PASSENT")
    return 0


if __name__ == "__main__":
    sys.exit(main())
