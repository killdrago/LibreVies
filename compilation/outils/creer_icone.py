"""Recree l'icone de l'application depuis le launcher.

Le launcher embarque deja son icone (ICO_B64) : inutile de garder un fichier
.ico dans le depot, il est reextrait a chaque build.

Utilise par build_launcher.bat avant PyInstaller :

    python outils\\creer_icone.py ..\\..\\jeu\\launcher.pyw icon.ico

Sortie 0 = icone valide (le build l'utilise), 1 = pas d'icone (le build
continue sans icone plutot que d'echouer).
"""
from __future__ import annotations

import importlib.machinery
import importlib.util
import sys
from pathlib import Path

ENTETE_ICO = b"\x00\x00\x01\x00"
TAILLE_MINIMALE = 1000


def charger_launcher(chemin: Path):
    """Charge launcher.pyw (extension .pyw : le chargeur est force)."""
    chargeur = importlib.machinery.SourceFileLoader("librevies_launcher", str(chemin))
    spec = importlib.util.spec_from_loader("librevies_launcher", chargeur)
    if spec is None:
        raise ImportError("lecture impossible de %s" % chemin)
    module = importlib.util.module_from_spec(spec)
    chargeur.exec_module(module)
    return module


def main() -> int:
    if len(sys.argv) < 2:
        print("usage : creer_icone.py <launcher.pyw> [icon.ico]")
        return 2
    launcher = Path(sys.argv[1]).resolve()
    icone = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else Path("icon.ico").resolve()

    if not launcher.is_file():
        print("ERREUR : launcher introuvable : %s" % launcher)
        return 1
    try:
        module = charger_launcher(launcher)
    except Exception as erreur:                       # tkinter absent, fichier casse...
        print("ERREUR : impossible de lire le launcher (%s)" % erreur)
        return 1
    if not hasattr(module, "export_icon"):
        print("ERREUR : le launcher ne contient pas d'icone (export_icon absent)")
        return 1

    try:
        module.export_icon(str(icone))
    except Exception as erreur:
        print("ERREUR : ecriture de l'icone impossible (%s)" % erreur)
        return 1

    try:
        donnees = icone.read_bytes()
    except OSError as erreur:
        print("ERREUR : icone illisible (%s)" % erreur)
        return 1
    if donnees[:4] != ENTETE_ICO or len(donnees) < TAILLE_MINIMALE:
        print("ERREUR : %s n'est pas une icone Windows valide" % icone)
        return 1

    print("Icone prete : %s (%d octets)" % (icone, len(donnees)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
