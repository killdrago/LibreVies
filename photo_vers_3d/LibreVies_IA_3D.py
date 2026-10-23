#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
LibreVies - Photo vers modele 3D (TripoSR, processeur, sans carte NVIDIA).

UN SEUL FICHIER. Double-clic (ou python LibreVies_IA_3D.py) :
  1. cree le dossier photo_vers_3d\\IA\\ (tout ce qui est IA est la dedans) ;
  2. cree un environnement Python prive (IA\\venv) ;
  3. installe automatiquement les paquets manquants (PyTorch, TripoSR, rembg...) ;
  4. telecharge le code officiel TripoSR (IA\\TripoSR) ;
  5. ouvre la fenetre : tu choisis une photo, tu indiques la taille reelle,
     et le modele 3D est cree dans IA\\sorties\\<nom>\\ (.glb et .obj).

Le premier lancement telecharge beaucoup de choses (plusieurs Go) : c'est normal.
Les lancements suivants ne retelechargent rien.

Le fichier garde les sorties dans photo_vers_3d\\IA\\. Le dossier IA est ignore par Git.
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
    "safetensors",
    "psutil",
]

# Generation d'image locale (processeur, sans carte NVIDIA) : SDXL-Turbo, telecharge au premier usage.
MODELE_IMAGE = "stabilityai/sdxl-turbo"
# diffusers est installe sans ses dependances (sinon pip ne resout pas avec transformers==4.35.0).
PAQUETS_DIFFUSERS = ["huggingface-hub==0.36.2", "diffusers==0.35.2"]

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


def paquets_presents(log=None):
    """Vrai si tous les modules importants sont deja installes dans l'environnement IA.
    En cas d'echec, la fin de l'erreur d'import est envoyee au journal."""
    test = "import torch, torchvision, trimesh, rembg, skimage, transformers, einops, omegaconf, PIL, xatlas, diffusers, psutil; assert diffusers.__version__ == \"0.35.2\""
    proc = subprocess.run([venv_python(), "-c", test], env=env_ia(),
                          capture_output=True, text=True, encoding="utf-8", errors="replace")
    if proc.returncode != 0 and log:
        log("Import impossible, erreur :")
        for ligne in [l for l in (proc.stderr or "").splitlines() if l.strip()][-12:]:
            log("  " + ligne)
    return proc.returncode == 0


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

    if paquets_presents(log):
        log("Paquets Python deja installes.")
    else:
        log("Installation des paquets (long la premiere fois, plusieurs Go)...")
        if lancer([venv_python(), "-m", "pip", "install", "--upgrade", "--disable-pip-version-check"]
                  + PAQUETS, log) != 0:
            raise RuntimeError("echec de l'installation des paquets Python")
        log("Installation de diffusers (generation d'image, sans ses dependances)...")
        if lancer([venv_python(), "-m", "pip", "install", "--disable-pip-version-check", "--no-deps"]
                  + PAQUETS_DIFFUSERS, log) != 0:
            raise RuntimeError("echec de l'installation de diffusers")
        if not paquets_presents(log):
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

def _propager_couleurs(mesh, c, ok):
    """Chaque sommet non visible prend la moyenne de ses voisins deja colores (vectorise, rapide)."""
    import numpy as np
    E = np.asarray(mesh.edges_unique)
    a, b = E[:, 0], E[:, 1]
    c = c.copy()
    ok = ok.copy()
    for _ in range(400):
        if ok.all():
            break
        somme = np.zeros_like(c)
        n = np.zeros(len(c))
        m1 = ok[b] & ~ok[a]
        np.add.at(somme, a[m1], c[b[m1]])
        np.add.at(n, a[m1], 1.0)
        m2 = ok[a] & ~ok[b]
        np.add.at(somme, b[m2], c[a[m2]])
        np.add.at(n, b[m2], 1.0)
        nouveau = (n > 0) & ~ok
        if not nouveau.any():
            break
        c[nouveau] = somme[nouveau] / n[nouveau, None]
        ok = ok | nouveau
    return c


def _colorer_generique(mesh, im, masque_photo, X0, X1, Y0, Y1):
    """Projection generique de la photo (objets sans forme de marteau).

    La photo est vue de face : la hauteur du modele correspond a la hauteur de l'objet sur la photo,
    et l'axe horizontal (X ou Z) est celui dont la silhouette projetee ressemble le plus a l'objet.
    Les faces visibles (tournees vers la photo, cote +profondeur) prennent la couleur de la photo ;
    les autres prennent la moyenne de leurs voisins visibles."""
    import numpy as np
    V = np.asarray(mesh.vertices, dtype=np.float64)
    N = np.asarray(mesh.vertex_normals)
    lo, hi = V.min(0), V.max(0)
    H, W = im.shape[:2]

    def projeter(h):
        u = (V[:, h] - lo[h]) / max(hi[h] - lo[h], 1e-9)
        v = (V[:, 1] - lo[1]) / max(hi[1] - lo[1], 1e-9)
        px = np.clip(np.round(X0 + u * (X1 - X0)).astype(int), 0, W - 1)
        py = np.clip(np.round(Y1 - v * (Y1 - Y0)).astype(int), 0, H - 1)
        return px, py

    # Choix de l'axe horizontal par ressemblance des silhouettes
    meilleur = None
    for h in (0, 2):
        px, py = projeter(h)
        forme = np.zeros((H, W), bool)
        forme[py, px] = True
        for dy in (-2, -1, 0, 1, 2):
            for dx in (-2, -1, 0, 1, 2):
                forme |= np.roll(np.roll(forme, dy, 0), dx, 1)
        inter = np.logical_and(forme, masque_photo).sum()
        union = np.logical_or(forme, masque_photo).sum()
        iou = inter / max(union, 1)
        if meilleur is None or iou > meilleur[0]:
            meilleur = (iou, h)
    iou, h = meilleur
    prof = 2 if h == 0 else 0            # axe de profondeur
    px, py = projeter(h)
    c = im[py, px].astype(np.float64)
    visible = N[:, prof] > 0.2
    if visible.any() and not visible.all():
        c = _propager_couleurs(mesh, c, visible)
    rgba = np.zeros((len(V), 4), dtype=np.uint8)
    rgba[:, :3] = np.clip(c, 0, 255).astype(np.uint8)
    rgba[:, 3] = 255
    mesh.visual.vertex_colors = rgba
    print("Couleurs reprises de la photo (projection generique, axe %s, ressemblance %.2f)."
          % ("XZ"[h // 2], iou), flush=True)


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
        return _colorer_generique(mesh, im, np.abs(im - fond).sum(-1) > 60, X0, X1, Y0, Y1)

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
        print("Forme non reconnue comme un marteau (ecart %.0f) : projection generique." % score, flush=True)
        return _colorer_generique(mesh, im, np.abs(im - fond).sum(-1) > 60, X0, X1, Y0, Y1)
    # Couleurs de la photo projetees sur tout le modele, sans assombrissement ni adoucissement
    # (les ombres seront gerees dans le jeu).

    # Les faces qui ne regardent pas la photo (cotes, dos) ne doivent pas recevoir les pixels
    # de la projection (souvent sombres) : elles prennent la moyenne de leurs voisins visibles.
    normales = np.asarray(mesh.vertex_normals)
    visible = normales[:, axe] * signe > 0.2
    if visible.any() and not visible.all():
        c = _propager_couleurs(mesh, couleurs.astype(np.float64), visible)
        couleurs = c
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
    mesh_uv.export(os.path.join(dossier, nom + ".glb"), include_normals=True)




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

    nb_corps = len(parties)  # les morceaux suivants sont les ailes
    if getattr(args, "ailes", False):
        p = PARAM
        position = min(max(float(getattr(args, "position_ailes", 0.5)), 0.05), 0.95)
        x_pre = p["x_debut"] + position * (p["L_manche"] - p["x_debut"])
        u = np.clip(x_pre / p["L_manche"], 0.0, 1.0)
        r_pre = p["r_col"] + (p["r_bout"] - p["r_col"]) * u
        x_final = x_pre * echelle + decalage[0]
        parties += _ailes_param(x_final, decalage[1], decalage[2], r_pre * echelle, position=position,
                                longueur=float(getattr(args, "longueur_ailes", 10.0)) / 100.0)
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

    # noeuds nommes : le corps et les ailes restent separables dans l'edition 3D
    morceaux = trimesh.Scene()
    for k, (materiel, V, UV, F) in enumerate(parties):
        img = img_bois if materiel == "Bois" else img_acier
        vis = trimesh.visual.TextureVisuals(uv=UV, material=trimesh.visual.material.SimpleMaterial(image=img))
        nom_noeud = ("ailes_%d" % (k - nb_corps)) if k >= nb_corps else ("corps_%d" % k)
        morceaux.add_geometry(trimesh.Trimesh(vertices=V, faces=F, visual=vis, process=False),
                              node_name=nom_noeud)
    morceaux.export(os.path.join(dossier, args.nom + ".glb"), include_normals=True)

    print("Fichiers crees dans : " + dossier, flush=True)
    print("  %d sommets, %d faces" % (len(tout), sum(len(p[3]) for p in parties)), flush=True)
    if os.path.isdir(os.path.join(ROOT, "unity")):
        cible = os.path.join(UNITY_ITEMS, args.nom)
        os.makedirs(cible, exist_ok=True)
        for nom_fichier in (args.nom + ".obj", args.nom + ".mtl", args.nom + "_bois.png", args.nom + "_acier.png"):
            shutil.copy2(os.path.join(dossier, nom_fichier), os.path.join(cible, nom_fichier))
        print("Copie pour Unity : Assets/Resources/Items/" + args.nom + "/", flush=True)
    print("Termine en %d secondes." % int(time.time() - debut), flush=True)



def _matrice_redressement_auto(mesh):
    """Matrice 4x4 qui aligne l'objet sur ses axes principaux, l'axe le plus vertical allant vers le haut."""
    import numpy as np
    import trimesh
    to_origin, _ = trimesh.bounds.oriented_bounds(mesh)
    axes = np.linalg.inv(to_origin)[:3, :3]        # colonnes : axes de la boite dans le repere de depart
    i = int(np.argmax(np.abs(axes[1, :])))         # axe de la boite le plus proche de la verticale
    autres = [j for j in range(3) if j != i]
    P = np.zeros((3, 3))
    P[1, i] = float(np.sign(axes[1, i])) or 1.0    # sens : vers le haut
    P[0, autres[0]] = 1.0
    P[2, autres[1]] = 1.0
    if np.linalg.det(P) < 0:
        P[0, :] *= -1.0
    M = np.eye(4)
    M[:3, :3] = P
    return M @ to_origin


def _matrice_inclinaison(tx, tz):
    """Inclinaison manuelle en degres : tx autour de X (avant/arriere), tz autour de Z (gauche/droite).
    Meme convention que la page de redressement (Rz x Rx)."""
    import math
    import numpy as np
    a, b = math.radians(tx), math.radians(tz)
    Rx = np.array([[1, 0, 0, 0],
                   [0, math.cos(a), -math.sin(a), 0],
                   [0, math.sin(a), math.cos(a), 0],
                   [0, 0, 0, 1]])
    Rz = np.array([[math.cos(b), -math.sin(b), 0, 0],
                   [math.sin(b), math.cos(b), 0, 0],
                   [0, 0, 1, 0],
                   [0, 0, 0, 1]])
    return Rz @ Rx


def _poser_au_sol(sc):
    """Base de l'objet a y = 0, centre sur x et z."""
    bmin, bmax = sc.bounds
    sc.apply_translation([-(bmin[0] + bmax[0]) / 2.0, -bmin[1], -(bmin[2] + bmax[2]) / 2.0])


def redresser_fichier(args):
    """Redresse le modele du piece : alignement automatique, puis inclinaison (tx, tz en degres).
    --apercu : ecrit <nom>_redresse_apercu.glb et sauvegarde le modele d'origine.
    Sans --apercu : ecrit le GLB final <nom>.glb a partir de la sauvegarde."""
    import trimesh
    dossier = os.path.join(SORTIES, args.nom)
    cible = os.path.join(dossier, args.nom + ".glb")
    if not os.path.isfile(cible):
        print("Redressement impossible : modele introuvable " + cible, flush=True)
        return 1
    source = os.path.join(dossier, args.nom + "_avant_redressement.glb")
    if args.apercu or not os.path.isfile(source):
        shutil.copyfile(cible, source)
    sc = trimesh.load(source, force="scene")
    sc.apply_transform(_matrice_redressement_auto(sc.dump(concatenate=True)))
    sc.apply_transform(_matrice_inclinaison(args.tx, args.tz))
    _poser_au_sol(sc)
    sortie = os.path.join(dossier, args.nom + "_redresse_apercu.glb") if args.apercu else cible
    sc.export(sortie, include_normals=True)
    print("Redressement : inclinaison manuelle %+.1f (avant/arriere), %+.1f (gauche/droite) degres -> %s"
          % (args.tx, args.tz, os.path.basename(sortie)), flush=True)
    return 0


def ajouter_ailes_fichier(args):
    """Ajoute deux ailes a un marteau deja fabrique (.glb). Le marteau d'origine est garde en copie."""
    import numpy as np
    import trimesh

    cible = os.path.join(SORTIES, args.nom, args.nom + ".glb")
    os.makedirs(os.path.dirname(cible), exist_ok=True)
    sauvegarde = os.path.join(SORTIES, args.nom, args.nom + "_sans_ailes.glb")
    if not os.path.isfile(sauvegarde):
        shutil.copyfile(args.image, sauvegarde)

    sc = trimesh.load(args.image, force="scene")
    bmin, bmax = sc.bounds
    sc.apply_scale((args.taille / 100.0) / max(float((bmax - bmin).max()), 1e-9))
    bmin, bmax = sc.bounds
    centre_h = (bmin + bmax) / 2.0

    # Ailes : meme geometrie que le marteau parametrique, a la taille demandee.
    parties = [("Bois", *_manche_param()), ("Acier", *_tete_param()), ("Acier", *_panne_param())]
    tout = np.vstack([q[1] for q in parties])
    etendue = tout.max(0) - tout.min(0)
    echelle = (args.taille / 100.0) / float(etendue.max())
    bas = tout.min(0) * echelle
    decalage = np.array([-(tout[:, 0].min() + tout[:, 0].max()) / 2.0 * echelle,
                         -bas[1],
                         -(tout[:, 2].min() + tout[:, 2].max()) / 2.0 * echelle])
    centre_param = np.array([0.0, float(etendue[1]) * echelle / 2.0, 0.0])
    position = 0.5
    x_pre = PARAM["x_debut"] + position * (PARAM["L_manche"] - PARAM["x_debut"])
    u = np.clip(x_pre / PARAM["L_manche"], 0.0, 1.0)
    r_pre = PARAM["r_col"] + (PARAM["r_bout"] - PARAM["r_col"]) * u
    x_final = x_pre * echelle + decalage[0]
    ailes = _ailes_param(x_final, decalage[1], decalage[2], r_pre * echelle, position=position,
                         longueur=float(args.longueur_ailes) / 100.0)
    decalage_monde = centre_h - centre_param
    img_bois = _texture_bois_param()
    img_acier = _texture_acier_param()
    for k, (materiel, V, UV, F) in enumerate(ailes):
        img = img_bois if materiel == "Bois" else img_acier
        vis = trimesh.visual.TextureVisuals(uv=UV, material=trimesh.visual.material.SimpleMaterial(image=img))
        morceau = trimesh.Trimesh(vertices=np.asarray(V) + decalage_monde, faces=F, visual=vis, process=False)
        sc.add_geometry(morceau, node_name="ailes_%d" % k)
    sc.export(cible, include_normals=True)
    print("Ailes ajoutees au marteau : " + cible, flush=True)


def generer_image_ia(args):
    """Cree une image a partir d'une description (SDXL-Turbo, processeur), sur fond uni."""
    os.environ.update(env_ia())
    import torch
    from diffusers import AutoPipelineForText2Image

    torch.set_num_threads(os.cpu_count() or 8)
    print("Chargement du modele d'image (premiere fois : telechargement, plusieurs Go)...", flush=True)
    pipe = AutoPipelineForText2Image.from_pretrained(MODELE_IMAGE, torch_dtype=torch.float32,
                                                     use_safetensors=True)
    pipe = pipe.to("cpu")
    pipe.enable_attention_slicing()
    texte = args.prompt.strip() + ", seul au centre, fond blanc uni, photo nette, vue de cote"
    print("Creation de l'image (peut prendre quelques minutes sur processeur)...", flush=True)
    image = pipe(prompt=texte, num_inference_steps=4, guidance_scale=0.0,
                 width=512, height=512).images[0]
    os.makedirs(os.path.dirname(args.sortie_image), exist_ok=True)
    image.save(args.sortie_image)
    print("Image creee : " + args.sortie_image, flush=True)


def generer_detourage(args):
    """Detoure la photo (sans TripoSR) et enregistre l'apercu avec transparence."""
    os.environ.update(env_ia())
    sys.path.insert(0, TRIPO)
    import rembg
    from PIL import Image
    from tsr.utils import remove_background, resize_foreground

    image = Image.open(args.image)
    if getattr(args, "recadrage", ""):
        x0, y0, x1, y1 = [float(t) for t in args.recadrage.split(",")]
        largeur_p, hauteur_p = image.size
        image = image.crop((int(x0 * largeur_p), int(y0 * hauteur_p),
                            int(x1 * largeur_p), int(y1 * hauteur_p)))
    image = recadrer_objet(image)
    session = rembg.new_session()
    image = remove_background(image, session)
    image = resize_foreground(image, 0.85)
    dossier = os.path.join(SORTIES, args.nom)
    os.makedirs(dossier, exist_ok=True)
    image.save(os.path.join(dossier, "detourage.png"))
    print("Detourage pret (apercu). Verifiez-le avant la fabrication 3D.", flush=True)


SF3D_DEPOT = "https://codeload.github.com/Stability-AI/stable-fast-3d/zip/refs/heads/main"
SF3D_DIR = os.path.join(IA, "stable-fast-3d")
VENV_SF3D = os.path.join(IA, "venv_sf3d")
MOTEUR_FICHIER = "moteur.txt"


def venv_sf3d_python():
    if os.name == "nt":
        return os.path.join(VENV_SF3D, "Scripts", "python.exe")
    return os.path.join(VENV_SF3D, "bin", "python")


def sf3d_installe():
    return os.path.isfile(venv_sf3d_python()) and os.path.isfile(os.path.join(SF3D_DIR, "run.py"))


def moteur_actif():
    """'sf3d', 'triposr' ou 'les_deux' (choix du menu deroulant, enregistre dans IA/moteur.txt)."""
    try:
        with open(os.path.join(IA, MOTEUR_FICHIER), encoding="utf-8") as f:
            valeur = f.read().strip()
            return valeur if valeur in ("sf3d", "triposr", "les_deux") else "sf3d"
    except OSError:
        return "sf3d"


ETIQUETTE_MOTEUR = {"sf3d": "Stable Fast 3D", "triposr": "TripoSR", "les_deux": "TripoSR + Stable Fast 3D"}
VALEUR_MOTEUR = {v: k for k, v in ETIQUETTE_MOTEUR.items()}


def ecrire_moteur(valeur):
    os.makedirs(IA, exist_ok=True)
    with open(os.path.join(IA, MOTEUR_FICHIER), "w", encoding="utf-8") as f:
        f.write(valeur)


def installer_sf3d(log):
    """Installe Stable Fast 3D dans IA\\venv_sf3d. Renvoie 0 si tout est en place."""
    os.makedirs(IA, exist_ok=True)
    if not os.path.isfile(os.path.join(SF3D_DIR, "run.py")):
        zip_chemin = os.path.join(IA, "stable-fast-3d.zip")
        log("Telechargement du code de Stable Fast 3D...")
        try:
            urllib.request.urlretrieve(SF3D_DEPOT, zip_chemin)
        except OSError as erreur:
            log("ECHEC du telechargement : " + str(erreur))
            return 1
        with zipfile.ZipFile(zip_chemin) as z:
            z.extractall(IA)
        extrait = os.path.join(IA, "stable-fast-3d-main")
        if os.path.isdir(SF3D_DIR):
            shutil.rmtree(SF3D_DIR)
        os.rename(extrait, SF3D_DIR)
    if not os.path.isfile(venv_sf3d_python()):
        log("Creation de l'environnement IA\\venv_sf3d...")
        if lancer([sys.executable, "-m", "venv", VENV_SF3D], log) != 0:
            log("ECHEC : creation de l'environnement impossible.")
            return 1
    py = venv_sf3d_python()
    etapes = [
        [py, "-m", "pip", "install", "-U", "pip"],
        [py, "-m", "pip", "install", "setuptools==69.5.1", "wheel"],
        [py, "-m", "pip", "install", "torch", "torchvision"],
        [py, "-m", "pip", "install", "einops==0.7.0", "jaxtyping==0.2.31", "omegaconf==2.3.0",
         "transformers==4.42.3", "open_clip_torch==2.24.0", "trimesh==4.4.1", "numpy==1.26.4",
         "huggingface-hub==0.23.4", "rembg==2.0.57", "onnxruntime", "pynanoinstantmeshes==0.0.3",
         "gpytoolbox==0.2.0", "pillow", "tqdm", "imageio", "scipy"],
        [py, "-m", "pip", "install", "--no-build-isolation", os.path.join(SF3D_DIR, "texture_baker")],
        [py, "-m", "pip", "install", "--no-build-isolation", os.path.join(SF3D_DIR, "uv_unwrapper")],
    ]
    for etape in etapes:
        if lancer(etape, log) != 0:
            log("=== ECHEC de l'installation de Stable Fast 3D : voir le journal ===")
            if "texture_baker" in etape or "uv_unwrapper" in etape:
                log("Les extensions C++ demandent les outils de build C++ de Visual Studio 2022 (sous Windows).")
            return 1
    log("Installation de Stable Fast 3D terminee.")
    log("Derniere etape (une seule fois) : demande l'acces sur https://huggingface.co/stabilityai/stable-fast-3d")
    log("puis, dans une invite de commandes : IA\\venv_sf3d\\Scripts\\huggingface-cli login (colle ton jeton la-bas).")
    return 0


def generer_sf3d(args):
    """Modele depuis une photo avec Stable Fast 3D. Le modele fournit deja sa texture (UV) : pas de recoloration."""
    os.environ.update(env_ia())
    sys.path.insert(0, SF3D_DIR)
    import torch
    import rembg
    from PIL import Image
    from sf3d.system import SF3D
    from sf3d.utils import remove_background, resize_foreground

    debut = time.time()
    device = "cuda:0" if torch.cuda.is_available() else "cpu"
    if device == "cpu":
        os.environ["SF3D_USE_CPU"] = "1"
    torch.set_num_threads(os.cpu_count() or 4)
    print("Calcul sur : " + device + " (" + str(torch.get_num_threads()) + " threads)", flush=True)
    print("Chargement de Stable Fast 3D (premiere fois : telechargement)...", flush=True)
    model = SF3D.from_pretrained("stabilityai/stable-fast-3d", config_name="config.yaml",
                                 weight_name="model.safetensors")
    model.to(device)
    model.eval()
    session = rembg.new_session()
    image = remove_background(Image.open(args.image).convert("RGBA"), session)
    image = resize_foreground(image, 0.85)
    dossier = os.path.join(SORTIES, args.nom)
    os.makedirs(dossier, exist_ok=True)
    image.save(os.path.join(dossier, "photo_traitee.png"))
    print("Creation du modele 3D (processeur, patientez)...", flush=True)
    with torch.no_grad():
        mesh = model.run_image([image], bake_resolution=1024, remesh="none", vertex_count=-1)
    if isinstance(mesh, (list, tuple)):
        mesh = mesh[0]
    bornes = mesh.bounds
    etendue = bornes[1] - bornes[0]
    if float(etendue.max()) <= 0:
        raise RuntimeError("le modele genere est vide : essaie une autre photo")
    mesh.apply_scale((args.taille / 100.0) / float(etendue.max()))
    bornes = mesh.bounds
    mesh.apply_translation([
        -(bornes[0][0] + bornes[1][0]) / 2.0,
        -bornes[0][1],
        -(bornes[0][2] + bornes[1][2]) / 2.0,
    ])
    mesh.export(os.path.join(dossier, args.nom + ".glb"), include_normals=True)
    print("Modele Stable Fast 3D : " + args.nom + ".glb", flush=True)
    print("Termine en %d secondes." % int(time.time() - debut), flush=True)


def _cmd_avec_nom(cmd, nouveau_nom):
    """Copie de la commande worker avec un autre --nom (pour garder les deux resultats)."""
    copie = list(cmd)
    copie[copie.index("--nom") + 1] = nouveau_nom
    return copie


def lancer_generation(cmd_triposr, log):
    """Genere depuis une photo avec le moteur choisi. Si Stable Fast 3D echoue, TripoSR prend le relais.
    Mode « les deux » : Stable Fast 3D garde le nom d'origine, TripoSR sort dans <nom>_triposr.
    Renvoie le code de sortie (0 si le resultat principal est la)."""
    if moteur_actif() == "les_deux":
        nom = cmd_triposr[cmd_triposr.index("--nom") + 1]
        if not sf3d_installe():
            log("Stable Fast 3D n'est pas installe : TripoSR seul est lance (resultat principal).")
            return executer_worker(cmd_triposr, log)
        log("=== Moteur 1/2 : Stable Fast 3D ===")
        code_sf3d = executer_worker([venv_sf3d_python()] + cmd_triposr[1:] + ["--moteur", "sf3d"], log)
        log("=== Moteur 2/2 : TripoSR (resultat separe : " + nom + "_triposr) ===")
        code_triposr = executer_worker(_cmd_avec_nom(cmd_triposr, nom + "_triposr"), log)
        if code_triposr == 0:
            log("TripoSR a produit : sorties\\" + nom + "_triposr\\" + nom + "_triposr.glb")
        if code_sf3d == 0:
            return 0
        return code_triposr
    if moteur_actif() == "sf3d":
        if sf3d_installe():
            cmd_sf3d = [venv_sf3d_python()] + cmd_triposr[1:] + ["--moteur", "sf3d"]
            log("=== Moteur : Stable Fast 3D ===")
            if executer_worker(cmd_sf3d, log) == 0:
                return 0
            log("Stable Fast 3D a echoue : essai avec TripoSR.")
        else:
            log("Stable Fast 3D n'est pas installe : TripoSR est utilise. Bouton « Installer Stable Fast 3D » (dans la fenetre).")
    log("=== Moteur : TripoSR ===")
    return executer_worker(cmd_triposr, log)


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
    image = Image.open(args.image)
    if getattr(args, "recadrage", ""):
        # recadrage choisi par l'utilisateur : x0,y0,x1,y1 en fractions de la photo
        x0, y0, x1, y1 = [float(t) for t in args.recadrage.split(",")]
        largeur_p, hauteur_p = image.size
        image = image.crop((int(x0 * largeur_p), int(y0 * hauteur_p),
                            int(x1 * largeur_p), int(y1 * hauteur_p)))
    if not getattr(args, "deja_detoure", False):
        image = recadrer_objet(image)
    apercu_transparent = None  # version avec transparence, pour la page de placement
    if getattr(args, "deja_detoure", False):
        # photo deja detouree et retouchee a la main : on garde sa transparence
        image = resize_foreground(Image.open(args.image).convert("RGBA"), 0.85)
        apercu_transparent = image.copy()
        arr = np.array(image).astype(np.float32) / 255.0
        arr = arr[:, :, :3] * arr[:, :, 3:4] + (1 - arr[:, :, 3:4]) * 0.5
        image = Image.fromarray((arr * 255.0).astype(np.uint8))
    elif args.retirer_fond:
        session = rembg.new_session()
        image = remove_background(image, session)
        image = resize_foreground(image, 0.85)
        apercu_transparent = image.copy()
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
    (apercu_transparent if apercu_transparent is not None else image).save(
        os.path.join(dossier, "photo_traitee.png"))

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
        mesh.export(os.path.join(dossier, args.nom + ".glb"), include_normals=True)
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
    lu = None
    if trouve:
        lu = (float(trouve.group(1).replace(",", ".")), trouve.group(2))
    else:
        # Sans unite (ex. « 48 ») : le dernier nombre de la phrase est lu en cm.
        nombres = re.findall(r"\d+(?:[.,]\d+)?", t)
        if nombres:
            lu = (float(nombres[-1].replace(",", ".")), "cm")
    if lu:
        valeur, unite = lu
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
    import re
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
        if mots_cles and not any(re.search(r"\b" + re.escape(mot) + r"\b", titre.lower())
                                 for mot in mots_cles):
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


def chercher_images_libres(requete, n=15, mots_cles=("hammer", "mallet", "marteau")):
    import urllib.parse
    url = COMMONS_API + "?" + urllib.parse.urlencode({
        "action": "query", "format": "json", "generator": "search",
        "gsrsearch": requete, "gsrnamespace": 6, "gsrlimit": 50,
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
    t = re.sub(r"\b(fait|fais|faire|donne|donner|cree|crée|creer|créer|veux|voudrais|met|mets|mettre|pose|poser|ajoute|ajouter|place|placer|je|moi|nous|svp|stp)\b", " ", t)
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


def fusionner_ailes(pieces):
    """« marteau avec ailes » : les ailes ne sont pas une piece a part, elles vont sur le marteau."""
    sortie = []
    for p in pieces:
        if p["nom"] in ("ailes", "aile") and sortie and "marteau" in sortie[-1]["nom"]:
            sortie[-1]["ailes"] = True
            continue
        sortie.append(dict(p, ailes=False))
    return sortie


def requete_en(nom):
    """Requete Wikimedia Commons pour une piece, et mots a retrouver dans les titres.

    Un mot sans traduction garde son nom francais (la recherche le tente quand meme)."""
    mots = nom.split()
    if not mots:
        return None, ()
    traduits = [TRADUCTIONS.get(m, m) for m in mots]
    return " ".join(traduits), tuple(set(" ".join(traduits + mots).split()))


LIMITE_CPU_FICHIER = "limite_cpu.txt"


def lire_limite_cpu():
    """Limite de processeur voulue (5 a 100 %), lue dans IA/limite_cpu.txt a chaque cycle."""
    try:
        with open(os.path.join(IA, LIMITE_CPU_FICHIER), encoding="utf-8") as f:
            return max(5, min(100, int(float(f.read().strip()))))
    except (OSError, ValueError):
        return 100


def ecrire_limite_cpu(valeur):
    """Enregistre la limite choisie avec le curseur (le limiteur la prend en compte tout de suite)."""
    os.makedirs(IA, exist_ok=True)
    with open(os.path.join(IA, LIMITE_CPU_FICHIER), "w", encoding="utf-8") as f:
        f.write(str(max(5, min(100, int(float(valeur))))))


def controler_cpu(pid, parent=None):
    """Limite la charge processeur d'un processus : pause/reprise par cycles de 0,1 s.
    Avec 50 %, le processus tourne la moitie du temps. S'arrete quand le processus se termine."""
    import psutil
    try:
        p = psutil.Process(pid)
    except psutil.Error:
        return
    cycle = 0.1
    actif = True
    try:
        while True:
            try:
                if not p.is_running() or p.status() == psutil.STATUS_ZOMBIE:
                    break
            except psutil.Error:
                break
            if parent is not None:
                try:
                    if psutil.Process(parent).status() == psutil.STATUS_ZOMBIE:
                        break  # l'app est fermee : on enleve la limite
                except psutil.Error:
                    break
            limite = lire_limite_cpu()
            if limite >= 100:
                if not actif:
                    p.resume()
                    actif = True
                time.sleep(0.2)
                continue
            marche = cycle * limite / 100.0
            if not actif:
                p.resume()
                actif = True
            time.sleep(marche)
            p.suspend()
            actif = False
            time.sleep(cycle - marche)
    finally:
        try:
            if not actif:
                p.resume()
        except psutil.Error:
            pass


def demarrer_limiteur(proc):
    """Lance le limiteur (processus separe) pour le travail proc. Renvoie le processus limiteur."""
    try:
        return subprocess.Popen([venv_python(), os.path.abspath(__file__), "--limiteur-pid", str(proc.pid),
                                 "--limiteur-parent", str(os.getpid())],
                                env=env_ia(), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except OSError:
        return None


def executer_worker(cmd, log):
    proc = subprocess.Popen(cmd, env=env_ia(), stdout=subprocess.PIPE,
                            stderr=subprocess.STDOUT, text=True,
                            encoding="utf-8", errors="replace")
    limiteur = demarrer_limiteur(proc)
    for ligne in proc.stdout:
        log(ligne.rstrip())
    code = proc.wait()
    if limiteur is not None:
        limiteur.wait()
    return code


def _points(V, M):
    import numpy as np
    return (np.asarray(V) @ M[:3, :3].T) + M[:3, 3]


def assembler(args):
    """Pose les pieces (fichiers .glb) les unes par rapport aux autres et exporte une scene.

    ancrage = [u, v, facteur, rotation] sur une piece posee « sur » la precedente :
    u, v : point choisi sur la photo du support ; facteur : taille relative ;
    rotation : degres, sens horaire comme sur la page de placement."""
    import json
    import numpy as np
    import trimesh
    entrees = json.loads(args.entrees)
    dossier = os.path.join(SORTIES, args.nom)
    os.makedirs(dossier, exist_ok=True)
    marge = 0.02  # 2 cm entre deux pieces posees cote a cote
    combine = trimesh.Scene()
    precedent = None  # (min, max) de la piece precedente, en metres
    prec_sommets = None  # sommets de la piece precedente (dans la scene)
    for k, e in enumerate(entrees):
        sc = trimesh.load(e["fichier"], force="scene")
        anc = e.get("ancrage")
        rotation = float(anc[3]) if anc and len(anc) > 3 else 0.0
        bmin0, bmax0 = sc.bounds
        c0 = (bmin0 + bmax0) / 2.0
        # rotation dans le plan de l'image (autour de Z), dans le meme sens que la page
        Rz = trimesh.transformations.rotation_matrix(np.radians(-rotation), [0, 0, 1])
        Rc = (trimesh.transformations.translation_matrix(c0) @ Rz
              @ trimesh.transformations.translation_matrix(-c0))
        noeuds = []
        for node in sc.graph.nodes_geometry:
            T, nom_geom = sc.graph[node]
            noeuds.append((node, nom_geom, Rc @ T))
        pts = np.vstack([_points(sc.geometry[nom].vertices, TT) for _, nom, TT in noeuds])
        bmin, bmax = pts.min(0), pts.max(0)

        if e.get("taille") is None and precedent is not None:
            # piece posee sur une autre : facteur (0.5 par defaut) x taille du support
            pmin_p, pmax_p = precedent
            facteur = float(anc[2]) if anc and len(anc) > 2 else 0.5
            taille_m = facteur * float((pmax_p - pmin_p).max())
        else:
            taille_m = float(e["taille"]) / 100.0
        echelle = taille_m / float(max((bmax - bmin).max(), 1e-9))
        smin, smax = bmin * echelle, bmax * echelle
        bas_centre = np.array([(smin[0] + smax[0]) / 2.0, smin[1], (smin[2] + smax[2]) / 2.0])

        if precedent is None:
            cible = np.array([0.0, 0.0, 0.0])
        else:
            pmin, pmax = precedent
            milieu_z = (pmin[2] + pmax[2]) / 2.0
            if e["relation"] == "sur" and anc is not None and prec_sommets is not None:
                u, v = float(anc[0]), float(anc[1])  # u : gauche -> droite, v : haut -> bas
                xa = pmin[0] + u * (pmax[0] - pmin[0])
                largeur = max(pmax[0] - pmin[0], 1e-6)
                proches = prec_sommets[np.abs(prec_sommets[:, 0] - xa) <= 0.03 * largeur]
                if len(proches) == 0:
                    proches = prec_sommets[np.argsort(np.abs(prec_sommets[:, 0] - xa))[:50]]
                dessus = proches[np.argmax(proches[:, 1])]
                cible = np.array([xa, dessus[1], dessus[2]])
            elif e["relation"] == "sur":
                cible = np.array([(pmin[0] + pmax[0]) / 2.0, pmax[1], milieu_z])
            else:
                cible = np.array([pmax[0] + marge + (smax[0] - smin[0]) / 2.0, 0.0, milieu_z])

        M = np.eye(4)
        M[:3, :3] *= echelle
        M[:3, 3] = cible - bas_centre
        sommets_new = []
        pmin_new, pmax_new = None, None
        for node, nom, TT in noeuds:
            g = sc.geometry[nom].copy()
            g.apply_transform(M @ TT)
            combine.add_geometry(g, node_name="p%d_%s" % (k, node))
            sommets_new.append(np.asarray(g.vertices))
            gmin, gmax = g.bounds
            pmin_new = gmin if pmin_new is None else np.minimum(pmin_new, gmin)
            pmax_new = gmax if pmax_new is None else np.maximum(pmax_new, gmax)
        precedent = (pmin_new, pmax_new)
        prec_sommets = np.vstack(sommets_new)
        print("Piece %d posee (%s)." % (k + 1, e["relation"]), flush=True)
    chemin = os.path.join(dossier, args.nom + ".glb")
    combine.export(chemin, include_normals=True)
    etendue = (combine.bounds[1] - combine.bounds[0]) * 100
    print("Scene assemblee : " + chemin, flush=True)
    print("Dimensions : %.1f x %.1f x %.1f cm" % tuple(etendue), flush=True)


EDITION_HTML = r"""<!doctype html>
<html lang="fr">
<head>
<meta charset="utf-8">
<title>LibreVies - edition 3D</title>
<style>
body { font-family: sans-serif; margin: 16px; background: #f2f2f2; }
#scene { width: 800px; height: 520px; border: 2px solid #777; display: inline-block; vertical-align: top; }
#panneau { display: inline-block; vertical-align: top; margin-left: 14px; width: 300px; }
button { font-size: 14px; padding: 6px 10px; margin: 2px; }
select { font-size: 15px; padding: 4px; width: 100%; }
.actif { background: #9cf !important; }
</style>
<script type="importmap">
{ "imports": {
    "three": "https://unpkg.com/three@0.160.0/build/three.module.js",
    "three/addons/": "https://unpkg.com/three@0.160.0/examples/jsm/" } }
</script>
</head>
<body>
<h3>Edition 3D : placer les pieces avant le fichier final</h3>
<p>Dans la liste, choisis <b>Tous les objets</b> pour tout deplacer ensemble, ou une seule partie
(marteau, ailes...) pour ne modifier qu'elle. Glisse la souris dans le vide pour tourner la vue ; la molette zoome.</p>
<div id="scene"></div>
<div id="panneau">
  <p><b>Objet a modifier :</b><br><select id="choix"></select></p>
  <p>Mode :
    <button id="m_translate">Deplacer</button>
    <button id="m_scale">Redimensionner</button>
    <button id="m_rotate">Tourner</button></p>
  <p>Taille : <input type="range" id="taille" min="10" max="300" value="100" disabled>
    <span id="taillev">100</span> %</p>
  <p>Dimensions : <span id="dims">-</span></p>
  <p>Couleur : <input type="color" id="couleur" value="#c8a878">
    <button id="appliquer_couleur">Appliquer</button>
    <button id="couleur_origine">Couleur d'origine</button></p>
  <p>
    <button id="supprimer">Supprimer la partie selectionnee</button>
    <button id="ok">Valider et creer le modele final</button>
    <button id="annuler">Annuler</button>
  </p>
  <p id="etat"></p>
</div>
<script type="module">
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { TransformControls } from 'three/addons/controls/TransformControls.js';

const PIECES = __PIECES__;

const LARG = 800, HAUT = 520;
const rendu = new THREE.WebGLRenderer({ antialias: true });
rendu.setSize(LARG, HAUT);
document.getElementById('scene').appendChild(rendu.domElement);
const scene = new THREE.Scene();
scene.background = new THREE.Color(0xe8e8e8);
const camera = new THREE.PerspectiveCamera(45, LARG / HAUT, 0.005, 20);
camera.position.set(0.6, 0.5, 0.8);
camera.lookAt(0, 0.1, 0);
scene.add(new THREE.HemisphereLight(0xffffff, 0x888888, 2.0));
const lumiere = new THREE.DirectionalLight(0xffffff, 1.5);
lumiere.position.set(1, 2, 1);
scene.add(lumiere);
scene.add(new THREE.GridHelper(1.0, 20));

const orbite = new OrbitControls(camera, rendu.domElement);
orbite.target.set(0, 0.1, 0);
orbite.enableZoom = false;
// zoom molette : 5 % par cran, limites 30 cm a 4 m
rendu.domElement.addEventListener('wheel', (e) => {
  e.preventDefault();
  const vers = orbite.target.clone().sub(camera.position);
  const d = vers.length();
  const facteur = Math.exp(Math.sign(e.deltaY) * 0.05);
  const nd = THREE.MathUtils.clamp(d * facteur, 0.3, 4);
  vers.normalize();
  camera.position.copy(orbite.target).addScaledVector(vers, -nd);
}, { passive: false });

const transf = new TransformControls(camera, rendu.domElement);
transf.addEventListener('dragging-changed', e => { orbite.enabled = !e.value; });
transf.addEventListener('objectChange', majDims);
scene.add(transf);

const ensemble = new THREE.Group();
scene.add(ensemble);
const groupes = [];   // {nom, groupe, couleur}
let cible = null;     // objet actuellement manipule (ensemble ou une partie)
const etat = document.getElementById('etat');
const choix = document.getElementById('choix');
const taille = document.getElementById('taille');

function majDims() {
  if (!cible) return;
  const t = new THREE.Box3().setFromObject(cible).getSize(new THREE.Vector3());
  document.getElementById('dims').textContent =
    (t.x * 100).toFixed(1) + ' x ' + (t.y * 100).toFixed(1) + ' x ' + (t.z * 100).toFixed(1) + ' cm';
  taille.value = Math.round(cible.scale.x * 100);
  document.getElementById('taillev').textContent = Math.round(cible.scale.x * 100);
}

function selectionner(valeur) {
  cible = (valeur === 'tous') ? ensemble : groupes[parseInt(valeur, 10)].groupe;
  transf.attach(cible);
  taille.disabled = false;
  majDims();
}
choix.onchange = () => selectionner(choix.value);

function setMode(m) {
  transf.setMode(m);
  ['m_translate', 'm_scale', 'm_rotate'].forEach(id =>
    document.getElementById(id).classList.toggle('actif', id === 'm_' + m));
}
document.getElementById('m_translate').onclick = () => setMode('translate');
document.getElementById('m_scale').onclick = () => setMode('scale');
document.getElementById('m_rotate').onclick = () => setMode('rotate');
setMode('translate');

taille.oninput = (e) => {
  if (!cible) return;
  const v = parseFloat(e.target.value) / 100;
  cible.scale.setScalar(v);
  document.getElementById('taillev').textContent = e.target.value;
  majDims();
};

function piecesCiblees() {
  return choix.value === 'tous' ? groupes.filter(g => !g.supprime) : [groupes[parseInt(choix.value, 10)]];
}

document.getElementById('appliquer_couleur').onclick = () => {
  const hex = document.getElementById('couleur').value;
  piecesCiblees().forEach(p => {
    p.groupe.traverse(o => {
      if (o.isMesh) {
        if (o.userData.matOrig === undefined) o.userData.matOrig = o.material;
        o.material = new THREE.MeshStandardMaterial({ color: hex, roughness: 0.6, metalness: 0.0, side: THREE.DoubleSide });
      }
    });
    p.couleur = hex;
  });
  etat.textContent = 'Couleur appliquee.';
};

document.getElementById('couleur_origine').onclick = () => {
  piecesCiblees().forEach(p => {
    p.groupe.traverse(o => {
      if (o.isMesh && o.userData.matOrig !== undefined) o.material = o.userData.matOrig;
    });
    p.couleur = null;
  });
  etat.textContent = 'Couleur d\'origine restauree.';
};

async function charger() {
  const chargeur = new GLTFLoader();
  let decalage = 0;
  const opt = document.createElement('option');
  opt.value = 'tous';
  opt.textContent = 'Tous les objets';
  choix.appendChild(opt);
  for (let i = 0; i < PIECES.length; i++) {
    const p = PIECES[i];
    etat.textContent = 'Chargement de ' + p.nom + '...';
    const gltf = await chargeur.loadAsync(p.url);
    const groupe = new THREE.Group();
    groupe.add(gltf.scene);
    gltf.scene.traverse(o => {
      if (o.isMesh) {
        o.material = o.material.clone();
        o.material.side = THREE.DoubleSide;
      }
    });
    ensemble.add(groupe);
    groupe.updateMatrixWorld(true);
    const boite = new THREE.Box3().setFromObject(groupe);
    const dim = boite.getSize(new THREE.Vector3());
    // Pose initiale : pieces cote a cote, posees sur le sol
    groupe.position.set(decalage - boite.min.x, -boite.min.y, -(boite.min.z + boite.max.z) / 2);
    decalage += dim.x + 0.02;
    groupes.push({ nom: p.nom, groupe, couleur: null });
    const o = document.createElement('option');
    o.value = String(i);
    o.textContent = p.nom;
    choix.appendChild(o);
  }
  choix.value = 'tous';
  etat.textContent = PIECES.length + ' partie(s) chargee(s).';
  selectionner('tous');
}

document.getElementById('supprimer').onclick = () => {
  if (choix.value === 'tous') {
    etat.textContent = 'Choisis une partie precise dans la liste pour la supprimer.';
    return;
  }
  const idx = parseInt(choix.value, 10);
  const g = groupes[idx];
  if (!g || g.supprime) return;
  transf.detach();
  g.groupe.removeFromParent();
  g.supprime = true;
  choix.querySelector('option[value="' + idx + '"]').remove();
  choix.value = 'tous';
  selectionner('tous');
  etat.textContent = 'Partie supprimee : ' + g.nom;
};

document.getElementById('ok').onclick = async () => {
  scene.updateMatrixWorld(true);
  const pieces = groupes.filter(g => !g.supprime)
    .map(g => ({ nom: g.nom, matrice: g.groupe.matrixWorld.toArray(), couleur: g.couleur }));
  etat.textContent = 'Envoi...';
  const rep = await fetch('/valider', { method: 'POST', headers: { 'Content-Type': 'application/json' },
                                        body: JSON.stringify({ pieces: pieces }) });
  etat.textContent = rep.ok ? 'Valide. Tu peux fermer cette page.' : 'Erreur d envoi.';
};
document.getElementById('annuler').onclick = async () => {
  await fetch('/valider', { method: 'POST', headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify({ annule: true }) });
  etat.textContent = 'Annule. Tu peux fermer cette page.';
};

function boucle() {
  requestAnimationFrame(boucle);
  orbite.update();
  rendu.render(scene, camera);
}
boucle();
charger().catch(err => { etat.textContent = 'Erreur de chargement : ' + err; });
</script>
</body>
</html>
"""


def editer_3d(args):
    """Applique les positions/tailles choisies sur la page d'edition et ecrit <nom>_edite.glb."""
    import json
    import numpy as np
    import trimesh
    with open(args.entrees_fichier, encoding="utf-8") as f:
        donnees = json.load(f)
    dossier = os.path.join(SORTIES, args.nom)
    os.makedirs(dossier, exist_ok=True)
    combine = trimesh.Scene()
    for k, p in enumerate(donnees["pieces"]):
        sc = trimesh.load(p["fichier"], force="scene")
        # three.js : matrice en ordre colonne par colonne
        M = np.array(p["matrice"], dtype=float).reshape(4, 4).T
        couleur = p.get("couleur")
        if couleur:
            h = couleur.lstrip("#")
            rgba = np.array([int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255], dtype=np.uint8)
        for node in sc.graph.nodes_geometry:
            T, nom_geom = sc.graph[node]
            g = sc.geometry[nom_geom].copy()
            g.apply_transform(M @ T)
            if couleur:
                # Vrai materiau PBR de la couleur choisie : visible dans tous les visionneurs
                # (les couleurs de sommets seules sont ignorees par certains).
                from trimesh.visual.material import PBRMaterial
                mat = PBRMaterial(name="couleur_" + h.lower(), baseColorFactor=rgba,
                                  metallicFactor=0.0, roughnessFactor=0.6, doubleSided=True)
                g.visual = trimesh.visual.TextureVisuals(material=mat)
            combine.add_geometry(g, node_name="p%d_%s" % (k, node))
        print("Piece %d placee : %s" % (k + 1, p.get("nom", "")), flush=True)
    chemin = os.path.join(dossier, args.nom + ".glb")
    combine.export(chemin, include_normals=True)
    etendue = (combine.bounds[1] - combine.bounds[0]) * 100
    print("Modele final : " + chemin, flush=True)
    print("Dimensions : %.1f x %.1f x %.1f cm" % tuple(etendue), flush=True)


# Decoupage generique des objets d'un seul bloc (sortie d'un moteur 3D) en pieces nommees.
# Mettre a False pour revenir au comportement d'avant (un objet = une piece).
DECOUPAGE_AUTO_OBJETS = True
DECOUPAGE_AUTO_NB_PIECES = 2


def decouper_fichier(args):
    """Worker : separe les ailes du reste du modele, pour les deplacer independamment.
    Un modele sans ailes reste un seul objet. Lit --entrees-fichier ({"pieces": [{fichier, nom}]})
    et ecrit <entrees>_parties.json : deux objets, « <nom> » et « ailes de <nom> »."""
    import json
    import re
    import trimesh
    with open(args.entrees_fichier, encoding="utf-8") as f:
        donnees = json.load(f)
    sortie = []
    for k, p in enumerate(donnees["pieces"]):
        sc = trimesh.load(p["fichier"], force="scene")
        noeuds = list(sc.graph.nodes_geometry)
        ailes = [n for n in noeuds if str(n).lower().startswith("ailes")]
        corps = [n for n in noeuds if n not in ailes]
        if not ailes and DECOUPAGE_AUTO_OBJETS and len(noeuds) == 1:
            # Objet d'un seul bloc : decoupage generique en pieces, noms automatiques.
            import sys
            sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
            import decoupage_generique
            dossier = os.path.join(os.path.dirname(p["fichier"]), "parties", "p%d" % k)
            catalogue = decoupage_generique.decouper(p["fichier"], dossier, k=DECOUPAGE_AUTO_NB_PIECES)
            for piece in catalogue["pieces"]:
                sortie.append({"fichier": piece["fichier"], "nom": p["nom"] + " - " + piece["nom"]})
            continue
        if not ailes or not corps:
            sortie.append({"fichier": p["fichier"], "nom": p["nom"]})
            continue
        dossier = os.path.join(os.path.dirname(p["fichier"]), "parties")
        os.makedirs(dossier, exist_ok=True)
        for groupe, nom_objet, suffixe in ((corps, p["nom"], "corps"),
                                           (ailes, "ailes de " + p["nom"], "ailes")):
            nouvelle = trimesh.Scene()
            for node in groupe:
                T, nom_geom = sc.graph[node]
                g = sc.geometry[nom_geom].copy()
                g.apply_transform(T)
                nouvelle.add_geometry(g, node_name=str(node))
            fichier = os.path.join(dossier, "p%d_%s.glb" % (k, suffixe))
            nouvelle.export(fichier, include_normals=True)
            sortie.append({"fichier": fichier, "nom": nom_objet})
    with open(args.entrees_fichier.replace(".json", "_parties.json"), "w", encoding="utf-8") as f:
        json.dump({"pieces": sortie}, f)
    print("Objets : %d" % len(sortie), flush=True)


def lancer_edition(chemins, nom, log, noms=None, delai=3600):
    """Ouvre la page d'edition 3D avec les pieces (eclatees en parties), puis ecrit le modele final <nom>.glb.
    Renvoie True si le modele a ete cree."""
    import json
    noms = noms or [os.path.basename(c) for c in chemins]
    dossier = os.path.join(SORTIES, nom)
    os.makedirs(dossier, exist_ok=True)
    source = os.path.join(dossier, nom + "_source_edition.json")
    with open(source, "w", encoding="utf-8") as f:
        json.dump({"pieces": [{"fichier": c, "nom": n} for c, n in zip(chemins, noms)]}, f)
    cmd_decoupe = [venv_python(), os.path.abspath(__file__), "--worker", "--decouper-3d",
                   "--entrees-fichier", source, "--nom", nom, "--taille", "0"]
    if executer_worker(cmd_decoupe, log) == 0 and os.path.isfile(source.replace(".json", "_parties.json")):
        with open(source.replace(".json", "_parties.json"), encoding="utf-8") as f:
            parties = json.load(f)["pieces"]
        chemins = [p["fichier"] for p in parties]
        noms = [p["nom"] for p in parties]
    pieces_js = [{"nom": n, "url": "/piece/%d.glb" % i} for i, n in enumerate(noms)]
    fichiers = {"/piece/%d.glb" % i: c for i, c in enumerate(chemins)}
    page = EDITION_HTML.replace("__PIECES__", json.dumps(pieces_js))
    log("=== Edition 3D : placez, agrandissez ou reduisez les pieces dans le navigateur ===")
    res = lancer_page(page, fichiers, log, delai=delai)
    if not res or res.get("annule") or not res.get("pieces"):
        log("Edition 3D annulee : rien n'a ete cree.")
        return False
    entrees = os.path.join(dossier, nom + "_placement.json")
    donnees = {"pieces": [{"fichier": c, "nom": n, "matrice": p["matrice"], "couleur": p.get("couleur")}
                          for c, n, p in zip(chemins, noms, res["pieces"])]}
    with open(entrees, "w", encoding="utf-8") as f:
        json.dump(donnees, f)
    cmd = [venv_python(), os.path.abspath(__file__), "--worker", "--editer-3d",
           "--entrees-fichier", entrees, "--nom", nom, "--taille", "0"]
    code = executer_worker(cmd, log)
    log("=== Fini : " + os.path.join(dossier, nom + ".glb") + " ===" if code == 0
        else "=== ECHEC edition 3D : voir le journal ===")
    return code == 0


CHAT_QUEUE = queue.Queue()


def apercu_3d(args):
    """Ecrit <modele>_apercu.png : vue rapide du modele (couleurs et ombrage)."""
    import numpy as np
    import trimesh
    from PIL import Image, ImageDraw
    sc = trimesh.load(args.image, force="scene")
    m = sc.dump(concatenate=True)
    V = np.asarray(m.vertices, dtype=float)
    F = np.asarray(m.faces)
    if len(F) == 0:
        raise SystemExit("modele vide : apercu impossible")
    try:
        couleurs = np.asarray(m.visual.to_color().face_colors[:, :3], dtype=float)
    except Exception:
        couleurs = np.tile(np.array([190.0, 170.0, 140.0]), (len(F), 1))
    N = np.asarray(m.face_normals, dtype=float)
    V = V - (V.min(0) + V.max(0)) / 2.0
    a, b = np.radians(-35.0), np.radians(-15.0)
    Ry = np.array([[np.cos(a), 0, np.sin(a)], [0, 1, 0], [-np.sin(a), 0, np.cos(a)]])
    Rx = np.array([[1, 0, 0], [0, np.cos(b), -np.sin(b)], [0, np.sin(b), np.cos(b)]])
    R = Rx @ Ry
    P = V @ R.T
    N = N @ R.T
    if len(F) > 60000:
        idx = np.random.default_rng(0).choice(len(F), 60000, replace=False)
        F, couleurs, N = F[idx], couleurs[idx], N[idx]
    W, H = 720, 540
    etendue = P[:, :2].max(0) - P[:, :2].min(0)
    echelle = 0.85 * min(W / max(etendue[0], 1e-9), H / max(etendue[1], 1e-9))
    XY = np.stack([W / 2 + P[:, 0] * echelle, H / 2 - P[:, 1] * echelle], 1)
    lum = np.array([0.3, 0.5, 1.0])
    lum = lum / np.linalg.norm(lum)
    ombre = 0.4 + 0.6 * np.abs(N @ lum)
    ordre = np.argsort(P[F].mean(1)[:, 2])  # du plus loin au plus proche
    img = Image.new("RGB", (W, H), (232, 232, 232))
    dessin = ImageDraw.Draw(img)
    for i in ordre:
        pts = [tuple(p) for p in XY[F[i]]]
        c = tuple(int(min(255, x * ombre[i])) for x in couleurs[i])
        dessin.polygon(pts, fill=c)
    chemin = os.path.splitext(args.image)[0] + "_apercu.png"
    img.save(chemin)
    print("Apercu : " + chemin, flush=True)


def apres_generation(glbs, nom_final, log, noms, dans_chat=True):
    """Apres la generation : montre l'apercu de chaque piece (assistant ou journal),
    puis ouvre l'edition 3D avant l'enregistrement du modele final."""
    def dire(msg):
        if dans_chat:
            CHAT_QUEUE.put(("msg", msg))
        else:
            log(msg)

    for glb, nom in zip(glbs, noms):
        if not os.path.isfile(glb):
            log("Fichier introuvable : " + glb)
            continue
        cmd = [venv_python(), os.path.abspath(__file__), "--worker", "--apercu-3d",
               "--image", glb, "--nom", nom, "--taille", "0"]
        png = os.path.splitext(glb)[0] + "_apercu.png"
        if executer_worker(cmd, log) == 0 and os.path.isfile(png):
            if dans_chat:
                CHAT_QUEUE.put(("image", png, "Apercu : " + nom))
            else:
                log("Apercu : " + png)
        else:
            dire("Apercu indisponible pour '%s' (voir le journal)." % nom)
    dire("edition 3D avant l'enregistrement de '%s' : placez, agrandissez ou "
         "reduisez les pieces, puis validez." % nom_final)
    if lancer_edition(glbs, nom_final, log, noms=noms):
        dire("modele final enregistre : sorties\\%s\\%s.glb" % (nom_final, nom_final))
    else:
        dire("edition annulee : rien n'a ete enregistre pour '%s'." % nom_final)


PLACEMENT_HTML = r"""<!doctype html>
<html lang="fr">
<head>
<meta charset="utf-8">
<title>LibreVies - placement</title>
<style>
body { font-family: sans-serif; margin: 16px; background: #f2f2f2; }
#zone { position: relative; display: inline-block; border: 2px solid #777; background: #fff; touch-action: none; }
#support { display: block; max-width: 640px; height: auto; user-select: none; }
#piece { position: absolute; cursor: move; border: 2px dashed #d33; user-select: none; }
button { font-size: 16px; padding: 8px 16px; margin-top: 12px; }
</style>
</head>
<body>
<h3>Placez « __PIECE__ » sur « __SUPPORT__ »</h3>
<p>Glissez l'image pour que sa base soit posee sur le dessus de la tete.</p>
<div id="zone">
  <img id="support" src="/img/support" alt="support">
  <img id="piece" src="/img/piece" alt="piece">
</div>
<p>Taille : <input type="range" id="taille" min="5" max="150" value="50"> <span id="tv">50</span> % de la largeur du support</p>
<p>Rotation : <input type="range" id="rotation" min="-180" max="180" value="0"> <span id="rv">0</span> degres</p>
<button id="valider">Valider le placement</button>
<span id="etat"></span>
<script>
const zone = document.getElementById('zone');
const sup = document.getElementById('support');
const pc = document.getElementById('piece');
const curseur = document.getElementById('taille');
const rot = document.getElementById('rotation');
let glisse = null;

function appliquerRotation() {
  pc.style.transform = 'rotate(' + rot.value + 'deg)';
  document.getElementById('rv').textContent = rot.value;
}
function dimensionner() {
  const W = sup.clientWidth;
  if (!W || !pc.naturalWidth) return;
  const deja = pc.style.top !== '';
  const bas = deja ? pc.offsetTop + pc.offsetHeight : 0;
  const centre = deja ? pc.offsetLeft + pc.offsetWidth / 2 : W / 2;
  const largeur = W * curseur.value / 100;
  pc.style.width = largeur + 'px';
  const h = pc.offsetHeight;
  pc.style.left = (centre - largeur / 2) + 'px';
  pc.style.top = (deja ? bas - h : sup.clientHeight * 0.1) + 'px';
  document.getElementById('tv').textContent = curseur.value;
  appliquerRotation();
}
sup.addEventListener('load', dimensionner);
pc.addEventListener('load', dimensionner);
window.addEventListener('load', dimensionner);
curseur.addEventListener('input', dimensionner);
rot.addEventListener('input', appliquerRotation);

pc.addEventListener('pointerdown', function (e) {
  const r = zone.getBoundingClientRect();
  glisse = { dx: e.clientX - r.left - pc.offsetLeft, dy: e.clientY - r.top - pc.offsetTop };
  pc.setPointerCapture(e.pointerId);
  e.preventDefault();
});
pc.addEventListener('pointermove', function (e) {
  if (!glisse) return;
  const r = zone.getBoundingClientRect();
  pc.style.left = (e.clientX - r.left - glisse.dx) + 'px';
  pc.style.top = (e.clientY - r.top - glisse.dy) + 'px';
});
pc.addEventListener('pointerup', function () { glisse = null; });
pc.addEventListener('pointercancel', function () { glisse = null; });

document.getElementById('valider').addEventListener('click', async function () {
  const W = sup.clientWidth, H = sup.clientHeight;
  const w = pc.offsetWidth, h = pc.offsetHeight;
  const cx = pc.offsetLeft + w / 2, cy = pc.offsetTop + h / 2;
  const t = parseFloat(rot.value) * Math.PI / 180;
  // point de la base (bas au centre de l'image), apres rotation autour du centre
  const bx = cx - (h / 2) * Math.sin(t);
  const by = cy + (h / 2) * Math.cos(t);
  const corps = { u: bx / W, v: by / H, facteur: curseur.value / 100, rotation: parseFloat(rot.value) };
  const rep = await fetch('/valider', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corps) });
  document.getElementById('etat').textContent = rep.ok ? ' Placement enregistre. Vous pouvez fermer cette page.' : ' Erreur, reessayez.';
});
</script>
</body>
</html>
"""

CADRAGE_HTML = r"""<!doctype html>
<html lang="fr">
<head>
<meta charset="utf-8">
<title>LibreVies - recadrage</title>
<style>
body { font-family: sans-serif; margin: 16px; background: #f2f2f2; }
#zone { position: relative; display: inline-block; border: 2px solid #777; background: #fff; touch-action: none; cursor: crosshair; }
#photo { display: block; max-width: 640px; height: auto; user-select: none; }
#cadre { position: absolute; border: 2px dashed #d33; background: rgba(255, 0, 0, 0.08); display: none; pointer-events: none; }
button { font-size: 16px; padding: 8px 16px; margin: 12px 8px 0 0; }
</style>
</head>
<body>
<h3>Recadrez « __PIECE__ »</h3>
<p>Dessinez un rectangle autour de l'objet a modeliser (en gardant la main ou la personne hors du cadre si possible).</p>
<div id="zone">
  <img id="photo" src="/img/piece" alt="photo">
  <div id="cadre"></div>
</div>
<br>
<button id="valider">Valider le recadrage</button>
<button id="tout">Garder la photo entiere</button>
<span id="etat"></span>
<script>
const zone = document.getElementById('zone');
const photo = document.getElementById('photo');
const cadre = document.getElementById('cadre');
let debut = null, fin = null, enCours = false;

function position(e) {
  const r = photo.getBoundingClientRect();
  return {
    x: Math.min(Math.max((e.clientX - r.left) / r.width, 0), 1),
    y: Math.min(Math.max((e.clientY - r.top) / r.height, 0), 1)
  };
}
function dessiner() {
  if (!debut || !fin) { cadre.style.display = 'none'; return; }
  const x0 = Math.min(debut.x, fin.x), x1 = Math.max(debut.x, fin.x);
  const y0 = Math.min(debut.y, fin.y), y1 = Math.max(debut.y, fin.y);
  cadre.style.display = 'block';
  cadre.style.left = (photo.offsetLeft + x0 * photo.offsetWidth) + 'px';
  cadre.style.top = (photo.offsetTop + y0 * photo.offsetHeight) + 'px';
  cadre.style.width = ((x1 - x0) * photo.offsetWidth) + 'px';
  cadre.style.height = ((y1 - y0) * photo.offsetHeight) + 'px';
}
zone.addEventListener('pointerdown', function (e) {
  enCours = true;
  debut = position(e); fin = debut;
  zone.setPointerCapture(e.pointerId);
  dessiner();
  e.preventDefault();
});
zone.addEventListener('pointermove', function (e) {
  if (!enCours) return;
  fin = position(e);
  dessiner();
});
zone.addEventListener('pointerup', function () { enCours = false; });
zone.addEventListener('pointercancel', function () { enCours = false; });

async function envoyer(corps) {
  const rep = await fetch('/valider', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corps) });
  document.getElementById('etat').textContent = rep.ok ? ' Recadrage enregistre. Vous pouvez fermer cette page.' : ' Erreur, reessayez.';
}
document.getElementById('valider').addEventListener('click', function () {
  if (!debut || !fin) { envoyer({ x0: 0, y0: 0, x1: 1, y1: 1 }); return; }
  envoyer({
    x0: Math.min(debut.x, fin.x), y0: Math.min(debut.y, fin.y),
    x1: Math.max(debut.x, fin.x), y1: Math.max(debut.y, fin.y)
  });
});
document.getElementById('tout').addEventListener('click', function () {
  envoyer({ x0: 0, y0: 0, x1: 1, y1: 1 });
});
</script>
</body>
</html>
"""


def lancer_page(page_html, fichiers, log, delai=1800):
    """Sert une page locale dans le navigateur et attend son envoi (POST /valider).

    fichiers : {adresse: chemin} des images servies a la page. Renvoie le dict envoye, ou None."""
    import http.server
    import json
    import mimetypes
    import webbrowser
    page = page_html.encode("utf-8")
    resultat = {"valeur": None}
    fin = threading.Event()

    class Gestion(http.server.BaseHTTPRequestHandler):
        def log_message(self, *args):
            pass

        def _envoyer(self, code, corps, type_):
            self.send_response(code)
            self.send_header("Content-Type", type_)
            self.send_header("Content-Length", str(len(corps)))
            self.end_headers()
            self.wfile.write(corps)

        def do_GET(self):
            if self.path == "/":
                self._envoyer(200, page, "text/html; charset=utf-8")
            elif self.path in fichiers and os.path.isfile(fichiers[self.path]):
                with open(fichiers[self.path], "rb") as f:
                    donnees = f.read()
                type_ = mimetypes.guess_type(fichiers[self.path])[0] or "image/png"
                self._envoyer(200, donnees, type_)
            else:
                self._envoyer(404, b"introuvable", "text/plain")

        def do_POST(self):
            if self.path != "/valider":
                self._envoyer(404, b"introuvable", "text/plain")
                return
            try:
                corps = self.rfile.read(int(self.headers.get("Content-Length", "0")))
                resultat["valeur"] = json.loads(corps.decode("utf-8"))
                self._envoyer(200, b"ok", "text/plain")
                fin.set()
            except (ValueError, TypeError):
                self._envoyer(400, b"donnees invalides", "text/plain")

    serveur = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Gestion)
    serveur.daemon_threads = True
    threading.Thread(target=serveur.serve_forever, daemon=True).start()
    adresse = "http://127.0.0.1:%d/" % serveur.server_address[1]
    try:
        webbrowser.open(adresse)
        log("Page ouverte dans le navigateur : " + adresse)
        fin.wait(delai)
    finally:
        serveur.shutdown()
        serveur.server_close()
    return resultat["valeur"]


def _borne(valeur, bas, haut):
    return min(max(float(valeur), bas), haut)


def lancer_placement(image_support, image_piece, nom_support, nom_piece, log, delai=1800):
    """Page de placement. Renvoie {u, v, facteur, rotation} ou None."""
    import html as htmllib
    page = (PLACEMENT_HTML.replace("__SUPPORT__", htmllib.escape(nom_support))
            .replace("__PIECE__", htmllib.escape(nom_piece)))
    d = lancer_page(page, {"/img/support": image_support, "/img/piece": image_piece}, log, delai)
    if d is None:
        return None
    try:
        return {"u": _borne(d["u"], 0.0, 1.0), "v": _borne(d["v"], 0.0, 1.0),
                "facteur": _borne(d["facteur"], 0.05, 1.5),
                "rotation": _borne(d.get("rotation", 0.0), -180.0, 180.0)}
    except (KeyError, ValueError, TypeError):
        return None


def lancer_cadrage(image_piece, nom_piece, log, delai=1800):
    """Page de recadrage avant la fabrication. Renvoie 'x0,y0,x1,y1' ou None (photo entiere)."""
    import html as htmllib
    page = CADRAGE_HTML.replace("__PIECE__", htmllib.escape(nom_piece))
    d = lancer_page(page, {"/img/piece": image_piece}, log, delai)
    if d is None:
        return None
    try:
        x0, y0 = _borne(d["x0"], 0.0, 1.0), _borne(d["y0"], 0.0, 1.0)
        x1, y1 = _borne(d["x1"], 0.0, 1.0), _borne(d["y1"], 0.0, 1.0)
    except (KeyError, ValueError, TypeError):
        return None
    if x1 - x0 < 0.05 or y1 - y0 < 0.05 or (x0 == 0 and y0 == 0 and x1 == 1 and y1 == 1):
        return None
    return "%.4f,%.4f,%.4f,%.4f" % (x0, y0, x1, y1)


VALIDATION_HTML = r"""<!doctype html>
<html lang="fr">
<head>
<meta charset="utf-8">
<title>LibreVies - detourage</title>
<style>
body { font-family: sans-serif; margin: 16px; background: #f2f2f2; }
#fond { display: block; width: 640px; height: 520px; overflow: auto; padding: 0; border: 2px solid #777; box-sizing: content-box;
  background-color: #fff;
  background-image: linear-gradient(45deg, #ddd 25%, transparent 25%),
    linear-gradient(-45deg, #ddd 25%, transparent 25%),
    linear-gradient(45deg, transparent 75%, #ddd 75%),
    linear-gradient(-45deg, transparent 75%, #ddd 75%);
  background-size: 20px 20px; background-position: 0 0, 0 10px, 10px -10px, -10px 0; }
#toile { display: block; max-width: none; height: auto; touch-action: none; cursor: crosshair; }
button { font-size: 16px; padding: 8px 16px; margin: 12px 8px 0 0; }
</style>
</head>
<body>
<h3>Le detourage de « __PIECE__ » est-il correct ?</h3>
<p>Le damier = transparent. Pour retoucher : maintenez le bouton de la souris et passez sur l'image.
 « Effacer » rend transparent, « Restaurer » remet la photo d'origine.
 <b>Molette</b> : zoom avant/arriere (au niveau du curseur). Barres de defilement pour deplacer l'image.</p>
<div id="fond"><canvas id="toile"></canvas></div>
<p>
  <label><input type="radio" name="outil" value="effacer" checked> Effacer</label>
  <label><input type="radio" name="outil" value="restaurer"> Restaurer</label>
  &nbsp; Taille du pinceau : <input type="range" id="pinceau" min="4" max="150" value="30">
  &nbsp; <button id="reset">Annuler mes retouches</button>
</p>
<button id="ok">Oui, lancer la fabrication 3D</button>
<button id="non">Non, recadrer a nouveau</button>
<span id="etat"></span>
<script>
const toile = document.getElementById('toile');
const ctx = toile.getContext('2d');
const copie = document.createElement('canvas');
const copieCtx = copie.getContext('2d');
const original = new Image();
let peint = false;

let zoom = 1;
function appliquerZoom() {
  toile.style.width = (toile.width * zoom) + 'px';
  toile.style.height = 'auto';
}
original.onload = function () {
  toile.width = original.naturalWidth;
  toile.height = original.naturalHeight;
  copie.width = original.naturalWidth;
  copie.height = original.naturalHeight;
  copieCtx.drawImage(original, 0, 0);
  remettreOriginal();
  zoom = Math.min(1, 600 / original.naturalWidth);
  appliquerZoom();
};

// Molette : zoom de 10 % par cran, centre sur le curseur (limites 20 % a 800 %).
const zone = document.getElementById('fond');
zone.addEventListener('wheel', function (e) {
  if (!toile.width) return;
  e.preventDefault();
  const avant = toile.getBoundingClientRect();
  const fx = (e.clientX - avant.left) / avant.width;
  const fy = (e.clientY - avant.top) / avant.height;
  zoom = Math.min(8, Math.max(0.2, zoom * Math.exp(-Math.sign(e.deltaY) * 0.1)));
  appliquerZoom();
  const apres = toile.getBoundingClientRect();
  zone.scrollLeft += (apres.left + fx * apres.width) - e.clientX;
  zone.scrollTop += (apres.top + fy * apres.height) - e.clientY;
}, { passive: false });
original.src = '/img/detour';

function remettreOriginal() {
  ctx.clearRect(0, 0, toile.width, toile.height);
  ctx.drawImage(copie, 0, 0);
}
function coords(e) {
  const r = toile.getBoundingClientRect();
  return { x: (e.clientX - r.left) * toile.width / r.width, y: (e.clientY - r.top) * toile.height / r.height };
}
function tamponner(p) {
  const echelle = toile.width / toile.getBoundingClientRect().width;
  const rayon = parseFloat(document.getElementById('pinceau').value) / 2 * echelle;
  const restaurer = document.querySelector('input[name=outil]:checked').value === 'restaurer';
  ctx.save();
  ctx.beginPath();
  ctx.arc(p.x, p.y, rayon, 0, Math.PI * 2);
  if (restaurer) {
    ctx.clip();
    ctx.drawImage(copie, 0, 0);
  } else {
    ctx.globalCompositeOperation = 'destination-out';
    ctx.fill();
  }
  ctx.restore();
}
toile.addEventListener('pointerdown', function (e) {
  peint = true;
  toile.setPointerCapture(e.pointerId);
  tamponner(coords(e));
});
toile.addEventListener('pointermove', function (e) {
  if (peint) tamponner(coords(e));
});
toile.addEventListener('pointerup', function () { peint = false; });
toile.addEventListener('pointercancel', function () { peint = false; });
document.getElementById('reset').addEventListener('click', remettreOriginal);

async function envoyer(ok) {
  const corps = { ok: ok, png: toile.toDataURL('image/png') };
  const rep = await fetch('/valider', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(corps) });
  document.getElementById('etat').textContent = rep.ok ? ' Merci, vous pouvez fermer cette page.' : ' Erreur, reessayez.';
}
document.getElementById('ok').addEventListener('click', function () { envoyer(true); });
document.getElementById('non').addEventListener('click', function () { envoyer(false); });
</script>
</body>
</html>
"""


def ajouter_ailes_glb(glb, nom, taille, longueur, log):
    cmd = [venv_python(), os.path.abspath(__file__), "--worker", "--ajouter-ailes",
           "--image", glb, "--nom", nom, "--taille", str(taille), "--longueur-ailes", str(longueur)]
    log("=== Ajout des ailes au marteau ===")
    if executer_worker(cmd, log) != 0:
        log("=== ECHEC de l'ajout des ailes : voir le journal ===")


REDRESSEMENT_HTML = r"""<!doctype html>
<html lang="fr">
<head>
<meta charset="utf-8">
<title>LibreVies - redressement</title>
<style>
body { font-family: sans-serif; margin: 16px; background: #f2f2f2; }
#scene { width: 640px; height: 480px; background: #e8e8e8; border: 2px solid #777; }
button { font-size: 16px; padding: 8px 14px; margin: 4px; }
#fleches { display: inline-block; vertical-align: top; margin-left: 16px; }
#fleches div { text-align: center; }
</style>
<script type="importmap">
{ "imports": {
    "three": "https://unpkg.com/three@0.160.0/build/three.module.js",
    "three/addons/": "https://unpkg.com/three@0.160.0/examples/jsm/" } }
</script>
</head>
<body>
<h3>Redressement de « __PIECE__ »</h3>
<p>Le modele a deja ete redresse automatiquement. Si il penche encore, utilisez les fleches :
le dessus de l'objet suit la fleche. Glissez avec la souris pour tourner la vue.</p>
<p><button id="depart">Revenir au départ</button> <span>(orientation juste après la génération, redressement automatique)</span></p>
<div id="scene"></div>
<div id="fleches">
  <div><button id="haut">&uarr; Haut</button></div>
  <div><button id="gauche">&larr; Gauche</button> <button id="droite">Droite &rarr;</button></div>
  <div><button id="bas">&darr; Bas</button></div>
</div>
<p>Pas : <input type="range" id="pas" min="1" max="30" value="5"> <span id="pasv">5</span> degres
  &nbsp; Inclinaison : <span id="angles">0, 0</span></p>
<p>
  <button id="ok">Valider et enregistrer le modele</button>
  <button id="non">Garder le modele d'origine</button>
  <span id="etat"></span>
</p>
<script type="module">
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

const zone = document.getElementById('scene');
const rendu = new THREE.WebGLRenderer({ antialias: true });
rendu.setSize(640, 480);
zone.appendChild(rendu.domElement);
const scene = new THREE.Scene();
scene.add(new THREE.AmbientLight(0xffffff, 0.8));
const lumiere = new THREE.DirectionalLight(0xffffff, 1.0);
lumiere.position.set(2, 4, 3);
scene.add(lumiere);
const sol = new THREE.GridHelper(2, 20, 0x999999, 0xbbbbbb);
scene.add(sol);
const camera = new THREE.PerspectiveCamera(40, 640 / 480, 0.01, 100);
camera.position.set(1.6, 1.2, 2.2);
camera.lookAt(0, 0.3, 0);
const commandes = new OrbitControls(camera, rendu.domElement);
commandes.target.set(0, 0.3, 0);
commandes.enableZoom = false;  // zoom gere ci-dessous : 5 % par cran, limites 20 cm a 3 m
rendu.domElement.addEventListener('wheel', (e) => {
  e.preventDefault();
  const vers = commandes.target.clone().sub(camera.position);
  const d = vers.length();
  const facteur = Math.exp(Math.sign(e.deltaY) * 0.05);
  const nd = THREE.MathUtils.clamp(d * facteur, 0.2, 3);
  vers.normalize();
  camera.position.copy(commandes.target).addScaledVector(vers, -nd);
}, { passive: false });
commandes.update();

const pivot = new THREE.Group();
scene.add(pivot);
let tx = 0, tz = 0;
let pas = 5;

function miseAJour() {
  const m = new THREE.Matrix4().makeRotationZ(THREE.MathUtils.degToRad(tz))
    .multiply(new THREE.Matrix4().makeRotationX(THREE.MathUtils.degToRad(tx)));
  pivot.position.set(0, 0, 0);
  pivot.quaternion.setFromRotationMatrix(m);
  pivot.updateMatrixWorld(true);
  const b = new THREE.Box3().setFromObject(pivot, true);
  pivot.position.set(-(b.min.x + b.max.x) / 2, -b.min.y, -(b.min.z + b.max.z) / 2);
  document.getElementById('angles').textContent = tx.toFixed(0) + ', ' + tz.toFixed(0);
}

new GLTFLoader().load('/modele.glb', (gltf) => {
  pivot.add(gltf.scene);
  miseAJour();
  document.getElementById('etat').textContent = 'Modele charge.';
}, undefined, () => {
  document.getElementById('etat').textContent = 'Modele introuvable : garder le modele d\'origine.';
});

document.getElementById('haut').onclick = () => { tx -= pas; miseAJour(); };
document.getElementById('bas').onclick = () => { tx += pas; miseAJour(); };
document.getElementById('gauche').onclick = () => { tz += pas; miseAJour(); };
document.getElementById('droite').onclick = () => { tz -= pas; miseAJour(); };
document.getElementById('depart').onclick = () => { tx = 0; tz = 0; pas = 5; document.getElementById('pas').value = 5; document.getElementById('pasv').textContent = 5; miseAJour(); };
document.getElementById('pas').oninput = (e) => {
  pas = parseFloat(e.target.value);
  document.getElementById('pasv').textContent = pas;
};

function envoyer(corps) {
  document.getElementById('ok').disabled = true;
  document.getElementById('non').disabled = true;
  fetch('/valider', { method: 'POST', headers: { 'Content-Type': 'application/json' },
                      body: JSON.stringify(corps) })
    .then(() => { document.getElementById('etat').textContent = 'Enregistre. Vous pouvez fermer cette page.'; });
}
document.getElementById('ok').onclick = () => envoyer({ valide: true, tx: tx, tz: tz });
document.getElementById('non').onclick = () => envoyer({ valide: false });

function boucle() {
  requestAnimationFrame(boucle);
  commandes.update();
  rendu.render(scene, camera);
}
boucle();
</script>
</body>
</html>
"""


def lancer_redressement(nom, nom_piece, log, delai=1800):
    """Redresse le modele d'une piece : alignement automatique, puis page pour corriger a la main."""
    import html as htmllib
    cmd = [venv_python(), os.path.abspath(__file__), "--worker", "--redresser", "--apercu",
           "--nom", nom, "--taille", "0"]
    log("=== Redressement automatique de '%s' ===" % nom_piece)
    if executer_worker(cmd, log) != 0:
        log("Redressement automatique impossible : modele garde tel quel.")
        return
    apercu = os.path.join(SORTIES, nom, nom + "_redresse_apercu.glb")
    page = REDRESSEMENT_HTML.replace("__PIECE__", htmllib.escape(nom_piece))
    log("Page de redressement ouverte : corrigez a la main si besoin, puis validez.")
    d = lancer_page(page, {"/modele.glb": apercu}, log, delai)
    if not d or not d.get("valide"):
        log("Modele d'origine garde pour '%s'." % nom_piece)
        return
    cmd = [venv_python(), os.path.abspath(__file__), "--worker", "--redresser",
           "--nom", nom, "--taille", "0",
           "--tx", str(float(d.get("tx", 0.0))), "--tz", str(float(d.get("tz", 0.0)))]
    if executer_worker(cmd, log) != 0:
        log("=== ECHEC du redressement final : voir le journal ===")


def lancer_validation(image_detour, nom_piece, log, delai=1800):
    """Renvoie (valide, png_edite) ou None si la page est fermee.

    valide : True (on continue la fabrication), False (recadrer a nouveau).
    png_edite : octets du detourage retouche, ou None."""
    import base64
    import html as htmllib
    page = VALIDATION_HTML.replace("__PIECE__", htmllib.escape(nom_piece))
    d = lancer_page(page, {"/img/detour": image_detour}, log, delai)
    if d is None:
        return None
    png = None
    donnee = d.get("png")
    if isinstance(donnee, str) and donnee.startswith("data:image/png;base64,"):
        try:
            png = base64.b64decode(donnee.split(",", 1)[1])
        except (ValueError, TypeError):
            png = None
    return bool(d.get("ok")), png


def interface():
    import tkinter as tk
    from tkinter import filedialog, messagebox, ttk

    fenetre = tk.Tk()
    fenetre.title("LibreVies - Photo vers modele 3D")
    fenetre.geometry("760x560")

    file_journal = queue.Queue()

    def log(msg):
        file_journal.put(str(msg))
        try:  # copie dans un fichier, pour pouvoir relire le diagnostic apres coup
            os.makedirs(IA, exist_ok=True)
            with open(os.path.join(IA, "journal_LibreVies.txt"), "a", encoding="utf-8") as f:
                f.write(time.strftime("%H:%M:%S") + "  " + str(msg) + "\n")
        except OSError:
            pass

    etat = {"pret": False, "photo": tk.StringVar(value=""), "nom": tk.StringVar(value="objet"),
            "taille": tk.StringVar(value="30"), "mc": tk.StringVar(value="256"),
            "fond": tk.BooleanVar(value=True)}

    cadre = ttk.Frame(fenetre, padding=10)
    cadre.pack(fill="both", expand=True)

    # --- Connexion Hugging Face (jeton) : bandeau en haut de la fenetre ---
    jeton_fichier = os.path.join(MODELES, "huggingface", "token")
    jeton_var = tk.StringVar(value="")
    file_compte = queue.Queue()

    bandeau_hf = ttk.Frame(fenetre, padding=(10, 8, 10, 0))
    bandeau_hf.pack(side="top", fill="x", before=cadre)
    ttk.Label(bandeau_hf, text="Jeton Hugging Face :").pack(side="left")
    ttk.Entry(bandeau_hf, textvariable=jeton_var, width=40, show="*").pack(side="left", padx=5)
    etiquette_compte = ttk.Label(bandeau_hf, text="non connecte")
    etiquette_compte.pack(side="left", padx=5)

    def verifier_compte(jeton):
        """Demande a Hugging Face le pseudo lie au jeton (en arriere-plan)."""
        def travail():
            file_compte.put("verification...")
            env = env_ia()
            env["HF_TOKEN"] = jeton
            py = venv_sf3d_python() if os.path.isfile(venv_sf3d_python()) else venv_python()
            try:
                res = subprocess.run([py, "-c", "from huggingface_hub import whoami; print(whoami()['name'])"],
                                     env=env, capture_output=True, text=True, timeout=90)
                if res.returncode == 0 and res.stdout.strip():
                    file_compte.put("connecte : " + res.stdout.strip().splitlines()[-1])
                else:
                    file_compte.put("jeton refuse")
            except Exception as erreur:
                file_compte.put("erreur : %s" % erreur)
        threading.Thread(target=travail, daemon=True).start()

    def connecter_hf():
        jeton = jeton_var.get().strip()
        if not jeton:
            etiquette_compte.config(text="colle ton jeton")
            return
        os.makedirs(os.path.dirname(jeton_fichier), exist_ok=True)
        with open(jeton_fichier, "w", encoding="utf-8") as f:
            f.write(jeton)
        jeton_var.set("")
        verifier_compte(jeton)

    ttk.Button(bandeau_hf, text="Connecter", command=connecter_hf).pack(side="left", padx=5)

    def poll_compte():
        try:
            while True:
                etiquette_compte.config(text=file_compte.get_nowait())
        except queue.Empty:
            pass
        fenetre.after(200, poll_compte)

    # Au demarrage : reprend le jeton deja enregistre (dossier IA ou cache par defaut du CLI)
    cache_defaut = os.path.join(os.path.expanduser("~"), ".cache", "huggingface", "token")
    if not os.path.isfile(jeton_fichier) and os.path.isfile(cache_defaut):
        os.makedirs(os.path.dirname(jeton_fichier), exist_ok=True)
        shutil.copyfile(cache_defaut, jeton_fichier)
    if os.path.isfile(jeton_fichier):
        with open(jeton_fichier, encoding="utf-8") as f:
            verifier_compte(f.read().strip())
    fenetre.after(200, poll_compte)

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

    cadre_moteur = ttk.Frame(cadre)
    cadre_moteur.grid(row=4, column=2, sticky="w", padx=5)
    ttk.Label(cadre_moteur, text="Moteur :").pack(side="left")
    etat["moteur"] = tk.StringVar(value=ETIQUETTE_MOTEUR.get(moteur_actif(), "Stable Fast 3D"))
    ecrire_moteur(VALEUR_MOTEUR.get(etat["moteur"].get(), "sf3d"))

    def changer_moteur(_evenement=None):
        ecrire_moteur(VALEUR_MOTEUR.get(etat["moteur"].get(), "sf3d"))

    combo_moteur = ttk.Combobox(cadre_moteur, textvariable=etat["moteur"],
                                values=["Stable Fast 3D", "TripoSR", "TripoSR + Stable Fast 3D"],
                                width=24, state="readonly")
    combo_moteur.pack(side="left", padx=4)
    combo_moteur.bind("<<ComboboxSelected>>", changer_moteur)

    def installer_sf3d_fenetre():
        def travail_install():
            installer_sf3d(log)

        log("=== Installation de Stable Fast 3D (longue, patientez) ===")
        threading.Thread(target=travail_install, daemon=True).start()

    if not sf3d_installe():
        ttk.Button(cadre_moteur, text="Installer Stable Fast 3D", command=installer_sf3d_fenetre).pack(side="left", padx=4)

    cadre_cpu = ttk.Frame(cadre)
    cadre_cpu.grid(row=4, column=0, sticky="w", pady=4)
    ttk.Label(cadre_cpu, text="Limite CPU :").pack(side="left")
    etat["limite_cpu"] = tk.IntVar(value=100)
    etiquette_cpu = ttk.Label(cadre_cpu, text="100 %", width=5)
    etiquette_cpu.pack(side="right")

    def changer_limite_cpu(valeur):
        valeur = int(round(float(valeur)))
        etat["limite_cpu"].set(valeur)
        etiquette_cpu.config(text="%d %%" % valeur)
        ecrire_limite_cpu(valeur)

    curseur_cpu = tk.Scale(cadre_cpu, from_=5, to=100, orient="horizontal", length=90,
                           showvalue=0, command=changer_limite_cpu)
    curseur_cpu.set(100)
    curseur_cpu.pack(side="left", padx=4)
    ecrire_limite_cpu(100)

    ttk.Label(cadre, text="Ou decrire l'objet (image creee sur ce PC) :").grid(row=6, column=0, sticky="w")
    etat["description"] = tk.StringVar(value="")
    ttk.Entry(cadre, textvariable=etat["description"], width=60).grid(row=6, column=1, sticky="we", padx=5)
    bouton_image = ttk.Button(cadre, text="Creer l'image", command=lambda: creer_image())
    bouton_image.grid(row=6, column=2)

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
        file_chat = CHAT_QUEUE

        def afficher(msg):
            historique.config(state="normal")
            historique.insert("end", msg + "\n")
            historique.see("end")
            historique.config(state="disabled")

        def ajouter_ailes(params, nom, log):
            ajouter_ailes_glb(os.path.join(SORTIES, nom, nom + ".glb"), nom, params["taille"],
                              params.get("longueur_ailes", 10.0), log)

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
                    code = lancer_generation(cmd, log)
                    if code == 0:
                        lancer_redressement(nom, nom, log)
                    if code == 0 and params.get("ailes_a_ajouter"):
                        ajouter_ailes(params, nom, log)
                    if code == 0:
                        ecrire_credits(nom, choisi)
                        log("Source et licence enregistrees dans sorties\\" + nom + "\\source.txt")
                    if code == 0:
                        apres_generation([os.path.join(SORTIES, nom, nom + ".glb")], nom, log, [nom])
                    log("=== Fini ===" if code == 0 else "=== ECHEC (code %d) : voir le journal ===" % code)
                except Exception as exc:
                    log("ERREUR : " + str(exc))

            threading.Thread(target=travail, daemon=True).start()

        def ouvrir_bibliotheque(params, chemin_deja=None):
            chemin = chemin_deja or filedialog.askopenfilename(
                parent=fenetre_chat, title="Choisir une image dans sorties",
                initialdir=SORTIES,
                filetypes=[("Images et modeles 3D", "*.png *.jpg *.jpeg *.webp *.bmp *.glb"), ("Tous", "*.*")])
            if not chemin:
                afficher("Assistant : aucune image choisie. Tapez 1, 2 ou 3.")
                return
            contexte["attente"] = None
            contexte["params"] = None
            afficher("Assistant : j'utilise l'image choisie : " + os.path.basename(chemin))
            lancer_depuis_fichier(chemin, params)

        def lancer_depuis_fichier(chemin, params):
            nom = params["nom"]

            def travail():
                try:
                    if os.path.splitext(chemin)[1].lower() == ".glb":
                        cible_glb = os.path.join(SORTIES, nom, nom + ".glb")
                        os.makedirs(os.path.dirname(cible_glb), exist_ok=True)
                        shutil.copyfile(chemin, cible_glb)
                        log("Modele 3D deja fait, copie : " + cible_glb)
                        if params.get("ailes_a_ajouter"):
                            ajouter_ailes(params, nom, log)
                        apres_generation([cible_glb], nom, log, [nom])
                        return
                    os.makedirs(os.path.join(IA, "images"), exist_ok=True)
                    ext = os.path.splitext(chemin)[1].lower() or ".png"
                    cible = os.path.join(IA, "images", nom + ext)
                    if os.path.abspath(chemin) != os.path.abspath(cible):
                        shutil.copyfile(chemin, cible)
                    cmd = [venv_python(), os.path.abspath(__file__), "--worker",
                           "--image", cible, "--nom", nom,
                           "--taille", str(params["taille"]), "--mc", "256"]
                    log("=== Generation de '" + nom + "' depuis ma bibliotheque ===")
                    code = lancer_generation(cmd, log)
                    if code == 0:
                        lancer_redressement(nom, nom, log)
                    if code == 0 and params.get("ailes_a_ajouter"):
                        ajouter_ailes(params, nom, log)
                    if code == 0:
                        apres_generation([os.path.join(SORTIES, nom, nom + ".glb")], nom, log, [nom])
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
            fermer_apercu()
            fen = tk.Toplevel(fenetre_chat)
            fen.title("Images libres proposees")
            contexte["fenetre_apercu"] = fen
            contexte["photos"] = []
            cadre_ap = ttk.Frame(fen, padding=8)
            cadre_ap.pack(fill="both", expand=True)
            nav = ttk.Frame(fen, padding=(8, 0, 8, 8))
            nav.pack(fill="x")
            bouton_prec = ttk.Button(nav, text="< Precedent")
            bouton_prec.pack(side="left")
            etiquette = ttk.Label(nav, text="")
            etiquette.pack(side="left", expand=True)
            bouton_suiv = ttk.Button(nav, text="Suivant >")
            bouton_suiv.pack(side="right")
            par_page = 5
            nb_pages = max(1, (len(res) + par_page - 1) // par_page)

            def remplir(p):
                p = min(max(p, 0), nb_pages - 1)
                contexte["page"] = p
                for w in cadre_ap.winfo_children():
                    w.destroy()
                contexte["photos"] = []
                for i in range(p * par_page, min(p * par_page + par_page, len(res))):
                    r, chemin = res[i], chemins[i]
                    col = ttk.Frame(cadre_ap, padding=4)
                    col.grid(row=0, column=i - p * par_page, padx=4, sticky="n")
                    if chemin:
                        photo = tk.PhotoImage(file=chemin)
                        contexte["photos"].append(photo)
                        tk.Label(col, image=photo).pack()
                    else:
                        tk.Label(col, text="(apercu indisponible)", width=24, height=8).pack()
                    tk.Label(col, text="%d. %s" % (i + 1, r["titre"][:40]), wraplength=190).pack()
                    tk.Label(col, text=r["licence"], fg="gray", wraplength=190).pack()
                    ttk.Button(col, text="Modeliser %d" % (i + 1),
                               command=lambda n=i + 1: action(n)).pack(pady=4)
                etiquette.config(text="Page %d / %d  (%d resultats)" % (p + 1, nb_pages, len(res)))
                bouton_prec.config(state="normal" if p > 0 else "disabled")
                bouton_suiv.config(state="normal" if p < nb_pages - 1 else "disabled")

            bouton_prec.config(command=lambda: remplir(contexte.get("page", 0) - 1))
            bouton_suiv.config(command=lambda: remplir(contexte.get("page", 0) + 1))
            contexte["aller_page"] = remplir
            contexte["nb_pages"] = nb_pages
            remplir(0)

        def fermer_apercu():
            fen = contexte.get("fenetre_apercu")
            if fen is not None:
                try:
                    fen.destroy()
                except tk.TclError:
                    pass

        def demander_piece():
            piece = contexte["pieces"][contexte["i"]]
            contexte["attente"] = "taille_piece"
            afficher("Assistant : pour « %s » : quelle taille reelle (cm, entre 5 et 100) ?" % piece["nom"])

        def demander_source(piece):
            contexte["attente"] = "source_piece"
            if "marteau" in piece["nom"]:
                afficher("Assistant : pour « %s » : 1 = fabrique par le generateur, 2 = recherche web, "
                         "3 = ma bibliotheque (dossier sorties ou un fichier)" % piece["nom"])
                if piece.get("ailes"):
                    afficher("  les ailes seront dessinees avec le marteau (generateur) ; "
                             "avec 2 ou 3, l'image doit deja contenir les ailes.")
            else:
                afficher("Assistant : pour « %s » : 1 = generer l'image (SDXL-Turbo, en local, quelques minutes), "
                         "2 = recherche web, 3 = ma bibliotheque ou mon fichier" % piece["nom"])

        def lancer_pieces(pieces):
            for piece in pieces:
                piece["requete"], piece["mots"] = requete_en(piece["nom"])
                piece["fichier"] = None
                piece["url"] = None
                piece["taille"] = None
                piece["source"] = None
            contexte["pieces"] = pieces
            contexte["i"] = 0
            afficher("Assistant : je vois %d piece(s) : %s. Je demande la source de chacune."
                     % (len(pieces), ", ".join(p["nom"] for p in pieces)))
            demander_piece()

        def piece_suivante():  # passe a la piece suivante, ou fabrique tout
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
                    res = chercher_images_libres(requete, 15, mots)
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

        def lire_cm(message, bas, haut):
            try:
                cm = float(message.replace(",", "."))
            except ValueError:
                return None
            return round(cm, 1) if bas <= cm <= haut else None

        def appliquer_source_piece(piece, choix):
            if choix == "3" and piece.get("fichier"):
                piece["source"] = 3
                piece_suivante()
                return
            if choix == "2":
                piece["source"] = 2
                contexte["attente"] = "attente_recherche"
                afficher("Assistant : je cherche « %s » sur Wikimedia Commons..." % piece["requete"])
                lancer_recherche_piece(piece["requete"], piece["mots"])
                return
            chemin = filedialog.askopenfilename(
                parent=fenetre_chat, title="Image ou modele 3D pour " + piece["nom"], initialdir=SORTIES,
                filetypes=[("Images et modeles 3D", "*.png *.jpg *.jpeg *.webp *.bmp *.glb"), ("Tous", "*.*")])
            if not chemin:
                contexte["attente"] = "source_piece"
                afficher("Assistant : aucun fichier choisi. Tapez 2 ou 3 pour reessayer.")
                return
            piece["source"] = 3
            piece["fichier"] = chemin
            afficher("Assistant : fichier de %s enregistre." % piece["nom"])
            piece_suivante()

        def traiter_piece(message):
            piece = contexte["pieces"][contexte["i"]]
            attente = contexte.get("attente")
            if attente == "taille_piece":
                cm = lire_cm(message, 5, 100)
                if cm is None:
                    afficher("Assistant : donne une taille entre 5 et 100 cm pour « %s »." % piece["nom"])
                    return
                piece["taille"] = cm
                demander_source(piece)
                return
            if attente == "taille_ailes_piece":
                cm = lire_cm(message, 3, 40)
                if cm is None:
                    afficher("Assistant : donne une taille entre 3 et 40 cm pour les ailes.")
                    return
                piece["longueur_ailes"] = cm
                if piece.get("ailes_a_ajouter"):
                    contexte["attente"] = None
                    appliquer_source_piece(piece, piece["hammer_choix"])
                    return
                piece_suivante()
                return
            if attente == "ailes_image_piece":
                if message == "3":
                    chemin = filedialog.askopenfilename(
                        parent=fenetre_chat, title="Image ou modele 3D pour " + piece["nom"], initialdir=SORTIES,
                        filetypes=[("Images et modeles 3D", "*.png *.jpg *.jpeg *.webp *.bmp *.glb"), ("Tous", "*.*")])
                    if not chemin:
                        afficher("Assistant : aucun fichier choisi. Tapez 1, 2 ou 3.")
                        return
                    piece["fichier"] = chemin
                    piece["source"] = 3
                    piece["source_choisie"] = "3"
                    afficher("Assistant : fichier de %s enregistre. L'image contient-elle deja les ailes ?"
                             % piece["nom"])
                    afficher("  1 = oui   2 = non, je les dessine avec le generateur")
                    afficher("  3 = prendre une autre image dans ma bibliotheque (dossier sorties)")
                    return
                if message == "1":
                    appliquer_source_piece(piece, piece.pop("source_choisie", "2"))
                    return
                if message == "2":
                    piece["ailes_a_ajouter"] = True
                    piece["hammer_choix"] = piece.pop("source_choisie", "2")
                    contexte["attente"] = "taille_ailes_piece"
                    afficher("Assistant : les ailes de « %s » seront ajoutees a l'image. "
                             "Quelle taille (cm, entre 3 et 40) ?" % piece["nom"])
                    return
                afficher("Assistant : tapez 1 (oui) ou 2 (non).")
                return
            if attente == "source_piece":
                if message == "1" and "marteau" not in piece["nom"]:
                    piece["source"] = "gen"
                    piece["fichier"] = None
                    afficher("Assistant : je creerai l'image de « %s » avec SDXL-Turbo pendant la fabrication." % piece["nom"])
                    piece_suivante()
                    return
                if message == "1":
                    piece["source"] = 1
                    if piece.get("ailes"):
                        contexte["attente"] = "taille_ailes_piece"
                        afficher("Assistant : quelle taille pour les ailes de « %s » (cm, entre 3 et 40) ?"
                                 % piece["nom"])
                        return
                    piece_suivante()
                    return
                if message in ("2", "3") and piece.get("ailes"):
                    piece["source_choisie"] = message
                    contexte["attente"] = "ailes_image_piece"
                    afficher("Assistant : l'image de « %s » contient-elle deja les ailes ?" % piece["nom"])
                    afficher("  1 = oui   2 = non, je les dessine avec le generateur")
                    afficher("  3 = prendre une autre image dans ma bibliotheque (dossier sorties)")
                    return
                if message in ("2", "3"):
                    appliquer_source_piece(piece, message)
                    return
                afficher("Assistant : tapez 1, 2 ou 3.")
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
                        if piece.get("source") == 1:
                            cmd = [venv_python(), os.path.abspath(__file__), "--worker", "--parametrique",
                                   "--nom", nom, "--taille", str(piece["taille"]), "--mc", "256"]
                            if piece.get("ailes"):
                                cmd += ["--ailes", "--longueur-ailes", str(piece.get("longueur_ailes", 10.0))]
                            log("=== Piece %d/%d : %s (generateur) ===" % (k, len(pieces), piece["nom"]))
                            if executer_worker(cmd, log) != 0:
                                log("=== ECHEC sur la piece %s : voir le journal ===" % piece["nom"])
                                return
                            entrees.append({"fichier": os.path.join(SORTIES, nom, nom + ".glb"),
                                            "taille": None if piece["relation"] == "sur" and k > 1
                                            else piece["taille"],
                                            "relation": piece["relation"]})
                            continue
                        if piece.get("source") == "gen":
                            sortie_gen = os.path.join(IA, "images", nom + "_ia.png")
                            cmd_gen = [venv_python(), os.path.abspath(__file__), "--worker", "--generer-image",
                                       "--prompt", "a single " + piece["requete"] + " object",
                                       "--sortie-image", sortie_gen, "--nom", nom, "--taille", "1"]
                            log("=== Piece %d/%d : %s (image creee par SDXL-Turbo) ===" % (k, len(pieces), piece["nom"]))
                            if executer_worker(cmd_gen, log) != 0 or not os.path.isfile(sortie_gen):
                                log("=== ECHEC de la creation de l'image pour %s : voir le journal ===" % piece["nom"])
                                return
                            piece["fichier"] = sortie_gen
                        if piece.get("fichier") and piece["fichier"].lower().endswith(".glb"):
                            cible = os.path.join(SORTIES, nom, nom + ".glb")
                            os.makedirs(os.path.dirname(cible), exist_ok=True)
                            shutil.copyfile(piece["fichier"], cible)
                            log("=== Piece %d/%d : %s (modele 3D deja fait) ===" % (k, len(pieces), piece["nom"]))
                            lancer_redressement(nom, piece["nom"], log)
                            if piece.get("ailes_a_ajouter"):
                                ajouter_ailes(piece, nom, log)
                            entrees.append({"fichier": cible,
                                            "taille": None if piece["relation"] == "sur" and k > 1
                                            else piece["taille"],
                                            "relation": piece["relation"]})
                            continue
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
                               "--taille", str(piece["taille"]), "--mc", "256"]
                        while True:
                            recadrage = lancer_cadrage(chemin, piece["nom"], log)
                            cmd_detour = [venv_python(), os.path.abspath(__file__), "--worker",
                                          "--detourer", "--image", chemin, "--nom", nom, "--taille", "0"]
                            if recadrage:
                                cmd_detour += ["--recadrage", recadrage]
                            log("=== Detourage de '%s' (apercu) ===" % piece["nom"])
                            if executer_worker(cmd_detour, log) != 0:
                                log("=== ECHEC du detourage : voir le journal ===")
                                return
                            retour = lancer_validation(os.path.join(SORTIES, nom, "detourage.png"),
                                                       piece["nom"], log)
                            if retour is None:
                                log("Page fermee sans reponse : fabrication annulee.")
                                return
                            valide, png_edite = retour
                            if valide:
                                if png_edite:
                                    chemin = os.path.join(IA, "images", nom + "_edite.png")
                                    with open(chemin, "wb") as f:
                                        f.write(png_edite)
                                    log("Retouches du detourage enregistrees.")
                                else:
                                    chemin = os.path.join(SORTIES, nom, "detourage.png")
                                break
                            log("Recadrage a refaire pour '%s'." % piece["nom"])
                        cmd = [venv_python(), os.path.abspath(__file__), "--worker",
                               "--image", chemin, "--nom", nom,
                               "--taille", str(piece["taille"]), "--mc", "256",
                               "--deja-detoure"]
                        if lancer_generation(cmd, log) != 0:
                            log("=== ECHEC sur la piece %s : voir le journal ===" % piece["nom"])
                            return
                        lancer_redressement(nom, piece["nom"], log)
                        if piece.get("ailes_a_ajouter"):
                            ajouter_ailes(piece, nom, log)
                        if piece.get("url"):
                            ecrire_credits(nom, piece)
                        entrees.append({"fichier": os.path.join(SORTIES, nom, nom + ".glb"),
                                        "taille": None if piece["relation"] == "sur" and k > 1
                                        else piece["taille"],
                                        "relation": piece["relation"]})
                    if entrees:
                        apres_generation([e["fichier"] for e in entrees], base, log,
                                         noms=[pc["nom"] for pc in pieces])
                        return
                    for k in range(1, len(pieces)):
                        if pieces[k]["relation"] != "sur":
                            continue
                        img_support = os.path.join(os.path.dirname(entrees[k - 1]["fichier"]), "photo_traitee.png")
                        img_piece = os.path.join(os.path.dirname(entrees[k]["fichier"]), "photo_traitee.png")
                        if not os.path.isfile(img_support) or not os.path.isfile(img_piece):
                            log("Pas de photo traitee pour placer '%s' : posee au-dessus par defaut." % pieces[k]["nom"])
                            continue
                        log("=== Placement de '%s' sur '%s' : glissez l'image dans le navigateur ==="
                            % (pieces[k]["nom"], pieces[k - 1]["nom"]))
                        place = lancer_placement(img_support, img_piece, pieces[k - 1]["nom"],
                                                 pieces[k]["nom"], log)
                        if place is None:
                            log("Pas de placement enregistre : posee au-dessus par defaut.")
                        else:
                            entrees[k]["ancrage"] = [place["u"], place["v"], place["facteur"],
                                                     place["rotation"]]
                            log("Placement enregistre.")
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
            mot = message.lower().strip()
            if contexte.get("aller_page") and mot in ("suivant", "suivante", "plus", "next"):
                contexte["aller_page"](contexte.get("page", 0) + 1)
                afficher("Assistant : page suivante des images.")
                return
            if contexte.get("aller_page") and mot in ("precedent", "précédent", "avant"):
                contexte["aller_page"](contexte.get("page", 0) - 1)
                afficher("Assistant : page precedente des images.")
                return
            if contexte.get("pieces"):
                traiter_piece(message)
                return
            if contexte["choix"] and message.isdigit() and 1 <= int(message) <= len(contexte["choix"]):
                choisir(int(message))
                return
            if contexte.get("attente") == "source_marteau" and message in ("1", "2", "3"):
                params = contexte["params"]
                if message in ("2", "3") and params["ailes"]:
                    params["source_choisie"] = message
                    contexte["attente"] = "ailes_image"
                    afficher("Assistant : l'image contient-elle deja les ailes ?")
                    afficher("  1 = oui, elle a deja les ailes   2 = non, je dessine les ailes avec le generateur")
                    afficher("  3 = prendre une autre image dans ma bibliotheque (dossier sorties)")
                    return
                if message == "2":
                    contexte["attente"] = None
                    contexte["params"] = None
                    afficher("Assistant : je cherche des images libres de droits (Wikimedia Commons)...")
                    lancer_recherche(params)
                    return
                if message == "3":
                    ouvrir_bibliotheque(params, params.get("fichier_choisi"))
                    return
                if params["ailes"]:
                    contexte["attente"] = "taille_ailes"
                    afficher("Assistant : quelle taille pour les ailes (en cm, entre 3 et 40, ex. 10) ?")
                    return
                contexte["attente"] = None
                contexte["params"] = None
                afficher("Assistant : je lance la creation d'un marteau de %s cm." % params["taille"])
                lancer_parametrique(params["nom"], params["taille"])
                return
            if contexte.get("attente") == "taille_ailes":
                try:
                    cm = float(message.replace(",", "."))
                except ValueError:
                    cm = None
                if cm is None or not 3 <= cm <= 40:
                    afficher("Assistant : donne une taille entre 3 et 40 cm pour les ailes (ex. 10).")
                    return
                params = contexte["params"]
                params["longueur_ailes"] = cm
                if params.get("ailes_a_ajouter"):
                    contexte["attente"] = None
                    if params.get("hammer_choix") == "2":
                        afficher("Assistant : je cherche une image libre de marteau...")
                        lancer_recherche(params)
                    else:
                        contexte["attente"] = "source_marteau"
                        ouvrir_bibliotheque(params, params.get("fichier_choisi"))
                    return
                contexte["attente"] = "place"
                afficher("Assistant : ou placer les ailes sur le manche ?")
                afficher("  1 = milieu du manche   2 = pres du pommeau   3 = pres de la tete")
                return
            if contexte.get("attente") == "source" and message in ("2", "3"):
                afficher("Assistant : les ailes d'une image ne peuvent pas etre combinees avec un marteau "
                         "fabrique par le generateur. Tapez 1.")
                return
            if contexte.get("attente") == "source" and message in ("1", "2", "3"):
                params = contexte["params"]
                if message == "3":
                    ouvrir_bibliotheque(params, params.get("fichier_choisi"))
                    return
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
                lancer_parametrique(params["nom"], params["taille"], position, params.get("longueur_ailes"))
                return
            if contexte.get("attente") == "ailes_image" and message == "3":
                params = contexte["params"]
                params["source_choisie"] = "3"
                chemin = filedialog.askopenfilename(
                    parent=fenetre_chat, title="Choisir l'image du marteau", initialdir=SORTIES,
                    filetypes=[("Images et modeles 3D", "*.png *.jpg *.jpeg *.webp *.bmp *.glb"), ("Tous", "*.*")])
                if not chemin:
                    afficher("Assistant : aucune image choisie. Tapez 1 ou 2.")
                    return
                params["fichier_choisi"] = chemin
                afficher("Assistant : image choisie : " + os.path.basename(chemin))
                afficher("Assistant : l'image contient-elle deja les ailes ?")
                afficher("  1 = oui, elle a deja les ailes   2 = non, je dessine les ailes avec le generateur")
                afficher("  3 = prendre une autre image dans ma bibliotheque (dossier sorties)")
                return
            if contexte.get("attente") == "ailes_image" and message in ("1", "2"):
                params = contexte["params"]
                choix = params.pop("source_choisie", "2")
                if message == "2":
                    params["ailes_a_ajouter"] = True
                    params["hammer_choix"] = choix
                    contexte["attente"] = "taille_ailes"
                    afficher("Assistant : les ailes seront ajoutees au marteau de l'image. Quelle taille pour les ailes "
                             "(cm, entre 3 et 40, ex. 10) ?")
                    return
                if choix == "2":
                    contexte["attente"] = None
                    contexte["params"] = None
                    afficher("Assistant : je cherche des images libres de droits (Wikimedia Commons)...")
                    lancer_recherche(params)
                    return
                contexte["attente"] = "source_marteau"
                ouvrir_bibliotheque(params, params.get("fichier_choisi"))
                return
            if contexte.get("attente") == "ailes_image":
                afficher("Assistant : tapez 1 (oui) ou 2 (non).")
                return
            if contexte.get("attente") in ("source", "place", "source_marteau", "taille_ailes"):
                contexte["attente"] = None
                contexte["params"] = None
            if not contexte.get("attente"):
                pieces = fusionner_ailes(decouper_demande(message))
                if pieces and not (len(pieces) == 1 and "marteau" in pieces[0]["nom"]):
                    lancer_pieces(pieces)
                    return
            contexte["choix"] = None
            contexte["texte"] += " " + message
            params, questions = analyser_demande(contexte["texte"])
            if questions:
                for q in questions:
                    afficher("Assistant : " + q)
                return
            contexte["texte"] = ""
            contexte["params"] = params
            contexte["attente"] = "source_marteau"
            afficher("Assistant : pour le marteau, que voulez-vous ?")
            afficher("  1 = fabrique par le generateur")
            afficher("  2 = chercher une image libre sur le web (resultats incertains)")
            afficher("  3 = prendre une image dans ma bibliotheque (dossier sorties)")
            return

        def verifier_chat():
            try:
                while True:
                    item = file_chat.get_nowait()
                    if item[0] == "msg":
                        afficher("Assistant : " + item[1])
                    elif item[0] == "image":
                        afficher("Assistant : " + item[2])
                        try:
                            photo = tk.PhotoImage(file=item[1])
                            contexte.setdefault("photos", []).append(photo)
                            historique.config(state="normal")
                            historique.image_create("end", image=photo)
                            historique.insert("end", "\n")
                            historique.see("end")
                            historique.config(state="disabled")
                        except tk.TclError as exc:
                            afficher("Apercu impossible a afficher : " + str(exc))
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
                                 "dans la fenetre des images, tapez le numero ici, ou « suivant » / « precedent » :")
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

    def lancer_parametrique(nom, taille, position_ailes=None, longueur_ailes=None):
        cmd = [venv_python(), os.path.abspath(__file__), "--worker", "--parametrique",
               "--nom", nom, "--taille", str(taille), "--mc", "256"]
        if position_ailes is not None:
            cmd += ["--ailes", "--position-ailes", str(position_ailes)]
            if longueur_ailes:
                cmd += ["--longueur-ailes", str(longueur_ailes)]

        def travail():
            log("=== Generation parametrique de '" + nom + "' ===")
            proc = subprocess.Popen(cmd, env=env_ia(), stdout=subprocess.PIPE,
                                    stderr=subprocess.STDOUT, text=True,
                                    encoding="utf-8", errors="replace")
            for ligne in proc.stdout:
                log(ligne.rstrip())
            code = proc.wait()
            if code == 0:
                apres_generation([os.path.join(SORTIES, nom, nom + ".glb")], nom, log, [nom])
            log("=== Fini ===" if code == 0 else "=== ECHEC (code %d) : voir le journal ===" % code)

        threading.Thread(target=travail, daemon=True).start()

    ttk.Button(cadre, text="Assistant (discussion)...", command=ouvrir_assistant).grid(
        row=5, column=0, sticky="w", pady=8)

    ttk.Button(cadre, text="Ouvrir le dossier des sorties",
               command=lambda: ouvrir_sorties()).grid(row=5, column=2, sticky="e")

    def ouvrir_edition_3d():
        import json
        from tkinter import simpledialog
        chemins = filedialog.askopenfilenames(parent=fenetre, title="Pieces 3D a placer",
                                              initialdir=SORTIES,
                                              filetypes=[("Modeles 3D", "*.glb"), ("Tous", "*.*")])
        if not chemins:
            return
        nom = simpledialog.askstring("Edition 3D", "Nom du modele final (sans espace) :",
                                     parent=fenetre, initialvalue="scene")
        if not nom or " " in nom.strip():
            log("Edition 3D annulee : nom vide ou avec espace.")
            return
        nom = nom.strip()

        def travail():
            lancer_edition(chemins, nom, log)

        threading.Thread(target=travail, daemon=True).start()

    ttk.Button(cadre, text="Edition 3D : placer les pieces...",
               command=ouvrir_edition_3d).grid(row=7, column=0, sticky="w", pady=(6, 0))

    def voir_detourage():
        # Ouvre la page de retouche du detourage sur le dernier detourage.png trouve,
        # sans relancer de generation.
        candidats = []
        if os.path.isdir(SORTIES):
            for racine, _dirs, fichiers in os.walk(SORTIES):
                if "detourage.png" in fichiers:
                    chemin = os.path.join(racine, "detourage.png")
                    candidats.append((os.path.getmtime(chemin), chemin))
        if candidats:
            chemin = max(candidats)[1]
        else:
            chemin = filedialog.askopenfilename(
                parent=fenetre, title="Choisir un detourage a voir",
                initialdir=SORTIES,
                filetypes=[("Images", "*.png"), ("Tous", "*.*")])
            if not chemin:
                return
        nom_piece = os.path.splitext(os.path.basename(chemin))[0]
        log("Apercu du detourage ouvert : " + chemin)

        def travail():
            retour = lancer_validation(chemin, os.path.basename(os.path.dirname(chemin)) or nom_piece, log, delai=900)
            if retour is None:
                log("Page du detourage fermee sans validation.")
            else:
                log("Page du detourage fermee (validation : %s)." % ("oui" if retour[0] else "non"))

        threading.Thread(target=travail, daemon=True).start()

    ttk.Button(cadre, text="Voir le detourage...",
               command=voir_detourage).grid(row=7, column=2, sticky="e", pady=(6, 0))

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
            code = lancer_generation(cmd, log)
            if code == 0:
                apres_generation([os.path.join(SORTIES, nom, nom + ".glb")], nom, log, [nom],
                                 dans_chat=False)
            log("=== Fini ===" if code == 0 else "=== ECHEC (code %d) : voir le journal ===" % code)
            file_journal.put(("FIN", code))

        threading.Thread(target=travail, daemon=True).start()

    def creer_image():
        description = etat["description"].get().strip()
        if not description:
            messagebox.showerror("Description", "Ecris ce que tu veux (ex. un rocher avec un avion dessus).")
            return
        nom = etat["nom"].get().strip() or "objet"
        if any(c in nom for c in '\\/:*?"<>| '):
            messagebox.showerror("Nom", "Nom sans espace ni caractere speciaux (ex. marteau).")
            return
        sortie = os.path.join(IA, "images", nom + "_ia.png")
        bouton_image.config(state="disabled")
        cmd = [venv_python(), os.path.abspath(__file__), "--worker", "--generer-image",
               "--prompt", description, "--sortie-image", sortie, "--nom", nom, "--taille", "1"]

        def travail():
            log("=== Creation de l'image : " + description + " ===")
            proc = subprocess.Popen(cmd, env=env_ia(), stdout=subprocess.PIPE,
                                    stderr=subprocess.STDOUT, text=True,
                                    encoding="utf-8", errors="replace")
            for ligne in proc.stdout:
                log(ligne.rstrip())
            code = proc.wait()
            if code == 0 and os.path.isfile(sortie):
                log("=== Image prete : elle est mise dans le champ Photo ===")
                file_journal.put(("PHOTO", sortie))
            else:
                log("=== ECHEC de la creation d'image (code %d) : voir le journal ===" % code)
            file_journal.put(("FIN_IMAGE", None))

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
                    elif item[0] == "PHOTO":
                        etat["photo"].set(item[1])
                    elif item[0] == "FIN_IMAGE":
                        bouton_image.config(state="normal")
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
    if "--limiteur-pid" in sys.argv:
        parent = None
        if "--limiteur-parent" in sys.argv:
            parent = int(sys.argv[sys.argv.index("--limiteur-parent") + 1])
        controler_cpu(int(sys.argv[sys.argv.index("--limiteur-pid") + 1]), parent)
        return
    if "--worker" in sys.argv:
        parseur = argparse.ArgumentParser()
        parseur.add_argument("--worker", action="store_true")
        parseur.add_argument("--image", default="")
        parseur.add_argument("--parametrique", action="store_true")
        parseur.add_argument("--ailes", action="store_true")
        parseur.add_argument("--assembler", action="store_true")
        parseur.add_argument("--entrees", default="[]")
        parseur.add_argument("--recadrage", default="")
        parseur.add_argument("--detourer", action="store_true")
        parseur.add_argument("--deja-detoure", action="store_true")
        parseur.add_argument("--position-ailes", type=float, default=0.5)
        parseur.add_argument("--longueur-ailes", type=float, default=10.0)
        parseur.add_argument("--nom", required=True)
        parseur.add_argument("--taille", type=float, required=True)
        parseur.add_argument("--mc", type=int, default=256)
        parseur.add_argument("--garder-fond", action="store_true")
        parseur.add_argument("--generer-image", action="store_true")
        parseur.add_argument("--ajouter-ailes", action="store_true")
        parseur.add_argument("--editer-3d", action="store_true")
        parseur.add_argument("--apercu-3d", action="store_true")
        parseur.add_argument("--decouper-3d", action="store_true")
        parseur.add_argument("--entrees-fichier", default="")
        parseur.add_argument("--prompt", default="")
        parseur.add_argument("--sortie-image", default="")
        parseur.add_argument("--redresser", action="store_true")
        parseur.add_argument("--moteur", default="triposr")
        parseur.add_argument("--apercu", action="store_true")
        parseur.add_argument("--tx", type=float, default=0.0)
        parseur.add_argument("--tz", type=float, default=0.0)
        args = parseur.parse_args()
        args.retirer_fond = not args.garder_fond
        if args.redresser:
            sys.exit(redresser_fichier(args))
        if args.moteur == "sf3d":
            generer_sf3d(args)
            return
        if args.decouper_3d:
            decouper_fichier(args)
            return
        if args.apercu_3d:
            apercu_3d(args)
            return
        if args.editer_3d:
            editer_3d(args)
            return
        if args.ajouter_ailes:
            ajouter_ailes_fichier(args)
            return
        if args.generer_image:
            generer_image_ia(args)
            return
        if args.detourer:
            generer_detourage(args)
            return
        if args.assembler:
            assembler(args)
            return
        generer(args)
        return
    interface()


if __name__ == "__main__":
    main()
