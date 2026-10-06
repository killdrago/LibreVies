# LibreVies

Deux dossiers, deux rôles bien séparés :

| Dossier | Pour qui | Contenu |
|---|---|---|
| **`jeu/`** | l'auteur et le joueur local | sortie locale de compilation : launcher, `game/` et `personnage/`. |
| **`jeucompiler/`** | la distribution GitHub | copie publiée de `jeu/`, déjà compilée, surveillée par le launcher. Elle ne contient jamais le projet Unity source. |
| **`compilation/`** | l'auteur seul | projet Unity du jeu, projet Unity séparé du créateur humain, scripts de build Windows, images de travail et outils de publication. Il **compile** puis dépose le résultat dans `jeu/`. **Jamais téléchargé par le joueur.** |

## Le parcours du joueur

1. Il reçoit **un seul fichier** : `LibreVies.exe`.
2. Il double-clique : le launcher vérifie le manifeste publié dans
   `jeucompiler/version_url.json`, compare les hashes et télécharge seulement
   les fichiers modifies (jeu déjà compilé, sans Unity chez le joueur).
3. Il clique sur **JOUER**.

Aucun Unity, aucun Python, aucun compte, aucune installation chez le joueur.
Un jeu déjà installé se lance même hors ligne.

## Publier une mise à jour (côté auteur)

1. `compilation\build_launcher.bat` — **un seul double-clic, tout est
   automatique** : il télécharge ce qui manque (le projet depuis GitHub,
   Python, PyInstaller, Unity) sans jamais écraser tes fichiers, puis il
   compile et dépose le résultat **dans `jeu/`** : `jeu\LibreVies.exe`
   (le launcher), `jeu\game\` (le jeu exporté) et
   `jeu\personnage\LibreViesPersonnage.exe` (le créateur humain autonome).
2. `publier_jeu_compiler.bat` — ouvre l'application de
   publication : elle compare `jeu/` avec `jeucompiler/`, regroupe les fichiers
   modifies selon la limite de Mo choisie, met à jour le manifeste et pousse
   chaque lot sur GitHub.
3. Les joueurs reçoivent les fichiers déjà compilés au prochain lancement.

L'ancien `outils\publier_jeu.bat` reste disponible pour les publications par
archive GitHub Release, mais le flux courant utilise `jeucompiler/` et le
publieur graphique.

La **première** publication est obligatoire : avant elle, le launcher affiche
« aucune compilation Unity publiee » et le bouton JOUER reste grisé. Le jeu est
une compilation Unity (`LibreViesGame.exe`) : aucun autre moteur n'est utilisé.

Détails, commandes et dépannage : [`compilation/GUIDE-COMPILATION.md`](compilation/GUIDE-COMPILATION.md)
