"""Transfert de texture entre deux pieces (GLB).

On prend l'image de texture de la piece source (ex. la masse de l'objet 1) et on
recalcule les coordonnees UV de la piece cible (ex. la masse de l'objet 2) pour
que l'image la recouvre, en l'etirant ou la resserrant a sa taille.

Methode : projection plane. On projette la piece cible sur le plan perpendiculaire
a sa plus petite dimension, puis on ramene ces coordonnees entre 0 et 1.
Resultat exact seulement si les deux formes se ressemblent ; sinon la texture est deformee.
"""
import os

import numpy as np
import trimesh


def image_de_texture(mesh):
    """Image de texture d'un maillage (None si absente)."""
    vis = mesh.visual
    if not isinstance(vis, trimesh.visual.TextureVisuals):
        return None
    mat = vis.material
    if mat is None:
        return None
    image = getattr(mat, "image", None)
    if image is None:
        image = getattr(mat, "baseColorTexture", None)
    return image


def _maillage_unique(fichier):
    """Fusionne toutes les pieces d'un GLB en un seul maillage (les textures sont relues a part)."""
    scene = trimesh.load(fichier, force="scene")
    morceaux = [g for g in scene.dump()]
    if not morceaux:
        raise ValueError("GLB vide : " + fichier)
    return morceaux


def uv_projetees(sommets, garder_proportions=False):
    """Coordonnees UV (entre 0 et 1) par projection plane sur la plus petite dimension."""
    bmin = sommets.min(axis=0)
    bmax = sommets.max(axis=0)
    etendue = np.maximum(bmax - bmin, 1e-9)
    axe = int(np.argmin(etendue))
    a, b = [i for i in range(3) if i != axe]
    if garder_proportions:
        # Meme echelle sur les deux axes : la texture n'est pas deformee.
        echelle = max(etendue[a], etendue[b])
        u = (sommets[:, a] - bmin[a]) / echelle
        v = (sommets[:, b] - bmin[b]) / echelle
    else:
        # Etirement : la texture couvre exactement la piece (peut se deformer).
        u = (sommets[:, a] - bmin[a]) / etendue[a]
        v = (sommets[:, b] - bmin[b]) / etendue[b]
    return np.stack([u, v], axis=1)


def transferer(fichier_source, fichier_cible, fichier_sortie, garder_proportions=False):
    """Ecrit fichier_sortie : la piece cible avec la texture de la piece source.
    Renvoie le nombre de sommets de la cible."""
    morceaux_source = _maillage_unique(fichier_source)
    image = None
    for m in morceaux_source:
        image = image_de_texture(m)
        if image is not None:
            break
    if image is None:
        raise ValueError("la piece source n'a pas de texture")

    morceaux_cible = _maillage_unique(fichier_cible)
    cible = trimesh.util.concatenate(morceaux_cible)
    uv = uv_projetees(np.asarray(cible.vertices, dtype=float), garder_proportions)
    cible.visual = trimesh.visual.TextureVisuals(
        uv=uv, material=trimesh.visual.material.SimpleMaterial(image=image))
    os.makedirs(os.path.dirname(os.path.abspath(fichier_sortie)), exist_ok=True)
    cible.export(fichier_sortie)
    return len(cible.vertices)


if __name__ == "__main__":
    import argparse
    parseur = argparse.ArgumentParser(description="Applique la texture d'une piece sur une autre.")
    parseur.add_argument("source")
    parseur.add_argument("cible")
    parseur.add_argument("sortie")
    parseur.add_argument("--garder-proportions", action="store_true")
    args = parseur.parse_args()
    n = transferer(args.source, args.cible, args.sortie, args.garder_proportions)
    print("Texture appliquee : %d sommets -> %s" % (n, args.sortie))
