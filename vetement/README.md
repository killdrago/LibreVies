# Atelier de vetements LibreVies

Ce dossier contient un petit logiciel Python autonome pour preparer les futurs
vetements du jeu.

## Installation Windows

1. Installer Python 3.10 ou plus recent depuis python.org.
2. Double-cliquer sur `lancer_vetement.bat`.
3. Le script installe Pillow puis ouvre l'atelier.

Installation manuelle :

```text
python -m pip install -r requirements.txt
python vetement.py
```

## Fonctionnement actuel

### Depuis une image

- `Importer une image` charge un PNG, JPG ou WEBP.
- `Decouper le fond` rend transparent le fond connecte aux bords de l'image.
- L'image transparente est exportee dans un GLB avec son motif.
- Le GLB est un panneau 3D epais, utile pour une image decoupee, un logo, un
  motif ou une piece plane.

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
- `nom.json` : position, zone d'attache et zone de couverture du corps.

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

Le maillage humain conserve ici son matériau Standard d'origine pour rester
stable dans l'aperçu. Les triangles de peau couverts par l'alpha du soutien-gorge
sont retires du premier sous-maillage, puis le PNG est dessine dans un second
sous-maillage du meme SkinnedMeshRenderer, avec les memes vertices, os et poids.
Le devant et les bretelles arrière suivent ainsi directement la surface du corps :
il n'y a plus de quad frontal ni de ruban flottant. La culotte et le calecon
restent volontairement reportes.

## Limite importante, franchement

La decoupe d'une image ne peut pas deviner seule la vraie profondeur d'un
vetement, ses coutures, ses bretelles 3D et son adaptation a tous les os. La
premiere version fabrique donc un objet image epais ou une forme parametrique.
Pour un vrai vetement deformable sur un personnage, il faudra ensuite :

1. positionner le GLB sur le personnage ;
2. ajuster les points d'attache ;
3. ajouter un skinning sur le squelette ;
4. definir les zones de peau a cacher.

Le format GLB est utilise en priorite car son export est possible directement
en Python. L'export FBX depend d'un logiciel externe comme Blender et n'est pas
fabrique par ce script.
