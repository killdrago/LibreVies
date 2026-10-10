"""Contrats ADMIN/NPC : droits serveur, edition, villes et prise des hallebardes.

    python -B compilation/outils/test_admin_npc.py

Recette statique, ne remplace pas le rendu/compilateur Unity natif.
"""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
GAME = (ROOT / "compilation/unity/Assets/Scripts/LibreViesGame.cs").read_text()
API = (ROOT / "jeu/serveur/api.php").read_text()


def section(source, start, end):
    return source.split(start, 1)[1].split(end, 1)[0]


class AdminNpc(unittest.TestCase):
    def test_droit_vient_de_la_bdd_et_admin_est_masque(self):
        self.assertIn("SELECT id, pseudo, valider, droit, bani FROM membre", API)
        self.assertIn("'droit' => (int)$membre['droit']", API)
        self.assertIn("if (!CompteAdministrateur) { adminOpen = false; return; }", GAME)
        self.assertIn("if (CompteAdministrateur && !creationPersonnageOpen && GUI.Button", GAME)
        client = (ROOT / "compilation/unity/Assets/Scripts/LibreViesCompte.cs").read_text()
        self.assertIn("Joueur.droit == 1", client)
        self.assertIn("public int droit", client)

    def test_sauvegarde_npc_admin_seulement_et_transaction(self):
        action = section(API, "if ($action === 'save_npc')", "if ($action === 'get_npcs')")
        self.assertIn("membre_authentifie", action)
        self.assertIn("(int)$membre['droit'] !== 1", action)
        self.assertIn("array(), 403", action)
        self.assertLess(action.index("(int)$membre['droit'] !== 1"), action.index("enregistrer_npc"))
        self.assertIn("valider_profil_personnage", action)
        self.assertIn("beginTransaction", action)
        self.assertIn("rollBack", action)
        helper = (ROOT / "jeu/serveur/npc.php").read_text()
        update = helper.split("function enregistrer_npc", 1)[1]
        self.assertNotIn("objets =", update)
        self.assertNotIn("INSERT", update)

    def test_dropdown_et_onglets_villes(self):
        panel = section(GAME, "private void DessinerAdminNpc", "private void DessinerAdminMonstres")
        self.assertIn("GUI.Toolbar(barreVilles", panel)
        self.assertIn("villes.Contains(tous[i].Ville)", panel)
        self.assertIn("tous[i].Ville == villes[adminNpcVilleSelection]", panel)
        self.assertIn("adminNpcListeOuverte", panel)
        self.assertIn("MODIFIER L'ESTHETIQUE", panel)
        self.assertNotIn("Le NPC Esthétique propose", panel)
        names = section(GAME, "private List<AdminNpcEntry> ObtenirNpcsAdminTries", "private void DessinerAdminNpc")
        self.assertIn('"Garde Nord"', names)
        self.assertIn('"Garde Sud"', names)
        self.assertNotIn('ToString("00")', names)

    def test_panel_partage_sauve_npc_sans_modifier_le_joueur(self):
        openNpc = section(GAME, "private void OuvrirCreationNpc", "private void FermerCreationPersonnage")
        self.assertIn("if (!CompteAdministrateur", openNpc)
        self.assertIn("cibleEditionNpc = entree.Id", openNpc)
        save = section(GAME, "IEnumerator EnregistrerEtAppliquerNpc", "private float SliderHumain")
        self.assertLess(save.index("compteJoueur.SauvegarderNpc"), save.index("createur.ApplyTo"))
        self.assertNotIn("personnageActuel =", save)
        self.assertIn("premier.SetParent(root, true)", save)
        self.assertIn("ancienneMain", save)
        self.assertIn("VALIDER / ENREGISTRER", GAME)

    def test_hallebarde_suit_paume_et_garde_orientation_verticale(self):
        weapon = section(GAME, "private void MettreAJourArmeGarde", "private bool AppliquerAvatarsNpcs")
        self.assertIn('AppliedBone("wrist.R")', weapon)
        self.assertIn('AppliedBone("finger3-1.R")', weapon)
        self.assertIn("Vector3.Lerp", weapon)
        self.assertIn("garde.Hallebarde.position = prise - garde.Hallebarde.TransformVector(garde.PriseHallebarde)", weapon)
        self.assertIn("MesurerPriseHallebarde", GAME)
        self.assertIn("sharedMesh.bounds", GAME)
        self.assertIn("garde.Hallebarde.rotation = garde.Root.transform.rotation", weapon)
        update = section(GAME, "private void UpdateGuards", "private void TuerEnnemiParGarde")
        self.assertLess(update.index("AnimateAppliedHuman(marche"), update.index("MettreAJourArmeGarde(garde)"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
