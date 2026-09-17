# Distribution LibreVies — Unity

## Ce que voit le joueur

Le client peut être lancé directement avec `launcher.pyw` pendant le
développement, ou avec `LibreVies.exe` dans la distribution finale. Dans les
deux cas, le fonctionnement est identique :

1. double-clic sur `launcher.pyw` ou `LibreVies.exe` ;
2. le launcher vérifie le manifeste et télécharge uniquement les fichiers de
   mise à jour nécessaires ;
3. clic sur **JOUER** ;
4. le jeu Unity démarre.

Il n'y a **aucune installation de Unity Editor, Python, Unity Hub, Unreal ou
autre composant** chez le joueur. `game/LibreViesGame.exe` est une build Unity Windows accompagnée de ses
fichiers runtime (`UnityPlayer.dll` et `LibreViesGame_Data/`). Le joueur ne
lance pas cet exécutable directement : seul le launcher est à utiliser.

Une connexion Internet est uniquement nécessaire pour rechercher et télécharger
les mises à jour. Si le serveur est momentanément indisponible, le jeu déjà
installé peut quand même être lancé.

## Fabriquer la distribution

`build_launcher.bat` est le **seul script à lancer** pour fabriquer la
distribution complète. Il ouvre Unity en mode batch, exporte le projet situé
dans `unity/`, compile le launcher et produit `release/`. Les outils de
développement ne sont pas copiés dans `release/`.

Si Unity Editor n'est pas trouvé, `build_launcher.bat` appelle
automatiquement `setup_unity_build_tools.bat`. Ce script télécharge Unity Hub
depuis le site officiel Unity et demande à Unity Hub d'installer Unity 6.6 (version `6000.6.1f1`)
avec le module Windows. Unity Hub peut demander une connexion à un compte Unity
et l'activation de la licence Personal : cette licence est obligatoire pour
fabriquer une build, mais jamais pour le joueur.

PyInstaller et Python restent nécessaires uniquement sur la machine de build.
Ces outils et Unity ne sont jamais copiés dans `release/` et ne sont jamais
installés chez le joueur. `build_unity_game.bat` existe seulement comme étape
de diagnostic séparée ; dans le cas normal, il faut lancer uniquement
`build_launcher.bat`.

## Mise à jour des fichiers

Pour publier une version, publier les fichiers suivants sur l'hébergement
choisi :

```text
LibreVies.exe
version_url.json
game/
├── LibreViesGame.exe
├── UnityPlayer.dll
└── LibreViesGame_Data/   (tout le dossier)
```

Avant de lancer le build, définir `LIBREVIES_ASSET_BASE_URL` vers l'URL publique
du dossier qui contient `LibreVies.exe` et `game/LibreViesGame.exe`. Le script
inscrit alors les tailles et hash MD5 dans `version_url.json`. Le champ `url`
de chaque fichier permet d'utiliser une release GitHub ou un autre hébergement
sans mettre un gros binaire dans l'historique git.

Le launcher remplace son propre `.exe` après sa fermeture, puis se relance
automatiquement. Les téléchargements sont vérifiés avant remplacement afin
qu'une mise à jour interrompue ne casse pas l'installation existante.

Pour que `launcher.pyw` puisse jouer sans rien fabriquer localement, le champ
`package.files` du `version_url.json` publié doit contenir le hash, la taille et
l'URL de `game/LibreViesGame.exe`. Le launcher téléchargera alors cette build
comme n'importe quelle mise à jour, puis activera automatiquement JOUER.

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
