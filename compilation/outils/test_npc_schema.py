"""Contrat SQL NPC : personnalisation identique a personnage, ID texte.

    python -B compilation/outils/test_npc_schema.py

Verification statique du schema MySQL, sans import dans une vraie BDD.
"""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]


def colonnes(sql):
    return dict(re.findall(
        r'^\s{2}(`?\w+`?)\s+((?:INT|TINYINT|VARCHAR|ENUM|DECIMAL|TEXT)\b[^\n]*?)(?:,)?$',
        sql, re.M))


class SchemaNpc(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.personnage = (ROOT / "jeu/serveur/personnage.sql").read_text()
        cls.npc = (ROOT / "jeu/serveur/npc.sql").read_text()

    def test_memes_colonnes_et_types_de_personnalisation(self):
        attendu = colonnes(self.personnage)
        del attendu["id"]
        del attendu["`default`"]
        actuel = colonnes(self.npc)
        del actuel["id"]
        self.assertEqual(actuel, attendu)

    def test_identifiant_texte_sans_default_sans_compte(self):
        self.assertEqual(colonnes(self.npc)["id"], "VARCHAR(100) NOT NULL")
        self.assertNotIn("`default`", colonnes(self.npc))
        self.assertIn("PRIMARY KEY (id)", self.npc)
        self.assertNotIn("FOREIGN KEY", self.npc)
        self.assertNotIn("AUTO_INCREMENT", self.npc)
        self.assertIn("ENGINE=InnoDB", self.npc)
        self.assertIn("CHARSET=utf8mb4", self.npc)

    def test_schema_copie_depuis_jeu_sans_import_automatique(self):
        sync = (ROOT / "compilation/outils/synchroniser_serveur_local.ps1").read_text()
        fichiers = sync.split("foreach ($nom in @(", 1)[1].split("$source =", 1)[0]
        self.assertIn("'npc.sql'", fichiers)
        self.assertNotIn("'config.php'", fichiers)
        self.assertNotIn("INSERT INTO", self.npc)


if __name__ == "__main__":
    unittest.main(verbosity=2)
