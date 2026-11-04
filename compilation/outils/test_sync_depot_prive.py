#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Verifie que le build sait lire les sources d'un depot GitHub PRIVE.

Pourquoi cet outil : le build a echoue avec

    Invoke-WebRequest : 404: Not Found
    ERREUR synchronisation differentielle : (404) Introuvable.

Les deux lectures fautives (raw.githubusercontent.com et api.github.com)
etaient anonymes. GitHub repond 404 a une lecture anonyme d'un depot prive,
meme quand le commit existe : seul le Git du poste, qui a deja ses
identifiants, voit le depot. Le build doit donc lire les sources dans la base
Git locale (le "git fetch" vient de la remplir) et ne garder l'API GitHub
qu'en secours, avec un jeton.

Usage :
    python compilation/outils/test_sync_depot_prive.py
"""

import unittest
from pathlib import Path

RACINE = Path(__file__).resolve().parents[2]
BAT = RACINE / "compilation" / "build_launcher.bat"
SYNC = RACINE / "compilation" / "outils" / "synchroniser_sources.ps1"


class DepôtPrivé(unittest.TestCase):
    def setUp(self):
        self.bat = BAT.read_bytes().decode("ascii")
        self.sync = SYNC.read_text(encoding="utf-8")

    def test_outil_de_synchronisation_lu_dans_la_base_git_locale(self):
        # L'outil de synchronisation ne doit plus etre le premier telechargement
        # anonyme : il est extrait du commit deja recupere par git fetch.
        self.assertIn("cat-file -p", self.bat)
        self.assertLess(self.bat.index("cat-file -p"),
                        self.bat.index('LV_SYNC_URL=https://raw.githubusercontent.com'))

    def test_synchronisation_des_sources_par_git_d_abord(self):
        self.assertIn("ls-tree -r", self.sync)
        self.assertIn("cat-file blob", self.sync)
        self.assertIn("Sync-DepuisGit", self.sync)
        self.assertIn("Sync-DepuisGithub", self.sync)
        self.assertLess(self.sync.index("Sync-DepuisGit $racine"),
                        self.sync.index("$resultat = Sync-DepuisGithub"))

    def test_secours_http_avec_jeton_du_poste(self):
        # Si la base Git locale est inutilisable, l'API GitHub doit etre appelee
        # avec un jeton : sinon un depot prive repond encore 404.
        self.assertIn("credential fill", self.sync)
        self.assertIn("Authorization", self.sync)
        self.assertIn("git/blobs/", self.sync)

    def test_fins_de_ligne_des_bat_preservees(self):
        # Les blobs du depot sont en LF, les .bat doivent rester en CRLF :
        # sans cela le script recopie ne pourrait plus s'executer correctement.
        self.assertIn("Set-FinDeLigneWindows", self.sync)

    def test_aucun_fichier_local_ecrase_par_un_telechargement_partiel(self):
        # Un fichier temporaire est verifie par son hash AVANT de remplacer la
        # source : une coupure reseau ne laisse jamais un C# tronque.
        self.assertIn(".librevies-tmp", self.sync)
        self.assertNotIn("OutFile $destination", self.sync)


if __name__ == "__main__":
    unittest.main(verbosity=2)
