#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Verifie la SYNTAXE des fichiers C# du jeu, sans compilateur Unity.

Pourquoi cet outil : il n'y a ni Unity ni compilateur C# (mono/dotnet) dans
l'environnement ou travaille l'IA. Une faute de frappe dans LibreViesGame.cs se
decouvre donc seulement chez l'utilisateur, apres une compilation Unity de
plusieurs minutes. Ce script parse le fichier avec la grammaire C# de
tree-sitter et signale les erreurs de syntaxe AVANT la compilation.

Il ne remplace PAS un compilateur : il ne verifie pas les types ni les
references Unity. Il attrape en revanche les accolades, les points-virgules,
les parentheses, les constructions mal ecrites, les APPELS a des methodes qui
n'existent pas, les BOUCLES INFINIES (une boucle qui ajoute dans la liste
qu'elle parcourt : incident 19, un gel du jeu sans aucun message) et les
variables locales qui masquent un champ de la classe (meme incident).

Installation (une seule fois, dans le bac a sable) :
    pip install --break-system-packages tree_sitter tree_sitter_c_sharp

Usage :
    python compilation/outils/verifier_cs_syntaxe.py
"""

import re
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


# API connues (Unity + .NET) : un appel a autre chose qu'une de ces fonctions
# et qu'une methode declaree dans le fichier est presque toujours une faute de
# frappe, ou l'appel a une methode renommee/supprimee (erreur que le parseur de
# syntaxe ne voit pas, mais qui empeche Unity de compiler).
API_CONNUES = set("""
GameObject Vector2 Vector3 Vector4 Quaternion Mathf Color Material Shader Resources Debug
Texture2D Font Input Time Application Screen PlayerPrefs RenderSettings QualitySettings
PrimitiveType Camera CameraClearFlags Light LightType LightShadows Mesh MeshFilter
MeshRenderer MeshCollider CapsuleCollider BoxCollider SphereCollider Collider Destroy
Object System String StringComparison Convert Random TextMesh TextAnchor Rect GUI GUIStyle
GUIContent GUISkin Texture Sprite SpriteRenderer Bounds Ray RaycastHit Physics JsonUtility
Directory Path File DateTime List Dictionary Array Exception UnityEngine MonoBehaviour
GUILayout GUIUtility Space SendMessage Instantiate Math MeshRenderer MeshFilter
AddComponent GetComponent SetActive LookRotation Euler Slerp Distance Lerp Clamp Clamp01
InverseLerp SmoothStep Sin Cos Sqrt Abs Max Min Round RoundToInt Sign Pow Atan2 Tan Floor
Ceil Exp Log MoveTowards PingPong Repeat SetParent Rotate TransformPoint Vector2Int
SetVertices SetTriangles RecalculateNormals RecalculateBounds GetBuiltinResource Load
Find HasProperty SetColor SetFloat EnableKeyword GetFloat SetInt GetInt GetKey GetKeyDown
GetKeyUp GetMouseButton GetMouseButtonDown GetMouseButtonUp GetAxis GetAxisRaw
IsInstanceOfType Equals GetType ToString GetHashCode DestroyImmediate FindObjectsOfType
Combine GetInstanceID CompareTag Invoke CancelInvoke StartCoroutine StopCoroutine
QuaternionIdentity Normalize SetResolution RunInBackground targetFrameRate deltaTime
DllImport
unscaledDeltaTime fixedDeltaTime timeScale frameCount realtimeSinceStartup
""".split())

RACINE_IF = None


def appels_inconnus(chemin):
    """Appels de type Methode(...) qui ne correspondent a rien de connu."""
    import re

    source = chemin.read_text(encoding="utf-8")
    # On retire commentaires et chaines : sinon les mots des commentaires
    # ressortent comme de faux appels.
    sans_commentaires = re.sub(r"//[^\n]*", "", source)
    sans_commentaires = re.sub(r"/\*.*?\*/", "", sans_commentaires, flags=re.S)
    sans_chaines = re.sub(r'"(\\.|[^"\\])*"', '""', sans_commentaires)
    declarees = set(re.findall(
        r"\b(?:private|public|internal|protected|static|override|sealed|virtual)\s+"
        r"(?:static\s+)?(?:override\s+)?[\w<>, \[\]\.]+?\s+(\w+)\s*\(",
        source))
    declarees |= set(re.findall(r"\b(?:class|struct)\s+(\w+)", source))
    suspects = []
    for appel in re.findall(r"(?<![\w.])([A-Z][A-Za-z0-9_]*)\s*\(", sans_chaines):
        if appel in declarees or appel in API_CONNUES:
            continue
        suspects.append(appel)
    return sorted(set(suspects))


def _sans_commentaires(chemin):
    """Source sans commentaires : evite les faux positifs des analyses."""
    import re

    source = chemin.read_text(encoding="utf-8")
    source = re.sub(r"//([^\n]*)", "", source)
    source = re.sub(r"/\*.*?\*/", "", source, flags=re.S)
    return source


def _corps_boucle(lignes, depart):
    """Texte du corps de la boucle qui commence a la ligne 'depart'."""
    texte = []
    profondeur = 0
    ouvert = False
    for ligne in lignes[depart:]:
        for caractere in ligne:
            if caractere == "{":
                profondeur += 1
                ouvert = True
            elif caractere == "}":
                profondeur -= 1
        texte.append(ligne)
        if ouvert and profondeur <= 0:
            break
        if not ouvert and len(texte) >= 2:
            break            # corps d'une seule instruction, sans accolade
    return "\n".join(texte)


def boucles_qui_gonflent(chemin):
    """Boucles qui AJOUTENT dans la liste qu'elles parcourent.

    C'est l'incident 19 (gel du jeu au demarrage) : une boucle du genre

        for (int g = 0; g < portails.Count; g++)
            portails.Add(new Vector2(...));

    ne s'arrete JAMAIS : la liste grandit d'un element a chaque tour, donc la
    condition reste vraie. Le jeu se fige sans aucun message. Aucun compilateur
    ne signale cette faute : elle est donc controlee ici.
    """
    import re

    lignes = _sans_commentaires(chemin).split("\n")
    suspects = []
    for i, ligne in enumerate(lignes):
        trouve = re.search(
            r"\bfor\s*\([^;]*;\s*[^;]*?([A-Za-z_]\w*)\s*\.\s*Count", ligne)
        if not trouve:
            continue
        nom = trouve.group(1)
        corps = _corps_boucle(lignes, i)
        if re.search(r"\b%s\s*\.\s*(Add|Insert)\s*\(" % re.escape(nom), corps):
            suspects.append((i + 1, nom))
    return suspects


MOTS_CLES_CS = set("""
else if return while for foreach switch case using lock yield throw new
var int float double bool string void true false null this base
get set public private protected internal static readonly const class struct
""".split())


def _profondeurs(lignes):
    """Profondeur d'accolades de chaque ligne, et plages des classes imbriquees.

    Une classe imbriquee (Maillage, EnemyState...) a ses propres champs : le
    masquage y est sans consequence, et ses champs ne doivent pas etre compares
    a ceux de la classe principale. On repere donc les plages a ignorer.
    """
    profondeur = 0
    niveaux = []
    apres = []
    for ligne in lignes:
        niveaux.append(profondeur)
        profondeur += ligne.count("{") - ligne.count("}")
        apres.append(profondeur)

    imbrique = [False] * len(lignes)
    i = 0
    while i < len(lignes):
        if niveaux[i] == 1 and re.search(r"\bclass\s+\w+", lignes[i]):
            j = i + 1
            while j < len(lignes) and apres[j] > 1:
                imbrique[j] = True
                j += 1
            if j < len(lignes):
                imbrique[j] = True        # accolade fermante de la classe
            i = j + 1
        else:
            i += 1
    return niveaux, imbrique


def champs_masques(chemin):
    """Variables locales qui portent le NOM d'un champ de la classe.

    Incident 19 : 'var portails = new List<Vector2>()' dans une methode masquait
    le champ 'portails'. Tout ce qui suivait travaillait sur la copie locale :
    la boucle de construction tournait a l'infini et les gardes ne recevaient
    plus aucun portail. Un avertissement ici evite de refaire la faute.

    Seuls les champs de la classe principale sont consideres : les classes
    imbriquees (Maillage...) ont leurs propres noms et n'ont pas d'effet ici.
    """
    lignes = _sans_commentaires(chemin).split("\n")
    niveaux, imbrique = _profondeurs(lignes)

    champs = set()
    for i, ligne in enumerate(lignes):
        if niveaux[i] != 1 or imbrique[i]:
            continue
        trouve = re.match(
            r"\s*(?:private|public|protected|internal)\s+[^;{}=()]*?\s(\w+)\s*(?:=|;)", ligne)
        if trouve:
            champs.add(trouve.group(1))

    suspects = []
    for i, ligne in enumerate(lignes):
        if niveaux[i] < 2 or imbrique[i]:
            continue
        trouve = re.match(r"\s*(?:var|(?:[A-Za-z_][\w<>,.\[\]]*))\s+(\w+)\s*=", ligne)
        if not trouve:
            continue
        mot = re.match(r"\s*(?:var|([A-Za-z_]\w*))", ligne)
        if mot and mot.group(1) in MOTS_CLES_CS:
            continue
        nom = trouve.group(1)
        if nom in champs:
            suspects.append((i + 1, nom))
    return suspects


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
        suspects = appels_inconnus(chemin)
        nom = chemin.relative_to(RACINE)
        if suspects:
            total += len(suspects)
            print("APPELS A VERIFIER : %s" % nom)
            for appel in suspects:
                print("      %s(...) n'est ni declare ici ni connu de l'API" % appel)
        for ligne, nom_liste in boucles_qui_gonflent(chemin):
            total += 1
            print("BOUCLE INFINIE : %s" % nom)
            print("      ligne %d : la boucle qui parcourt '%s.Count' AJOUTE dans "
                  "'%s'" % (ligne, nom_liste, nom_liste))
            print("      -> elle ne s'arretera jamais (le jeu se figera). "
                  "Utiliser une seconde liste.")
        for ligne, nom_champ in champs_masques(chemin):
            print("A VERIFIER : %s" % nom)
            print("      ligne %d : la variable locale '%s' masque le champ du "
                  "meme nom" % (ligne, nom_champ))
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
