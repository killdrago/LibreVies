"""Recette locale sans Unity/Tk/HTTP : le build de jeu ne peut pas etre retrograde.

    python -B compilation/outils/test_jeu_local.py

Les fichiers PE de la recette sont synthetiques et restent dans un dossier temporaire.
"""
from __future__ import annotations
import hashlib
import importlib.util
import importlib.machinery
import json
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest.mock import Mock, patch
from test_launcher import charger_launcher

ROOT = Path(__file__).resolve().parents[2]


def charger_preparation():
    spec = importlib.util.spec_from_file_location("lv_local_builder", ROOT / "compilation/outils/preparer_jeu_local.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class JeuLocal(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.preparation = charger_preparation()

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="lv-jeu-local-")
        self.addCleanup(self.temp.cleanup)
        self.jeu = Path(self.temp.name) / "jeu"
        self.jeu.mkdir()
        (self.jeu / "launcher.pyw").write_bytes((ROOT / "jeu/launcher.pyw").read_bytes())
        (self.jeu / "version_url.json").write_text(json.dumps({
            "mode_local": True, "game_version": "0.5.79", "raw_url": "http://ancienne-distribution.invalid"}))
        self.launcher = charger_launcher(self.jeu)
        self.game = self.jeu / "game"
        self.dll = self.game / "LibreViesGame_Data/Managed/Assembly-CSharp.dll"

    def creer_export(self, version="0.5.85"):
        self.dll.parent.mkdir(parents=True, exist_ok=True)
        self.dll.write_bytes(version.encode("utf-16le") + b"\0" + b"\0".join(
            nom.encode() for nom in self.preparation.REQUIRED) + b"\0")
        (self.game / "LibreViesGame.exe").write_bytes(b"MZ-recette")
        (self.game / "version_jeu.json").write_text(json.dumps({
            "version": version, "assembly_sha256": hashlib.sha256(self.dll.read_bytes()).hexdigest()}))

    def test_export85_lance_malgre_ancien_manifeste79(self):
        self.creer_export()
        self.assertEqual(Path(self.launcher.find_game()), self.game / "LibreViesGame.exe")
        self.assertIn("v0.5.85", self.launcher.diagnostic_jeu_local()[1])

    def test_aucun_http_et_aucun_remplacement_du_game(self):
        self.creer_export()
        avant = self.dll.read_bytes()
        with patch.object(self.launcher.urllib.request, "urlopen") as http:
            resultat = self.launcher.check_for_updates(Mock())
        http.assert_not_called()
        self.assertEqual(resultat["modified"], [])
        self.assertEqual(self.dll.read_bytes(), avant)

    def test_thread_local_ignore_toute_installation_distante(self):
        callbacks = []
        faux = types.SimpleNamespace(_ui_call=callbacks.append, _updates_finished=Mock())
        with patch.object(self.launcher, "check_for_updates") as distant:
            self.launcher.App._run_update(faux)
        distant.assert_not_called()
        self.assertEqual(len(callbacks), 1)
        callbacks[0]()
        faux._updates_finished.assert_called_once_with(True)

    def test_export_verifie_mais_obsolete_rejete(self):
        self.creer_export("0.5.79")
        self.assertIsNone(self.launcher.find_game())
        self.assertIn("obsolete", self.launcher.diagnostic_jeu_local()[1])
        with self.assertRaises(ValueError):
            self.preparation.preparer(self.jeu, "0.5.85")
        self.assertEqual(json.loads((self.jeu / "version_url.json").read_text())["game_version"], "0.5.79")

    def test_ancienne_assembly_non_verifiee_rejetee(self):
        self.creer_export()
        self.dll.write_bytes(b"ancien jeu sans pseudo")
        self.assertIsNone(self.launcher.find_game())
        with self.assertRaises(ValueError):
            self.preparation.preparer(self.jeu, "0.5.85")

    def test_preparation_met_version85_sans_toucher_aux_secrets(self):
        self.creer_export()
        prive = {"autolog.dat": b"chiffre-recette", "auth_config.json": b'{"api_url":"http://localhost/serveur/api.php"}'}
        for name, contenu in prive.items():
            (self.jeu / name).write_bytes(contenu)
        self.preparation.preparer(self.jeu, "0.5.85")
        manifest = json.loads((self.jeu / "version_url.json").read_text())
        self.assertTrue(manifest["mode_local"])
        self.assertEqual(manifest["game_version"], "0.5.85")
        self.assertEqual(manifest["game_build"]["mode"], "local")
        self.assertEqual(json.loads((self.jeu / "etat_jeu.json").read_text())["version"], "0.5.85")
        for name, contenu in prive.items():
            self.assertEqual((self.jeu / name).read_bytes(), contenu)
        self.assertNotIn("autolog", json.dumps(manifest))

    def test_build_verifie_avant_remplacement_et_prepare_mode_local(self):
        for name in ("build_launcher.bat", "build_unity_game.bat"):
            bat = (ROOT / "compilation" / name).read_text()
            self.assertLess(bat.index("--verifier-seulement --export"), bat.index('rmdir /s /q "%JEU%\\game"'))
            self.assertIn('--jeu "%JEU%"', bat)
        bat = (ROOT / "compilation/build_launcher.bat").read_text()
        self.assertNotIn("Publier cette compilation maintenant", bat)
        self.assertNotIn("call \"%ROOT%outils\\publier_jeu.bat\"", bat)
        sync = (ROOT / "compilation/outils/synchroniser_sources.ps1").read_text()
        self.assertNotIn("publier_jeu_compiler", sync)

    def test_poignee_blanche_droite_mihauteur_taille_nez_maire(self):
        cs = (ROOT / "compilation/unity/Assets/Scripts/LibreViesGame.cs").read_text()
        poignee = cs.split("private void CreerPoigneesPorte", 1)[1].split("private bool TryTrouverCote", 1)[0]
        self.assertIn("PrimitiveType.Sphere", poignee)
        self.assertIn('"White"', poignee)
        self.assertIn("new Vector3(1.06f, 0f", poignee)
        self.assertIn("0.11f * 1.02f", poignee)
        self.assertNotIn("MetalAluminium", poignee)
        self.assertNotIn("Box(", poignee)
        # Porte large de 1.20 m : centre a 1.06 m, donc 14 cm du bord droit.
        self.assertAlmostEqual(1.20 - 1.06, 0.14)

    def test_pseudo_redescendu10cm_et_taille_quart_conservee(self):
        cs = (ROOT / "compilation/unity/Assets/Scripts/LibreViesGame.cs").read_text()
        nom = cs.split("private void CreerNomJoueur()", 1)[1].split("private void ReconfigurerMonstresAdmin()", 1)[0]
        self.assertIn("0.035f", nom)
        self.assertIn("hauteur + 0.38f", nom)
        self.assertIn("new Vector3(0f, 2.80f, 0f)", nom)
        self.assertAlmostEqual(0.14 / 4, 0.035)
        self.assertAlmostEqual(0.48 - 0.38, 0.10)
        self.assertIn("Quaternion.LookRotation", nom)
        self.assertIn("!firstPerson && !dead", nom)


if __name__ == "__main__":
    unittest.main(verbosity=2)
