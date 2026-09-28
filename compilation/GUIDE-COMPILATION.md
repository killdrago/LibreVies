# Guide de compilation Unreal — LibreVies

`compilation/` est le cote auteur. Il contient le projet Unreal, les sources
C++, les assets MakeHuman CC0, les caches et les outils de compilation.

```text
compilation/
  unreal/
    LibreVies.uproject
    Content/Characters/MakeHuman/  mesh et cibles morphologiques CC0
    Content/SourceAssets/          sources a convertir pour le monde
    Source/LibreVies/              code du jeu, du createur et de l'atelier
  build_launcher.bat               build Windows Unreal
  INSTALLATION-UNREAL.md            logiciels a installer pour l'auteur

jeu/
  game/                             package Unreal a donner au joueur
```

## Build

Après l'installation décrite dans `INSTALLATION-UNREAL.md`, lancer :

```bat
compilation\build_launcher.bat
```

Le script lance Unreal Automation Tool, compile le module C++, cuisine les
assets, crée les Paks et place la distribution dans :

```text
jeu\game\
```

Ne pas donner uniquement le `.exe`. Le joueur reçoit le dossier entier.

Le createur de personnages n'est plus un deuxieme logiciel externe : il est
intégré au projet Unreal et pourra devenir l'écran **Nouvelle partie** du jeu.
La base MakeHuman est chargee par le generateur, les cibles sont appliquees par
les curseurs et le preset est sauvegarde en JSON.

## Atelier d'objets

L'interface de présentation contient les premières commandes réutilisables :

- arbre ;
- chaise ;
- brique ;
- arme ;
- prototype de vêtement.

Ces boutons fabriquent des prototypes modulaires. L'étape suivante consiste à
remplacer chaque prototype par un mesh artistique, des matériaux, des sockets,
un rig ou une simulation de tissu selon l'objet. L'interface et les appels
resteront les mêmes.

## Migration

Le depot ne contient plus l ancien projet moteur. Les règles de jeu historiques restent
à réimplémenter dans les acteurs Unreal : bras fixes du Maire et du Forgeron,
feuille du Maire, marteau du Forgeron uniquement et aucun marteau sur le joueur.
Elles doivent être validées dans une scène de village Unreal avant publication.

Le code Unreal de cette première étape valide le socle technique : projet C++,
chargement de la base MakeHuman, morphologie, preset et atelier. Les caches
`Binaries`, `Intermediate`, `Saved` et `DerivedDataCache` peuvent être supprimés
sans supprimer les sources.
