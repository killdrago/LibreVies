# API d'authentification LibreVies

Le launcher ne se connecte pas directement à MySQL : cela obligerait à
livrer le mot de passe de la base avec le jeu. Il appelle `api.php` avec
l'adresse IP du serveur, et PHP utilise MySQL sur le port 3306 côté serveur.

## Installation XAMPP

1. Copier le contenu de `serveur/` dans `C:\xampp\htdocs\librevies\`.
2. Renommer `config.php.example` en `config.php`.
3. Renseigner dans `config.php` le nom, l'utilisateur et le mot de passe de
   la base `librevies`.
4. Si la table `membre` existe déjà avec les types de la capture, importer
   d'abord `corriger_membre.sql` (la table doit encore être vide). Sinon,
   importer `membre.sql` pour créer la base et la table correctement.
5. Démarrer Apache et MySQL dans XAMPP.
6. Vérifier depuis le PC du joueur :
   `http://92.133.115.121/librevies/api.php?action=health`
   doit répondre `API et base de données accessibles.`.
7. Ouvrir le port HTTP d'Apache et autoriser Apache dans le pare-feu si le
   launcher est utilisé depuis un autre réseau.

Si l'inscription répond « Connexion à la base impossible », passez
provisoirement `debug` à `true` dans `config.php`, réessayez, puis remettez-le
à `false`. Le détail SQL apparaîtra alors dans le launcher et sera également
écrit dans le journal PHP d'Apache.

L'adresse modifiable par l'utilisateur est dans `auth_config.json`, à côté de
`LibreVies.exe` :

```json
{
  "api_url": "http://92.133.115.121/librevies/api.php",
  "timeout": 15
}
```

Si le launcher est utilisé sur le même réseau que le serveur, remplacez
l'adresse publique par l'IPv4 LAN du serveur, par exemple
`http://192.168.1.42/librevies/api.php`. Si Apache écoute sur un autre port,
ajoutez-le dans l'URL, par exemple `:8080`.

Le mot de passe n'est pas enregistré en MD5. L'API utilise `password_hash`
avec Argon2id si PHP le fournit, sinon l'algorithme moderne par défaut de PHP,
et vérifie ensuite avec `password_verify`. Le port `3306` ne doit donc pas être
exposé au launcher ni ouvert sur Internet.
