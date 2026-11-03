# Journal de diagnostic DMTrader

## Installer

Dans `index.html`, le script `./diag.js` est chargé **en premier** (avant
`journal.js` et `index.js`). Il suffit donc de copier `diag.js` à la racine du
dossier de l'app.

## Lancer

Lancer l'app normalement. Toutes les mesures sont écrites :

- dans la console de l'app (DevTools, touche F12) avec le préfixe `[DIAG]`
- dans le fichier **`diag.log`** à la racine de l'app (à côté de `dmtrader.exe`)

## Lire le journal

| Ligne | Signification |
|-------|---------------|
| `MILESTONE` | Jalons : fin du parse du bundle (2.8 MB), rendu React, dialogue de connexion |
| `NET ... START/FIN` | Requêtes réseau : URL, statut, octets, durée. `[API/UPDATE]` = serveur `92.222.92.14` (login + vérification de mise à jour), `SLOW!` si > 2 s |
| `TIMERS actifs` | Tous les `setInterval` avec leur période, leur nombre d'exécutions et leur origine (trace de la pile) |
| `RES` | Mémoire (RSS/heap), lag de l'event loop, nombre de timers tous les 5 s |
| `ALERTE lag event loop` | L'UI est bloquée (le fil d'événements ne tourne plus) |
| `FS` | Opérations fichiers : copies des EA TraderConnect, dézip de mise à jour |
| `JS-ERROR` / `APP-ERR` | Erreurs JS de l'app |
| `EVENT nw.App.quit` | Fermetures de l'app (cycle auto-update) |

## Console manuelle (DevTools)

```js
__diag.report()    // état complet : mémoire, lag, dernières requêtes, timers actifs
__diag.net()       // dernières requêtes réseau
__diag.timers()    // timers actifs avec origine
__diag.mute()      // suspendre le journal
__diag.unmute()    // le reprendre
__diag.mark("marque")  // ajouter un jalon personnalisé
```

## Coexistence avec le journal d'activité

Le `journal.js` (journal d'activité trading : connexions, ordres, signaux) et
`diag.js` (journal de performances) sont complémentaires : le premier
décrit les faits trading, le second mesure où passent le temps et les
ressources. `diag.js` doit être chargé avant `journal.js` pour que les deux
captent correctement les `console.error`.

## Ce qu'il permet de trouver

- **l'app est lente au démarrage** : chronologie des `MILESTONE` + durées des
  `NET` vers `92.222.92.14` (vérification de mise à jour) et des opérations `FS`
- **l'app consomme CPU/mémoire en continu** : lignes `RES` + `ALERTE lag` +
  fréquences d'exécution dans `TIMERS actifs`
- **l'app se ferme/redémarre toute seule** : `EVENT nw.App.quit` après un gros
  `NET ... FIN ... dmtrader.zip` = l'auto-update a déclenché un téléchargement
