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
