#!/usr/bin/env python3
"""Test en ligne de commande : parler a un PNJ sans lancer Unity.

    python tester_pnj.py maire
    python tester_pnj.py forgeron libre
"""
import json
import os
import sys
import urllib.request

PORT = os.environ.get("LIBREVIES_IA_PORT", "8765")
URL = "http://127.0.0.1:" + PORT + "/npc_chat"


def main():
    metier = sys.argv[1] if len(sys.argv) > 1 else "maire"
    mode = sys.argv[2] if len(sys.argv) > 2 else "court"
    nom = metier.capitalize()
    historique = []
    print("Dialogue avec " + nom + " (mode " + mode + "). Ligne vide pour quitter.")
    while True:
        message = input("Vous : ").strip()
        if not message:
            break
        corps = json.dumps({"metier": metier, "nom": nom, "ville": "Village de depart",
                            "mode": mode, "message": message,
                            "historique": historique}, ensure_ascii=False).encode("utf-8")
        requete = urllib.request.Request(URL, data=corps,
                                         headers={"Content-Type": "application/json; charset=utf-8"})
        with urllib.request.urlopen(requete, timeout=120) as reponse:
            reste = json.loads(reponse.read().decode("utf-8"))
        texte = reste.get("reponse", reste.get("message", ""))
        print(nom + " : " + texte + "\n")
        historique += [{"role": "user", "content": message},
                       {"role": "assistant", "content": texte}]


if __name__ == "__main__":
    main()
