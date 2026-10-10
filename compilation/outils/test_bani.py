"""Bannissement/admin et geometrie des armes, sans vraie BDD/Tk/Unity.

    python -B compilation/outils/test_bani.py

Les API/transactions s'executent en plus dans test_api_personnage.mjs (PHP WASM).
Ces controles ne remplacent pas la compilation/rendu natifs Unity/Windows.
"""
from pathlib import Path
from unittest.mock import Mock, patch
import copy
import io
import json
import math
import shutil
import tempfile
import unittest
import urllib.error
from test_launcher import charger_launcher

ROOT = Path(__file__).resolve().parents[2]
GAME = (ROOT / 'compilation/unity/Assets/Scripts/LibreViesGame.cs').read_text()
API = (ROOT / 'jeu/serveur/api.php').read_text()
COMPTE = (ROOT / 'compilation/unity/Assets/Scripts/LibreViesCompte.cs').read_text()


class BaniLauncher(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.directory = tempfile.TemporaryDirectory(prefix='lv-bani-')
        folder = Path(cls.directory.name)
        shutil.copyfile(ROOT / 'jeu/launcher.pyw', folder / 'launcher.pyw')
        cls.mod = charger_launcher(folder)

    @classmethod
    def tearDownClass(cls):
        cls.directory.cleanup()

    def setUp(self):
        self.app = self.mod.App.__new__(self.mod.App)
        for key, value in {
            'updates_done': True, 'authenticated': True, 'auth_busy': False,
            'compte_bani': False, 'compte_verifie': True, 'verification_compte_en_cours': False,
            'ready': True, 'game': 'recette.exe', 'logged_pseudo': 'Canonique', 'logged_membre_id': 123,
            'session_token': 'session-recette-secrete-12345678', 'session_expires_at': 4102444800,
            'connected_api_url': 'http://localhost/serveur/api.php', 'auth_message': '',
            'diagnostic_ancien_jeu': '', 'login_form_canvas_items': [], 'autolog': None,
        }.items():
            setattr(self.app, key, value)
        self.app.play_btn = Mock()
        self.app._upd_bar = Mock()
        self.app.after = Mock()
        self.app.destroy = Mock()
        self.app.canvas = Mock()
        for name in ['login_greeting', 'login_pseudo', 'login_mdp', 'login_button', 'register_button', 'remember_var']:
            setattr(self.app, name, Mock())
        self.app.remember_var.get.return_value = False
        self.callbacks = []
        self.app._ui_call = self.callbacks.append
        local = patch.object(self.mod, 'mode_local_actif', return_value=False)
        local.start(); self.addCleanup(local.stop)

    def response(self, bani='non', id=123):
        return {'ok': True, 'membre': {'id': id, 'pseudo': 'Canonique', 'bani': bani},
                'session': {'token': self.app.session_token, 'expires_at': 4102444800}}

    def verifier(self, response=None, error=None, play=True):
        self.app.verification_compte_en_cours = True
        self.app.compte_verifie = False
        with patch.object(self.mod, 'auth_api_request', return_value=response, side_effect=error) as api:
            self.app._verification_compte_worker(self.app.session_token, 123,
                                                self.app.connected_api_url, play)
        self.assertEqual(len(self.callbacks), 1)
        return api

    def test_statut_non_explicitement_autorise(self):
        self.assertEqual(self.mod.verifier_statut_bani({'bani': 'non'}), 'non')

    def test_statut_oui_message_exact(self):
        with self.assertRaisesRegex(self.mod.CompteBaniError, 'Joueur bani veuillez contacter'):
            self.mod.verifier_statut_bani({'bani': 'oui'})

    def test_absent_ou_inconnu_ferme_le_verrou(self):
        for val in [None, True, False, 0, 1, 'NON', '', 'yes']:
            with self.subTest(val=val), self.assertRaises(ValueError):
                self.mod.verifier_statut_bani({'bani': val})
        with self.assertRaises(ValueError): self.mod.verifier_statut_bani({})

    def test_session_login_exige_bani_non(self):
        good = self.response()
        self.assertEqual(self.mod.session_compte(good)[0], 123)
        del good['membre']['bani']
        with self.assertRaises(ValueError): self.mod.session_compte(good)
        with self.assertRaises(self.mod.CompteBaniError): self.mod.session_compte(self.response('oui'))

    def test_non_verifie_bloque_bouton(self):
        self.app.compte_verifie = False
        self.app._update_play_state()
        self.assertEqual(self.app.play_btn.config.call_args.kwargs['state'], 'disabled')

    def test_oui_bloque_bouton_et_message(self):
        self.app.compte_bani = True
        self.app._update_play_state()
        self.assertEqual(self.app.play_btn.config.call_args.kwargs['state'], 'disabled')
        self.assertEqual(self.app._upd_bar.call_args.args, (0, self.mod.BANI_MESSAGE))

    def test_verification_en_cours_bloque(self):
        self.app.verification_compte_en_cours = True
        self.app._update_play_state()
        self.assertEqual(self.app.play_btn.config.call_args.kwargs['state'], 'disabled')

    def test_non_verifie_ok_debloque(self):
        self.app._update_play_state()
        self.assertEqual(self.app.play_btn.config.call_args.kwargs['state'], 'normal')

    def test_clic_jouer_ne_lance_jamais_sans_relecture(self):
        self.app._verifier_compte = Mock()
        with patch.object(self.mod, 'launch') as launch:
            self.app.play()
        launch.assert_not_called()
        self.app._verifier_compte.assert_called_once_with(ouvrir_jeu=True)
        self.app.destroy.assert_not_called()

    def test_controle_effectif_temps_de_connexion_ban_revoque(self):
        api = self.verifier(error=self.mod.CompteBaniError(self.mod.BANI_MESSAGE))
        api.assert_called_once_with('get_character', {'session_token': self.app.session_token},
                                   api_url='http://localhost/serveur/api.php')
        with patch.object(self.mod, 'launch') as launch:
            self.callbacks[0]()
        self.assertTrue(self.app.compte_bani)
        self.assertFalse(self.app.authenticated)
        self.assertEqual(self.app.session_token, '')
        self.assertEqual(self.app.play_btn.config.call_args.kwargs['state'], 'disabled')
        launch.assert_not_called()
        self.app.destroy.assert_not_called()

    def test_non_relu_lance_seulement_apres_retour_ui(self):
        token = self.app.session_token
        self.verifier(self.response())
        with patch.object(self.mod, 'launch') as launch:
            launch.assert_not_called()
            self.callbacks[0]()
        launch.assert_called_once_with('recette.exe', 'Canonique', token, 'http://localhost/serveur/api.php')
        self.app.destroy.assert_called_once()

    def test_controle_periodique_non_ne_lance_pas(self):
        self.verifier(self.response(), play=False)
        with patch.object(self.mod, 'launch') as launch:
            self.callbacks[0]()
        launch.assert_not_called()
        self.assertTrue(self.app.compte_verifie)

    def test_reseau_indisponible_aucune_autorisation_de_cache(self):
        self.verifier(error=ValueError('Serveur indisponible'))
        with patch.object(self.mod, 'launch') as launch:
            self.callbacks[0]()
        launch.assert_not_called()
        self.assertFalse(self.app.compte_verifie)
        self.assertEqual(self.app.play_btn.config.call_args.kwargs['state'], 'disabled')

    def test_compte_usurpe_et_bani_absent_refuses(self):
        for response in [self.response(id=456), {'ok': True, 'membre': {'id': 123}}]:
            self.callbacks.clear()
            self.verifier(response)
            with patch.object(self.mod, 'launch') as launch:
                self.callbacks[0]()
            launch.assert_not_called()
            self.assertFalse(self.app.compte_verifie)

    def test_session_revoquee_reaffiche_connexion(self):
        self.verifier(error=self.mod.SessionCompteError('Session expiree'))
        with patch.object(self.mod, 'launch') as launch:
            self.callbacks[0]()
        launch.assert_not_called()
        self.assertFalse(self.app.authenticated)
        self.assertFalse(self.app.compte_bani)
        self.assertEqual(self.app.session_token, '')
        self.app.login_button.place.assert_called()

    def test_reponse_d_ancienne_session_ignoree(self):
        self.verifier(self.response())
        self.app.session_token = 'nouvelle-session-recette-012345'
        with patch.object(self.mod, 'launch') as launch:
            self.callbacks[0]()
        launch.assert_not_called()
        self.assertFalse(self.app.compte_verifie)

    def test_timer_10secondes(self):
        self.app._verifier_compte = Mock()
        self.app._controle_compte_periodique()
        self.app._verifier_compte.assert_called_once_with(ouvrir_jeu=False)
        self.assertEqual(self.app.after.call_args.args[0], 10000)

    def test_message_bani_prioritaire_sur_progression(self):
        self.app.compte_bani = True
        self.app.bar_fill, self.app.bar_text, self.app.pct_text = 1, 2, 3
        self.mod.App._upd_bar(self.app, 100, 'Pret !')
        self.app.canvas.itemconfig.assert_any_call(2, text=self.mod.BANI_MESSAGE)
        self.app.canvas.itemconfig.assert_any_call(3, text='0%')

    def test_erreur_http_bani_typee_sans_journal_de_session(self):
        messages = []
        response = io.BytesIO(json.dumps({'ok': False, 'code': 'membre_bani',
            'message': self.mod.BANI_MESSAGE}).encode())
        error = urllib.error.HTTPError('http://localhost/serveur/api.php', 403, 'Forbidden', {}, response)
        with patch.object(self.mod.urllib.request, 'urlopen', side_effect=error), \
                patch.object(self.mod, 'journal_auth', side_effect=messages.append):
            with self.assertRaises(self.mod.CompteBaniError):
                self.mod.auth_api_request('get_character', {'session_token': self.app.session_token})
        self.assertNotIn(self.app.session_token, '\n'.join(messages))


class ContratsBaniEtArmes(unittest.TestCase):
    def test_coroutines_non_generiques_regression_cs0305(self):
        # Unity StartCoroutine attend System.Collections.IEnumerator, sans <T>.
        # Le scanner syntaxique n'avait pas detecte la resolution vers Generic.
        for nom in ['ConstruireMonde', 'RechercherJoueursAdministration',
                    'ChargerJoueurAdministration', 'EnregistrerBannissementJoueur',
                    'EnregistrerEtAppliquerPersonnage', 'EnregistrerEtAppliquerNpc']:
            self.assertIn('private System.Collections.IEnumerator ' + nom + '(', GAME)
        self.assertNotRegex(GAME, r'\b(?:private|public|protected)\s+IEnumerator\s+\w+\s*\(')

    def test_schema_non_et_registre_ignore_statut_client(self):
        sql = (ROOT / 'jeu/serveur/membre.sql').read_text()
        self.assertIn("bani ENUM('non', 'oui') NOT NULL DEFAULT 'non'", sql)
        self.assertIn("VALUES (:pseudo, :motdepasse, :email, 'oui', 0, 'non', NULL)", API)
        self.assertNotIn('ALTER TABLE', API.split('// La migration est volontairement manuelle')[0])

    def test_identite_bdd_et_bans_reverifies(self):
        self.assertIn('SELECT id, pseudo, valider, droit, bani FROM membre', API)
        self.assertIn('verifier_bannissement($membre)', API)
        action = API.split("if ($action === 'search_players'", 1)[1].split("if ($action === 'save_npc')", 1)[0]
        self.assertIn('verifier_administrateur($admin)', action)
        self.assertIn('FOR UPDATE', action)
        self.assertIn('UPDATE membre SET bani = :bani WHERE id = :id', action)
        self.assertNotIn('UPDATE personnage', action)
        self.assertNotIn('UPDATE classement', action)
        query = API.split("function lire_joueur_administration", 1)[1].split('function verifier_validation_email', 1)[0]
        self.assertNotIn('motdepasse', query)
        self.assertNotIn('session_token', query)
        self.assertIn('LIMIT 30', action)
        self.assertIn("ESCAPE '!'", action)

    def test_onglet_joueur_recherche_fiche_et_case_sauvee(self):
        panel = GAME.split('private void DessinerAdminJoueur', 1)[1].split('private void ValiderEditionHumaine', 1)[0]
        for text in ['GUI.TextField', 'RECHERCHER', 'ChargerJoueurAdministration',
                     'CaracteristiquesJoueurAdministration', 'GUI.Toggle', 'VALIDER', 'EnregistrerBannissementJoueur']:
            self.assertIn(text, panel)
        self.assertIn('yield return compteJoueur.SauvegarderBannissement', GAME)
        self.assertIn('Joueur = reponse.membre; // Toujours', COMPTE)
        self.assertNotIn('Joueur = reponse.player.membre', COMPTE)
        self.assertIn('Joueur.bani == "non"', COMPTE)
        self.assertIn('finally { GUI.matrix = ancienneMatrice; }', GAME)

    def test_pseudo_75pourcent_sans_changer_hauteur(self):
        nom = GAME.split('private void CreerNomJoueur()', 1)[1].split('private void MettreAJourNomJoueur', 1)[0]
        self.assertIn('0.02625f', nom)
        self.assertAlmostEqual(0.035 * 0.75, 0.02625)
        position = GAME.split('private void MettreAJourNomJoueur', 1)[1].split('private ', 1)[0]
        self.assertIn('0.38f', position)

    def test_point_de_prise_mesure_dans_obj_pas_pivot_vide(self):
        measure = GAME.split('private static Vector3 MesurerPriseHallebarde', 1)[1].split('private void MettreAJourArmeGarde', 1)[0]
        for text in ['Guard_HalberdShaft', 'sharedMesh.bounds', 'InverseTransformPoint', 'TransformPoint']:
            self.assertIn(text, measure)
        weapon = GAME.split('private void MettreAJourArmeGarde', 1)[1].split('private bool AppliquerAvatarsNpcs', 1)[0]
        self.assertIn('prise - garde.Hallebarde.TransformVector(garde.PriseHallebarde)', weapon)
        self.assertLess(weapon.index('Hallebarde.rotation'), weapon.index('Hallebarde.position'))
        self.assertIn('state.PriseHallebarde = MesurerPriseHallebarde', GAME)

    def test_invariant_geometrique_miroir_echelle_rotation(self):
        # Invariant de TransformPoint(p)=position+TransformVector(p), pas un rendu Unity.
        palm = (2.1, 1.35, -4.2)
        for local in [(0., .36, 0.), (-1.18, .36, 0.), (-1.18, .36, -.10)]:
            for scale in [.7, 1., 1.6]:
                for angle in [0., math.pi / 2, math.pi]:
                    x, y, z = [v * scale for v in local]
                    vector = (math.cos(angle)*x + math.sin(angle)*z, y,
                              -math.sin(angle)*x + math.cos(angle)*z)
                    root = [target - offset for target, offset in zip(palm, vector)]
                    actual = [p + offset for p, offset in zip(root, vector)]
                    for a, b in zip(actual, palm): self.assertAlmostEqual(a, b)


if __name__ == '__main__':
    unittest.main(verbosity=2)
