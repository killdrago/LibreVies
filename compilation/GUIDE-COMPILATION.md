# LibreVies — guide de compilation (côté auteur uniquement)

Ce dossier ne part **jamais** chez le joueur. Il contient tout ce qu'il faut
pour fabriquer le launcher et publier le jeu.

```
jeu/                       sortie locale de compilation
  launcher.pyw             le launcher (source)
  version_url.json         le manifeste de secours
  game/                    le build Unity

jeucompiler/              copie publiee sur GitHub
  version_url.json         le manifeste surveille par le launcher
  game/                    fichiers Unity deja compiles

compilation/               reserve a l'auteur
  unity/                   projet Unity (le jeu)
  personnage/              projet Unity séparé du créateur humain 3D
  build_launcher.bat       fabrique LibreVies.exe + le jeu + le créateur
  build_personnage.bat     exporte uniquement le créateur vers jeu\personnage\
  build_unity_game.bat     exporte le jeu et le créateur (diagnostic local)
  setup_unity_build_tools.bat / download_unity_hub.ps1
                           installation automatique d'Unity
  image/                   images de travail (bannieres, chapitres, logos)
  outils/
    publier_jeu.bat/.py    ancien mode de publication par archive GitHub Release
    definir_url_publication.bat/.py
                           change la branche surveillee par le launcher
    tester_launcher.bat/.py recette : telechargement, md5, reprise, securite
    creer_icone.bat/.py    reextrait l'icone du launcher (utilise par le build)
  publier_jeu_compiler.bat/.py
                           publie les fichiers modifies par lots vers jeucompiler/
  release/                 (cree au build, ignore par git)
```

## 1. Fabriquer la distribution

Double-clic sur `build_launcher.bat`. **Il se débrouille tout seul** : il
télécharge d'abord ce qui manque, puis il compile.

Ce qu'il télécharge automatiquement (uniquement si c'est absent) :

| Élément | Source | Quand |
|---|---|---|
| le projet (Unity + launcher + outils) | GitHub, branche indiquée par `BRANCHE` | si le dossier ne contient que ce `.bat` |
| Python | winget, sinon python.org | si `python` n'est pas installé |
| PyInstaller | `pip` | si absent (une seule fois) |
| Unity Hub + Unity Editor | site officiel Unity | si Unity absent (gros, une seule fois) |

Puis il compile et **dépose tout dans `jeu/`** — le dossier du joueur :

| Résultat du build | Destinataire |
|---|---|
| `jeu\LibreVies.exe` | le launcher du jeu principal |
| `jeu\game\` (LibreViesGame.exe + UnityPlayer.dll + *_Data) | à publier (étape 2) |
| `jeu\personnage\LibreViesPersonnage.exe` + son dossier `_Data` | créateur humain autonome à prendre avec le build |

Le créateur de personnages est construit par le même `build_launcher.bat`, mais
reste séparé du jeu principal. Il contient la base humaine, les morphologies et
le rig : il ne demande aucune installation au joueur.

Si tu as déjà téléchargé les sources dans `compilation/` mais que le créateur
manque dans `jeu/`, lance directement `compilation\build_personnage.bat`.
Il fabrique l'exécutable puis déplace tout son dossier Unity (exe, `*_Data`,
`UnityPlayer.dll`, etc.) dans `jeu\personnage\`. Le build du jeu principal
n'est pas nécessaire pour cette étape. `build_unity_game.bat` appelle aussi ce
script après l'export du jeu.

Il ne touche à rien d'autre dans `jeu/` : `launcher.pyw`, `version_url.json` et
`LIS-MOI.txt` restent en place. **`compilation/` ne sert qu'à compiler** : après
avoir vérifié que `jeu\personnage\LibreViesPersonnage.exe` démarre, tu peux
supprimer localement `compilation\personnage\` pour récupérer de la place.
Ne supprime jamais `jeu\personnage\` : ce dossier contient le logiciel livré.
Si tu veux reconstruire plus tard après avoir supprimé les sources, relance
`build_launcher.bat` ; il téléchargera de nouveau le projet du créateur.

Deux garanties importantes :

* **Les sources suivies sont synchronisées avec la branche publiée.** Une
  sauvegarde locale précède leur remplacement ; les coordonnées d'édition
  sont publiées avant cette synchronisation. La configuration MySQL réelle
  `config.php` n'est pas remplacée.
* **Rien de tout cela n'arrive chez le joueur.** Ni Python, ni PyInstaller, ni
  Unity, ni ce dossier `compilation\` : le joueur ne reçoit que `LibreVies.exe`.
  La licence Unity (compte Unity + licence Personal, demandée par Unity Hub)
  n'est nécessaire que pour *fabriquer* la build, jamais pour jouer.

Pour changer la branche dont le script récupère le projet :

```bat
set LIBREVIES_BRANCHE=main
build_launcher.bat
```

ou en ligne de commande : `build_launcher.bat main`.
(`outils\definir_url_publication.py` met aussi cette branche à jour tout seul.)

## 2. Publier la compilation vers `jeucompiler/`

Le flux courant publie les fichiers déjà compilés dans le dossier GitHub
`jeucompiler/`. Le joueur ne reçoit jamais le projet Unity ni Python.

Double-clic sur `publier_jeu_compiler.bat` à la racine du dépôt. L'application graphique :

1. compare le contenu local de `jeu/` avec `jeucompiler/` ;
2. fabrique `jeucompiler/version_url.json` avec le hash de chaque fichier ;
3. repère le build Unity dans `jeu/game/` ;
4. regroupe les nouveaux fichiers et fichiers modifiés selon la limite en Mo ;
5. pousse chaque lot sur la branche GitHub choisie ;
6. conserve les fichiers déjà identiques et peut supprimer les fichiers absents
   si la case correspondante est cochée.

La limite concerne la taille d'un lot. Un fichier individuel plus gros que cette
limite est envoyé seul : il n'est pas découpé. Les fichiers de plus de 100 Mo
peuvent être refusés par GitHub classique ; dans ce cas il faudra utiliser Git
LFS ou une GitHub Release.

Le launcher lit ensuite `jeucompiler/version_url.json`, télécharge seulement les
fichiers dont le hash local ne correspond pas et active **JOUER** après leur
vérification. Il ne compile jamais chez le joueur.

La compilation doit donc être faite avant la publication :

```text
compilation/ -> build Unity -> jeu/ -> application de publication
                                      -> jeucompiler/ -> launcher joueur
```

L'ancien `outils\publier_jeu.bat` reste disponible pour le flux historique par
archive GitHub Release.

## 3. Ce que fait le joueur

1. Il double-clique sur `LibreVies.exe`.
2. Le launcher lit le manifeste distant `jeucompiler/version_url.json`,
   compare les hashs et télécharge uniquement les fichiers modifies. Les
   fichiers sont déjà compiles : aucune compilation n'est faite chez le joueur.
3. Il clique sur **JOUER** — le jeu démarre.

Rien n'est installé ailleurs que dans le dossier du launcher :

```
jeu\                     UN SEUL dossier pour le joueur
  LibreVies.exe          le launcher (il verifie les MAJ et telecharge le jeu)
  version_url.json       le manifeste
  LIS-MOI.txt            l'explication
  game\                  le jeu (LibreViesGame.exe + UnityPlayer.dll + *_Data)
  etat_jeu.json          version installee (hash, date, exe)
  jeu.download.part      telechargement en cours (reprise)
  game.install\ / game.ancien\   dossiers de travail, supprimes apres coup
```

Le joueur peut aussi recevoir **tout le dossier `jeu\`** (clé USB, zip) : le
launcher y trouve le jeu déjà installé et JOUER est disponible immédiatement,
même sans Internet.

## 4. Changer la branche publiée

Trois fichiers indiquent la branche utilisée : `jeu/version_url.json` (`raw_url`,
lu par le launcher), `jeu/launcher.pyw` (`DEFAULT_RAW_URL`, valeur de secours) et
`compilation/build_launcher.bat` (`BRANCHE`, d'où le build récupère le projet
quand il est absent). Après avoir fusionné le travail dans `main` :

```bat
python outils\definir_url_publication.py --branche main
```

Le script met les trois d'accord, vérifie que le launcher compile toujours et que
le `.bat` garde ses étiquettes, puis il reste à faire `git add` / `commit` / `push`.

## 5. Vérifier avant de publier

```bat
python outils\test_launcher.py
```

La recette simule un joueur avec un serveur local : première installation,
mise à jour, téléchargement coupé puis repris, archive corrompue (refusée sans
casser le jeu installé) et archive piégée (`../`). Aucun fichier du dépôt n'est
touché.

### Personnage du compte et API locale

La source du jeu passe à **0.5.80** pour le parcours Esthétique. Cela ne
remplace pas une ancienne build : reconstruire avec `build_launcher.bat`, puis
publier les fichiers compilés avec le flux habituel. Le manifeste du jeu doit
continuer à annoncer la version du binaire réellement publié, pas celle de
sources encore non compilées.

L'API a une seule source : `jeu/serveur/`. Les deux builds appellent
`outils/synchroniser_serveur_local.ps1` pour mettre à jour les fichiers publics
chez Apache local (XAMPP/EasyPHP/WAMP), **sans remplacer son vrai `config.php`**. Cet outil copie aussi `securite.php`
(generateur de session compatible sans OpenSSL) et `classement.sql`. Ce SQL
ajoute les classements manquants des anciens comptes sans reinitialiser leurs
scores. Pour une correction uniquement PHP, lancer cet outil seul suffit :
pas besoin de recompiler le jeu.
L'adresse actuelle reste `http://localhost/serveur/api.php`.
Une racine personnalisée peut être indiquée par `LIBREVIES_WEB_ROOT`.
Les éventuelles migrations SQL restent dans `jeu/serveur/` et ne sont jamais
importées automatiquement. Voir [`../jeu/serveur/README.md`](../jeu/serveur/README.md).

Le salon utilise le panel humain du jeu ; il ne lance pas l'exécutable du
projet autonome `compilation/personnage/`. Vérifier dans la build :

1. compte neuf : `valider=0`, base inchangée, `personnage.default=1` et autres champs NULL,
   classement au meme ID avec experience/chasse/territoire a zero ;
2. parler au NPC Esthétique, changer les 13 sliders et les choix, puis annuler : aucune sauvegarde ;
3. rouvrir, valider : attendre la confirmation serveur, constater `default=0` sur la même ligne ;
4. fermer/reconnecter : mêmes proportions, sexe, peau et équipements ;
5. rouvrir le salon : profil sauvegardé prérempli ; pseudo visible au-dessus de la tête ;
6. ADMIN ne propose plus le créateur, mais conserve les autres fonctions ;
7. couper l'API : message explicite, pas de confirmation de sauvegarde ni de faux personnage de base.

Recettes complémentaires depuis la racine :

```text
python -B compilation/outils/test_personnage.py
python -B compilation/outils/test_edition.py
python -B compilation/outils/test_amorce.py
python -B compilation/outils/verifier_cs_syntaxe.py
```

`test_api_personnage.mjs` exécute aussi une recette PHP avec SQLite de test
(instructions dans le fichier). Le parseur C# et cette base de test ne remplacent
ni la compilation Unity ni les contrôles visuels et MySQL locaux.

## 6. En cas de problème

| Symptôme | Cause probable |
|---|---|
| « Hors ligne » chez le joueur | `raw_url` pointe une branche qui n'existe plus, ou pas de connexion |
| « Jeu : aucune compilation publiee » | le manifeste n'a pas encore de `game_build` : lance `publier_jeu.bat` |
| Le joueur reste sur une vieille version | le manifeste n'a pas été envoyé (`git push`) ou l'archive n'est pas dans la release |
| Le launcher ne se met pas à jour | publie aussi `LibreVies.exe` (`--exe`) : la mise à jour du launcher passe par lui |
| L'export Unity échoue | regarde `compilation\build\unity.log` |
| `FileNotFoundError: Icon input file ...\build\icon.ico` | PyInstaller résout un chemin d'icône **relatif au dossier du `.spec`**. Le script passe donc l'icône en chemin absolu. Si ça revient : lance `outils\creer_icone.bat`, il doit afficher « Icone prete » |
| `LibreVies.exe` énorme (pygame, numpy dedans) | ces modules sont installés sur la machine de build et PyInstaller les aspire : ils sont exclus dans `build_launcher.bat` (`--exclude-module`) |

## Rappels utiles

* Règle des mises à jour : tout changement de fichier suivi = nouveau hash dans
  `jeu/version_url.json`. Pour le jeu, c'est l'archive entière qui a un hash :
  une nouvelle compilation = une nouvelle archive.
* Une archive publiée n'est jamais écrasée en silence : son nom contient son
  md5 (`LibreVies_jeu_<md5>.zip`), donc un joueur qui télécharge n'est jamais
  coupé au milieu par une nouvelle publication.
* L'archive publiée est un `.zip` dont les fichiers sont **à la racine** : le
  launcher la dézippe directement dans `game\`.

## PHP 8.3.3 / phpMyAdmin 5.2.3 / Apache 2.4.43 et pseudo en jeu

Le code serveur est compatible avec cette pile pour les essais locaux,
sous reserve de `pdo_mysql`, des sessions inscriptibles et du bon module PHP
charge par Apache. Voir **`jeu/serveur/README.md`**, section Environnement local,
pour les limites des tests et les exigences des extensions de phpMyAdmin.

Le jeu actuellement publie est **0.5.79** et son assembly ne contient pas
encore l'affichage du pseudo. Les sources sont **0.5.80** : relancer
**`compilation/build_launcher.bat`** (met les sources a jour puis compile),
puis la publication qu'il propose. Modifier PHP ne met pas le jeu a jour.
Le build Unity refuse maintenant les scripts charges obsoletes, un decalage
entre la version des sources et du projet ou un export sans les methodes du
compte/pseudo. Ne jamais relever simplement le numero dans le manifeste d'un
ancien jeu pour simuler une nouvelle compilation.

Apres installation du nouvel export, se connecter avec le launcher puis
passer en **troisieme personne** : le vrai pseudo doit etre au-dessus de la tete.
Le journal runtime contient `pseudo joueur pret` ou un diagnostic si la police
3D manque. Aucun nouvel export natif n'a ete fabrique dans la verification Linux.
