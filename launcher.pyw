"""
LibreVies — Launcher
Double-clique sur launcher.pyw pour lancer
"""
import tkinter as tk
from tkinter import ttk
import subprocess, threading, os, time, zipfile, urllib.request

GAME_DIR = os.path.dirname(os.path.abspath(__file__))
GODOT_URL = "https://github.com/godotengine/godot/releases/download/4.4.1-stable/Godot_v4.4.1-stable_win64.exe.zip"
BANNER = os.path.join(GAME_DIR, "banniere_v1.png")

BG = "#1a1a2e"; BG2 = "#222244"; CARD = "#2a2a50"
ACCENT = "#f1c40f"; TEXT = "#ffffff"; TEXT2 = "#aabbcc"
GREEN = "#27ae60"

NEWS = [
    {"date":"28/07/2026","t":"Version 0.1 — Premier lancement","d":"Ville de départ, personnage voxel, ennemis, système de combat et d'argent."},
    {"date":"28/07/2026","t":"Personnage voxel animé","d":"19 parties séparées, animation marche, marteau qui suit le bras."},
    {"date":"28/07/2026","t":"Système d'économie","d":"Argent au sol, cailloux, chasse de bêtes, HUD complet."},
    {"date":"28/07/2026","t":"Launcher LibreVies","d":"Nouveau launcher avec chargement automatique de Godot."},
]

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
        cb(0, "Téléchargement de Godot...")
        r = urllib.request.urlopen(urllib.request.Request(GODOT_URL, headers={"User-Agent":"LibreVies"}), timeout=180)
        total = int(r.headers.get('content-length', 0)); dl = 0
        with open(zp, 'wb') as f:
            while True:
                ch = r.read(8192)
                if not ch: break
                f.write(ch); dl += len(ch)
                if total > 0: cb(int(dl/total*50), f"Téléchargement... {int(dl/total*100)}%")
        cb(50, "Extraction...")
        with zipfile.ZipFile(zp) as z: z.extractall(tools)
        os.remove(zp); cb(100, "Godot installé !"); done(find_godot())
    except Exception as e:
        cb(0, f"Erreur : {e}"); done(None)

def preload(path, cb, done):
    try:
        cb(10, "Préparation...")
        p = subprocess.Popen([path, "--import", "--headless", "--path", GAME_DIR], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        for i in range(10, 90, 3):
            time.sleep(0.2); cb(i, f"Chargement... {i}%")
        p.wait(timeout=60); cb(100, "Prêt !"); done(True)
    except:
        cb(100, "Prêt !"); done(True)

def launch(path):
    subprocess.Popen([path, "--path", GAME_DIR])

class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("LibreVies"); self.geometry("1024x768")
        self.resizable(False, False); self.configure(bg=BG)

        # Icône
        icon_path = os.path.join(GAME_DIR, "pp_lv_3.png")
        if os.path.exists(icon_path):
            try:
                from PIL import Image, ImageTk
                icon_img = ImageTk.PhotoImage(Image.open(icon_path).resize((32, 32)))
                self.iconphoto(True, icon_img)
                self._icon = icon_img
            except:
                pass
        self.godot = find_godot(); self.ready = False
        self.news_idx = 0
        self.build(); self.check()

    def frame(self, x1, y1, x2, y2, color=CARD, outline=ACCENT):
        self.canvas.create_rectangle(x1, y1, x2, y2, fill=color, outline=outline, width=2)

    def build(self):
        self.canvas = tk.Canvas(self, width=1024, height=768, highlightthickness=0)
        self.canvas.pack(fill="both", expand=True)

        # FOND
        self.bg_img = None
        if os.path.exists(BANNER):
            try:
                from PIL import Image, ImageTk
                img = Image.open(BANNER).resize((1024, 768), Image.LANCZOS)
                self.bg_img = ImageTk.PhotoImage(img)
                self.canvas.create_image(0, 0, image=self.bg_img, anchor="nw")
            except:
                self.canvas.configure(bg=BG)
        else:
            self.canvas.configure(bg=BG)

        self.canvas.create_rectangle(0, 0, 1024, 768, fill="black", stipple="gray25")

        # ====== CADRE TITRE (haut gauche) ======
        self.frame(20, 15, 400, 85)
        self.canvas.create_text(35, 25, text="LibreVies", font=("Segoe UI", 30, "bold"),
                                fill=ACCENT, anchor="nw")
        self.canvas.create_text(35, 62, text="MMO Open World", font=("Segoe UI", 11),
                                fill=TEXT2, anchor="nw")

        # ====== CADRE STATISTIQUES (haut droit) ======
        self.frame(650, 15, 1004, 85)
        self.canvas.create_text(670, 28, text="Statistiques", font=("Segoe UI", 12, "bold"),
                                fill=ACCENT, anchor="nw")
        self.canvas.create_text(670, 50, text="Version : v0.1-alpha", font=("Segoe UI", 10),
                                fill=TEXT2, anchor="nw")
        self.canvas.create_text(670, 66, text="Moteur : Godot 4.4.1  |  PC Windows 10", font=("Segoe UI", 10),
                                fill=TEXT2, anchor="nw")

        # ====== CADRE ACTUALITÉS ======
        self.frame(20, 100, 1004, 260)
        self.canvas.create_text(40, 112, text="Actualités et mises à jour",
                                font=("Segoe UI", 14, "bold"), fill=ACCENT, anchor="nw")
        self.news_date_t = self.canvas.create_text(40, 140, text="", font=("Segoe UI", 10), fill=TEXT2, anchor="nw")
        self.news_title_t = self.canvas.create_text(40, 160, text="", font=("Segoe UI", 14, "bold"), fill=TEXT, anchor="nw")
        self.news_desc_t = self.canvas.create_text(40, 185, text="", font=("Segoe UI", 10), fill=TEXT2, anchor="nw", width=940)
        self.dot_ids = []
        for i in range(len(NEWS)):
            x = 490 + i * 25
            dot = self.canvas.create_text(x, 240, text="●", font=("Segoe UI", 14),
                                          fill=ACCENT if i == 0 else "#555555", anchor="center")
            self.dot_ids.append(dot)
            self.canvas.tag_bind(dot, "<Button-1>", lambda e, n=i: self.goto_news(n))

        # ====== CADRE ANNONCES (grand) ======
        self.frame(20, 275, 1004, 580, color="#1a2a4a")
        self.canvas.create_text(512, 305, text="Annonces", font=("Segoe UI", 16, "bold"),
                                fill=ACCENT, anchor="center")
        self.canvas.create_text(512, 420, text="Espace réservé aux annonceurs",
                                font=("Segoe UI", 16), fill=TEXT2, anchor="center")
        self.canvas.create_text(512, 460, text="Contactez-nous pour placer votre pub ici",
                                font=("Segoe UI", 11), fill="#556677", anchor="center")

        # ====== BARRE + BOUTONS (tout en bas) ======
        # Barre de progression
        self.canvas.create_rectangle(20, 700, 720, 740, fill="#333355", outline="#555577", width=1)
        self.bar_fill = self.canvas.create_rectangle(21, 701, 21, 739, fill=GREEN, outline="")
        self.bar_text = self.canvas.create_text(370, 720, text="En attente...",
                                                font=("Segoe UI", 10), fill=TEXT, anchor="center")
        self.pct_text = self.canvas.create_text(695, 720, text="0%",
                                                font=("Segoe UI", 10, "bold"), fill=ACCENT, anchor="center")

        # Boutons
        self.play_btn = tk.Button(self, text="JOUER", font=("Segoe UI", 14, "bold"),
                                  fg=TEXT, bg="#444444", relief="flat", cursor="hand2",
                                  state="disabled", width=10, command=self.play)
        self.play_btn.place(x=750, y=698, height=42)

        tk.Button(self, text="QUITTER", font=("Segoe UI", 14, "bold"),
                  fg=TEXT, bg="#aa3333", relief="flat", cursor="hand2",
                  width=10, command=self.destroy).place(x=886, y=698, height=42)

        self.update_news()
        self.animate_news()

    def goto_news(self, idx):
        self.news_idx = idx; self.update_news()

    def update_news(self):
        n = NEWS[self.news_idx]
        self.canvas.itemconfig(self.news_date_t, text=n['date'])
        self.canvas.itemconfig(self.news_title_t, text=n['t'])
        self.canvas.itemconfig(self.news_desc_t, text=n['d'])
        for i, d in enumerate(self.dot_ids):
            self.canvas.itemconfig(d, fill=ACCENT if i == self.news_idx else "#555555")

    def animate_news(self):
        self.news_idx = (self.news_idx + 1) % len(NEWS)
        self.update_news()
        self.after(5000, self.animate_news)

    def _upd_bar(self, pct, txt):
        w = max(2, int(pct / 100 * 698))
        self.canvas.coords(self.bar_fill, 21, 701, 21 + w, 739)
        self.canvas.itemconfig(self.bar_text, text=txt)
        self.canvas.itemconfig(self.pct_text, text=f"{pct}%")

    def upd(self, p, t):
        self.after(0, lambda: self._upd_bar(p, t))

    def check(self):
        if self.godot:
            self.upd(5, "Godot trouvé, chargement...")
            threading.Thread(target=preload, args=(self.godot, self.upd, self.on_ready), daemon=True).start()
        else:
            self.upd(0, "Godot non trouvé. Téléchargement...")
            threading.Thread(target=download_godot, args=(self.upd, self.on_dl), daemon=True).start()

    def on_dl(self, p):
        self.after(0, lambda: self._on_dl(p))

    def _on_dl(self, p):
        if p:
            self.godot = p; self.upd(50, "Chargement des assets...")
            threading.Thread(target=preload, args=(p, self.upd, self.on_ready), daemon=True).start()
        else:
            self._upd_bar(0, "Erreur de téléchargement")

    def on_ready(self, ok):
        self.after(0, lambda: self._ready(ok))

    def _ready(self, ok):
        if ok:
            self.ready = True
            self.play_btn.config(state="normal", bg=GREEN)
            self._upd_bar(100, "Prêt !")

    def play(self):
        if self.ready and self.godot:
            launch(self.godot); self.destroy()

if __name__ == "__main__":
    App().mainloop()
