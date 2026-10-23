"""Decoupage generique d'un modele 3D (GLB) en pieces, avec noms automatiques editables.

Principe :
1. Les morceaux non relies entre eux (composantes connexes) deviennent des pieces.
2. Si le maillage est d'un seul tenant, il est decoupe en K groupes de faces
   proches en position et en orientation (k-means sur centre + normale).
3. Chaque piece recoit un nom automatique deduit de sa forme et de sa position
   (ex. « piece haute allongee »). L'utilisateur peut le changer.
4. Le catalogue (JSON) liste les pieces ; renommer et fusionner se font sur ce catalogue.

Ce module ne depend que de numpy, scipy et trimesh.
"""
import json
import os

import numpy as np
import trimesh
from scipy.cluster.vq import kmeans2


def _nom_automatique(morceau, centre_total, taille_totale):
    """Nom lisible deduit de la position et de la forme de la piece (pas une verite)."""
    c = morceau.bounds.mean(axis=0)
    d = (c - centre_total) / np.maximum(taille_totale, 1e-6)
    positions = []
    if d[1] > 0.2:
        positions.append("haute")
    elif d[1] < -0.2:
        positions.append("basse")
    if d[0] > 0.2:
        positions.append("droite")
    elif d[0] < -0.2:
        positions.append("gauche")
    if d[2] > 0.2:
        positions.append("avant")
    elif d[2] < -0.2:
        positions.append("arriere")
    ext = np.sort(morceau.extents)[::-1]
    if ext[0] > 2.5 * ext[1]:
        forme = "allongee"
    elif ext[2] < 0.25 * ext[0]:
        forme = "plate"
    else:
        forme = "compacte"
    base = "piece " + forme
    return base + (" " + " ".join(positions) if positions else " centrale")


def _decouper_en_composantes(mesh, taille_min_faces, max_pieces=8):
    """Pieces = gros morceaux relies. Les petits fragments (bruit de generation) sont
    rattaches au gros morceau le plus proche. Renvoie None si un seul gros morceau."""
    morceaux = mesh.split(only_watertight=False)
    total = sum(len(m.faces) for m in morceaux)
    seuil = max(taille_min_faces, int(0.02 * total))
    majeurs = sorted([m for m in morceaux if len(m.faces) >= seuil], key=lambda m: -len(m.faces))
    if len(majeurs) <= 1:
        return None
    majeurs = majeurs[:max_pieces]
    groupes = [[m] for m in majeurs]
    centres = np.array([m.bounds.mean(axis=0) for m in majeurs])
    for m in morceaux:
        if any(m is g for lst in groupes for g in lst):
            continue
        proche = int(np.argmin(np.linalg.norm(centres - m.bounds.mean(axis=0), axis=1)))
        groupes[proche].append(m)
    return [trimesh.util.concatenate(lst) if len(lst) > 1 else lst[0] for lst in groupes]


def _decouper_par_groupes(mesh, k):
    """k-means sur centre et normale des faces : regroupe les faces proches."""
    centres = mesh.triangles_center
    normales = mesh.face_normals
    taille = np.maximum(mesh.extents.max(), 1e-6)
    # Position seulement : sur un modele genere, les normales bruitees coupent en lamelles.
    caracteristiques = centres / taille
    del normales
    np.random.seed(0)
    _, etiquettes = kmeans2(caracteristiques.astype(np.float64), k, minit="++", seed=0)
    morceaux = []
    for g in range(k):
        masque = etiquettes == g
        if masque.sum() == 0:
            continue
        sous = mesh.submesh([np.where(masque)[0]], append=True, repair=False)
        morceaux.append(sous)
    return morceaux


def decouper(fichier_glb, dossier_sortie, k=2, taille_min_faces=4):
    """Decoupe le GLB et ecrit les pieces + catalogue.json. Renvoie le catalogue (dict)."""
    scene = trimesh.load(fichier_glb, force="scene")
    if not scene.geometry:
        raise ValueError("GLB vide : " + fichier_glb)
    mesh = trimesh.util.concatenate(list(scene.dump()))
    mesh.merge_vertices()
    os.makedirs(dossier_sortie, exist_ok=True)

    morceaux = _decouper_en_composantes(mesh, taille_min_faces)
    if morceaux is None:
        morceaux = _decouper_par_groupes(mesh, k)

    centre_total = mesh.bounds.mean(axis=0)
    taille_totale = mesh.extents
    pieces = []
    for i, m in enumerate(morceaux):
        nom_fichier = os.path.join(dossier_sortie, "piece_%02d.glb" % i)
        m.export(nom_fichier)
        pieces.append({
            "id": i,
            "nom": _nom_automatique(m, centre_total, taille_totale),
            "nom_auto": True,
            "fichier": nom_fichier,
            "faces": int(len(m.faces)),
        })
    catalogue = {"source": os.path.abspath(fichier_glb), "pieces": pieces}
    with open(os.path.join(dossier_sortie, "catalogue.json"), "w", encoding="utf-8") as f:
        json.dump(catalogue, f, ensure_ascii=False, indent=2)
    return catalogue


def renommer(catalogue, id_piece, nouveau_nom):
    for p in catalogue["pieces"]:
        if p["id"] == id_piece:
            p["nom"] = nouveau_nom
            p["nom_auto"] = False
            return catalogue
    raise KeyError("piece inconnue : %s" % id_piece)


def fusionner(catalogue, ids, dossier_sortie):
    """Fusionne plusieurs pieces en une seule (fichier GLB recree)."""
    ids = list(ids)
    if len(ids) < 2:
        raise ValueError("il faut au moins deux pieces")
    selection = [p for p in catalogue["pieces"] if p["id"] in ids]
    maillages = [trimesh.load(p["fichier"], force="mesh") for p in selection]
    fusion = trimesh.util.concatenate(maillages)
    nouvel_id = max(p["id"] for p in catalogue["pieces"]) + 1
    fichier = os.path.join(dossier_sortie, "piece_%02d.glb" % nouvel_id)
    fusion.export(fichier)
    catalogue["pieces"] = [p for p in catalogue["pieces"] if p["id"] not in ids]
    catalogue["pieces"].append({
        "id": nouvel_id,
        "nom": " + ".join(p["nom"] for p in selection),
        "nom_auto": False,
        "fichier": fichier,
        "faces": int(len(fusion.faces)),
    })
    return catalogue


if __name__ == "__main__":
    import argparse
    parseur = argparse.ArgumentParser(description="Decoupe un GLB en pieces.")
    parseur.add_argument("glb")
    parseur.add_argument("dossier")
    parseur.add_argument("--k", type=int, default=4)
    args = parseur.parse_args()
    cat = decouper(args.glb, args.dossier, k=args.k)
    for p in cat["pieces"]:
        print(p["id"], p["nom"], p["faces"], "faces")
