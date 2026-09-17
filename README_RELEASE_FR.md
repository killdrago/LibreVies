# Distribution LibreVies — Unity

## Ce que voit le joueur

La distribution Windows est autonome :

1. double-clic sur `LibreVies.exe` ;
2. le launcher vérifie le manifeste et télécharge uniquement les fichiers de
   mise à jour nécessaires ;
3. clic sur **JOUER** ;
4. le jeu Unity démarre.

Il n'y a **aucune installation de Unity Editor, Python, Unity Hub, Unreal ou
autre composant** chez le joueur. `game/LibreViesGame.exe` est une build Unity
Windows qui contient son runtime, ses bibliothèques et les données du jeu. Le
joueur ne lance pas cet exécutable directement : seul le launcher est à utiliser.

Une connexion Internet est uniquement nécessaire pour rechercher et télécharger
les mises à jour. Si le serveur est momentanément indisponible, le jeu déjà
installé peut quand même être lancé.

## Fabriquer la distribution

`build_launcher.bat` est le script de fabrication destiné au développeur. Il
ouvre Unity en mode batch, exporte le projet situé dans `unity/`, compile le
launcher et produit `release/`. Les outils de développement ne sont pas copiés
dans `release/`.

La machine de build doit fournir :

- Unity Editor 2022.3 LTS avec le module Windows Build Support ; le chemin est
  fourni par `LIBREVIES_UNITY` ;
- PyInstaller pour compiler le launcher ;
- Python uniquement sur la machine de build.

Ces prérequis ne sont pas des prérequis du joueur. Le script ne lance pas de
`pip install` automatiquement et ne télécharge jamais Unity sur la machine du
joueur.

## Mise à jour des fichiers

Pour publier une version, publier les fichiers suivants sur l'hébergement
choisi :

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

## Migration Unity

Le nouveau projet Unity se trouve dans `unity/`. Le jeu est construit sans
asset externe obligatoire : le monde low-poly, les bâtiments, le château, le
personnage, les ennemis, le combat, les objets, la caméra et le HUD sont créés
par `Assets/Scripts/LibreViesGame.cs`.

La version Unity conserve le parcours demandé :

```text
launcher → mises à jour → jouer → LibreViesGame.exe
```

Aucun éditeur ni projet de développement n'est copié dans la distribution
finale : seul le launcher et la build Windows Unity sont nécessaires au joueur.
