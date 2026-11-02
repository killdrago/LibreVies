# Connecteur DMTrader pour MT4

`TraderConnect.mq4` est une reconstruction indépendante du connecteur absent en source. Elle ne décompile pas `TraderConnect.ex4`.

## Données transmises à DMTrader

- compte, terminal, symboles de la fenêtre Market Watch et ordres ouverts ;
- bougies OHLCV courantes pour les symboles et périodes MT4 disponibles ;
- historique OHLCV demandé par DMTrader via `CopyRates` ;
- historique des ordres (jusqu'à la limite configurée dans les paramètres de l'EA).

L'application reçoit ainsi des données de graphique sous forme de bougies et non des captures d'écran. Le connecteur traite également les actions `send`, `modify`, `close` et `delete`.

## Installation et compilation

1. Ouvrir `TraderConnect.mq4` dans MetaEditor et compiler avec **F7**. MetaEditor doit produire `TraderConnect.ex4` à côté du source. Le `.ex4` fourni avec l'application peut être ancien.
2. Dans MT4, utiliser **Fichier > Ouvrir le dossier de données**, puis copier le `.ex4` compilé dans `MQL4/Experts`, en remplaçant l'ancien fichier du terminal utilisé. L'application ne copie désormais son EA intégré que si le fichier cible est absent : elle ne remplacera plus une version déjà installée à chaque démarrage.
3. Dans MT4, ouvrir **Outils > Options > Expert Advisors**, autoriser WebRequest pour `http://127.0.0.1:2122`, puis activer **Autoriser le trading algorithmique**.
4. Attacher l'EA à un seul graphique de ce terminal. Le serveur DMTrader écoute par défaut sur le port `2122` ; adapter `ServerURL` si le port a été changé dans DMTrader.
5. Tester d'abord sur un compte démo. Le paramètre `AllowTradeCommands` est volontairement désactivé par défaut : l'activer dans les propriétés de l'EA seulement après vérification du flux de signaux et des volumes.

La compilation et l'exécution n'ont pas été vérifiées ici : MetaEditor/MT4 n'est pas disponible dans l'environnement de développement.
