"""Diagnostic graphique de connexion LibreVies.

Cette application ne demande aucun mot de passe et n'essaie jamais de se
connecter à MySQL avec des identifiants. Elle teste uniquement DNS, TCP et
HTTP afin de déterminer si le problème vient du PC, du pare-feu, du routeur,
du chemin de l'API ou de PHP.
"""
from __future__ import print_function

import datetime
import http.client
import json
import os
import platform
import socket
import sys
import threading
import tkinter as tk
import tkinter.scrolledtext as scrolledtext
import urllib.error
import urllib.parse
import urllib.request

DEFAULT_LOCAL_URL = "http://localhost/serveur/api.php?action=health"
DEFAULT_REMOTE_URL = "http://92.133.115.121/serveur/api.php?action=health"
DEFAULT_PORTS = "80,443,3306"
TIMEOUT = 8
USER_AGENT = "LibreVies-Connection-Diagnostic/1.0"


class Diagnostic:
    def __init__(self, local_url, remote_url, ports):
        self.local_url = normaliser_url(local_url)
        self.remote_url = normaliser_url(remote_url)
        self.ports = ports
        self.lines = []

    def log(self, message=""):
        self.lines.append(message)

    def run(self, mode="all"):
        self.log("DIAGNOSTIC DE CONNEXION LIBREVIES")
        self.log("=" * 42)
        self.log("Date : %s" % datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S"))
        self.log("Ordinateur : %s" % platform.platform())
        self.log("Python : %s" % sys.version.split()[0])
        self.log("Dossier : %s" % os.getcwd())
        self.log("Aucun mot de passe n'a ete utilise ou enregistre.")
        self.log("")

        self._reseau_local()
        if mode in ("all", "local"):
            self._tester_cible("URL LOCALE", self.local_url)
        if mode in ("all", "remote"):
            self._tester_cible("URL AVEC IP / DOMAINE", self.remote_url)
        self._conclusion(mode)
        return "\n".join(self.lines)

    def _reseau_local(self):
        self.log("INFORMATIONS RESEAU LOCAL")
        self.log("-" * 42)
        try:
            nom = socket.gethostname()
            self.log("Nom du PC : %s" % nom)
            adresses = []
            for famille, _, _, _, adresse in socket.getaddrinfo(nom, None):
                if famille == socket.AF_INET and adresse[0] not in adresses:
                    adresses.append(adresse[0])
            if adresses:
                self.log("IPv4 detectees : %s" % ", ".join(adresses))
            else:
                self.log("IPv4 detectees : aucune")
        except Exception as erreur:
            self.log("Impossible de lire l'IPv4 locale : %s" % erreur)
        try:
            with urllib.request.urlopen("https://api.ipify.org", timeout=TIMEOUT) as reponse:
                ip_publique = reponse.read().decode("ascii", "replace").strip()
            self.log("IP publique vue depuis Internet : %s" % ip_publique)
        except Exception as erreur:
            self.log("IP publique non determinee : %s" % erreur)
        self.log("")

    def _tester_cible(self, titre, url):
        self.log(titre)
        self.log("-" * 42)
        if not url:
            self.log("URL absente.")
            self.log("")
            return
        self.log("URL testee : %s" % url)
        try:
            morceaux = urllib.parse.urlsplit(url)
            if morceaux.scheme not in ("http", "https") or not morceaux.hostname:
                raise ValueError("URL HTTP/HTTPS invalide")
            hote = morceaux.hostname
            port_url = morceaux.port or (443 if morceaux.scheme == "https" else 80)
        except Exception as erreur:
            self.log("ERREUR URL : %s" % erreur)
            self.log("Diagnostic : corrige l'URL dans le champ de saisie.")
            self.log("")
            return

        self._dns(hote)
        ports = list(self.ports)
        if port_url not in ports:
            ports.insert(0, port_url)
        self.log("Tests TCP vers %s :" % hote)
        for port in ports:
            self._tcp(hote, port)
        self._http(url)
        self.log("")

    def _dns(self, hote):
        try:
            adresses = sorted(set(socket.gethostbyname_ex(hote)[2]))
            self.log("DNS : OK -> %s" % ", ".join(adresses))
        except socket.gaierror as erreur:
            self.log("DNS : ECHEC -> %s" % erreur)
            self.log("Diagnostic : le nom ou l'adresse n'est pas resolu(e).")
        except Exception as erreur:
            self.log("DNS : ERREUR -> %s" % erreur)

    def _tcp(self, hote, port):
        try:
            with socket.create_connection((hote, port), timeout=TIMEOUT):
                self.log("  TCP %s : OUVERT / joignable" % port)
        except socket.timeout:
            self.log("  TCP %s : TIMEOUT" % port)
            self.log("    Diagnostic : pare-feu, routeur, port non redirige ou CGNAT possible.")
        except ConnectionRefusedError:
            self.log("  TCP %s : REFUSE" % port)
            self.log("    Diagnostic : la machine repond, mais aucun service n'ecoute sur ce port.")
        except OSError as erreur:
            self.log("  TCP %s : ECHEC -> %s" % (port, erreur))

    def _http(self, url):
        self.log("HTTP :")
        requete = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
        try:
            with urllib.request.urlopen(requete, timeout=TIMEOUT) as reponse:
                contenu = reponse.read(4096).decode("utf-8", "replace")
                self._http_resultat(reponse.status, reponse.headers, contenu)
        except urllib.error.HTTPError as erreur:
            contenu = ""
            try:
                contenu = erreur.read(4096).decode("utf-8", "replace")
            except Exception:
                pass
            self._http_resultat(erreur.code, erreur.headers, contenu)
        except urllib.error.URLError as erreur:
            self.log("  HTTP : ECHEC -> %s" % erreur.reason)
            self.log("  Diagnostic : impossible d'obtenir une reponse HTTP.")
        except socket.timeout:
            self.log("  HTTP : TIMEOUT")
            self.log("  Diagnostic : serveur inaccessible ou port bloque.")
        except Exception as erreur:
            self.log("  HTTP : ECHEC -> %s" % erreur)

    def _http_resultat(self, code, headers, contenu):
        self.log("  Code HTTP : %s" % code)
        serveur = headers.get("Server") if headers else None
        if serveur:
            self.log("  Serveur : %s" % serveur)
        apercu = " ".join(contenu.strip().split())[:500]
        self.log("  Reponse : %s" % (apercu or "(vide)"))
        if code == 200:
            try:
                donnees = json.loads(contenu)
                if isinstance(donnees, dict) and donnees.get("ok"):
                    self.log("  API LibreVies : OK")
                elif isinstance(donnees, dict):
                    self.log("  API LibreVies : erreur -> %s" % donnees.get("message", "sans message"))
            except ValueError:
                self.log("  Diagnostic : HTTP fonctionne, mais la reponse n'est pas du JSON API.")
        elif code == 404:
            self.log("  Diagnostic : serveur web joignable, mais chemin/fichier introuvable.")
        elif code >= 500:
            self.log("  Diagnostic : PHP/API ou connexion a la base en erreur cote serveur.")
        elif code in (401, 403):
            self.log("  Diagnostic : acces refuse par le serveur ou le pare-feu applicatif.")

    def _conclusion(self, mode):
        self.log("CONCLUSION")
        self.log("-" * 42)
        if mode == "local":
            self.log("Si l'URL locale echoue : verifie le serveur web et le chemin /serveur/api.php.")
        elif mode == "remote":
            self.log("Si le TCP/HTTP distant echoue : verifie port, pare-feu, NAT et CGNAT.")
        else:
            self.log("Compare les blocs LOCAL et IP pour savoir ou la connexion se casse.")
        self.log("Pour le launcher, le port MySQL 3306 n'a pas besoin d'etre expose sur Internet.")
        self.log("Copie-colle tout ce rapport pour le diagnostic.")


def normaliser_url(valeur):
    valeur = (valeur or "").strip()
    if valeur and "://" not in valeur:
        valeur = "http://" + valeur
    return valeur


def lire_ports(texte):
    resultat = []
    for morceau in (texte or "").replace(";", ",").split(","):
        morceau = morceau.strip()
        if not morceau:
            continue
        try:
            port = int(morceau)
            if 1 <= port <= 65535 and port not in resultat:
                resultat.append(port)
        except ValueError:
            pass
    return resultat or [80, 443, 3306]


class Application(tk.Tk):
    def __init__(self):
        tk.Tk.__init__(self)
        self.title("Diagnostic connexion LibreVies")
        self.geometry("940x700")
        self.minsize(760, 560)
        self.configure(bg="#172033")
        self._construire()

    def _construire(self):
        tk.Label(self, text="DIAGNOSTIC DE CONNEXION LIBREVIES",
                 font=("Segoe UI", 16, "bold"), fg="#f1c40f", bg="#172033").pack(
                     anchor="w", padx=16, pady=(14, 4))
        tk.Label(self, text="Aucun mot de passe n'est demande. Le test verifie DNS, TCP et HTTP.",
                 font=("Segoe UI", 9), fg="#c8d1dc", bg="#172033").pack(
                     anchor="w", padx=16, pady=(0, 10))

        cadre = tk.Frame(self, bg="#202b42")
        cadre.pack(fill="x", padx=16, pady=(0, 10))
        cadre.columnconfigure(1, weight=1)
        self.local = self._champ(cadre, 0, "URL locale", DEFAULT_LOCAL_URL)
        self.remote = self._champ(cadre, 1, "URL avec IP / domaine", DEFAULT_REMOTE_URL)
        self.ports = self._champ(cadre, 2, "Ports TCP a tester", DEFAULT_PORTS)

        boutons = tk.Frame(self, bg="#172033")
        boutons.pack(fill="x", padx=16, pady=(0, 8))
        self.bouton_tout = tk.Button(boutons, text="TESTER TOUT", command=lambda: self._lancer("all"),
                                     bg="#2f8f58", fg="white", relief="flat",
                                     font=("Segoe UI", 10, "bold"), padx=12)
        self.bouton_tout.pack(side="left", padx=(0, 6))
        self.bouton_local = tk.Button(boutons, text="TESTER LOCAL", command=lambda: self._lancer("local"),
                                      bg="#356ca8", fg="white", relief="flat",
                                      font=("Segoe UI", 10, "bold"), padx=12)
        self.bouton_local.pack(side="left", padx=6)
        self.bouton_ip = tk.Button(boutons, text="TESTER IP", command=lambda: self._lancer("remote"),
                                   bg="#356ca8", fg="white", relief="flat",
                                   font=("Segoe UI", 10, "bold"), padx=12)
        self.bouton_ip.pack(side="left", padx=6)
        tk.Button(boutons, text="COPIER LE RAPPORT", command=self._copier,
                  bg="#6d5a2e", fg="white", relief="flat",
                  font=("Segoe UI", 10, "bold"), padx=12).pack(side="right")
        tk.Button(boutons, text="EFFACER", command=self._effacer,
                  bg="#555b66", fg="white", relief="flat",
                  font=("Segoe UI", 10, "bold"), padx=12).pack(side="right", padx=6)

        self.etat = tk.Label(self, text="Pret.", anchor="w", fg="#c8d1dc", bg="#172033")
        self.etat.pack(fill="x", padx=16)
        self.resultat = scrolledtext.ScrolledText(
            self, wrap="word", bg="#0d1220", fg="#e8edf2", insertbackground="white",
            font=("Consolas", 10), relief="flat", padx=10, pady=10)
        self.resultat.pack(fill="both", expand=True, padx=16, pady=(4, 16))
        self.resultat.insert("1.0", "Clique sur TESTER TOUT puis copie le rapport complet.\n")

    def _champ(self, parent, ligne, texte, valeur):
        tk.Label(parent, text=texte + " :", fg="#f2f4f8", bg="#202b42",
                 font=("Segoe UI", 9, "bold"), anchor="w").grid(
                     row=ligne, column=0, sticky="w", padx=10, pady=6)
        entree = tk.Entry(parent, bg="#0d1220", fg="white", insertbackground="white",
                          relief="flat", font=("Consolas", 10))
        entree.insert(0, valeur)
        entree.grid(row=ligne, column=1, sticky="ew", padx=(8, 10), pady=6)
        return entree

    def _lancer(self, mode):
        self._verrouiller(True)
        self.resultat.delete("1.0", "end")
        self.resultat.insert("1.0", "Tests en cours...\n")
        self.etat.config(text="Tests en cours, ne ferme pas cette fenetre...")
        diagnostic = Diagnostic(self.local.get(), self.remote.get(),
                                lire_ports(self.ports.get()))
        threading.Thread(target=self._travail, args=(diagnostic, mode), daemon=True).start()

    def _travail(self, diagnostic, mode):
        try:
            rapport = diagnostic.run(mode)
        except Exception as erreur:
            rapport = "ERREUR INTERNE DU DIAGNOSTIC : %s" % erreur
        self.after(0, lambda: self._terminer(rapport))

    def _terminer(self, rapport):
        self.resultat.delete("1.0", "end")
        self.resultat.insert("1.0", rapport)
        self.etat.config(text="Diagnostic termine. Clique sur COPIER LE RAPPORT.")
        self._verrouiller(False)

    def _verrouiller(self, verrouille):
        etat = "disabled" if verrouille else "normal"
        for bouton in (self.bouton_tout, self.bouton_local, self.bouton_ip):
            bouton.config(state=etat)

    def _copier(self):
        try:
            self.clipboard_clear()
            self.clipboard_append(self.resultat.get("1.0", "end-1c"))
            self.etat.config(text="Rapport copie dans le presse-papiers.")
        except Exception as erreur:
            self.etat.config(text="Copie impossible : %s" % erreur)

    def _effacer(self):
        self.resultat.delete("1.0", "end")
        self.etat.config(text="Pret.")


if __name__ == "__main__":
    Application().mainloop()
