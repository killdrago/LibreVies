# LibreVies — créateur de personnages 3D

Ce dossier reçoit la compilation autonome du créateur de personnages lorsque
`compilation\build_launcher.bat` est exécuté. Si les projets sont déjà présents
dans `compilation\`, `compilation\build_personnage.bat` suffit aussi. Le fichier
à lancer sera :

```text
jeu\personnage\LibreViesPersonnage.exe
```

Le logiciel final contient déjà la base humaine, les cibles de morphologie,
les poids du squelette et les matériaux. Les coupes de cheveux sont prévues
pour une étape ultérieure mais restent désactivées dans cette version de base.
L'utilisateur n'a
besoin ni de Unity, ni de Python, ni de Blender, ni de MakeHuman, ni de MPFB.

## Fonctions

- base humaine continue MakeHuman hm08, homme ou femme ;
- rig `default` avec poids intégrés pour une future utilisation Unity ;
- ventre, épaisseur et longueur des bras, épaisseur et longueur des jambes,
  taille des pieds ;
- forme de la tête, des yeux, du nez, de la bouche et des oreilles ;
- base homme/femme nue, sans vêtement ni accessoire pour la première version ;
- bouton **ALÉATOIRE** qui choisit le sexe et la morphologie ;
- les cinq coupes de cheveux sont conservées dans les sources pour une étape ultérieure, mais désactivées pour valider d'abord la tête et le corps ;
- export d'un preset JSON dans le dossier de données utilisateur.

Les sources de fabrication restent dans `compilation/personnage/`. La base
humaine et les cibles MakeHuman Community sont distribuées sous CC0 1.0 ; la
notice complète est dans `compilation/personnage/Assets/Resources/Characters/MAKEHUMAN_CC0_LICENSE.md`.
