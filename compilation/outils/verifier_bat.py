#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Verifie les scripts .bat du projet SANS Windows (incident 8).

Pourquoi cet outil : les .bat ne peuvent pas etre executes dans le bac a sable
Linux ou travaille l'IA. Les erreurs les plus courantes se voient pourtant a la
lecture, et ce sont celles qui ont deja casse le build :

  1. un accent ou un caractere non ASCII (cmd.exe execute en page de code 850
     et affiche des horreurs, voire casse la ligne) ;
  2. des parentheses NON ECHAPPEES dans un echo situe DANS un bloc ( ... ) ;
  3. un label appele par call/goto qui n'existe pas ;
  4. une parenthese de bloc non fermee (ou fermee deux fois).

Usage :
    python compilation/outils/verifier_bat.py                 (tous les .bat)
    python compilation/outils/verifier_bat.py fichier.bat ...
"""

import re
import sys
from pathlib import Path

RACINE = Path(__file__).resolve().parents[2]


def script_a_verifier():
    if len(sys.argv) > 1:
        return [Path(a) for a in sys.argv[1:]]
    fichiers = sorted(RACINE.glob("compilation/*.bat"))
    fichiers += sorted((RACINE / "compilation" / "outils").glob("*.bat"))
    return fichiers


def verifier(chemin):
    """Renvoie la liste des problemes trouves (vide = tout va bien)."""
    problemes = []
    octets = chemin.read_bytes()

    # 1. ASCII uniquement
    try:
        texte = octets.decode("ascii")
    except UnicodeDecodeError as erreur:
        mauvais = {
            octets[i] for i in range(erreur.start, min(erreur.end, len(octets)))
        }
        problemes.append(
            "caracteres non ASCII ligne %d (%s)"
            % (
                octets[: erreur.start].count(b"\n") + 1,
                ", ".join("0x%02X" % o for o in sorted(mauvais)),
            )
        )
        texte = octets.decode("latin-1")

    lignes = texte.split("\n")
    labels = set()
    appels = []
    profondeur = 0
    blocs_par_ligne = []

    for numero, brute in enumerate(lignes, start=1):
        ligne = brute.rstrip("\r")
        nue = ligne.strip()
        bas = nue.lower()
        if bas.startswith("rem ") or bas == "rem" or bas.startswith("::"):
            continue

        # labels
        if nue.startswith(":"):
            labels.add(nue[1:].split()[0].lower())

        # appels de sous-programmes
        for motif in (r"\bcall\s+:([\w\-]+)", r"\bgoto\s+:([\w\-]+)"):
            for cible in re.findall(motif, bas):
                appels.append((numero, cible))

        # analyse des parentheses : on retire les chaines entre guillemets
        sans_guillemets = re.sub(r'"[^"]*"', '""', nue)
        if bas.startswith("echo") and profondeur > 0:
            interieur = sans_guillemets[len("echo") :]
            interieur_sans_echappement = re.sub(r"\^.", "", interieur)
            for caractere in "()":
                if caractere in interieur_sans_echappement:
                    problemes.append(
                        "ligne %d : '%s' non echappee dans un echo en bloc : %s"
                        % (numero, caractere, nue[:70])
                    )
        profondeur += sans_guillemets.count("(") - sans_guillemets.count(")")
        blocs_par_ligne.append(profondeur)
        if profondeur < 0:
            problemes.append(
                "ligne %d : parenthese fermante de trop : %s" % (numero, nue[:70])
            )
            profondeur = 0

    if profondeur != 0:
        problemes.append(
            "parentheses de bloc non equilibrees (reste %d ouvertes)" % profondeur
        )

    for numero, cible in appels:
        if cible == "eof":
            continue
        if cible not in labels:
            problemes.append(
                "ligne %d : call/goto vers un label qui n'existe pas : :%s"
                % (numero, cible)
            )

    # Un .bat ne doit jamais etre coupe en plein milieu d'une ligne
    if octets and not octets.endswith(b"\n"):
        problemes.append("le fichier ne se termine pas par un retour a la ligne")

    return problemes


def main():
    total = 0
    for chemin in script_a_verifier():
        problemes = verifier(chemin)
        try:
            nom = chemin.resolve().relative_to(RACINE)
        except ValueError:
            nom = chemin
        if problemes:
            total += len(problemes)
            print("ECHEC %s" % nom)
            for probleme in problemes:
                print("      - %s" % probleme)
        else:
            print("OK    %s" % nom)
    if total:
        print("\n%d probleme(s) a corriger." % total)
        return 1
    print("\nTous les scripts sont propres (ASCII, blocs, labels).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
