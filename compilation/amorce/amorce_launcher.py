"""
LibreVies — Amorce du launcher.

Ce fichier est compile en jeu\\LibreVies.exe. Il ne contient AUCUNE logique
du launcher : il execute simplement le fichier launcher.pyw place a cote de
lui (ou, au premier lancement, la copie embarquee dans l'executable).

Pourquoi cette amorce ?
  * LibreVies.exe ne change pratiquement jamais : il n'y a donc plus besoin
    de reconstruire le launcher a chaque modification ;
  * tout le code du launcher vit dans launcher.pyw, qui se met a jour tout
    seul par son hash (version_url.json) ;
  * le joueur n'a jamais rien a remplacer a la main.

Modules importes ci-dessous : ils sont importes pour qu'ils soient EMBARQUES
dans l'executable. En effet le code de launcher.pyw est charge dynamiquement :
PyInstaller ne peut pas deviner ses imports. Tout import ajoute a
launcher.pyw doit donc etre ajoute ici aussi (sinon l'executable ne le
contiendra pas).
"""
import os
import sys
import traceback

# --- bibliotheque standard utilisee par le launcher -------------------------
# (imports volontairement litteraux : c'est ce que PyInstaller analyse)
try:
    import subprocess
except ImportError:
    pass
try:
    import threading
except ImportError:
    pass
try:
    import time
except ImportError:
    pass
try:
    import urllib.request
except ImportError:
    pass
try:
    import hashlib
except ImportError:
    pass
try:
    import json
except ImportError:
    pass
try:
    import base64
except ImportError:
    pass
try:
    import io
except ImportError:
    pass
try:
    import tempfile
except ImportError:
    pass
try:
    import shutil
except ImportError:
    pass
try:
    import zipfile
except ImportError:
    pass
try:
    import webbrowser
except ImportError:
    pass
try:
    import ctypes
except ImportError:
    pass

# --- interface graphique ----------------------------------------------------
try:
    import tkinter
except ImportError:
    pass
try:
    import tkinter.ttk
except ImportError:
    pass
try:
    import tkinter.filedialog
except ImportError:
    pass
try:
    import tkinter.messagebox
except ImportError:
    pass
try:
    import tkinter.font
except ImportError:
    pass
try:
    import tkinter.constants
except ImportError:
    pass
try:
    import tkinter.colorchooser
except ImportError:
    pass
try:
    import tkinter.simpledialog
except ImportError:
    pass
try:
    import _tkinter
except ImportError:
    pass

NOM_COEUR = "launcher.pyw"
NOM_JOURNAL = "journal_launcher.txt"


def dossier_application():
    """Dossier ou se trouve LibreVies.exe (et donc launcher.pyw)."""
    if getattr(sys, "frozen", False):
        return os.path.dirname(os.path.abspath(sys.executable))
    return os.path.dirname(os.path.abspath(__file__))


def dossier_embarque():
    """Dossier des fichiers ajoutes avec --add-data (mode onefile)."""
    base = getattr(sys, "_MEIPASS", None)
    if base:
        return base
    return os.path.join(dossier_application(), "amorce")


def installer_coeur(dossier):
    """Retourne le chemin du launcher.pyw a executer.

    Au premier lancement (ou si le fichier a ete supprime), la copie
    embarquee dans l'executable est recopiee a cote de lui.
    """
    destination = os.path.join(dossier, NOM_COEUR)
    if os.path.isfile(destination):
        return destination
    source = os.path.join(dossier_embarque(), NOM_COEUR)
    if not os.path.isfile(source):
        return None
    try:
        with open(source, "rb") as f:
            donnees = f.read()
        with open(destination, "wb") as f:
            f.write(donnees)
        return destination
    except OSError:
        # Dossier protege en ecriture : on execute la copie embarquee.
        return source


def journaliser(texte):
    """Ecrit dans journal_launcher.txt, a cote de l'executable."""
    try:
        chemin = os.path.join(dossier_application(), NOM_JOURNAL)
        with open(chemin, "a", encoding="utf-8", newline="\n") as f:
            f.write(texte.rstrip() + "\n")
        return chemin
    except OSError:
        return None


def alerte(message):
    """Boite de dialogue Windows (sans dependre de tkinter)."""
    try:
        ctypes.windll.user32.MessageBoxW(None, message, "LibreVies", 0x10)
    except Exception:
        try:
            sys.stderr.write(message + "\n")
        except Exception:
            pass


def principal():
    dossier = dossier_application()
    coeur = installer_coeur(dossier)
    if not coeur:
        alerte("Fichier %s introuvable a cote de LibreVies.exe.\n"
               "Recopiez le dossier jeu/ complet, ou relancez "
               "compilation\\build_launcher.bat." % NOM_COEUR)
        return 2

    try:
        with open(coeur, "r", encoding="utf-8") as f:
            source = f.read()
    except OSError as e:
        alerte("Lecture de %s impossible :\n%s" % (NOM_COEUR, e))
        return 3

    # Le code du launcher s'execute comme s'il etait lance directement :
    # __name__ doit valoir "__main__" pour que sa fenetre s'ouvre.
    espace = {"__name__": "__main__", "__file__": coeur,
              "__package__": None, "__spec__": None,
              "__builtins__": __builtins__}
    try:
        exec(compile(source, coeur, "exec"), espace)
    except SystemExit:
        raise
    except BaseException:
        detail = traceback.format_exc()
        chemin = journaliser("--- erreur du launcher ---\n" + detail)
        alerte("Le launcher a rencontre une erreur.\n\n"
               + detail.strip().splitlines()[-1]
               + "\n\nDetail complet : %s" % (chemin or NOM_JOURNAL))
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(principal())
