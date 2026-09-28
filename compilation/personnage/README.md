# Projet de fabrication — créateur de personnages

Projet Unity séparé du jeu principal. `compilation/build_launcher.bat` l'ouvre en
mode batch et appelle `CharacterCreatorBuild.BuildWindows`; le résultat est
copié dans `jeu/personnage/`.

Les données de `Assets/Resources/Characters/` comprennent :

- `MakeHumanBase.obj` et `MakeHumanBaseData.txt`, la base humaine hm08 ;
- les cibles de forme ventre, bras, jambes, pieds, tête, yeux, nez, bouche et
  oreilles ;
- `rig.csv` et `weights.csv`, construits à partir du rig `default` MakeHuman ;
- la notice CC0 complète dans `MAKEHUMAN_CC0_LICENSE.md`.

La build finale lit tout depuis ses ressources intégrées. MakeHuman, MPFB,
Blender et Python ne sont pas nécessaires pour l'utilisateur final.
