"""
LibreVies — Launcher avec auto-update intégré
Double-clique sur launcher.pyw ou LibreVies.exe pour lancer.
"""
import tkinter as tk
from tkinter import ttk, messagebox
import subprocess, threading, os, sys, time, zipfile
import urllib.request, hashlib, json, shutil

GAME_DIR = os.path.dirname(os.path.abspath(__file__))
GODOT_URL = "https://github.com/godotengine/godot/releases/download/4.4.1-stable/Godot_v4.4.1-stable_win64.exe.zip"
BANNER = os.path.join(GAME_DIR, "banniere_v1.png")
CONFIG_PATH = os.path.join(GAME_DIR, "version_url.json")

LAUNCHER_VERSION = "2.2.0"
GAME_VERSION = "0.2.0"

BG = "#1a1a2e"; BG2 = "#222244"; CARD = "#2a2a50"
ACCENT = "#f1c40f"; TEXT = "#ffffff"; TEXT2 = "#aabbcc"
GREEN = "#27ae60"; RED = "#e74c3c"; BLUE = "#3498db"

NEWS = [
    {"date": "31/07/2026", "t": "Auto-Update v2.0",
     "d": "Le launcher verifie les mises a jour et affiche les fichiers modifies. Cliquez sur un fichier texte pour le visualiser."},
    {"date": "31/07/2026", "t": "Version bump integre",
     "d": "Le launcher gere les versions directement. Plus besoin de script separe pour mettre a jour."},
    {"date": "28/07/2026", "t": "Version 0.1 — Premier lancement",
     "d": "Ville de depart, personnage voxel, ennemis, combat et argent. Le jeu fonctionne avec des formes CSG de base."},
    {"date": "28/07/2026", "t": "Personnage voxel anime",
     "d": "19 parties separees generees par Python, animation marche avec bras et jambes, marteau qui suit le bras droit."},
]


# ============================================================
# OUTILS
# ============================================================

def file_hash(path):
    try:
        with open(path, 'rb') as f:
            return hashlib.md5(f.read()).hexdigest()
    except:
        return None

def load_local_config():
    if os.path.exists(CONFIG_PATH):
        try:
            with open(CONFIG_PATH, 'r') as f:
                return json.load(f)
        except:
            pass
    return {"launcher_version": LAUNCHER_VERSION, "game_version": GAME_VERSION,
            "notes": "", "game_url": "", "raw_url": "", "files": {}}

def save_local_config(cfg):
    with open(CONFIG_PATH, 'w') as f:
        json.dump(cfg, f, indent=2)

def is_text_file(fname):
    text_exts = ('.gd', '.tscn', '.godot', '.pyw', '.py', '.bat', '.json',
                 '.cfg', '.txt', '.md', '.html', '.css', '.js', '.csv')
    return fname.lower().endswith(text_exts)

def get_remote_url(fname, raw_url):
    return f"{raw_url}/{urllib.request.quote(fname)}"


# ============================================================
# VERIFICATION DES MISES A JOUR
# ============================================================

def check_for_updates(progress_cb):
    local_cfg = load_local_config()
    raw_url = local_cfg.get("raw_url", "")
    if not raw_url:
        return {"error": "Pas d'URL configuree", "modified": [],
                "remote_cfg": None, "launcher_needs_update": False}
    try:
        progress_cb(5, "Connexion a GitHub...")
        url = get_remote_url("version_url.json", raw_url)
        req = urllib.request.Request(url, headers={"User-Agent": f"LibreVies/{LAUNCHER_VERSION}"})
        resp = urllib.request.urlopen(req, timeout=15)
        remote_cfg = json.loads(resp.read().decode())
    except Exception as e:
        return {"error": f"Pas de connexion: {e}", "modified": [],
                "remote_cfg": None, "launcher_needs_update": False}

    remote_files = remote_cfg.get("files", {})
    modified = []
    for fname, info in remote_files.items():
        local_h = file_hash(os.path.join(GAME_DIR, fname))
        if local_h is None:
            action = "NOUVEAU"
        elif local_h != info["hash"]:
            action = "MODIFIE"
        else:
            continue
        modified.append((fname, local_h, info["hash"], action))

    launcher_needs_update = any(f == "launcher.pyw" for f, _, _, _ in modified)
    return {"error": None, "modified": modified, "remote_cfg": remote_cfg,
            "launcher_needs_update": launcher_needs_update}


def apply_updates(modified, remote_cfg, progress_cb):
    raw_url = remote_cfg.get("raw_url", "")
    errors = []; downloaded = 0; total = len(modified)
    for i, (fname, _, _, _) in enumerate(modified):
        pct = int(20 + (i / total) * 70)
        progress_cb(pct, f"Telechargement ({i+1}/{total}): {fname}")
        try:
            local_path = os.path.join(GAME_DIR, fname)
            os.makedirs(os.path.dirname(local_path), exist_ok=True)
            req = urllib.request.Request(get_remote_url(fname, raw_url),
                                         headers={"User-Agent": f"LibreVies/{LAUNCHER_VERSION}"})
            resp = urllib.request.urlopen(req, timeout=30)
            tmp = local_path + ".tmp"
            with open(tmp, 'wb') as f:
                f.write(resp.read())
            if os.path.exists(local_path):
                os.remove(local_path)
            os.rename(tmp, local_path)
            downloaded += 1
        except Exception as e:
            errors.append(f"{fname}: {e}")
        time.sleep(0.05)
    save_local_config(remote_cfg)
    return downloaded, errors


def update_launcher_exe():
    download_file_remote("launcher.pyw")
    exe_path = os.path.abspath(sys.executable if getattr(sys, 'frozen', False) else sys.argv[0])
    exe_dir = os.path.dirname(exe_path)
    exe_name = os.path.basename(exe_path)
    new_exe = os.path.join(exe_dir, f"{exe_name}.new")
    old_exe = os.path.join(exe_dir, f"{exe_name}.old")
    bat_path = os.path.join(exe_dir, "_update.bat")
    pid = os.getpid()

    local_cfg = load_local_config()
    repo_url = local_cfg.get("game_url", "").replace("/tree/main", "")
    if not repo_url:
        repo_url = ""
    try:
        req = urllib.request.Request(f"{repo_url}/releases/latest/download/LibreVies.exe",
                                     headers={"User-Agent": "LibreVies"})
        resp = urllib.request.urlopen(req, timeout=60)
        with open(new_exe, 'wb') as f:
            f.write(resp.read())
    except:
        try:
            subprocess.run([sys.executable, "-m", "PyInstaller",
                            "--onefile", "--noconsole", "--name", "LibreVies",
                            "--distpath", exe_dir,
                            "--workpath", os.path.join(exe_dir, "_build"),
                            "--specpath", exe_dir,
                            os.path.join(GAME_DIR, "launcher.pyw")],
                           capture_output=True, timeout=120)
            built = os.path.join(exe_dir, "LibreVies.exe")
            if os.path.exists(built) and built != exe_path:
                shutil.copy2(built, new_exe); os.remove(built)
        except:
            pass
    if not os.path.exists(new_exe):
        return False
    with open(bat_path, 'w', encoding='utf-8') as f:
        f.write(f"""@echo off
:wait_loop
tasklist /FI "PID eq {pid}" 2>nul | find "{pid}" >nul
if not errorlevel 1 ( timeout /t 1 /nobreak >nul & goto wait_loop )
if exist "{old_exe}" del /f /q "{old_exe}"
if exist "{exe_path}" ren "{exe_name}" "{os.path.basename(old_exe)}"
if exist "{new_exe}" ren "{os.path.basename(new_exe)}" "{exe_name}"
if exist "{exe_path}" ( start "" "{exe_path}" ) else (
    if exist "{old_exe}" ren "{os.path.basename(old_exe)}" "{exe_name}" & pause )
del /f /q "%~f0"
""")
    subprocess.Popen(["cmd", "/c", "start", "", bat_path], cwd=exe_dir,
                     creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    sys.exit(0)


def download_file_remote(fname):
    local_cfg = load_local_config()
    raw_url = local_cfg.get("raw_url", "")
    local_path = os.path.join(GAME_DIR, fname)
    os.makedirs(os.path.dirname(local_path), exist_ok=True)
    req = urllib.request.Request(get_remote_url(fname, raw_url),
                                 headers={"User-Agent": f"LibreVies/{LAUNCHER_VERSION}"})
    resp = urllib.request.urlopen(req, timeout=30)
    with open(local_path, 'wb') as f:
        f.write(resp.read())


# ============================================================
# GODOT
# ============================================================

def find_godot():
    for d in [os.path.join(GAME_DIR, "tools"), GAME_DIR]:
        if os.path.isdir(d):
            for f in os.listdir(d):
                if f.lower().startswith("godot") and f.endswith(".exe"):
                    return os.path.join(d, f)
    return None

def download_godot(cb, done):
    tools = os.path.join(GAME_DIR, "tools"); os.makedirs(tools, exist_ok=True)
    zp = os.path.join(tools, "godot.zip")
    try:
        cb(0, "Telechargement de Godot...")
        r = urllib.request.urlopen(urllib.request.Request(GODOT_URL, headers={"User-Agent": "LibreVies"}), timeout=180)
        total = int(r.headers.get('content-length', 0)); dl = 0
        with open(zp, 'wb') as f:
            while True:
                ch = r.read(8192)
                if not ch: break
                f.write(ch); dl += len(ch)
                if total > 0: cb(int(dl / total * 50), f"Telechargement... {int(dl / total * 100)}%")
        cb(50, "Extraction...")
        with zipfile.ZipFile(zp) as z: z.extractall(tools)
        os.remove(zp); cb(100, "Godot installe !"); done(find_godot())
    except Exception as e:
        cb(0, f"Erreur : {e}"); done(None)

def preload(path, cb, done):
    try:
        cb(5, "Nettoyage du cache Godot...")
        godot_cache = os.path.join(GAME_DIR, ".godot")
        if os.path.isdir(godot_cache):
            import shutil as _shutil; _shutil.rmtree(godot_cache, ignore_errors=True); time.sleep(0.5)
        cb(10, "Import des assets (peut prendre 1-2 min)...")
        p = subprocess.Popen([path, "--import", "--headless", "--path", GAME_DIR],
                             stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        p.wait(timeout=120)
        if not os.path.isdir(godot_cache):
            cb(70, "Deuxieme tentative d'import...")
            p2 = subprocess.Popen([path, "--import", "--headless", "--path", GAME_DIR],
                                  stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            p2.wait(timeout=120)
        cb(90, "Finalisation..."); time.sleep(1)
        cb(100, "Pret !"); done(True)
    except Exception as e:
        cb(100, f"Pret ({e})"); done(True)

def launch(path):
    subprocess.Popen([path, "--path", GAME_DIR])


# ============================================================
# APPLICATION
# ============================================================

class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("LibreVies")
        self.geometry("1024x768")
        self.resizable(False, False)
        self.configure(bg=BG)

        icon_path = os.path.join(GAME_DIR, "pp_lv_3.png")
        if os.path.exists(icon_path):
            try:
                from PIL import Image, ImageTk
                self.iconphoto(True, ImageTk.PhotoImage(Image.open(icon_path).resize((32, 32))))
            except:
                pass

        self.godot = None
        self.ready = False
        self.update_result = None
        self.build()
        self._load_saved_login()
        self.start_update()

    def frame(self, x1, y1, x2, y2, color=CARD, outline=ACCENT):
        self.canvas.create_rectangle(x1, y1, x2, y2, fill=color, outline=outline, width=2)

    def build(self):
        self.canvas = tk.Canvas(self, width=1024, height=768, highlightthickness=0)
        self.canvas.pack(fill="both", expand=True)

        # ====== FOND ======
        bg_loaded = False
        if os.path.exists(BANNER):
            try:
                from PIL import Image, ImageTk
                self.bg_img = ImageTk.PhotoImage(Image.open(BANNER).resize((1024, 768), Image.LANCZOS))
                self.canvas.create_image(0, 0, image=self.bg_img, anchor="nw")
                bg_loaded = True
            except:
                try:
                    self._bg_photo = tk.PhotoImage(file=BANNER)
                    self.canvas.create_image(0, 0, image=self._bg_photo, anchor="nw")
                    bg_loaded = True
                except:
                    pass
        if not bg_loaded:
            self.canvas.configure(bg=BG)
        self.canvas.create_rectangle(0, 0, 1024, 768, fill="black", stipple="gray25")

        # ====== LIGNE DU HAUT ======

        # --- LIBREVIES (gauche) ---
        self.canvas.create_rectangle(20, 15, 250, 100, fill=CARD, outline=ACCENT, width=2)
        self.canvas.create_text(135, 22, text="LibreVies", font=("Segoe UI", 30, "bold"),
                                fill=ACCENT, anchor="n")
        self.canvas.create_text(135, 65, text="MMO Open World",
                                font=("Segoe UI", 11), fill=TEXT2, anchor="n")
        self.canvas.create_text(135, 82, text=f"v{GAME_VERSION}",
                                font=("Segoe UI", 10), fill="#667788", anchor="n")

        # --- CONNEXION (centre) — réduit 25%, bouton sous les champs ---
        self.canvas.create_rectangle(275, 15, 559, 100, fill=CARD, outline=ACCENT, width=2)
        self.canvas.create_text(417, 20, text="Connexion", font=("Segoe UI", 12, "bold"),
                                fill=ACCENT, anchor="n")

        self.canvas.create_text(285, 48, text="Pseudo:", font=("Segoe UI", 9),
                                fill=TEXT2, anchor="w")
        self.login_pseudo = tk.Entry(self, font=("Segoe UI", 9), width=7,
                                     bg="#111122", fg=TEXT, insertbackground=ACCENT,
                                     relief="flat", highlightthickness=1, highlightcolor=ACCENT)
        self.login_pseudo.place(x=335, y=39, width=75, height=19)

        self.canvas.create_text(415, 48, text="MDP:", font=("Segoe UI", 9),
                                fill=TEXT2, anchor="w")
        self.login_mdp = tk.Entry(self, font=("Segoe UI", 9), width=7, show="\u2022",
                                  bg="#111122", fg=TEXT, insertbackground=ACCENT,
                                  relief="flat", highlightthickness=1, highlightcolor=ACCENT)
        self.login_mdp.place(x=448, y=39, width=65, height=19)

        tk.Button(self, text="Connexion", font=("Segoe UI", 8, "bold"),
                  fg=TEXT, bg=GREEN, relief="flat", cursor="hand2",
                  command=self._do_login).place(x=330, y=70, width=80, height=20)

        self.remember_var = tk.BooleanVar(value=False)
        tk.Checkbutton(self, text="Enregistrer", font=("Segoe UI", 8),
                       fg=TEXT2, bg=CARD, selectcolor="#111122",
                       activebackground=CARD, activeforeground=TEXT,
                       variable=self.remember_var, cursor="hand2").place(x=420, y=70)

        # --- INSCRIPTION (droite) — même espace titre → champs ---
        self.canvas.create_rectangle(584, 15, 1004, 100, fill=CARD, outline=ACCENT, width=2)
        self.canvas.create_text(794, 20, text="Inscription", font=("Segoe UI", 13, "bold"),
                                fill=ACCENT, anchor="n")

        # Ligne 1 : Email + Pseudo + MDP
        self.canvas.create_text(596, 48, text="Email:", font=("Segoe UI", 9),
                                fill=TEXT2, anchor="w")
        self.reg_email = tk.Entry(self, font=("Segoe UI", 9), width=10,
                                  bg="#111122", fg=TEXT, insertbackground=ACCENT,
                                  relief="flat", highlightthickness=1, highlightcolor=ACCENT)
        self.reg_email.place(x=638, y=39, width=130, height=18)

        self.canvas.create_text(774, 48, text="Pseudo:", font=("Segoe UI", 9),
                                fill=TEXT2, anchor="w")
        self.reg_pseudo = tk.Entry(self, font=("Segoe UI", 9), width=7,
                                   bg="#111122", fg=TEXT, insertbackground=ACCENT,
                                   relief="flat", highlightthickness=1, highlightcolor=ACCENT)
        self.reg_pseudo.place(x=822, y=39, width=70, height=18)

        self.canvas.create_text(898, 48, text="MDP:", font=("Segoe UI", 9),
                                fill=TEXT2, anchor="w")
        self.reg_mdp = tk.Entry(self, font=("Segoe UI", 9), width=6, show="\u2022",
                                bg="#111122", fg=TEXT, insertbackground=ACCENT,
                                relief="flat", highlightthickness=1, highlightcolor=ACCENT)
        self.reg_mdp.place(x=930, y=39, width=62, height=18)

        # Ligne 2 : bouton Inscription
        tk.Button(self, text="S'inscrire", font=("Segoe UI", 9, "bold"),
                  fg=TEXT, bg=BLUE, relief="flat", cursor="hand2",
                  command=self._do_register).place(x=680, y=70, width=230, height=18)

        # ====== ZONE MILIEU : ACTUALITES (gauche) + CLASSEMENT (droite) ======

        # --- ACTUALITES (gauche) — rotation auto ---
        self.frame(20, 110, 510, 370, color="#1a2a4a")
        self.canvas.create_text(265, 128, text="Dernieres actualites",
                                font=("Segoe UI", 13, "bold"), fill=ACCENT, anchor="center")
        self.actu_frame = tk.Frame(self, bg="#1a2a4a")
        self.actu_frame.place(x=35, y=148, width=460, height=208)
        self.actu_idx = 0
        self._build_actu_page()

        # --- CLASSEMENT (droite) — rotation auto ---
        self.frame(520, 110, 1004, 370, color="#1a2a4a")
        self.canvas.create_text(762, 128, text="Classement",
                                font=("Segoe UI", 13, "bold"), fill=ACCENT, anchor="center")
        self.classement_frame = tk.Frame(self, bg="#1a2a4a")
        self.classement_frame.place(x=535, y=148, width=455, height=208)
        self.classement_pages = ["territoire", "xp", "chasse"]
        self.classement_idx = 0
        self._build_classement_page()

        # ====== ANNONCES (jusqu'a la barre) ======
        self.frame(20, 380, 1004, 690, color="#1a2a4a")
        self.canvas.create_text(512, 410, text="Annonces",
                                font=("Segoe UI", 16, "bold"), fill=ACCENT, anchor="center")
        self.canvas.create_text(512, 500, text="Espace reserve aux annonceurs",
                                font=("Segoe UI", 14), fill=TEXT2, anchor="center")
        self.canvas.create_text(512, 540, text="Contactez-nous pour placer votre pub ici",
                                font=("Segoe UI", 10), fill="#556677", anchor="center")

        # Frame fichiers a mettre a jour
        self.files_frame = tk.Frame(self, bg="#1a2a4a")
        self.files_frame.place(x=20, y=110, width=984, height=580)
        self.files_frame.place_forget()

        # ====== BARRE + BOUTONS ======
        self.canvas.create_rectangle(20, 700, 720, 740, fill="#333355", outline="#555577", width=1)
        self.bar_fill = self.canvas.create_rectangle(21, 701, 21, 739, fill=GREEN, outline="")
        self.bar_text = self.canvas.create_text(370, 720, text="En attente...",
                                                font=("Segoe UI", 10), fill=TEXT, anchor="center")
        self.pct_text = self.canvas.create_text(695, 720, text="0%",
                                                font=("Segoe UI", 10, "bold"), fill=ACCENT, anchor="center")
        self.play_btn = tk.Button(self, text="JOUER", font=("Segoe UI", 14, "bold"),
                                  fg=TEXT, bg="#444444", relief="flat", cursor="hand2",
                                  state="disabled", width=10, command=self.play)
        self.play_btn.place(x=750, y=698, height=42)
        tk.Button(self, text="QUITTER", font=("Segoe UI", 14, "bold"),
                  fg=TEXT, bg="#aa3333", relief="flat", cursor="hand2",
                  width=10, command=self.destroy).place(x=886, y=698, height=42)

    # ============================================================
    # ACTUALITES — rotation auto (comme classement)
    # ============================================================

    def _build_actu_page(self):
        for w in self.actu_frame.winfo_children():
            w.destroy()

        n = NEWS[self.actu_idx]

        # Titre
        tk.Label(self.actu_frame, text=n["t"], font=("Segoe UI", 13, "bold"),
                 fg=TEXT, bg="#1a2a4a", wraplength=430, justify="left").pack(anchor="w", padx=10, pady=(10, 2))

        # Date
        tk.Label(self.actu_frame, text=n["date"], font=("Segoe UI", 9),
                 fg="#667788", bg="#1a2a4a").pack(anchor="w", padx=10, pady=(0, 8))

        # Separateur
        tk.Frame(self.actu_frame, bg="#444466", height=1).pack(fill="x", padx=10, pady=4)

        # Description
        tk.Label(self.actu_frame, text=n["d"], font=("Segoe UI", 10),
                 fg=TEXT2, bg="#1a2a4a", wraplength=430, justify="left").pack(anchor="w", padx=10, pady=5)

        # Indicateurs de page
        dots = tk.Frame(self.actu_frame, bg="#1a2a4a")
        dots.pack(side="bottom", pady=8)
        for i in range(len(NEWS)):
            color = ACCENT if i == self.actu_idx else "#555555"
            lbl = tk.Label(dots, text="\u25cf", font=("Segoe UI", 12),
                           fg=color, bg="#1a2a4a", cursor="hand2")
            lbl.pack(side="left", padx=4)
            lbl.bind("<Button-1>", lambda e, idx=i: self._goto_actu(idx))

        self.actu_timer = self.after(7000, self._next_actu)

    def _next_actu(self):
        self.actu_idx = (self.actu_idx + 1) % len(NEWS)
        self._build_actu_page()

    def _goto_actu(self, idx):
        if hasattr(self, 'actu_timer'):
            self.after_cancel(self.actu_timer)
        self.actu_idx = idx
        self._build_actu_page()

    # ============================================================
    # CONNEXION
    # ============================================================

    def _load_saved_login(self):
        cfg = load_local_config()
        if cfg.get("saved_pseudo"):
            self.login_pseudo.insert(0, cfg["saved_pseudo"])
            self.remember_var.set(True)
        if cfg.get("saved_mdp"):
            self.login_mdp.insert(0, cfg["saved_mdp"])

    def _do_login(self):
        pseudo = self.login_pseudo.get().strip()
        mdp = self.login_mdp.get().strip()
        if not pseudo or not mdp:
            return
        cfg = load_local_config()
        if self.remember_var.get():
            cfg["saved_pseudo"] = pseudo
            cfg["saved_mdp"] = mdp
        else:
            cfg.pop("saved_pseudo", None)
            cfg.pop("saved_mdp", None)
        save_local_config(cfg)

    def _do_register(self):
        email = self.reg_email.get().strip()
        pseudo = self.reg_pseudo.get().strip()
        mdp = self.reg_mdp.get().strip()
        if not email or not pseudo or not mdp:
            return
        # TODO: envoyer au serveur {email, pseudo, mdp}
        # Le serveur enverra un email de confirmation avec un lien
        # Le joueur clique sur le lien pour activer son compte

    # ============================================================
    # CLASSEMENT — pages rotatives (vide)
    # ============================================================

    def _build_classement_page(self):
        for w in self.classement_frame.winfo_children():
            w.destroy()

        pages = {
            "territoire": {"title": "Territoire", "icon": "\U0001f3f0", "color": "#e67e22"},
            "xp":         {"title": "Experience", "icon": "\u2b50",     "color": "#f1c40f"},
            "chasse":     {"title": "Chasse",     "icon": "\U0001f5e1", "color": "#e74c3c"},
        }
        p = pages[self.classement_pages[self.classement_idx]]

        tk.Label(self.classement_frame, text=f"{p['icon']} {p['title']}",
                 font=("Segoe UI", 13, "bold"), fg=p["color"],
                 bg="#1a2a4a").pack(pady=(15, 10))
        tk.Label(self.classement_frame, text="Aucune donnee pour le moment",
                 font=("Segoe UI", 11), fg="#555555", bg="#1a2a4a").pack(pady=8)
        tk.Label(self.classement_frame,
                 text="Les classements apparaitront\nquand le serveur sera en ligne",
                 font=("Segoe UI", 9), fg="#444444", bg="#1a2a4a").pack()

        dots = tk.Frame(self.classement_frame, bg="#1a2a4a")
        dots.pack(side="bottom", pady=8)
        for i in range(len(self.classement_pages)):
            color = ACCENT if i == self.classement_idx else "#555555"
            lbl = tk.Label(dots, text="\u25cf", font=("Segoe UI", 12),
                           fg=color, bg="#1a2a4a", cursor="hand2")
            lbl.pack(side="left", padx=4)
            lbl.bind("<Button-1>", lambda e, idx=i: self._goto_classement(idx))

        self.classement_timer = self.after(6000, self._next_classement)

    def _next_classement(self):
        self.classement_idx = (self.classement_idx + 1) % len(self.classement_pages)
        self._build_classement_page()

    def _goto_classement(self, idx):
        if hasattr(self, 'classement_timer'):
            self.after_cancel(self.classement_timer)
        self.classement_idx = idx
        self._build_classement_page()

    # ============================================================
    # BARRE DE PROGRESSION
    # ============================================================

    def _upd_bar(self, pct, txt):
        w = max(2, int(pct / 100 * 698))
        self.canvas.coords(self.bar_fill, 21, 701, 21 + w, 739)
        self.canvas.itemconfig(self.bar_text, text=txt)
        self.canvas.itemconfig(self.pct_text, text=f"{pct}%")

    def upd(self, p, t):
        self.after(0, lambda: self._upd_bar(p, t))

    # ============================================================
    # MISE A JOUR
    # ============================================================

    def start_update(self):
        self.upd(2, "Verification des mises a jour...")
        threading.Thread(target=self._run_update, daemon=True).start()

    def _run_update(self):
        result = check_for_updates(self.upd)
        self.update_result = result
        if result["error"]:
            self.after(0, lambda: self._upd_bar(100, f"Erreur: {result['error']}"))
            time.sleep(1); self.after(0, self._check_godot); return
        modified = result["modified"]
        if not modified:
            self.after(0, lambda: self._upd_bar(100, "A jour !"))
            time.sleep(0.5); self.after(0, self._check_godot); return
        self.after(0, lambda: self._show_modified_files(modified))

    def _show_modified_files(self, modified):
        self._upd_bar(50, f"{len(modified)} fichier(s) a mettre a jour")
        self.files_frame.place(x=20, y=110, width=984, height=580)
        for w in self.files_frame.winfo_children():
            w.destroy()

        canvas = tk.Canvas(self.files_frame, bg="#1a2a4a", highlightthickness=0)
        scrollbar = tk.Scrollbar(self.files_frame, orient="vertical", command=canvas.yview)
        scrollable = tk.Frame(canvas, bg="#1a2a4a")
        scrollable.bind("<Configure>", lambda e: canvas.configure(scrollregion=canvas.bbox("all")))
        canvas.create_window((0, 0), window=scrollable, anchor="nw")
        canvas.configure(yscrollcommand=scrollbar.set)
        canvas.pack(side="left", fill="both", expand=True)
        scrollbar.pack(side="right", fill="y")

        tk.Label(scrollable, text=f"Mises a jour disponibles ({len(modified)} fichiers)",
                 font=("Segoe UI", 13, "bold"), fg=ACCENT, bg="#1a2a4a").pack(pady=(10, 10))

        for fname, local_h, remote_h, action in modified:
            row = tk.Frame(scrollable, bg="#1a2a4a")
            row.pack(fill="x", padx=10, pady=1)
            is_text = is_text_file(fname)
            color = GREEN if action == "NOUVEAU" else BLUE

            name_lbl = tk.Label(row, text=f"  {fname}", font=("Segoe UI", 10),
                                fg=color if is_text else TEXT2, bg="#1a2a4a",
                                width=35, anchor="w", cursor="hand2" if is_text else "arrow")
            name_lbl.pack(side="left")
            if is_text:
                name_lbl.bind("<Button-1>", lambda e, f=fname: self.view_file(f))
                name_lbl.bind("<Enter>", lambda e, l=name_lbl: l.configure(fg=ACCENT))
                name_lbl.bind("<Leave>", lambda e, l=name_lbl, c=color: l.configure(fg=c))

            status_text = "+ NOUVEAU" if action == "NOUVEAU" else "~ MODIFIE"
            tk.Label(row, text=status_text, font=("Segoe UI", 9),
                     fg=color, bg="#1a2a4a", width=12, anchor="w").pack(side="left")

            if is_text:
                tk.Button(row, text="Visualiser", font=("Segoe UI", 8),
                          fg=TEXT, bg="#333366", relief="flat", cursor="hand2",
                          command=lambda f=fname: self.view_file(f)).pack(side="left", padx=5)
            else:
                info = self.update_result["remote_cfg"]["files"].get(fname, {})
                tk.Label(row, text=f"Binaire ({info.get('size', 0) // 1024} KB)",
                         font=("Segoe UI", 9), fg="#666666", bg="#1a2a4a", width=15, anchor="w").pack(side="left")

        btn_frame = tk.Frame(self.files_frame, bg="#1a2a4a")
        btn_frame.pack(side="bottom", fill="x", padx=10, pady=8)
        tk.Button(btn_frame, text="Tout mettre a jour", font=("Segoe UI", 11, "bold"),
                  fg=TEXT, bg=GREEN, relief="flat", cursor="hand2", width=20,
                  command=lambda: self._apply_all_updates(modified)).pack(side="left", padx=5)
        tk.Button(btn_frame, text="Plus tard", font=("Segoe UI", 10),
                  fg=TEXT2, bg="#444466", relief="flat", cursor="hand2", width=12,
                  command=self._skip_update).pack(side="left", padx=5)
        tk.Button(btn_frame, text="Voir sur GitHub", font=("Segoe UI", 10),
                  fg=TEXT2, bg="#444466", relief="flat", cursor="hand2", width=15,
                  command=self._open_github).pack(side="right", padx=5)

    # ============================================================
    # VISUALISATION DE FICHIER
    # ============================================================

    def view_file(self, fname):
        win = tk.Toplevel(self)
        win.title(f"Visualisation — {fname}")
        win.geometry("900x600"); win.configure(bg=BG); win.transient(self)

        hdr = tk.Frame(win, bg=CARD, height=40); hdr.pack(fill="x")
        tk.Label(hdr, text=f"  {fname}", font=("Segoe UI", 14, "bold"),
                 fg=ACCENT, bg=CARD).pack(side="left", padx=10, pady=5)

        local_cfg = load_local_config()
        info = local_cfg.get("files", {}).get(fname, {})
        if info:
            tk.Label(hdr, text=f"  ({info.get('size', 0)} octets)",
                     font=("Segoe UI", 10), fg=TEXT2, bg=CARD).pack(side="left", pady=5)

        local_path = os.path.join(GAME_DIR, fname)
        if os.path.exists(local_path):
            tk.Button(hdr, text="Ouvrir dans l'editeur", font=("Segoe UI", 9),
                      fg=TEXT, bg=BLUE, relief="flat", cursor="hand2",
                      command=lambda: os.startfile(local_path) if sys.platform == 'win32' else None
                      ).pack(side="right", padx=10, pady=5)

        text_frame = tk.Frame(win, bg=BG)
        text_frame.pack(fill="both", expand=True, padx=5, pady=5)
        text_widget = tk.Text(text_frame, bg="#0d1117", fg="#c9d1d9",
                              font=("Consolas", 11), wrap="none",
                              insertbackground=ACCENT, selectbackground="#264f78")
        scroll_y = tk.Scrollbar(text_frame, orient="vertical", command=text_widget.yview)
        scroll_x = tk.Scrollbar(text_frame, orient="horizontal", command=text_widget.xview)
        text_widget.configure(yscrollcommand=scroll_y.set, xscrollcommand=scroll_x.set)
        scroll_y.pack(side="right", fill="y"); scroll_x.pack(side="bottom", fill="x")
        text_widget.pack(fill="both", expand=True)

        content = None
        if os.path.exists(local_path):
            try:
                with open(local_path, 'r', encoding='utf-8', errors='replace') as f:
                    content = f.read()
            except: pass
        if content is None:
            try:
                raw_url = local_cfg.get("raw_url", "")
                url = get_remote_url(fname, raw_url)
                req = urllib.request.Request(url, headers={"User-Agent": "LibreVies"})
                content = urllib.request.urlopen(req, timeout=15).read().decode('utf-8', errors='replace')
                text_widget.insert("1.0", f"# Fichier distant\n# URL: {url}\n\n{content}")
            except Exception as e:
                text_widget.insert("1.0", f"Erreur: {e}")
        else:
            text_widget.insert("1.0", content)
        text_widget.config(state="disabled")

        status = tk.Frame(win, bg=CARD, height=25); status.pack(fill="x")
        tk.Label(status, text=f"  {text_widget.get('1.0', 'end').count(chr(10))} lignes",
                 font=("Segoe UI", 9), fg=TEXT2, bg=CARD).pack(side="left", padx=10)
        tk.Button(status, text="Fermer", font=("Segoe UI", 9),
                  fg=TEXT, bg="#444466", relief="flat", cursor="hand2",
                  command=win.destroy).pack(side="right", padx=10, pady=2)

    # ============================================================
    # APPLIQUER / SKIP
    # ============================================================

    def _apply_all_updates(self, modified):
        for w in self.files_frame.winfo_children():
            w.destroy()
        threading.Thread(target=self._do_apply_updates, args=(modified,), daemon=True).start()

    def _do_apply_updates(self, modified):
        result = self.update_result
        downloaded, errors = apply_updates(modified, result["remote_cfg"], self.upd)
        if result["launcher_needs_update"]:
            self.after(0, lambda: self._upd_bar(95, "Mise a jour du launcher..."))
            try: update_launcher_exe()
            except Exception as e: errors.append(f"Launcher: {e}")
        msg = f"{downloaded} OK, {len(errors)} erreur(s)" if errors else f"{downloaded} fichier(s) mis a jour !"
        self.after(0, lambda: self._upd_bar(100, msg))
        time.sleep(0.5); self.after(0, self._check_godot)

    def _skip_update(self):
        self.files_frame.place_forget()
        self._upd_bar(100, "Mise a jour ignoree")
        self.after(500, self._check_godot)

    def _open_github(self):
        import webbrowser
        cfg = load_local_config()
        url = cfg.get("game_url", "")
        if url:
            webbrowser.open(url)

    # ============================================================
    # GODOT
    # ============================================================

    def _check_godot(self):
        self.godot = find_godot()
        if self.godot:
            self.upd(5, "Godot trouve, chargement...")
            threading.Thread(target=preload, args=(self.godot, self.upd, self.on_ready), daemon=True).start()
        else:
            self.upd(0, "Godot non trouve. Telechargement...")
            threading.Thread(target=download_godot, args=(self.upd, self.on_dl), daemon=True).start()

    def on_dl(self, p):
        self.after(0, lambda: self._on_dl(p))

    def _on_dl(self, p):
        if p:
            self.godot = p
            self.upd(50, "Chargement des assets...")
            threading.Thread(target=preload, args=(p, self.upd, self.on_ready), daemon=True).start()
        else:
            self._upd_bar(0, "Erreur de telechargement")

    def on_ready(self, ok):
        self.after(0, lambda: self._ready(ok))

    def _ready(self, ok):
        if ok:
            self.ready = True
            self.play_btn.config(state="normal", bg=GREEN)
            self._upd_bar(100, "Pret !")

    def play(self):
        if self.ready and self.godot:
            launch(self.godot); self.destroy()


if __name__ == "__main__":
    App().mainloop()
