(function () {
  "use strict";

  var STORAGE_KEY = "dmtrader.activityJournal.v1";
  var MAX_ENTRIES = 500;
  var entries = loadEntries();
  var dedupe = {};
  var lastHeartbeat = {};
  var lastTickAt = {};
  var staleLinks = {};
  var orderSnapshots = {};
  var unread = 0;
  var isOpen = false;
  var activeFilter = "all";
  var searchText = "";
  var toggleButton;
  var badge;
  var dialog;
  var panelNode;
  var listNode;
  var statusNode;
  var searchNode;

  function loadEntries() {
    try {
      var value = window.localStorage.getItem(STORAGE_KEY);
      var parsed = value ? JSON.parse(value) : [];
      return Object.prototype.toString.call(parsed) === "[object Array]"
        ? parsed.slice(-MAX_ENTRIES)
        : [];
    } catch (error) {
      return [];
    }
  }

  function saveEntries() {
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(entries));
    } catch (error) {
      // The journal remains usable for this session if storage is unavailable.
    }
  }

  function text(value) {
    if (value === null) return "null";
    if (typeof value === "undefined") return "";
    if (value instanceof Error) return value.name + ": " + value.message;
    if (typeof value === "string") return value;
    try {
      var serialized = JSON.stringify(value);
      return typeof serialized === "undefined" ? String(value) : serialized;
    } catch (error) {
      return String(value);
    }
  }

  function detailText(value) {
    if (value === null || typeof value === "undefined" || value === "") return "";
    var result;
    try {
      result = typeof value === "string" ? value : JSON.stringify(value, null, 2);
      if (typeof result === "undefined") result = String(value);
    } catch (error) {
      result = String(value);
    }
    if (result.length > 5000) result = result.slice(0, 5000) + "\n… détails tronqués";
    return result;
  }

  function add(category, level, message, details, options) {
    options = options || {};
    message = text(message).trim();
    if (!message) return;

    var now = Date.now();
    var key = options.dedupeKey || "";
    var duration = options.throttleMs || 0;
    if (key && duration && dedupe[key] && now - dedupe[key] < duration) return;
    if (key) dedupe[key] = now;

    var entry = {
      id: now.toString(36) + Math.random().toString(36).slice(2, 7),
      time: now,
      category: category || "Système",
      level: level || "info",
      message: message,
      details: detailText(details)
    };
    entries.push(entry);
    if (entries.length > MAX_ENTRIES) entries.splice(0, entries.length - MAX_ENTRIES);
    saveEntries();

    if (!isOpen) unread += 1;
    updateBadge();
    if (isOpen) renderEntries();
  }

  function updateBadge() {
    if (!badge) return;
    badge.textContent = unread > 99 ? "99+" : String(unread);
    badge.hidden = unread < 1;
    toggleButton.setAttribute(
      "aria-label",
      unread ? "Ouvrir le journal, nouveaux événements : " + unread : "Ouvrir le journal d’activité"
    );
  }

  function formattedTime(timestamp) {
    try {
      return new Date(timestamp).toLocaleString("fr-FR", {
        day: "2-digit", month: "2-digit", year: "2-digit",
        hour: "2-digit", minute: "2-digit", second: "2-digit"
      });
    } catch (error) {
      return new Date(timestamp).toLocaleString();
    }
  }

  function formattedNumber(value, decimals) {
    var number = Number(value);
    if (!isFinite(number)) return text(value);
    try {
      return number.toLocaleString("fr-FR", {
        maximumFractionDigits: decimals === undefined ? 2 : decimals
      });
    } catch (error) {
      return String(number);
    }
  }

  function orderSide(value) {
    var types = ["ACHAT", "VENTE", "ACHAT LIMITÉ", "VENTE LIMITÉE", "ACHAT STOP", "VENTE STOP"];
    var index = Number(value);
    return isFinite(index) && index >= 0 && index < types.length
      ? types[index]
      : text(value || "ORDRE").toUpperCase();
  }

  function isPendingOrder(order) {
    var type = Number(order && order.type);
    return type >= 2 && type <= 5;
  }

  function sameValue(left, right) {
    if (left === right) return true;
    if (left === null || typeof left === "undefined") return right === null || typeof right === "undefined";
    if (right === null || typeof right === "undefined") return false;
    return String(left) === String(right);
  }

  function orderDescription(order) {
    var type = orderSide(order.type);
    var lots = order.lots !== undefined ? order.lots : order.volume;
    return type + " " + (order.symbol || "symbole inconnu") + " · " +
      formattedNumber(lots, 4) + " lot(s) · ticket " + (order.ticket || "?");
  }

  function orderMap(orders) {
    var result = {};
    (orders || []).forEach(function (order, index) {
      var key = order.ticket !== undefined ? String(order.ticket) : "row-" + index;
      result[key] = order;
    });
    return result;
  }

  function tick(payload) {
    if (!payload || payload.error || !payload.result) return;
    var result = payload.result;
    var account = result.account || {};
    var terminal = payload.terminal || (result.terminal && result.terminal.name) || "MT4";
    var login = payload.login || account.login || "?";
    var key = String(terminal) + ":" + String(login);
    var now = Date.now();
    var previousHeartbeat = lastHeartbeat[key];
    var wasStale = staleLinks[key] === true;
    lastTickAt[key] = now;
    if (wasStale) {
      staleLinks[key] = false;
      add("Connexion", "success", "Le signal MT4 est revenu pour le compte " + login + ".", {
        terminal: terminal,
        compte: login
      });
    }

    if (!previousHeartbeat || now - previousHeartbeat >= 60000) {
      var marketSymbols = Object.prototype.toString.call(result.symbols) === "[object Array]" ? result.symbols : [];
      var marketSnapshot = marketSymbols.slice(0, 8).map(function (symbol) {
        return {
          symbole: symbol.name,
          bid: symbol.bid,
          ask: symbol.ask,
          spread: symbol.spread,
          heure: symbol.time
        };
      });
      var detail = {
        terminal: terminal,
        compte: login,
        serveur: account.server || "",
        solde: account.balance,
        equity: account.equity,
        devise: account.currency || "",
        symbolesSurveillés: marketSymbols.length,
        aperçuMarché: marketSnapshot,
        symbolesNonAffichés: Math.max(0, marketSymbols.length - marketSnapshot.length),
        positionsOuvertes: (result.orders || []).length
      };
      add(
        previousHeartbeat ? "Vérification" : "Connexion",
        previousHeartbeat ? "info" : "success",
        previousHeartbeat
          ? "Synchronisation MT4 toujours active pour le compte " + login + "."
          : "MT4 connecté : " + terminal + " · compte " + login + ".",
        detail,
        { dedupeKey: "heartbeat:" + key, throttleMs: 59000 }
      );
      lastHeartbeat[key] = now;
    }

    if (statusNode) {
      statusNode.textContent = "MT4 connecté · compte " + login;
      statusNode.className = "dmt-journal-status is-connected";
    }

    var orders = Object.prototype.toString.call(result.orders) === "[object Array]" ? result.orders : [];
    var current = orderMap(orders);
    var previous = orderSnapshots[key];
    if (!previous) {
      add(
        "Vérification",
        "info",
        orders.length
          ? "État initial : " + orders.length + " position(s)/ordre(s) déjà présent(s) dans MT4."
          : "Vérification initiale terminée : aucune position ouverte signalée par MT4.",
        orders.map(function (order) {
          return {
            ticket: order.ticket,
            symbole: order.symbol,
            type: orderSide(order.type),
            lots: order.lots !== undefined ? order.lots : order.volume,
            prix: order.openPrice !== undefined ? order.openPrice : order.price
          };
        }),
        { dedupeKey: "initial-orders:" + key, throttleMs: 60000 }
      );
      orderSnapshots[key] = current;
      return;
    }

    Object.keys(current).forEach(function (ticket) {
      var order = current[ticket];
      var oldOrder = previous[ticket];
      var details = {
        ticket: order.ticket,
        symbole: order.symbol,
        type: orderSide(order.type),
        lots: order.lots !== undefined ? order.lots : order.volume,
        prixOuverture: order.openPrice !== undefined ? order.openPrice : order.price,
        stopLoss: order.stopLoss,
        takeProfit: order.takeProfit
      };

      if (!oldOrder) {
        add("Ordres", "success", (isPendingOrder(order) ? "Ordre en attente détecté dans MT4 : " : "Position détectée dans MT4 : ") + orderDescription(order) + ".", details);
        return;
      }

      var wasPendingOrder = isPendingOrder(oldOrder);
      var nowPendingOrder = isPendingOrder(order);
      if (wasPendingOrder && !nowPendingOrder) {
        add("Ordres", "success", "Ordre en attente déclenché : MT4 signale maintenant " + orderDescription(order) + ".", {
          avant: { type: orderSide(oldOrder.type), lots: oldOrder.lots, prix: oldOrder.openPrice },
          après: details
        });
        return;
      }

      var changed = !sameValue(oldOrder.type, order.type) ||
        !sameValue(oldOrder.lots, order.lots) ||
        !sameValue(oldOrder.openPrice, order.openPrice) ||
        !sameValue(oldOrder.stopLoss, order.stopLoss) ||
        !sameValue(oldOrder.takeProfit, order.takeProfit);
      if (changed) {
        add("Ordres", "info", "Paramètres de l’ordre ou de la position modifiés dans MT4 : ticket " + ticket + ".", {
          avant: {
            type: orderSide(oldOrder.type),
            lots: oldOrder.lots,
            prixOuverture: oldOrder.openPrice,
            stopLoss: oldOrder.stopLoss,
            takeProfit: oldOrder.takeProfit
          },
          après: details
        });
      }
    });
    Object.keys(previous).forEach(function (ticket) {
      if (!current[ticket]) {
        var oldOrder = previous[ticket];
        add("Vérification", "warning", "Le ticket " + ticket + " n’apparaît plus dans le relevé MT4 ; vérifie son état dans MT4.", {
          symbole: oldOrder.symbol,
          type: orderSide(oldOrder.type),
          lots: oldOrder.lots !== undefined ? oldOrder.lots : oldOrder.volume
        });
      }
    });
    orderSnapshots[key] = current;
  }

  function request(terminal, login, action, args) {
    args = args || [];
    var accountKey = String(terminal || "MT4") + ":" + String(login || "");

    if (action === "send") {
      var symbol = args[0] || "symbole inconnu";
      var side = orderSide(args[1]);
      var lots = args[2];
      var price = Number(args[3]) > 0 ? formattedNumber(args[3], 8) : "marché";
      var slippage = args[4] === undefined ? "défaut" : text(args[4]);
      var stopLoss = Number(args[5]) > 0 ? formattedNumber(args[5], 8) : "aucun";
      var takeProfit = Number(args[6]) > 0 ? formattedNumber(args[6], 8) : "aucun";
      add("Ordres", "info", "Ordre " + side + " " + symbol + " (" + formattedNumber(lots, 4) +
        " lot(s)) mis en file pour le compte " + login + " ; attente de la réponse MT4.", {
          terminal: terminal,
          compte: login,
          symbole: symbol,
          type: side,
          volume: lots,
          prix: price,
          slippagePoints: slippage,
          stopLoss: stopLoss,
          takeProfit: takeProfit,
          commentaire: args[7] || ""
        });
      return;
    }

    if (action === "rates") {
      var rateSymbol = args[0] || "tous les symboles";
      add("Analyse", "info", "Demande de données de marché pour " + rateSymbol + " afin d’actualiser le graphique ou l’analyse.", {
        terminal: terminal,
        compte: login,
        symbole: rateSymbol,
        paramètres: args.slice(1, 6)
      }, { dedupeKey: "rates:" + accountKey + ":" + rateSymbol, throttleMs: 15000 });
      return;
    }

    if (action === "orders") {
      add("Vérification", "info", "Demande à MT4 de vérifier les positions et ordres ouverts.", {
        terminal: terminal,
        compte: login
      }, { dedupeKey: "orders:" + accountKey, throttleMs: 10000 });
      return;
    }

    if (action === "close" || action === "modify" || action === "delete") {
      add("Ordres", "warning", "Demande " + action + " transmise à MT4 pour le ticket " + (args[0] || "inconnu") + ".", {
        terminal: terminal,
        compte: login,
        action: action,
        paramètres: args
      });
      return;
    }

    add("Système", "info", "Action " + text(action) + " mise en file pour MT4.", {
      terminal: terminal,
      compte: login,
      paramètres: args
    });
  }

  function response(terminal, login, action, payload, requestArgs) {
    payload = payload || {};
    requestArgs = requestArgs || [];
    var result = payload.result;
    var details = {
      terminal: terminal,
      compte: login,
      action: action,
      résultat: result
    };
    if (payload.error) {
      details.erreur = payload.error;
      add("Erreur", "error", "MT4 a refusé l’action " + action + " : " + text(payload.error), details);
      return;
    }

    if (action === "send") {
      var ticket = Number(result);
      if (isFinite(ticket) && ticket > 0) {
        add("Ordres", "success", "MT4 a confirmé l’ordre. Ticket " + ticket + ".", details);
      } else if (ticket === -1) {
        add("Erreur", "error", "MT4 a renvoyé -1 sans détail ; consulte le journal Experts/Journal de MT4 avant tout nouvel essai.", details);
      } else {
        add("Ordres", "warning", "Réponse MT4 inattendue à la demande d’ordre : " + text(result), details);
      }
      return;
    }

    if (action === "rates") {
      var symbol = requestArgs[0] || (payload.input || "").split("~")[0] || "données demandées";
      add("Analyse", "success", "Réponse de données reçue de MT4 pour " + symbol + ".", details, {
        dedupeKey: "rates-response:" + String(terminal) + ":" + String(login) + ":" + symbol,
        throttleMs: 15000
      });
      return;
    }

    if (action === "orders") {
      var count = Object.prototype.toString.call(result) === "[object Array]" ? result.length : 0;
      add("Vérification", "success", "MT4 a répondu au contrôle des ordres : " + count + " position(s)/ordre(s) retourné(s).", details, {
        dedupeKey: "orders-response:" + String(terminal) + ":" + String(login),
        throttleMs: 10000
      });
      return;
    }

    var ok = result === true || result === "true" || (typeof result === "number" && result > 0);
    add("Ordres", ok ? "success" : "warning", "Réponse MT4 à " + action + " : " + text(result), details);
  }

  function alert(message) {
    var value = text(message);
    var lower = value.toLowerCase();
    var isError = /erreur|refus|refusé|échec|impossible|aucune réponse|invalid|failed|sans détail/.test(lower);
    var isOrder = /ordre|position|ticket/.test(lower);
    var category = isError ? "Erreur" : (isOrder ? "Ordres" : "Système");
    var level = isError ? "warning" : "info";
    add(category, level, value, null, {
      dedupeKey: "alert:" + value,
      throttleMs: 2000
    });
  }

  function serverListening(port) {
    if (statusNode) {
      statusNode.textContent = "Serveur local actif · port " + port + " · en attente de MT4";
      statusNode.className = "dmt-journal-status";
    }
    add("Système", "success", "Serveur local de liaison MT4 en écoute sur le port " + port + ".", {
      port: port,
      adresse: "0.0.0.0"
    }, { dedupeKey: "server-listening:" + port, throttleMs: 60000 });
  }

  function renderEntries() {
    if (!listNode) return;
    var stickToBottom = listNode.scrollHeight - listNode.scrollTop - listNode.clientHeight < 50;
    while (listNode.firstChild) listNode.removeChild(listNode.firstChild);

    var query = searchText.toLowerCase();
    var visible = entries.filter(function (entry) {
      if (activeFilter !== "all" && entry.category !== activeFilter) return false;
      if (!query) return true;
      return (entry.category + " " + entry.message + " " + entry.details).toLowerCase().indexOf(query) !== -1;
    });

    if (!visible.length) {
      var empty = document.createElement("div");
      empty.className = "dmt-journal-empty";
      empty.textContent = entries.length ? "Aucun événement ne correspond à ce filtre." : "En attente d’événements de DMTrader et MT4…";
      listNode.appendChild(empty);
      return;
    }

    visible.forEach(function (entry) {
      var row = document.createElement("article");
      row.className = "dmt-journal-entry level-" + entry.level;

      var meta = document.createElement("div");
      meta.className = "dmt-journal-meta";
      var timeNode = document.createElement("time");
      timeNode.dateTime = new Date(entry.time).toISOString();
      timeNode.textContent = formattedTime(entry.time);
      meta.appendChild(timeNode);

      var category = document.createElement("span");
      category.className = "dmt-journal-category category-" + categoryClass(entry.category);
      category.textContent = entry.category;
      meta.appendChild(category);
      row.appendChild(meta);

      var message = document.createElement("div");
      message.className = "dmt-journal-message";
      message.textContent = entry.message;
      row.appendChild(message);

      if (entry.details) {
        var details = document.createElement("details");
        details.className = "dmt-journal-details";
        var summary = document.createElement("summary");
        summary.textContent = "Afficher les détails";
        var pre = document.createElement("pre");
        pre.textContent = entry.details;
        details.appendChild(summary);
        details.appendChild(pre);
        row.appendChild(details);
      }
      listNode.appendChild(row);
    });

    if (stickToBottom) listNode.scrollTop = listNode.scrollHeight;
  }

  function categoryClass(category) {
    if (category === "Analyse") return "analysis";
    if (category === "Vérification") return "check";
    if (category === "Ordres") return "orders";
    if (category === "Connexion") return "connection";
    if (category === "Erreur") return "error";
    return "system";
  }

  function setFilter(filter, buttons) {
    activeFilter = filter;
    Array.prototype.forEach.call(buttons, function (button) {
      var selected = button.getAttribute("data-filter") === filter;
      button.className = selected ? "dmt-journal-filter is-selected" : "dmt-journal-filter";
      button.setAttribute("aria-pressed", selected ? "true" : "false");
    });
    renderEntries();
  }

  function exportEntries() {
    try {
      var blob = new Blob([JSON.stringify(entries, null, 2)], { type: "application/json;charset=utf-8" });
      var urlApi = window.URL || window.webkitURL;
      var url = urlApi.createObjectURL(blob);
      var anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = "journal-dmtrader-" + new Date().toISOString().slice(0, 10) + ".json";
      document.body.appendChild(anchor);
      anchor.click();
      document.body.removeChild(anchor);
      window.setTimeout(function () { urlApi.revokeObjectURL(url); }, 1000);
    } catch (error) {
      add("Erreur", "error", "Impossible d’exporter le journal : " + text(error));
    }
  }

  function closeDialog() {
    if (!dialog || !isOpen) return;
    dialog.hidden = true;
    isOpen = false;
    toggleButton.focus();
  }

  function openDialog() {
    if (!dialog) return;
    isOpen = true;
    unread = 0;
    updateBadge();
    dialog.hidden = false;
    renderEntries();
    window.setTimeout(function () {
      if (listNode) listNode.scrollTop = listNode.scrollHeight;
      if (panelNode) panelNode.focus();
    }, 0);
  }

  function clearEntries() {
    if (!window.confirm("Effacer l’historique local du journal ?")) return;
    entries = [];
    unread = 0;
    saveEntries();
    updateBadge();
    renderEntries();
    add("Système", "info", "Historique du journal effacé par l’utilisateur.");
  }

  function initUi() {
    var root = document.createElement("div");
    root.id = "dmt-journal-root";
    root.innerHTML =
      '<button id="dmt-journal-toggle" type="button" title="Journal d’activité" aria-label="Ouvrir le journal d’activité">' +
        '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 3.75h9.5L19 7.2v13.05H6z"></path><path d="M15.5 3.75V7.5H19M9 11h7M9 14.5h7M9 18h5"></path></svg>' +
        '<span>Journal</span><span id="dmt-journal-badge" hidden></span>' +
      '</button>' +
      '<div id="dmt-journal-dialog" class="dmt-journal-overlay" role="presentation" hidden>' +
        '<section class="dmt-journal-panel" role="dialog" aria-modal="true" aria-labelledby="dmt-journal-title" tabindex="-1">' +
          '<header class="dmt-journal-header">' +
            '<div><div class="dmt-journal-kicker">ACTIVITÉ LOCALE</div><h2 id="dmt-journal-title">Journal d’activité</h2><div id="dmt-journal-status" class="dmt-journal-status">En attente de DMTrader</div></div>' +
            '<button id="dmt-journal-close" class="dmt-journal-icon-button" type="button" aria-label="Fermer le journal" title="Fermer">×</button>' +
          '</header>' +
          '<div class="dmt-journal-toolbar">' +
            '<div class="dmt-journal-filters" role="group" aria-label="Filtrer les événements">' +
              '<button class="dmt-journal-filter is-selected" data-filter="all" aria-pressed="true">Tout</button>' +
              '<button class="dmt-journal-filter" data-filter="Analyse" aria-pressed="false">Analyse</button>' +
              '<button class="dmt-journal-filter" data-filter="Vérification" aria-pressed="false">Vérifications</button>' +
              '<button class="dmt-journal-filter" data-filter="Ordres" aria-pressed="false">Ordres</button>' +
              '<button class="dmt-journal-filter" data-filter="Connexion" aria-pressed="false">Connexion</button>' +
              '<button class="dmt-journal-filter" data-filter="Erreur" aria-pressed="false">Erreurs</button>' +
              '<button class="dmt-journal-filter" data-filter="Système" aria-pressed="false">Système</button>' +
            '</div>' +
            '<input id="dmt-journal-search" class="dmt-journal-search" type="search" placeholder="Rechercher dans le journal…" aria-label="Rechercher dans le journal">' +
            '<div class="dmt-journal-actions"><button id="dmt-journal-export" type="button">Exporter</button><button id="dmt-journal-clear" type="button">Effacer</button></div>' +
          '</div>' +
          '<div class="dmt-journal-note">Le journal décrit les faits observables (données de marché, contrôles, simulations, ordres, réponses et alertes). Il n’invente pas de signaux et ne révèle pas de raisonnement interne caché.</div>' +
          '<div id="dmt-journal-list" class="dmt-journal-list" aria-live="polite"></div>' +
          '<footer class="dmt-journal-footer"><span>Historique enregistré localement sur cet ordinateur.</span><span id="dmt-journal-count">0 événement</span></footer>' +
        '</section>' +
      '</div>';
    document.body.appendChild(root);

    toggleButton = document.getElementById("dmt-journal-toggle");
    badge = document.getElementById("dmt-journal-badge");
    dialog = document.getElementById("dmt-journal-dialog");
    panelNode = root.querySelector(".dmt-journal-panel");
    listNode = document.getElementById("dmt-journal-list");
    statusNode = document.getElementById("dmt-journal-status");
    searchNode = document.getElementById("dmt-journal-search");
    var filters = root.querySelectorAll(".dmt-journal-filter");
    var closeButton = document.getElementById("dmt-journal-close");
    var clearButton = document.getElementById("dmt-journal-clear");
    var exportButton = document.getElementById("dmt-journal-export");
    var countNode = document.getElementById("dmt-journal-count");

    toggleButton.addEventListener("click", openDialog);
    closeButton.addEventListener("click", closeDialog);
    dialog.addEventListener("click", function (event) {
      if (event.target === dialog) closeDialog();
    });
    Array.prototype.forEach.call(filters, function (button) {
      button.addEventListener("click", function () {
        setFilter(button.getAttribute("data-filter"), filters);
      });
    });
    searchNode.addEventListener("input", function () {
      searchText = searchNode.value || "";
      renderEntries();
    });
    clearButton.addEventListener("click", clearEntries);
    exportButton.addEventListener("click", exportEntries);
    document.addEventListener("keydown", function (event) {
      if (event.key === "Escape" && isOpen) closeDialog();
    });

    // Keep the visible count current without exposing the internal entries array.
    window.setInterval(function () {
      var now = Date.now();
      if (countNode) countNode.textContent = entries.length + (entries.length === 1 ? " événement" : " événements");
      Object.keys(lastTickAt).forEach(function (key) {
        var elapsed = now - lastTickAt[key];
        if (elapsed >= 15000 && staleLinks[key] !== true) {
          staleLinks[key] = true;
          if (statusNode) {
            statusNode.textContent = "Aucun signal MT4 depuis " + Math.floor(elapsed / 1000) + " s";
            statusNode.className = "dmt-journal-status is-stale";
          }
          add("Connexion", "warning", "Aucun signal MT4 reçu depuis au moins 15 secondes.", {
            compteTerminal: key,
            dernièresDonnées: formattedTime(lastTickAt[key])
          }, { dedupeKey: "stale-link:" + key, throttleMs: 60000 });
        }
      });
    }, 1000);
    updateBadge();
    renderEntries();
  }

  function installErrorCapture() {
    if (window.console && typeof window.console.error === "function" && !window.console.__dmtJournalWrapped) {
      var originalError = window.console.error;
      window.console.error = function () {
        var parts = [];
        for (var i = 0; i < arguments.length; i++) parts.push(text(arguments[i]));
        var message = parts.join(" ");
        try {
          add("Erreur", "error", "Erreur application : " + message, null, {
            dedupeKey: "console-error:" + message,
            throttleMs: 3000
          });
        } catch (journalError) {
          // A logging failure must never suppress the application error itself.
        }
        return originalError.apply(window.console, arguments);
      };
      window.console.__dmtJournalWrapped = true;
    }

    window.addEventListener("error", function (event) {
      var message = event && event.message ? event.message : "Erreur JavaScript non détaillée";
      add("Erreur", "error", message, {
        fichier: event && event.filename,
        ligne: event && event.lineno,
        colonne: event && event.colno
      }, { dedupeKey: "window-error:" + message, throttleMs: 3000 });
    });
    window.addEventListener("unhandledrejection", function (event) {
      add("Erreur", "error", "Erreur asynchrone : " + text(event && event.reason), null, {
        dedupeKey: "promise-error:" + text(event && event.reason),
        throttleMs: 3000
      });
    });
  }

  initUi();

  window.DMTraderJournal = {
    log: add,
    alert: alert,
    request: request,
    response: response,
    tick: tick,
    serverListening: serverListening
  };

  installErrorCapture();
  add("Système", "info", "Journal d’activité activé. Les événements sont conservés localement (500 maximum).", {
    stockage: "localStorage",
    limiteEvenements: MAX_ENTRIES
  });
  add("Analyse", "info", "Limite de cette version : aucune ouverture automatique issue des stratégies n’a été trouvée dans le code ; les simulations restent historiques et ne transmettent pas d’ordre. Le journal ne créera pas de signaux absents.");
})();
