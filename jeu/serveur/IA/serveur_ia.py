#!/usr/bin/env python3
"""Serveur IA local de LibreVies : repond aux PNJ avec Qwen2.5-1.5B-Instruct.

Usage (sur le PC du serveur, depuis ce dossier) :
    python serveur_ia.py

Le modele doit se trouver dans le meme dossier que ce script, sous les noms
attendus par transformers : config.json, tokenizer.json, tokenizer_config.json,
et les poids renommes en model.safetensors (voir README.md).

Route :
    POST /npc_chat   corps JSON -> {"ok": true, "reponse": "..."}
    GET  /health     -> {"ok": true, "modele_charge": true|false}

Le serveur n'ecoute que sur 127.0.0.1 : il n'est pas expose a Internet.
Les messages des joueurs ne sont jamais journalises.
"""

import json
import os
import sys
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

DOSSIER = os.path.dirname(os.path.abspath(__file__))
MODELE = os.environ.get("LIBREVIES_IA_MODELE", DOSSIER)
HOTE = os.environ.get("LIBREVIES_IA_HOTE", "127.0.0.1")
PORT = int(os.environ.get("LIBREVIES_IA_PORT", "8765"))

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
        sys.exit("Installez les dependances : pip install -r requirements.txt")

    fichiers = ["config.json", "tokenizer.json", "model.safetensors"]
    manquants = [f for f in fichiers if not os.path.isfile(os.path.join(MODELE, f))]
    if manquants:
        sys.exit("Fichiers manquants dans " + MODELE + " : " + ", ".join(manquants)
                 + "\nRenommez les poids en model.safetensors (voir README.md).")

    appareil = "cuda" if torch.cuda.is_available() else "cpu"
    dtype = torch.float16 if appareil == "cuda" else torch.float32
    print("Chargement de Qwen2.5-1.5B-Instruct sur " + appareil + "...", flush=True)
    tokenizer = AutoTokenizer.from_pretrained(MODELE)
    modele = AutoModelForCausalLM.from_pretrained(MODELE, torch_dtype=dtype)
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
    consigne = CONSIGNE_LIBRE if mode == "libre" else CONSIGNE_COURT
    identite = "Ton nom est " + nom + ". Tu habites " + (ville or "le village") + "."
    systeme = "\n\n".join([REGLE_COMMUNE, personnage, identite, consigne])

    messages = [{"role": "system", "content": systeme}]
    messages.extend(historique)
    messages.append({"role": "user", "content": message})

    texte = tokenizer.apply_chat_template(messages, tokenize=False,
                                          add_generation_prompt=True)
    entree = tokenizer(texte, return_tensors="pt").to(ETAT["appareil"])
    max_tokens = 320 if mode == "libre" else 120

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
        mode = donnees.get("mode") if donnees.get("mode") in ("court", "libre") else "court"
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


def main():
    charger_modele()
    serveur = ThreadingHTTPServer((HOTE, PORT), GestionnaireIA)
    print("Serveur IA LibreVies sur http://" + HOTE + ":" + str(PORT) + "/npc_chat",
          flush=True)
    try:
        serveur.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        serveur.server_close()


if __name__ == "__main__":
    main()
