"""Contrats statiques du chargement NPC SQL -> API -> avatars MakeHuman.

    python -B compilation/outils/test_npc_avatars.py

Ne remplace pas la recette visuelle Unity (rig/accessoires/animations).
"""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]


class AvatarsNpc(unittest.TestCase):
    def test_tous_les_npc_humains_dans_sql_et_helper(self):
        sql = (ROOT / "compilation/outils/fixtures/npc_profiles_fixture.sql").read_text()
        helper = (ROOT / "jeu/serveur/npc.php").read_text()
        for id in ("maire", "forgeron", "marchand", "esthetique", "garde_nord", "garde_sud"):
            self.assertIn("'" + id + "'", sql)
            self.assertIn("'" + id + "'", helper)
        self.assertEqual(sql.count("ROUND("), 13)
        self.assertIn("RAND()", sql)
        self.assertIn("ON DUPLICATE KEY UPDATE", sql)
        self.assertNotIn("objets = VALUES", sql)

    def test_lecture_authentifiee_et_sans_sauvegarde_client(self):
        api = (ROOT / "jeu/serveur/api.php").read_text()
        npc = api.split("if ($action === 'get_npcs')", 1)[1].split("if ($action === 'get_character'", 1)[0]
        self.assertIn("membre_authentifie", npc)
        self.assertIn("lire_npcs_village($pdo)", npc)
        self.assertNotIn("$donnees['id']", npc)
        helper = (ROOT / "jeu/serveur/npc.php").read_text()
        self.assertIn("valider_profil_personnage($ligne)", helper)
        self.assertNotIn("INSERT", helper)
        self.assertNotIn("UPDATE", helper)

    def test_chargement_avant_monde_et_application_avant_edition(self):
        game = (ROOT / "compilation/unity/Assets/Scripts/LibreViesGame.cs").read_text()
        world = game.split("IEnumerator ConstruireMonde()", 1)[1].split("void ControlerCouvertureShader", 1)[0]
        self.assertLess(world.index("compteJoueur.ChargerNpcs()"), world.index("CreateTown"))
        self.assertLess(world.index("AppliquerAvatarsNpcs()"), world.index("ChargerCoordonneesEdition()"))
        self.assertIn("yield break", world)

    def test_avatars_corps_reels_sans_apercu_et_anciens_modeles_masques(self):
        game = (ROOT / "compilation/unity/Assets/Scripts/LibreViesGame.cs").read_text()
        avatar = game.split("AdminHumanCreator CreerAvatarNpc", 1)[1].split("void CreateBuilding", 1)[0]
        self.assertIn("npc.ProfilHumain().ChargerDans(createur)", avatar)
        self.assertIn("createur.ApplyTo(cible)", avatar)
        self.assertNotIn("BuildPreview", avatar)
        self.assertNotIn("Primitive(", avatar)
        self.assertIn("pnj.Model.gameObject.SetActive(false)", avatar)
        self.assertIn("garde.Model.gameObject.SetActive(false)", avatar)
        self.assertIn("erreurChargementCompte", avatar)

    def test_metiers_et_accessoires_restent_lies_au_rig(self):
        game = (ROOT / "compilation/unity/Assets/Scripts/LibreViesGame.cs").read_text()
        avatar = game.split("bool AppliquerAvatarsNpcs()", 1)[1].split("void CreateBuilding", 1)[0]
        self.assertIn('AppliedBone("wrist.R")', avatar)
        for nom in ("pnj.Marteau", "pnj.Feuille", "garde.Hallebarde"):
            self.assertIn("AttacherAccessoireNpc(" + nom, avatar)
        self.assertIn("AnimateAppliedHuman(marche, false", game)
        self.assertIn("AnimateAppliedHuman(false, false, pnj.Phase)", game)

    def test_acces_os_defini_dans_humanpreview_regression_cs1061(self):
        source = (ROOT / "compilation/unity/Assets/Scripts/AdminHumanCreator.cs").read_text()
        facade = source.split("public Transform AppliedBone", 1)[1].split("private void EnsurePreviewCamera", 1)[0]
        self.assertIn("appliedHuman.FindBone(nom)", facade)
        interieur = source.split("private sealed class HumanPreview", 1)[1]
        self.assertIn("public Transform FindBone(string name)", interieur)
        lookup = interieur.split("public Transform FindBone(string name)", 1)[1].split("private void LoadTargets", 1)[0]
        self.assertIn("bones == null || boneIndexes == null", lookup)
        self.assertIn("boneIndexes.TryGetValue(name, out index)", lookup)
        self.assertIn("index >= bones.Length", lookup)
        self.assertIn("return bones[index]", lookup)

    def test_id_texte_et_validation_du_client(self):
        dto = (ROOT / "compilation/unity/Assets/Scripts/LibreViesNpc.cs").read_text()
        client = (ROOT / "compilation/unity/Assets/Scripts/LibreViesCompte.cs").read_text()
        self.assertIn("public string id", dto)
        self.assertIn("@default = 0", dto)  # Adapteur du createur seulement, pas colonne NPC.
        self.assertIn("ProfilHumain().EstValide", dto)
        self.assertIn("reponse.npcs.Length != 6", client)
        self.assertIn("identifiants.Add", client)
        self.assertIn('Envoyer("get_npcs", null)', client)


if __name__ == "__main__":
    unittest.main(verbosity=2)
