# API de compte et de personnage LibreVies

**Adresse locale conservée : `http://localhost/serveur/api.php`.**
Le launcher et le jeu appellent PHP. Ils ne se connectent jamais à MySQL :
les identifiants de la base restent dans le vrai `config.php` du serveur,
qui n'est pas versionné ni distribué aux joueurs.
Le paquet serveur a une seule source : **`jeu/serveur/`**.

## Mise à jour locale

`compilation/build_launcher.bat` met les sources à jour puis appelle
`compilation/outils/synchroniser_serveur_local.ps1`. Le build local de diagnostic
`compilation/build_unity_game.bat` appelle aussi cet outil, sans téléchargement.
Il repère l'API locale existante sous XAMPP, EasyPHP ou WAMP et synchronise les
fichiers publics depuis `jeu/serveur/`, notamment **`api.php`, `personnage.php`
et `securite.php`**. Il n'est pas necessaire de reconstruire Unity pour une
correction uniquement PHP : l'outil de synchronisation peut etre lance seul.
**Il ne remplace jamais `config.php` et n'importe aucun SQL automatiquement.**

Pour une installation Apache non standard, `LIBREVIES_WEB_ROOT` permet de
préciser sa racine web. L'outil indique explicitement si aucun serveur n'a été
repéré ; il ne modifie pas une installation distante et ne choisit pas au hasard
entre plusieurs racines. Il peut également être exécuté seul :

```bat
powershell -NoProfile -ExecutionPolicy Bypass -File compilation\outils\synchroniser_serveur_local.ps1
```

Les fichiers à utiliser pour la base sont ceux du **projet** :

- `jeu/serveur/membre.sql` : création d'une nouvelle table `membre` ;
- `jeu/serveur/corriger_membre.sql` : correction des types d'une ancienne table ;
- `jeu/serveur/personnage.sql` : création de la table nullable actuelle ;
- `jeu/serveur/classement.sql` : table `classement` liee au meme ID de membre,
  avec `experience`, `chasse` et `territoire` a zero. Ce fichier complete aussi
  les comptes deja inscrits qui n'ont pas de ligne, sans effacer les scores
  des joueurs deja presents ;
- `jeu/serveur/mettre_a_jour_personnage.sql` : migration d'une ancienne table
  possédant déjà ces colonnes. Sauvegarder la base avant cette migration.
  Elle n'efface pas la table, préserve les profils `default=0`, rend les champs
  nullable et nettoie uniquement les anciennes valeurs de base `default=1`.

`CREATE TABLE IF NOT EXISTS` ne corrige pas les types d'une table déjà existante :
la migration est nécessaire si ses champs sont encore `NOT NULL`.

## Parcours du joueur

1. **Inscription** : création de `membre` avec `valider=0` et, dans la même
   transaction, de `personnage` avec le même `id` et `default=1`, puis de
   `classement` avec cet ID et `experience=0`, `chasse=0`, `territoire=0`.
   Tous les autres champs du personnage restent **NULL**, y compris `sexe`.
   Si une des trois insertions echoue, aucune des trois lignes n'est conservee.
   La table `classement` doit exister avant une nouvelle inscription : utiliser
   le fichier du projet `jeu/serveur/classement.sql` si elle n'existe pas encore.
2. **Connexion** : vérification du mot de passe par PHP/BDD. Un compte
   `valider=0` peut encore se connecter. Aucun email n'est envoyé pour l'instant.
   L'option `require_email_validation` reste absente ou `false` jusqu'à ce que
   la validation par email soit réellement mise en place.
3. **Jeu** : le profil est récupéré avant la création du joueur.
   `default=1` garde la base actuelle ; `default=0` reconstruit le profil sauvegardé.
   Une erreur de chargement reste visible, sans remplacer silencieusement le
   personnage par la base.
4. **Maison Esthétique** : parler au NPC et utiliser le panel humain existant.
   Le créateur n'est plus accessible depuis ADMIN. Le projet autonome
   `compilation/personnage/` n'est pas utilisé pour ce parcours.
5. **Valider / appliquer** : les réglages sont validés côté serveur et enregistrés
   dans la même ligne ; `default=0` et les réglages sont modifiés ensemble.
   Le jeu applique ensuite la réponse relue en BDD. Les contrôles sont bloqués
   pendant la requête ; un refus garde le panel ouvert sans faux succès.
   Fermer le panel sans avoir lancé une validation n'écrit rien en BDD.
   En cas de réponse réseau perdue, la confirmation est inconnue : le profil
   serveur sera relu à la prochaine connexion.
6. Le vrai **pseudo du compte** est affiché au-dessus de la tête. Le prochain
   passage au salon reprend les réglages sauvegardés, pas le dernier brouillon annulé.

Un ancien compte sans ligne `personnage` reçoit seulement sa ligne de base,
sans remplacer un profil déjà existant. Il n'y a pas de personnages multiples.

## Réglages persistés

Chaque slider a sa propre colonne `DECIMAL(10,4)` ; aucune colonne JSON `sliders`,
ni `cree_le` ou `modifie_le` n'est ajoutée. Valeurs internes : **0 à 5**,
affichées **0 à 500** dans le panel existant. Le serveur normalise à quatre décimales.

| Colonne SQL | Réglage du panel / du créateur |
|---|---|
| `tete` | `headShape` |
| `yeux` | `eyesShape` |
| `nez` | `noseShape` |
| `bouche` | `mouthShape` |
| `oreilles` | `earsShape` |
| `seins` | `chestShape` — Seins volume |
| `volume` | `legThickness` — Jambes largeur |
| `hanche` | `hipShape` |
| `ventre` | `belly` |
| `largeur_bras` | `armThickness` |
| `longueur_bras` | `armLength` |
| `hauteur_jambe` | `legLength` |
| `pieds` | `feetSize` |

`sexe` vaut `homme` ou `femme` seulement après création. `teinte_peau` est
l'identifiant `0` à `6`. `coiffure`, `tenue`, `chapeau` et `chaussures` utilisent
les **identifiants stables des ressources**, pas les indices des menus :
les catalogues autorisés sont dans `personnage.php` et correspondent à
`MakeHumanClothingFactory`. « Aucun » reste **NULL** pour chapeau/chaussures,
même si JsonUtility transporte une chaîne vide. La colonne `objets` est
conservée : aucun contrôle du panel actuel ne permet encore de la modifier.
Les ressources, morphologies et réglages de rendu MakeHuman restent inchangés.

## Authentification des appels du jeu

- `login` reçoit `pseudo` et `password`, puis retourne `membre`, `personnage`
  et `session` (`token`, `expires_at`). Le mot de passe n'est jamais retourné.
- `get_character` reçoit seulement `session_token` pour l'identité.
- `save_character` reçoit `session_token` et `personnage` (profil complet).
  Les éventuels `id`, `pseudo`, `default` et `objets` envoyés par le client
  ne permettent ni de choisir un autre compte ni d'écraser ses données.
- PHP garde la session côté serveur, avec une expiration de 24 heures et
  nouvel identifiant aléatoire de 256 bits à chaque connexion, jamais choisi par
  le client. Les appels authentifiés utilisent le mode strict. Le helper
  `securite.php` essaie `random_bytes`, puis OpenSSL, puis le generateur
  cryptographique du systeme : `/dev/urandom` sous Unix ou le CSPRNG Windows
  de .NET via PowerShell sous Windows. Ce dernier permet la connexion sur
  PHP 5.6/EasyPHP sans avoir a activer l'extension OpenSSL. `proc_open` et le
  PowerShell du systeme doivent alors etre autorises cote serveur. La commande
  est fixe, sans donnee HTTP, mot de passe ou jeton. Aucun `rand`, `mt_rand`
  ou `uniqid` n'est utilise en secours : sans source cryptographique accessible,
  la connexion est refusee plutot que de creer un jeton previsible.
  Aucune table de session n'est ajoutée ; le dossier de sessions PHP doit être inscriptible.
- Le launcher garde le jeton en RAM et le transmet au seul processus du jeu via
  `LIBREVIES_SESSION_TOKEN` et `LIBREVIES_API_URL`, jamais dans les arguments,
  les fichiers Autolog, le manifeste ou les journaux. Le jeu efface la variable
  de session de son environnement puis conserve sa session uniquement en RAM.
  Une recharge de scène ne fait pas perdre cette session.
- Les clients utilisent un POST formulaire, compatible PHP 5.6. Le profil est
  encodé en JSON uniquement pour son transport ; les colonnes SQL restent séparées.
  Un POST JSON direct est aussi accepté. Une session manquante/expirée renvoie 401,
  un profil invalide 400, une panne serveur 500/503.

L'Autolog local existant reste chiffré, dans `GAME_DIR`. Décocher Autolog supprime
ses fichiers locaux ; le jeton de session n'y est jamais enregistré.

## Installation chez un hébergeur

Envoyer le contenu de **`jeu/serveur/`** au serveur web. Pour une nouvelle
installation seulement, créer le vrai `config.php` à partir de
`jeu/serveur/config.php.example` et renseigner la base côté serveur.
Conserver le `config.php` existant lors des mises à jour.
Importer les fichiers SQL du projet adaptés à l'état de la base, puis tester :

```text
http://localhost/serveur/api.php?action=health
```

Le résultat attendu est `API et base de donnees accessibles.`.
Pour une installation distante, modifier `api_url` dans `auth_config.json`
à côté du launcher. **Utiliser HTTPS hors du poste local** pour protéger le
mot de passe et la session pendant leur transport ; ne jamais exposer le port
MySQL 3306 au launcher. Les mots de passe utilisent `password_hash` (Argon2id
si disponible, sinon l'algorithme moderne par défaut), puis `password_verify`.

`debug=true` permet temporairement d'obtenir le détail d'une erreur SQL.
Le remettre à `false` ensuite. Aucun mot de passe ni jeton ne doit être copié
dans les diagnostics ou dans Git.

## Vérifications

Depuis la racine du projet :

```text
python -B compilation/outils/test_personnage.py
python -B compilation/outils/test_launcher.py
python -B compilation/outils/test_edition.py
python -B compilation/outils/test_amorce.py
python -B compilation/outils/verifier_cs_syntaxe.py
php compilation/outils/test_securite.php
```

Une recette exécutable PHP est aussi fournie dans
`compilation/outils/test_api_personnage.mjs` (instructions en tête du fichier).
Elle vérifie inscription/connexion, profils complets, NULL, sessions, tentative
d'usurpation, expiration, refus sans écriture et rollback en cas de panne.
Elle verifie aussi l'inscription membre/personnage/classement, les scores a zero,
le rollback si classement echoue, la preservation des scores existants et
une connexion sans random_bytes/OpenSSL. Les I/O du chemin Windows sont simulees
pour les tests unitaires ; l'execution reelle de PowerShell necessite Windows.
Elle utilise **PHP WebAssembly et SQLite de test en RAM**, pas la vraie base
MySQL ; elle ne remplace donc pas la recette finale MySQL/Unity.

Après compilation Unity, vérifier en jeu : base sur un compte neuf, ouverture
chez Esthétique, annulation, sauvegarde des 13 sliders et des choix, reconnexion
avec le même aspect, édition du profil existant, pseudo en troisième personne,
absence du créateur dans ADMIN et erreur visible si l'API est coupée.
Les modifications des sources ne mettent pas à jour un ancien exécutable Unity :
il faut reconstruire puis publier le jeu avec le flux de compilation existant.
