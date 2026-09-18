# LibreVies — guide de compilation (côté auteur uniquement)

Ce dossier ne part **jamais** chez le joueur. Il contient tout ce qu'il faut
pour fabriquer le launcher et publier le jeu.

```
jeu/                       ce que le joueur utilise
  launcher.pyw             le launcher (source)
  version_url.json         le manifeste lu par le launcher
  LIS-MOI.txt              explication envoyee au joueur

compilation/               reserve a l'auteur
  unity/                   projet Unity (le jeu)
  build_launcher.bat       fabrique LibreVies.exe + l'export du jeu
  build_unity_game.bat     exporte le jeu seul (diagnostic)
  setup_unity_build_tools.bat / download_unity_hub.ps1
                           installation automatique d'Unity
  image/                   images de travail (bannieres, chapitres, logos)
  outils/
    publier_jeu.bat/.py    met le jeu en ligne pour tous les joueurs
    definir_url_publication.bat/.py
                           change la branche surveillee par le launcher
    tester_launcher.bat/.py recette : telechargement, md5, reprise, securite
  release/                 (cree au build, ignore par git)
```

## 1. Fabriquer la distribution

Double-clic sur `build_launcher.bat`. Il fait tout :

1. exporte le projet Unity (`unity/`) avec son runtime,
2. compile `jeu/launcher.pyw` en `release\LibreVies.exe`,
3. copie `jeu/version_url.json` à côté, pour tester ici.

Résultat :

| Fichier | Destinataire |
|---|---|
| `release\LibreVies.exe` | **le joueur : c'est le seul fichier à lui donner** |
| `release\game\` | à publier (étape 2) |
| `release\version_url.json` | test local uniquement |

Unity n'est nécessaire que sur cette machine : ni Unity, ni Python, ni Unity
Hub ne sont installés chez le joueur. Si Unity Editor est absent,
`build_launcher.bat` lance `setup_unity_build_tools.bat`, qui télécharge Unity
Hub depuis le site officiel Unity (compte Unity et licence Personal
nécessaires pour fabriquer une build, jamais pour jouer).

## 2. Publier la compilation (les joueurs la reçoivent)

> **Première fois : cette étape est obligatoire.** Tant qu'aucune compilation
> n'a été publiée, le manifeste contient `"game_build": {}` et le launcher
> affiche « aucune compilation Unity publiee » (le bouton JOUER reste grisé).
> Dès la première publication, tout s'enchaîne : le launcher télécharge le jeu,
> puis se met à jour tout seul à chaque nouvelle publication.

Double-clic sur `outils\publier_jeu.bat` (ou lance-le avec la version en
paramètre). Le script :

1. rassemble `release\game\` en **une seule archive** `LibreVies_jeu_<md5>.zip` ;
2. l'envoie dans la release GitHub `derniere` (via `gh`) ;
3. met à jour `jeu/version_url.json` : `url`, `size`, `hash` (md5), `moteur`, `exe` ;
4. si `release\LibreVies.exe` existe, le publie aussi : le launcher des joueurs
   se met à jour tout seul ;
5. avec `--pousser` (utilisé par le `.bat`), il envoie le manifeste dans git.

Sans GitHub CLI (`gh`) sur la machine, le script fabrique quand même l'archive
et met le manifeste à jour : il indique alors l'URL exacte où déposer le `.zip`
à la main (release → *Edit* → *Attach binaries*).

Commande équivalente en ligne de commande :

```bat
python outils\publier_jeu.py --jeu release\game --version 0.5.0 ^
    --notes "Village Unity, camera corrigee" --exe release\LibreVies.exe --pousser
```

## 3. Ce que fait le joueur

1. Il double-clique sur `LibreVies.exe`.
2. Le launcher lit `jeu/version_url.json` (branche indiquée par `raw_url`),
   compare les hashs, télécharge l'archive du jeu **avec reprise** si la
   connexion coupe, vérifie son md5, l'installe dans `game\`.
3. Il clique sur **JOUER** — le jeu démarre.

Rien n'est installé ailleurs que dans le dossier du launcher :

```
LibreVies.exe            le launcher
version_url.json         le manifeste (recopie localement)
etat_jeu.json            version installee (hash, date, exe)
jeu.download.part        telechargement en cours (reprise)
game\                    le jeu (LibreViesGame.exe + UnityPlayer.dll + *_Data)
game.install\ / game.ancien\   dossiers de travail, supprimes apres coup
```

## 4. Changer la branche publiée

Le launcher lit le manifeste sur la branche indiquée dans `raw_url`
(`jeu/version_url.json`) et, à défaut, dans `DEFAULT_RAW_URL` (`jeu/launcher.pyw`).
Après avoir fusionné le travail dans `main` :

```bat
python outils\definir_url_publication.py --branche main
```

Le script met les deux endroits d'accord, vérifie que le launcher compile
toujours, puis il reste à faire `git add` / `commit` / `push`.

## 5. Vérifier avant de publier

```bat
python outils\test_launcher.py
```

La recette simule un joueur avec un serveur local : première installation,
mise à jour, téléchargement coupé puis repris, archive corrompue (refusée sans
casser le jeu installé) et archive piégée (`../`). Aucun fichier du dépôt n'est
touché.

## 6. En cas de problème

| Symptôme | Cause probable |
|---|---|
| « Hors ligne » chez le joueur | `raw_url` pointe une branche qui n'existe plus, ou pas de connexion |
| « Jeu : aucune compilation publiee » | le manifeste n'a pas encore de `game_build` : lance `publier_jeu.bat` |
| Le joueur reste sur une vieille version | le manifeste n'a pas été envoyé (`git push`) ou l'archive n'est pas dans la release |
| Le launcher ne se met pas à jour | publie aussi `LibreVies.exe` (`--exe`) : la mise à jour du launcher passe par lui |
| L'export Unity échoue | regarde `compilation\build\unity.log` |

## Rappels utiles

* Règle des mises à jour : tout changement de fichier suivi = nouveau hash dans
  `jeu/version_url.json`. Pour le jeu, c'est l'archive entière qui a un hash :
  une nouvelle compilation = une nouvelle archive.
* Une archive publiée n'est jamais écrasée en silence : son nom contient son
  md5 (`LibreVies_jeu_<md5>.zip`), donc un joueur qui télécharge n'est jamais
  coupé au milieu par une nouvelle publication.
* L'archive publiée est un `.zip` dont les fichiers sont **à la racine** : le
  launcher la dézippe directement dans `game\`.
