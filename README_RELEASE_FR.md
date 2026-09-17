# Distribution LibreVies — fonctionnement joueur

## Ce que voit le joueur

La distribution Windows est autonome :

1. double-clic sur `LibreVies.exe` ;
2. le launcher vérifie le manifeste et télécharge uniquement les fichiers de
   mise à jour nécessaires ;
3. clic sur **JOUER** ;
4. le jeu démarre.

Il n'y a **aucune installation de Godot, Python, Unity, Unreal ou autre
composant** chez le joueur. `game/LibreViesGame.exe` contient le runtime Godot
et le PCK du jeu. Le joueur ne lance pas cet exécutable directement : seul le
launcher est à utiliser.

Une connexion Internet est uniquement nécessaire pour rechercher et télécharger
les mises à jour. Si le serveur est momentanément indisponible, le jeu déjà
installé peut quand même être lancé.

## Fabriquer la distribution

`build_launcher.bat` est un script de fabrication destiné au développeur. Il
exporte le projet Godot, compile le launcher et produit `release/`. Les outils
utilisés pour fabriquer le jeu ne sont pas copiés dans `release/`.

La machine de build doit fournir :

- un exécutable portable de l'éditeur Godot 4.7 compatible Windows dans
  `tools/`, ou le chemin dans la variable `LIBREVIES_GODOT` ;
- PyInstaller pour compiler le launcher ;
- Python uniquement sur la machine de build.

Ces prérequis ne sont pas des prérequis du joueur. Le script ne lance plus de
`pip install` automatiquement et ne télécharge jamais Godot sur la machine du
joueur.

Pour que les mises à jour des binaires fonctionnent, publier les fichiers
suivants sur l'hébergement choisi :

```text
LibreVies.exe
 game/LibreViesGame.exe
 version_url.json
```

Avant de lancer le build, définir `LIBREVIES_ASSET_BASE_URL` vers l'URL publique
du dossier qui contient `LibreVies.exe` et `game/LibreViesGame.exe`. Le script
inscrit alors les tailles et hash MD5 dans `version_url.json`. Le champ `url`
de chaque fichier permet d'utiliser une release GitHub ou un autre hébergement
sans mettre un gros binaire dans l'historique git.

Le launcher remplace son propre `.exe` après sa fermeture, puis se relance
automatiquement. Les téléchargements sont vérifiés avant remplacement afin
qu'une mise à jour interrompue ne casse pas l'installation existante.

## À propos d'Unity et Unreal

Unity et Unreal ne sont pas des thèmes graphiques que l'on peut brancher sur un
projet Godot : ce sont d'autres moteurs et un portage demanderait de réécrire
le jeu. Pour obtenir exactement le comportement demandé sans installation
joueur, le projet reste donc exporté en Godot mais le runtime est embarqué dans
l'exécutable du jeu. Le rendu peut continuer à évoluer dans Godot sans changer
le parcours `launcher → mise à jour → jouer`.
