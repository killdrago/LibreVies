(function (root, factory) {
  var api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  if (root) root.DMTraderStrategyRanking = api;
})(typeof window !== "undefined" ? window : globalThis, function () {
  "use strict";

  function numeric(value) {
    if (value === null || value === undefined || value === "") return null;
    var parsed = Number(value);
    return isFinite(parsed) ? parsed : null;
  }

  function side(type) {
    if (type === 0 || type === 2 || type === 4) return "buy";
    if (type === 1 || type === 3 || type === 5) return "sell";
    return null;
  }

  function entryPrice(input, symbol, orderSide) {
    var requested = numeric(input[6]);
    if (requested !== null && requested > 0) return requested;
    return numeric(orderSide === "buy" ? symbol.ask : symbol.bid);
  }

  function inspectAction(action, symbol) {
    if (!action || action.action !== "send" || !Array.isArray(action.input)) return null;
    var input = action.input;
    var orderSide = side(numeric(input[1]));
    var lots = numeric(input[2]);
    var point = numeric(symbol && symbol.point);
    var entry = entryPrice(input, symbol || {}, orderSide);
    if (!orderSide || lots === null || lots <= 0 || point === null || point <= 0 || entry === null) return null;

    var metrics = { action: action, tpGain: null, slDistance: null };
    var takeProfit = numeric(input[4]);
    if (takeProfit !== null && takeProfit > 0) {
      var favorableTp = orderSide === "buy" ? takeProfit > entry : takeProfit < entry;
      if (favorableTp) {
        var tpDistance = Math.abs(takeProfit - entry);
        var tickValue = numeric(symbol.tradeTickValue);
        metrics.tpGain = tpDistance / point * (tickValue !== null && tickValue > 0 ? tickValue : 1) * lots;
        metrics.gainMonetary = tickValue !== null && tickValue > 0;
      }
    }

    var stopLoss = numeric(input[3]);
    if (stopLoss !== null && stopLoss > 0) {
      var protectiveSl = orderSide === "buy" ? stopLoss < entry : stopLoss > entry;
      if (protectiveSl) metrics.slDistance = Math.abs(entry - stopLoss) / point;
    }
    return metrics;
  }

  function choose(candidates, symbol) {
    var withTakeProfit = [];
    var withStopLoss = [];
    (candidates || []).forEach(function (candidate, order) {
      var metrics = [];
      (candidate.actions || []).forEach(function (action) {
        var result = inspectAction(action, symbol || {});
        if (result) metrics.push(result);
      });

      var tpMetrics = metrics.filter(function (result) { return result.tpGain !== null; });
      if (tpMetrics.length) {
        var bestTp = tpMetrics.reduce(function (best, result) {
          return !best || result.tpGain > best.tpGain ? result : best;
        }, null);
        withTakeProfit.push({
          candidate: candidate,
          action: bestTp.action,
          score: bestTp.tpGain,
          gainMonetary: bestTp.gainMonetary,
          order: order,
          reason: "tp",
          stopLoss: numeric(bestTp.action.input[3]),
          takeProfit: numeric(bestTp.action.input[4])
        });
        return;
      }

      var slMetrics = metrics.filter(function (result) { return result.slDistance !== null; });
      if (slMetrics.length) {
        var bestSl = slMetrics.reduce(function (best, result) {
          return !best || result.slDistance < best.slDistance ? result : best;
        }, null);
        withStopLoss.push({
          candidate: candidate,
          action: bestSl.action,
          score: bestSl.slDistance,
          order: order,
          reason: "sl",
          stopLoss: numeric(bestSl.action.input[3]),
          takeProfit: numeric(bestSl.action.input[4])
        });
      }
    });

    var pool = withTakeProfit.length ? withTakeProfit : withStopLoss;
    if (!pool.length) return null;
    var winner = pool[0];
    for (var i = 1; i < pool.length; i++) {
      if (winner.reason === "tp" ? pool[i].score > winner.score : pool[i].score < winner.score) winner = pool[i];
    }
    return {
      file: winner.candidate.file,
      action: winner.action,
      reason: winner.reason,
      score: winner.score,
      gainMonetary: winner.gainMonetary === true,
      stopLoss: winner.stopLoss,
      takeProfit: winner.takeProfit,
      candidateOrder: winner.order
    };
  }

  return { choose: choose };
});
