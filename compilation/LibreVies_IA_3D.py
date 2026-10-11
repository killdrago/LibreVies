#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
LibreVies - Photo vers modele 3D (TripoSR, processeur, sans carte NVIDIA).

UN SEUL FICHIER. Double-clic (ou python LibreVies_IA_3D.py) :
  1. cree le dossier compilation\\IA\\ (tout ce qui est IA est la dedans) ;
  2. cree un environnement Python prive (IA\\venv) ;
  3. installe automatiquement les paquets manquants (PyTorch, TripoSR, rembg...) ;
  4. telecharge le code officiel TripoSR (IA\\TripoSR) ;
  5. ouvre la fenetre : tu choisis une photo, tu indiques la taille reelle,
     et le modele 3D est cree dans IA\\sorties\\<nom>\\ (.glb et .obj).

Le premier lancement telecharge beaucoup de choses (plusieurs Go) : c'est normal.
Les lancements suivants ne retelechargent rien.

Le fichier garde les sorties dans compilation\\IA\\. Le dossier IA est ignore par Git.
"""

import argparse
import os
import queue
import shutil
import subprocess
import sys
import threading
import time
import urllib.request
import zipfile

ROOT = os.path.dirname(os.path.abspath(__file__))
IA = os.path.join(ROOT, "IA")
VENV = os.path.join(IA, "venv")
TRIPO = os.path.join(IA, "TripoSR")
MODELES = os.path.join(IA, "modeles")
SORTIES = os.path.join(IA, "sorties")
UNITY_ITEMS = os.path.join(ROOT, "unity", "Assets", "Resources", "Items")
TRIPO_ZIP_URL = "https://codeload.github.com/VAST-AI-Research/TripoSR/zip/refs/heads/main"

# Paquets pour TripoSR (torchmcubes est remplace par scikit-image, plus simple sous Windows).
PAQUETS = [
    "pip",
    "torch",
    "torchvision",
    "numpy<2",
    "pillow",
    "xatlas",
    "einops",
    "omegaconf",
    "transformers==4.35.0",
    "huggingface-hub<1.0",
    "trimesh",
    "rembg",
    "onnxruntime",
    "scikit-image",
    "imageio",
    "imageio-ffmpeg",
]

# Remplacement de torchmcubes : meme fonction marching_cubes(volume, niveau) -> (sommets, faces).
SHIM_TORCHMCUBES = '''"""Remplacement de torchmcubes par scikit-image (installe automatiquement)."""
import numpy as np
import torch
from skimage.measure import marching_cubes as _mc


def marching_cubes(volume, level):
    v = volume.detach().cpu().numpy() if torch.is_tensor(volume) else np.asarray(volume)
    verts, faces, _normals, _values = _mc(v, level=float(level))
    return torch.from_numpy(verts.astype(np.float32)), torch.from_numpy(faces.astype(np.int64))
'''


def venv_python():
    if os.name == "nt":
        return os.path.join(VENV, "Scripts", "python.exe")
    return os.path.join(VENV, "bin", "python")


def env_ia():
    """Variables d'environnement : tous les modeles sont ranges dans IA\\modeles."""
    env = dict(os.environ)
    env["HF_HOME"] = os.path.join(MODELES, "huggingface")
    env["U2NET_HOME"] = os.path.join(MODELES, "rembg")
    env["PYTHONIOENCODING"] = "utf-8"
    env["PYTHONUTF8"] = "1"
    return env


# ---------------------------------------------------------------------------
# Installation (mode graphique, puis mode terminal)
# ---------------------------------------------------------------------------

def lancer(cmd, log, cwd=None):
    """Lance une commande et envoie chaque ligne au journal. Retourne le code de sortie."""
    log("$ " + " ".join(os.path.basename(c) if i == 0 else c for i, c in enumerate(cmd)))
    proc = subprocess.Popen(
        cmd, cwd=cwd, env=env_ia(), stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
        text=True, encoding="utf-8", errors="replace",
    )
    for ligne in proc.stdout:
        ligne = ligne.rstrip()
        if ligne:
            log("  " + ligne)
    return proc.wait()


def paquets_presents():
    """Vrai si tous les modules importants sont deja installes dans l'environnement IA."""
    test = "import torch, torchvision, trimesh, rembg, skimage, transformers, einops, omegaconf, PIL, xatlas"
    return subprocess.call([venv_python(), "-c", test], env=env_ia(),
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) == 0


def installer(log):
    """Verifie et installe tout ce qui manque. Leve une exception en cas d'echec."""
    log("Dossier IA : " + IA)
    for dossier in (IA, MODELES, SORTIES):
        os.makedirs(dossier, exist_ok=True)

    if not os.path.isfile(venv_python()):
        log("Creation de l'environnement Python prive (IA\\venv)...")
        if lancer([sys.executable, "-m", "venv", VENV], log) != 0:
            raise RuntimeError("impossible de creer l'environnement IA\\venv")
    else:
        log("Environnement IA\\venv deja present.")

    if paquets_presents():
        log("Paquets Python deja installes.")
    else:
        log("Installation des paquets (long la premiere fois, plusieurs Go)...")
        if lancer([venv_python(), "-m", "pip", "install", "--upgrade", "--disable-pip-version-check"]
                  + PAQUETS, log) != 0:
            raise RuntimeError("echec de l'installation des paquets Python")
        if not paquets_presents():
            raise RuntimeError("paquets installes mais imports impossibles : voir le journal")
        log("Paquets installes.")

    if not os.path.isfile(os.path.join(TRIPO, "tsr", "system.py")):
        log("Telechargement du code officiel TripoSR...")
        zip_path = os.path.join(IA, "triposr.zip")
        urllib.request.urlretrieve(TRIPO_ZIP_URL, zip_path)
        with zipfile.ZipFile(zip_path) as archive:
            archive.extractall(IA)
        os.remove(zip_path)
        extrait = os.path.join(IA, "TripoSR-main")
        if os.path.isdir(TRIPO):
            shutil.rmtree(TRIPO)
        os.rename(extrait, TRIPO)
        log("TripoSR installe dans IA\\TripoSR.")
    else:
        log("Code TripoSR deja present.")

    with open(os.path.join(TRIPO, "torchmcubes.py"), "w", encoding="utf-8") as f:
        f.write(SHIM_TORCHMCUBES)
    log("Installation terminee. Modeles (telechargement au premier calcul) : IA\\modeles.")


# ---------------------------------------------------------------------------
# Generation (execute par l'environnement IA, avec --worker)
# ---------------------------------------------------------------------------

def colorer_depuis_photo(mesh, image):
    """Remplace les couleurs de sommets de TripoSR (pales) par celles de la photo.

    Le maillage est projete sur la photo : l'axe Y du modele (tete -> bout du manche)
    correspond a l'axe horizontal de la photo. L'axe de profondeur (X ou Z) et son sens
    sont choisis automatiquement : on garde la correspondance ou le manche (clair) et
    la tete (sombre) ressortent le mieux, comme sur la photo.
    """
    import numpy as np

    im = np.asarray(image.convert("RGB")).astype(np.float32)
    bord = np.concatenate([im[:4].reshape(-1, 3), im[-4:].reshape(-1, 3)])
    fond = np.median(bord, axis=0)
    ys, xs = np.where(np.abs(im - fond).sum(-1) > 60)
    if xs.size == 0:
        raise RuntimeError("objet introuvable dans la photo")
    X0, X1, Y0, Y1 = xs.min(), xs.max(), ys.min(), ys.max()

    V = np.asarray(mesh.vertices, dtype=np.float64)
    centre = (V.min(0) + V.max(0)) / 2.0
    demi = (V.max(0) - V.min(0)) / 2.0
    hauteur = V[:, 1].max() - V[:, 1].min()
    ymin = V[:, 1].min()
    tete = V[:, 1] < ymin + 0.17 * hauteur
    manche = (V[:, 1] > ymin + 0.40 * hauteur) & \
        (np.abs(V[:, 0] - centre[0]) < 0.35 * demi[0]) & \
        (np.abs(V[:, 2] - centre[2]) < 0.35 * demi[2])
    if not tete.any() or not manche.any():
        raise RuntimeError("forme du modele inattendue")

    def projeter(axe, signe):
        px = X0 + (V[:, 1] - ymin) / hauteur * (X1 - X0)
        d = (V[:, axe] - centre[axe]) / max(demi[axe], 1e-9)
        py = (Y0 + Y1) / 2.0 - signe * d * (Y1 - Y0) / 2.0
        px = np.clip(np.round(px).astype(int), 0, im.shape[1] - 1)
        py = np.clip(np.round(py).astype(int), 0, im.shape[0] - 1)
        return im[py, px]

    meilleur = None
    for axe in (0, 2):
        for signe in (1, -1):
            c = projeter(axe, signe)
            lum = c.mean(axis=1)
            score = lum[manche].mean() - lum[tete].mean()
            if meilleur is None or score > meilleur[0]:
                meilleur = (score, c, axe, signe)
    score, couleurs, axe, signe = meilleur
    if score < 30:
        raise RuntimeError("la photo ne correspond pas clairement au modele (ecart %.0f)" % score)

    rgba = np.zeros((len(V), 4), dtype=np.uint8)
    rgba[:, :3] = np.clip(couleurs, 0, 255).astype(np.uint8)
    rgba[:, 3] = 255
    mesh.visual.vertex_colors = rgba
    print("Couleurs reprises de la photo (axe %s, sens %+d, ecart %.0f)." % (
        "XZ"[axe // 2], signe, score), flush=True)


def lisser_couleurs(mesh, passes=4):
    """Adoucit les couleurs de sommets : chaque sommet prend la moyenne de ses voisins
    et de lui-meme. Supprime les traits de couleur dus a la projection sur une surface irreguliere."""
    import numpy as np
    c = np.asarray(mesh.visual.vertex_colors)[:, :3].astype(np.float64)
    voisins = mesh.vertex_neighbors
    for _ in range(passes):
        nouveau = np.empty_like(c)
        for i, v in enumerate(voisins):
            nouveau[i] = (c[i] + c[v].sum(axis=0)) / (1 + len(v))
        c = nouveau
    rgba = np.zeros((len(c), 4), dtype=np.uint8)
    rgba[:, :3] = np.clip(c, 0, 255).astype(np.uint8)
    rgba[:, 3] = 255
    mesh.visual.vertex_colors = rgba


def texturer(mesh, resolution=1024):
    """Deplie le maillage (UV, xatlas) et cuit les couleurs de la photo dans une texture.

    Les couleurs viennent des sommets calcules par TripoSR (issues de la photo).
    Retourne (mesh_uv, image_PIL).
    """
    import numpy as np
    import xatlas
    from PIL import Image as _Image

    if mesh.visual.kind != "vertex":
        raise RuntimeError("pas de couleurs de sommets dans le modele")
    V = np.asarray(mesh.vertices, dtype=np.float32)
    F = np.asarray(mesh.faces, dtype=np.uint32)
    couleurs = np.asarray(mesh.visual.vertex_colors)[:, :3].astype(np.float32) / 255.0

    vmap, idx, uv = xatlas.parametrize(V, F)
    V2 = V[vmap]
    C2 = couleurs[vmap]
    F2 = idx.astype(np.int64)
    uv = uv.astype(np.float32)

    img = np.zeros((resolution, resolution, 3), dtype=np.float32)
    img[:, :] = np.clip(couleurs.mean(axis=0), 0, 1)
    for a, b, c in F2:
        pa = uv[a] * (resolution - 1)
        pb = uv[b] * (resolution - 1)
        pc = uv[c] * (resolution - 1)
        xmin = max(int(np.floor(min(pa[0], pb[0], pc[0]))), 0)
        xmax = min(int(np.ceil(max(pa[0], pb[0], pc[0]))), resolution - 1)
        ymin = max(int(np.floor(min(pa[1], pb[1], pc[1]))), 0)
        ymax = min(int(np.ceil(max(pa[1], pb[1], pc[1]))), resolution - 1)
        if xmax < xmin or ymax < ymin:
            continue
        xs, ys = np.meshgrid(np.arange(xmin, xmax + 1), np.arange(ymin, ymax + 1))
        px = xs.ravel().astype(np.float32)
        py = ys.ravel().astype(np.float32)
        den = (pb[1] - pc[1]) * (pa[0] - pc[0]) + (pc[0] - pb[0]) * (pa[1] - pc[1])
        if abs(den) < 1e-9:
            continue
        l1 = ((pb[1] - pc[1]) * (px - pc[0]) + (pc[0] - pb[0]) * (py - pc[1])) / den
        l2 = ((pc[1] - pa[1]) * (px - pc[0]) + (pa[0] - pc[0]) * (py - pc[1])) / den
        l3 = 1.0 - l1 - l2
        dedans = (l1 >= -1e-4) & (l2 >= -1e-4) & (l3 >= -1e-4)
        if not dedans.any():
            continue
        col = (l1[dedans, None] * C2[a] + l2[dedans, None] * C2[b] + l3[dedans, None] * C2[c])
        img[py[dedans].astype(np.int64), px[dedans].astype(np.int64)] = col

    # Convention UV OBJ / glTF : image retournee verticalement.
    image = _Image.fromarray(np.clip(img * 255, 0, 255).astype(np.uint8), "RGB")
    image = image.transpose(_Image.FLIP_TOP_BOTTOM)
    import trimesh as _trimesh
    mesh_uv = _trimesh.Trimesh(vertices=V2, faces=F2, process=False)
    mesh_uv.visual = _trimesh.visual.TextureVisuals(
        uv=uv, material=_trimesh.visual.material.SimpleMaterial(image=image))
    return mesh_uv, image, uv, F2, V2


def exporter_texture(mesh_uv, image, uv, F2, V2, dossier, nom):
    """Ecrit OBJ + MTL + texture PNG (logiciel 3D) et GLB (texture incluse)."""
    png = nom + "_texture.png"
    image.save(os.path.join(dossier, png))
    lignes = ["mtllib " + nom + ".mtl", "o " + nom, "usemtl Texture"]
    for p in V2:
        lignes.append("v %.6f %.6f %.6f" % (p[0], p[1], p[2]))
    for t in uv:
        lignes.append("vt %.6f %.6f" % (t[0], t[1]))
    for a, b, c in F2:
        a, b, c = int(a) + 1, int(b) + 1, int(c) + 1
        lignes.append("f %d/%d %d/%d %d/%d" % (a, a, b, b, c, c))
    with open(os.path.join(dossier, nom + ".obj"), "w", encoding="utf-8") as f:
        f.write("\n".join(lignes) + "\n")
    with open(os.path.join(dossier, nom + ".mtl"), "w", encoding="utf-8") as f:
        f.write("newmtl Texture\nKa 1 1 1\nKd 1 1 1\nKs 0.1 0.1 0.1\nNs 20\nmap_Kd " + png + "\n")
    mesh_uv.export(os.path.join(dossier, nom + ".glb"))




def recadrer_objet(image, taille_sortie=1024):
    """Recadre la photo autour de l'objet (fond uni suppose), en carre, avec une marge.

    L'objet occupe alors une plus grande partie de l'image avant la decoupe du fond.
    Si l'objet n'est pas detecte, la photo est renvoyee telle quelle.
    """
    import numpy as np
    from PIL import Image as _Image

    rgb = image.convert("RGB")
    im = np.asarray(rgb).astype(np.float32)
    h, w = im.shape[:2]
    bord = np.concatenate([im[:4].reshape(-1, 3), im[-4:].reshape(-1, 3),
                           im[:, :4].reshape(-1, 3), im[:, -4:].reshape(-1, 3)])
    fond = np.median(bord, axis=0)
    masque = np.abs(im - fond).sum(-1) > 60
    ys, xs = np.where(masque)
    if xs.size < 50:
        return image
    x0, x1 = np.percentile(xs, [1, 99])
    y0, y1 = np.percentile(ys, [1, 99])
    marge = 0.12 * max(x1 - x0, y1 - y0)
    x0, x1 = max(x0 - marge, 0), min(x1 + marge, w - 1)
    y0, y1 = max(y0 - marge, 0), min(y1 + marge, h - 1)
    cote = max(x1 - x0, y1 - y0)
    cx, cy = (x0 + x1) / 2.0, (y0 + y1) / 2.0
    boite = (int(cx - cote / 2), int(cy - cote / 2), int(cx + cote / 2), int(cy + cote / 2))
    carre = _Image.new("RGB", (int(cote), int(cote)), tuple(int(v) for v in fond))
    carre.paste(rgb.crop(boite), (0, 0))
    return carre.resize((taille_sortie, taille_sortie), _Image.LANCZOS)


# ---------------------------------------------------------------------------
# Generateur parametrique (sans IA) : marteau de mecanicien, lisse, avec textures
# ---------------------------------------------------------------------------

PARAM = {
    # Manche : axe X, la tete est a x < 0.
    "L_manche": 0.27, "r_col": 0.0175, "r_bout": 0.0140, "r_bout_arr": 0.012, "x_debut": -0.040,
    # Tete : fine dans l'axe du manche (X), haute (Y), large (Z), angles arrondis.
    "tete_x0": -0.060, "tete_x1": -0.022, "tete_y": 0.040, "tete_z": 0.021, "rayon_arr": 0.006,
    # Panne : monte au-dessus de la tete, comme sur la photo.
    "panne_y": 0.040,
}
TEX_PARAM = 512


def _manche_param(n_angle=96, n_station=140):
    import numpy as np
    p = PARAM
    t = np.linspace(0.0, 1.0, n_station)
    xs = p["x_debut"] + (p["L_manche"] - p["x_debut"]) * (0.5 - 0.5 * np.cos(np.pi * t))
    rs = []
    for x in xs:
        u = np.clip(x / p["L_manche"], 0.0, 1.0)
        r = p["r_col"] + (p["r_bout"] - p["r_col"]) * u
        if x < 0:
            r = p["r_col"]
        debut = p["L_manche"] - p["r_bout_arr"]
        if x > debut:
            k = min((x - debut) / p["r_bout_arr"], 1.0)
            r = (p["r_bout"] - p["r_bout_arr"]) + p["r_bout_arr"] * np.sqrt(max(0.0, 1.0 - k * k))
        rs.append(r)
    th = np.linspace(0, 2 * np.pi, n_angle + 1)
    V, UV = [], []
    for x, r in zip(xs, rs):
        v = (x - p["x_debut"]) / (p["L_manche"] - p["x_debut"])
        for a in th:
            V.append([x, r * np.cos(a), r * np.sin(a)])
            UV.append([a / (2 * np.pi), v])
    cols = n_angle + 1
    F = []
    for i in range(n_station - 1):
        for j in range(n_angle):
            a = i * cols + j
            b = a + 1
            c = a + cols
            d = c + 1
            F.append([a, b, d])
            F.append([a, d, c])
    c0 = len(V)
    V.append([xs[0], 0, 0])
    UV.append([0.5, 0.0])
    c1 = len(V)
    V.append([xs[-1], 0, 0])
    UV.append([0.5, 1.0])
    last = (n_station - 1) * cols
    for j in range(n_angle):
        F.append([c0, j + 1, j])
        F.append([c1, last + j, last + j + 1])
    return np.array(V), np.array(UV), np.array(F)


def _sphere_param(c, r):
    import trimesh
    s = trimesh.creation.icosphere(subdivisions=3, radius=r)
    s.apply_translation(c)
    return s


def _hull_param(pieces):
    import numpy as np
    import trimesh
    m = trimesh.util.concatenate(pieces).convex_hull
    V = np.asarray(m.vertices)
    F = np.asarray(m.faces)
    mn, mx = V.min(0), V.max(0)
    UV = np.column_stack([
        (V[:, 1] - mn[1]) / (mx[1] - mn[1] + 1e-9),
        (V[:, 2] - mn[2]) / (mx[2] - mn[2] + 1e-9),
    ])
    return V, UV, F


def _tete_param():
    p = PARAM
    r = p["rayon_arr"]
    pts = []
    for x in (p["tete_x0"] + r, p["tete_x1"] - r):
        for y in (-p["tete_y"] + r, p["tete_y"] - r):
            for z in (-p["tete_z"] + r, p["tete_z"] - r):
                pts.append([x, y, z])
    return _hull_param([_sphere_param(q, r) for q in pts])


def _panne_param():
    p = PARAM
    h = p["tete_y"]
    c = (p["tete_x0"] + p["tete_x1"]) / 2.0
    return _hull_param([
        _sphere_param((c - 0.005, h - 0.006, -0.012), 0.0080),
        _sphere_param((c - 0.005, h - 0.006, 0.012), 0.0080),
        _sphere_param((c - 0.009, h + 0.015, 0.0), 0.0060),
        _sphere_param((c - 0.013, h + 0.034, 0.0), 0.0035),
    ])


def _texture_bois_param(seed=7):
    import numpy as np
    from PIL import Image
    rng = np.random.default_rng(seed)
    u = np.linspace(0, 1, TEX_PARAM, endpoint=False)[None, :]
    v = np.linspace(0, 1, TEX_PARAM, endpoint=False)[:, None]
    deform = np.zeros((TEX_PARAM, 1))
    for _ in range(3):
        f = rng.integers(1, 4)
        ph = rng.uniform(0, 2 * np.pi)
        amp = rng.uniform(0.02, 0.05)
        deform += amp * np.sin(2 * np.pi * f * v + ph)
    cernes = np.zeros((TEX_PARAM, TEX_PARAM))
    for _ in range(4):
        k = rng.integers(3, 14)
        ph = rng.uniform(0, 2 * np.pi)
        cernes += np.sin(2 * np.pi * (k * u + deform) + ph)
    cernes = (cernes - cernes.min()) / (cernes.max() - cernes.min())
    veine = cernes ** 3
    claire = np.array([0.91, 0.82, 0.63])
    fonce = np.array([0.74, 0.60, 0.42])
    mix = veine[..., None] * fonce + (1 - veine[..., None]) * claire
    mix *= (1.0 + 0.015 * rng.standard_normal((TEX_PARAM, TEX_PARAM, 1)))
    return Image.fromarray(np.clip(mix * 255, 0, 255).astype(np.uint8), "RGB")


def _texture_acier_param(seed=3):
    import numpy as np
    from PIL import Image
    rng = np.random.default_rng(seed)
    base = np.array([0.13, 0.13, 0.14])
    bruit = 1.0 + 0.03 * rng.standard_normal((TEX_PARAM, TEX_PARAM, 1))
    return Image.fromarray(np.clip(base * bruit * 255, 0, 255).astype(np.uint8), "RGB")


def _parametres_depuis_triposr(mesh):
    """Mesure le maillage TripoSR (manche sur Y, tete en bas) pour caler le modele lisse."""
    import numpy as np
    V = np.asarray(mesh.vertices, dtype=np.float64)
    mn, mx = V.min(0), V.max(0)
    T = mx[1] - mn[1]
    # Rayon du manche : mediane de la distance a l'axe sur une tranche au milieu du manche.
    tranche = V[np.abs(V[:, 1] - (mn[1] + 0.55 * T)) < 0.01 * T]
    cx, cz = tranche[:, 0].mean(), tranche[:, 2].mean()
    r_col = float(np.median(np.hypot(tranche[:, 0] - cx, tranche[:, 2] - cz)))
    # Tete : bas du modele (10 % de la longueur).
    bas = V[V[:, 1] < mn[1] + 0.10 * T]
    demi_hauteur = (bas[:, 0].max() - bas[:, 0].min()) / 2.0
    demi_largeur = (bas[:, 2].max() - bas[:, 2].min()) / 2.0
    epaisseur = max(float(bas[:, 1].max() - bas[:, 1].min()), 0.035)
    r_col = min(max(r_col, 0.008), 0.03)
    r_arr = min(0.006, 0.4 * epaisseur, 0.4 * demi_largeur)
    return {
        "L_manche": T - epaisseur, "r_col": r_col, "r_bout": 0.8 * r_col,
        "r_bout_arr": 0.7 * r_col, "x_debut": -0.5 * epaisseur,
        "tete_x0": -epaisseur, "tete_x1": 0.0,
        # La panne depasse la tete de ~0.034 m : on retire cette saillie pour rester
        # dans l'enveloppe mesuree sur TripoSR.
        "tete_y": float(max(demi_hauteur - 0.03, 0.02)), "tete_z": float(demi_largeur),
        "rayon_arr": float(r_arr), "panne_y": float(max(demi_hauteur - 0.03, 0.02)),
    }


def _generer_forme_texturee_photo(parties, photo, dossier, nom):
    """Forme parametrique lisse + couleurs projetees depuis la photo (texture)."""
    import numpy as np
    import trimesh

    V = np.vstack([p[1] for p in parties])
    faces, off = [], 0
    for _, Vp, _, Fp in parties:
        faces.append(np.asarray(Fp) + off)
        off += len(Vp)
    # Meme orientation que TripoSR : manche sur Y, tete en bas (y minimum).
    # (x, y, z) -> (y, x, z) est une symetrie : on inverse le sens des faces.
    V = V[:, [1, 0, 2]]
    F = np.vstack(faces)[:, [0, 2, 1]]
    mesh = trimesh.Trimesh(vertices=V, faces=F, process=False)
    mesh.visual.vertex_colors = np.full((len(V), 4), 255, dtype=np.uint8)
    colorer_depuis_photo(mesh, photo)
    mesh_uv, image, uv, F2, V2 = texturer(mesh)
    exporter_texture(mesh_uv, image, uv, F2, V2, dossier, nom)
    print("Forme parametrique texturee avec les couleurs de la photo.", flush=True)



def _plume_param(base, direction, longueur, largeur):
    """Une plume : enveloppe convexe de 3 spheres le long de la direction."""
    import numpy as np
    d = np.asarray(direction, dtype=float)
    d = d / np.linalg.norm(d)
    base = np.asarray(base, dtype=float)
    return _hull_param([
        _sphere_param(base, largeur * 0.4),
        _sphere_param(base + d * longueur * 0.5, largeur),
        _sphere_param(base + d * longueur, largeur * 0.3),
    ])


def _ailes_param(x_attache, y_attache, z_centre, rayon, longueur=0.10, position=0.5):
    """Deux ailes en eventail, une de chaque cote du manche. Dimensions reelles (m)."""
    import numpy as np
    parties = []
    for signe in (1.0, -1.0):
        base = np.array([x_attache, y_attache, z_centre + signe * rayon * 0.6])
        for i in range(5):
            t = i / 4.0
            phi = 0.15 + 1.05 * t  # de la pointe basse vers le haut, en s'ecartant du manche
            direction = (-0.25, np.sin(phi), signe * np.cos(phi))
            longueur_plume = longueur * (0.55 + 0.45 * t)
            V, UV, F = _plume_param(base, direction, longueur_plume, 0.010)
            parties.append(("Acier", V, UV, F))
    return parties


def generer_parametrique(args, photo_pil=None):
    import numpy as np
    import trimesh

    debut = time.time()
    print("=== Generateur parametrique (sans IA) : marteau ===", flush=True)
    parties = [("Bois", *_manche_param()), ("Acier", *_tete_param()), ("Acier", *_panne_param())]

    # Mise a l'echelle : la plus grande dimension devient la taille reelle (en metres).
    tout = np.vstack([p[1] for p in parties])
    etendue = tout.max(0) - tout.min(0)
    echelle = (args.taille / 100.0) / float(etendue.max())
    bas = tout.min(0) * echelle
    decalage = np.array([-(tout[:, 0].min() + tout[:, 0].max()) / 2.0 * echelle,
                         -bas[1],
                         -(tout[:, 2].min() + tout[:, 2].max()) / 2.0 * echelle])
    parties = [(m, V * echelle + decalage, UV, F) for m, V, UV, F in parties]

    if getattr(args, "ailes", False):
        p = PARAM
        position = min(max(float(getattr(args, "position_ailes", 0.5)), 0.05), 0.95)
        x_pre = p["x_debut"] + position * (p["L_manche"] - p["x_debut"])
        u = np.clip(x_pre / p["L_manche"], 0.0, 1.0)
        r_pre = p["r_col"] + (p["r_bout"] - p["r_col"]) * u
        x_final = x_pre * echelle + decalage[0]
        parties += _ailes_param(x_final, decalage[1], decalage[2], r_pre * echelle, position=position)
        print("Ailes ajoutees : 2 x 10 cm, position %.2f du manche." % position, flush=True)

    dossier = os.path.join(SORTIES, args.nom)
    os.makedirs(dossier, exist_ok=True)
    if photo_pil is not None or (args.image and os.path.isfile(args.image)):
        try:
            if photo_pil is None:
                from PIL import Image
                photo_pil = Image.open(args.image).convert("RGB")
            _generer_forme_texturee_photo(parties, photo_pil, dossier, args.nom)
            if os.path.isdir(os.path.join(ROOT, "unity")):
                cible = os.path.join(UNITY_ITEMS, args.nom)
                os.makedirs(cible, exist_ok=True)
                for nom_fichier in (args.nom + ".obj", args.nom + ".mtl", args.nom + "_texture.png"):
                    shutil.copy2(os.path.join(dossier, nom_fichier), os.path.join(cible, nom_fichier))
                print("Copie pour Unity : Assets/Resources/Items/" + args.nom + "/", flush=True)
            print("Termine en %d secondes." % int(time.time() - debut), flush=True)
            return
        except Exception as erreur:
            print("Couleurs de la photo impossibles (" + str(erreur) + ") : textures bois et acier.", flush=True)
    img_bois = _texture_bois_param()
    img_acier = _texture_acier_param()
    img_bois.save(os.path.join(dossier, args.nom + "_bois.png"))
    img_acier.save(os.path.join(dossier, args.nom + "_acier.png"))

    lignes = ["mtllib " + args.nom + ".mtl", "o " + args.nom]
    off = 0
    for materiel, V, UV, F in parties:
        lignes.append("usemtl " + materiel)
        for p in V:
            lignes.append("v %.6f %.6f %.6f" % (p[0], p[1], p[2]))
        for t in UV:
            lignes.append("vt %.6f %.6f" % (t[0], t[1]))
        for f in F:
            a, b, c = [int(i) + off + 1 for i in f]
            lignes.append("f %d/%d %d/%d %d/%d" % (a, a, b, b, c, c))
        off += len(V)
    with open(os.path.join(dossier, args.nom + ".obj"), "w", encoding="utf-8") as f:
        f.write("\n".join(lignes) + "\n")
    with open(os.path.join(dossier, args.nom + ".mtl"), "w", encoding="utf-8") as f:
        f.write("newmtl Bois\nKa 1 1 1\nKd 1 1 1\nKs 0.05 0.05 0.05\nNs 10\nmap_Kd " + args.nom + "_bois.png\n\n")
        f.write("newmtl Acier\nKa 1 1 1\nKd 1 1 1\nKs 0.4 0.4 0.4\nNs 60\nmap_Kd " + args.nom + "_acier.png\n")

    morceaux = []
    for materiel, V, UV, F in parties:
        img = img_bois if materiel == "Bois" else img_acier
        vis = trimesh.visual.TextureVisuals(uv=UV, material=trimesh.visual.material.SimpleMaterial(image=img))
        morceaux.append(trimesh.Trimesh(vertices=V, faces=F, visual=vis, process=False))
    trimesh.Scene(morceaux).export(os.path.join(dossier, args.nom + ".glb"))

    print("Fichiers crees dans : " + dossier, flush=True)
    print("  %d sommets, %d faces" % (len(tout), sum(len(p[3]) for p in parties)), flush=True)
    if os.path.isdir(os.path.join(ROOT, "unity")):
        cible = os.path.join(UNITY_ITEMS, args.nom)
        os.makedirs(cible, exist_ok=True)
        for nom_fichier in (args.nom + ".obj", args.nom + ".mtl", args.nom + "_bois.png", args.nom + "_acier.png"):
            shutil.copy2(os.path.join(dossier, nom_fichier), os.path.join(cible, nom_fichier))
        print("Copie pour Unity : Assets/Resources/Items/" + args.nom + "/", flush=True)
    print("Termine en %d secondes." % int(time.time() - debut), flush=True)



def generer(args):
    if args.parametrique:
        return generer_parametrique(args)
    os.environ.update(env_ia())
    sys.path.insert(0, TRIPO)

    import numpy as np
    import torch
    import trimesh
    import rembg
    from PIL import Image
    from tsr.system import TSR
    from tsr.utils import remove_background, resize_foreground

    debut = time.time()
    device = "cuda:0" if torch.cuda.is_available() else "cpu"
    torch.set_num_threads(os.cpu_count() or 4)
    print("Calcul sur : " + device + " (" + str(torch.get_num_threads()) + " threads)", flush=True)

    print("Chargement du modele TripoSR (premiere fois : telechargement)...", flush=True)
    model = TSR.from_pretrained(
        "stabilityai/TripoSR", config_name="config.yaml", weight_name="model.ckpt",
    )
    model.renderer.set_chunk_size(8192)
    model.to(device)

    print("Preparation de la photo...", flush=True)
    image = recadrer_objet(Image.open(args.image))
    if args.retirer_fond:
        session = rembg.new_session()
        image = remove_background(image, session)
        image = resize_foreground(image, 0.85)
        arr = np.array(image).astype(np.float32) / 255.0
        arr = arr[:, :, :3] * arr[:, :, 3:4] + (1 - arr[:, :, 3:4]) * 0.5
        image = Image.fromarray((arr * 255.0).astype(np.uint8))
    else:
        image = image.convert("RGB")

    print("Creation du modele 3D (le processeur travaille, patientez)...", flush=True)
    with torch.no_grad():
        codes = model([image], device=device)
    meshes = model.extract_mesh(codes, True, resolution=args.mc)
    mesh = meshes[0]

    # Mise a l'echelle : la plus grande dimension devient la taille reelle donnee (en metres).
    extents = mesh.bounding_box.extents
    if extents.max() <= 0:
        raise RuntimeError("le modele genere est vide : essaie une autre photo")
    mesh.apply_scale((args.taille / 100.0) / float(extents.max()))
    # Centre sur X et Z, pose le bas sur y = 0.
    bornes = mesh.bounds
    mesh.apply_translation([
        -(bornes[0][0] + bornes[1][0]) / 2.0,
        -bornes[0][1],
        -(bornes[0][2] + bornes[1][2]) / 2.0,
    ])

    dossier = os.path.join(SORTIES, args.nom)
    os.makedirs(dossier, exist_ok=True)
    image.save(os.path.join(dossier, "photo_traitee.png"))

    # Etape 1 : forme TripoSR + couleurs de la photo (texture gardee sous le nom _brut).
    print("Etape 1/2 : forme TripoSR et couleurs de la photo...", flush=True)
    try:
        colorer_depuis_photo(mesh, image)
        mesh_uv, image_tex, uv, F2, V2 = texturer(mesh)
        exporter_texture(mesh_uv, image_tex, uv, F2, V2, dossier, args.nom + "_brut")
        print("  texture brute creee : " + args.nom + "_brut_texture.png", flush=True)
    except Exception as erreur:
        print("  texture brute impossible (" + str(erreur) + ")", flush=True)
    print("  %d sommets, %d faces (TripoSR)" % (len(mesh.vertices), len(mesh.faces)), flush=True)

    # Etape 2 : forme TripoSR conservee, surface lissee (Taubin : le volume et les
    # dimensions ne changent pas), puis couleurs de la photo et texture.
    print("Etape 2/2 : lissage de la forme TripoSR et texture...", flush=True)
    try:
        from trimesh.smoothing import filter_taubin
        lisse = mesh.copy()
        filter_taubin(lisse, lamb=0.5, nu=0.53, iterations=150)
        colorer_depuis_photo(lisse, image)
        lisser_couleurs(lisse, passes=4)
        mesh_uv, image_fin, uv, F2, V2 = texturer(lisse)
        exporter_texture(mesh_uv, image_fin, uv, F2, V2, dossier, args.nom)
        print("  forme lissee et texturee : " + args.nom + ".obj / .glb", flush=True)
        if os.path.isdir(os.path.join(ROOT, "unity")):
            cible = os.path.join(UNITY_ITEMS, args.nom)
            os.makedirs(cible, exist_ok=True)
            for nom_fichier in (args.nom + ".obj", args.nom + ".mtl", args.nom + "_texture.png"):
                shutil.copy2(os.path.join(dossier, nom_fichier), os.path.join(cible, nom_fichier))
            print("Copie pour Unity : Assets/Resources/Items/" + args.nom + "/", flush=True)
    except Exception as erreur:
        import traceback
        print("Lissage impossible (" + str(erreur) + ") : export TripoSR sans lissage.", flush=True)
        print(traceback.format_exc(), flush=True)
        mesh.export(os.path.join(dossier, args.nom + ".glb"))
        mesh.export(os.path.join(dossier, args.nom + ".obj"))
    print("Termine en %d secondes." % int(time.time() - debut), flush=True)


# ---------------------------------------------------------------------------
# Interface graphique
# ---------------------------------------------------------------------------

def analyser_demande(texte):
    """Lit la demande de l'utilisateur et renvoie (params, questions).

    params : objet, taille (cm), nom ; questions : liste de questions a poser.
    Une demande est prete quand questions est vide.
    """
    import re
    t = texte.lower()
    params = {"objet": None, "taille": None, "nom": "marteau_assistant",
              "ailes": False, "requete": "hammer"}
    questions = []

    if "marteau" in t:
        params["objet"] = "marteau"
    else:
        questions.append("Quel objet voulez-vous ? Pour l'instant je sais fabriquer un marteau.")

    trouve = re.search(r"(\d+(?:[.,]\d+)?)\s*(cm|mm|m)\b", t)
    if trouve:
        valeur = float(trouve.group(1).replace(",", "."))
        unite = trouve.group(2)
        cm = valeur * {"cm": 1.0, "mm": 0.1, "m": 100.0}[unite]
        if 5 <= cm <= 100:
            params["taille"] = round(cm, 1)
        else:
            questions.append("La taille doit etre entre 5 et 100 cm. Quelle taille reelle voulez-vous ?")
    else:
        questions.append("Quelle taille reelle voulez-vous (la plus grande dimension, en cm) ?")

    if re.search(r"\bailes?\b", t) and not re.search(r"\bsans\s+ailes\b", t):
        params["ailes"] = True
        params["nom"] = "marteau_ailes"
        params["requete"] = "hammer angel wings" if "ange" in t else "hammer wings"

    return params, questions


COMMONS_API = "https://commons.wikimedia.org/w/api.php"
AGENT_UA = "LibreVies-IA-3D/1.0 (outil local du jeu LibreVies)"


def _http_json(url):
    import json
    import urllib.request
    req = urllib.request.Request(url, headers={"User-Agent": AGENT_UA})
    with urllib.request.urlopen(req, timeout=20) as reponse:
        return json.loads(reponse.read().decode("utf-8"))


def _sans_html(texte):
    import re
    return re.sub(r"<[^>]+>", "", texte or "").strip()


def resultats_commons(donnees, n=5, mots_cles=("hammer", "mallet", "marteau")):
    """Garde les images libres (CC ou domaine public), dans l'ordre de pertinence."""
    pages = (donnees.get("query") or {}).get("pages") or {}
    trouves = []
    for page in pages.values():
        infos = page.get("imageinfo") or []
        if not infos:
            continue
        info = infos[0]
        if info.get("mime") not in ("image/jpeg", "image/png"):
            continue
        meta = info.get("extmetadata") or {}
        licence = _sans_html((meta.get("LicenseShortName") or {}).get("value", ""))
        if not any(m in licence for m in ("CC", "Public domain", "PD")):
            continue
        titre = page.get("title", "").replace("File:", "")
        if mots_cles and not any(mot in titre.lower() for mot in mots_cles):
            continue
        url = info.get("thumburl") or info.get("url")
        if not url:
            continue
        trouves.append((page.get("index", 9999), {
            "titre": titre,
            "url": url,
            "licence": licence,
            "auteur": _sans_html((meta.get("Artist") or {}).get("value", "")),
            "page": info.get("descriptionurl", ""),
        }))
    trouves.sort(key=lambda couple: couple[0])
    return [r for _, r in trouves[:n]]


def chercher_images_libres(requete, n=5, mots_cles=("hammer", "mallet", "marteau")):
    import urllib.parse
    url = COMMONS_API + "?" + urllib.parse.urlencode({
        "action": "query", "format": "json", "generator": "search",
        "gsrsearch": requete, "gsrnamespace": 6, "gsrlimit": 30,
        "prop": "imageinfo", "iiprop": "url|mime|extmetadata", "iiurlwidth": 800})
    return resultats_commons(_http_json(url), n, mots_cles)


def telecharger_image(url, chemin):
    import urllib.request
    req = urllib.request.Request(url, headers={"User-Agent": AGENT_UA})
    with urllib.request.urlopen(req, timeout=60) as reponse:
        donnees = reponse.read()
    with open(chemin, "wb") as f:
        f.write(donnees)


APERCU_SCRIPT = (
    "import sys\n"
    "from PIL import Image\n"
    "im = Image.open(sys.argv[1]).convert('RGBA')\n"
    "im.thumbnail((200, 200))\n"
    "im.save(sys.argv[2])\n"
)


def preparer_apercus(res):
    """Telecharge une miniature PNG par resultat. Retourne le chemin, ou None si echec."""
    dossier = os.path.join(IA, "images", "apercus")
    os.makedirs(dossier, exist_ok=True)
    chemins = []
    for i, r in enumerate(res, 1):
        brut = os.path.join(dossier, "apercu_%d.bin" % i)
        png = os.path.join(dossier, "apercu_%d.png" % i)
        try:
            telecharger_image(r["url"], brut)
            code = subprocess.call([venv_python(), "-c", APERCU_SCRIPT, brut, png],
                                   env=env_ia(), stdout=subprocess.DEVNULL,
                                   stderr=subprocess.DEVNULL)
            chemins.append(png if code == 0 and os.path.isfile(png) else None)
        except Exception:
            chemins.append(None)
    return chemins


def ecrire_credits(nom, choisi):
    dossier = os.path.join(SORTIES, nom)
    os.makedirs(dossier, exist_ok=True)
    with open(os.path.join(dossier, "source.txt"), "w", encoding="utf-8") as f:
        f.write("Image source : %s\nAuteur : %s\nLicence : %s\nPage : %s\n" % (
            choisi["titre"], choisi["auteur"], choisi["licence"], choisi["page"]))


DEFAUT_TAILLE_PIECE = 30  # cm, taille de chaque piece (modifiable plus tard)

TRADUCTIONS = {
    "marteau": "hammer", "marteaux": "hammer", "armure": "armor", "ours": "bear",
    "baskets": "sneakers", "basket": "sneakers", "souris": "mouse",
    "table": "table", "chaise": "chair", "ailes": "wings", "ange": "angel", "chapeau": "hat", "chapeaux": "hat", "chien": "dog", "chat": "cat", "voiture": "car", "arbre": "tree", "maison": "house", "poisson": "fish", "pomme": "apple", "lunettes": "glasses", "fleur": "flower",
    "cheval": "horse", "bouteille": "bottle", "casque": "helmet", "epee": "sword",
    "bouclier": "shield", "lampe": "lamp", "vase": "vase", "livre": "book", "tasse": "mug",
}

PIECE_SEPARATEURS = r"(\s+(?:avec|et|posée sur|posé sur|pose sur|sur|dans|à côté de|a cote de)\s+|,)"
DETERMINANTS = r"^(d'une\s*|d'un\s*|l'|un\s+|une\s+|des\s+|de\s+|du\s+|le\s+|la\s+|les\s+|mon\s+|ma\s+|mes\s+|son\s+|sa\s+|ses\s+)"


def decouper_demande(texte):
    """Repere les pieces d'une demande : [{nom, relation}] ; relation = 'sur' ou 'cote'."""
    import re
    t = texte.lower().strip()
    # Formules de politesse et verbes : ne font pas partie des pieces
    t = re.sub(r"\b(fait|fais|faire|donne|donner|cree|crée|creer|créer|veux|voudrais|je|moi|nous|svp|stp)\b", " ", t)
    # « sur la tete », « sur le dos »... : le petit objet est pose dessus
    t = re.sub(r"\bsur\s+(la\s+|le\s+|sa\s+|son\s+|ses\s+|les\s+)?(tete|tête|dos|epaule|épaule|main|mains)\b", " dessus ", t)
    morceaux = re.split(PIECE_SEPARATEURS, re.sub(r"\s+", " ", t))
    pieces, relation = [], "cote"
    inverser = False
    for k, morceau in enumerate(morceaux):
        if k % 2 == 1:
            relation = "sur" if "sur" in morceau else "cote"
            inverser = relation == "sur"
            continue
        nom = morceau.strip()
        for _ in range(2):
            nom = re.sub(DETERMINANTS, "", nom)
        nom = re.sub(r"\s+de\s+\d.*$|\s*\d.*$", "", nom).strip()
        if nom.endswith(" dessus"):
            nom, relation, inverser = nom[:-len(" dessus")].strip(), "sur", False
        if len(nom) < 3:
            relation = "cote"
            continue
        if inverser and relation == "sur" and pieces:
            # "A sur B" : B est le support, il doit etre pose en premier
            a_poser = pieces.pop()
            pieces.append({"nom": nom, "relation": "cote"})
            pieces.append({"nom": a_poser["nom"], "relation": "sur"})
        else:
            pieces.append({"nom": nom, "relation": relation})
        relation, inverser = "cote", False
    return pieces


def requete_en(nom):
    """Requete Wikimedia Commons pour une piece, et mots a retrouver dans les titres.

    Un mot sans traduction garde son nom francais (la recherche le tente quand meme)."""
    mots = nom.split()
    if not mots:
        return None, ()
    traduits = [TRADUCTIONS.get(m, m) for m in mots]
    return " ".join(traduits), tuple(set(" ".join(traduits + mots).split()))


def executer_worker(cmd, log):
    proc = subprocess.Popen(cmd, env=env_ia(), stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, text=True,
                            encoding="utf-8", errors="replace")
    for ligne in proc.stdout:
        log(ligne.rstrip())
    return proc.wait()


def assembler(args):
    """Pose les pieces (fichiers .glb) les unes par rapport aux autres et exporte une scene."""
    import json
    import numpy as np
    import trimesh
    entrees = json.loads(args.entrees)
    dossier = os.path.join(SORTIES, args.nom)
    os.makedirs(dossier, exist_ok=True)
    marge = 0.02  # 2 cm entre deux pieces posees cote a cote
    combine = trimesh.Scene()
    precedent = None  # (min, max) de la piece precedente, en metres
    for k, e in enumerate(entrees):
        sc = trimesh.load(e["fichier"], force="scene")
        bmin, bmax = sc.bounds
        echelle = (float(e["taille"]) / 100.0) / float(max((bmax - bmin).max(), 1e-9))
        smin, smax = bmin * echelle, bmax * echelle
        bas_centre = np.array([(smin[0] + smax[0]) / 2.0, smin[1], (smin[2] + smax[2]) / 2.0])
        if precedent is None:
            cible = np.array([0.0, 0.0, 0.0])
        else:
            pmin, pmax = precedent
            milieu_z = (pmin[2] + pmax[2]) / 2.0
            if e["relation"] == "sur":
                cible = np.array([(pmin[0] + pmax[0]) / 2.0, pmax[1], milieu_z])
            else:
                cible = np.array([pmax[0] + marge + (smax[0] - smin[0]) / 2.0, 0.0, milieu_z])
        M = np.eye(4)
        M[:3, :3] *= echelle
        M[:3, 3] = cible - bas_centre
        pmin_new, pmax_new = None, None
        for node in sc.graph.nodes_geometry:
            T, nom_geom = sc.graph[node]
            g = sc.geometry[nom_geom].copy()
            g.apply_transform(M @ T)
            combine.add_geometry(g, node_name="p%d_%s" % (k, node))
            gmin, gmax = g.bounds
            pmin_new = gmin if pmin_new is None else np.minimum(pmin_new, gmin)
            pmax_new = gmax if pmax_new is None else np.maximum(pmax_new, gmax)
        precedent = (pmin_new, pmax_new)
        print("Piece %d posee (%s)." % (k + 1, e["relation"]), flush=True)
    chemin = os.path.join(dossier, args.nom + ".glb")
    combine.export(chemin)
    etendue = (combine.bounds[1] - combine.bounds[0]) * 100
    print("Scene assemblee : " + chemin, flush=True)
    print("Dimensions : %.1f x %.1f x %.1f cm" % tuple(etendue), flush=True)


def interface():
    import tkinter as tk
    from tkinter import filedialog, messagebox, ttk

    fenetre = tk.Tk()
    fenetre.title("LibreVies - Photo vers modele 3D")
    fenetre.geometry("760x560")

    file_journal = queue.Queue()

    def log(msg):
        file_journal.put(str(msg))

    etat = {"pret": False, "photo": tk.StringVar(value=""), "nom": tk.StringVar(value="objet"),
            "taille": tk.StringVar(value="30"), "mc": tk.StringVar(value="256"),
            "fond": tk.BooleanVar(value=True)}

    cadre = ttk.Frame(fenetre, padding=10)
    cadre.pack(fill="both", expand=True)

    ttk.Label(cadre, text="Photo de l'objet :").grid(row=0, column=0, sticky="w")
    ttk.Entry(cadre, textvariable=etat["photo"], width=60).grid(row=0, column=1, sticky="we", padx=5)
    bouton_photo = ttk.Button(cadre, text="Choisir...",
                              command=lambda: choisir_photo())
    bouton_photo.grid(row=0, column=2)

    ttk.Label(cadre, text="Nom du fichier (sans espace) :").grid(row=1, column=0, sticky="w", pady=4)
    ttk.Entry(cadre, textvariable=etat["nom"], width=20).grid(row=1, column=1, sticky="w", padx=5)

    ttk.Label(cadre, text="Taille reelle la plus grande dimension (cm) :").grid(row=2, column=0, sticky="w")
    ttk.Entry(cadre, textvariable=etat["taille"], width=10).grid(row=2, column=1, sticky="w", padx=5)

    ttk.Label(cadre, text="Precision du maillage :").grid(row=3, column=0, sticky="w", pady=4)
    ttk.Combobox(cadre, textvariable=etat["mc"], values=["128", "192", "256"], width=8,
                 state="readonly").grid(row=3, column=1, sticky="w", padx=5)

    ttk.Checkbutton(cadre, text="Retirer le fond de la photo (recommande)",
                    variable=etat["fond"]).grid(row=4, column=1, sticky="w", padx=5)

    bouton_generer = ttk.Button(cadre, text="Generer le modele 3D", state="disabled",
                                command=lambda: generer_modele())
    bouton_generer.grid(row=5, column=1, sticky="w", pady=8, padx=5)

    def ouvrir_assistant():
        fenetre_chat = tk.Toplevel(fenetre)
        fenetre_chat.title("Assistant LibreVies")
        fenetre_chat.geometry("600x460")
        historique = tk.Text(fenetre_chat, wrap="word", height=20, state="disabled")
        historique.pack(fill="both", expand=True, padx=8, pady=8)
        saisie = tk.StringVar(value="")
        entree = ttk.Entry(fenetre_chat, textvariable=saisie, width=70)
        entree.pack(side="left", fill="x", expand=True, padx=(8, 4), pady=(0, 8))
        contexte = {"texte": "", "choix": None, "params": None, "attente": None, "pieces": None, "i": 0}
        file_chat = queue.Queue()

        def afficher(msg):
            historique.config(state="normal")
            historique.insert("end", msg + "\n")
            historique.see("end")
            historique.config(state="disabled")

        def lancer_recherche(params):
            requete = params["requete"]

            def travail():
                try:
                    res = chercher_images_libres(requete)
                    if not res and "angel" in requete:
                        res = chercher_images_libres("hammer wings")
                    if res:
                        file_chat.put(("choix", params, res, preparer_apercus(res)))
                    else:
                        file_chat.put(("msg", "Aucune image libre de marteau a ailes trouvee sur "
                                              "Wikimedia Commons. Repondez « sans ailes » pour un "
                                              "marteau normal, ou utilisez votre propre photo "
                                              "(bouton Choisir dans la fenetre principale)."))
                except Exception as exc:
                    file_chat.put(("msg", "Recherche impossible (connexion internet ?) : " + str(exc)))

            threading.Thread(target=travail, daemon=True).start()

        def lancer_depuis_image(choisi, params):
            nom = params["nom"]

            def travail():
                try:
                    os.makedirs(os.path.join(IA, "images"), exist_ok=True)
                    ext = ".png" if choisi["url"].lower().endswith(".png") else ".jpg"
                    chemin = os.path.join(IA, "images", nom + ext)
                    log("=== Telechargement : " + choisi["titre"] + " ===")
                    telecharger_image(choisi["url"], chemin)
                    cmd = [venv_python(), os.path.abspath(__file__), "--worker",
                           "--image", chemin, "--nom", nom,
                           "--taille", str(params["taille"]), "--mc", "256"]
                    log("=== Generation de '" + nom + "' depuis l'image ===")
                    proc = subprocess.Popen(cmd, env=env_ia(), stdout=subprocess.PIPE,
                                            stderr=subprocess.STDOUT, text=True,
                                            encoding="utf-8", errors="replace")
                    for ligne in proc.stdout:
                        log(ligne.rstrip())
                    code = proc.wait()
                    if code == 0:
                        ecrire_credits(nom, choisi)
                        log("Source et licence enregistrees dans sorties\\" + nom + "\\source.txt")
                    log("=== Fini ===" if code == 0 else "=== ECHEC (code %d) : voir le journal ===" % code)
                except Exception as exc:
                    log("ERREUR : " + str(exc))

            threading.Thread(target=travail, daemon=True).start()

        def choisir(numero):
            choisi = contexte["choix"][numero - 1]
            params = contexte["params"]
            contexte["choix"] = None
            fen = contexte.get("fenetre_apercu")
            if fen is not None:
                try:
                    fen.destroy()
                except tk.TclError:
                    pass
            afficher("Assistant : je telecharge l'image n° %d puis je la modelise." % numero)
            lancer_depuis_image(choisi, params)

        def ouvrir_apercus(res, chemins, action=None):
            action = action or choisir
            fen = tk.Toplevel(fenetre_chat)
            fen.title("Images libres proposees")
            contexte["fenetre_apercu"] = fen
            contexte["photos"] = []
            cadre_ap = ttk.Frame(fen, padding=8)
            cadre_ap.pack(fill="both", expand=True)
            for i, (r, chemin) in enumerate(zip(res, chemins), 1):
                col = ttk.Frame(cadre_ap, padding=4)
                col.grid(row=0, column=i - 1, padx=4, sticky="n")
                if chemin:
                    photo = tk.PhotoImage(file=chemin)
                    contexte["photos"].append(photo)
                    tk.Label(col, image=photo).pack()
                else:
                    tk.Label(col, text="(apercu indisponible)", width=24, height=8).pack()
                tk.Label(col, text="%d. %s" % (i, r["titre"][:40]), wraplength=190).pack()
                tk.Label(col, text=r["licence"], fg="gray", wraplength=190).pack()
                ttk.Button(col, text="Modeliser %d" % i,
                           command=lambda n=i: action(n)).pack(pady=4)

        def fermer_apercu():
            fen = contexte.get("fenetre_apercu")
            if fen is not None:
                try:
                    fen.destroy()
                except tk.TclError:
                    pass

        def demander_piece():
            piece = contexte["pieces"][contexte["i"]]
            contexte["attente"] = "source_piece"
            options = "1 = ma photo (je l'ouvre)"
            options += ", 2 = recherche web (images libres)"
            afficher("Assistant : pour « %s » : %s" % (piece["nom"], options))

        def lancer_pieces(pieces):
            for piece in pieces:
                piece["requete"], piece["mots"] = requete_en(piece["nom"])
                piece["fichier"] = None
                piece["url"] = None
            contexte["pieces"] = pieces
            contexte["i"] = 0
            afficher("Assistant : je vois %d piece(s) : %s. Je demande la source de chacune."
                     % (len(pieces), ", ".join(p["nom"] for p in pieces)))
            demander_piece()

        def piece_suivante():
            contexte["i"] += 1
            if contexte["i"] < len(contexte["pieces"]):
                demander_piece()
            else:
                fabriquer_pieces(contexte["pieces"])
                contexte["pieces"] = None
                contexte["attente"] = None

        def lancer_recherche_piece(requete, mots):

            def travail():
                try:
                    res = chercher_images_libres(requete, 5, mots)
                    if res:
                        file_chat.put(("choix_piece", res, preparer_apercus(res)))
                    else:
                        file_chat.put(("msg", "Aucune image libre trouvee pour « %s »." % requete))
                        file_chat.put(("reattendre", None, None))
                except Exception as exc:
                    file_chat.put(("msg", "Recherche impossible (connexion internet ?) : " + str(exc)))
                    file_chat.put(("reattendre", None, None))

            threading.Thread(target=travail, daemon=True).start()

        def choisir_piece(numero):
            piece = contexte["pieces"][contexte["i"]]
            choisi = contexte["choix"][numero - 1]
            contexte["choix"] = None
            fermer_apercu()
            piece.update({"url": choisi["url"], "titre": choisi["titre"],
                          "licence": choisi["licence"], "auteur": choisi["auteur"],
                          "page": choisi["page"]})
            afficher("Assistant : image %d retenue pour %s." % (numero, piece["nom"]))
            piece_suivante()

        def traiter_piece(message):
            piece = contexte["pieces"][contexte["i"]]
            attente = contexte.get("attente")
            if attente == "source_piece":
                if message == "1":
                    chemin = filedialog.askopenfilename(
                        parent=fenetre_chat, title="Photo de " + piece["nom"],
                        filetypes=[("Images", "*.png *.jpg *.jpeg *.webp *.bmp"), ("Tous", "*.*")])
                    if not chemin:
                        afficher("Assistant : pas de photo choisie. Tapez 1 pour choisir, ou 2 pour le web.")
                        return
                    piece["fichier"] = chemin
                    afficher("Assistant : photo de %s enregistree." % piece["nom"])
                    piece_suivante()
                    return
                if message == "2":
                    contexte["attente"] = "attente_recherche"
                    afficher("Assistant : je cherche « %s » sur Wikimedia Commons..." % piece["requete"])
                    lancer_recherche_piece(piece["requete"], piece["mots"])
                    return
                afficher("Assistant : tapez 1 (ma photo) ou 2 (recherche web).")
                return
            if attente == "choix_piece" and message.isdigit() and 1 <= int(message) <= len(contexte["choix"]):
                choisir_piece(int(message))
                return
            afficher("Assistant : tapez le numero demande.")

        def fabriquer_pieces(pieces):
            import json
            base = "scene_%d" % int(time.time())
            afficher("Assistant : je fabrique les %d piece(s) puis je les assemble (suivez le journal)."
                     % len(pieces))

            def travail():
                try:
                    os.makedirs(os.path.join(IA, "images"), exist_ok=True)
                    entrees = []
                    for k, piece in enumerate(pieces, 1):
                        nom = "%s_%d" % (base, k)
                        if piece.get("fichier"):
                            ext = os.path.splitext(piece["fichier"])[1].lower() or ".jpg"
                            chemin = os.path.join(IA, "images", nom + ext)
                            shutil.copy2(piece["fichier"], chemin)
                        else:
                            ext = ".png" if piece["url"].lower().endswith(".png") else ".jpg"
                            chemin = os.path.join(IA, "images", nom + ext)
                            log("=== Telechargement : " + piece["titre"] + " ===")
                            telecharger_image(piece["url"], chemin)
                        log("=== Piece %d/%d : %s ===" % (k, len(pieces), piece["nom"]))
                        cmd = [venv_python(), os.path.abspath(__file__), "--worker",
                               "--image", chemin, "--nom", nom,
                               "--taille", str(DEFAUT_TAILLE_PIECE), "--mc", "256"]
                        if executer_worker(cmd, log) != 0:
                            log("=== ECHEC sur la piece %s : voir le journal ===" % piece["nom"])
                            return
                        if piece.get("url"):
                            ecrire_credits(nom, piece)
                        entrees.append({"fichier": os.path.join(SORTIES, nom, nom + ".glb"),
                                        "taille": DEFAUT_TAILLE_PIECE,
                                        "relation": piece["relation"]})
                    log("=== Assemblage de '" + base + "' ===")
                    cmd = [venv_python(), os.path.abspath(__file__), "--worker", "--assembler",
                           "--nom", base, "--taille", "0", "--entrees", json.dumps(entrees)]
                    code = executer_worker(cmd, log)
                    log("=== Fini : sorties\\" + base + "\\" + base + ".glb ===" if code == 0
                        else "=== ECHEC assemblage : voir le journal ===")
                except Exception as exc:
                    log("ERREUR : " + str(exc))

            threading.Thread(target=travail, daemon=True).start()

        def envoyer(event=None):
            message = saisie.get().strip()
            if not message:
                return
            saisie.set("")
            afficher("Vous : " + message)
            if contexte.get("pieces"):
                traiter_piece(message)
                return
            if contexte["choix"] and message.isdigit() and 1 <= int(message) <= len(contexte["choix"]):
                choisir(int(message))
                return
            if contexte.get("attente") == "source" and message in ("1", "2"):
                params = contexte["params"]
                if message == "1":
                    contexte["attente"] = "place"
                    afficher("Assistant : ou placer les ailes sur le manche ?")
                    afficher("  1 = milieu du manche   2 = pres du pommeau   3 = pres de la tete")
                else:
                    contexte["attente"] = None
                    afficher("Assistant : je cherche des images libres de droits (Wikimedia Commons)...")
                    lancer_recherche(params)
                return
            if contexte.get("attente") == "place" and message in ("1", "2", "3"):
                position = {"1": 0.5, "2": 0.85, "3": 0.15}[message]
                params = contexte["params"]
                contexte["attente"] = None
                contexte["params"] = None
                afficher("Assistant : je dessine le marteau avec ses ailes (%s cm)." % params["taille"])
                lancer_parametrique(params["nom"], params["taille"], position)
                return
            if contexte.get("attente") in ("source", "place"):
                contexte["attente"] = None
                contexte["params"] = None
            if not contexte.get("attente") and "marteau" not in message.lower() \
                    and "aile" not in message.lower():
                pieces = decouper_demande(message)
                if pieces:
                    lancer_pieces(pieces)
                    return
            contexte["choix"] = None
            contexte["texte"] += " " + message
            params, questions = analyser_demande(contexte["texte"])
            if questions:
                for q in questions:
                    afficher("Assistant : " + q)
                return
            if params["ailes"]:
                contexte["texte"] = ""
                contexte["params"] = params
                contexte["attente"] = "source"
                afficher("Assistant : pour les ailes, que voulez-vous ?")
                afficher("  1 = dessinees par le generateur (10 cm chaque aile, fiable)")
                afficher("  2 = chercher une image libre sur le web (resultats incertains)")
                return
            afficher("Assistant : je lance la creation d'un marteau de %s cm." % params["taille"])
            lancer_parametrique(params["nom"], params["taille"])
            contexte["texte"] = ""

        def verifier_chat():
            try:
                while True:
                    item = file_chat.get_nowait()
                    if item[0] == "msg":
                        afficher("Assistant : " + item[1])
                    elif item[0] == "reattendre":
                        contexte["attente"] = "source_piece"
                    elif item[0] == "choix_piece":
                        _, res, chemins = item
                        contexte["choix"] = res
                        contexte["attente"] = "choix_piece"
                        nom_piece = contexte["pieces"][contexte["i"]]["nom"]
                        afficher("Assistant : images libres pour « %s » :" % nom_piece)
                        for i, r in enumerate(res, 1):
                            afficher("  %d. %s  [%s]" % (i, r["titre"], r["licence"]))
                        try:
                            ouvrir_apercus(res, chemins, choisir_piece)
                        except Exception as exc:
                            afficher("Assistant : fenetre des images impossible : " + str(exc))
                    elif item[0] == "choix":
                        _, params, res, chemins = item
                        contexte["choix"] = res
                        contexte["params"] = params
                        afficher("Assistant : voici des images libres. Cliquez sur « Modeliser n » "
                                 "dans la fenetre des images, ou tapez le numero ici :")
                        for i, r in enumerate(res, 1):
                            afficher("  %d. %s  [%s]" % (i, r["titre"], r["licence"]))
                        try:
                            ouvrir_apercus(res, chemins)
                        except Exception as exc:
                            afficher("Assistant : fenetre des images impossible : " + str(exc))
            except queue.Empty:
                pass
            try:
                fenetre_chat.after(200, verifier_chat)
            except tk.TclError:
                pass

        ttk.Button(fenetre_chat, text="Envoyer", command=envoyer).pack(side="right", padx=(0, 8), pady=(0, 8))
        entree.bind("<Return>", envoyer)
        afficher("Assistant : decrivez l'objet (pour l'instant : un marteau, avec ou sans ailes).")
        verifier_chat()

    def lancer_parametrique(nom, taille, position_ailes=None):
        cmd = [venv_python(), os.path.abspath(__file__), "--worker", "--parametrique",
               "--nom", nom, "--taille", str(taille), "--mc", "256"]
        if position_ailes is not None:
            cmd += ["--ailes", "--position-ailes", str(position_ailes)]

        def travail():
            log("=== Generation parametrique de '" + nom + "' ===")
            proc = subprocess.Popen(cmd, env=env_ia(), stdout=subprocess.PIPE,
                                    stderr=subprocess.STDOUT, text=True,
                                    encoding="utf-8", errors="replace")
            for ligne in proc.stdout:
                log(ligne.rstrip())
            code = proc.wait()
            log("=== Fini ===" if code == 0 else "=== ECHEC (code %d) : voir le journal ===" % code)

        threading.Thread(target=travail, daemon=True).start()

    ttk.Button(cadre, text="Assistant (discussion)...", command=ouvrir_assistant).grid(
        row=5, column=0, sticky="w", pady=8)

    ttk.Button(cadre, text="Ouvrir le dossier des sorties",
               command=lambda: ouvrir_sorties()).grid(row=5, column=2, sticky="e")

    journal = tk.Text(cadre, height=20, wrap="word")
    journal.grid(row=6, column=0, columnspan=3, sticky="nsew", pady=(8, 0))
    cadre.columnconfigure(1, weight=1)
    cadre.rowconfigure(6, weight=1)

    def ecrire(texte):
        journal.insert("end", texte + "\n")
        journal.see("end")

    def choisir_photo():
        chemin = filedialog.askopenfilename(
            title="Choisir la photo",
            filetypes=[("Images", "*.png *.jpg *.jpeg *.webp *.bmp"), ("Tous", "*.*")],
        )
        if chemin:
            etat["photo"].set(chemin)

    def ouvrir_sorties():
        os.makedirs(SORTIES, exist_ok=True)
        if os.name == "nt":
            os.startfile(SORTIES)
        else:
            subprocess.call(["xdg-open", SORTIES])

    def generer_modele():
        photo = etat["photo"].get().strip().strip('"')
        nom = etat["nom"].get().strip()
        try:
            taille = float(etat["taille"].get().replace(",", "."))
        except ValueError:
            messagebox.showerror("Taille", "La taille doit etre un nombre (en cm).")
            return
        if not os.path.isfile(photo):
            messagebox.showerror("Photo", "Choisis une photo existante.")
            return
        if not nom or any(c in nom for c in '\\/:*?"<>| '):
            messagebox.showerror("Nom", "Nom sans espace ni caractere speciaux (ex. marteau).")
            return
        if taille <= 0:
            messagebox.showerror("Taille", "La taille doit etre positive.")
            return
        bouton_generer.config(state="disabled")
        cmd = [venv_python(), os.path.abspath(__file__), "--worker",
               "--image", photo, "--nom", nom, "--taille", str(taille),
               "--mc", etat["mc"].get()]
        if not etat["fond"].get():
            cmd.append("--garder-fond")

        def travail():
            log("=== Generation de '" + nom + "' ===")
            proc = subprocess.Popen(cmd, env=env_ia(), stdout=subprocess.PIPE,
                                    stderr=subprocess.STDOUT, text=True,
                                    encoding="utf-8", errors="replace")
            for ligne in proc.stdout:
                log(ligne.rstrip())
            code = proc.wait()
            log("=== Fini ===" if code == 0 else "=== ECHEC (code %d) : voir le journal ===" % code)
            file_journal.put(("FIN", code))

        threading.Thread(target=travail, daemon=True).start()

    def installation():
        try:
            installer(log)
            file_journal.put(("PRET", None))
        except Exception as exc:  # affiche l'erreur dans la fenetre
            log("ERREUR : " + str(exc))
            log("Corrige le probleme (connexion, espace disque) puis relance ce fichier.")
            file_journal.put(("ECHEC_INSTALL", None))

    def poll():
        try:
            while True:
                item = file_journal.get_nowait()
                if isinstance(item, tuple):
                    if item[0] == "PRET":
                        etat["pret"] = True
                        bouton_generer.config(state="normal")
                        ecrire("Pret : choisis une photo puis clique sur Generer.")
                    elif item[0] == "FIN":
                        bouton_generer.config(state="normal" if etat["pret"] else "disabled")
                    elif item[0] == "ECHEC_INSTALL":
                        bouton_generer.config(state="disabled")
                else:
                    ecrire(item)
        except queue.Empty:
            pass
        fenetre.after(150, poll)

    ecrire("Version : assistant multi-pieces (armure, ours, baskets...) + ailes.")
    threading.Thread(target=installation, daemon=True).start()
    fenetre.after(150, poll)
    fenetre.mainloop()


def main():
    if "--worker" in sys.argv:
        parseur = argparse.ArgumentParser()
        parseur.add_argument("--worker", action="store_true")
        parseur.add_argument("--image", default="")
        parseur.add_argument("--parametrique", action="store_true")
        parseur.add_argument("--ailes", action="store_true")
        parseur.add_argument("--assembler", action="store_true")
        parseur.add_argument("--entrees", default="[]")
        parseur.add_argument("--position-ailes", type=float, default=0.5)
        parseur.add_argument("--nom", required=True)
        parseur.add_argument("--taille", type=float, required=True)
        parseur.add_argument("--mc", type=int, default=256)
        parseur.add_argument("--garder-fond", action="store_true")
        args = parseur.parse_args()
        args.retirer_fond = not args.garder_fond
        if args.assembler:
            assembler(args)
            return
        generer(args)
        return
    interface()


if __name__ == "__main__":
    main()
