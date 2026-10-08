# API d'authentification LibreVies

Le launcher ne se connecte pas directement à MySQL : cela obligerait à
livrer le mot de passe de la base avec le jeu. Il appelle `api.php` avec
l'adresse IP du serveur, et PHP utilise MySQL sur le port 3306 côté serveur.

## Installation chez l'hebergeur

1. Envoyer ce dossier `serveur/` en entier chez l'hebergeur, dans le dossier
   web public. L'URL sera alors par exemple :
   `http://92.133.115.121/serveur/api.php`.
2. Copier `config.php.example` sous le nom `config.php` dans ce meme dossier.
3. Renseigner dans `config.php` le nom, l'utilisateur et le mot de passe de
   la base `librevies`.
4. Importer `membre.sql` si la table n'existe pas. Si la table existe déjà
   avec les types de la capture, importer d'abord `corriger_membre.sql`.
5. Si la table `membre` existe déjà sans la colonne `valider`, importer une
   seule fois `ajouter_valider.sql`. La valeur `0` signifie « non valide »
   et la valeur `1` signifie « valide ».
6. Tester :
   `http://92.133.115.121/serveur/api.php?action=health`
   doit répondre `API et base de données accessibles.`.

Si l'inscription répond « Connexion à la base impossible », passez
provisoirement `debug` à `true` dans `config.php`, réessayez, puis remettez-le
à `false`. Le détail SQL apparaîtra alors dans le launcher et sera également
écrit dans le journal PHP d'Apache.

L'adresse modifiable par l'utilisateur est dans `auth_config.json`, à côté de
`LibreVies.exe`. Elle doit pointer vers le dossier `serveur/` de l'hébergeur :

```json
{
  "api_url": "http://92.133.115.121/serveur/api.php",
  "timeout": 15
}
```

Si Apache écoute sur un autre port, ajoutez-le dans l'URL, par exemple
`http://92.133.115.121:8080/serveur/api.php`.

Le mot de passe n'est pas enregistré en MD5. L'API utilise `password_hash`
avec Argon2id si PHP le fournit, sinon l'algorithme moderne par défaut de PHP,
et vérifie ensuite avec `password_verify`. Le port `3306` ne doit donc pas être
exposé au launcher ni ouvert sur Internet.
