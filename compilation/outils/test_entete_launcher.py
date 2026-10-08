"""Recette de l'en-tete du launcher sans Tk/serveur/identifiants reels.

    python -B compilation/outils/test_entete_launcher.py

Simule les widgets pour verifier connexion manuelle, Autolog et retour au
formulaire, sans remplacer une verification visuelle sous Windows.
"""
from __future__ import annotations

import hashlib
import importlib.machinery
import importlib.util
import json
import tempfile
import types
import unittest
from pathlib import Path
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[2]


class Widget:
    def __init__(self, *args, **options):
        self.options = options
        self.placement = None

    def place(self, **options):
        self.placement = options

    def place_forget(self):
        self.placement = None

    def config(self, **options):
        self.options.update(options)

    configure = config

    def pack(self, **options):
        pass

    def bind(self, *args):
        pass

    def destroy(self):
        pass


class BooleanVar:
    def __init__(self, value=False):
        self.value = value

    def get(self):
        return self.value

    def set(self, value):
        self.value = value


class Canvas(Widget):
    def __init__(self, *args, **options):
        super().__init__(*args, **options)
        self.items = {}

    def _create(self, *coords, **options):
        key = len(self.items) + 1
        self.items[key] = {"coords": coords, "state": "normal", **options}
        return key

    create_rectangle = _create
    create_text = _create
    create_image = _create
    create_line = _create

    def itemconfigure(self, key, **options):
        self.items[key].update(options)

    itemconfig = itemconfigure


class EnteteLauncher(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        # Importer une copie temporaire pour que GAME_DIR reste isole.
        cls.temp = tempfile.TemporaryDirectory(prefix="librevies-entete-")
        source = Path(cls.temp.name) / "launcher.pyw"
        source.write_bytes((ROOT / "jeu/launcher.pyw").read_bytes())
        tk = types.ModuleType("tkinter")
        for name in ("Tk", "Frame", "Label", "Button", "Entry", "Checkbutton", "PhotoImage"):
            setattr(tk, name, Widget)
        tk.Canvas = Canvas
        tk.BooleanVar = BooleanVar
        loader = importlib.machinery.SourceFileLoader("lv_entete_recette", str(source))
        spec = importlib.util.spec_from_loader(loader.name, loader)
        cls.launcher = importlib.util.module_from_spec(spec)
        with patch.dict("sys.modules", {"tkinter": tk}):
            loader.exec_module(cls.launcher)

    @classmethod
    def tearDownClass(cls):
        cls.temp.cleanup()

    def setUp(self):
        self.app = self.launcher.App.__new__(self.launcher.App)
        self.app._build_actu_page = Mock()
        self.app._build_classement_page = Mock()
        self.app._upd_bar = Mock()
        self.app._update_play_state = Mock()
        with patch.object(self.launcher, "_embedded_photo", return_value=None):
            self.app.build()

    def connexion(self, autolog=False):
        self.app.remember_var.set(autolog)
        with patch.object(self.launcher, "save_autolog") as save, \
                patch.object(self.launcher, "clear_autolog") as clear:
            self.app._auth_succeeded(
                "Pseudo Canonique", "motdepasse-de-recette", autolog, 123,
                "jeton-recette-0123456789", 4102444800,
                "http://localhost/serveur/api.php")
        return save, clear

    def verifier_entete_connecte(self):
        for key in self.app.login_form_canvas_items:
            self.assertEqual(self.app.canvas.items[key]["state"], "hidden")
        self.assertEqual(self.app.canvas.items[self.app.login_canvas_items[0]]["state"], "normal")
        self.assertEqual(self.app.login_greeting.options["text"], "Bonjour Pseudo Canonique")
        self.assertEqual(self.app.login_greeting.placement,
                         {"x": 285, "y": 39, "width": 264, "height": 22})
        for widget in (self.app.login_pseudo, self.app.login_mdp, self.app.login_button):
            self.assertIsNone(widget.placement)
        self.assertIsNotNone(self.app.autolog_check.placement)
        self.assertEqual(self.app.autolog_check.options["text"], "Autolog")

    def test_avant_connexion_formulaire_et_autolog_visibles(self):
        self.assertEqual([self.app.canvas.items[key]["text"]
                          for key in self.app.login_form_canvas_items],
                         ["Connexion", "Pseudo:", "MDP:"])
        for key in self.app.login_form_canvas_items:
            self.assertEqual(self.app.canvas.items[key]["state"], "normal")
        self.assertIsNotNone(self.app.login_pseudo.placement)
        self.assertIsNone(self.app.login_greeting.placement)
        self.assertIsNotNone(self.app.autolog_check.placement)

    def test_connexion_manuelle_bonjour_remplace_pseudo(self):
        save, clear = self.connexion()
        self.verifier_entete_connecte()
        self.assertTrue(self.app.authenticated)
        self.assertEqual(self.app.logged_pseudo, "Pseudo Canonique")
        self.assertFalse(self.app.remember_var.get())
        save.assert_not_called()
        clear.assert_called_once()

    def test_connexion_autolog_meme_affichage_et_case_conservee(self):
        save, clear = self.connexion(autolog=True)
        self.verifier_entete_connecte()
        self.assertTrue(self.app.remember_var.get())
        save.assert_called_once_with("Pseudo Canonique", "motdepasse-de-recette")
        clear.assert_not_called()

    def test_autolog_decoche_apres_connexion_ne_change_pas_le_bonjour(self):
        self.connexion(autolog=True)
        self.app.remember_var.set(False)
        with patch.object(self.launcher, "clear_autolog") as clear:
            self.app._autolog_toggle()
        clear.assert_called_once()
        self.assertIsNone(self.app.autolog)
        self.verifier_entete_connecte()

    def test_session_expiree_reaffiche_tous_les_libelles(self):
        self.connexion()
        self.app._auth_failed("Session expiree")
        self.assertFalse(self.app.authenticated)
        self.assertEqual(self.app.session_token, "")
        self.assertIsNone(self.app.login_greeting.placement)
        for key in self.app.login_form_canvas_items:
            self.assertEqual(self.app.canvas.items[key]["state"], "normal")
        for widget in (self.app.login_pseudo, self.app.login_mdp, self.app.login_button):
            self.assertIsNotNone(widget.placement)
        self.assertIsNotNone(self.app.autolog_check.placement)

    def test_copies_identiques_et_hash_de_publication_a_jour(self):
        source = (ROOT / "jeu/launcher.pyw").read_bytes()
        self.assertEqual(source, (ROOT / "jeucompiler/launcher.pyw").read_bytes())
        manifest = json.loads((ROOT / "jeucompiler/version_url.json").read_text(encoding="utf-8"))
        self.assertEqual(manifest["files"]["launcher.pyw"],
                         {"hash": hashlib.md5(source).hexdigest(), "size": len(source)})


if __name__ == "__main__":
    unittest.main(verbosity=2)
