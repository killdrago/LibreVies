"""Publieur graphique de la version compilee de LibreVies.

Le programme synchronise le contenu local de ``jeu`` vers le dossier Git
``jeucompiler`` puis pousse les changements sur GitHub. Les fichiers sont
regroupes en lots dont la taille maximale est choisie dans l'interface. Un
fichier individuel plus grand que cette limite est envoye seul : la limite
concerne un lot, elle ne decoupe pas un binaire.

Le launcher des joueurs lit ensuite ``jeucompiler/version_url.json`` et
telecharge les fichiers deja compiles. Il ne compile jamais le jeu chez le
joueur.
"""
from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import subprocess
import tempfile
import threading
import tkinter as tk
from dataclasses import dataclass
from pathlib import Path
from tkinter import filedialog, messagebox, ttk
from urllib.parse import quote

RACINE = Path(__file__).resolve().parent
SOURCE_DEFAUT = RACINE / "jeu"
CIBLE_DEFAUT = RACINE / "jeucompiler"
DEPOT_DEFAUT = "killdrago/LibreVies"
DOSSIER_DISTANT_DEFAUT = "jeucompiler"
BRANCHE_DEFAUT = "arena/01a0b32c-librevies"

TEXT_EXTS = {
    ".pyw", ".py", ".bat", ".cmd", ".json", ".cfg", ".txt", ".md",
    ".html", ".css", ".js", ".csv", ".cs", ".meta", ".unity",
    ".asset", ".yaml", ".yml", ".ini"
}
IGNORER_NOMS = {
    "__pycache__", ".git", "Library", "Temp", "Logs", "obj", "Build",
    "build", "game.install", "game.ancien", "sauvegarde_locale"
}
IGNORER_SUFFIXES = (".download", ".download.part", ".new", ".part", ".tmp")


@dataclass
class Operation:
    relatif: str
    source: Path | None
    taille: int
    hash_source: str
    suppression: bool = False


class PublicationError(RuntimeError):
    pass


def commande(*args: str, cwd: Path | None = None, check: bool = True) -> str:
    resultat = subprocess.run(
        list(args), cwd=str(cwd or RACINE), capture_output=True, text=True,
        encoding="utf-8", errors="replace")
    sortie = (resultat.stdout or "") + (resultat.stderr or "")
    if check and resultat.returncode != 0:
        raise PublicationError(sortie.strip() or "commande echouee : " + " ".join(args))
    return sortie.strip()


def depot_github() -> str:
    try:
        distant = commande("git", "remote", "get-url", "origin", check=False)
    except OSError:
        return DEPOT_DEFAUT
    distant = distant.strip()
    match = re.search(r"github\.com[/:]([^/ :]+/[^/ :]+?)(?:\.git)?$", distant)
    return match.group(1) if match else DEPOT_DEFAUT


def normaliser_cible_github(depot: str, branche: str, dossier: str):
    """Accepte aussi une URL GitHub collee dans l'un des champs.

    L'interface attend normalement ``killdrago/LibreVies`` et ``jeucompiler``
    (ou ``jeucompiler``), mais un copier-coller d'une URL /tree/... ne doit
    pas fabriquer une URL raw invalide.
    """
    depot = (depot or '').strip().rstrip('/')
    branche = (branche or '').strip().strip('/')
    dossier = (dossier or '').strip().strip('/')
    for valeur in (depot, dossier):
        match = re.search(
            r"github\.com/([^/]+)/([^/]+?)(?:\.git)?(?:/(?:tree|blob)/([^/]+)(?:/(.*))?)?$",
            valeur)
        if not match:
            continue
        depot = "%s/%s" % (match.group(1), match.group(2))
        if match.group(3):
            branche = match.group(3)
        if match.group(4):
            dossier = match.group(4).strip('/')
        break
    if depot.startswith('http://') or depot.startswith('https://'):
        depot = DEPOT_DEFAUT
    if not dossier or dossier.startswith('http://') or dossier.startswith('https://'):
        dossier = DOSSIER_DISTANT_DEFAUT
    return depot, branche, dossier


def _mettre_de_cote_conflits_non_suivis(racine_git: Path, journal):
    """Ecarte temporairement les fichiers non suivis que FETCH_HEAD ajoutera.

    Une copie manuelle de jeucompiler peut exister dans un clone ancien alors
    que la branche distante vient seulement de recevoir ces mêmes fichiers.
    Git refuse alors le checkout avant meme de lancer le rebase. Les fichiers
    sont deplaces dans un dossier temporaire, puis restaures apres le rebase.
    """
    non_suivis = commande("git", "ls-files", "--others", "--exclude-standard",
                         cwd=racine_git, check=False)
    distants = set(commande("git", "ls-tree", "-r", "--name-only", "FETCH_HEAD",
                            cwd=racine_git, check=False).splitlines())
    conflits = [ligne.strip() for ligne in non_suivis.splitlines()
                if ligne.strip() in distants]
    if not conflits:
        return None
    sauvegarde = Path(tempfile.mkdtemp(prefix="librevies-git-"))
    journal("Fichiers locaux non suivis mis temporairement de cote : %d" % len(conflits))
    deplaces = []
    try:
        for relatif in conflits:
            source = racine_git / Path(relatif)
            destination = sauvegarde / Path(relatif)
            if not source.is_file():
                continue
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.move(str(source), str(destination))
            deplaces.append(relatif)
    except (OSError, shutil.Error):
        # Restaurer immédiatement ce qui a deja ete deplace si le dossier
        # temporaire ne peut pas etre prepare completement.
        _restaurer_conflits_non_suivis(racine_git, sauvegarde, deplaces)
        shutil.rmtree(sauvegarde, ignore_errors=True)
        raise
    return sauvegarde, deplaces


def _restaurer_conflits_non_suivis(racine_git: Path, sauvegarde: Path, deplaces):
    """Restaure les fichiers ecartes avant le rebase, sans les perdre."""
    for relatif in deplaces:
        source = sauvegarde / Path(relatif)
        destination = racine_git / Path(relatif)
        if not source.is_file():
            continue
        destination.parent.mkdir(parents=True, exist_ok=True)
        # Si Git a recree exactement le meme fichier, on conserve sa version
        # suivie et on supprime seulement la copie temporaire.
        if destination.is_file() and destination.read_bytes() == source.read_bytes():
            source.unlink()
        else:
            if destination.is_file():
                destination.unlink()
            shutil.move(str(source), str(destination))
    shutil.rmtree(sauvegarde, ignore_errors=True)


def pousser_avec_rebase(racine_git: Path, branche: str, journal=None):
    """Pousse un lot et resynchronise automatiquement si GitHub a avance.

    Plusieurs publications peuvent arriver en parallele, ou un fichier peut
    avoir ete ajoute depuis GitHub. Dans ce cas, le premier push renvoie
    « fetch first ». On recupere la branche distante, on rebase le commit du
    lot dessus et on retente.
    """
    journal = journal or (lambda _message: None)
    try:
        commande("git", "push", "origin", branche, cwd=racine_git)
        return
    except PublicationError as erreur:
        texte = str(erreur).lower()
        if not any(mot in texte for mot in
                   ("fetch first", "non-fast-forward", "rejected")):
            raise
        journal("GitHub contient une publication plus recente : synchronisation automatique...")
    sauvegarde = None
    try:
        commande("git", "fetch", "origin", branche, cwd=racine_git)
        # Le projet local peut contenir des reglages ou des fichiers suivis
        # modifies en dehors de jeucompiler. Git refuse normalement le
        # rebase dans ce cas ; autostash les met temporairement de cote puis
        # les restaure apres l'integration de la branche distante. Les fichiers
        # non suivis que la branche distante va ajouter sont deplaces dans une
        # sauvegarde temporaire, car autostash ne les prend pas en charge.
        sauvegarde = _mettre_de_cote_conflits_non_suivis(racine_git, journal)
        commande("git", "rebase", "--autostash", "FETCH_HEAD", cwd=racine_git)
        if sauvegarde:
            _restaurer_conflits_non_suivis(racine_git, *sauvegarde)
            sauvegarde = None
        commande("git", "push", "origin", branche, cwd=racine_git)
        journal("Branche distante integree, lot republie.")
    except (OSError, PublicationError) as erreur:
        commande("git", "rebase", "--abort", cwd=racine_git, check=False)
        if sauvegarde:
            _restaurer_conflits_non_suivis(racine_git, *sauvegarde)
        raise PublicationError(
            "GitHub a avance et le rebase automatique a rencontre un conflit. "
            "Aucun rebase n'est laisse en cours. Detail : %s" % erreur)


def branche_courante() -> str:
    try:
        valeur = commande("git", "branch", "--show-current", check=False).strip()
        return valeur or BRANCHE_DEFAUT
    except OSError:
        return BRANCHE_DEFAUT


def est_texte(chemin: Path) -> bool:
    return chemin.suffix.lower() in TEXT_EXTS


def octets_normalises(chemin: Path) -> bytes:
    data = chemin.read_bytes()
    if est_texte(chemin):
        return data.replace(b"\r\n", b"\n").replace(b"\r", b"\n")
    return data


def hash_fichier(chemin: Path) -> str:
    return hashlib.md5(octets_normalises(chemin)).hexdigest()


def hash_bytes(data: bytes, texte: bool) -> str:
    if texte:
        data = data.replace(b"\r\n", b"\n").replace(b"\r", b"\n")
    return hashlib.md5(data).hexdigest()


def taille_fichier(chemin: Path) -> int:
    return chemin.stat().st_size


def chemin_eligible(chemin: Path) -> bool:
    if any(part in IGNORER_NOMS for part in chemin.parts):
        return False
    return not any(chemin.name.endswith(suffix) for suffix in IGNORER_SUFFIXES)


def fichiers_source(source: Path) -> dict[str, Path]:
    resultat: dict[str, Path] = {}
    if not source.is_dir():
        raise PublicationError("Dossier source introuvable : %s" % source)
    for chemin in sorted(source.rglob("*")):
        if not chemin.is_file() or not chemin_eligible(chemin):
            continue
        relatif = chemin.relative_to(source).as_posix()
        # Ces fichiers appartiennent a la machine de compilation et ne doivent
        # jamais etre distribues aux joueurs.
        if relatif in {"version_url.json", "etat_jeu.json"}:
            continue
        resultat[relatif] = chemin
    if not resultat:
        raise PublicationError("Le dossier source ne contient aucun fichier publiable.")
    return resultat


def lire_version_source(source: Path) -> str:
    try:
        donnees = json.loads((source / "version_url.json").read_text(encoding="utf-8"))
        return str(donnees.get("game_version") or "0.0.0")
    except (OSError, ValueError, TypeError):
        return "0.0.0"


def lire_version_launcher(source: Path) -> str:
    try:
        texte = (source / "launcher.pyw").read_text(encoding="utf-8")
        trouve = re.search(r'^LAUNCHER_VERSION\s*=\s*"([^"]+)"', texte, re.M)
        return trouve.group(1) if trouve else ""
    except OSError:
        return ""


def trouver_executable(source_files: dict[str, Path]) -> tuple[str, Path] | tuple[None, None]:
    for relatif, chemin in source_files.items():
        if relatif.lower().startswith("game/") and chemin.name.lower() == "libreviesgame.exe":
            return relatif, chemin
    return None, None


def construire_manifeste(source: Path, depot: str, branche: str,
                         dossier_distant: str, version: str, notes: str,
                         files: dict[str, Path]) -> bytes:
    infos: dict[str, dict[str, object]] = {}
    game_hashes: list[str] = []
    exe_rel, _ = trouver_executable(files)
    for relatif, chemin in sorted(files.items()):
        digest = hash_fichier(chemin)
        infos[relatif] = {"hash": digest, "size": taille_fichier(chemin)}
        if relatif.lower().startswith("game/"):
            game_hashes.append(relatif + ":" + digest)

    aggregate = hashlib.md5("\n".join(game_hashes).encode("utf-8")).hexdigest()
    raw = "https://raw.githubusercontent.com/%s/%s/%s" % (
        depot, branche, quote(dossier_distant.strip("/"), safe="/"))
    tree = "https://github.com/%s/tree/%s/%s" % (
        depot, branche, quote(dossier_distant.strip("/"), safe="/"))
    exe = "LibreViesGame.exe" if exe_rel else ""
    manifeste = {
        "launcher_version": lire_version_launcher(source),
        "game_version": version,
        "notes": notes,
        "game_url": tree,
        "raw_url": raw,
        "files": infos,
        "game_build": {
            "moteur": "unity",
            "mode": "fichiers",
            "version": version,
            "dossier": "game",
            "exe": exe,
            "hash": aggregate,
            "files": len(game_hashes),
        },
    }
    return (json.dumps(manifeste, indent=2, ensure_ascii=False) + "\n").encode("utf-8")


def ecrire_manifeste_local(cible: Path, contenu: bytes) -> Path:
    cible.mkdir(parents=True, exist_ok=True)
    chemin = cible / "version_url.json"
    chemin.write_bytes(contenu)
    return chemin


def construire_operations(source: Path, cible: Path, manifeste: bytes,
                           synchroniser_tout: bool, supprimer_absents: bool) -> list[Operation]:
    source_files = fichiers_source(source)
    operations: list[Operation] = []
    for relatif, chemin_source in source_files.items():
        chemin_cible = cible / relatif
        digest = hash_fichier(chemin_source)
        different = (not chemin_cible.is_file() or hash_fichier(chemin_cible) != digest)
        if synchroniser_tout or different:
            operations.append(Operation(relatif, chemin_source, taille_fichier(chemin_source), digest))

    chemin_manifest = cible / "version_url.json"
    digest_manifest = hash_bytes(manifeste, True)
    if synchroniser_tout or not chemin_manifest.is_file() or hash_fichier(chemin_manifest) != digest_manifest:
        operations.append(Operation("version_url.json", None, len(manifeste), digest_manifest))

    if supprimer_absents and cible.is_dir():
        attendus = set(source_files) | {"version_url.json"}
        for chemin in sorted(cible.rglob("*"), reverse=True):
            if not chemin.is_file():
                continue
            relatif = chemin.relative_to(cible).as_posix()
            if relatif not in attendus:
                operations.append(Operation(relatif, None, 0, "", suppression=True))
    return operations


def grouper_operations(operations: list[Operation], limite_octets: int) -> list[list[Operation]]:
    lots: list[list[Operation]] = []
    courant: list[Operation] = []
    taille = 0
    for operation in operations:
        # Les suppressions n'ont pas de poids mais restent dans le lot suivant.
        si_trop_grand = courant and taille + operation.taille > limite_octets
        if si_trop_grand:
            lots.append(courant)
            courant, taille = [], 0
        courant.append(operation)
        taille += operation.taille
        if operation.taille > limite_octets:
            lots.append(courant)
            courant, taille = [], 0
    if courant:
        lots.append(courant)
    return lots


def taille_lisible(valeur: int) -> str:
    if valeur >= 1024 * 1024:
        return "%.2f Mo" % (valeur / 1048576.0)
    if valeur >= 1024:
        return "%.1f Ko" % (valeur / 1024.0)
    return "%d octets" % valeur


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("LibreVies - publication vers jeucompiler")
        self.geometry("920x720")
        self.minsize(820, 620)
        self.configure(bg="#172033")
        self.operations: list[Operation] = []
        self.lots: list[list[Operation]] = []
        self.commits_en_attente = False
        self.manifeste = b""
        self.construction_en_cours = False
        self._creer_interface()

    def _creer_interface(self):
        style = ttk.Style(self)
        try:
            style.theme_use("clam")
        except tk.TclError:
            pass
        style.configure("TLabel", background="#172033", foreground="#f2f4f8")
        style.configure("TButton", padding=6)
        style.configure("TCheckbutton", background="#172033", foreground="#f2f4f8")
        style.configure("TLabelframe", background="#172033", foreground="#f1c40f")
        style.configure("TLabelframe.Label", background="#172033", foreground="#f1c40f")

        titre = tk.Label(self, text="PUBLICATION DU JEU COMPILE", font=("Segoe UI", 18, "bold"),
                         bg="#172033", fg="#f1c40f")
        titre.pack(anchor="w", padx=18, pady=(14, 3))
        tk.Label(self, text="Les fichiers sont regroupes par lots puis envoyes dans le dossier GitHub jeucompiler.",
                 bg="#172033", fg="#b9c7d8").pack(anchor="w", padx=20, pady=(0, 12))

        cadre = ttk.LabelFrame(self, text="Parametres de publication")
        cadre.pack(fill="x", padx=18, pady=4)
        self.source_var = tk.StringVar(value=str(SOURCE_DEFAUT))
        self.cible_var = tk.StringVar(value=str(CIBLE_DEFAUT))
        self.depot_var = tk.StringVar(value=depot_github())
        self.branche_var = tk.StringVar(value=branche_courante())
        self.distant_var = tk.StringVar(value=DOSSIER_DISTANT_DEFAUT)
        self.version_var = tk.StringVar(value=lire_version_source(SOURCE_DEFAUT))
        self.notes_var = tk.StringVar(value="")
        self.limite_var = tk.StringVar(value="50")
        self.tout_var = tk.BooleanVar(value=False)
        self.supprimer_var = tk.BooleanVar(value=False)

        lignes = [
            ("Dossier source jeu", self.source_var, self._choisir_source),
            ("Dossier local cible", self.cible_var, self._choisir_cible),
            ("Depot GitHub", self.depot_var, None),
            ("Branche", self.branche_var, None),
            ("Dossier distant", self.distant_var, None),
            ("Version du jeu", self.version_var, None),
            ("Notes", self.notes_var, None),
            ("Maximum par envoi (Mo)", self.limite_var, None),
        ]
        for ligne, (nom, variable, bouton) in enumerate(lignes):
            ttk.Label(cadre, text=nom + " :").grid(row=ligne, column=0, sticky="w", padx=10, pady=4)
            ttk.Entry(cadre, textvariable=variable, width=76).grid(row=ligne, column=1, sticky="ew", padx=6, pady=4)
            if bouton:
                ttk.Button(cadre, text="Parcourir", command=bouton).grid(row=ligne, column=2, padx=8, pady=4)
        cadre.columnconfigure(1, weight=1)
        ttk.Checkbutton(cadre, text="Renvoyer tous les fichiers, meme ceux deja identiques",
                        variable=self.tout_var).grid(row=8, column=1, sticky="w", padx=6, pady=(7, 2))
        ttk.Checkbutton(cadre, text="Supprimer de jeucompiler les fichiers absents de jeu",
                        variable=self.supprimer_var).grid(row=9, column=1, sticky="w", padx=6, pady=(2, 8))

        barre = tk.Frame(self, bg="#172033")
        barre.pack(fill="x", padx=18, pady=10)
        self.analyser_btn = ttk.Button(barre, text="ANALYSER LES FICHIERS", command=self.analyser)
        self.analyser_btn.pack(side="left", padx=(0, 8))
        self.envoyer_btn = ttk.Button(barre, text="ENVOYER VERS GITHUB", command=self.envoyer, state="disabled")
        self.envoyer_btn.pack(side="left")
        self.progress = ttk.Progressbar(barre, mode="determinate", maximum=100)
        self.progress.pack(side="right", fill="x", expand=True, padx=(18, 0))

        sortie_frame = ttk.LabelFrame(self, text="Lots prepares")
        sortie_frame.pack(fill="both", expand=True, padx=18, pady=(0, 6))
        self.sortie = tk.Text(sortie_frame, height=15, bg="#0d1422", fg="#dce6f2",
                              insertbackground="white", relief="flat", wrap="word")
        self.sortie.pack(side="left", fill="both", expand=True, padx=6, pady=6)
        scroll = ttk.Scrollbar(sortie_frame, command=self.sortie.yview)
        scroll.pack(side="right", fill="y")
        self.sortie.configure(yscrollcommand=scroll.set)
        self.status_var = tk.StringVar(value="Pret.")
        tk.Label(self, textvariable=self.status_var, anchor="w", bg="#0d1422", fg="#f1c40f").pack(fill="x", padx=18, pady=(0, 12))

    def _choisir_source(self):
        chemin = filedialog.askdirectory(initialdir=self.source_var.get())
        if chemin:
            self.source_var.set(chemin)
            self.version_var.set(lire_version_source(Path(chemin)))

    def _choisir_cible(self):
        chemin = filedialog.askdirectory(initialdir=self.cible_var.get())
        if chemin:
            self.cible_var.set(chemin)

    def log(self, texte: str):
        self.sortie.insert("end", texte + "\n")
        self.sortie.see("end")
        self.update_idletasks()

    def analyser(self):
        try:
            limite = float(self.limite_var.get().replace(",", "."))
            if limite <= 0:
                raise ValueError
            source = Path(self.source_var.get()).expanduser().resolve()
            cible = Path(self.cible_var.get()).expanduser().resolve()
            depot, branche, distant = normaliser_cible_github(
                self.depot_var.get(), self.branche_var.get(), self.distant_var.get())
            self.depot_var.set(depot)
            self.branche_var.set(branche)
            self.distant_var.set(distant)
            version = self.version_var.get().strip()
            if not depot or not branche or not distant or not version:
                raise PublicationError("Depot, branche, dossier distant et version sont obligatoires.")
            files = fichiers_source(source)
            self.manifeste = construire_manifeste(source, depot, branche, distant, version,
                                                  self.notes_var.get().strip(), files)
            self.operations = construire_operations(source, cible, self.manifeste,
                                                     self.tout_var.get(), self.supprimer_var.get())
            self.lots = grouper_operations(self.operations, int(limite * 1048576))
            try:
                racine_git = Path(commande("git", "rev-parse", "--show-toplevel")).resolve()
                self.commits_en_attente = self._nombre_commits_en_attente(
                    racine_git, branche, actualiser=False) > 0
            except (OSError, PublicationError):
                self.commits_en_attente = False
            self.sortie.delete("1.0", "end")
            self.log("Source : %s" % source)
            self.log("Cible   : %s" % cible)
            self.log("Depot   : %s / %s / %s" % (depot, branche, distant))
            self.log("Fichiers source analyses : %d" % len(files))
            self.log("Fichiers a envoyer : %d" % len(self.operations))
            if self.commits_en_attente:
                self.log("Commit(s) local(aux) en attente : ils seront pousse(s) avant la fin.")
            if not self.operations:
                self.log("Aucun changement de fichier a envoyer.")
            for numero, lot in enumerate(self.lots, 1):
                poids = sum(operation.taille for operation in lot)
                noms = [operation.relatif for operation in lot[:4]]
                suffixe = " ..." if len(lot) > 4 else ""
                self.log("Lot %d/%d : %d fichier(s), %s : %s%s" % (
                    numero, len(self.lots), len(lot), taille_lisible(poids),
                    ", ".join(noms), suffixe))
            self.envoyer_btn.configure(state="normal" if (self.operations or self.commits_en_attente) else "disabled")
            self.status_var.set("Analyse terminee : %d lot(s)." % len(self.lots))
        except (OSError, ValueError, PublicationError) as erreur:
            self.envoyer_btn.configure(state="disabled")
            messagebox.showerror("Analyse impossible", str(erreur))

    def _nombre_commits_en_attente(self, racine_git: Path, branche: str, actualiser=False) -> int:
        """Compte les commits locaux pas encore presents sur origin/branche."""
        if actualiser:
            commande("git", "fetch", "origin", branche, cwd=racine_git)
        valeur = commande("git", "rev-list", "--count",
                         "origin/%s..HEAD" % branche, cwd=racine_git)
        try:
            return int(valeur.strip() or "0")
        except ValueError:
            return 0


    def _verifier_depot(self):
        racine_git = Path(commande("git", "rev-parse", "--show-toplevel")).resolve()
        cible = Path(self.cible_var.get()).expanduser().resolve()
        try:
            cible.relative_to(racine_git)
        except ValueError:
            raise PublicationError("Le dossier cible doit etre dans le depot Git local.")
        branche = self.branche_var.get().strip()
        actuelle = branche_courante()
        if actuelle != branche:
            raise PublicationError(
                "La branche locale active est « %s », pas « %s ». Change la branche dans Git "
                "ou choisis-la dans le champ avant d'envoyer." % (actuelle, branche))
        if not commande("git", "remote", "get-url", "origin", check=False):
            raise PublicationError("Aucun depot GitHub origin n'est configure.")
        return racine_git, cible

    def envoyer(self):
        if self.construction_en_cours:
            return
        if not self.operations:
            self.analyser()
        try:
            racine_git, cible = self._verifier_depot()
            if not self.operations:
                self.commits_en_attente = self._nombre_commits_en_attente(
                    racine_git, self.branche_var.get().strip(), actualiser=True) > 0
                if not self.commits_en_attente:
                    return
                # Le lot a deja ete commite avant un ancien push refuse : il
                # faut seulement le rebase/pousser, pas recopier les fichiers.
                self.lots = [[]]
        except (OSError, PublicationError) as erreur:
            messagebox.showerror("Publication impossible", str(erreur))
            return
        if not messagebox.askyesno("Confirmer la publication",
                                   "Envoyer %d lot(s) vers GitHub ?\n\n%s" %
                                   (len(self.lots), self.depot_var.get())):
            return
        self.construction_en_cours = True
        self.analyser_btn.configure(state="disabled")
        self.envoyer_btn.configure(state="disabled")
        threading.Thread(target=self._envoyer_thread, args=(racine_git, cible), daemon=True).start()

    def _envoyer_thread(self, racine_git: Path, cible: Path):
        try:
            total = len(self.lots)
            cible.mkdir(parents=True, exist_ok=True)
            for numero, lot in enumerate(self.lots, 1):
                self._interface(lambda n=numero, t=total: self.status_var.set(
                    "Envoi du lot %d/%d..." % (n, t)))
                poids = sum(operation.taille for operation in lot)
                if lot:
                    for operation in lot:
                        chemin = cible / operation.relatif
                        if operation.suppression:
                            if chemin.is_file():
                                chemin.unlink()
                            continue
                        chemin.parent.mkdir(parents=True, exist_ok=True)
                        if operation.relatif == "version_url.json":
                            chemin.write_bytes(self.manifeste)
                        else:
                            assert operation.source is not None
                            shutil.copy2(operation.source, chemin)
                    chemins_git = [str((cible / operation.relatif).relative_to(racine_git))
                                   for operation in lot]
                    commande("git", "add", "--all", "--", *chemins_git, cwd=racine_git)
                    message = "Publication jeu compiler - lot %d/%d" % (numero, total)
                    commande("git", "commit", "-m", message, cwd=racine_git)
                else:
                    self._interface(lambda: self.log("Push d'un commit local deja prepare..."))
                messages = []
                pousser_avec_rebase(racine_git, self.branche_var.get().strip(), messages.append)
                for message_git in messages:
                    self._interface(lambda message_git=message_git: self.log(message_git))
                if not lot:
                    self.commits_en_attente = False
                self._interface(lambda n=numero, t=total, p=poids: self.log(
                    "Lot %d/%d envoye : %s" % (n, t, taille_lisible(p))))
                self._interface(lambda n=numero, t=total: self.progress.configure(
                    value=int(n * 100 / max(1, t))))
            self._interface(lambda: self.status_var.set("Publication terminee. Le launcher verra la prochaine version."))
            self._interface(lambda: messagebox.showinfo("Publication terminee",
                                                          "Les fichiers ont ete envoyes vers jeucompiler."))
        except (OSError, PublicationError, AssertionError) as erreur:
            self._interface(lambda e=str(erreur): self.log("ERREUR : " + e))
            self._interface(lambda e=str(erreur): self.status_var.set("Publication interrompue."))
            self._interface(lambda e=str(erreur): messagebox.showerror("Publication interrompue", e))
        finally:
            self._interface(lambda: self.analyser_btn.configure(state="normal"))
            self._interface(lambda: self.construction_en_cours_set_false())

    def construction_en_cours_set_false(self):
        self.construction_en_cours = False
        self.envoyer_btn.configure(state="normal" if (self.operations or self.commits_en_attente) else "disabled")

    def _interface(self, callback):
        self.after(0, callback)


def main():
    application = App()
    application.mainloop()


if __name__ == "__main__":
    main()
