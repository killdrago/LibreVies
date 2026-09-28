# Installation Unreal pour LibreVies

Ce document concerne uniquement l'ordinateur qui fabrique le jeu. Le joueur
n'installe rien.

## 1. Installer Unreal Engine

1. Installer **Epic Games Launcher** depuis le site officiel d'Epic Games.
2. Ouvrir l'onglet **Library**.
3. Installer **Unreal Engine 5.6**.
4. Garder au minimum les composants Windows 64 bits.
5. Laisser plusieurs dizaines de Go libres sur un SSD.

Le projet utilise `EngineAssociation: 5.6`. Il ne faut pas ouvrir le projet avec
une version 4.x. Une autre version 5.x peut demander une conversion : pour la
premiere ouverture, utiliser 5.6.

## 2. Installer la compilation C++ Windows

Pour le projet actuel en Unreal Engine 5.6, installer **Visual Studio Community 2022**
en parallele de Visual Studio 2026. La documentation Epic indique que Visual
Studio 2026 n'est pas supporte avec Unreal Engine 5.6 ; notre projet est donc
fixe sur VS 2022 pour cette migration.

Dans Visual Studio 2022, cocher les deux charges de travail :

- **Developpement Desktop en C++** ;
- **Developpement de jeux avec C++**.

Dans les details, verifier :

- MSVC v143 ;
- Windows 10 ou Windows 11 SDK ;
- Visual Studio Tools for Unreal Engine, s'il est propose ;
- outils CMake pour Windows (facultatif mais utile).

Unreal utilise Visual Studio pour compiler le module `LibreVies` et les futures
extensions de l'atelier. Il n'est pas necessaire chez le joueur.

## 3. Ouvrir le projet

Ouvrir :

```text
compilation\unreal\LibreVies.uproject
```

Au premier lancement, accepter la generation des fichiers Visual Studio et
attendre la compilation des shaders. Cela peut durer longtemps la premiere fois.

Cliquer ensuite sur **Play**. Le GameMode construit automatiquement une scene
de presentation, charge la base humaine MakeHuman CC0 et affiche :

- le choix homme/femme ;
- les curseurs de morphologie ;
- le bouton Aleatoire ;
- la sauvegarde du preset ;
- les boutons de generation d'arbre, chaise, brique, arme et vetement.

Le preset est ecrit dans le dossier `Saved\CharacterCreator\` du projet. Il ne
s'agit pas encore d'une sauvegarde de partie : ce format deviendra le format de
creation du joueur.

## 4. Compiler le jeu Windows

Fermer une session Play, puis lancer :

```text
compilation\build_launcher.bat
```

Le script cherche Unreal Engine 5.6 dans les emplacements habituels. Si Unreal
est installe ailleurs, definir une fois la variable :

```bat
set LIBREVIES_UNREAL=C:\Program Files\Epic Games\UE_5.6
compilation\build_launcher.bat
```

Le resultat est place dans :

```text
jeu\game\
```

Il faut conserver tout le dossier. Le fichier `.exe` est accompagne des
fichiers `Content\Paks`, `Engine`, `Binaries` et des DLL necessaires.

## 5. Ce que le joueur installe

Rien concernant Unreal :

- pas d'Epic Games Launcher ;
- pas d'Unreal Engine ;
- pas de Visual Studio ;
- pas de MakeHuman ;
- pas de Blender.

Le joueur recoit le dossier package dans `jeu\game\`. Il faut tester la build
sur un Windows propre avant distribution : Unreal peut demander les runtimes
Windows et le pilote graphique correspondant a la configuration choisie.
