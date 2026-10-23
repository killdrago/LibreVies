# Plan : melanger des pieces generees par plusieurs moteurs

Statut : brouillon, rien n'est code. A valider avant implementation.

## Decisions (retour utilisateur)
- Generalite : l'app doit decouper N'IMPORTE QUELLE partie de N'IMPORTE QUEL objet.
  Les listes ci-dessous sont des exemples, pas une liste fermee.
- Nommage automatique par defaut, avec edition manuelle des noms et une possibilite
  de rejeter le decoupage si ca ne plait pas.

## Idee
Pour une meme photo, on genere deux versions (SF3D et TripoSR), on decoupe
chaque version en pieces nommees, puis l'utilisateur choisit pour chaque piece
le moteur qui la rend le mieux. Le fichier final est assemble a partir de ces choix.

Exemple (marteau) : manche SF3D + masse TripoSR.

## Exemples de pieces (indicatifs, non obligatoires)
- Marteau : manche, masse
- Immeuble : mur_face, mur_gauche, mur_droit, mur_arriere, toit, fenetres, porte
- Souris : queue, tete, corps, pattes
- Voiture : pare_chocs_av, pare_chocs_ar, porte_avg, porte_avd, porte_arg, porte_ard,
  toit, pare_brise, vitre_avg, vitre_avd, vitre_arg, vitre_ard,
  roue_avg, roue_avd, roue_arg, roue_ard

## Etapes
1. Generer la version A (SF3D) et la version B (TripoSR) de la meme photo.
2. Normaliser les deux : meme echelle, axe vertical Y, centre sur la base.
3. Decouper chaque version en pieces, de facon generique (segmentation geometrique du maillage :
   composantes connexes, regroupement par proximite et orientation des normales). Nommage automatique
   (piece_1, piece_2... ou nom propose), editable par l'utilisateur. Un bouton permet de refaire le
   decoupage ou de fusionner / separer des pieces. Sortie : un catalogue JSON
   (moteur, nom de piece, fichier GLB, boite englobante, texture).
4. Affichage dans la page 3D : pour chaque piece, choix du moteur (liste).
5. Assemblage : les pieces choisies sont posees dans le repere commun, puis
   exportees dans un seul GLB avec des materiaux homogenes.

## Points de vigilance
- Les decoupages des deux moteurs ne donnent pas toujours les memes morceaux.
  Il faut soit un nommage manuel, soit une association par position.
- Formats de texture differents (UV et materiaux pour SF3D, couleurs de sommets
  pour TripoSR) : a ramener a un format commun dans le GLB final.
- Temps : deux generations, plus lentes en CPU.
- Licence : SF3D (Community) et TripoSR (MIT), a verifier pour un usage commercial.

## Limites connues
- Le nommage automatique par la forme seule est approximatif : il faut prevoir l'edition.
- Un decoupage generique peut couper une piece en plusieurs morceaux ou en fusionner deux.

## Questions ouvertes
- Prototype d'abord sur un objet simple (marteau) pour valider la chaine, puis generaliser ?
