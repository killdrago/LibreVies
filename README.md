# LibreVies

Deux dossiers, deux rôles bien séparés :

| Dossier | Pour qui | Contenu |
|---|---|---|
| **`jeu/`** | le joueur et le launcher | `launcher.pyw`, `version_url.json`, `LIS-MOI.txt`. C'est ici que le jeu s'installe (`jeu/game/`). |
| **`compilation/`** | l'auteur seul | projet Unity, scripts de build Windows, images de travail, outils de publication. **Jamais téléchargé par le joueur.** |

## Le parcours du joueur

1. Il reçoit **un seul fichier** : `LibreVies.exe`.
2. Il double-clique : le launcher vérifie `jeu/version_url.json`, télécharge
   l'archive du jeu publiée (avec reprise si la connexion coupe), contrôle son
   md5 et l'installe.
3. Il clique sur **JOUER**.

Aucun Unity, aucun Python, aucun compte, aucune installation chez le joueur.
Un jeu déjà installé se lance même hors ligne.

## Publier une mise à jour (côté auteur)

1. `compilation\build_launcher.bat` — **un seul double-clic, tout est
   automatique** : il télécharge ce qui manque (le projet depuis GitHub,
   Python, PyInstaller, Unity) sans jamais écraser tes fichiers, puis il
   exporte le jeu Unity et fabrique `LibreVies.exe`.
2. `compilation\outils\publier_jeu.bat` — met l'archive dans la release GitHub
   et met à jour `jeu/version_url.json`.
3. `git push` — les joueurs reçoivent la mise à jour au prochain lancement.

La **première** publication est obligatoire : avant elle, le launcher affiche
« aucune compilation Unity publiee » et le bouton JOUER reste grisé. Le jeu est
une compilation Unity (`LibreViesGame.exe`) : aucun autre moteur n'est utilisé.

Détails, commandes et dépannage : [`compilation/GUIDE-COMPILATION.md`](compilation/GUIDE-COMPILATION.md)
