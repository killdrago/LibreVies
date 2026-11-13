#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
mondeLV.py : le monde vit sur le serveur.

Ce script tourne en continu, comme serveur_ia.py, et fait vivre les monstres
(déplacements, poursuite, réapparition) que les joueurs connectés soient là ou non.
Le jeu ne calcule plus les monstres : il lit leurs positions dans la base et
affiche ce qu'il reçoit. Les monstres sont marqués « tenus par le serveur »
(proprietaire_id = -1) : l'API (monde.php) refuse alors les positions envoyées
par les jeux.

Lancement (même principe que le serveur IA) :
    cd serveur
    pip install pymysql          (une fois)
    python mondeLV.py

Lit la configuration dans config.php (même dossier). Garder la fenêtre ouverte.

Limites actuelles :
  - les monstres ne tiennent compte que du village (cercle), de la rivière et des
    bords de la carte ; les arbres, rochers et barrières ne sont pas pris en compte ;
  - les monstres ne frappent pas les joueurs (les dégâts restent côté jeu) ;
  - la géométrie de la rivière et les constantes de vitesse sont recopiées du jeu
    (LibreViesGame.cs) : à garder synchronisées si le jeu change.
"""

import math
import os
import random
import re
import sys
import time

try:
    import pymysql
except ImportError:
    print("Module manquant : pip install pymysql")
    sys.exit(1)

HERE = os.path.dirname(os.path.abspath(__file__))
CONFIG = os.path.join(HERE, "config.php")

PAS_DU_MONDE = 0.5            # secondes entre deux tours de calcul
RAYON_VILLAGE = 40.0          # cercle du village (LibreViesGame.cs : VillageRadius)
MARGE_VILLAGE = 12.0          # les monstres restent a cette distance de l'enceinte
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
