#property strict
#property copyright "Gregory Lechanoine"
#property version   "1.01"
#property description "DMTrader HTTP bridge for MetaTrader 4"

// Clean-room replacement for the unavailable TraderConnect.mq4 source.
// It implements the DMTrader local HTTP/JSON protocol; it is not a
// decompilation or an exact binary conversion of TraderConnect.ex4.

input string ServerURL              = "http://127.0.0.1:2122/";
input string TerminalLabel          = "";
input int    PollIntervalSeconds    = 1;
input int    RequestTimeoutMs       = 3000;
input int    DefaultSlippagePoints  = 5;
input int    HistoryOrdersLimit     = 250;
input bool   AllowTradeCommands     = false; // Enable only after demo-account testing.

string   g_terminalName = "";
bool     g_busy = false;
datetime g_lastHttpLog = 0;

int OnInit()
{
   g_terminalName = TerminalLabel;
   if(StringLen(g_terminalName) == 0)
   {
      g_terminalName = TerminalInfoString(TERMINAL_NAME);
      if(StringLen(g_terminalName) == 0)
         g_terminalName = "MetaTrader 4";
      if(StringLen(AccountServer()) > 0)
         g_terminalName += " - " + AccountServer();
   }

   int interval = PollIntervalSeconds;
   if(interval < 1)
      interval = 1;
   if(!EventSetTimer(interval))
   {
      Print("[TraderConnect] Could not start the polling timer. MT4 error ", GetLastError());
      return(INIT_FAILED);
   }

   Print("[TraderConnect] DMTrader URL: ", ServerURL,
         "; terminal id: ", g_terminalName,
         "; trade commands: ", (AllowTradeCommands ? "ENABLED" : "DISABLED"));
   Print("[TraderConnect] Add http://127.0.0.1:2122 under MT4 Tools > Options > Expert Advisors > Allow WebRequest.");
   if(AccountNumber() <= 0)
      Print("[TraderConnect] MT4 is not logged in; DMTrader requires a positive account login number.");
   return(INIT_SUCCEEDED);
}

void OnDeinit(const int reason)
{
   EventKillTimer();
}

void OnTimer()
{
   if(g_busy)
      return;
   g_busy = true;
   PollDMTrader();
   g_busy = false;
}

void PollDMTrader()
{
   string response = "";
   string body = MakeEnvelope("tick", BuildTickResult(), "");
   if(!HttpPost(body, response))
      return;
   DispatchCommands(response);
}

void DispatchCommands(string response)
{
   // An acknowledgement may return the next queued action. Process a bounded
   // number at once so the EA cannot get stuck in a server response loop.
   for(int step = 0; step < 12; step++)
   {
      string action = "";
      string actionInput = "";
      if(!JsonStringField(response, "action", action) || StringLen(action) == 0)
         return;
      JsonStringField(response, "input", actionInput);

      string result = "null";
      string error = "";
      if(action == "rates")
         result = BuildRatesResult(actionInput);
      else if(action == "orders")
         result = BuildOrdersJSON(true);
      else if(action == "send")
         result = ExecuteSend(actionInput, error);
      else if(action == "modify")
         result = ExecuteModify(actionInput, error);
      else if(action == "close")
         result = ExecuteClose(actionInput, error);
      else if(action == "delete")
         result = ExecuteDelete(actionInput, error);
      else
      {
         error = "Unsupported DMTrader action: " + action;
         Print("[TraderConnect] ", error);
      }

      string nextResponse = "";
      if(!HttpPost(MakeEnvelope(action, result, error), nextResponse))
         return;
      response = nextResponse;
   }
   Print("[TraderConnect] Queue limit reached; remaining actions will be polled next time.");
}

string MakeEnvelope(const string action, const string resultJSON, const string error)
{
   string body = "{\"version\":4,\"terminal\":\"" + JsonEscape(g_terminalName) +
                 "\",\"login\":" + IntegerToString(AccountNumber()) +
                 ",\"action\":\"" + JsonEscape(action) +
                 "\",\"result\":" + resultJSON;
   if(StringLen(error) > 0)
      body += ",\"error\":\"" + JsonEscape(error) + "\"";
   body += "}";
   return(body);
}

bool HttpPost(const string body, string &response)
{
   char postData[];
   char responseData[];
   string responseHeaders = "";
   string headers = "Content-Type: application/json\r\n";

   int byteCount = StringToCharArray(body, postData, 0, WHOLE_ARRAY, CP_UTF8);
   if(byteCount <= 1)
   {
      Print("[TraderConnect] Could not encode HTTP request.");
      return(false);
   }
   // StringToCharArray includes the trailing zero byte; JSON does not.
   ArrayResize(postData, byteCount - 1);

   ResetLastError();
   int status = WebRequest("POST", ServerURL, headers, RequestTimeoutMs,
                           postData, responseData, responseHeaders);
   if(status == -1)
   {
      int err = GetLastError();
      datetime now = TimeLocal();
      if(g_lastHttpLog == 0 || now - g_lastHttpLog >= 30)
      {
         Print("[TraderConnect] WebRequest failed (MT4 error ", err,
               "). Check that DMTrader is running, the port is correct, and ",
               "http://127.0.0.1:2122 is allowed in MT4 WebRequest settings.");
         g_lastHttpLog = now;
      }
      ResetLastError();
      return(false);
   }

   response = CharArrayToString(responseData, 0, WHOLE_ARRAY, CP_UTF8);
   if(status < 200 || status >= 300)
   {
      datetime now = TimeLocal();
      if(g_lastHttpLog == 0 || now - g_lastHttpLog >= 30)
      {
         Print("[TraderConnect] DMTrader returned HTTP status ", status,
               ". Response: ", StringSubstr(response, 0, 300));
         g_lastHttpLog = now;
      }
      return(false);
   }
   return(true);
}

string BuildTickResult()
{
   string result = "{\"terminal\":{\"name\":\"" + JsonEscape(g_terminalName) +
                   "\",\"time\":" + JsonTimestamp(TimeCurrent()) + "}";
   result += ",\"account\":{\"login\":" + IntegerToString(AccountNumber());
   result += ",\"name\":\"" + JsonEscape(AccountName()) + "\"";
   result += ",\"server\":\"" + JsonEscape(AccountServer()) + "\"";
   result += ",\"currency\":\"" + JsonEscape(AccountCurrency()) + "\"";
   result += ",\"balance\":" + JsonDouble(AccountBalance(), 2);
   result += ",\"equity\":" + JsonDouble(AccountEquity(), 2);
   result += ",\"margin\":" + JsonDouble(AccountMargin(), 2);
   result += ",\"freeMargin\":" + JsonDouble(AccountFreeMargin(), 2);
   result += ",\"leverage\":" + IntegerToString(AccountLeverage());
   result += ",\"tradeMode\":" + (IsDemo() ? "0" : "2") + "}";
   result += ",\"symbols\":" + BuildSymbolsJSON();
   result += ",\"orders\":" + BuildOrdersJSON(false);
   // Current, still-forming candles keep the app's charts and strategies live.
   result += ",\"rates\":" + BuildLiveRatesJSON();
   result += "}";
   return(result);
}

int GetMarketWatchSymbols(string &symbols[])
{
   ArrayResize(symbols, 0);
   int total = SymbolsTotal(true);
   for(int i = 0; i < total; i++)
      AddSymbol(symbols, SymbolName(i, true));
   AddSymbol(symbols, Symbol());
   return(ArraySize(symbols));
}

void AddSymbol(string &symbols[], const string symbol)
{
   if(StringLen(symbol) == 0)
      return;
   for(int i = 0; i < ArraySize(symbols); i++)
      if(symbols[i] == symbol)
         return;
   int n = ArraySize(symbols);
   ArrayResize(symbols, n + 1);
   symbols[n] = symbol;
}

string BuildSymbolsJSON()
{
   string symbols[];
   GetMarketWatchSymbols(symbols);
   string json = "[";
   for(int i = 0; i < ArraySize(symbols); i++)
   {
      if(i > 0)
         json += ",";
      json += BuildOneSymbolJSON(symbols[i]);
   }
   json += "]";
   return(json);
}

string BuildOneSymbolJSON(const string symbol)
{
   int digits = (int)MarketInfo(symbol, MODE_DIGITS);
   if(digits < 0 || digits > 8)
      digits = 5;

   double point = MarketInfo(symbol, MODE_POINT);
   double tickValue = MarketInfo(symbol, MODE_TICKVALUE);
   double tickSize = MarketInfo(symbol, MODE_TICKSIZE);
   double valuePerPoint = tickValue;
   if(point > 0.0 && tickSize > 0.0)
   {
      // Broker builds report MODE_TICKSIZE either as a price increment or points.
      if(tickSize <= point * 10.0)
         valuePerPoint = tickValue * point / tickSize;
      else
         valuePerPoint = tickValue / tickSize;
   }

   double bid = MarketInfo(symbol, MODE_BID);
   double ask = MarketInfo(symbol, MODE_ASK);
   datetime quoteTime = (datetime)MarketInfo(symbol, MODE_TIME);
   if(quoteTime <= 0)
      quoteTime = TimeCurrent();

   string json = "{\"name\":\"" + JsonEscape(symbol) + "\"";
   json += ",\"bid\":" + JsonDouble(bid, digits);
   json += ",\"ask\":" + JsonDouble(ask, digits);
   json += ",\"point\":" + JsonDouble(point, 8);
   json += ",\"digits\":" + IntegerToString(digits);
   json += ",\"spread\":" + IntegerToString((int)MarketInfo(symbol, MODE_SPREAD));
   json += ",\"tradeTickValue\":" + JsonDouble(valuePerPoint, 8);
   json += ",\"tradeTickSize\":" + JsonDouble(tickSize, 8);
   json += ",\"volumeMin\":" + JsonDouble(MarketInfo(symbol, MODE_MINLOT), 8);
   json += ",\"volumeMax\":" + JsonDouble(MarketInfo(symbol, MODE_MAXLOT), 8);
   json += ",\"volumeStep\":" + JsonDouble(MarketInfo(symbol, MODE_LOTSTEP), 8);
   json += ",\"tradeStopLevel\":" + IntegerToString((int)MarketInfo(symbol, MODE_STOPLEVEL));
   json += ",\"tradeAllowed\":" + (MarketInfo(symbol, MODE_TRADEALLOWED) > 0 ? "true" : "false");
   json += ",\"time\":" + JsonTimestamp(quoteTime) + "}";
   return(json);
}

string BuildLiveRatesJSON()
{
   int frames[9] = {PERIOD_M1, PERIOD_M5, PERIOD_M15, PERIOD_M30,
                    PERIOD_H1, PERIOD_H4, PERIOD_D1, PERIOD_W1, PERIOD_MN1};
   string symbols[];
   GetMarketWatchSymbols(symbols);

   string json = "{";
   bool firstSymbol = true;
   for(int s = 0; s < ArraySize(symbols); s++)
   {
      string perSymbol = "{";
      bool firstFrame = true;
      for(int f = 0; f < 9; f++)
      {
         datetime barTime = iTime(symbols[s], frames[f], 0);
         if(barTime <= 0)
            continue;
         if(!firstFrame)
            perSymbol += ",";
         perSymbol += "\"" + IntegerToString(frames[f]) + "\":[" +
                      BuildBarJSON(symbols[s], frames[f], 0) + "]";
         firstFrame = false;
      }
      perSymbol += "}";
      if(firstFrame)
         continue;
      if(!firstSymbol)
         json += ",";
      json += "\"" + JsonEscape(symbols[s]) + "\":" + perSymbol;
      firstSymbol = false;
   }
   json += "}";
   return(json);
}

string BuildBarJSON(const string symbol, const int timeframe, const int shift)
{
   datetime barTime = iTime(symbol, timeframe, shift);
   int digits = (int)MarketInfo(symbol, MODE_DIGITS);
   if(digits < 0 || digits > 8)
      digits = 5;
   string json = "{\"time\":" + JsonTimestamp(barTime);
   json += ",\"open\":" + JsonDouble(iOpen(symbol, timeframe, shift), digits);
   json += ",\"high\":" + JsonDouble(iHigh(symbol, timeframe, shift), digits);
   json += ",\"low\":" + JsonDouble(iLow(symbol, timeframe, shift), digits);
   json += ",\"close\":" + JsonDouble(iClose(symbol, timeframe, shift), digits);
   json += ",\"volume\":" + DoubleToString((double)iVolume(symbol, timeframe, shift), 0) + "}";
   return(json);
}

string BuildOrdersJSON(const bool includeHistory)
{
   string json = "[";
   bool first = true;

   int openCount = OrdersTotal();
   for(int i = 0; i < openCount; i++)
   {
      if(!OrderSelect(i, SELECT_BY_POS, MODE_TRADES))
         continue;
      if(!first)
         json += ",";
      json += SerializeSelectedOrder();
      first = false;
   }

   if(includeHistory)
   {
      int historyCount = OrdersHistoryTotal();
      int limit = HistoryOrdersLimit;
      if(limit < 0)
         limit = 0;
      int firstHistory = historyCount - limit;
      if(firstHistory < 0)
         firstHistory = 0;
      for(int h = firstHistory; h < historyCount; h++)
      {
         if(!OrderSelect(h, SELECT_BY_POS, MODE_HISTORY))
            continue;
         if(!first)
            json += ",";
         json += SerializeSelectedOrder();
         first = false;
      }
   }

   json += "]";
   return(json);
}

string SerializeSelectedOrder()
{
   string symbol = OrderSymbol();
   int digits = (int)MarketInfo(symbol, MODE_DIGITS);
   if(digits < 0 || digits > 8)
      digits = 5;

   string json = "{\"ticket\":" + IntegerToString(OrderTicket());
   json += ",\"type\":" + IntegerToString(OrderType());
   json += ",\"symbol\":\"" + JsonEscape(symbol) + "\"";
   json += ",\"lots\":" + JsonDouble(OrderLots(), 8);
   json += ",\"openPrice\":" + JsonDouble(OrderOpenPrice(), digits);
   json += ",\"stopLoss\":" + JsonDouble(OrderStopLoss(), digits);
   json += ",\"takeProfit\":" + JsonDouble(OrderTakeProfit(), digits);
   json += ",\"openTime\":" + JsonTimestamp(OrderOpenTime());
   json += ",\"closeTime\":" + JsonTimestamp(OrderCloseTime());
   json += ",\"closePrice\":" + JsonDouble(OrderClosePrice(), digits);
   json += ",\"expiration\":" + JsonTimestamp(OrderExpiration());
   json += ",\"commission\":" + JsonDouble(OrderCommission(), 2);
   json += ",\"swap\":" + JsonDouble(OrderSwap(), 2);
   json += ",\"profit\":" + JsonDouble(OrderProfit(), 2);
   json += ",\"comment\":\"" + JsonEscape(OrderComment()) + "\"";
   json += ",\"magicNumber\":" + IntegerToString(OrderMagicNumber()) + "}";
   return(json);
}

string BuildRatesResult(const string requestInput)
{
   string fields[];
   int count = StringSplit(requestInput, (ushort)126, fields);
   if(count < 4)
      return("{}");

   string requestedSymbol = fields[0];
   long fromMs = (long)StringToDouble(fields[1]);
   long toMs = (long)StringToDouble(fields[2]);
   int minutes = (int)StringToInteger(fields[3]);
   int timeframe = TimeframeFromMinutes(minutes);
   if(timeframe <= 0)
      return("{}");

   if(toMs <= 0)
      toMs = (long)TimeCurrent() * 1000;
   if(fromMs > toMs)
   {
      long swapTime = fromMs;
      fromMs = toMs;
      toMs = swapTime;
   }

   datetime fromTime = (datetime)(fromMs / 1000);
   datetime toTime = (datetime)(toMs / 1000);
   string symbols[];
   if(requestedSymbol == "ALL")
      GetMarketWatchSymbols(symbols);
   else
   {
      ArrayResize(symbols, 1);
      symbols[0] = requestedSymbol;
   }

   string json = "{";
   bool firstSymbol = true;
   for(int s = 0; s < ArraySize(symbols); s++)
   {
      MqlRates bars[];
      ArraySetAsSeries(bars, false);
      int copied = -1;
      for(int retry = 0; retry < 3; retry++)
      {
         ResetLastError();
         copied = CopyRates(symbols[s], (ENUM_TIMEFRAMES)timeframe, fromTime, toTime, bars);
         if(copied > 0)
            break;
         Sleep(150);
      }

      if(!firstSymbol)
         json += ",";
      json += "\"" + JsonEscape(symbols[s]) + "\":{\"" +
              IntegerToString(minutes) + "\":[";

      if(copied > 0)
      {
         int digits = (int)MarketInfo(symbols[s], MODE_DIGITS);
         if(digits < 0 || digits > 8)
            digits = 5;
         int start = 0;
         int step = 1;
         if(copied > 1 && bars[0].time > bars[copied - 1].time)
         {
            start = copied - 1;
            step = -1;
         }
         bool firstBar = true;
         for(int b = start; b >= 0 && b < copied; b += step)
         {
            if(!firstBar)
               json += ",";
            json += "{\"time\":" + JsonTimestamp(bars[b].time);
            json += ",\"open\":" + JsonDouble(bars[b].open, digits);
            json += ",\"high\":" + JsonDouble(bars[b].high, digits);
            json += ",\"low\":" + JsonDouble(bars[b].low, digits);
            json += ",\"close\":" + JsonDouble(bars[b].close, digits);
            json += ",\"volume\":" + DoubleToString((double)bars[b].tick_volume, 0) + "}";
            firstBar = false;
         }
      }

      json += "]}";
      firstSymbol = false;
   }
   json += "}";
   return(json);
}

int TimeframeFromMinutes(const int minutes)
{
   switch(minutes)
   {
      case 1:     return(PERIOD_M1);
      case 5:     return(PERIOD_M5);
      case 15:    return(PERIOD_M15);
      case 30:    return(PERIOD_M30);
      case 60:    return(PERIOD_H1);
      case 240:   return(PERIOD_H4);
      case 1440:  return(PERIOD_D1);
      case 10080: return(PERIOD_W1);
      case 43200: return(PERIOD_MN1);
   }
   return(0);
}

string ExecuteSend(const string requestInput, string &error)
{
   if(!AllowTradeCommands)
   {
      error = "Trade commands are disabled. Enable AllowTradeCommands only after demo testing.";
      return("-1");
   }
   if(!IsTradeAllowed())
   {
      error = "MT4 trading is not currently allowed; check AutoTrading and EA permissions.";
      return("-1");
   }

   string fields[];
   int count = StringSplit(requestInput, (ushort)126, fields);
   if(count < 3)
   {
      error = "Invalid send command; expected symbol, type and lots.";
      return("-1");
   }

   string symbol = fields[0];
   int type = (int)StringToInteger(fields[1]);
   double lots = StringToDouble(fields[2]);
   if(StringLen(symbol) == 0 || type < OP_BUY || type > OP_SELLSTOP || lots <= 0.0)
   {
      error = "Invalid symbol, order type or volume in send command.";
      return("-1");
   }

   double stopLoss = OptionalNumber(fields, count, 3, 0.0);
   double takeProfit = OptionalNumber(fields, count, 4, 0.0);
   int slippage = DefaultSlippagePoints;
   if(count > 5 && StringLen(fields[5]) > 0)
      slippage = (int)StringToInteger(fields[5]);
   double requestedPrice = OptionalNumber(fields, count, 6, 0.0);
   string comment = (count > 7 ? fields[7] : "");
   int magic = (count > 8 && StringLen(fields[8]) > 0 ? (int)StringToInteger(fields[8]) : 0);
   datetime expiration = (count > 9 ? ParseExpiration(fields[9]) : 0);

   double step = MarketInfo(symbol, MODE_LOTSTEP);
   double minLot = MarketInfo(symbol, MODE_MINLOT);
   double maxLot = MarketInfo(symbol, MODE_MAXLOT);
   lots = NormalizeLots(lots, step);
   if(lots <= 0.0 || (minLot > 0.0 && lots + 1e-9 < minLot))
   {
      error = "Requested volume is below the broker's minimum or lot step for " + symbol + ".";
      return("-1");
   }
   if(maxLot > 0.0 && lots > maxLot)
      lots = NormalizeLots(maxLot, step);

   int digits = (int)MarketInfo(symbol, MODE_DIGITS);
   if(digits < 0 || digits > 8)
      digits = 5;
   RefreshRates();
   double price = requestedPrice;
   if(type == OP_BUY)
      price = (requestedPrice > 0.0 ? requestedPrice : MarketInfo(symbol, MODE_ASK));
   else if(type == OP_SELL)
      price = (requestedPrice > 0.0 ? requestedPrice : MarketInfo(symbol, MODE_BID));
   if(price <= 0.0)
   {
      error = "No valid market or pending-order price is available for " + symbol + ".";
      return("-1");
   }

   price = NormalizeDouble(price, digits);
   if(stopLoss > 0.0)
      stopLoss = NormalizeDouble(stopLoss, digits);
   if(takeProfit > 0.0)
      takeProfit = NormalizeDouble(takeProfit, digits);

   ResetLastError();
   int ticket = OrderSend(symbol, type, lots, price, slippage, stopLoss, takeProfit,
                          comment, magic, expiration, clrNONE);
   if(ticket < 0)
   {
      int err = GetLastError();
      error = "OrderSend failed for " + symbol + " (MT4 error " + IntegerToString(err) + ").";
      ResetLastError();
      return("-1");
   }
   return(IntegerToString(ticket));
}

string ExecuteModify(const string requestInput, string &error)
{
   if(!AllowTradeCommands)
   {
      error = "Trade commands are disabled. Enable AllowTradeCommands only after demo testing.";
      return("false");
   }
   if(!IsTradeAllowed())
   {
      error = "MT4 trading is not currently allowed; check AutoTrading and EA permissions.";
      return("false");
   }

   string fields[];
   int count = StringSplit(requestInput, (ushort)126, fields);
   if(count < 1)
   {
      error = "Invalid modify command; missing ticket.";
      return("false");
   }
   int ticket = (int)StringToInteger(fields[0]);
   if(ticket <= 0 || !OrderSelect(ticket, SELECT_BY_TICKET))
   {
      error = "Could not select order for modify; ticket " + IntegerToString(ticket) + ".";
      return("false");
   }

   double price = OrderOpenPrice();
   double stopLoss = OrderStopLoss();
   double takeProfit = OrderTakeProfit();
   datetime expiration = OrderExpiration();
   if(count > 1 && StringLen(fields[1]) > 0)
      stopLoss = StringToDouble(fields[1]);
   if(count > 2 && StringLen(fields[2]) > 0)
      takeProfit = StringToDouble(fields[2]);
   if(count > 3 && StringLen(fields[3]) > 0)
   {
      double newPrice = StringToDouble(fields[3]);
      if(newPrice > 0.0)
         price = newPrice;
   }
   if(count > 4 && StringLen(fields[4]) > 0)
      expiration = ParseExpiration(fields[4]);

   int digits = (int)MarketInfo(OrderSymbol(), MODE_DIGITS);
   if(digits < 0 || digits > 8)
      digits = 5;
   price = NormalizeDouble(price, digits);
   if(stopLoss > 0.0)
      stopLoss = NormalizeDouble(stopLoss, digits);
   if(takeProfit > 0.0)
      takeProfit = NormalizeDouble(takeProfit, digits);

   ResetLastError();
   bool ok = OrderModify(ticket, price, stopLoss, takeProfit, expiration, clrNONE);
   if(!ok)
   {
      int err = GetLastError();
      error = "OrderModify failed for ticket " + IntegerToString(ticket) +
              " (MT4 error " + IntegerToString(err) + ").";
      ResetLastError();
      return("false");
   }
   return("true");
}

string ExecuteClose(const string requestInput, string &error)
{
   if(!AllowTradeCommands)
   {
      error = "Trade commands are disabled. Enable AllowTradeCommands only after demo testing.";
      return("false");
   }
   if(!IsTradeAllowed())
   {
      error = "MT4 trading is not currently allowed; check AutoTrading and EA permissions.";
      return("false");
   }

   string fields[];
   int count = StringSplit(requestInput, (ushort)126, fields);
   if(count < 1)
   {
      error = "Invalid close command; missing ticket.";
      return("false");
   }
   int ticket = (int)StringToInteger(fields[0]);
   if(ticket <= 0 || !OrderSelect(ticket, SELECT_BY_TICKET))
   {
      error = "Could not select order for close; ticket " + IntegerToString(ticket) + ".";
      return("false");
   }

   // MT4 pending orders are removed rather than closed at market.
   if(OrderType() > OP_SELL)
   {
      ResetLastError();
      bool deleted = OrderDelete(ticket, clrNONE);
      if(!deleted)
      {
         int delErr = GetLastError();
         error = "OrderDelete failed for ticket " + IntegerToString(ticket) +
                 " (MT4 error " + IntegerToString(delErr) + ").";
         ResetLastError();
         return("false");
      }
      return("true");
   }

   string symbol = OrderSymbol();
   double lots = OrderLots();
   if(count > 1 && StringLen(fields[1]) > 0)
   {
      double requestedLots = StringToDouble(fields[1]);
      if(requestedLots > 0.0)
         lots = requestedLots;
   }
   lots = NormalizeLots(lots, MarketInfo(symbol, MODE_LOTSTEP));
   if(lots <= 0.0)
   {
      error = "Invalid close volume for ticket " + IntegerToString(ticket) + ".";
      return("false");
   }

   int slippage = DefaultSlippagePoints;
   if(count > 2 && StringLen(fields[2]) > 0)
      slippage = (int)StringToInteger(fields[2]);
   double requestedPrice = OptionalNumber(fields, count, 3, 0.0);
   RefreshRates();
   double price = requestedPrice;
   if(price <= 0.0)
      price = (OrderType() == OP_BUY ? MarketInfo(symbol, MODE_BID) : MarketInfo(symbol, MODE_ASK));
   int digits = (int)MarketInfo(symbol, MODE_DIGITS);
   if(digits < 0 || digits > 8)
      digits = 5;
   price = NormalizeDouble(price, digits);

   ResetLastError();
   bool closed = OrderClose(ticket, lots, price, slippage, clrNONE);
   if(!closed)
   {
      int err = GetLastError();
      error = "OrderClose failed for ticket " + IntegerToString(ticket) +
              " (MT4 error " + IntegerToString(err) + ").";
      ResetLastError();
      return("false");
   }
   return("true");
}

string ExecuteDelete(const string requestInput, string &error)
{
   if(!AllowTradeCommands)
   {
      error = "Trade commands are disabled. Enable AllowTradeCommands only after demo testing.";
      return("false");
   }
   if(!IsTradeAllowed())
   {
      error = "MT4 trading is not currently allowed; check AutoTrading and EA permissions.";
      return("false");
   }

   string fields[];
   int count = StringSplit(requestInput, (ushort)126, fields);
   if(count < 1)
   {
      error = "Invalid delete command; missing ticket.";
      return("false");
   }
   int ticket = (int)StringToInteger(fields[0]);
   ResetLastError();
   bool ok = OrderDelete(ticket, clrNONE);
   if(!ok)
   {
      int err = GetLastError();
      error = "OrderDelete failed for ticket " + IntegerToString(ticket) +
              " (MT4 error " + IntegerToString(err) + ").";
      ResetLastError();
      return("false");
   }
   return("true");
}

double OptionalNumber(string &fields[], const int count, const int index, const double fallback)
{
   if(index < 0 || index >= count || StringLen(fields[index]) == 0)
      return(fallback);
   return(StringToDouble(fields[index]));
}

datetime ParseExpiration(const string value)
{
   if(StringLen(value) == 0)
      return(0);
   double raw = StringToDouble(value);
   if(raw > 100000000000.0) // DMTrader timestamps are milliseconds.
      raw /= 1000.0;
   if(raw < 0.0)
      return(0);
   return((datetime)raw);
}

double NormalizeLots(const double lots, const double step)
{
   if(lots <= 0.0)
      return(0.0);
   double adjusted = lots;
   if(step > 0.0)
      adjusted = MathFloor((lots + step * 1e-8) / step) * step;

   int precision = 2;
   if(step > 0.0)
   {
      precision = 0;
      double scaled = step;
      while(precision < 8 && MathAbs(scaled - MathRound(scaled)) > 1e-8)
      {
         scaled *= 10.0;
         precision++;
      }
   }
   return(NormalizeDouble(adjusted, precision));
}

string JsonTimestamp(const datetime value)
{
   if(value <= 0)
      return("0");
   return(DoubleToString((double)((long)value * 1000), 0));
}

string JsonDouble(const double value, const int precision)
{
   if(!MathIsValidNumber(value))
      return("0");
   int digits = precision;
   if(digits < 0)
      digits = 0;
   if(digits > 8)
      digits = 8;
   return(DoubleToString(value, digits));
}

string JsonEscape(const string value)
{
   string escaped = "";
   for(int i = 0; i < StringLen(value); i++)
   {
      ushort ch = StringGetCharacter(value, i);
      if(ch == 34)
         escaped += "\\\"";
      else if(ch == 92)
         escaped += "\\\\";
      else if(ch == 8)
         escaped += "\\b";
      else if(ch == 9)
         escaped += "\\t";
      else if(ch == 10)
         escaped += "\\n";
      else if(ch == 12)
         escaped += "\\f";
      else if(ch == 13)
         escaped += "\\r";
      else if(ch < 32)
         escaped += " ";
      else
         escaped += StringSubstr(value, i, 1);
   }
   return(escaped);
}

bool JsonStringField(const string json, const string key, string &value)
{
   string marker = "\"" + key + "\"";
   int position = StringFind(json, marker);
   if(position < 0)
      return(false);
   position = StringFind(json, ":", position + StringLen(marker));
   if(position < 0)
      return(false);
   position++;

   int length = StringLen(json);
   while(position < length)
   {
      ushort ch = StringGetCharacter(json, position);
      if(ch != 32 && ch != 9 && ch != 10 && ch != 13)
         break;
      position++;
   }
   if(position >= length || StringGetCharacter(json, position) != 34)
      return(false);

   position++;
   value = "";
   bool escaped = false;
   for(int i = position; i < length; i++)
   {
      ushort ch = StringGetCharacter(json, i);
      if(escaped)
      {
         if(ch == 34)
            value += "\"";
         else if(ch == 92)
            value += "\\";
         else if(ch == 110)
            value += "\n";
         else if(ch == 114)
            value += "\r";
         else if(ch == 116)
            value += "\t";
         else if(ch == 98)
            value += " ";
         else if(ch == 102)
            value += " ";
         else
            value += StringSubstr(json, i, 1);
         escaped = false;
         continue;
      }
      if(ch == 92)
      {
         escaped = true;
         continue;
      }
      if(ch == 34)
         return(true);
      value += StringSubstr(json, i, 1);
   }
   return(false);
}
