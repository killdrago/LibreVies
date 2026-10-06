# API d'authentification LibreVies

Le launcher ne se connecte pas directement à MySQL : cela obligerait à
livrer le mot de passe de la base avec le jeu. Il appelle `api.php` avec
l'adresse IP du serveur, et PHP utilise MySQL sur le port 3306 côté serveur.

## Installation XAMPP

1. Copier le dossier `serveur` dans `C:\xampp\htdocs\librevies`.
2. Renommer `config.php.example` en `config.php`.
3. Renseigner dans `config.php` le nom, l'utilisateur et le mot de passe de
   la base `librevies`.
4. Importer `membre.sql` dans phpMyAdmin, ou créer la table avec les mêmes
   types.
5. Vérifier depuis le PC du joueur :
   `http://92.133.115.121/librevies/api.php`
   doit répondre que la méthode GET n'est pas autorisée. Cela prouve que
   Apache est joignable ; les connexions du launcher utilisent POST.
6. Ouvrir le port HTTP d'Apache et autoriser Apache dans le pare-feu si le
   launcher est utilisé depuis un autre réseau.

L'adresse modifiable par l'utilisateur est dans `auth_config.json`, à côté de
`LibreVies.exe` :

```json
{
  "api_url": "http://92.133.115.121/librevies/api.php",
  "timeout": 15
}
```

Le port `3306` est utilisé entre PHP et MySQL. Il ne doit pas être exposé au
launcher ni ouvert sur Internet.
