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
- `get_npcs` : session_token -> les six profils NPC complets (lecture seule).

L'identite provient de la session, jamais d'un ID/pseudo/droit fourni par le
client. Les jetons sont 32 octets cryptographiques (256 bits), expires apres
24 heures, differents a chaque connexion et jamais choisis par le client.
Les appels authentifies utilisent les sessions PHP en mode strict, sans cookies
ni jeton dans l'URL. Le launcher transmet la session au seul processus du jeu
par l'environnement ; Unity l'efface ensuite de son environnement et la garde
en RAM. Aucun corps de requete, mot de passe ou jeton n'est journalise.

Reponse 401 : session absente/expiree. Reponse 400 : reglage refuse. Reponse 409 :
profil NPC absent/incomplet, a corriger dans la table `npc`. Reponse 500/503 :
probleme serveur/BDD. Aucun endpoint ne permet au client de modifier un NPC.
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
renomme artificiellement en nouvelle version. Sources actuelles : **0.5.85**.
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
