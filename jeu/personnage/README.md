# LibreVies — créateur de personnages 3D

Ce dossier reçoit la compilation autonome du créateur de personnages lorsque
`compilation\build_launcher.bat` est exécuté. Le fichier à lancer sera :

```text
jeu\personnage\LibreViesPersonnage.exe
```

Le logiciel final contient déjà la base humaine, les cibles de morphologie, les
poids du squelette, les matériaux et les coupes de cheveux. L'utilisateur n'a
besoin ni de Unity, ni de Python, ni de Blender, ni de MakeHuman, ni de MPFB.

## Fonctions

- base humaine continue MakeHuman hm08, homme ou femme ;
- rig `default` avec poids intégrés pour une future utilisation Unity ;
- ventre, épaisseur et longueur des bras, épaisseur et longueur des jambes,
  taille des pieds ;
- forme de la tête, des yeux, du nez, de la bouche et des oreilles ;
- cinq coupes de cheveux intégrées avec textures peau et cheveux embarquées ;
- bouton **ALÉATOIRE** qui choisit sexe, morphologie et coupe ;
- export d'un preset JSON dans le dossier de données utilisateur.

Les sources de fabrication restent dans `compilation/personnage/`. La base
humaine et les cibles MakeHuman Community sont distribuées sous CC0 1.0 ; la
notice complète est dans `compilation/personnage/Assets/Resources/Characters/MAKEHUMAN_CC0_LICENSE.md`.
