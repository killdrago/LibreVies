# IA des PNJ (Qwen2.5-1.5B-Instruct, local)

Ce dossier contient le serveur qui fait répondre les PNJ du jeu.
Pour l'instant il tourne sur votre PC ; il sera déplacé sur l'hébergeur plus tard.

## Installation (une fois)

1. Téléchargez les fichiers de <https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct/tree/main>
   dans ce dossier `IA` (ceux que vous avez déjà : `config.json`, `tokenizer.json`,
   `tokenizer_config.json`, `vocab.json`, `merges.txt`, `generation_config.json`).
2. Renommez le fichier des poids `qwen2.5 1.5b.safetensors` en **`model.safetensors`**.
3. Installez les dépendances : `pip install -r requirements.txt`
   (PyTorch peut demander une commande spécifique selon votre carte graphique sur pytorch.org).

## Lancer le serveur

```bat
cd serveur\IA
python serveur_ia.py
```

Attendez le message « Modele pret ». Le serveur écoute sur `http://127.0.0.1:8765`.
Laissez cette fenêtre ouverte pendant que vous jouez.

## Tester sans le jeu

Dans une autre fenêtre : `python tester_pnj.py maire` (ou `forgeron`, `marchand`,
`esthetique`, `medecin`). Ajoutez `libre` pour une conversation plus longue.

## Notes

- Le modèle tourne sur CPU si aucune carte NVIDIA n'est détectée : la réponse
  peut prendre plusieurs secondes.
- Le dossier `IA` ne doit pas être versionné : les poids pèsent plusieurs Go
  (voir `.gitignore`).
- Pour l'hébergeur, il faudra ajouter une authentification et passer par `api.php`.
