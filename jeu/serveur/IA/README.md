# IA des PNJ (Qwen2.5-1.5B-Instruct, local)

Ce dossier contient le serveur qui fait répondre les PNJ du jeu.
Il tourne sur votre PC, dans le meme programme que le monde (`serveur/mondeLV.py`) ; il sera déplacé sur l'hébergeur plus tard.

## Installation (une fois)

1. Téléchargez les fichiers de <https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct/tree/main>
   dans ce dossier `IA` (ceux que vous avez déjà : `config.json`, `tokenizer.json`,
   `tokenizer_config.json`, `vocab.json`, `merges.txt`, `generation_config.json`).
2. Renommez le fichier des poids `qwen2.5 1.5b.safetensors` en **`model.safetensors`**.
3. Installez les dépendances : `pip install -r requirements.txt`
   (PyTorch peut demander une commande spécifique selon votre carte graphique sur pytorch.org).

## Lancer le serveur

Le serveur IA est maintenant dans `serveur\mondeLV.py`, avec le monde des monstres.
Pour le lancer, voir le fichier `serveur\demarrer_mondeLV.bat` (ou `python mondeLV.py`
dans le dossier `serveur`). Les poids du modele restent dans ce dossier `IA`.

Si les poids ou `torch` manquent, le monde tourne quand meme : le message
« IA desactivee » est affiche au demarrage.

## Tester sans le jeu

Dans une autre fenêtre : `python tester_pnj.py maire` (ou `forgeron`, `marchand`,
`esthetique`, `medecin`). Ajoutez `libre` pour une conversation plus longue.

## Notes

- Le modèle tourne sur CPU si aucune carte NVIDIA n'est détectée : la réponse
  peut prendre plusieurs secondes.
- Le dossier `IA` ne doit pas être versionné : les poids pèsent plusieurs Go
  (voir `.gitignore`).
- Pour l'hébergeur, il faudra ajouter une authentification et passer par `api.php`.
