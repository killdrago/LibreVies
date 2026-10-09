"""Recette sans Unity/Tk/BDD reelle : publication, portes et affiches 0.5.81.

    python -B compilation/outils/test_correctifs_81.py

Les assemblies de test sont des octets synthetiques, jamais des builds publies.
"""
from __future__ import annotations
import hashlib
import importlib.util
import json
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
CS = (ROOT / "compilation/unity/Assets/Scripts/LibreViesGame.cs").read_text()


def section(start, end):
    return CS.split(start, 1)[1].split(end, 1)[0]


def charger_publieur():
    tk = types.ModuleType("tkinter")
    tk.Tk = type("Tk", (), {})
    for name in ("filedialog", "messagebox", "ttk"):
        setattr(tk, name, types.ModuleType(name))
    spec = importlib.util.spec_from_file_location("lv_pub_81", ROOT / "publier_jeu_compiler.py")
    module = importlib.util.module_from_spec(spec)
    with patch.dict(sys.modules, {"tkinter": tk, spec.name: module}):
        spec.loader.exec_module(module)
    return module


class Publication(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.pub = charger_publieur()

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="lv-publication-81-")
        self.addCleanup(self.temp.cleanup)
        self.source = Path(self.temp.name) / "jeu"
        self.game = self.source / "game"
        self.dll = self.game / "LibreViesGame_Data/Managed/Assembly-CSharp.dll"
        self.dll.parent.mkdir(parents=True)
        self.dll.write_bytes("0.5.81".encode("utf-16le") + b"\0" + b"\0".join(
            name.encode() for name in ("InitialiserPseudoJoueur", "CreerNomJoueur",
            "MettreAJourNomJoueur", "LibreViesCompte", "LibreViesPersonnage", "CreerPoigneesPorte", "AjusterTexteDansPanneau")) + b"\0")
        (self.game / "LibreViesGame.exe").write_bytes(b"MZ-recette")
        (self.source / "launcher.pyw").write_text('LAUNCHER_VERSION = "4.2.1"\n')
        (self.source / "version_url.json").write_text('{"game_version":"0.5.79"}')
        self.marquer()

    def marquer(self):
        (self.game / "version_jeu.json").write_text(json.dumps({
            "version": "0.5.81", "assembly_sha256": hashlib.sha256(self.dll.read_bytes()).hexdigest()}))

    def test_version_vient_du_build_pas_du_manifeste_79(self):
        self.assertEqual(self.pub.lire_version_source(self.source), "0.5.81")
        files = self.pub.fichiers_source(self.source)
        manifest = json.loads(self.pub.construire_manifeste(
            self.source, "killdrago/LibreVies", "arena/01a0b32c-librevies", "jeucompiler", "0.5.81", "", files))
        self.assertEqual(manifest["game_version"], "0.5.81")
        self.assertEqual(manifest["game_build"]["version"], "0.5.81")
        self.assertEqual(manifest["game_build"]["mode"], "fichiers")
        self.assertTrue(manifest["raw_url"].endswith("/jeucompiler"))
        self.assertIn("game/version_jeu.json", manifest["files"])

    def test_ancien_binaire_ou_hash_non_conforme_refuse(self):
        self.dll.write_bytes(b"ancien jeu")
        with self.assertRaises(self.pub.PublicationError):
            self.pub.lire_version_source(self.source)
        self.marquer()  # Un hash seul ne suffit pas : les methodes doivent exister.
        with self.assertRaises(self.pub.PublicationError):
            self.pub.lire_version_source(self.source)

    def test_numero_saisi_a_la_main_ne_renomme_pas_un_build(self):
        with self.assertRaises(self.pub.PublicationError):
            self.pub.construire_manifeste(self.source, "killdrago/LibreVies",
                "arena/01a0b32c-librevies", "jeucompiler", "0.5.79", "", self.pub.fichiers_source(self.source))

    def test_autolog_et_serveur_ne_sont_jamais_distribues(self):
        for name in ("autolog.dat", "autolog.json", "auth_config.json"):
            (self.source / name).write_text("prive-recette")
        (self.source / "serveur").mkdir()
        (self.source / "serveur/config.php").write_text("prive-recette")
        (self.source / "journaux").mkdir()
        (self.source / "journaux/launcher_auth.log").write_text("prive-recette")
        files = self.pub.fichiers_source(self.source)
        self.assertFalse(any(name.startswith("journaux/") for name in files))
        self.assertFalse(set(files) & {"autolog.dat", "autolog.json", "auth_config.json"})
        self.assertFalse(any(name.startswith("serveur/") for name in files))

    def test_build_est_local_sans_publieur(self):
        bat = (ROOT / "compilation/build_launcher.bat").read_text()
        self.assertIn("preparer_jeu_local.py", bat)
        self.assertNotIn("call \"%ROOT%outils\\publier_jeu.bat\"", bat)
        self.assertNotIn("Publier cette compilation maintenant", bat)
        export = (ROOT / "compilation/unity/Assets/Editor/LibreViesBuild.cs").read_text()
        self.assertLess(export.index("VerifierExport(output, versionAttendue)"), export.index("EcrireVersionExport(output, versionAttendue)"))
        self.assertIn("SHA256.Create()", export)
        sync = (ROOT / "compilation/outils/synchroniser_sources.ps1").read_text()
        self.assertNotIn("publier_jeu_compiler", sync)


class Monde(unittest.TestCase):
    def test_poignees_deux_faces_suivent_le_pivot(self):
        construction = section("private void CreateBuilding(", "private bool TryTrouverCoteRedimensionnement")
        self.assertIn("CreerPoigneesPorte(pivotPorte)", construction)
        self.assertIn('new GameObject("Poignees_Porte")', construction)
        self.assertIn("poignees.SetParent(pivot, false)", construction)
        self.assertIn("face <= 1; face += 2", construction)
        self.assertIn('"Poignee_Ronde_Blanche"', construction)
        self.assertIn('"White"', construction)
        self.assertIn("PrimitiveType.Sphere", construction)
        self.assertIn("new Vector3(0.14f, 0f", construction)
        self.assertNotIn("collider: true", construction)

    def test_shader_affiche_teste_la_profondeur(self):
        shader = (ROOT / "compilation/unity/Assets/Resources/LVShaders/LVFacadeText.shader").read_text()
        self.assertIn("ZTest LEqual", shader)
        self.assertNotIn("ZTest Always", shader)
        self.assertIn("i.uv).a", shader)
        self.assertIn("LVShaders/LVFacadeText", CS)
        visibility = section("private void MettreAJourVisibiliteAffiches", "private Material MateriauFade")
        self.assertIn("Physics.Raycast", visibility)
        self.assertIn("AjusterTexteDansPanneau(affiche)", visibility)

    def test_texte_reste_dans_sa_pancarte(self):
        fit = section("private void AjusterTexteDansPanneau", "private void ModifierTaillePancarte")
        self.assertIn("rendu.localBounds.size", fit)
        self.assertIn("facade.LargeurPanneau - 0.24f", fit)
        self.assertNotIn("facade.Taille =", fit)
        for width, height in ((100, 9), (5, 0.2), (0.1, 0.01), (20, 20)):
            available_w, available_h = 5 - .24, .68 - .12
            factor = min(1, available_w / max(width, .001), available_h / max(height, .001))
            self.assertLessEqual(width * factor, available_w + 1e-9)
            self.assertLessEqual(height * factor, available_h + 1e-9)


if __name__ == "__main__":
    unittest.main(verbosity=2)
