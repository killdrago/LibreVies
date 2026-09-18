"""Recette du launcher LibreVies — a lancer sur la machine de compilation.

    python outils\\test_launcher.py

Elle simule un joueur : un serveur HTTP local publie version_url.json et une
archive du jeu, exactement comme la release GitHub. On verifie que le launcher
installe le jeu, reprend un telechargement coupe, refuse une archive corrompue
et ne se laisse pas sortir de son dossier par une archive piegee.
Aucun fichier du depot n'est modifie : tout se passe dans un dossier temporaire.
"""
from __future__ import annotations

import hashlib
import importlib.machinery
import importlib.util
import json
import os
import shutil
import sys
import tempfile
import threading
import types
import zipfile
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

RACINE = Path(__file__).resolve().parents[2]
PORT = 8791
echecs: list[str] = []


def verifier(condition, message):
    print(("  OK   " if condition else "  ECHEC ") + message)
    if not condition:
        echecs.append(message)


class FauxTkinter(types.ModuleType):
    """tkinter n'est pas necessaire pour tester la logique du launcher."""

    def __init__(self):
        super().__init__("tkinter")

        class Widget:
            def __init__(self, *a, **k):
                pass

        self.Tk = Widget
        for nom in ("Canvas", "Frame", "Label", "Button", "Entry", "Checkbutton",
                    "BooleanVar", "PhotoImage", "StringVar", "IntVar"):
            setattr(self, nom, Widget)


class Serveur:
    def __init__(self, dossier: Path, port: int = PORT):
        self.dossier = dossier
        gestionnaire = lambda *a, **k: SimpleHTTPRequestHandler(
            *a, directory=str(dossier), **k)
        self.httpd = ThreadingHTTPServer(("127.0.0.1", port), gestionnaire)
        threading.Thread(target=self.httpd.serve_forever, daemon=True).start()

    def arreter(self):
        self.httpd.shutdown()


def creer_archive_jeu(chemin: Path, contenu_sup: str = "", piege: bool = False):
    with zipfile.ZipFile(chemin, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("LibreViesGame.exe", b"MZ faux executable " + b"\0" * 100000)
        z.writestr("UnityPlayer.dll", b"DLL runtime" + b"\1" * 50000)
        z.writestr("LibreViesGame_Data/level0", b"donnees de niveau" * 500)
        if contenu_sup:
            z.writestr("Version.txt", contenu_sup)
        if piege:
            z.writestr("../evade.txt", b"interdit")


def ecrire_manifeste(dossier: Path, archive: Path, version: str, port: int = PORT,
                     hash_force: str = "", taille_force: int | None = None):
    donnees = archive.read_bytes()
    manifeste = {
        "launcher_version": "4.0.0",
        "game_version": version,
        "notes": "recette",
        "raw_url": "http://127.0.0.1:%d" % port,
        "files": {},
        "game_build": {
            "moteur": "unity",
            "version": version,
            "dossier": "game",
            "exe": "LibreViesGame.exe",
            "url": "http://127.0.0.1:%d/%s" % (port, archive.name),
            "size": taille_force if taille_force is not None else len(donnees),
            "hash": hash_force or hashlib.md5(donnees).hexdigest(),
        },
    }
    (dossier / "version_url.json").write_text(
        json.dumps(manifeste, indent=2, ensure_ascii=False), encoding="utf-8")
    return manifeste


def charger_launcher(dossier_jeu: Path):
    sys.modules["tkinter"] = FauxTkinter()
    chemin = dossier_jeu / "launcher.pyw"
    chargeur = importlib.machinery.SourceFileLoader("lv_launcher", str(chemin))
    spec = importlib.util.spec_from_loader("lv_launcher", chargeur)
    module = importlib.util.module_from_spec(spec)
    chargeur.exec_module(module)
    return module


def progressions():
    def cb(pct, texte):
        print("        %3d%%  %s" % (pct, texte))
    return cb


def main() -> int:
    temporaire = Path(tempfile.mkdtemp(prefix="librevies-recette-"))
    jeu = temporaire / "jeu"
    publication = temporaire / "publication"
    jeu.mkdir()
    publication.mkdir()
    shutil.copy(RACINE / "jeu" / "launcher.pyw", jeu / "launcher.pyw")
    (jeu / "version_url.json").write_text(
        json.dumps({"raw_url": "http://127.0.0.1:%d" % PORT}), encoding="utf-8")
    print("dossier de test : %s" % temporaire)

    archive1 = publication / "LibreVies_jeu_v1.zip"
    creer_archive_jeu(archive1)
    ecrire_manifeste(publication, archive1, "1.0.0")
    serveur = Serveur(publication)
    launcher = charger_launcher(jeu)
    cb = progressions()

    print("== 1. premier lancement : rien d'installe ==")
    resultat = launcher.check_for_updates(cb)
    verifier(resultat["error"] is None, "manifeste lu depuis le serveur")
    a_installer, raison = launcher.build_a_installer(resultat["build"])
    verifier(a_installer, "telechargement necessaire (%s)" % raison)
    verifier(launcher.find_game() is None, "aucun jeu installe au depart")

    print("== 2. telechargement + installation ==")
    ok, message, exe = launcher.installer_build_jeu(resultat["build"], cb)
    verifier(ok, "installation reussie (%s)" % message)
    verifier(bool(exe) and os.path.isfile(exe), "executable en place : %s" % exe)
    verifier((jeu / "game" / "UnityPlayer.dll").is_file(), "runtime installe")
    verifier((jeu / "game" / "LibreViesGame_Data" / "level0").is_file(),
             "dossier de donnees installe")
    verifier(launcher.find_game() == os.path.abspath(exe), "find_game retrouve le jeu")
    verifier((jeu / "etat_jeu.json").is_file(), "etat de l'installation enregistre")
    verifier(not (jeu / "jeu.download.part").exists(),
             "fichier temporaire nettoye apres installation")

    print("== 3. deuxieme lancement : rien a faire ==")
    resultat = launcher.check_for_updates(cb)
    a_installer, raison = launcher.build_a_installer(resultat["build"])
    verifier(not a_installer, "jeu deja a jour (%s)" % raison)

    print("== 4. nouvelle compilation publiee ==")
    archive2 = publication / "LibreVies_jeu_v2.zip"
    creer_archive_jeu(archive2, contenu_sup="nouvelle compilation 2.0.0")
    ecrire_manifeste(publication, archive2, "2.0.0")
    resultat = launcher.check_for_updates(cb)
    a_installer, raison = launcher.build_a_installer(resultat["build"])
    verifier(a_installer, "nouvelle compilation detectee (%s)" % raison)
    ok, message, exe = launcher.installer_build_jeu(resultat["build"], cb)
    verifier(ok, "mise a jour installee (%s)" % message)
    verifier(launcher.lire_etat_jeu().get("version") == "2.0.0", "version enregistree")
    verifier(not (jeu / "game.ancien").exists(), "ancienne installation nettoyee")

    print("== 5. telechargement coupe puis repris ==")
    (jeu / "etat_jeu.json").unlink()
    donnees = archive2.read_bytes()
    (jeu / "jeu.download.part").write_bytes(donnees[:len(donnees) // 3])
    ok, message, exe = launcher.installer_build_jeu(resultat["build"], cb)
    verifier(ok, "reprise automatique (%s)" % message)
    verifier(not (jeu / "jeu.download.part").exists(), "fichier partiel nettoye")

    print("== 6. archive corrompue : le jeu installe est preserve ==")
    ecrire_manifeste(publication, archive2, "3.0.0", hash_force="0" * 32)
    resultat = launcher.check_for_updates(cb)
    ancien_exe = launcher.find_game()
    ok, message, _ = launcher.installer_build_jeu(resultat["build"], cb)
    verifier(not ok, "archive refusee (%s)" % message[:60])
    verifier(launcher.find_game() == ancien_exe, "jeu installe toujours utilisable")

    print("== 7. archive piegee (chemin ../) ==")
    piege = publication / "LibreVies_piege.zip"
    creer_archive_jeu(piege, piege=True)
    ecrire_manifeste(publication, piege, "4.0.0")
    resultat = launcher.check_for_updates(cb)
    ok, message, _ = launcher.installer_build_jeu(resultat["build"], cb)
    verifier(not ok, "archive piegee refusee (%s)" % message[:60])
    verifier(not (temporaire / "evade.txt").exists(),
             "aucun fichier ecrit hors du dossier du jeu")

    print("== 8. manifeste et chemins ==")
    cfg = {"files": {"launcher.pyw": {"hash": "x"}, "LibreVies.exe": {"hash": "y"},
                     "LIS-MOI.txt": {"hash": "z"}}}
    verifier(sorted(launcher._manifest_files(cfg)) == ["LIS-MOI.txt"],
             "sources et exe ignores en mode developpement")
    verifier(launcher.normalize_raw_url(
        "https://github.com/killdrago/LibreVies/tree/main/jeu")
        == "https://raw.githubusercontent.com/killdrago/LibreVies/main/jeu",
        "URL GitHub convertie en URL raw")
    for mauvais in ("../hors.txt", "/etc/passwd", "C:/windows/system32/x.dll"):
        try:
            launcher._safe_local_path(mauvais)
            verifier(False, "chemin interdit accepte : %s" % mauvais)
        except ValueError:
            verifier(True, "chemin interdit refuse : %s" % mauvais)

    print("== 9. lancement du jeu ==")
    ecrire_manifeste(publication, archive2, "2.0.0")
    resultat = launcher.check_for_updates(cb)
    ok, message, exe = launcher.installer_build_jeu(resultat["build"], cb)
    appels = {}
    vrai_popen = launcher.subprocess.Popen
    launcher.subprocess.Popen = lambda args, **k: appels.update(
        {"args": args, "cwd": k.get("cwd")})
    launcher.launch(exe)
    launcher.subprocess.Popen = vrai_popen
    verifier(appels.get("args") == [exe], "le jeu est lance par son chemin complet")
    verifier(appels.get("cwd") == os.path.dirname(exe),
             "le jeu demarre dans son propre dossier")

    serveur.arreter()
    print()
    if echecs:
        print("ECHECS (%d) :" % len(echecs))
        for echec in echecs:
            print(" -", echec)
        return 1
    print("TOUS LES TESTS PASSENT")
    print("dossier de test (a supprimer) : %s" % temporaire)
    return 0


if __name__ == "__main__":
    sys.exit(main())
