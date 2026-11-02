# Connecteur DMTrader pour MT4

`TraderConnect.mq4` est une reconstruction indépendante : le dépôt ne contient pas le source de l'ancien `TraderConnect.ex4`. Les paramètres historiques `Host`, `Port`, `Interval` et la trace `+Open Inet...` suggèrent un transport HTTP basé sur les API Internet de Windows, mais ne permettent pas de confirmer les appels exacts de l'ancien EA. Cette version utilise **WinINet** pour parler au serveur HTTP/JSON local de DMTrader ; ce n'est ni une décompilation ni une copie vérifiée de l'ancien binaire.

## Données et actions

- transmet le compte, le terminal, les symboles de Market Watch et les ordres ouverts ;
- transmet les bougies OHLCV courantes et l'historique demandé par DMTrader ;
- traite les actions `send`, `modify`, `close` et `delete`.

Les commandes de trading restent désactivées par défaut (`AllowTradeCommands=false`). Aucun ordre n'est envoyé automatiquement par le connecteur.

## Installation et compilation

1. Ouvrir `TraderConnect.mq4` dans MetaEditor et compiler avec **F7**. La compilation doit produire un nouveau `TraderConnect.ex4` à côté du source. Le `.ex4` déjà fourni par l'application est conservé tel quel et peut correspondre à une ancienne version.
2. Dans MT4, ouvrir **Fichier > Ouvrir le dossier de données**, puis copier le `.ex4` compilé dans `MQL4/Experts` du terminal utilisé. DMTrader ne copie désormais son EA intégré que si le fichier cible est absent ; il ne remplacera plus un EA déjà installé au démarrage.
3. Dans **Outils > Options > Expert Advisors** de MT4, autoriser les imports de DLL. Dans les propriétés de l'EA, onglet **Commun**, cocher également **Autoriser les imports de DLL**. Ce connecteur appelle `wininet.dll` ; il n'utilise pas `WebRequest`, donc aucune liste d'URL WebRequest n'est nécessaire.
4. Attacher l'EA à un seul graphique. Les paramètres réseau sont `Host=127.0.0.1`, `Port=2122` et `Interval=1000` millisecondes (minimum appliqué : 250 ms). Si DMTrader écoute sur un autre port, modifier `Port`.
5. Vérifier d'abord que le journal MT4 affiche `+Open Inet...` et que DMTrader reconnaît le terminal. Garder `AllowTradeCommands` désactivé jusqu'à confirmation de la connexion. N'activer des commandes d'ordre que pour un essai contrôlé sur un compte démo.

La compilation et l'exécution de ce source WinINet n'ont pas pu être vérifiées dans cet environnement : MetaEditor/MT4 n'y est pas installé. Le fichier `.ex4` fourni n'a pas été recompilé ni remplacé.
