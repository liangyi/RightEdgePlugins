using System;
using System.Collections.Generic;
using System.Text;
using RightEdge.Common;
using Krs.Ats.IBNet;
using System.Linq;
using System.Xml;

using TickType = RightEdge.Common.TickType;
using KRSTickType = Krs.Ats.IBNet.TickType;
using System.Diagnostics;
using System.Threading;
using System.Globalization;
using System.Xml.Linq;
using System.IO;
using System.Reflection;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace RightEdge.TWSCSharpPlugin
{
    public sealed class TWSPlugin : IService, ITickRetrieval, IBarDataRetrieval, IBroker
    {
        //public static EventHandler OutOfBandCallback;

        IBClient client;
        private object _lockObject = new object();

        private ServiceConnectOptions _connectOptions;

        private bool _connected = false;
        private bool hadError = false;
        private bool _watching = false;

        //private bool _firstHistRequest = true;

        public class Settings
        {
            public bool UseRTH { get; set; }
            public bool IgnoreLastHistBar { get; set; }

            public bool EnableLogging { get; set; }
            public string LogPath { get; set; }
            public bool CleanupLogs { get; set; }
            public int DaysToKeepLogs { get; set; }

            public int ClientIDBroker { get; set; }
            public int ClientIDLiveData { get; set; }
            public int ClientIDHist { get; set; }

            public string AccountCode { get; set; }
            public FinancialAdvisorAllocationMethod FAMethod { get; set; }
            public string FAPercentage { get; set; }
            public string FAProfile { get; set; }

            public Settings()
            {
                //  Defaults
                UseRTH = false;
                IgnoreLastHistBar = false;

                EnableLogging = true;

                PropertyInfo appDataPathProperty = typeof(CommonGlobals).GetProperty("UserAppDataPath", BindingFlags.Public | BindingFlags.Static);
                if (appDataPathProperty != null)
                {
                    string appDataPath = (string)appDataPathProperty.GetValue(null, new object[0]);
                    LogPath = Path.Combine(appDataPath, "TWSLogs");
                }
                else
                {
                    LogPath = @"C:\RightEdgeTWSLogs";
                }

                CleanupLogs = true;
                DaysToKeepLogs = 30;

                ClientIDBroker = 1001;
                ClientIDLiveData = 1002;
                ClientIDHist = 1003;

                AccountCode = "";
                FAMethod = FinancialAdvisorAllocationMethod.None;
                FAPercentage = "";
                FAProfile = "";
            }

            public void SaveTo(IDictionary<string, string> dict)
            {
                dict["UseRTH"] = UseRTH.ToString();
                dict["IgnoreLastHistBar"] = IgnoreLastHistBar.ToString();

                dict["EnableLogging"] = EnableLogging.ToString();
                dict["LogPath"] = LogPath;
                dict["CleanupLogs"] = CleanupLogs.ToString();
                dict["DaysToKeepLogs"] = DaysToKeepLogs.ToString();

                dict["ClientIDBroker"] = ClientIDBroker.ToString();
                dict["ClientIDLiveData"] = ClientIDLiveData.ToString();
                dict["ClientIDHist"] = ClientIDHist.ToString();

                dict["AccountCode"] = AccountCode;
                dict["FAMethod"] = FAMethod.ToString();
                dict["FAPercentage"] = FAPercentage;
                dict["FAProfile"] = FAProfile;
            }

            public void LoadFrom(IDictionary<string, string> settings)
            {
                string rth;
                string ignorelast;

                string enableLogging;
                string logPath;
                string cleanupLogs;
                string daysToKeepLogs;

                string clientIDBroker = ClientIDBroker.ToString();
                string clientIDLiveData = ClientIDLiveData.ToString();
                string clientIDHist = ClientIDHist.ToString();

                string acctCode = "";
                string faMethod = "";
                string faPercentage = "";
                string faProfile = "";


                if (settings.TryGetValue("UseRTH", out rth))
                {
                    UseRTH = Convert.ToBoolean(rth);
                }

                if (settings.TryGetValue("IgnoreLastHistBar", out ignorelast))
                {
                    IgnoreLastHistBar = Convert.ToBoolean(ignorelast);
                }

                if (settings.TryGetValue("EnableLogging", out enableLogging))
                {
                    EnableLogging = Convert.ToBoolean(enableLogging);
                }

                if (settings.TryGetValue("LogPath", out logPath))
                {
                    LogPath = logPath;
                }

                if (settings.TryGetValue("CleanupLogs", out cleanupLogs))
                {
                    CleanupLogs = Convert.ToBoolean(cleanupLogs);
                }

                if (settings.TryGetValue("DaysToKeepLogs", out daysToKeepLogs))
                {
                    int i;
                    if (int.TryParse(daysToKeepLogs, out i))
                    {
                        DaysToKeepLogs = i;
                    }
                }

                if (settings.TryGetValue("ClientIDBroker", out clientIDBroker))
                {
                    int i;
                    if (int.TryParse(clientIDBroker, out i))
                    {
                        ClientIDBroker = i;
                    }
                }

                if (settings.TryGetValue("ClientIDLiveData", out clientIDLiveData))
                {
                    int i;
                    if (int.TryParse(clientIDLiveData, out i))
                    {
                        ClientIDLiveData = i;
                    }
                }

                if (settings.TryGetValue("ClientIDHist", out clientIDHist))
                {
                    int i;
                    if (int.TryParse(clientIDHist, out i))
                    {
                        ClientIDHist = i;
                    }
                }

                if (settings.TryGetValue("AccountCode", out acctCode))
                {
                    AccountCode = acctCode;
                }

                if (settings.TryGetValue("FAMethod", out faMethod))
                {
                    FAMethod = GetFAMethod(faMethod);
                }

                if (settings.TryGetValue("FAPercentage", out faPercentage))
                {
                    FAPercentage = faPercentage;
                }

                if (settings.TryGetValue("FAProfile", out faProfile))
                {
                    FAProfile = faProfile;
                }
            }

            public Settings Clone()
            {
                return (Settings)this.MemberwiseClone();
            }

            private static FinancialAdvisorAllocationMethod GetFAMethod(string s)
            {
                var parsedValue = EnumUtil<FinancialAdvisorAllocationMethod>.Parse(s);
                if (parsedValue.Success)
                {
                    return parsedValue.Value;
                }
                else
                {
                    return FinancialAdvisorAllocationMethod.None;
                }
            }
        }

        private Settings _settings = new Settings();


        public int nextID = 0;
        int nextOrderId = -1;
        string lastError = "";

        //	Reconnection data
        EventWaitHandle _connectWaitHandle;
        private bool _gettingReconnectData = false;
        private Dictionary<string, object> _potentiallyCancelledOrders = new Dictionary<string, object>();
        private Dictionary<string, List<Fill>> _knownFills = new Dictionary<string, List<Fill>>();

        private Dictionary<Symbol, int?> watchedSymbols = new Dictionary<Symbol, int?>();
        GotTickData tickListener = null;

        //	We have to store this data because TWS sends us the price and size seperately
        private Dictionary<Symbol, double> lastPrices = new Dictionary<Symbol, double>();
        private Dictionary<Symbol, UInt64> lastVolumes = new Dictionary<Symbol, ulong>();
        private Dictionary<Symbol, double> lastBidPrices = new Dictionary<Symbol, double>();
        private Dictionary<Symbol, UInt64> lastBidSizes = new Dictionary<Symbol, ulong>();
        private Dictionary<Symbol, double> lastAskPrices = new Dictionary<Symbol, double>();
        private Dictionary<Symbol, UInt64> lastAskSizes = new Dictionary<Symbol, ulong>();
        //private Dictionary<Symbol, double> lastHigh = new Dictionary<Symbol, double>();
        //private Dictionary<Symbol, double> lastLow = new Dictionary<Symbol, double>();
        //private Dictionary<Symbol, double> lastClose = new Dictionary<Symbol, double>();

        //	We don't get the time with each tick, but we do get an account time update every so often
        //	So we store the difference here
        TimeSpan accountTimeDiff = new TimeSpan(0);
        bool bGotAccountTime = false;

        private double buyingPower = 0.0;
        private double cashBalance = 0.0;
        private Dictionary<string, RightEdge.Common.BrokerOrder> openOrders = new Dictionary<string, RightEdge.Common.BrokerOrder>();
        Dictionary<string, List<string>> faAccountGroups = new Dictionary<string, List<string>>();

        //	This dictionary keeps track of orders that have been "placed", but for which
        //	we have not received the "Submitted" order status.  If these orders are
        //	canceled, we will probably not receive any cancellation confirmation.
        //	(How this works is mostly guesswork)
        private Dictionary<string, bool> unSubmittedOrders = new Dictionary<string, bool>();

        //	Keep track of order IDs that were filled so if a cancellation is requested we don't error
        private OrderHistory _orderHistory = new OrderHistory();

        //private Dictionary<Symbol, ulong> sharesLong = new Dictionary<Symbol, ulong>();
        //private Dictionary<Symbol, ulong> sharesShort = new Dictionary<Symbol, ulong>();

        // These are shares that already exist at IB on startup.
        private Dictionary<Symbol, int> openShares = new Dictionary<Symbol, int>();

        public event OrderUpdatedDelegate OrderUpdated;
        //public event PositionUpdatedDelegate PositionUpdated;
        //public event AccountUpdatedDelegate AccountUpdated;
        public event PositionAvailableDelegate PositionAvailable;
        public event EventHandler<ServiceEventArgs> ServiceEvent;

        private HistRetrieval _histRetrieval = null;

        public TWSPlugin()
        {
            ServerAddress = "127.0.0.1";
            Port = 7496;
        }

        private void ClearError()
        {
            lastError = "";
            hadError = false;
        }

        private bool CheckError()
        {
            return !hadError;
        }

        private Symbol idToSymbol(int id)
        {
            foreach (var kvp in watchedSymbols)
            {
                if (kvp.Value == id)
                {
                    return kvp.Key;
                }
            }
            //foreach (Symbol symbol in watchedSymbols.Keys)
            //{
            //    if (watchedSymbols[symbol] == id)
            //    {
            //        return symbol;
            //    }
            //}
            return null;
        }

        private DateTime GetAccountTime(string desc)
        {
            if (bGotAccountTime)
            {
                return DateTime.Now.Add(accountTimeDiff);
            }
            else
            {
                System.Diagnostics.Trace.WriteLine("Got " + desc + " before account time update.");
                return DateTime.Now;
            }
        }

        public static ulong AdjustVolume(Symbol symbol, ulong TWSVol)
        {
            if (symbol.AssetClass == AssetClass.Stock)
            {
                return TWSVol * 100;
            }
            else
            {
                return TWSVol;
            }
        }

        void client_TickPrice(object sender, TickPriceEventArgs e)
        {
            Symbol symbol = null;
            TickData data;
            GotTickData listener = null;
            lock (_lockObject)
            {
                symbol = idToSymbol(e.TickerId);
                if (symbol == null)
                {
                    //	Not a watched symbol
                    return;
                }

                data = new TickData();
                data.time = GetAccountTime("price tick");
                data.price = (double)e.Price;

                if (data.price <= 0 &&
                    symbol.AssetClass != AssetClass.Future &&
                    symbol.AssetClass != AssetClass.Option &&
                    symbol.AssetClass != AssetClass.FuturesOption)
                {
                    //	GBP/USD was getting bid and ask ticks with a price of zero, so ignore these.
                    //	4/20/2010 - A user reported various forex symbols were getting negative prices, so these will also be ignored
                    return;
                }

                if (e.TickType == KRSTickType.BidPrice)
                {
                    //	Bid price
                    data.tickType = TickType.Bid;
                    lastBidSizes.TryGetValue(symbol, out data.size);
                    lastBidPrices[symbol] = (double)e.Price;
                }
                else if (e.TickType == KRSTickType.AskPrice)
                {
                    //	Ask price
                    data.tickType = TickType.Ask;
                    lastAskSizes.TryGetValue(symbol, out data.size);
                    lastAskPrices[symbol] = (double)e.Price;
                }
                else if (e.TickType == KRSTickType.LastPrice)
                {
                    //	Last price;
                    lastPrices[symbol] = (double)e.Price;

                    if (symbol.AssetClass == AssetClass.Index)
                    {
                        // Indexes don't come with volume ticks, so we can
                        // force this tick through instead of trying to match
                        // it up with a volume tick.
                        data.tickType = TickType.Trade;
                    }
                    else
                    {
                        return;
                    }
                }
                else if (e.TickType == KRSTickType.HighPrice)
                {
                    //	High price
                    data.tickType = TickType.HighPrice;
                }
                else if (e.TickType == KRSTickType.LowPrice)
                {
                    //	Low price
                    data.tickType = TickType.LowPrice;
                }
                else if (e.TickType == KRSTickType.ClosePrice)
                {
                    //	Close price
                    data.tickType = TickType.PreviousClose;
                }
                else if (e.TickType == Krs.Ats.IBNet.TickType.OpenPrice)
                {
                    data.tickType = TickType.OpenPrice;
                }
                else
                {
                    //	Unknown tick type
                    return;
                }
                listener = tickListener;
            }
            if (data.tickType != TickType.NotSet && listener != null)
            {
                listener(symbol, data);
            }

        }

        void client_TickSize(object sender, TickSizeEventArgs e)
        {
            Symbol symbol = null;
            TickData data;
            GotTickData listener = null;

            lock (_lockObject)
            {
                symbol = idToSymbol(e.TickerId);
                if (symbol == null)
                {
                    //	Not a watched symbol
                    return;
                }

                data = new TickData();
                data.time = GetAccountTime("size tick");
                data.size = AdjustVolume(symbol, (UInt64)e.Size);
                if (e.TickType == KRSTickType.BidSize)
                {
                    //	Bid size
                    data.tickType = TickType.Bid;
                    lastBidPrices.TryGetValue(symbol, out data.price);
                    lastBidSizes[symbol] = data.size;
                }
                else if (e.TickType == KRSTickType.AskSize)
                {
                    //	Ask size
                    data.tickType = TickType.Ask;
                    lastAskPrices.TryGetValue(symbol, out data.price);
                    lastAskSizes[symbol] = data.size;
                }
                else if (e.TickType == KRSTickType.LastSize)
                {
                    //	Last Size
                    return;
                    //data.tickType = TickType.LastSize;
                }
                else if (e.TickType == KRSTickType.Volume)
                {
                    //	Volume
                    bool bSend = true;
                    UInt64 lastVolume;
                    TickData tradeTick = new TickData();
                    if (!lastVolumes.TryGetValue(symbol, out lastVolume))
                    {
                        bSend = false;
                    }
                    else if ((UInt64)e.Size <= lastVolume)
                    {
                        bSend = false;
                    }
                    else if (!lastPrices.TryGetValue(symbol, out tradeTick.price))
                    {
                        bSend = false;
                    }
                    //if (lastVolume == -1 || data.value <= lastVolume)
                    //{
                    //    bSend = false;
                    //}

                    if (bSend)
                    {
                        tradeTick.time = data.time;
                        tradeTick.tickType = TickType.Trade;
                        tradeTick.size = AdjustVolume(symbol, (UInt64)e.Size - lastVolume);

                        //lastVolume = e.size * 100;


                        if (tickListener != null)
                        {
                            tickListener(symbol, tradeTick);
                        }
                    }

                    if (e.Size > 0)
                    {
                        lastVolumes[symbol] = (UInt64)e.Size;
                    }

                    data.tickType = TickType.DailyVolume;


                }
                else
                {
                    //	Unknown tick type
                    return;
                }
                listener = tickListener;
            }

            if (listener != null)
            {
                listener(symbol, data);
            }
        }

        void client_UpdateAccountValue(object sender, UpdateAccountValueEventArgs e)
        {
            lock (_lockObject)
            {
                //	You can use e.Key == "NetLiquidation" instead if preferred
                if (e.Key == "BuyingPower")
                {
                    buyingPower = Convert.ToDouble(e.Value, CultureInfo.InvariantCulture);
                }
                else if (e.Key == "CashBalance")
                {
                    cashBalance = Convert.ToDouble(e.Value, CultureInfo.InvariantCulture);
                }
                else
                {
                    //Trace.WriteLine(string.Format("TWS {0}: {1} {2}", e.Key, e.Value, e.Currency));
                }
            }
        }

        void client_UpdatePortfolio(object sender, UpdatePortfolioEventArgs e)
        {
            PositionAvailableDelegate del;
            Symbol symbol;
            lock (_lockObject)
            {
                symbol = TWSAssetArgs.SymbolFromContract(e.Contract);
                openShares[symbol] = e.Position;
                del = PositionAvailable;
            }
            if (del != null)
            {
                del(symbol, e.Position);
            }
        }

        void client_OrderStatus(object sender, OrderStatusEventArgs e)
        {
            lock (_lockObject)
            {
                string msg = "IB order status: " + e.OrderId + " " + e.Status;
                //Console.WriteLine(msg);
                Trace.WriteLine(msg);

                if (!openOrders.ContainsKey(e.OrderId.ToString()))
                {
                    Log(null, e.OrderId.ToString(), null, "OrderStatus" + e.Status.ToString(), "Order ID not currently tracked by TWS plugin.");
                }
                else
                {
                    RightEdge.Common.BrokerOrder openOrder = openOrders[e.OrderId.ToString()];

                    Log(openOrder, "OrderStatus" + e.Status.ToString(), "Order status updated");

                    bool orderProcessed = false;
                    Fill fill = null;
                    string information = "";

                    switch (e.Status)
                    {
                        case OrderStatus.Filled:
                            //	Handle fills with ExecDetails event

                            break;

                        //	Apparently, stop orders don't get "Submitted", they get "presubmitted" instead
                        case OrderStatus.Submitted:
                        case OrderStatus.PreSubmitted:
                        case OrderStatus.ApiPending:
                            if (e.Status == OrderStatus.ApiPending)
                            {
                                //	Not sure what the ApiPending status is used for
                                int b = 0;
                            }

                            Trace.WriteLine("IB " + e.Status.ToString() + ": " + openOrder.ToString());
                            if (_gettingReconnectData)
                            {
                                if (_potentiallyCancelledOrders.ContainsKey(openOrder.OrderId))
                                {
                                    _potentiallyCancelledOrders.Remove(openOrder.OrderId);
                                }
                            }
                            else
                            {
                                openOrder.OrderState = BrokerOrderState.Submitted;
                                orderProcessed = true;
                            }
                            if (unSubmittedOrders.ContainsKey(openOrder.OrderId))
                            {
                                unSubmittedOrders.Remove(openOrder.OrderId);
                            }

                            break;

                        case OrderStatus.Canceled:
                            openOrder.OrderState = BrokerOrderState.Cancelled;
                            information = "Cancelled";
                            orderProcessed = true;
                            break;

                    }

                    if (orderProcessed)
                    {
                        SendOrderUpdate(openOrder, fill, information, GetAccountTime("client_OrderStatus"));

                        if (openOrder.OrderState == BrokerOrderState.Filled)
                        {
                            //	TODO: AddShares
                            //AddShares(openOrder);
                        }
                    }

                    if (openOrder.OrderState == BrokerOrderState.Filled || openOrder.OrderState == BrokerOrderState.Rejected ||
                        openOrder.OrderState == BrokerOrderState.Cancelled)
                    {
                        openOrders.Remove(openOrder.OrderId);
                    }
                }
            }
        }

        void client_ExecDetails(object sender, ExecDetailsEventArgs e)
        {
            lock (_lockObject)
            {
                Symbol symbol = TWSAssetArgs.SymbolFromContract(e.Contract);

                BrokerOrder openOrder;
                openOrders.TryGetValue(e.OrderId.ToString(), out openOrder);

                //string msg = "IB ExecDetails: " + e.Execution.Time + " " + e.Execution.Side + " " + symbol + " Order ID: " + e.Execution.OrderId +
                //    " Size: " + e.Execution.Shares + " Price: " + e.Execution.Price;
                //Trace.WriteLine(msg);
                //Console.WriteLine(msg);

                string logDetails = e.Execution.Time + " " + e.Execution.Side + " Size: " + e.Execution.Shares + " Price: " + e.Execution.Price;
                if (!string.IsNullOrEmpty(e.Execution.AccountNumber))
                {
                    logDetails += " Acct: " + e.Execution.AccountNumber;
                }

                if (openOrder != null)
                {
                    Log(openOrder, "ExecDetails", logDetails);
                }
                else
                {
                    Log(symbol.ToString(), e.OrderId.ToString(), null, "ExecDetails: untracked order", logDetails);
                }

                bool bIgnore = false;

                if (!string.IsNullOrEmpty(_settings.AccountCode) && e.Execution.AccountNumber != _settings.AccountCode)
                {
                    // Now check to see if the account code is contained in one of the groups.
                    if (faAccountGroups.ContainsKey(_settings.AccountCode))
                    {
                        List<string> accounts = faAccountGroups[_settings.AccountCode];
                        if (accounts != null && accounts.Count > 0)
                        {
                            if (!accounts.Contains(e.Execution.AccountNumber))
                            {
                                bIgnore = false;
                            }
                        }
                    }
                    else
                    {
                        //Trace.WriteLine("### Execution ignored - Wrong Account");
                        Log(openOrder, "ExecDetails", "### Execution ignored - Wrong Account");
                        bIgnore = true;
                    }
                }
                else if (e.Execution.Shares < 0)
                {
                    //Trace.WriteLine("### Execution Ignored - Negative Fill");
                    Log(openOrder, "ExecDetails", "### Execution Ignored - Negative Fill");
                    bIgnore = true;
                }

                if (!bIgnore)
                {
                    DateTimeFormatInfo dateFormat = new DateTimeFormatInfo();
                    dateFormat.SetAllDateTimePatterns(new[] { "yyyyMMdd" }, 'd');
                    dateFormat.SetAllDateTimePatterns(new[] { "HH:mm:ss" }, 't');

                    string[] dateSplit = e.Execution.Time.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    DateTime execDateTime = DateTime.ParseExact(dateSplit[0], "d", dateFormat).Date;
                    TimeSpan execTime = DateTime.ParseExact(dateSplit[1], "t", dateFormat).TimeOfDay;
                    execDateTime += execTime;

                    //BrokerOrder openOrder;

                    //if (openOrders.TryGetValue(e.OrderId.ToString(), out openOrder))
                    if (openOrder != null)
                    {
                        bool alreadyReported = false;

                        string information = "";

                        Fill fill = new Fill();
                        fill.FillDateTime = execDateTime;
                        fill.Price = new Price(e.Execution.Price, e.Execution.Price);
                        fill.Quantity = e.Execution.Shares;

                        //	Apparently IB doesn't send commissions
                        fill.Commission = 0;

                        if (_gettingReconnectData)
                        {
                            //	If we are reconnecting after a disconnect, we may get execDetails for partial fills that
                            //	we already know about.  So check if we should actually send this fill
                            List<Fill> knownFills;
                            if (!_knownFills.TryGetValue(openOrder.OrderId, out knownFills))
                            {
                                Log(openOrder, "Fill reconciliation", "Unexpected order");
                                knownFills = new List<Fill>();
                            }

                            int newIndex = openOrder.Fills.Count;
                            if (newIndex < knownFills.Count)
                            {
                                alreadyReported = true;
                                //	This fill should already have been recorded, check to see if it matches what was expected
                                if (fill.FillDateTime != knownFills[newIndex].FillDateTime ||
                                    fill.Price.SymbolPrice != knownFills[newIndex].Price.SymbolPrice ||
                                    fill.Quantity != knownFills[newIndex].Quantity)
                                {
                                    Log(openOrder, "Fill reconciliation", "ERROR: Fill " + newIndex + " didn't match.  Expected: " + knownFills[newIndex] + " Actual: " + fill);
                                }
                            }

                        }

                        if (!alreadyReported)
                        {
                            openOrder.Fills.Add(fill);

                            long totalFilled = 0;
                            foreach (Fill f in openOrder.Fills)
                            {
                                totalFilled += f.Quantity;
                            }
                            if (totalFilled < openOrder.Shares)
                            {
                                openOrder.OrderState = BrokerOrderState.PartiallyFilled;
                                information = "Partial fill";
                                Log(openOrder, "ExecDetails", "Partial fill: total " + totalFilled + "/" + openOrder.Shares);
                            }
                            else
                            {
                                openOrder.OrderState = BrokerOrderState.Filled;
                                openOrders.Remove(openOrder.OrderId);

                                //	Only remove from the potentially cancelled orders if it was completely filled
                                //	It may have been partially filled while disconnected and then cancelled
                                if (_gettingReconnectData)
                                {
                                    if (_potentiallyCancelledOrders.ContainsKey(openOrder.OrderId))
                                    {
                                        _potentiallyCancelledOrders.Remove(openOrder.OrderId);
                                    }
                                }

                            }

                            //var callback = OutOfBandCallback;
                            //if (callback != null)
                            //{
                            //    callback(this, EventArgs.Empty);
                            //}

                            SendOrderUpdate(openOrder, fill, information, fill.FillDateTime);
                        }
                    }
                }
            }
        }

        void client_OpenOrderEnd(object sender, EventArgs e)
        {
            Log("OpenOrderEnd", "");
        }

        void client_ExecutionDataEnd(object sender, ExecutionDataEndEventArgs e)
        {
            Log("ExecutionDataEnd", "Request ID: " + e.RequestId);
        }

        void client_Error(object sender, Krs.Ats.IBNet.ErrorEventArgs e)
        {
            lock (_lockObject)
            {
                try
                {
                    //  Error code reference: http://institutions.interactivebrokers.com/php/apiguide/interoperability/socket_client_c++/errors.htm
                    int errorCode = (int)e.ErrorCode;

                    if (errorCode == 165)
                    {
                        return;
                    }

                    //string errorText = "IB error/warning! id=" + e.TickerId + "  " + e.ErrorCode + ": " + e.ErrorMsg;
                    string errorText = errorCode + ": " + e.ErrorMsg;

                    //if (errorCode == 2106 && _histRetrieval != null && !_histRetrieval.ReceivedData)
                    //{
                    //    System.Diagnostics.Trace.WriteLine("Historical data available.  Resending historical data request...");
                    //    _histRetrieval.SendRequest();
                    //}

                    if (errorCode == 2107 && _histRetrieval != null && _histRetrieval.bPaused)
                    {
                        //	Resume historical data collection
                        System.Diagnostics.Trace.WriteLine("Historical data available.  Resuming data collection...");
                        _histRetrieval.bPaused = false;
                        _histRetrieval.SendRequest();
                    }
                    else if (errorCode == 200)
                    {
                        //	No security definition has been found for the request
                        Symbol symbol = idToSymbol(e.TickerId);
                        if (symbol != null)
                        {
                            errorText = errorText + " Symbol: " + symbol.ToString();
                        }
                    }


                    RightEdge.Common.BrokerOrder order;
                    if (!openOrders.TryGetValue(e.TickerId.ToString(), out order))
                    {
                        Log(null, e.TickerId.ToString(), null, "IB error/warning", errorText);
                    }
                    else
                    {
                        Log(order, "IB error/warning", errorText);

                        string message = string.Format("IB error/warning {0}", errorText);
                        bool bError = true;

                        if (errorCode >= 2100 && errorCode <= 3000)
                        {
                            //	Error code 2109 can happen often and is apparently just noise.
                            //	The error text for error 2109 is: Order Event Warning: Attribute �Outside Regular Trading Hours� is ignored based on the order type and destination. PlaceOrder is now processed
                            if (errorCode != 2109)
                            {
                                //	It's probably just a warning, and the order may continue
                                Trace.WriteLine("IB Warning code " + errorCode + " for order ID " + e.TickerId + ": " + e.ErrorMsg);
                            }
                            bError = false;
                        }
                        else if (errorCode == 404)
                        {
                            //	404: Shares for this order are not immediately available for short sale. The order will be held while we attempt to locate the shares.
                            bError = false;
                        }
                        else if (errorCode == 399)
                        {
                            //	Order Message:\nWarning: your order will not be placed at the exchange until 2010-04-22 09:30:00 US/Eastern
                            if (e.ErrorMsg.StartsWith("Order Message:\nWarning: your order will not be placed at the exchange until"))
                            {
                                bError = false;
                            }
                            //  399: Order Message:\nWarning: Your order size is below the EUR 20000 IdealPro minimum and will be routed as an odd lot order.
                            else if (e.ErrorMsg.Contains("Warning: Your order size is below the") &&
                                e.ErrorMsg.Contains("minimum and will be routed as an odd lot order"))
                            {
                                bError = false;
                            }

                        }

                        //string message = string.Format("Error code {0}: {1}", errorCode, e.ErrorMsg);

                        // error code 202 is a cancelled order ... we want to know about these!
                        if (errorCode == 202)
                        {
                            order.OrderState = BrokerOrderState.Cancelled;
                        }
                        else if (bError)
                        {
                            order.OrderState = BrokerOrderState.Rejected;
                        }

                        SendOrderUpdate(order, null, message, GetAccountTime("client_Error"));

                        return;
                    }

                    errorText = "IB error/warning! id=" + e.TickerId + "  " + errorText;

                    System.Diagnostics.Trace.WriteLine(errorText);

                    if (errorCode == 1100)		//	Connectivity has been lost
                    {
                        _watching = false;
                        _connected = false;
                        errorText = "Disconnected: " + errorText;

                        var args = new ServiceEventArgs();
                        args.EventType = ServiceEventType.Disconnected;
                        args.SuppressAutoReconnect = true;
                        args.Message = errorText;
                        RaiseServiceEvent(args);
                    }
                    else if (errorCode == 1101 || errorCode == 1102)
                    {
                        //  1101: Connectivity restored - data lost
                        //  1102: Connectivity restored - data mantained
                        //  If TWS has reconnected, call Connect() so that we will request and process order updates that happened while TWS was disconnected
                        _connected = true;
                        Log("Reconnected", "Sending service event to notify of reconnection and to ask for SyncAccountState to be called");

                        var args = new ServiceEventArgs();
                        args.EventType = ServiceEventType.Reconnected;
                        args.ShouldSyncAccountState = true;
                        args.Message = errorText;
                        RaiseServiceEvent(args);
                    }

                    if (errorCode < 2000 && errorCode != 202)
                    {
                        if (_histRetrieval != null)
                        {
                            //	Currently retrieving historical data
                            if (errorCode == 162 && e.ErrorMsg.Contains("Historical data request pacing violation"))
                            {
                                _histRetrieval.bPaused = true;
                                System.Diagnostics.Trace.WriteLine("Historical data pacing violation.  Waiting for data to become available...");
                                _histRetrieval.waitEvent.Set();
                                //	Need to wait for this:
                                //	Error! id=-1 errorCode=2107
                                //	HMDS data farm connection is inactive but should be available upon demand.:ushmds2a
                            }
                            else if ((errorCode == 321 && e.ErrorMsg.Contains("Historical data queries on this contract requesting any data earlier than")) ||
                                    (errorCode == 162 && e.ErrorMsg.Contains("query returned no data")))
                            {
                                //	Error code 321
                                //	Error validating request:-'qb' : cause - Historical data queries on this contract requesting any data earlier than one year back from now which is 20060218 12:34:47 EST are rejected.  Your query would have run from 20060214 00:00:00 EST to 20060221 00:00:00 EST.

                                //	Error! id=34 errorCode=162
                                //	Historical Market Data Service error message:HMDS query returned no data: ESU8@GLOBEX Trades

                                //	We will not treat this as an error.  We will simply return the data that we could get.

                                _histRetrieval.Done = true;
                                _histRetrieval.waitEvent.Set();
                            }
                            else
                            {

                                System.Diagnostics.Trace.WriteLine("Error ended historical data retrieval: " + errorText);
                                lastError = errorText;
                                hadError = true;
                                _histRetrieval.waitEvent.Set();
                            }
                        }
                        else if (errorCode == 200)
                        {
                            //	No security definition has been found for the request
                            hadError = false;
                        }
                        else
                        {
                            lastError = errorText;
                            hadError = true;
                        }
                    }

                }
                catch (Exception ex)
                {
                    RightEdge.Common.Internal.TraceHelper.DumpExceptionToTrace(ex);
                }
            }
        }

        private void RaiseServiceEvent(ServiceEventArgs args)
        {
            var eventHandler = ServiceEvent;
            if (eventHandler != null)
            {
                eventHandler(this, args);
            }
        }

        void client_NextValidId(object sender, NextValidIdEventArgs e)
        {
            lock (_lockObject)
            {
                nextOrderId = e.OrderId;
            }
        }

        private DateTime _waitHandleTime;

        void client_CurrentTime(object sender, CurrentTimeEventArgs e)
        {
            lock (_lockObject)
            {
                DateTime accountTime = e.Time.ToLocalTime();
                accountTimeDiff = accountTime.Subtract(DateTime.Now);

                bGotAccountTime = true;

                //System.Diagnostics.Trace.WriteLine("Account time updated: " + accountTime.ToString() + "  Current time: " + DateTime.Now +
                //    " Diff: " + accountTimeDiff.ToString());
                //Console.WriteLine("IB Current Time: " + accountTime.ToString());

                Log(null, null, null, "CurrentTime", "Account time updated: " + accountTime.ToString() + "  Current time: " + DateTime.Now +
                    " Diff: " + accountTimeDiff.ToString());

                if (_gettingReconnectData)
                {
                    Log("CurrentTime", "Current time update recieved, ending connect sync process");
                    _waitHandleTime = DateTime.Now;
                    _connectWaitHandle.Set();
                }
            }
        }

        void SendOrderUpdate(BrokerOrder order, Fill fill, string information, DateTime currentTime)
        {
            if (order.OrderState == BrokerOrderState.Filled ||
                order.OrderState == BrokerOrderState.Cancelled ||
                order.OrderState == BrokerOrderState.Rejected)
            {
                _orderHistory.RecordOrder(order.OrderId, currentTime, order.OrderState);
            }

            OrderUpdatedDelegate handler = OrderUpdated;
            if (handler != null)
            {
                handler(order, fill, information);
            }
        }

        #region IService Members

        public string ServiceName()
        {
            return "Interactive Brokers Plugin";
        }

        public string Author()
        {
            return "Yye Software";
        }

        public string Description()
        {
            return "Retrieves data and provides broker functions for Interactive Brokers.  " +
                "This is a pure .NET plugin and does not require the IB API to be installed.";
        }

        public string CompanyName()
        {
            return "Yye Software";
        }

        public string Version()
        {
            return "1.0";
        }

        public string id()
        {
            return "{B03427B2-5405-4686-A922-F888836C19BC}";
        }

        public bool NeedsServerAddress()
        {
            return true;
        }

        public bool NeedsPort()
        {
            return true;
        }

        public bool NeedsAuthentication()
        {
            return false;
        }

        public bool SupportsMultipleInstances()
        {
            return true;
        }

        public string ServerAddress
        {
            get;
            set;
        }

        public int Port
        {
            get;
            set;
        }

        public string UserName
        {
            get
            {
                return "";
            }
            set
            {

            }
        }

        public string Password
        {
            get
            {
                return "";
            }
            set
            {

            }
        }

        public bool BarDataAvailable
        {
            get { return true; }
        }

        public bool TickDataAvailable
        {
            get { return true; }
        }

        public bool BrokerFunctionsAvailable
        {
            get { return true; }
        }

        public IBarDataRetrieval GetBarDataInterface()
        {
            return this;
        }

        public ITickRetrieval GetTickDataInterface()
        {
            return this;
        }

        public IBroker GetBrokerInterface()
        {
            return this;
        }

        public bool HasCustomSettings()
        {
            return true;
        }

        public bool ShowCustomSettingsForm(ref SerializableDictionary<string, string> settings)
        {
            TWSSettings dlg = new TWSSettings();

            dlg.Settings = _settings.Clone();
            dlg.Settings.LoadFrom(settings);


            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                dlg.Settings.SaveTo(settings);
            }

            return true;
        }



        public bool Initialize(SerializableDictionary<string, string> settings)
        {
            _settings.LoadFrom(settings);

            if (_settings.EnableLogging)
            {
                LoggingConfiguration config = new LoggingConfiguration();


                FileTarget fileTarget = new FileTarget();
                //fileTarget.FileName = Path.Combine(_settings.LogPath, "RightEdgeTWSPluginLog${shortdate}.txt");
                fileTarget.FileName = Path.Combine(_settings.LogPath, "RightEdgeTWSPluginLog${date:format=yyyy-MM-dd}.txt");
                fileTarget.Layout = "${longdate}\t${message}";
                //fileTarget.ArchiveEvery = FileArchivePeriod.Minute;
                //fileTarget.MaxArchiveFiles = 60;
                //fileTarget.MaxArchiveFiles = 3;
                //fileTarget.ArchiveFileName = fileTarget.FileName;
                //fileTarget.ArchiveFileName = Path.Combine(_settings.LogPath, "ArchiveRightEdgeTWSPluginLog${date:format=yyyy-MM-dd HHmm}.txt");


                config.AddTarget("TWSfile", fileTarget);

                var asyncTargetWrapper = new AsyncTargetWrapper(fileTarget);
                config.AddTarget("TWSasyncFile", asyncTargetWrapper);

                TraceTarget traceTarget = new TraceTarget();
                traceTarget.Layout = fileTarget.Layout;

                config.AddTarget("TWStrace", traceTarget);

                var rule = new LoggingRule(typeof(TWSPlugin).FullName, NLog.LogLevel.Trace, asyncTargetWrapper);
                rule.Targets.Add(traceTarget);

                config.LoggingRules.Add(rule);

                LogManager.Configuration = config;

                _logger = LogManager.GetLogger(typeof(TWSPlugin).FullName);


                if (!string.IsNullOrEmpty(_settings.LogPath) && !Directory.Exists(_settings.LogPath))
                {
                    Directory.CreateDirectory(_settings.LogPath);
                }
            }

            return true;
        }

        public bool Connect(ServiceConnectOptions connectOptions)
        {
            ClearError();

            _connectOptions = connectOptions;

            if ((_connectOptions & ServiceConnectOptions.Broker) == ServiceConnectOptions.Broker)
            {
                CleanupLogs();
            }

            if (_connected)
                return true;

            foreach (Symbol symbol in new List<Symbol>(watchedSymbols.Keys))
            {
                watchedSymbols[symbol] = null;
            }

            if (client == null)
            {
                client = new IBClient();
                client.ThrowExceptions = true;

                client.Error += client_Error;
                client.TickPrice += client_TickPrice;
                client.TickSize += client_TickSize;
                client.UpdateAccountValue += client_UpdateAccountValue;
                client.UpdatePortfolio += client_UpdatePortfolio;
                client.OrderStatus += client_OrderStatus;
                client.ExecDetails += client_ExecDetails;
                client.NextValidId += client_NextValidId;
                client.CurrentTime += client_CurrentTime;
                client.ReceiveFA += new EventHandler<ReceiveFAEventArgs>(client_ReceiveFA);
                client.OpenOrderEnd += new EventHandler<EventArgs>(client_OpenOrderEnd);
                client.ExecutionDataEnd += new EventHandler<ExecutionDataEndEventArgs>(client_ExecutionDataEnd);
            }

            int clientID = -1;

            if ((connectOptions & ServiceConnectOptions.Broker) == ServiceConnectOptions.Broker)
            {
                clientID = _settings.ClientIDBroker;
            }
            else if ((connectOptions & ServiceConnectOptions.LiveData) == ServiceConnectOptions.LiveData)
            {
                clientID = _settings.ClientIDLiveData;
            }
            else if ((connectOptions & ServiceConnectOptions.HistoricalData) == ServiceConnectOptions.HistoricalData)
            {
                clientID = _settings.ClientIDHist;
            }
            if (clientID < 0)
            {
                clientID = new Random().Next();
            }

            Log("Symbol", "Order", "Position", "Event", "Details");
            Log("Connect", "Connecting...");
            client.Connect(string.IsNullOrEmpty(ServerAddress) ? "127.0.0.1" : ServerAddress, (Port == 0) ? 7496 : Port, clientID);
            Log("Connect", "Connected to TWS");
            lock (_lockObject)
            {
                _connected = true;
            }

            Log("Connect", "Done with connect process");

            return true;
        }

        public void SyncAccountState()
        {
            Log("Connect", "SyncAccountState() called");
            if (!_connected)
            {
                throw new RightEdgeError("Not connected.");
            }

            lock (_lockObject)
            {
                _connectWaitHandle = new ManualResetEvent(false);
                _gettingReconnectData = true;
                foreach (string id in openOrders.Keys)
                {
                    _potentiallyCancelledOrders[id] = null;
                }

                //	TWS should send us all existing fills for any open orders.  So that we don't get duplicate fills, we store a copy of the fills we knew about,
                //	and then clear the fills for all orders.  When we get a fill while reconnecting we only send the update if it's a new one.
                _knownFills = openOrders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Fills.Select(f => f.Clone()).ToList());

                foreach (var order in openOrders.Values)
                {
                    order.Fills.Clear();
                }
            }

            client.RequestAccountUpdates(true, _settings.AccountCode);
            //client.ReqAllOpenOrders();
            client.RequestOpenOrders();
            client.RequestFA(FADataType.Groups);

            ExecutionFilter filter = new ExecutionFilter();
            filter.ClientId = _settings.ClientIDBroker;
            Log("RequestExecutions", "Requesting buy executions, requestID = " + nextID);
            filter.Side = ActionSide.Buy;
            client.RequestExecutions(nextID++, filter);
            Log("RequestExecutions", "Requesting sell executions, requestID = " + nextID);
            filter.Side = ActionSide.Sell;
            client.RequestExecutions(nextID++, filter);

            //	Request the current time so that when we get it, we know that (hopefully)
            //	we have gotten all the results from ReqOpenOrders and ReqExecutions
            client.RequestCurrentTime();

            if (!_connectWaitHandle.WaitOne(TimeSpan.FromSeconds(10.0), true))
            {
                string msg = "SyncAccountState timed out waiting for TWS order and execution data to finish.";
                //Trace.WriteLine(msg);
                //Console.WriteLine(msg);
                Log("Connect", msg);
            }

            lock (_lockObject)
            {
                _gettingReconnectData = false;

                foreach (string orderID in _potentiallyCancelledOrders.Keys)
                {
                    BrokerOrder order;

                    if (openOrders.TryGetValue(orderID, out order))
                    {
                        Log(order, "Cancelled", "Order presumed cancelled while disconnected");

                        order.OrderState = BrokerOrderState.Cancelled;
                        SendOrderUpdate(order, null, "Order cancelled while disconnected.", GetAccountTime("Order cancelled"));
                        openOrders.Remove(orderID);
                    }
                    else
                    {
                        int b = 0;
                    }
                }
                _potentiallyCancelledOrders.Clear();
            }

            Log("Connect", "SyncAccountState ended");
        }

        void client_ReceiveFA(object sender, ReceiveFAEventArgs e)
        {
            switch (e.FADataType)
            {
                case FADataType.Groups:
                    var xml = XElement.Parse(e.Xml);
                    var query = from x in xml.Elements()
                                select x.Descendants("name");

                    foreach (var item in query)
                    {
                        string acctGroup = item.FirstOrDefault().Value;
                        var accountList = from a in xml.Elements()
                                          where (string)a.Element("name").Value == acctGroup
                                          select a.Descendants("ListOfAccts").Elements();
                        foreach (var accounts in accountList)
                        {
                            foreach (var account in accounts)
                            {
                                string acct = account.Value;
                                if (faAccountGroups.ContainsKey(acctGroup))
                                {
                                    faAccountGroups[acctGroup].Add(acct);
                                }
                                else
                                {
                                    faAccountGroups.Add(acctGroup, new List<string>() { acct });
                                }
                            }
                        }
                    }

                    break;
            }
        }

        public bool Disconnect()
        {
            Log("Disconnect", "Disconnect() called");
            if (client != null)
            {
                client.Error -= client_Error;
                client.TickPrice -= client_TickPrice;
                client.TickSize -= client_TickSize;
                client.UpdateAccountValue -= client_UpdateAccountValue;
                client.UpdatePortfolio -= client_UpdatePortfolio;
                client.OrderStatus -= client_OrderStatus;
                client.ExecDetails -= client_ExecDetails;
                client.NextValidId -= client_NextValidId;
                client.CurrentTime -= client_CurrentTime;

                client.Disconnect();
            }
            client = null;

            lock (_lockObject)
            {
                watchedSymbols.Clear();
                _watching = false;

                if (!_connected)
                {
                    lastError = "Not connected.";
                    hadError = true;
                    return false;
                }
                _connected = false;
                ClearError();
                return true;
            }
        }

        public string GetError()
        {
            lock (_lockObject)
            {
                return lastError;
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            //	TODO: ???
            Disconnect();
        }

        #endregion

        #region ITickRetrieval Members

        public bool RealTimeDataAvailable
        {
            get { return true; }
        }

        public GotTickData TickListener
        {
            set
            {
                lock (_lockObject)
                {
                    tickListener = value;
                }
            }
        }

        public bool SetWatchedSymbols(List<Symbol> symbols)
        {
            bool bNeedsExit = false;
            try
            {
                Monitor.Enter(_lockObject);
                bNeedsExit = true;
                foreach (Symbol symbol in symbols)
                {
                    if (!watchedSymbols.ContainsKey(symbol))
                    {
                        watchedSymbols[symbol] = null;
                    }
                }
                foreach (Symbol symbol in new List<Symbol>(watchedSymbols.Keys))
                {
                    if (!symbols.Contains(symbol))
                    {
                        if (_watching && watchedSymbols[symbol].HasValue)
                        {
                            int tickerToCancel = watchedSymbols[symbol].Value;

                            Monitor.Exit(_lockObject);
                            bNeedsExit = false;
                            client.CancelMarketData(tickerToCancel);
                            Monitor.Enter(_lockObject);
                            bNeedsExit = true;
                        }
                        watchedSymbols.Remove(symbol);
                        forgetData(symbol);
                    }
                }

                //	Check error here because StartWatching() will clear error status
                if (!CheckError())
                {
                    return false;
                }
            }
            finally
            {
                if (bNeedsExit)
                {
                    Monitor.Exit(_lockObject);
                }
            }

            if (_watching)
            {
                StartWatching();
            }
            lock (_lockObject)
            {
                return CheckError();
            }

        }

        public bool IsWatching()
        {
            lock (_lockObject)
            {
                return _watching;
            }
        }

        public bool StartWatching()
        {
            bool bNeedsExit = false;
            try
            {
                Monitor.Enter(_lockObject);
                bNeedsExit = true;

                ClearError();
                foreach (Symbol symbol in new List<Symbol>(watchedSymbols.Keys))
                {
                    if (watchedSymbols[symbol] == null)
                    {
                        int id = nextID++;
                        TWSAssetArgs args = TWSAssetArgs.Create(symbol);
                        //Contract contract = new Contract(args.Symbol, args.SecType, args.Expiry, args.Strike,
                        //    args.Right, args.Multiplier, args.Exchange, args.Currency, "", args.PrimaryExchange);
                        Contract contract = args.ToContract();

                        Monitor.Exit(_lockObject);
                        bNeedsExit = false;
                        client.RequestMarketData(id, contract, null, false);
                        Monitor.Enter(_lockObject);
                        bNeedsExit = true;

                        watchedSymbols[symbol] = id;
                    }
                }

                if (CheckError())
                {
                    _watching = true;
                }
            }
            finally
            {
                if (bNeedsExit)
                {
                    Monitor.Exit(_lockObject);
                }
            }

            client.RequestCurrentTime();

            // Request the next valid ID for an order
            client.RequestIds(1);

            lock (_lockObject)
            {
                return CheckError();
            }

        }

        public bool StopWatching()
        {
            bool bNeedsExit = false;
            try
            {
                Monitor.Enter(_lockObject);
                bNeedsExit = true;

                ClearError();
                _watching = false;
                foreach (Symbol symbol in new List<Symbol>(watchedSymbols.Keys))
                {
                    if (watchedSymbols[symbol] != null)
                    {
                        int idToCancel = watchedSymbols[symbol].Value;

                        Monitor.Exit(_lockObject);
                        bNeedsExit = false;
                        client.CancelMarketData(idToCancel);
                        Monitor.Enter(_lockObject);
                        bNeedsExit = true;

                        watchedSymbols[symbol] = null;
                        forgetData(symbol);
                    }
                }
                return true;
            }
            finally
            {
                if (bNeedsExit)
                {
                    Monitor.Exit(_lockObject);
                }
            }
        }

        public IService GetService()
        {
            return this;
        }

        #endregion

        private void forgetData(Symbol symbol)
        {
            if (lastPrices.ContainsKey(symbol))
            {
                lastPrices.Remove(symbol);
            }
            if (lastVolumes.ContainsKey(symbol))
            {
                lastVolumes.Remove(symbol);
            }
            if (lastBidPrices.ContainsKey(symbol))
            {
                lastBidPrices.Remove(symbol);
            }
            if (lastBidSizes.ContainsKey(symbol))
            {
                lastBidSizes.Remove(symbol);
            }
            if (lastAskPrices.ContainsKey(symbol))
            {
                lastAskPrices.Remove(symbol);
            }
            if (lastAskSizes.ContainsKey(symbol))
            {
                lastAskSizes.Remove(symbol);
            }

        }

        public List<BarData> RetrieveData(Symbol symbol, int frequency, DateTime startDate, DateTime endDate, BarConstructionType barConstruction)
        {
            if (_histRetrieval != null)
            {
                lastError = "Historical data retrieval already in progress.";
                return null;
            }

            if (Thread.CurrentThread.Name == null)
            {
                Thread.CurrentThread.Name = "RetrieveData thread";
            }
            Trace.WriteLine("RetrieveData: " + startDate.ToString() + "-" + endDate.ToString() + " Thread: " +
                Thread.CurrentThread.ManagedThreadId.ToString("x"));

            ClearError();
            if (!_connected)
            {
                if (!Connect(ServiceConnectOptions.HistoricalData))
                {
                    return null;
                }
            }

            EventHandler<HistoricalDataEventArgs> handler = null;
            try
            {
                BarSize barSize;
                //string TWSFreq;
                //	Legal ones are: 1 secs, 5 secs, 15 secs, 30 secs, 1 min, 2 mins, 3 mins, 5 mins, 15 mins, 30 mins, 1 hour, 1 day, 1 week, 1 month, 3 months, 1 year
                if (frequency == (int)BarFrequency.OneMinute)
                {
                    //TWSFreq = "1 min";
                    barSize = BarSize.OneMinute;
                }
                else if (frequency == 2)
                {
                    //TWSFreq = "2 mins";
                    barSize = BarSize.TwoMinutes;
                }
                else if (frequency == 3)
                {
                    //TWSFreq = "3 mins";
                    barSize = BarSize.ThreeMinutes;
                }
                else if (frequency == (int)BarFrequency.FiveMinute)
                {
                    //TWSFreq = "5 mins";
                    barSize = BarSize.FiveMinutes;
                }
                else if (frequency == (int)BarFrequency.FifteenMinute)
                {
                    //TWSFreq = "15 mins";
                    barSize = BarSize.FifteenMinutes;
                }
                else if (frequency == (int)BarFrequency.ThirtyMinute)
                {
                    //TWSFreq = "30 mins";
                    barSize = BarSize.ThirtyMinutes;
                }
                else if (frequency == (int)BarFrequency.SixtyMinute)
                {
                    //TWSFreq = "1 hour";
                    barSize = BarSize.OneHour;
                }
                else if (frequency == (int)BarFrequency.Daily)
                {
                    //TWSFreq = "1 day";
                    barSize = BarSize.OneDay;
                }
                else if (frequency == (int)BarFrequency.Weekly)
                {
                    //TWSFreq = "1 week";
                    barSize = BarSize.OneWeek;
                }
                else if (frequency == (int)BarFrequency.Monthly)
                {
                    //TWSFreq = "1 month";
                    barSize = BarSize.OneMonth;
                }
                else if (frequency == (int)BarFrequency.Yearly)
                {
                    //TWSFreq = "1 year";
                    barSize = BarSize.OneYear;
                }
                else
                {
                    lastError = "Frequency not supported for historical data retrieval.";
                    return null;
                }

                DateTime accountTime = GetAccountTime("RetrieveData call");
                if (endDate > accountTime || endDate == DateTime.MinValue)
                {
                    endDate = accountTime.AddHours(12);
                }

                _histRetrieval = new HistRetrieval();
                _histRetrieval.client = client;
                _histRetrieval.twsPlugin = this;
                _histRetrieval.symbol = symbol;
                _histRetrieval.barSize = barSize;
                _histRetrieval.startDate = startDate;
                _histRetrieval.endDate = endDate;
                _histRetrieval.RTHOnly = _settings.UseRTH;
                _histRetrieval.BarConstruction = barConstruction;

                _histRetrieval.waitEvent = new ManualResetEvent(false);

                //if (_firstHistRequest)
                //{
                //    //	The first request for historical data seems to fail.  So we will submit a small request first and then wait a bit
                //    _histRetrieval.barSize = BarSize.OneDay;
                //    _histRetrieval.Duration = new TimeSpan(5, 0, 0, 0);
                //    _histRetrieval.SendRequest();

                //    Trace.WriteLine("TWS Plugin sleeping...");
                //    Thread.Sleep(2500);
                //    Trace.WriteLine("TWS Plugin done sleeping.");

                //    if (hadError)
                //    {
                //        return null;
                //    }

                //    _firstHistRequest = false;
                //}

                _histRetrieval.barSize = barSize;


                //	Requesting a duration which will return more than 2000 bars gives an "invalid step" error message
                //	See http://www.interactivebrokers.com/cgi-bin/discus/board-auth.pl?file=/2/39164.html
                //	So we need to find the largest duration which will return less than 2000 bars

                //	Since the trading day is from 9:30 to 4:00, there are 6.5 hours, or 390 minutes per trading day
                //	This means that 5 days will have a bit less than 2000 bars.

                if (frequency < (int)BarFrequency.Daily)
                {
                    _histRetrieval.Duration = new TimeSpan(5, 0, 0, 0);
                }
                else
                {
                    //_histRetrieval.Duration = new TimeSpan(7, 0, 0, 0);
                    //	Can only request up to 52 weeks at once
                    //	Requesting data that is more than a year old will reject the whole request
                    _histRetrieval.Duration = new TimeSpan(360, 0, 0, 0);
                }



                handler = new EventHandler<HistoricalDataEventArgs>(
                    delegate(object sender, HistoricalDataEventArgs args)
                    {
                        _histRetrieval.GotData(args);
                    });

                client.HistoricalData += handler;

                int pacingPause = 1000;

                while (!_histRetrieval.Done)
                {
                    _histRetrieval.waitEvent.Reset();
                    _histRetrieval.SendRequest();
                    _histRetrieval.waitEvent.WaitOne();
                    if (!CheckError())
                    {
                        return null;
                    }
                    if (_histRetrieval.bPaused)
                    {
                        Trace.WriteLine("Waiting " + pacingPause + " ms.");
                        Thread.Sleep(pacingPause);
                        Trace.WriteLine("Attempting to resume data collection.");
                        _histRetrieval.bPaused = false;

                        if (pacingPause < 30000)
                        {
                            pacingPause *= 2;
                        }
                    }
                    else
                    {
                        pacingPause = 1000;
                    }
                }

                if (!CheckError())
                    return null;

                _histRetrieval.ret.Reverse();
                if (_settings.IgnoreLastHistBar && _histRetrieval.ret.Count > 0)
                {
                    _histRetrieval.ret.RemoveAt(_histRetrieval.ret.Count - 1);
                }

                return _histRetrieval.ret;
            }
            finally
            {
                if (handler != null)
                {
                    client.HistoricalData -= handler;
                }
                _histRetrieval = null;
            }
        }

        public void SetAccountState(BrokerAccountState state)
        {
            lock (_lockObject)
            {
                foreach (BrokerOrder order in state.PendingOrders)
                {
                    openOrders.Add(order.OrderId, order);
                }
            }
        }

        private double RoundPrice(Symbol symbol, double price)
        {
            if (symbol.SymbolInformation.TickSize > 0)
            {
                // If a tick size is specified, round to this value.
                return SystemUtils.RoundToNearestTick(price, symbol.SymbolInformation.TickSize);
            }
            else
            {
                // Otherwise, use decimal places specified in symbol setup.
                double multiplier = Math.Pow(10, symbol.SymbolInformation.DecimalPlaces);
                price = Math.Round(price * multiplier);
                price /= multiplier;
                return price;
            }
        }

        public bool SubmitOrder(RightEdge.Common.BrokerOrder order, out string orderId)
        {
            Krs.Ats.IBNet.Order apiOrder = new Krs.Ats.IBNet.Order();
            Contract contract;
            int intOrderId;



            lock (_lockObject)
            {
                // Before we submit the order, we need to make sure the price is trimmed
                // to something that IB will accept.  In other words, if a price is submitted
                // for 40.1032988923, this will be get rejected.

                order.LimitPrice = RoundPrice(order.OrderSymbol, order.LimitPrice);
                order.StopPrice = RoundPrice(order.OrderSymbol, order.StopPrice);

                contract = TWSAssetArgs.Create(order.OrderSymbol).ToContract();

                if (order.TransactionType == TransactionType.Buy ||
                    order.TransactionType == TransactionType.Cover)
                {
                    apiOrder.Action = ActionSide.Buy;
                }
                else if (order.TransactionType == TransactionType.Sell)
                {
                    apiOrder.Action = ActionSide.Sell;
                }
                else if (order.TransactionType == TransactionType.Short)
                {
                    //	SShort is apparently only used as part of a combo leg, and you get an "Invalid side" error if you try to use it otherwise
                    //apiOrder.Action = ActionSide.SShort;
                    apiOrder.Action = ActionSide.Sell;
                }
                else
                {
                    throw new RightEdgeError("Cannot submit order with transaction type " + order.TransactionType.ToString());
                }

                double limitPrice = 0.0;
                double auxPrice = 0.0;

                switch (order.OrderType)
                {
                    case RightEdge.Common.OrderType.Limit:
                        apiOrder.OrderType = Krs.Ats.IBNet.OrderType.Limit;
                        limitPrice = order.LimitPrice;
                        break;

                    case RightEdge.Common.OrderType.LimitOnClose:
                        apiOrder.OrderType = Krs.Ats.IBNet.OrderType.LimitOnClose;
                        limitPrice = order.LimitPrice;
                        break;

                    case RightEdge.Common.OrderType.Market:
                    case RightEdge.Common.OrderType.MarketOnOpen:
                        apiOrder.OrderType = Krs.Ats.IBNet.OrderType.Market;
                        break;

                    case RightEdge.Common.OrderType.MarketOnClose:
                        apiOrder.OrderType = Krs.Ats.IBNet.OrderType.MarketOnClose;
                        break;

                    case RightEdge.Common.OrderType.PeggedToMarket:
                        apiOrder.OrderType = Krs.Ats.IBNet.OrderType.PeggedToMarket;
                        break;

                    case RightEdge.Common.OrderType.Stop:
                        apiOrder.OrderType = Krs.Ats.IBNet.OrderType.Stop;
                        auxPrice = order.StopPrice;
                        break;

                    case RightEdge.Common.OrderType.StopLimit:
                        apiOrder.OrderType = Krs.Ats.IBNet.OrderType.StopLimit;
                        auxPrice = order.StopPrice;
                        limitPrice = order.LimitPrice;
                        break;

                    //	TODO: investigate and add support for trailing stop
                    case RightEdge.Common.OrderType.TrailingStop:
                        if (order.TrailingStopType != TargetPriceType.RelativePrice)
                        {
                            lastError = order.TrailingStopType.ToString() + " trailing stops not supported by IB.";
                            orderId = null;
                            return false;
                        }
                        apiOrder.OrderType = Krs.Ats.IBNet.OrderType.TrailingStop;
                        auxPrice = order.TrailingStop;
                        break;
                    default:
                        lastError = "Order type not supported by IB service: " + order.OrderType.ToString();
                        orderId = null;
                        return false;
                }

                if (double.IsNaN(limitPrice))
                {
                    throw new RightEdgeError("Limit price for order cannot be NaN");
                }
                if (double.IsNaN(auxPrice))
                {
                    throw new RightEdgeError("Stop price for order cannot be NaN");
                }

                apiOrder.LimitPrice = (Decimal)limitPrice;
                apiOrder.AuxPrice = (Decimal)auxPrice;

                if (order.GoodTillCanceled)
                {
                    //DateTime gtcDate = GetAccountTime("SubmitOrder").AddMonths(12);
                    //apiOrder.GoodTillDate = gtcDate.ToString("yyyyMMdd");
                    //apiOrder.GoodTillDate = "";
                    apiOrder.GoodTillDate = string.Empty;
                    apiOrder.Tif = TimeInForce.GoodTillCancel;
                }

                apiOrder.TotalQuantity = (int)order.Shares;
                apiOrder.FAGroup = _settings.AccountCode;
                apiOrder.FAMethod = _settings.FAMethod;
                apiOrder.FAPercentage = _settings.FAPercentage;
                apiOrder.FAProfile = _settings.FAProfile;
                //	TODOSOON: Verify that RTH still works after upgrading Krs library
                //if (_useRTH)
                //{
                //    apiOrder.IgnoreRth = true;
                //    apiOrder.RthOnly = true;
                //}
                apiOrder.OutsideRth = !_settings.UseRTH;

                orderId = nextOrderId.ToString();
                order.OrderId = orderId;
                openOrders.Add(orderId, order);

                unSubmittedOrders[order.OrderId] = true;

                intOrderId = nextOrderId;

                nextOrderId++;
            }

            client.PlaceOrder(intOrderId, contract, apiOrder);

            //Trace.WriteLine("IB Sent: " + order.ToString());

            Log(order, "SubmitOrder", order.ToString());

            return true;

        }

        private bool internalCancelOrder(int id, RightEdge.Common.BrokerOrder order)
        {
            using (Unlock(_lockObject))
            {
                client.CancelOrder(id);
            }
            Log(order, "CancelOrder", "Cancelling order");

            if (unSubmittedOrders.ContainsKey(order.OrderId))
            {
                Log(order, "CancelOrder", "Order cancelled when submission was not yet acknowledged by TWS");

                ////	Apparently IB will not send us a cancellation confirmation in this case
                //unSubmittedOrders.Remove(order.OrderId);
                //order.OrderState = BrokerOrderState.Cancelled;

                //openOrders.Remove(order.OrderId);

                //OrderUpdatedDelegate tmp = OrderUpdated;
                //if (tmp != null)
                //{
                //    tmp(order, null, "System Cancelled");
                //}
            }
            return true;
        }

        public bool CancelOrder(string orderId)
        {
            lock (_lockObject)
            {
                //	TODO: Perf issue here with many orders
                foreach (var kvp in openOrders)
                {
                    if (kvp.Value.OrderId == orderId)
                    {
                        int id;
                        if (!int.TryParse(orderId, out id))
                        {
                            lastError = "Unable to parse order id: " + orderId;
                            Log(null, orderId, null, "CancelOrder", lastError);
                            return false;
                        }

                        return internalCancelOrder(id, kvp.Value);
                    }
                }

                BrokerOrderState previousOrderState;
                if (_orderHistory.TryGetOrderState(orderId, out previousOrderState))
                {
                    if (previousOrderState == BrokerOrderState.Filled)
                    {
                        //	Order was already filled but the fill hasn't been processed by the system yet.
                        //	Return true to avoid error.
                        Log(null, orderId, null, "CancelOrder", "Cancellation requested for filled order.");
                    }
                    return true;
                }

                lastError = "Order not found: " + orderId;
                Log(null, orderId, null, "CancelOrder", lastError);
                return false;
            }
        }

        public bool CancelAllOrders()
        {
            lock (_lockObject)
            {
                foreach (var kvp in openOrders)
                {
                    int id;
                    if (int.TryParse(kvp.Key, out id))
                    {
                        internalCancelOrder(id, kvp.Value);
                    }
                }
                return true;
            }
        }

        public double GetBuyingPower()
        {
            lock (_lockObject)
            {
                return buyingPower;
            }
        }

        public double GetAccountBalance()
        {
            lock (_lockObject)
            {
                return cashBalance;
            }
        }

        public List<RightEdge.Common.BrokerOrder> GetOpenOrders()
        {
            lock (_lockObject)
            {
                return new List<RightEdge.Common.BrokerOrder>(openOrders.Values);
            }
        }

        public RightEdge.Common.BrokerOrder GetOpenOrder(string id)
        {
            RightEdge.Common.BrokerOrder ret;
            lock (_lockObject)
            {
                if (openOrders.TryGetValue(id, out ret))
                {
                    return ret;
                }
                return null;
            }
        }

        public int GetShares(Symbol symbol)
        {
            lock (_lockObject)
            {
                int shares = 0;
                if (openShares.ContainsKey(symbol))
                {
                    shares = openShares[symbol];
                }

                return shares;
            }
        }

        public void AddOrderUpdatedDelegate(OrderUpdatedDelegate orderUpdated)
        {
            lock (_lockObject)
            {
                OrderUpdated += orderUpdated;
            }
        }

        public void RemoveOrderUpdatedDelegate(OrderUpdatedDelegate orderUpdated)
        {
            lock (_lockObject)
            {
                OrderUpdated -= orderUpdated;
            }
        }

        public void AddPositionAvailableDelegate(PositionAvailableDelegate positionAvailable)
        {
            lock (_lockObject)
            {
                PositionAvailable += positionAvailable;
            }
        }

        public void RemovePositionAvailableDelegate(PositionAvailableDelegate positionAvailable)
        {
            lock (_lockObject)
            {
                PositionAvailable -= positionAvailable;
            }
        }

        public bool IsLiveBroker()
        {
            return true;
        }

        public object CustomMessage(string type, object data)
        {
            return null;
        }

        private class Unlocker : IDisposable
        {
            private object _lockObject;
            public Unlocker(object lockObject)
            {
                _lockObject = lockObject;
                Monitor.Exit(_lockObject);
            }

            public void Dispose()
            {
                Monitor.Enter(_lockObject);
            }
        }

        private IDisposable Unlock(object lockObject)
        {
            return new Unlocker(lockObject);
        }

        public void Log(string eventName, string details)
        {
            Log(null, null, null, eventName, details);
        }

        public void Log(BrokerOrder order, string eventName, string details)
        {
            if (order == null)
            {
                Log(null, null, null, eventName, details);
            }
            else
            {
                Log(order.OrderSymbol.ToString(), order.OrderId, order.PositionID, eventName, details);
            }
        }

        //private object _logLock = new object();
        Logger _logger;

        public void Log(string symbol, string orderId, string posId, string eventName, string details)
        {
            if (_settings.EnableLogging && ((_connectOptions & ServiceConnectOptions.Broker) == ServiceConnectOptions.Broker) && _logger != null)
            {
                symbol = symbol ?? "n/a";
                orderId = orderId ?? "n/a";
                posId = posId ?? "n/a";

                string logLine = string.Join("\t", new string[] { symbol, orderId, posId, eventName, details });

                _logger.Log(NLog.LogLevel.Info, logLine);

                //DateTime time = DateTime.Now;
                //string fileDateFormat = "yyyy'-'MM'-'dd";
                //string logDateFormat = "yyyy'-'MM'-'dd HH':'mm':'ss.fff";

                //string logFile = Path.Combine(_settings.LogPath, "RightEdgeTWSPluginLog" + time.ToString(fileDateFormat) + ".txt");

                //string logLine = string.Join("\t", new string[] { time.ToString(logDateFormat), symbol, orderId, posId, eventName, details });
                //Trace.WriteLine(logLine);

                //lock (_logLock)
                //{
                //    if (!File.Exists(logFile))
                //    {
                //        //  Write column headers
                //        var headerLine = string.Join("\t", new string[] { "Time", "Symbol", "Order ID", "Position ID", "Event", "Details" }) + Environment.NewLine;
                //        File.AppendAllText(logFile, headerLine);
                //    }
                //    File.AppendAllText(logFile, logLine + Environment.NewLine);
                //}
            }
        }

        private void CleanupLogs()
        {
            if (_settings.EnableLogging && _settings.CleanupLogs)
            {
                var files = Directory.GetFiles(_settings.LogPath, "*.txt");
                List<string> logFiles = files.Where(f => IsLogFile(f)).ToList();
                logFiles = logFiles.OrderByDescending(f => f).ToList();

                foreach (var fileToDelete in logFiles.Skip(_settings.DaysToKeepLogs))
                {
                    try
                    {
                        File.Delete(fileToDelete);
                    }
                    catch (Exception ex)
                    {
                        Log("Log Cleanup Failure", ex.ToString());
                    }
                }
            }
        }

        private bool IsLogFile(string filename)
        {
            var path = Path.GetDirectoryName(filename);
            var extension = Path.GetExtension(filename);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);

            if (!string.Equals(path, _settings.LogPath, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(extension, ".txt", StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            string prefix = "RightEdgeTWSPluginLog";
            if (!nameWithoutExtension.StartsWith(prefix))
            {
                return false;
            }

            string restOfName = nameWithoutExtension.Substring(prefix.Length);
            string dateFormat = "yyyy'-'MM'-'dd";
            DateTime date;
            if (DateTime.TryParseExact(restOfName, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

    }

    public class TWSAssetArgs
    {
        public string Symbol = string.Empty;
        //public string SecType = string.Empty;
        public SecurityType SecType = SecurityType.Undefined;
        public string Expiry = string.Empty;
        public double Strike = 0.0;
        public RightType Right = RightType.Undefined;
        public string Multiplier = string.Empty;
        public string Exchange = "SMART";
        public string PrimaryExchange = "SMART";
        public string Currency = "USD";

        public Contract ToContract()
        {
            Contract ret = new Contract(0, Symbol, SecType, Expiry, Strike, Right, Multiplier, Exchange, Currency, "", PrimaryExchange);
            if (!string.IsNullOrEmpty(Expiry))
            {
                ret.IncludeExpired = true;
            }

            return ret;
        }

        public static SecurityType GetSecurityType(AssetClass assetClass)
        {
            switch (assetClass)
            {
                case AssetClass.Stock:
                    return SecurityType.Stock;
                case AssetClass.Bond:
                    return SecurityType.Bond;
                case AssetClass.Forex:
                    return SecurityType.Cash;
                case AssetClass.Future:
                    return SecurityType.Future;
                case AssetClass.FuturesOption:
                    return SecurityType.FutureOption;
                case AssetClass.Index:
                    return SecurityType.Index;
                case AssetClass.Option:
                    return SecurityType.Option;
                default:
                    return SecurityType.Undefined;
            }
        }
        public static AssetClass GetREAssetClass(SecurityType type)
        {
            switch (type)
            {

                case SecurityType.Bond:
                    return AssetClass.Bond;
                case SecurityType.Future:
                    return AssetClass.Future;
                case SecurityType.FutureOption:
                    return AssetClass.FuturesOption;
                case SecurityType.Index:
                    return AssetClass.Index;
                case SecurityType.Option:
                    return AssetClass.Option;
                case SecurityType.Stock:
                    return AssetClass.Stock;
                case SecurityType.Cash:
                    return AssetClass.Forex;
                case SecurityType.Bag:
                default:
                    //	No corresponding type.  Return stock for now.
                    return AssetClass.Stock;
            }
        }

        /// <summary>
        /// Gets an expiration string formatted for IB and only if its an asset
        /// class that wastes, otherwise this string will be empty.
        /// </summary>
        public static string GetExpiration(Symbol symbol)
        {
            string expDate = "";

            if (symbol.AssetClass == AssetClass.Future ||
                symbol.AssetClass == AssetClass.FuturesOption ||
                symbol.AssetClass == AssetClass.Option)
            {
                if (symbol.ExpirationDate != DateTime.MinValue &&
                    symbol.ExpirationDate != DateTime.MaxValue)
                {
                    expDate = symbol.ExpirationDate.ToString("yyyyMMdd");
                }
            }

            return expDate;
        }



        /// <summary>
        /// Returns a "valid" strike price.  In other words, it checks to make sure
        /// a) that the asset class is one that uses strike prices (i.e. options and
        /// futures options) and then it returns the configured strike price.  Otherwise
        /// it returns 0.0;
        /// </summary>
        public static double GetValidStrikePrice(Symbol symbol)
        {
            double strike = 0.0;

            if (symbol.AssetClass == AssetClass.Option ||
                symbol.AssetClass == AssetClass.FuturesOption)
            {
                strike = symbol.StrikePrice;
            }

            return strike;
        }

        public static RightType GetRightType(ContractType contract)
        {
            switch (contract)
            {
                case ContractType.Call:
                    return RightType.Call;
                case ContractType.Put:
                    return RightType.Put;
                default:
                    return RightType.Undefined;
            }

        }

        public static string GetIBCurrency(CurrencyType currencyType)
        {
            string currency = "USD";

            switch (currencyType)
            {
                case CurrencyType.AUD:
                    currency = "AUD";
                    break;

                case CurrencyType.BRL:
                case CurrencyType.CNY:
                case CurrencyType.INR:
                case CurrencyType.None:
                    currency = "";		// Currency types not supported by IB
                    break;

                case CurrencyType.CAD:
                    currency = "CAD";
                    break;

                case CurrencyType.CHF:
                    currency = "CHF";
                    break;

                case CurrencyType.EUR:
                    currency = "EUR";
                    break;

                case CurrencyType.GBP:
                    currency = "GBP";
                    break;

                case CurrencyType.HKD:
                    currency = "HKD";
                    break;

                case CurrencyType.JPY:
                    currency = "JPY";
                    break;

                case CurrencyType.KRW:
                    currency = "KRW";
                    break;

                case CurrencyType.MXN:
                    currency = "MXN";
                    break;

                case CurrencyType.NOK:
                    currency = "NOK";
                    break;

                case CurrencyType.NZD:
                    currency = "NZD";
                    break;

                case CurrencyType.RUB:
                    currency = "RUB";
                    break;

                case CurrencyType.SEK:
                    currency = "SEK";
                    break;

                case CurrencyType.SGD:
                    currency = "SGD";
                    break;

                case CurrencyType.USD:
                    currency = "USD";
                    break;

                default:
                    currency = currencyType.ToString();
                    break;
            }

            return currency;
        }

        public static CurrencyType GetRECurrency(string IBCurrency)
        {
            ReturnValue<CurrencyType> ret = EnumUtil<CurrencyType>.Parse(IBCurrency);
            if (!ret.Success)
            {
                return CurrencyType.None;
            }
            else
            {
                return ret.Value;
            }
        }

        public static Symbol SymbolFromContract(Contract contract)
        {
            //	TODO: finish conversion code
            Symbol ret = new Symbol(contract.Symbol);
            ret.CurrencyType = GetRECurrency(contract.Currency);
            ret.Exchange = contract.Exchange;
            if (ret.Exchange == null)
            {
                ret.Exchange = "";
            }
            if (contract.SecurityType == SecurityType.Future ||
                contract.SecurityType == SecurityType.FutureOption ||
                contract.SecurityType == SecurityType.Option)
            {
                if (contract.Expiry != null && contract.Expiry.Length == 6)
                {
                    string year = contract.Expiry.Substring(0, 4);
                    string month = contract.Expiry.Substring(4, 2);
                    ret.ExpirationDate = new DateTime(Convert.ToInt32(year), Convert.ToInt32(month), 1);
                }
            }
            if (contract.Right == RightType.Call)
            {
                ret.ContractType = ContractType.Call;
            }
            else if (contract.Right == RightType.Put)
            {
                ret.ContractType = ContractType.Put;
            }
            else
            {
                ret.ContractType = ContractType.NoContract;
            }

            ret.AssetClass = GetREAssetClass(contract.SecurityType);
            ret.StrikePrice = contract.Strike;
            ret.Name = contract.Symbol;

            return ret;
        }

        public static TWSAssetArgs Create(Symbol symbol)
        {
            TWSAssetArgs ret = new TWSAssetArgs();
            if (symbol.AssetClass == AssetClass.Forex)
            {
                ret.Symbol = GetIBCurrency(symbol.BaseCurrency);
            }
            else
            {
                ret.Symbol = symbol.Name;
            }


            ret.SecType = GetSecurityType(symbol.AssetClass);
            ret.Expiry = GetExpiration(symbol);
            ret.Strike = GetValidStrikePrice(symbol);
            ret.Right = GetRightType(symbol.ContractType);

            //if (symbol.SymbolInformation.TickSize > 0)
            //{
            //    ret.Multiplier = symbol.SymbolInformation.TickSize.ToString();
            //}
            //else
            //{
            //    ret.Multiplier = "";
            //}
            if (symbol.SymbolInformation.ContractSize > 0)
            {
                ret.Multiplier = symbol.SymbolInformation.ContractSize.ToString();
            }
            else
            {
                ret.Multiplier = "";
            }

            if (string.IsNullOrEmpty(symbol.Exchange))
            {
                ret.Exchange = "SMART";
            }
            else
            {
                ret.Exchange = symbol.Exchange;
            }

            if (ret.Exchange.ToUpper() != "SMART")
            {
                ret.PrimaryExchange = ret.Exchange;
            }

            ret.Currency = GetIBCurrency(symbol.CurrencyType);

            // modification for XAU and XAG.
            // http://www.rightedgesystems.com/forums/14073/Physical-Gold-and-Silver-%28XAUUSD-and-XAGUSD%29#bm14083
            if (symbol.Name == "XAUUSD" ||
                symbol.Name == "XAGUSD")
            {
                // RightEdge: XAUUSD (stock) -> XAU/USD commodity (tws)
                // RightEdge: XAGUSD (stock) -> XAG/USD commodity (tws)
                ret.SecType = GetSecurityType(AssetClass.Forex);
            }

            return ret;
        }

    }
}
