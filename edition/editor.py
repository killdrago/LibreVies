#!/usr/bin/env python3
"""Éditeur de la BDD LibreVies — ouvre le JSON et permet de modifier les positions."""
import json, sys, os

BDD_PATH = os.path.join(os.path.dirname(os.path.dirname(__file__)), "BDD", "bdd.json")

with open(BDD_PATH, "r", encoding="utf-8") as f:
    bdd = json.load(f)

print("=== ÉDITEUR LIBREVIES ===")
print("Fichier :", BDD_PATH)
print("Éléments disponibles :", list(bdd["elements"].keys()))
print("Pour modifier : éditez le fichier BDD/bdd.json directement.")
print("Fermeture du jeu : l'édition est enregistrée dans BDD/bdd.json")
