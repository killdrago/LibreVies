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
    test = "import torch, torchvision, trimesh, rembg, skimage, transformers, einops, omegaconf, PIL"
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

def generer(args):
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
    mesh.export(os.path.join(dossier, args.nom + ".glb"))
    mesh.export(os.path.join(dossier, args.nom + ".obj"))
    image.save(os.path.join(dossier, "photo_traitee.png"))

    print("Fichiers crees dans : " + dossier, flush=True)
    print("  %d sommets, %d faces" % (len(mesh.vertices), len(mesh.faces)), flush=True)

    if os.path.isdir(os.path.join(ROOT, "unity")):
        cible = os.path.join(UNITY_ITEMS, args.nom)
        os.makedirs(cible, exist_ok=True)
        shutil.copy2(os.path.join(dossier, args.nom + ".obj"), os.path.join(cible, args.nom + ".obj"))
        print("Copie pour Unity : Assets/Resources/Items/" + args.nom + "/" + args.nom + ".obj", flush=True)
    print("Termine en %d secondes." % int(time.time() - debut), flush=True)


# ---------------------------------------------------------------------------
# Interface graphique
# ---------------------------------------------------------------------------

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

    threading.Thread(target=installation, daemon=True).start()
    fenetre.after(150, poll)
    fenetre.mainloop()


def main():
    if "--worker" in sys.argv:
        parseur = argparse.ArgumentParser()
        parseur.add_argument("--worker", action="store_true")
        parseur.add_argument("--image", required=True)
        parseur.add_argument("--nom", required=True)
        parseur.add_argument("--taille", type=float, required=True)
        parseur.add_argument("--mc", type=int, default=256)
        parseur.add_argument("--garder-fond", action="store_true")
        args = parseur.parse_args()
        args.retirer_fond = not args.garder_fond
        generer(args)
        return
    interface()


if __name__ == "__main__":
    main()
