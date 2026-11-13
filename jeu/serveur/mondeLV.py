#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
mondeLV.py : un seul programme pour le serveur LibreVies.

  1. Le monde vit en permanence : les monstres se deplacent, poursuivent, reapparaissent,
     que des joueurs soient connectes ou non. Le jeu ne fait que lire leurs positions.
     Les monstres sont marques proprietaire_id = -1 : monde.php refuse le pilotage des jeux.
  2. Le serveur IA des PNJ (Qwen2.5-1.5B-Instruct) tourne dans le meme programme,
     sur http://127.0.0.1:8765/npc_chat. Les poids se trouvent dans le dossier IA
     (voir IA/README.md). Sans poids ou sans torch, le monde tourne quand meme.

Lancement (sur le PC serveur, dans ce dossier) :
    pip install pymysql              (une fois)
    python mondeLV.py                (ou demarrer_mondeLV.bat)
Lit la configuration dans config.php (meme dossier). Garder la fenetre ouverte.

Limites actuelles :
  - les monstres ne tiennent compte que du village (cercle d'enceinte), de la riviere et
    des bords de la carte ; les arbres, rochers et barrieres ne sont pas pris en compte ;
  - les monstres ne frappent pas les joueurs (les degats restent cote jeu) ;
  - la geometrie de la riviere et les vitesses sont recopiees de LibreViesGame.cs.
"""

import json
import math
import os
import random
import re
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

try:
    import pymysql
except ImportError:
    print("Module manquant : pip install pymysql")
    sys.exit(1)

HERE = os.path.dirname(os.path.abspath(__file__))
CONFIG = os.path.join(HERE, "config.php")

# ---- Serveur IA des PNJ (ex serveur_ia.py) ----
IA_MODELE = os.environ.get("LIBREVIES_IA_MODELE", os.path.join(HERE, "IA"))
IA_HOTE = os.environ.get("LIBREVIES_IA_HOTE", "127.0.0.1")
IA_PORT = int(os.environ.get("LIBREVIES_IA_PORT", "8765"))

PAS_DU_MONDE = 0.5            # secondes entre deux tours de calcul
RAYON_VILLAGE = 40.0          # cercle du village (LibreViesGame.cs : VillageRadius)
MARGE_VILLAGE = 1.0           # les monstres vont jusqu'au mur (a cote de l'enceinte)
LIMITE_CARTE = 119.0          # bords de la carte utilisables pour les monstres
MARGE_EAU = 0.85              # MargeEauMonstres
LARGEUR_MIN_RIVIERE = 5.0
LARGEUR_MAX_RIVIERE = 10.0
PORTEE_AGRESSIF = 10.0        # poursuite d'un monstre agressif
PORTEE_PROVOQUE = 25.0        # poursuite d'un monstre frappe
DUREE_PROVOCATION = 10.0
ARRET_PRES_JOUEUR = 1.5       # distance a laquelle la bete s'arrete
DELAI_RESURRECTION = 10.0     # secondes avant la reapparition
DISTANCE_MIN_JOUEUR_RESURRECTION = 12.0
VITESSE_MIN = 1.2             # m/s, comme le jeu
VITESSE_MAX = 2.0
FACTEUR_BALADE = 0.6
FACTEUR_POURSUITE = 1.4
TEMPS_AFFICHAGE_BILAN = 30.0

# Tracé de la rivière principale (LibreViesGame.cs : RivierePrincipale).
RIVIERE = [
    (-118, 72), (-96, 64), (-65, 61), (-30, 60), (8, 60), (43, 60),
    (64, 45), (66, 10), (55, -30), (82, -68), (118, -108),
]

# Type de monstre -> identifiant dans la table monstre (id_monstre_du_type dans monde.php).
ID_TYPE = {"rat": 1, "souris": 2, "araignee": 3}
TYPES_DEFAUT_AGRESSIFS = {"rat": True, "souris": False, "araignee": True}



# ==================================================================
# Serveur IA des PNJ
# ==================================================================

MAX_MESSAGE = 400          # caracteres par message du joueur
MAX_NOM = 60
MAX_HISTORIQUE = 10        # messages gardes du dialogue en cours
MAX_CORPS = 20000          # octets acceptes dans une requete

REGLE_COMMUNE = (
    "Tu joues un habitant du village de LibreVies. Reponds toujours en francais, "
    "a la premiere personne et dans le role de ton personnage. Ne dis jamais que "
    "tu es une IA, un modele ou un programme. Si on te pose une question hors du "
    "jeu, reponds brievement en restant dans ton role et reviens au village. "
    "N'invente pas de prix, de dates ou de chiffres precis."
)

CONSIGNE_COURT = (
    "Reponds en une a trois phrases courtes, comme une replique de jeu video."
)
CONSIGNE_TRES_COURT = (
    "Reponds en une seule phrase tres courte, de moins de quinze mots."
)
CONSIGNE_LIBRE = (
    "Tu peux tenir une conversation plus developpee : trois a six phrases, "
    "en posant parfois une question en retour."
)

PERSONNAGES = {
    "maire": "Tu es le maire du village. Tu es sage et un peu solennel. Tu parles "
             "de la vie du village et de ses habitants.",
    "forgeron": "Tu es le forgeron du village. Tu es direct, travailleur, fier de "
                "ton metier. Tu parles de metal, de charbon et d'outils.",
    "marchand": "Tu es le marchand du village. Tu es commercant, souriant, tu "
                "aimes raconter les nouvelles qui circulent sur les routes.",
    "vendeur": "Tu es le marchand du village. Tu es commercant, souriant, tu "
               "aimes raconter les nouvelles qui circulent sur les routes.",
    "esthetique": "Tu es la conseillere en apparence du village. Tu es attentive, "
                  "aimable, tu aides les voyageurs a se presenter et a se "
                  "faire une belle image.",
    "medecin": "Tu es le medecin du village. Tu es calme et rassurant. Tu "
               "parles de soins, de repos et de prudence.",
}
PERSONNAGE_DEFAUT = "Tu es un habitant ordinaire du village, curieux et accueillant."

VERROU = threading.Lock()
ETAT = {"modele": None, "tokenizer": None, "appareil": "cpu"}


def charger_modele():
    """Charge le modele une seule fois, au demarrage du serveur."""
    try:
        import torch
        from transformers import AutoModelForCausalLM, AutoTokenizer
    except ImportError:
        print("IA desactivee : pip install -r IA/requirements.txt", flush=True)
        return

    fichiers = ["config.json", "tokenizer.json", "model.safetensors"]
    manquants = [f for f in fichiers if not os.path.isfile(os.path.join(IA_MODELE, f))]
    if manquants:
        print("IA desactivee : fichiers manquants dans " + IA_MODELE + " : " + ", ".join(manquants)
              + " (voir IA/README.md)", flush=True)
        return

    appareil = "cuda" if torch.cuda.is_available() else "cpu"
    dtype = torch.float16 if appareil == "cuda" else torch.float32
    print("Chargement de Qwen2.5-1.5B-Instruct sur " + appareil + "...", flush=True)
    tokenizer = AutoTokenizer.from_pretrained(IA_MODELE)
    modele = AutoModelForCausalLM.from_pretrained(IA_MODELE, torch_dtype=dtype)
    modele.to(appareil)
    modele.eval()
    ETAT.update(modele=modele, tokenizer=tokenizer, appareil=appareil)
    print("Modele pret.", flush=True)


def texte_propre(valeur, limite):
    """Retourne une chaine nettoyee, ou None si elle est absente ou invalide."""
    if not isinstance(valeur, str):
        return None
    valeur = valeur.strip()
    if not valeur or len(valeur) > limite:
        return None
    if any(ord(c) < 32 or ord(c) == 127 for c in valeur):
        return None
    return valeur


def historique_valide(brut):
    if brut is None:
        return []
    if not isinstance(brut, list):
        return None
    propre = []
    for entree in brut[-MAX_HISTORIQUE:]:
        if not isinstance(entree, dict):
            return None
        role = entree.get("role")
        contenu = texte_propre(entree.get("content"), MAX_MESSAGE)
        if role not in ("user", "assistant") or contenu is None:
            return None
        propre.append({"role": role, "content": contenu})
    return propre


def generer_reponse(metier, nom, ville, mode, message, historique):
    tokenizer = ETAT["tokenizer"]
    modele = ETAT["modele"]

    personnage = PERSONNAGES.get(metier, PERSONNAGE_DEFAUT)
    consigne = {"libre": CONSIGNE_LIBRE, "tres_court": CONSIGNE_TRES_COURT}.get(mode, CONSIGNE_COURT)
    identite = "Ton nom est " + nom + ". Tu habites " + (ville or "le village") + "."
    systeme = "\n\n".join([REGLE_COMMUNE, personnage, identite, consigne])

    messages = [{"role": "system", "content": systeme}]
    messages.extend(historique)
    messages.append({"role": "user", "content": message})

    texte = tokenizer.apply_chat_template(messages, tokenize=False,
                                          add_generation_prompt=True)
    entree = tokenizer(texte, return_tensors="pt").to(ETAT["appareil"])
    # Limite en tokens (morceaux de mots), pas en caracteres : 60 tokens ~ 200 caracteres.
    max_tokens = {"libre": 320, "tres_court": 60}.get(mode, 120)

    import torch
    with VERROU, torch.no_grad():
        sortie = modele.generate(
            **entree,
            max_new_tokens=max_tokens,
            do_sample=True,
            temperature=0.7,
            top_p=0.9,
            repetition_penalty=1.1,
            pad_token_id=tokenizer.eos_token_id,
        )
    nouveaux = sortie[0][entree["input_ids"].shape[1]:]
    return tokenizer.decode(nouveaux, skip_special_tokens=True).strip()


class GestionnaireIA(BaseHTTPRequestHandler):
    server_version = "LibreViesIA/1.0"

    def _envoyer(self, code, donnees):
        corps = json.dumps(donnees, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(corps)))
        self.end_headers()
        self.wfile.write(corps)

    def do_GET(self):
        if self.path.split("?")[0] == "/health":
            self._envoyer(200, {"ok": True, "modele_charge": ETAT["modele"] is not None})
        else:
            self._envoyer(404, {"ok": False, "message": "Route inconnue."})

    def do_POST(self):
        if self.path.split("?")[0] != "/npc_chat":
            self._envoyer(404, {"ok": False, "message": "Route inconnue."})
            return
        try:
            longueur = int(self.headers.get("Content-Length") or 0)
        except ValueError:
            longueur = 0
        if longueur <= 0 or longueur > MAX_CORPS:
            self._envoyer(400, {"ok": False, "message": "Requete invalide."})
            return
        try:
            donnees = json.loads(self.rfile.read(longueur).decode("utf-8"))
        except (ValueError, UnicodeDecodeError):
            self._envoyer(400, {"ok": False, "message": "JSON invalide."})
            return
        if not isinstance(donnees, dict):
            self._envoyer(400, {"ok": False, "message": "JSON invalide."})
            return

        metier = texte_propre(donnees.get("metier"), 40)
        nom = texte_propre(donnees.get("nom"), MAX_NOM) or "Habitant"
        ville = texte_propre(donnees.get("ville"), MAX_NOM) or ""
        message = texte_propre(donnees.get("message"), MAX_MESSAGE)
        mode = donnees.get("mode") if donnees.get("mode") in ("tres_court", "court", "libre") else "court"
        historique = historique_valide(donnees.get("historique"))
        if metier is None or message is None or historique is None:
            self._envoyer(400, {"ok": False, "message": "Message ou contexte invalide."})
            return
        if ETAT["modele"] is None:
            self._envoyer(503, {"ok": False, "message": "Le modele n'est pas encore charge."})
            return

        try:
            reponse = generer_reponse(metier.lower(), nom, ville, mode, message, historique)
        except Exception as erreur:  # pas de details internes vers le jeu
            print("Erreur de generation : " + type(erreur).__name__, flush=True)
            self._envoyer(500, {"ok": False, "message": "Generation impossible."})
            return
        if not reponse:
            reponse = "Hum... Pardonnez-moi, je n'ai pas bien saisi. Redites-moi cela ?"
        self._envoyer(200, {"ok": True, "reponse": reponse})

    def log_message(self, format, *args):
        # Silence volontaire : pas de journal des requetes ni des messages.
        pass



def demarrer_ia():
    """Ecoute les requetes des PNJ et charge le modele (dans un thread separe).
    Le serveur repond meme sans modele : /health indique alors modele_charge = false."""
    try:
        serveur = ThreadingHTTPServer((IA_HOTE, IA_PORT), GestionnaireIA)
    except OSError as erreur:
        print("IA : port " + str(IA_PORT) + " indisponible : " + str(erreur), flush=True)
        return
    print("Serveur IA LibreVies sur http://" + IA_HOTE + ":" + str(IA_PORT) + "/npc_chat",
          flush=True)
    threading.Thread(target=charger_modele, daemon=True).start()
    serveur.serve_forever()


def lire_config():
    """Lit les valeurs simples de config.php (sans l'interpréter)."""
    with open(CONFIG, encoding="utf-8") as fichier:
        texte = fichier.read()
    valeurs = {}
    for correspondance in re.finditer(r"'(\w+)'\s*=>\s*(?:'([^']*)'|(-?\d+))", texte):
        if correspondance.group(2) is not None:
            valeurs[correspondance.group(1)] = correspondance.group(2)
        else:
            valeurs[correspondance.group(1)] = int(correspondance.group(3))
    for cle in ("db_host", "db_port", "db_user", "db_password", "db_name", "monde_db_name"):
        if cle not in valeurs:
            raise RuntimeError("config.php : valeur manquante : " + cle)
    for cle in ("db_name", "monde_db_name"):
        if not re.match(r"^\w+$", str(valeurs[cle])):
            raise RuntimeError("config.php : nom de base invalide : " + str(valeurs[cle]))
    return valeurs


def largeur_riviere(ax, az):
    onde = math.sin(ax * 0.037 + 0.6) * math.cos(az * 0.029 + 1.3)
    onde2 = math.sin(ax * 0.093 + az * 0.061) * 0.35
    return min(LARGEUR_MAX_RIVIERE, max(LARGEUR_MIN_RIVIERE, 7.5 + 2.5 * onde + onde2))


def point_riviere(x, z):
    """Point de l'axe de la rivière le plus proche de (x, z)."""
    meilleur = RIVIERE[0]
    meilleure_d2 = float("inf")
    for i in range(len(RIVIERE) - 1):
        ax, az = RIVIERE[i]
        bx, bz = RIVIERE[i + 1]
        abx, abz = bx - ax, bz - az
        longueur2 = max(abx * abx + abz * abz, 0.001)
        t = max(0.0, min(1.0, ((x - ax) * abx + (z - az) * abz) / longueur2))
        px, pz = ax + abx * t, az + abz * t
        d2 = (x - px) ** 2 + (z - pz) ** 2
        if d2 < meilleure_d2:
            meilleure_d2 = d2
            meilleur = (px, pz)
    return meilleur


def zone_interdite(x, z):
    """Vrai si une bête ne peut pas se trouver ici (hors carte, village, rivière)."""
    if abs(x) > LIMITE_CARTE or abs(z) > LIMITE_CARTE:
        return True
    if math.hypot(x, z) < RAYON_VILLAGE + MARGE_VILLAGE:
        return True
    px, pz = point_riviere(x, z)
    if math.hypot(x - px, z - pz) <= largeur_riviere(px, pz) * 0.5 + MARGE_EAU:
        return True
    return False


def point_libre_au_hasard(joueurs):
    """Point valide pour une réapparition, loin des joueurs si possible."""
    solution = None
    for _ in range(300):
        x = random.uniform(-LIMITE_CARTE, LIMITE_CARTE)
        z = random.uniform(-LIMITE_CARTE, LIMITE_CARTE)
        if zone_interdite(x, z):
            continue
        solution = (x, z)
        if all(math.hypot(x - jx, z - jz) >= DISTANCE_MIN_JOUEUR_RESURRECTION for jx, jz in joueurs):
            return solution
    return solution


class Monde:
    def __init__(self, config):
        self.config = config
        self.base_monde = config["monde_db_name"]
        self.base_stats = config["db_name"]
        self.connexion = None
        self.etats = {}
        self.agressifs = dict(TYPES_DEFAUT_AGRESSIFS)
        self.dernier_tour = None
        self.prochain_bilan = 0.0
        self.prochaines_stats = 0.0

    # ---- base de données ----

    def connecter(self):
        self.connexion = pymysql.connect(
            host=str(self.config["db_host"]),
            port=int(self.config["db_port"]),
            user=str(self.config["db_user"]),
            password=str(self.config["db_password"]),
            database=self.base_monde,
            charset="utf8mb4",
            autocommit=False,
            cursorclass=pymysql.cursors.DictCursor,
        )
        print("mondeLV : connecte a la base " + self.base_monde)

    def lire_stats(self, curseur):
        """Agressivité par type, lue dans la table monstre (base principale)."""
        try:
            curseur.execute("SELECT id, agressif FROM `" + self.base_stats + "`.monstre WHERE id IN (1, 2, 3)")
            for ligne in curseur.fetchall():
                for type_monstre, identifiant in ID_TYPE.items():
                    if int(ligne["id"]) == identifiant:
                        self.agressifs[type_monstre] = int(ligne["agressif"]) == 1
        except pymysql.MySQLError as erreur:
            print("mondeLV : table monstre illisible, valeurs par defaut : " + str(erreur))

    def prochaine_revision(self, curseur):
        curseur.execute("UPDATE monde_compteur SET revision = LAST_INSERT_ID(revision + 1) WHERE id = 1")
        curseur.execute("SELECT LAST_INSERT_ID() AS revision")
        return int(curseur.fetchone()["revision"])

    # ---- un tour de calcul ----

    def tour(self):
        maintenant = time.monotonic()
        dt = PAS_DU_MONDE if self.dernier_tour is None else min(2.0, maintenant - self.dernier_tour)
        self.dernier_tour = maintenant

        # Commit en début de tour : sinon la lecture reste figée sur l'ancienne image de la base.
        self.connexion.commit()
        curseur = self.connexion.cursor()

        if maintenant >= self.prochaines_stats:
            self.lire_stats(curseur)
            self.prochaines_stats = maintenant + 60.0

        curseur.execute("SELECT position_x AS x, position_z AS z FROM monde_presence"
                        " WHERE mis_a_jour > NOW() - INTERVAL 10 SECOND")
        joueurs = [(float(ligne["x"]), float(ligne["z"])) for ligne in curseur.fetchall()]

        curseur.execute("SELECT cle, type, position_x, position_z, rotation_y, pv, pv_max, mort,"
                        " TIMESTAMPDIFF(SECOND, modifie_le, NOW()) AS age FROM monde_monstre")
        lignes = curseur.fetchall()

        # Les monstres sont tenus par le serveur : les jeux ne peuvent plus les piloter.
        curseur.execute("UPDATE monde_monstre SET proprietaire_id = -1"
                        " WHERE proprietaire_id IS NULL OR proprietaire_id <> -1")
        changements_proprietaire = curseur.rowcount

        deplacements = []
        reapparitions = []
        vivants = 0
        for ligne in lignes:
            cle = ligne["cle"]
            type_monstre = ligne["type"]
            x = float(ligne["position_x"])
            z = float(ligne["position_z"])
            pv = int(ligne["pv"])
            pv_max = int(ligne["pv_max"])

            etat = self.etats.get(cle)
            if etat is None:
                etat = {
                    "pv": pv,
                    "vitesse": random.uniform(VITESSE_MIN, VITESSE_MAX),
                    "cap": random.uniform(0.0, 2.0 * math.pi),
                    "timer": random.uniform(0.0, 4.0),
                    "provoque": 0.0,
                }
                self.etats[cle] = etat

            if int(ligne["mort"]) == 1:
                if int(ligne["age"]) >= DELAI_RESURRECTION:
                    point = point_libre_au_hasard(joueurs)
                    if point is not None:
                        reapparitions.append((cle, point[0], point[1], pv_max))
                        etat["pv"] = pv_max
                        etat["provoque"] = 0.0
                continue

            vivants += 1
            if pv < etat["pv"]:
                etat["provoque"] = maintenant + DUREE_PROVOCATION
            etat["pv"] = pv

            # Cible la plus proche parmi les joueurs connectés.
            distance_min = float("inf")
            cible = None
            for jx, jz in joueurs:
                distance = math.hypot(jx - x, jz - z)
                if distance < distance_min:
                    distance_min = distance
                    cible = (jx, jz)

            agressif = self.agressifs.get(type_monstre, True)
            poursuite = cible is not None and (
                (agressif and distance_min < PORTEE_AGRESSIF)
                or (maintenant < etat["provoque"] and distance_min < PORTEE_PROVOQUE)
            )

            if poursuite:
                if distance_min < ARRET_PRES_JOUEUR:
                    dx, dz = 0.0, 0.0
                else:
                    dx = (cible[0] - x) / distance_min
                    dz = (cible[1] - z) / distance_min
                vitesse = etat["vitesse"] * FACTEUR_POURSUITE
            else:
                etat["timer"] -= dt
                if etat["timer"] <= 0.0:
                    etat["cap"] = random.uniform(0.0, 2.0 * math.pi)
                    etat["timer"] = random.uniform(1.5, 4.0)
                dx, dz = math.cos(etat["cap"]), math.sin(etat["cap"])
                vitesse = etat["vitesse"] * FACTEUR_BALADE

            pas = vitesse * dt
            if pas <= 0.0 or (dx == 0.0 and dz == 0.0):
                continue

            nx, nz = x + dx * pas, z + dz * pas
            if zone_interdite(nx, nz):
                # Contournement : on essaie des virages de plus en plus grands.
                trouve = False
                for angle in (30, -30, 60, -60, 90, -90, 120, -120, 150, -150, 180):
                    c = math.cos(math.radians(angle))
                    s = math.sin(math.radians(angle))
                    ddx, ddz = dx * c - dz * s, dx * s + dz * c
                    tx, tz = x + ddx * pas, z + ddz * pas
                    if not zone_interdite(tx, tz):
                        dx, dz, nx, nz = ddx, ddz, tx, tz
                        trouve = True
                        break
                if not trouve:
                    etat["timer"] = 0.0
                    continue
                etat["cap"] = math.atan2(dz, dx)

            if abs(nx - x) + abs(nz - z) > 0.001:
                rotation = math.degrees(math.atan2(dx, dz))
                deplacements.append((round(nx, 3), round(nz, 3), round(rotation, 3), cle))

        if deplacements or reapparitions or changements_proprietaire:
            revision = self.prochaine_revision(curseur)
            if deplacements:
                curseur.executemany(
                    "UPDATE monde_monstre SET position_x = %s, position_z = %s, rotation_y = %s,"
                    " revision = " + str(revision) + " WHERE cle = %s",
                    deplacements)
            for cle, x, z, pv_max in reapparitions:
                curseur.execute(
                    "UPDATE monde_monstre SET position_x = %s, position_z = %s, rotation_y = 0,"
                    " pv = %s, mort = 0, proprietaire_id = -1, pv_modifie_le = NOW(),"
                    " revision = " + str(revision) + " WHERE cle = %s",
                    (round(x, 3), round(z, 3), pv_max, cle))
        self.connexion.commit()

        if maintenant >= self.prochain_bilan:
            self.prochain_bilan = maintenant + TEMPS_AFFICHAGE_BILAN
            print("mondeLV : joueurs connectes {} ; monstres {} (vivants {}) ; deplaces ce tour {} ; "
                  "reapparus {}".format(len(joueurs), len(lignes), vivants,
                                        len(deplacements), len(reapparitions)))


def main():
    config = lire_config()
    threading.Thread(target=demarrer_ia, daemon=True).start()
    monde = Monde(config)
    print("mondeLV : le monde tourne (un tour toutes les {} s). Ctrl+C pour arreter.".format(PAS_DU_MONDE))
    while True:
        debut = time.monotonic()
        try:
            if monde.connexion is None:
                monde.connecter()
            monde.tour()
        except KeyboardInterrupt:
            print("mondeLV : arret.")
            break
        except Exception as erreur:
            print("mondeLV : erreur, nouvel essai dans 5 s : " + str(erreur))
            try:
                if monde.connexion is not None:
                    monde.connexion.close()
            except Exception:
                pass
            monde.connexion = None
            time.sleep(5)
            continue
        time.sleep(max(0.0, PAS_DU_MONDE - (time.monotonic() - debut)))


if __name__ == "__main__":
    main()
