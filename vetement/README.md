# Atelier de vetements LibreVies

Ce dossier contient un petit logiciel Python autonome pour preparer les futurs
vetements du jeu.

## Installation Windows

1. Installer Python 3.10 ou plus recent depuis python.org.
2. Double-cliquer sur `lancer_vetement.bat`.
3. Le script installe Pillow puis ouvre l'atelier 3D leger, sans Blender.

Installation manuelle :

```text
python update_vetement.py
python -m pip install -r requirements.txt
python atelier3d.py
```

## Mise a jour automatique

`lancer_vetement.bat` lance d'abord `update_vetement.py`. Celui-ci consulte le
manifeste publie sur GitHub pour la branche `arena/01a0b32c-librevies`, compare
les empreintes SHA-256 et telecharge uniquement les fichiers de code modifies.
Les fichiers sont verifies puis remplaces de maniere atomique.

L'actualisation ne touche pas aux projets et exports de l'utilisateur dans
`export/`. En cas de probleme Internet, la version locale continue de se
lancer. Pour verifier sans rien modifier :

```text
python update_vetement.py --check
```

Apres avoir modifie l'atelier, le manifeste se regenere avec :

```text
python generer_manifest.py 2026.09.30.2
```

## Fonctionnement actuel

### Depuis une image

- `Importer une image` charge un PNG, JPG ou WEBP.
- `Decouper le fond` rend transparent le fond connecte aux bords de l'image.
- Le choix `Image decoupee` exporte une surface 3D fine avec cette image.
- Le choix `Soutien-gorge`, `Culotte`, `Pull`, etc. crée une geometrie 3D
  parametrique et utilise la photo comme texture.
- La zone de droite est un viewport 3D logiciel : glisser avec le bouton gauche
  pour tourner et utiliser la molette pour zoomer.

### Depuis un modele parametrique

L'atelier propose des formes de depart :

- soutien-gorge ;
- culotte ;
- calecon ;
- pull ;
- pantalon ;
- chaussure ;
- armure.

On peut modifier largeur, hauteur, epaisseur, echelle, rotation et position.

### Fichiers produits

L'export produit :

- `nom.glb` : modele 3D importable dans Unity, Blender ou Godot ;
- `nom.png` : texture conservee lorsqu'une photo a ete chargee ;
- `nom.json` : position, zone d'attache, texture et informations de skinning.

Les images avec alpha sont exportees avec `alphaMode: BLEND` dans le GLB. Le
fond noir d'un visionneur qui ne gere pas l'alpha ne fait donc pas partie du
vetement.

Le JSON est reserve au branchement du vetement sur le joueur : le jeu pourra
lire `anchor` et `coverage` pour masquer ou non la peau sous le vetement.

## Mettre un GLB dans Unity

Le projet Unity ne lit pas les GLB nativement. Deux solutions sont possibles :

1. ouvrir le GLB dans Blender, appliquer le materiau transparent, puis
   exporter en FBX ;
2. ajouter le package Unity **glTFast** via le Package Manager, puis charger le
   GLB avec son importeur.

Copier simplement le fichier dans `Assets` ne suffit pas encore pour le joueur :
le GLB exporte par cet atelier est un vetement rigide, pas encore skine sur les
os du personnage. Il faudra ensuite le placer sur le rig MakeHuman, lui donner
les os du torse/bassin et appliquer `anchor`/`coverage` du JSON. C'est cette
etape qui permettra de remplacer la peau sous le vetement sans detruire le
maillage humain.

### Placement du soutien-gorge dans l'aperçu ADMIN

Le projet Unity contient une copie transparente de la texture dans :

`compilation/unity/Assets/Resources/Characters/Clothing/soutien_gorge.png`

Dans ADMIN > Personnage, l'aperçu féminin affiche cette image comme repere
reglable. Les curseurs `Taille`, `Deplacement X`, `Deplacement Y` et
`Profondeur Z` reconstruisent l'aperçu a chaque modification. Le bouton
`SAUVER POSITION` ecrit `soutien_gorge_placement.json` dans le dossier
`Application.persistentDataPath/LibreVies` et affiche le chemin exact a
transmettre avec le modele.

Le personnage féminin conserve son SkinnedMeshRenderer pour le corps et la
culotte. Le soutien-gorge exporté dans `vetement/export/soutien gorge.glb` est
maintenant chargé comme un vrai mesh séparé, avec sa texture PNG, puis attaché
au même squelette. Ses poids sont transférés depuis les vertices du corps vers
les vertices du vêtement : le soutien-gorge suit donc les os et les animations
au lieu d'être une image ou un quad frontal. Le corps reçoit SkinBase.png, la
culotte reste opaque sur les hanches et la forme de la poitrine n'est pas
aplatie. Aucun vêtement n'est placé comme une image flottante devant le
personnage.

## Limite importante, franchement

Une seule photo ne peut pas deviner seule le dos, la vraie profondeur, les
coutures ou les poids d'animation d'un vetement. L'atelier 3D leger fabrique
une forme parametrique editable, affichee en 3D, et utilise la photo comme
texture. Il permet donc de travailler sans Blender, mais il ne remplace pas
encore une modelisation 3D complete.

Pour un vrai vetement deformable sur le personnage, le GLB exporte doit encore
etre branche au rig MakeHuman dans Unity :

1. charger le mesh du vetement ;
2. l'attacher aux memes os que le corps ;
3. transferer les poids depuis le body ;
4. definir les zones de peau a cacher ;
5. tester les poses et les animations.

Le format GLB est utilise en priorite car son export est possible directement
en Python. L'export FBX depend d'un logiciel externe comme Blender et n'est pas
fabrique par ce script. Le nouvel atelier evite toutefois Blender pour la
creation parametrique et la premiere mise en forme.
