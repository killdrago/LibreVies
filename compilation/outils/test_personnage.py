#!/usr/bin/env python3
"""Contrats personnage/launcher : controles statiques C# et tests Python sans Tk.

Usage : python -B compilation/outils/test_personnage.py
La recette HTTP/PHP complementaire est test_api_personnage.mjs.
Ces tests ne remplacent pas une compilation et une recette visuelle Unity.
"""
from pathlib import Path
import copy
import json
import re
import shutil
import tempfile
import types
import unittest
from unittest.mock import Mock, patch

from test_launcher import charger_launcher

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "compilation/unity/Assets/Scripts"
GAME = (SCRIPTS / "LibreViesGame.cs").read_text(encoding="utf-8")
DTO = (SCRIPTS / "LibreViesPersonnage.cs").read_text(encoding="utf-8")
COMPTE = (SCRIPTS / "LibreViesCompte.cs").read_text(encoding="utf-8")
PHP = (ROOT / "jeu/serveur/personnage.php").read_text(encoding="utf-8")
API = (ROOT / "jeu/serveur/api.php").read_text(encoding="utf-8")
MAPPING = {
    "tete": "headShape", "yeux": "eyesShape", "nez": "noseShape",
    "bouche": "mouthShape", "oreilles": "earsShape", "seins": "chestShape",
    "volume": "legThickness", "hanche": "hipShape", "ventre": "belly",
    "largeur_bras": "armThickness", "longueur_bras": "armLength",
    "hauteur_jambe": "legLength", "pieds": "feetSize",
}


def section(source, debut, fin):
    return source.split(debut, 1)[1].split(fin, 1)[0]


class ContratPersonnage(unittest.TestCase):
    def test_tous_les_sliders_dans_les_deux_sens(self):
        self.assertEqual(set(re.findall(r"public float (\w+);", DTO)), set(MAPPING))
        sauvegarde = section(DTO, "DepuisCreateur(", "private static string IdOption")
        chargement = section(DTO, "void ChargerDans(", "DepuisCreateur(")
        for colonne, reglage in MAPPING.items():
            self.assertIn(f"{colonne} = createur.{reglage}", sauvegarde)
            self.assertIn(f"createur.{reglage} = {colonne};", chargement)
            self.assertIn(f"'{colonne}'", PHP)
        self.assertIn("@default", DTO)
        self.assertIn("String.IsNullOrEmpty(identifiant) && optionnel", DTO)

    def test_identifiants_de_ressources_stables(self):
        catalogue = (SCRIPTS / "MakeHumanClothingFactory.cs").read_text(encoding="utf-8")
        identifiants = set(re.findall(r'new Option\("([^\"]+)"', catalogue))
        # Ne comparer que les identifiants presents dans les menus du panel.
        for identifiant in identifiants:
            self.assertIn(f"'{identifiant}'", PHP, identifiant)
        self.assertNotIn("hairStyle.ToString", DTO)
        self.assertNotIn("clothingStyle.ToString", DTO)

    def test_chargement_avant_joueur_et_base_inchangee(self):
        construction = section(GAME, "IEnumerator ConstruireMonde()", "void ControlerCouvertureShader")
        self.assertLess(construction.index("compteJoueur.Charger()"), construction.index("CreatePlayer"))
        self.assertIn("yield break", construction)
        joueur = section(GAME, "void CreatePlayer()", "void CreerNomJoueur()")
        self.assertIn("if (personnageActuel.EstPrimitif)", joueur)
        self.assertIn("CreateHumanHeroine(heroBody)", joueur)
        self.assertIn("AppliquerPersonnagePersonnalise()", joueur)

    def test_npc_seul_acces_panel_et_annulation_sans_sauvegarde(self):
        conversation = section(GAME, "void DessinerConversation()", "void OuvrirCreationPersonnage()")
        self.assertIn('conversationPnj.Metier == "Esthetique"', conversation)
        self.assertIn("OuvrirCreationPersonnage()", conversation)
        admin = section(GAME, "void DessinerAdmin()", "void OnGUI()")
        self.assertNotIn("OuvrirCreationPersonnage", admin)
        self.assertNotIn("DessinerReglagesPersonnage", admin)
        annulation = section(GAME, "void FermerCreationPersonnage()", "void DessinerCreationPersonnage()")
        self.assertNotIn("Sauvegarder", annulation)
        self.assertIn("SetPreviewVisible(false)", annulation)
        self.assertNotIn("GUI.FocusControl", annulation)  # Aussi appelee par la coroutine HTTP.
        self.assertIn("focusCreationAReinitialiser = true", annulation)
        self.assertIn("ResetPreview()", GAME)
        self.assertIn("personnageActuel.ChargerDans(humanCreator)", GAME)

    def test_confirmation_serveur_avant_application(self):
        methode = section(GAME, "IEnumerator EnregistrerEtAppliquerPersonnage(", "float SliderHumain(")
        self.assertLess(methode.index("compteJoueur.Sauvegarder"), methode.index("AppliquerPersonnagePersonnalise"))
        self.assertIn("personnageActuel = compteJoueur.Personnage;", methode)
        self.assertIn("yield break", methode)
        self.assertIn("sauvegardePersonnageEnCours", GAME)
        self.assertIn("session_token", COMPTE)
        self.assertNotIn("password", COMPTE)
        self.assertNotIn("idMembreJoueur", GAME)
        self.assertIn('Environment.SetEnvironmentVariable("LIBREVIES_SESSION_TOKEN", null)', COMPTE)
        self.assertIn("instance ?? (instance = new LibreViesCompte())", COMPTE)

    def test_pseudo_billboard_et_modal_redimensionnable(self):
        nom = section(GAME, "void CreerNomJoueur()", "void CreateEnemies()")
        self.assertIn("pseudoJoueur", nom)
        self.assertIn("richText = false", nom)
        self.assertIn("Quaternion.LookRotation", nom)
        self.assertIn("Quaternion.Euler(0f, 180f, 0f)", nom)
        self.assertIn("MettreAJourNomJoueur();", nom)
        self.assertIn("police 3D absente", nom)
        self.assertIn("!firstPerson && !dead", nom)
        self.assertIn("rendu.bounds.max.y", nom)
        fenetre = section(GAME, "void DessinerCreationPersonnage()", "void DessinerLigneCage(")
        self.assertIn("GUI.matrix", fenetre)
        self.assertIn("Screen.width", fenetre)
        self.assertIn("Screen.height", fenetre)

    def test_export_controle_version_et_fonctions_du_compte(self):
        editeur = (ROOT / "compilation/unity/Assets/Editor/LibreViesBuild.cs").read_text()
        self.assertLess(editeur.index("VerifierVersionSources();"), editeur.index("BuildPipeline.BuildPlayer"))
        self.assertLess(editeur.index("VerifierExport(output, versionAttendue)"),
                        editeur.index('Debug.Log("LibreVies Unity export'))
        self.assertIn("GetRawConstantValue()", editeur)
        self.assertIn("PlayerSettings.bundleVersion != attendue", editeur)
        self.assertIn("Encoding.Unicode.GetBytes(version)", editeur)
        self.assertIn("Assembly-CSharp.dll", editeur)
        for nom in ("InitialiserPseudoJoueur", "CreerNomJoueur", "MettreAJourNomJoueur",
                    "LibreViesCompte", "LibreViesPersonnage"):
            self.assertIn('"' + nom + '"', editeur)
        self.assertIn('nom + "\\0"', editeur)
        # Ce test est statique ; la compilation Unity doit encore etre faite sous Windows.

    def test_enregistrement_serveur_protege(self):
        actions = section(API, "if ($action === 'get_character'", "if ($action === 'login')")
        self.assertIn("membre_authentifie($pdo, $config, $donnees)", actions)
        self.assertIn("(int)$membre['id']", actions)
        self.assertNotIn("$donnees['id']", actions)
        self.assertNotIn("$donnees['pseudo']", actions)
        self.assertIn("beginTransaction()", actions)
        self.assertIn("rollBack()", actions)
        self.assertIn("'`default` = 0'", PHP)
        self.assertNotIn("objets = :", PHP)
        initial = section(API, "if ($action === 'register')", "if ($action === 'get_character'")
        self.assertIn("INSERT INTO personnage (id, `default`)", initial)
        self.assertIn("VALUES (:id, 1)", initial)
        self.assertIn("INSERT INTO classement (id, experience, chasse, territoire)", initial)
        self.assertIn("VALUES (:id, 0, 0, 0)", initial)
        self.assertLess(initial.index("INSERT INTO classement"), initial.index("$pdo->commit()"))
        self.assertIn("VALUES (:pseudo, :motdepasse, :email, 0, 0)", initial)

    def test_migration_ne_supprime_pas_les_profils_et_sync_preserve_config(self):
        migration = (ROOT / "jeu/serveur/mettre_a_jour_personnage.sql").read_text()
        self.assertNotIn("DROP TABLE", migration.upper())
        self.assertNotIn("TRUNCATE", migration.upper())
        self.assertIn("WHERE `default` = 1", migration)
        self.assertIn("WHERE p.id IS NULL", migration)
        synchronisation = (ROOT / "compilation/outils/synchroniser_serveur_local.ps1").read_text()
        fichiers = section(synchronisation, "foreach ($nom in @(", "$source =")
        self.assertIn("'personnage.php'", fichiers)
        self.assertIn("'securite.php'", fichiers)
        self.assertIn("'classement.sql'", fichiers)
        self.assertIn("'api.php'", fichiers)
        self.assertNotIn("'config.php'", fichiers)
        for fichier in ("build_launcher.bat", "build_unity_game.bat"):
            self.assertIn("synchroniser_serveur_local.ps1", (ROOT / "compilation" / fichier).read_text())


class SessionLauncher(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp = tempfile.TemporaryDirectory(prefix="librevies-session-")
        cls.directory = Path(cls.temp.name)
        shutil.copy2(ROOT / "jeu/launcher.pyw", cls.directory / "launcher.pyw")
        cls.launcher = charger_launcher(cls.directory)

    @classmethod
    def tearDownClass(cls):
        cls.temp.cleanup()

    def reponse(self):
        return {"ok": True, "message": "Connexion reussie.",
                "membre": {"id": 123, "pseudo": "Pseudo Canonique", "valider": 0},
                "session": {"token": "jeton-secret-recette-0123456789", "expires_at": 4102444800},
                "_api_url_utilisee": "http://localhost/serveur/api.php"}

    def test_session_et_pseudo_canoniques(self):
        r = self.reponse()
        self.assertEqual(self.launcher.session_compte(r), (
            123, "Pseudo Canonique", r["session"]["token"], r["session"]["expires_at"]))
        callbacks = []
        faux = types.SimpleNamespace(_ui_call=callbacks.append, _auth_succeeded=Mock(), _auth_failed=Mock())
        with patch.object(self.launcher, "auth_api_request", return_value=r):
            self.launcher.App._auth_worker(faux, "login", {
                "pseudo": "pseudo canonique", "password": "password-recette"}, False, False)
        self.assertEqual(len(callbacks), 1)
        callbacks[0]()
        self.assertEqual(faux._auth_succeeded.call_args.args[0], "Pseudo Canonique")
        self.assertEqual(faux._auth_succeeded.call_args.args[4], r["session"]["token"])
        self.assertFalse(faux._auth_failed.called)

    def test_reponse_sans_session_ou_expiree_refusee(self):
        for champ, valeur in (("token", ""), ("token", "../session-invalide"), ("expires_at", 0)):
            r = copy.deepcopy(self.reponse())
            r["session"][champ] = valeur
            with self.assertRaises(ValueError):
                self.launcher.session_compte(r)
        with self.assertRaises(ValueError):
            self.launcher.session_compte({"membre": {"id": 1, "pseudo": "Ancien"}})

    def test_aucun_corps_ni_secret_dans_journal_auth(self):
        r = self.reponse()
        class Reponse:
            status = 200
            def __enter__(self): return self
            def __exit__(self, *args): pass
            def read(self): return json.dumps(r).encode("utf-8")
        messages = []
        with patch.object(self.launcher.urllib.request, "urlopen", return_value=Reponse()), \
                patch.object(self.launcher, "journal_auth", side_effect=messages.append):
            self.launcher.auth_api_request("login", {"pseudo": "Test", "password": "password-recette"})
        texte = "\n".join(messages)
        self.assertNotIn(r["session"]["token"], texte)
        self.assertNotIn("password-recette", texte)
        self.assertNotIn("CORPS", texte)

    def test_autolog_decoche_supprime_les_deux_formats(self):
        for nom in ("autolog.dat", "autolog.json"):
            (self.directory / nom).write_bytes(b"faux-ancien-fichier")
        faux = types.SimpleNamespace(remember_var=Mock(), autolog={"pseudo": "Test"})
        faux.remember_var.get.return_value = False
        self.launcher.App._autolog_toggle(faux)
        self.assertIsNone(faux.autolog)
        self.assertFalse((self.directory / "autolog.dat").exists())
        self.assertFalse((self.directory / "autolog.json").exists())

    def test_session_expiree_empeche_lancement(self):
        faux = types.SimpleNamespace(updates_done=True, authenticated=True, ready=True,
            game="fake.exe", session_token="jeton-recette", session_expires_at=0,
            _auth_failed=Mock(), destroy=Mock())
        with patch.object(self.launcher, "launch") as launch:
            self.launcher.App.play(faux)
        self.assertFalse(launch.called)
        self.assertTrue(faux._auth_failed.called)
        self.assertFalse(faux.destroy.called)


if __name__ == "__main__":
    unittest.main(verbosity=2)
