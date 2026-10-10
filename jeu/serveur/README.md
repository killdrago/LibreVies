# Serveur LibreVies — paquet actuel

Le serveur a une seule source : **`jeu/serveur/`**. Le jeu et le launcher
contactent PHP par HTTP, jamais MySQL directement.
Adresse locale conservee : **`http://localhost/serveur/api.php`**.

## Fichiers conserves

- `api.php` : inscription, connexion, sessions, personnage et lecture des NPC ;
- `securite.php` : generateur cryptographique des jetons de session ;
- `personnage.php` : validation, catalogues et stockage du personnage ;
- `npc.php` : validation et lecture des six profils humains du village ;
- `config.php.example` : modele pour une nouvelle installation seulement ;
- `membre.sql`, `personnage.sql`, `classement.sql`, `npc.sql` : schemas actuels
  pour une nouvelle base ; ne pas les importer inutilement dans la base existante.

Les anciennes corrections/migrations et le script de peuplement ponctuel ont
ete retires. Le build ne les demande plus et supprime leurs anciennes copies
publiques connues pendant la synchronisation. **Aucune table ni donnee SQL
n'est supprimee ou reinitialisee.** Les profils NPC existants sont conserves.
Le SQL utilise par les tests est une fixture sous `compilation/outils/fixtures`,
hors du paquet serveur : elle n'est jamais importee automatiquement.

## Configuration et synchronisation

Le vrai `config.php` reste sur le serveur : il n'est pas versionne, distribue
aux joueurs ou remplace par la synchronisation. Garder les identifiants MySQL
uniquement dans ce fichier.

`compilation/build_launcher.bat` met les sources a jour et appelle
`compilation/outils/synchroniser_serveur_local.ps1`. Cet outil trouve la racine
web d'Apache/EasyPHP/XAMPP/WAMP, ou utilise `LIBREVIES_WEB_ROOT` si definie.
Plusieurs racines possibles ne sont pas choisies au hasard. Il copie les
helpers avant `api.php`, sans importer de SQL et sans toucher aux fichiers
locaux inconnus. On peut aussi le lancer depuis la racine du projet :

```bat
powershell -NoProfile -ExecutionPolicy Bypass -File compilation\outils\synchroniser_serveur_local.ps1
```

## Parcours du joueur

- Inscription : transaction unique pour `membre` (`valider=0`, `droit=0`),
  `personnage` au meme ID (`default=1`, tous les autres champs NULL) et
  `classement` au meme ID (experience/chasse/territoire a zero).
- Aucun email n'est encore envoye. Les comptes `valider=0` peuvent se connecter.
- Connexion : `password_verify` cote PHP/BDD. Bcrypt existant reste compatible ;
  Argon2id est choisi pour les nouveaux mots de passe lorsque PHP le propose.
- `default=1` garde le personnage de base ; `default=0` reconstruit le profil
  sauvegarde. Le panel humain est ouvert par le NPC Esthetique, pas par ADMIN.
- Validation du panel : sauvegarde de la meme ligne, puis application de la
  reponse relue en BDD. Un refus conserve le brouillon et l'avatar actuel.
- Autolog reste chiffre et local dans `GAME_DIR`. Le jeton n'y est jamais stocke.

Chaque slider est une colonne `DECIMAL(10,4)` : valeurs internes **0 a 5**,
affichees 0 a 500 dans le panel, precision de quatre decimales. Aucune colonne
JSON `sliders`, `cree_le` ou `modifie_le` n'est ajoutee.

| Colonne | Reglage du createur |
|---|---|
| tete | headShape |
| yeux | eyesShape |
| nez | noseShape |
| bouche | mouthShape |
| oreilles | earsShape |
| seins | chestShape |
| volume | legThickness |
| hanche | hipShape |
| ventre | belly |
| largeur_bras | armThickness |
| longueur_bras | armLength |
| hauteur_jambe | legLength |
| pieds | feetSize |

Les choix utilisent les identifiants du catalogue MakeHuman, pas les indices
des menus. Peau : `0` a `6`. Chapeau/chaussures « Aucun » restent NULL.
Le panel ne modifie pas encore `objets` : son contenu est preserve.

## Contrat API et sessions

Les actions du jeu sont des POST formulaire ou JSON :

- `login` : pseudo/password -> membre, personnage, session token/expires_at ;
- `get_character` : session_token -> membre et personnage ;
- `save_character` : session_token et profil complet -> profil enregistre ;
- `get_npcs` : session_token -> les six profils NPC complets (lecture joueur).
- `save_npc` : session_token, npc_id, personnage -> profils relus apres sauvegarde,
  uniquement pour un membre dont la BDD indique exactement `droit=1`.

L'identite provient de la session, jamais d'un ID/pseudo/droit fourni par le
client. Les jetons sont 32 octets cryptographiques (256 bits), expires apres
24 heures, differents a chaque connexion et jamais choisis par le client.
Les appels authentifies utilisent les sessions PHP en mode strict, sans cookies
ni jeton dans l'URL. Le launcher transmet la session au seul processus du jeu
par l'environnement ; Unity l'efface ensuite de son environnement et la garde
en RAM. Aucun corps de requete, mot de passe ou jeton n'est journalise.

Reponse 401 : session absente/expiree. Reponse 400 : reglage refuse. Reponse 409 :
profil NPC absent/incomplet, a corriger dans la table `npc`. Reponse 500/503 :
probleme serveur/BDD. Reponse 403 : edition NPC refusee pour un non-administrateur.
Les ID humains actuels sont `maire`, `forgeron`, `marchand`, `esthetique`,
`garde_nord`, `garde_sud`. Les tirages en BDD ne changent pas a la connexion.

## Environnement local

Le code fonctionne avec PHP 8.3, phpMyAdmin 5.2.3 et les appels HTTP classiques
d'Apache 2.4.43, sous reserve de la configuration locale. Activer `pdo_mysql`
dans le PHP charge par Apache et garder les sessions accessibles en ecriture.
`random_bytes` est natif sur PHP 8.3 : OpenSSL n'est pas obligatoire pour les
jetons de l'API (phpMyAdmin a ses propres exigences, dont mysqli/OpenSSL).

Sous Windows, architectures PHP/module Apache doivent correspondre. Un module
Apache utilise normalement PHP Thread Safe ; FastCGI normalement Non Thread Safe.

Pour verifier la BDD sans ecriture :

```text
http://localhost/serveur/api.php?action=health
```

Utiliser HTTPS hors du poste local et ne pas exposer MySQL ni phpMyAdmin a
Internet avec les anciennes versions de cette pile. `debug=true` affiche les
erreurs SQL temporairement ; remettre a false ensuite et ne pas copier de
credentials dans les diagnostics/Git.

## Build et verification

Flux actif : **Git -> compilation/build_launcher.bat -> jeu/LibreVies.exe**.
Aucune publication ou copie dans un autre dossier n'est necessaire. Le marqueur
`jeu/game/version_jeu.json` verifie l'assembly ; un ancien jeu 0.5.79 n'est jamais
renomme artificiellement en nouvelle version. Sources actuelles : **0.5.87**.
Pseudo visible en troisieme personne. Les avatars NPC utilisent le meme
createur MakeHuman que le joueur, en conservant roots/positions/fonctions.

La methode `HumanPreview.FindBone` existe maintenant pour les accessoires NPC :
corrige l'erreur de compilation Unity CS1061 signalee. Aucun changement de licence
Unity, PHP, de ressources MakeHuman ou de reglage visuel n'est requis.

Recettes disponibles depuis la racine du projet :

```text
python -B compilation/outils/test_jeu_local.py
python -B compilation/outils/test_npc_avatars.py
python -B compilation/outils/test_npc_schema.py
python -B compilation/outils/test_entete_launcher.py
```

`test_api_personnage.mjs` teste inscription, sessions, sauvegarde, NPC, refus,
transactions et rollback sur PHP WebAssembly/SQLite de test, pas la vraie BDD.
Les controles C# statiques ne remplacent pas un build et une verification
visuelle Unity/Windows : verifier les six avatars, leurs poses/accessoires,
le pseudo et la sauvegarde chez Esthetique apres compilation native.

## Administration NPC (0.5.86)

`droit` est retourne par PHP dans l'identite du compte puis lu par Unity.
Le bouton et le panneau ADMIN ne sont pas affiches pour `droit=0` ou toute
valeur autre que 1. Aucun droit n'est attribue automatiquement : si une personne
est admin, sa colonne `membre.droit` doit etre 1, puis elle doit se reconnecter.
Le lancement direct sans compte ne donne pas de droit admin.

Les changements d'esthetique NPC utilisent le meme createur humain que le salon
(13 sliders, sexe, peau, cheveux, tenue, chapeau et chaussures). Fermer le panel
n'ecrit rien. VALIDER envoie `save_npc` ; PHP relit le droit en BDD a chaque
requete, ignore les droits/ID de compte envoyes par le client et modifie seulement
le NPC nomme et autorise. Transaction, profil relu et precision DECIMAL(10,4) ;
`objets`/positions sont preserves. Un droit retire est refuse meme avec une
ancienne session. Le joueur n'est pas modifie par une edition de NPC.

Les acces admin aux NPC passent par une liste deroulante. La barre horizontale
est construite depuis les villes des NPC actuellement presents et filtre leur
liste. Le code cree actuellement un seul village : `Village de depart`. Aucun
nom de ville fictif n'est ajoute. Creer un NPC/garde dans une autre ville peut
renseigner son parametre `ville` et fait apparaitre ce nom dans la barre. La
creation complete de villes et l'ajout de nouveaux IDs de NPC a la BDD restent
un chantier separe ; cette mise a jour n'ajoute ni ville ni colonne SQL.

Les hallebardes suivent le centre de la paume (poignet vers racine du majeur)
apres l'animation et gardent l'axe vertical du garde, pas celui de son poignet.
La reconstruction d'un avatar detache ses accessoires avant de supprimer
l'ancienne armature, puis les rattache a la nouvelle main.

## Administration des joueurs et bannissement (0.5.87 — 10/10/2026)

Dans ce projet la table s'appelle **membre**, au singulier. Nouvelle colonne :
`bani ENUM('non','oui') NOT NULL DEFAULT 'non'`. Une inscription impose `non`,
même si le client envoie un autre statut. Le schéma `membre.sql` est à jour
pour une installation neuve ; il ne modifie pas une ancienne table existante.

**Base existante : sauvegarder, puis ajouter UNE FOIS la colonne avant de
lancer la nouvelle API/le jeu.** Ne pas importer/recréer toute la table :

```sql
USE librevies;
ALTER TABLE membre
  ADD COLUMN bani ENUM('non', 'oui') NOT NULL DEFAULT 'non';
```

Si `SHOW COLUMNS FROM membre LIKE 'bani';` retourne déjà la colonne, ne pas
exécuter cet ALTER à nouveau et ne pas remettre tous les statuts à non.
Aucune migration n'est exécutée automatiquement par le build ou PHP.
L'API refuse explicitement un schéma sans cette colonne, au lieu de supposer
que tous les joueurs sont autorisés. `config.php` reste préservé.

Dans **ADMIN > Joueur** : saisie d'un pseudo, RECHERCHER, résultats cliquables,
caractéristiques en dessous, case Bannir et VALIDER. Les caractéristiques sont
les informations de compte, profil et classement **enregistrées en BDD** ; les
PV/or/position locaux sont affichés seulement pour le compte actuellement joué.
Le mot de passe/hash et les jetons ne sont jamais affichés ni retournés.
Fermer ou cocher sans VALIDER n'écrit rien. Seule la colonne `membre.bani` est
modifiée ; personnage, scores, droits, coordonnées et autres comptes sont préservés.

API POST uniquement (health reste disponible en GET) :
- `search_players`: session_token, search -> players, identité de l'admin ;
- `get_player`: session_token, player_id -> player (membre/personnage/classement) ;
- `save_player_ban`: session_token, player_id, bani='oui'/'non' -> état relu.

Ces trois actions exigent un membre **non bani, droit exactement 1**, relu à
chaque requête. Recherche partielle préparée, caractères SQL %/_/! littéraux,
30 résultats maximum. L'écriture revalide/verrouille l'admin et la cible dans
la transaction InnoDB ; les données de compte fournies par le client ne sont
jamais une preuve d'identité/droit. Un droit retiré ou une panne SQL est refusé.

Un compte `bani=oui` est refusé à la connexion et sur ses anciennes sessions,
avec HTTP 403, code `membre_bani` et le message :
`Joueur bani veuillez contacter l'administrateur`.
Le launcher **4.2.3** exige explicitement `bani=non`, revérifie via PHP au clic
JOUER et toutes les 10 secondes tant qu'il reste connecté. Un serveur inaccessible,
un champ absent ou une session invalide ne débloquent pas le jeu. Une erreur 401
réaffiche la connexion ; aucun mot de passe n'est nécessaire pour ces contrôles.
Autolog DPAPI reste local/chiffré, jamais de jeton/secret dans un fichier ou log.
Le bannissement ne prétend pas être un système de kick multijoueur : les appels
API sont interdits ; le jeu local déjà lancé n'a pas une autorité réseau de mouvement.

Tester ban puis unban sur un **autre compte de test**, pas sur son propre admin :
un auto-bannissement supprime aussi l'accès ADMIN (avertissement dans le panel).
La validation native Unity/Windows/MySQL reste à effectuer après le build 0.5.87.

Les armes des gardes sont maintenant alignées sur le centre mesuré du **mesh de
hampe importé**, pas uniquement sur le pivot vide (OBJ éventuellement décentré/
miroité). Compensation TransformVector après animation, hauteur du pseudo inchangée,
texte du pseudo réduit de 0.035 à 0.0175. Aucune ressource MakeHuman modifiée.

Recette complémentaire : `python -B compilation/outils/test_bani.py`.
