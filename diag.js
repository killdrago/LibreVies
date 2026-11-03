/* ======================================================================
 * diag.js — Journal de diagnostic DMTrader (NW.js / Node v6.2.2)
 * -----------------------------------------------------------------------
 * À charger AVANT journal.js et index.js (voir index.html). Instrumente
 * l'app à l'exécution et écrit un journal détaillé :
 *
 *   - console de l'app (DevTools, F12) avec préfixe [DIAG]
 *   - fichier diag.log à la racine de l'app (le « journal »)
 *
 * S'ajoute sans conflit au journal d'activité (journal.js) : celui-ci
 * décrit les faits trading, diag.js mesure les performances.
 *
 * Ce qu'il mesure :
 *   - chronométrage du démarrage : parse du bundle index.js (2.8 MB),
 *     rendu React, dialogue de connexion
 *   - TOUTES les requêtes réseau (XHR du navigateur + Node http/https) :
 *     URL, méthode, statut, octets, durée, étiquette [API/UPDATE]
 *     pour le serveur 92.222.92.14 (API de login + manifest de mise à jour)
 *   - TOUS les timers (setInterval / setTimeout) avec leur origine
 *     (trace de la pile) et leur fréquence d'exécution
 *   - consommation mémoire (RSS/heap), lag de l'event loop, handles actifs
 *   - opérations fichiers (copies TraderConnect.ex4/ex5, dézip update)
 *   - fermetures de l'app (cycle auto-update) et erreurs JS
 *
 * Utilisation :
 *   - lancer l'app normalement, faire ce que vous faites habituellement
 *   - lire le fichier diag.log (ou la console DevTools)
 *   - dans la console DevTools : __diag.report() pour un état manuel
 *     ; __diag.mute() / __diag.unmute() pour suspendre le journal
 * ====================================================================== */

(function () {
  "use strict";

  var T0 = Date.now();

  /* ---------------- configuration ---------------- */

  var cfg = {
    slowNet: 2000,   // ms : une requête réseau plus longue est signalée SLOW
    slowLag: 80,     // ms : un lag d'event loop supérieur est signalé
    slowFs: 800      // ms : une opération fichier synchrone plus longue est signalée
  };

  /* ---------------- logger ---------------- */

  var fs = null, path = null;
  try { if (typeof require === "function") { fs = require("fs"); path = require("path"); } } catch (e) {}

  var LOG_PATH = null;
  try {
    if (fs && path && process && process.execPath) {
      LOG_PATH = path.join(path.dirname(process.execPath), "diag.log");
    }
  } catch (e) {}
  if (!LOG_PATH) {
    try {
      if (typeof nw !== "undefined" && nw.App && nw.App.dataPath && path) {
        LOG_PATH = path.join(nw.App.dataPath, "diag.log");
      }
    } catch (e) {}
  }

  var mem = [];          // dernières lignes en mémoire (pour le rotatage)
  var muted = false;
  var fileCount = 0;

  function stamp() {
    var d = new Date();
    function p(x) { return (x < 10 ? "0" + x : "" + x); }
    return p(d.getHours()) + ":" + p(d.getMinutes()) + ":" + p(d.getSeconds()) +
           "." + ("00" + d.getMilliseconds()).slice(-3);
  }

  // logger original (non remplacé) pour éviter toute récursion
  var oLog = (console.log || function () {}).bind(console);
  var oErr = (console.error || console.log || function () {}).bind(console);
  var oWarn = (console.warn || console.log || function () {}).bind(console);

  function line(msg) {
    if (muted) return;
    var s = "[" + stamp() + "] " + msg;
    mem.push(s);
    if (mem.length > 400) mem.shift();
    try { oLog("[DIAG] " + msg); } catch (e) {}
    if (fs && LOG_PATH) {
      try { fs.appendFileSync(LOG_PATH, s + "\n"); } catch (e) {}
      fileCount++;
      if (fileCount % 200 === 0) rotate();
    }
  }

  function rotate() {
    try {
      var st = fs.statSync(LOG_PATH);
      if (st.size > 1048576) {
        fs.writeFileSync(LOG_PATH, mem.slice(-500).join("\n") + "\n");
      }
    } catch (e) {}
  }

  /* ---------------- interception des logs de l'app ---------------- */

  try {
    function fmt(x) { try { return String(x); } catch (e) { return "?"; } }
    console.log = function () {
      var a = [];
      for (var i = 0; i < arguments.length; i++) a.push(fmt(arguments[i]));
      line("APP " + a.join(" ").slice(0, 300));
    };
    console.error = function () {
      var a = [];
      for (var i = 0; i < arguments.length; i++) a.push(fmt(arguments[i]));
      line("APP-ERR " + a.join(" ").slice(0, 300));
      return oErr.apply(console, arguments);
    };
    console.warn = function () {
      var a = [];
      for (var i = 0; i < arguments.length; i++) a.push(fmt(arguments[i]));
      line("APP-WARN " + a.join(" ").slice(0, 300));
      return oWarn.apply(console, arguments);
    };
  } catch (e) {}

  /* ---------------- démarrage ---------------- */

  line("=====================================================================");
  line("DEMARRAGE journal diagnostic DMTrader");
  try {
    line("INFO plateforme=" + process.platform + " node=" + process.version +
         " nw=" + ((typeof nw !== "undefined" && nw.App && nw.App.version) || "?") +
         " execPath=" + process.execPath);
  } catch (e) {}
  line("INFO journal=" + LOG_PATH);
  line("INFO dans la console DevTools : __diag.report() / __diag.mute() / __diag.unmute()");

  /* ---------------- jalons du démarrage ---------------- */

  try {
    document.addEventListener("DOMContentLoaded", function () {
      line("MILESTONE DOMContentLoaded t+ " + (Date.now() - T0) +
           " ms — bundle index.js chargé et exécuté");
    });
    window.addEventListener("load", function () {
      line("MILESTONE window.load t+ " + (Date.now() - T0) + " ms");
    });
    window.addEventListener("beforeunload", function () {
      line("MILESTONE beforeunload — fermeture de la fenêtre t+ " + (Date.now() - T0) + " ms");
    });
  } catch (e) {}

  try {
    var uiLogged = false, loginLogged = false;
    var mo = new MutationObserver(function () {
      try {
        var app = document.getElementById("app");
        if (!uiLogged && app && app.children && app.children.length) {
          uiLogged = true;
          line("MILESTONE UI React rendu dans #app t+ " + (Date.now() - T0) + " ms");
        }
        var le = document.getElementById("login-email");
        if (!loginLogged && le) {
          loginLogged = true;
          line("MILESTONE dialogue de connexion affiché t+ " + (Date.now() - T0) + " ms");
        }
      } catch (e) {}
    });
    mo.observe(document.body, { childList: true, subtree: true });
  } catch (e) {}

  /* ---------------- nw.* ---------------- */

  try {
    if (typeof nw !== "undefined") {
      if (nw.Window && nw.Window.get) {
        var origGet = nw.Window.get;
        nw.Window.get = function () {
          line("EVENT nw.Window.get() t+ " + (Date.now() - T0) + " ms");
          return origGet.apply(this, arguments);
        };
      }
      if (nw.App && nw.App.quit) {
        var origQuit = nw.App.quit;
        nw.App.quit = function () {
          line("EVENT nw.App.quit() — fermeture de l'app t+ " + (Date.now() - T0) + " ms" +
               " (cycle auto-update si une mise à jour était téléchargée)");
          return origQuit.apply(this, arguments);
        };
      }
    }
  } catch (e) {}

  /* ---------------- vérification du bundle courant ---------------- */

  try {
    if (fs && path) {
      var appDir = null;
      try {
        if (typeof nw !== "undefined" && nw.App && nw.App.getAppPath) {
          appDir = path.dirname(nw.App.getAppPath());
        }
      } catch (e) {}
      if (!appDir) appDir = path.dirname(process.execPath);
      try {
        var bundleSrc = fs.readFileSync(path.join(appDir, "index.js"), "utf8");
        var zoneOk = bundleSrc.indexOf("xLimit:r>=(t.props.levels[e]||[]).length?20:void 0") !== -1;
        var updOk = bundleSrc.indexOf("checkNewVersion(") === -1;
        line("VERIF bundle index.js: taille=" + bundleSrc.length + " octets" +
             " patchZones10Chandelles=" + (zoneOk ? "OK" : "ABSENT") +
             " checkMAJ=" + (updOk ? "RETIRÉ" : "ENCOURS"));
      } catch (e) {
        line("VERIF bundle index.js: inaccessible(" + (e && e.message ? e.message : "?") + ")");
      }
    }
  } catch (e) {}

  /* ---------------- erreurs JS ---------------- */

  try {
    window.addEventListener("error", function (e) {
      try {
        line("JS-ERROR " + (e.message || "?") + " @" + (e.filename || "?") + ":" + (e.lineno || 0));
      } catch (x) {}
    }, true);
  } catch (e) {}

  /* ---------------- réseau ---------------- */

  var netCount = 0, netBytes = 0;
  var netLog = [];

  function tagOf(url) {
    return url.indexOf("92.222.92.14") !== -1 ? " [API/UPDATE]" : "";
  }

  function logNet(kind, method, url, status, bytes, ms, err) {
    netCount++;
    netBytes += bytes || 0;
    var tag = tagOf(url);
    var slow = ms > cfg.slowNet ? " SLOW!" : "";
    var s = kind + " " + method + " " + url +
            (status ? " -> " + status : "") +
            (bytes ? " " + bytes + "B" : "") +
            (err ? " ERREUR:" + err : "") +
            " en " + ms + "ms" + slow + tag;
    netLog.push(s);
    if (netLog.length > 60) netLog.shift();
    line("NET " + s);
  }

  function fmtOptions(options) {
    if (typeof options === "string") return options;
    if (!options) return "?";
    var proto = options.protocol || "http:";
    var host = options.hostname || options.host || "?";
    var p = options.path || options.pathname || "/";
    if (options.port && options.port !== 80 && options.port !== 443) host += ":" + options.port;
    return proto + "//" + host + p;
  }

  function wrapReq(req, label, options) {
    try {
      var url = fmtOptions(options);
      var method = (typeof options === "object" && options.method) ? options.method : "GET";
      var t = Date.now();
      var done = false, bytes = 0, status = 0;
      line("NET START " + label + " " + method + " " + url + tagOf(url));
      req.on("response", function (res) {
        try {
          status = res.statusCode;
          res.on("data", function (c) { bytes += c.length; });
          res.on("end", function () { finish(); });
          res.on("close", function () { finish(); });
        } catch (e) {}
      });
      req.on("error", function (err) {
        finish(err && err.message ? err.message : "erreur");
      });
      req.on("close", function () { finish(); });
      function finish(err) {
        if (done) return;
        done = true;
        logNet(label, method, url, status, bytes, Date.now() - t, err);
      }
    } catch (e) {}
    return req;
  }

  function instrumentHttp(mod, label) {
    try {
      if (!mod) return;
      // request : on crée le req, on le marque (marqueture anti-double journal)
      var origRequest = mod.request;
      if (typeof origRequest === "function") {
        mod.request = function (options, cb) {
          var req = origRequest.apply(this, arguments);
          try { req.__diagFromRequest = true; } catch (e) {}
          return wrapReq(req, label, options);
        };
      }
      // get : appelle request (déjà patché) pour https, ou request interne pour http
      var origGet = mod.get;
      if (typeof origGet === "function") {
        mod.get = function (options, cb) {
          var req = origGet.apply(this, arguments);
          if (req.__diagFromRequest) return req; // déjà journalisé via request
          return wrapReq(req, label, options);
        };
      }
    } catch (e) {}
  }

  try {
    if (typeof require === "function") {
      instrumentHttp(require("http"), "HTTP");
      instrumentHttp(require("https"), "HTTPS");
    }
  } catch (e) {}

  try {
    var XHR = window.XMLHttpRequest;
    if (XHR && XHR.prototype) {
      var oOpen = XHR.prototype.open, oSend = XHR.prototype.send;
      XHR.prototype.open = function (method, url) {
        this.__diag = { method: method, url: url, t: Date.now() };
        return oOpen.apply(this, arguments);
      };
      XHR.prototype.send = function () {
        var self = this, d = this.__diag;
        if (d) {
          d.t = Date.now();
          line("NET START XHR " + d.method + " " + d.url + tagOf(d.url));
          try {
            self.addEventListener("load", function () {
              var len = 0;
              try { len = self.responseText ? self.responseText.length : 0; } catch (e) {}
              logNet("XHR", d.method, d.url, self.status, len, Date.now() - d.t);
            });
            self.addEventListener("error", function () {
              logNet("XHR", d.method, d.url, 0, 0, Date.now() - d.t, "erreur XHR");
            });
          } catch (e) {}
        }
        return oSend.apply(this, arguments);
      };
    }
  } catch (e) {}

  /* ---------------- opérations fichiers ---------------- */

  try {
    if (fs) {
      ["readdirSync", "readFileSync", "writeFileSync", "statSync"].forEach(function (name) {
        var orig = fs[name];
        if (typeof orig !== "function") return;
        fs[name] = function () {
          var t = Date.now();
          var r;
          try { r = orig.apply(this, arguments); }
          catch (e) {
            try { line("FS " + name + " " + arguments[0] + " ERREUR:" + e.message); } catch (x) {}
            throw e;
          }
          try {
            var p = arguments[0];
            if (p === LOG_PATH) return r; // ne pas journaliser l'écriture du journal
            if (name === "statSync") return r;
            var ms = Date.now() - t;
            var slow = ms > cfg.slowFs ? " SLOW!" : "";
            if (name === "readdirSync") {
              line("FS " + name + " " + p + " -> " + (r && r.length) + " éléments en " + ms + "ms" + slow);
            } else {
              var size = "";
              if (typeof r === "string") size = r.length + " chars";
              else if (r && typeof r.length === "number") size = r.length + " octets";
              line("FS " + name + " " + p + (size ? " -> " + size : "") + " en " + ms + "ms" + slow);
            }
          } catch (e) {}
          return r;
        };
      });
      ["createReadStream", "createWriteStream"].forEach(function (name) {
        var orig = fs[name];
        if (typeof orig !== "function") return;
        fs[name] = function () {
          try {
            var p = arguments[0];
            if (p !== LOG_PATH) line("FS " + name + " " + p);
          } catch (e) {}
          return orig.apply(this, arguments);
        };
      });
    }
  } catch (e) {}

  /* ---------------- timers ---------------- */

  var intervals = {}; // id -> {period, stack, count, t0, gone}
  var timeouts = {};  // id -> {period, stack, t0, gone}
  var totalExecs = 0;

  function src() {
    try {
      var st = (new Error().stack || "").split("\n");
      return st.slice(2, 4).join(" <- ").replace(/\s+/g, " ").slice(0, 140);
    } catch (e) { return "?"; }
  }

  try {
    var oSI = window.setInterval, oTO = window.setTimeout,
        oCI = window.clearInterval, oCTO = window.clearTimeout;

    window.setInterval = function (fn, period) {
      var holder = { period: period, stack: src(), count: 0, t0: Date.now(), gone: false };
      var id;
      if (typeof fn === "function") {
        var w = function () {
          var t0 = Date.now();
          holder.count++; totalExecs++;
          try { return fn.apply(this, arguments); } finally {
            var dt = Date.now() - t0;
            holder.totalMs = (holder.totalMs || 0) + dt;
            if (dt > 200) line("SLOW callback: " + dt + "ms @ " + holder.stack);
          }
        };
        id = oSI.call(this, w, period);
      } else {
        id = oSI.call(this, fn, period);
      }
      holder.id = id;
      intervals[id] = holder;
      return id;
    };

    window.setTimeout = function (fn, period) {
      var holder = { period: period, stack: src(), t0: Date.now(), gone: false };
      var id;
      if (typeof fn === "function") {
        var w = function () { holder.gone = true; return fn.apply(this, arguments); };
        id = oTO.call(this, w, period);
      } else {
        id = oTO.call(this, fn, period);
      }
      holder.id = id;
      timeouts[id] = holder;
      return id;
    };

    window.clearInterval = function (id) {
      try { if (intervals[id]) intervals[id].gone = true; } catch (e) {}
      return oCI.apply(this, arguments);
    };
    window.clearTimeout = function (id) {
      try { if (timeouts[id]) timeouts[id].gone = true; } catch (e) {}
      return oCTO.apply(this, arguments);
    };
  } catch (e) {}

  /* ---------------- lag de l'event loop ---------------- */

  var lagLast = -1, lagSamples = [], lastLagAlert = 0;
  try {
    // NW.js (page contexte) n'expose pas toujours setImmediate : on utilise
    // setTimeout(0) qui est toujours disponible. hrtime indisponible ⇒
    // repli sur Date.now(). On mesure le temps entre l'armement du timer
    // et son exécution : c'est le temps perdu par l'event loop.
    var hasHrtime = typeof process !== "undefined" && typeof process.hrtime === "function";
    (function loop() {
      var start = hasHrtime ? process.hrtime() : Date.now();
      var tick = function () {
        try {
          if (hasHrtime) {
            var d = process.hrtime(start);
            lagLast = Math.round(d[0] * 1000 + d[1] / 1e6);
          } else {
            lagLast = Date.now() - start;
          }
          if (lagSamples.length < 900) lagSamples.push(lagLast);
          if (lagLast > cfg.slowLag && Date.now() - lastLagAlert > 10000) {
            lastLagAlert = Date.now();
            line("ALERTE lag event loop élevé: " + lagLast + " ms — l'UI est bloquée");
          }
        } catch (e) {}
        loop();
      };
      if (typeof setImmediate === "function") setImmediate(tick);
      else setTimeout(tick, 0);
    })();
  } catch (e) {}

  /* ---------------- sondes périodiques ---------------- */

  try {
    setInterval(function () {
      try {
        var m = process.memoryUsage();
        var avg = lagSamples.length
          ? Math.round(lagSamples.reduce(function (a, b) { return a + b; }, 0) / lagSamples.length)
          : -1;
        line("RES t+" + Math.round((Date.now() - T0) / 1000) + "s mem RSS=" +
             Math.round(m.rss / 1048576) + "MB heap=" +
             Math.round(m.heapUsed / 1048576) + "MB lagEL=" + lagLast +
             "ms lagMoyen=" + avg + "ms timersActifs=" +
             Object.keys(intervals).length + " execTimers=" + totalExecs);
      } catch (e) {}
    }, 5000);
  } catch (e) {}

  try {
    setInterval(function () {
      var act = 0, parts = [];
      for (var id in intervals) {
        var it = intervals[id];
        if (!it || it.gone) continue;
        act++;
        var per = (it.period === undefined ? "?" : it.period) + "ms";
        var s = Math.round((Date.now() - it.t0) / 1000);
        var rate = s > 0 ? (it.count / s).toFixed(1) : "?";
        parts.push("id" + id + "[" + per + " exec=" + it.count + " ~" + rate + "/s] " + it.stack);
      }
      if (act > 0) {
        line("TIMERS actifs(" + act + "):");
        for (var k = 0; k < parts.length; k++) line("   " + parts[k]);
      }
    }, 15000);
  } catch (e) {}

  try {
    setInterval(function () {
      try {
        var h = process._getActiveHandles();
        var names = {};
        for (var i = 0; i < h.length; i++) {
          var x = h[i], n = "?";
          try { n = (x && x.constructor && x.constructor.name) || String(x).slice(0, 30); } catch (e) {}
          names[n] = (names[n] || 0) + 1;
        }
        line("HANDLES " + JSON.stringify(names));
      } catch (e) {}
    }, 30000);
  } catch (e) {}

  try {
    setInterval(function () {
      line("SUMMARY t+" + Math.round((Date.now() - T0) / 1000) + "s reqs=" + netCount +
           " octetsRecus=" + netBytes + " execTimers=" + totalExecs);
    }, 60000);
  } catch (e) {}

  /* ---------------- API manuelle ---------------- */

  try {
    window.__diag = {
      t0: T0,
      cfg: cfg,
      mark: function (m) { line("MILESTONE " + m + " t+ " + (Date.now() - T0) + " ms"); },
      mute: function () { muted = true; line("INFO journal muté"); },
      unmute: function () { muted = false; line("INFO journal démuté"); },
      net: function () { return netLog.slice(); },
      timers: function () {
        var out = [];
        for (var id in intervals) if (intervals[id] && !intervals[id].gone) out.push(intervals[id]);
        return out;
      },
      report: function () {
        var out = [];
        out.push("=== RAPPORT DIAGNOSTIC DMTrader t+" + Math.round((Date.now() - T0) / 1000) + "s ===");
        try {
          var m = process.memoryUsage();
          out.push("mémoire RSS=" + Math.round(m.rss / 1048576) + "MB heap=" +
                   Math.round(m.heapUsed / 1048576) + "MB");
        } catch (e) {}
        out.push("lag event loop: dernier=" + lagLast + "ms");
        out.push("requetes reseau: " + netCount + " (" + netBytes + " octets)");
        out.push("-- dernieres requetes --");
        for (var i = netLog.length - 1; i >= Math.max(0, netLog.length - 15); i--) {
          out.push("   " + netLog[i]);
        }
        out.push("-- timers actifs --");
        for (var id in intervals) {
          var it = intervals[id];
          if (it && !it.gone) {
            out.push("id" + id + " per=" + it.period + "ms exec=" + it.count + " " + it.stack);
          }
        }
        var s = out.join("\n");
        try { oLog(s); } catch (e) {}
        return s;
      }
    };
  } catch (e) {}

  line("INFO instrumentation prete — chargement de journal.js / index.js...");

// ===== sondes interactions : clics + durée du dispatch + réponse UI =====
try {
  var raf = (window.requestAnimationFrame || function (fn) { return window.setTimeout(fn, 16); });
  function interactionLabel(evt) {
    var el = evt && evt.target;
    if (!el) return "?";
    var parts = [];
    if (el.tagName) parts.push(String(el.tagName).toLowerCase());
    if (el.value) parts.push(String(el.value));
    if (el.className && typeof el.className === "string") parts.push(el.className.slice(0, 60));
    return parts.join(" | ");
  }
  function instrumentInteraction(evt, type) {
    var t0 = Date.now();
    line("INTERACTION " + type + ": " + interactionLabel(evt));
    function onBubble() {
      line("DISPATCH " + type + ": " + (Date.now() - t0) + "ms");
      try { window.removeEventListener(type, onBubble); } catch (e) {}
    }
    try { window.addEventListener(type, onBubble, false); } catch (e) {}
    try {
      raf(function () {
        line("RESPONSE " + type + ": " + (Date.now() - t0) + "ms");
      });
    } catch (e) {}
  }
  document.addEventListener("mousedown", function (evt) { instrumentInteraction(evt, "mousedown"); }, true);
  document.addEventListener("click", function (evt) { instrumentInteraction(evt, "click"); }, true);
  line("sondes interactions pretes (clics + dispatch + réponse UI)");
} catch (e) {}
})();
