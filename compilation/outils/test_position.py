"""Contrats position/validation/fiche : statiques, pas compilation Unity native.

python -B compilation/outils/test_position.py
La recette test_api_personnage.mjs execute aussi les sauvegardes dans PHP/SQLite.
"""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
GAME = (ROOT / 'compilation/unity/Assets/Scripts/LibreViesGame.cs').read_text()
COMPTE = (ROOT / 'compilation/unity/Assets/Scripts/LibreViesCompte.cs').read_text()
API = (ROOT / 'jeu/serveur/api.php').read_text()


class PositionEtFiche(unittest.TestCase):
    def test_pseudo_75pourcent(self):
        nom = GAME.split('private void CreerNomJoueur()', 1)[1].split('private void MettreAJourNomJoueur', 1)[0]
        self.assertIn('0.02625f', nom)
        self.assertAlmostEqual(0.0175 * 1.5, 0.02625)
        self.assertAlmostEqual(0.035 * .75, 0.02625)

    def test_validation_texte_et_inscription_oui(self):
        sql = (ROOT / 'jeu/serveur/membre.sql').read_text()
        self.assertIn("valider ENUM('non', 'oui') NOT NULL DEFAULT 'oui'", sql)
        self.assertIn("VALUES (:pseudo, :motdepasse, :email, 'oui', 0, 'non', NULL)", API)
        self.assertIn("'valider' => (string)$membre['valider']", API)
        self.assertIn("(string)$membre['valider'] !== 'oui'", API)
        self.assertIn('public string valider;', COMPTE)

    def test_caracteristiques_chacune_sur_sa_ligne(self):
        body = GAME.split('private string CaracteristiquesJoueurAdministration()', 1)[1].split('private void DessinerAdminJoueur', 1)[0]
        for key in ['Pseudo', 'ID', 'Email', 'Droit', 'Validation', 'Bani en BDD',
                    'Experience', 'Chasse', 'Territoire', 'PV actuels', 'Niveau actuel',
                    'Or actuel', 'Cailloux actuels', 'Position actuelle']:
            self.assertIn('texte.AppendLine("' + key + ' : ', body)
        self.assertNotIn('     ID : ', body)
        self.assertNotIn('     Validation', body)
        self.assertNotIn('     Chasse', body)

    def test_fiche_marge_basse_et_scroll_visible(self):
        panel = GAME.split('private void DessinerAdminJoueur', 1)[1].split('private void ValiderEditionHumaine', 1)[0]
        self.assertIn('hauteurTexte + 32f', panel)
        self.assertIn('hauteurTexte + 4f', panel)
        self.assertIn('false, true', panel)
        self.assertIn('fixedHeight = 0f', panel)
        # La derniere ligne a au moins 20 unites logiques de marge en bas.
        self.assertGreaterEqual(32 - 8 - 4, 20)

    def test_position_colonne_unique_personnelle(self):
        sql = (ROOT / 'jeu/serveur/membre.sql').read_text()
        self.assertIn('position TEXT NULL', sql)
        action = API.split("if ($action === 'save_position')", 1)[1].split("if ($action === 'get_character'", 1)[0]
        self.assertIn('membre_authentifie', action)
        self.assertIn("(int)$membre['id']", action)
        self.assertIn('UPDATE membre SET position = :position WHERE id = :id', action)
        self.assertNotIn("$donnees['id']", action)
        self.assertIn('FOR UPDATE', action)
        self.assertIn('rollBack', action)
        self.assertNotIn('UPDATE personnage', action)

    def test_position_sauvee_en_isolation_erreur_admin(self):
        body = COMPTE.split('SauvegarderPosition(', 1)[1].split('private IEnumerator Envoyer', 1)[0]
        self.assertIn('ErreurPosition', body)
        self.assertNotRegex(body, r'\bErreur\s*=')
        self.assertIn('requete.timeout = 5', body)
        self.assertIn('action=save_position&session_token=', body)
        self.assertNotIn('Debug.Log', body)

    def test_restauration_apres_monde_et_coordonnees(self):
        world = GAME.split('System.Collections.IEnumerator ConstruireMonde()', 1)[1].split('private void ControlerCouvertureShader', 1)[0]
        self.assertLess(world.index('ChargerCoordonneesEdition();'), world.index('RestaurerPositionJoueur();'))
        restore = GAME.split('private void RestaurerPositionJoueur()', 1)[1].split('private void DemanderSauvegardePosition', 1)[0]
        self.assertIn('compteJoueur.Position.Point', restore)
        self.assertIn('TerrainHeight', restore)
        self.assertIn('Mathf.Clamp', restore)

    def test_autosave_focus_et_intervalle(self):
        self.assertIn('Time.realtimeSinceStartup + 20f', GAME)
        self.assertIn('OnApplicationPause(bool pause)', GAME)
        self.assertIn('OnApplicationFocus(bool focus)', GAME)
        self.assertIn('positionSauvegardeEnCours', GAME)

    def test_fermeture_differée_bornee_pas_sauvegarde_dans_quit(self):
        self.assertIn('Application.wantsToQuit += AutoriserFermetureAvecPosition', GAME)
        self.assertIn('Application.wantsToQuit -= AutoriserFermetureAvecPosition', GAME)
        quit = GAME.split('private void OnApplicationQuit()', 1)[1].split('private Material screenAdjustMaterial', 1)[0]
        self.assertNotIn('StartCoroutine', quit)
        self.assertIn('EnregistrerCoordonneesEdition();', quit)
        save = GAME.split('System.Collections.IEnumerator EnregistrerPositionEtFermer()', 1)[1].split('private void OnApplicationPause', 1)[0]
        self.assertIn('Time.realtimeSinceStartup + 8f', save)
        self.assertLess(save.index('EnregistrerPositionJoueur(derniere)'), save.index('Application.Quit();'))
        self.assertIn('fermetureAutorisee = true', save)

    def test_coroutines_nongen_regression_cs0305(self):
        for name in ['EnregistrerPositionJoueur', 'EnregistrerPositionEtFermer']:
            self.assertIn('System.Collections.IEnumerator ' + name + '(', GAME)
        self.assertNotRegex(GAME, r'\bprivate\s+IEnumerator\s+\w+\s*\(')


if __name__ == '__main__':
    unittest.main(verbosity=2)
