# LibreVies

LibreVies utilise maintenant **Unreal Engine 5.6**. Le createur de personnages
et l'atelier d'objets sont dans le meme projet afin que le personnage puisse
etre reutilise plus tard directement au debut d'une nouvelle partie.

## Organisation

```text
compilation/
  unreal/                 projet Unreal, sources C++, assets et licences
  build_launcher.bat      compile et place la distribution dans jeu/game/
  INSTALLATION-UNREAL.md  installation de l'auteur
  GUIDE-COMPILATION.md    fabrication et distribution

jeu/
  game/                   distribution Windows a donner au joueur
  launcher.pyw            launcher optionnel de mise a jour
  edition/                donnees d'edition du monde
```

Le projet de l ancien moteur a ete retire du depot a la demande du createur.
Une sauvegarde locale du projet precedent doit etre conservee en dehors du
repo si elle est encore necessaire.

## Fonctions Unreal de la premiere migration

- base humaine MakeHuman Community CC0, homme ou femme ;
- morphologie independante du ventre, des bras, des jambes, des pieds et du
  visage ;
- bouton Aleatoire et sauvegarde JSON du preset ;
- base nue sans cheveux, accessoires ni vetements pour valider le mesh ;
- atelier de prototypes d'arbre, chaise, brique, arme et vetement ;
- source des meshes et des cibles morphologiques embarquee dans le projet ;
- structure prevue pour reutiliser le generateur dans le jeu au debut de la
  partie.

Les prototypes d'objets sont une base technique. Ils seront remplaces
progressivement par des meshes artistiques, des materiaux et des vetements
rigges sans changer le principe de l'atelier.

## Demarrage rapide auteur

1. Installer Epic Games Launcher, Unreal Engine 5.6 et les outils Visual Studio
   indiques dans `compilation/INSTALLATION-UNREAL.md`.
2. Ouvrir `compilation/unreal/LibreVies.uproject`.
3. Laisser Unreal compiler le module C++.
4. Cliquer sur **Play** : le createur MakeHuman et l'atelier s'ouvrent.
5. Pour fabriquer la version joueur, lancer `compilation/build_launcher.bat`.

Le joueur final ne recoit ni Unreal Engine, ni Epic Games Launcher, ni Visual
Studio : il recoit uniquement `jeu/game/`.
