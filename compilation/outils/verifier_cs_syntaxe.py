#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Verifie la SYNTAXE des fichiers C# du jeu, sans compilateur Unity.

Pourquoi cet outil : il n'y a ni Unity ni compilateur C# (mono/dotnet) dans
l'environnement ou travaille l'IA. Une faute de frappe dans LibreViesGame.cs se
decouvre donc seulement chez l'utilisateur, apres une compilation Unity de
plusieurs minutes. Ce script parse le fichier avec la grammaire C# de
tree-sitter et signale les erreurs de syntaxe AVANT la compilation.

Il ne remplace PAS un compilateur : il ne verifie pas les types, les methodes
appelees ni les references Unity. Il attrape en revanche les accolades, les
points-virgules, les parentheses et les constructions mal ecrites.

Installation (une seule fois, dans le bac a sable) :
    pip install --break-system-packages tree_sitter tree_sitter_c_sharp

Usage :
    python compilation/outils/verifier_cs_syntaxe.py
"""

import sys
from pathlib import Path

RACINE = Path(__file__).resolve().parents[2]
DOSSIER_ASSETS = RACINE / "compilation" / "unity" / "Assets"


def charger_parseur():
    try:
        from tree_sitter import Language, Parser
        import tree_sitter_c_sharp
    except ImportError:
        print("Module manquant. Installation :")
        print("  pip install --break-system-packages tree_sitter tree_sitter_c_sharp")
        return None
    return Parser(Language(tree_sitter_c_sharp.language()))


def verifier(parseur, chemin):
    source = chemin.read_bytes()
    arbre = parseur.parse(source)
    erreurs = []
    pile = [arbre.root_node]
    while pile:
        noeud = pile.pop()
        if noeud.type == "ERROR" or noeud.is_missing:
            ligne = source[: noeud.start_byte].count(b"\n") + 1
            extrait = (
                source[noeud.start_byte : noeud.end_byte][:60]
                .decode("utf-8", "replace")
                .replace("\n", " ")
            )
            erreurs.append((ligne, noeud.type, noeud.is_missing, extrait))
        pile.extend(noeud.children)
    return erreurs


def main():
    parseur = charger_parseur()
    if parseur is None:
        return 1

    fichiers = sorted(DOSSIER_ASSETS.rglob("*.cs"))
    if not fichiers:
        print("Aucun fichier .cs trouve dans %s" % DOSSIER_ASSETS)
        return 1

    total = 0
    for chemin in fichiers:
        erreurs = verifier(parseur, chemin)
        nom = chemin.relative_to(RACINE)
        if erreurs:
            total += len(erreurs)
            print("SYNTAXE KO : %s" % nom)
            for ligne, type_noeud, manquant, extrait in erreurs[:15]:
                print(
                    "      ligne %d : %s%s -> %s"
                    % (ligne, type_noeud, " (manquant)" if manquant else "", extrait)
                )
        else:
            print("SYNTAXE OK : %s" % nom)

    if total:
        print("\n%d erreur(s) de syntaxe : Unity refusera de compiler." % total)
        return 1
    print("\nAucune erreur de syntaxe. Penser a relire les types et les appels :")
    print("le parseur ne connait pas les API Unity.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
