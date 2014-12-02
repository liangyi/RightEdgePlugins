using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;

using RightEdge.Common;

namespace MBTrading
{
	public class MBTTickRetrieval : IService, ITickRetrieval, IBroker, MBTQUOTELib.IMbtQuotesNotify
	{
		private Dictionary<Symbol, string> watchedSymbols = new Dictionary<Symbol, string>();
		GotTickData tickListener = null;

		private MBTCOMLib.MbtComMgr moComMgr;
		private MBTQUOTELib.MbtQuotes moQuotes;
		private MBTORDERSLib.IMbtOrderClient orderClient;

		private string userName;
		private string password;
		private int sdkId;
		private bool connected = false;
		private bool watching = false;
		private bool hadError = false;
		string lastError = "";
		private bool disposed = false;

		private bool loginEnded = false;
		private object lockObject = new object();

		public event OrderUpdatedDelegate OrderUpdated;
		//public event PositionUpdatedDelegate PositionUpdated;
		//public event AccountUpdatedDelegate AccountUpdated;
		public event PositionAvailableDelegate PositionAvailable;

		private double buyingPower = 0.0;
		private Dictionary<string, BrokerOrder> openOrders = new Dictionary<string, BrokerOrder>();
		private Dictionary<string, string> orderIds = new Dictionary<string, string>();

		//	This dictionary keeps track of orders that have been "placed", but for which
		//	we have not received the "Submitted" order status.  If these orders are
		//	canceled, we will probably not receive any cancellation confirmation.
		//	(How this works is mostly guesswork)
		private Dictionary<string, bool> unSubmittedOrders = new Dictionary<string, bool>();

		private Dictionary<Symbol, long> sharesLong = new Dictionary<Symbol, long>();
		private Dictionary<Symbol, long> sharesShort = new Dictionary<Symbol, long>();

		// These are shares that already.
		private Dictionary<Symbol, int> openShares = new Dictionary<Symbol, int>();

		object lockOpenOrders = new object();

		#region IMbtQuotesNotify Members

		public void OnLevel2Data(ref MBTQUOTELib.LEVEL2RECORD pRec)
		{
		}

		public void OnOptionsData(ref MBTQUOTELib.OPTIONSRECORD pRec)
		{
		}

		public void OnQuoteData(ref MBTQUOTELib.QUOTERECORD pQuote)
		{
			TickData data;
			Symbol symbol = FindSymbolFromName(pQuote.bstrSymbol);

			if (symbol == null)
			{
				// Might be good to throw an exception here.  Theoretically, if someone
				// is getting data for a symbol we don't know about, then we probably
				// did something wrong somewhere.
				return;
			}

			if (pQuote.dLast > 0)
			{
				data = new TickData();
				data.tickType = TickType.Trade;
				data.price = pQuote.dLast;
				data.size = (ulong)pQuote.lLastSize;
				data.time = pQuote.UTCDateTime;

				if (tickListener != null)
				{
					tickListener(symbol, data);
				}
			}

			if (pQuote.dAsk > 0)
			{
				data = new TickData();
				data.tickType = TickType.Ask;
				data.price = pQuote.dAsk;
				data.size = (ulong)pQuote.lAskSize;
				data.time = pQuote.UTCDateTime;

				if (tickListener != null)
				{
					tickListener(symbol, data);
				}
			}

			if (pQuote.dBid > 0)
			{
				data = new TickData();
				data.tickType = TickType.Bid;
				data.price = pQuote.dBid;
				data.size = (ulong)pQuote.lBidSize;
				data.time = pQuote.UTCDateTime;

				if (tickListener != null)
				{
					tickListener(symbol, data);
				}
			}

			if (pQuote.dHigh > 0)
			{
				data = new TickData();
				data.tickType = TickType.HighPrice;
				data.price = pQuote.dHigh;
				data.time = pQuote.UTCDateTime;

				if (tickListener != null)
				{
					tickListener(symbol, data);
				}
			}

			if (pQuote.dLow > 0)
			{
				data = new TickData();
				data.tickType = TickType.LowPrice;
				data.price = pQuote.dLow;
				data.time = pQuote.UTCDateTime;

				if (tickListener != null)
				{
					tickListener(symbol, data);
				}
			}

			if (pQuote.dPrevClose > 0)
			{
				data = new TickData();
				data.tickType = TickType.PreviousClose;
				data.price = pQuote.dPrevClose;
				data.time = pQuote.UTCDateTime;

				if (tickListener != null)
				{
					tickListener(symbol, data);
				}
			}

			if (pQuote.lVolume > 0)
			{
				data = new TickData();
				data.tickType = TickType.DailyVolume;
				data.size = (ulong)pQuote.lVolume;
				data.time = pQuote.UTCDateTime;

				if (tickListener != null)
				{
					tickListener(symbol, data);
				}
			}
		}

		public Symbol FindSymbolFromName(string name)
		{
			foreach (Symbol symbol in watchedSymbols.Keys)
			{
				if (watchedSymbols[symbol] == name)
				{
					return symbol;
				}
			}

			return null;
		}

		public void OnTSData(ref MBTQUOTELib.TSRECORD pRec)
		{
		}

		#endregion

		#region IService Members

        public event EventHandler<ServiceEventArgs> ServiceEvent;

		public string ServiceName()
		{
			return "MB Trading";
		}

		public string Author()
		{
			return "Yye Software";
		}

		public string Description()
		{
			return "Provides realtime data and broker services from MB Trading";
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
			return "{E9A82F73-802C-4d01-B49B-FE4D6D5F7E0E}";
		}

		public bool NeedsServerAddress()
		{
			return false;
		}

		public bool NeedsPort()
		{
			return false;
		}

		public bool NeedsAuthentication()
		{
			return true;
		}

		public bool SupportsMultipleInstances()
		{
			return false;
		}

		public string ServerAddress
		{
			get
			{
				return "";
			}
			set
			{
			}
		}

		public int Port
		{
			get
			{
				return 0;
			}
			set
			{
			}
		}

		public string UserName
		{
			get
			{
				return userName;
			}
			set
			{
				userName = value;
			}
		}

		public string Password
		{
			get
			{
				return password;
			}
			set
			{
				password = value;
			}
		}

		public bool BarDataAvailable
		{
			get
			{
				return false;
			}
		}

		public bool TickDataAvailable
		{
			get
			{
				return true;
			}
		}

		public bool BrokerFunctionsAvailable
		{
			get
			{
				return true;
			}
		}

		public IBarDataRetrieval GetBarDataInterface()
		{
			return null;
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
			MBTradingSettings dlg = new MBTradingSettings();

			string sdkid = "";

			settings.TryGetValue("sdkid", out sdkid);

			dlg.SDKID = sdkid;

			if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				settings["sdkid"] = dlg.SDKID;
			}

			return true;
		}

		public bool Initialize(SerializableDictionary<string, string> settings)
		{
			string sdk = "";

			if (settings.TryGetValue("sdkid", out sdk))
			{
				if (!int.TryParse(sdk, out sdkId))
				{
					sdkId = 3089;
				}
				//sdkId = Convert.ToInt32(sdk);
			}

			return true;
		}

		public bool Connect(ServiceConnectOptions connectOptions)
		{
			ClearError();

			if (!connected)
			{
				watchedSymbols.Clear();

				moComMgr = new MBTCOMLib.MbtComMgr();
				moQuotes = moComMgr.Quotes;
				moComMgr.EnableSplash(false);
				moComMgr.SilentMode = true;

				bool loggedIn = false;
				if (!loggedIn)
				{

                    moComMgr.OnLogonDeny += new MBTCOMLib.IMbtComMgrEvents_OnLogonDenyEventHandler(moComMgr_OnLogonDeny);
                    moComMgr.OnLogonSucceed += new MBTCOMLib.IMbtComMgrEvents_OnLogonSucceedEventHandler(moComMgr_OnLogonSucceed);
                    moComMgr.OnHealthUpdate += new MBTCOMLib.IMbtComMgrEvents_OnHealthUpdateEventHandler(moComMgr_OnHealthUpdate);
                    moComMgr.OnCriticalShutdown += new MBTCOMLib.IMbtComMgrEvents_OnCriticalShutdownEventHandler(moComMgr_OnCriticalShutdown);
                    moComMgr.OnAlertAdded += new MBTCOMLib.IMbtComMgrEvents_OnAlertAddedEventHandler(moComMgr_OnAlertAdded);
                    moComMgr.OrderClient.OnSubmit += new MBTORDERSLib._IMbtOrderClientEvents_OnSubmitEventHandler(OrderClient_OnSubmit);
                    moComMgr.OrderClient.OnRemove += new MBTORDERSLib._IMbtOrderClientEvents_OnRemoveEventHandler(OrderClient_OnRemove);
                    moComMgr.OrderClient.OnExecute += new MBTORDERSLib._IMbtOrderClientEvents_OnExecuteEventHandler(OrderClient_OnExecute);
                    moComMgr.OrderClient.OnCancelRejected += new MBTORDERSLib._IMbtOrderClientEvents_OnCancelRejectedEventHandler(OrderClient_OnCancelRejected);
                    moComMgr.OrderClient.OnBalanceUpdate += new MBTORDERSLib._IMbtOrderClientEvents_OnBalanceUpdateEventHandler(OrderClient_OnBalanceUpdate);

					orderClient = moComMgr.OrderClient;
					orderClient.OnDemandMode = false;
					orderClient.SilentMode = true;

					connected = moComMgr.DoLogin(sdkId, userName, password, "");

                    /* To see if OnLogonDeny will catch this.
                     
                    if (!connected)
                    {
                        lastError = "Unable to connect to MB Trading";
                       hadError = true;
                        return CheckError();
                    }

                    */
					
                    int timeOut = 0;

					while (true)
					{
						lock (lockObject)
						{
							if (loginEnded || orderClient.GotInitialLogonSucceed() )
							{
								break;
							}
                            //if the API isn't letting us know what's up, wait 5 seconds before throwing
                            else if (timeOut > 500)
                            {
                                lastError = "Timed out waiting for MBTrading API.";
                                if (connected) lastError = lastError + " (connected = true)";
                                else lastError = lastError + " (connected = false)";
                                hadError = true;
                                break;
                            }
						}
						Thread.Sleep(10);
                        timeOut++;
					}
				}
			}

			return CheckError();
		}

        public void SyncAccountState()
        {

        }

		void OrderClient_OnBalanceUpdate(MBTORDERSLib.MbtAccount pAcct)
		{
			buyingPower = pAcct.CurrentBP;
		}

		void OrderClient_OnCancelRejected(MBTORDERSLib.MbtOpenOrder pOrd)
		{
		}

		void OrderClient_OnExecute(MBTORDERSLib.MbtOpenOrder pOrd)
		{
			string information = "Filled";
			Fill fill = null;

			BrokerOrder openOrder = openOrders[pOrd.Token];

			fill = new Fill();
			fill.FillDateTime = DateTime.Now;
			fill.Price = new Price(pOrd.Price, pOrd.Price);

			long alreadyFilled = 0;
			foreach (Fill existingFill in openOrder.Fills)
			{
				alreadyFilled += existingFill.Quantity;
			}

			fill.Quantity = pOrd.SharesFilled - alreadyFilled;

			openOrder.Fills.Add(fill);

			if (pOrd.SharesFilled == pOrd.Quantity)
			{
				openOrder.OrderState = BrokerOrderState.Filled;
			}
			else
			{
				openOrder.OrderState = BrokerOrderState.PartiallyFilled;
				information = "Partial fill";
			}

			if (OrderUpdated != null)
			{
				OrderUpdated(openOrder, fill, information);
			}

			if (openOrder.OrderState == BrokerOrderState.Filled)
			{
				AddShares(openOrder);
			}

			if (openOrder.OrderState == BrokerOrderState.Filled || openOrder.OrderState == BrokerOrderState.Rejected ||
				openOrder.OrderState == BrokerOrderState.Cancelled)
			{
				openOrders.Remove(openOrder.OrderId);
			}
		}

		void OrderClient_OnRemove(MBTORDERSLib.MbtOpenOrder pOrd)
		{
			throw new Exception("The method or operation is not implemented.");
		}

		void OrderClient_OnSubmit(MBTORDERSLib.MbtOpenOrder pOrd)
		{
			if (OrderUpdated != null)
			{
				// Update the actual order number against the token.  We'll need
				// this later.
				orderIds[pOrd.Token] = pOrd.OrderNumber;
				BrokerOrder order = openOrders[pOrd.Token];
				order.OrderState = BrokerOrderState.Submitted;
				OrderUpdated(order, null, "Submitted");
			}
		}

        void moComMgr_OnLogonDeny(string bstrReason)
        {
            lock (lockObject)
            {
                loginEnded = true;
            }
            connected = false;
            hadError = true;
            lastError = bstrReason;

            throw new Exception(lastError);
        }

        void moComMgr_OnLogonSucceed()
        {
            lock (lockObject)
            {
                loginEnded = true;
            }
            connected = true;
            //MessageBox.Show("Logon was successful.", "Success");
            return;
        }

		void moComMgr_OnAlertAdded(MBTCOMLib.MbtAlert pAlert)
		{
		}

		void moComMgr_OnCriticalShutdown()
		{
			lock (lockObject)
			{
				loginEnded = true;
			}

			connected = false;
			hadError = true;
			lastError = "Critical shutdown from MB Trading";

			throw new Exception(lastError);
		}

		void moComMgr_OnHealthUpdate(MBTCOMLib.enumServerIndex index, MBTQUOTELib.enumConnectionState state)
		{
			if (index == MBTCOMLib.enumServerIndex.siQuotes && state == MBTQUOTELib.enumConnectionState.csLoggedIn)
			{
				lock (lockObject)
				{
					loginEnded = true;
				}
			}
		}

		public bool Disconnect()
		{
			ClearError();
			if (connected)
			{
				moQuotes.UnadviseAll(this);
				moQuotes.Disconnect();
				connected = false;

				do
				{
				}
				while (System.Runtime.InteropServices.Marshal.ReleaseComObject(moComMgr) > 0);
			}

			return CheckError();
		}

		public string GetError()
		{
			return lastError;
		}

		#endregion

		#region IDisposable Members

		public void Dispose()
		{
			if (!disposed)
			{
				if (moComMgr != null)
				{
					do
					{
					}
					while (System.Runtime.InteropServices.Marshal.ReleaseComObject(moComMgr) > 0);
				}
			}

			disposed = true;
		}

		#endregion

		#region ITickRetrieval Members

		public bool RealTimeDataAvailable
		{
			get
			{
				return true;
			}
		}

		public GotTickData TickListener
		{
			set
			{
				tickListener = value;
			}
		}

		public bool SetWatchedSymbols(List<Symbol> symbols)
		{
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
					if (watching)
					{
						moQuotes.UnadviseSymbol(this, watchedSymbols[symbol], 0);
					}
					watchedSymbols.Remove(symbol);
				}
			}

			//	Check error here because StartWatching() will clear error status
			if (!CheckError())
			{
				return false;
			}

			if (watching)
			{
				StartWatching();
			}
			return CheckError();
		}

		public bool IsWatching()
		{
			return watching;
		}

		public bool StartWatching()
		{
			ClearError();
			foreach (Symbol symbol in new List<Symbol>(watchedSymbols.Keys))
			{
				MBTQUOTELib.enumQuoteServiceFlags quoteOption = MBTQUOTELib.enumQuoteServiceFlags.qsfLevelOne;

				if (symbol.AssetClass == AssetClass.Option)
				{
					quoteOption |= MBTQUOTELib.enumQuoteServiceFlags.qsfOptions;
				}

				moQuotes.AdviseSymbol(this, symbol.Name, (int)quoteOption);

				watchedSymbols[symbol] = symbol.Name;
			}

			if (CheckError())
			{
				watching = true;
			}

			return CheckError();
		}

		public bool StopWatching()
		{
			ClearError();
			watching = false;
			foreach (Symbol symbol in new List<Symbol>(watchedSymbols.Keys))
			{
				if (watchedSymbols[symbol] != null)
				{
					moQuotes.UnadviseSymbol(this, watchedSymbols[symbol], 0);
					watchedSymbols[symbol] = null;
				}
			}
			return CheckError();
		}

		public IService GetService()
		{
			return this;
		}

		#endregion

		private void ClearError()
		{
			lastError = "";
			hadError = false;
		}

		private bool CheckError()
		{
			return !hadError;
		}

		public class MBConst
		{
			public const int tickEvenUp = 0;
			public const int tickDown = 1;
			public const int VALUE_BUY = 10000;
			public const int VALUE_SELL = 10001;
			public const int VALUE_SELLSHT = 10002; // sell shit?
			public const int VALUE_DAYNB = 10009;
			public const int VALUE_GTC = 10008;
			public const int VALUE_DAY = 10011;
			public const int VALUE_AGENCY = 10020;
			public const int VALUE_NORMAL = 10042;

			// Order types
			public const int VALUE_DISCRETIONARY = 10043;
			public const int VALUE_LIMIT = 10030;
			public const int VALUE_LIMIT_CLOSE = 10057;
			public const int VALUE_LIMIT_OPEN = 10056;
			public const int VALUE_LIMIT_STOPMKT = 10064;
			public const int VALUE_LIMIT_TRAIL = 10054;
			public const int VALUE_LIMIT_TTO = 10050;
			public const int VALUE_MARKET = 10031;
			public const int VALUE_MARKET_CLOSE = 10039;
			public const int VALUE_MARKET_OPEN = 10038;
			public const int VALUE_MARKET_STOP = 10069;
			public const int VALUE_MARKET_TRAIL = 10055;
			public const int VALUE_MARKET_TTO = 10051;
			public const int VALUE_PART = 10046;
			public const int VALUE_PEGGED = 10062;
			public const int VALUE_RESERVE = 10040;
			public const int VALUE_RSV_DISC = 10044;
			public const int VALUE_RSV_PEGGED = 10066;
			public const int VALUE_RSV_TTO = 10052;
			public const int VALUE_STOPLMT_STOP = 10072;
			public const int VALUE_STOPLMT_TRAIL = 10068;
			public const int VALUE_STOPLMT_TTO = 10067;
			public const int VALUE_STOP_LIMIT = 10033;
			public const int VALUE_STOP_MARKET = 10032;
			public const int VALUE_STOP_STOP = 10073;
			public const int VALUE_STOP_TRAIL = 10065;
			public const int VALUE_STOP_TTO = 10053;
			public const int VALUE_TRAILING_STOP = 10034;
			public const int VALUE_TTO_ORDER = 10037;
			public const int VALUE_VWAP = 10063;
		}

		#region IBroker Members

		public void SetAccountState(BrokerAccountState state)
		{

		}

		private int GetOrderType(OrderType orderType)
		{
			int orderTypeConst = MBConst.VALUE_MARKET;

			switch (orderType)
			{
				case OrderType.Limit:
					orderTypeConst = MBConst.VALUE_LIMIT;
					break;

				case OrderType.LimitOnClose:
					orderTypeConst = MBConst.VALUE_LIMIT_CLOSE;
					break;

				case OrderType.MarketOnClose:
					orderTypeConst = MBConst.VALUE_MARKET_CLOSE;
					break;

				case OrderType.MarketOnOpen:
					orderTypeConst = MBConst.VALUE_MARKET_OPEN;
					break;

				case OrderType.PeggedToMarket:
					orderTypeConst = MBConst.VALUE_PEGGED;
					break;

				case OrderType.Stop:
					orderTypeConst = MBConst.VALUE_MARKET_STOP;
					break;

				case OrderType.StopLimit:
					orderTypeConst = MBConst.VALUE_LIMIT_STOPMKT;
					break;

				case OrderType.TrailingStop:
					orderTypeConst = MBConst.VALUE_LIMIT_TRAIL;
					break;
			}

			return orderTypeConst;
		}

		public bool SubmitOrder(BrokerOrder order, out string orderId)
		{
			int buySell = MBConst.VALUE_BUY;
			int timeInForce = MBConst.VALUE_GTC;
			int orderType = GetOrderType(order.OrderType);

			if (order.TransactionType == TransactionType.Sell)
			{
				buySell = MBConst.VALUE_SELL;
			}

			if (order.TransactionType == TransactionType.Short)
			{
				buySell = MBConst.VALUE_SELLSHT;
			}

			if (order.GoodTillCanceled || order.OrderSymbol.AssetClass == AssetClass.Forex)
			{
				// GTC order required for forex
				timeInForce = MBConst.VALUE_GTC;
			}

			if (order.OrderType == OrderType.Market ||
				order.OrderType == OrderType.MarketOnClose ||
				order.OrderType == OrderType.MarketOnOpen)
			{
				timeInForce = MBConst.VALUE_DAY;
			}

			string route = "MBTX";
			DateTime dt = new DateTime(0);

			if (order.OrderSymbol.Exchange.Length > 0)
			{
				route = order.OrderSymbol.Exchange;
			}

			int expirationMonth = 0;
			int expirationYear = 0;
			double strikePrice = 0.0;
			string orderToken = "";

			if (order.OrderSymbol.AssetClass == AssetClass.Option)
			{
				expirationMonth = order.OrderSymbol.ExpirationDate.Month;
				expirationYear = order.OrderSymbol.ExpirationDate.Year;
				strikePrice = order.OrderSymbol.StrikePrice;
			}

			string lastMessage = null;
			string message = null;

			if (orderClient.OrderHistories.Count > 0)
			{
				lastMessage = orderClient.OrderHistories[orderClient.OrderHistories.Count - 1].Message;
			}

			bool submitted =
				orderClient.Submit(buySell, (int)order.Shares, order.OrderSymbol.Name, order.LimitPrice,
				order.StopPrice, timeInForce, MBConst.VALUE_AGENCY, orderType, MBConst.VALUE_NORMAL,
				0, orderClient.Accounts.DefaultAccount, route, "", 0, 0, dt, dt, expirationMonth,
				expirationYear, strikePrice, "", 0, -1, 1, "", false, ref orderToken);

			if (submitted)
			{
				// An order token is given to us first.  We don't know anything until
				// we receive another event (OnHistoryAdded)
				openOrders.Add(orderToken, order);
				orderIds.Add(orderToken, "");
			}
			else
			{
				lastError = "Error submitting order.";

				if (orderClient.OrderHistories.Count > 0)
				{
					message = orderClient.OrderHistories[orderClient.OrderHistories.Count - 1].Message;
				}
			}

			order.OrderId = orderToken;
			orderId = orderToken;

			return submitted;
		}

		public bool CancelOrder(string orderId)
		{
			string mbtOrderNumber = orderIds[orderId];
			string errorMsg = "";

			bool cancelled = orderClient.Cancel(mbtOrderNumber, ref errorMsg);
			if (!cancelled)
			{
				lastError = errorMsg;
			}

			return cancelled;
		}

		public bool CancelAllOrders()
		{
			orderClient.OpenOrders.CancelAll(orderClient.Accounts.DefaultAccount);

			return true;
		}

		public double GetBuyingPower()
		{
			return buyingPower;
		}

        public double GetAccountBalance()
        {
            return GetBuyingPower();
        }

		public List<BrokerOrder> GetOpenOrders()
		{
			List<BrokerOrder> orders = new List<BrokerOrder>(openOrders.Values);

			return orders;
		}

		public BrokerOrder GetOpenOrder(string id)
		{
			if (openOrders.ContainsKey(id))
			{
				return openOrders[id];
			}
			else
			{
				return null;
			}
		}

		public int GetShares(Symbol symbol)
		{
			throw new Exception("The method or operation is not implemented.");
		}

		public void AddOrderUpdatedDelegate(OrderUpdatedDelegate orderUpdated)
		{
			OrderUpdated += orderUpdated;
		}

		public void RemoveOrderUpdatedDelegate(OrderUpdatedDelegate orderUpdated)
		{
			OrderUpdated -= orderUpdated;
		}

		public void AddPositionAvailableDelegate(PositionAvailableDelegate positionAvailable)
		{
			PositionAvailable += positionAvailable;
		}

		public void RemovePositionAvailableDelegate(PositionAvailableDelegate positionAvailable)
		{
			PositionAvailable -= positionAvailable;
		}

		public bool IsLiveBroker()
		{
			return true;
		}

		public object CustomMessage(string type, object data)
		{
            return null;
		}

		#endregion

		/// <summary>
		/// Adds shares to the list of shares long or short
		/// </summary>
		/// <param name="order">The filled order.</param>
		private void AddShares(BrokerOrder order)
		{
			// Only filled orders can go here!
			Debug.Assert(order.OrderState == BrokerOrderState.Filled);

			if (order.TransactionType == TransactionType.Buy)
			{
				if (sharesLong.ContainsKey(order.OrderSymbol))
				{
					sharesLong[order.OrderSymbol] += order.Shares;
				}
				else
				{
					sharesLong.Add(order.OrderSymbol, order.Shares);
				}
			}
			else
			{
				if (sharesShort.ContainsKey(order.OrderSymbol))
				{
					sharesShort[order.OrderSymbol] += order.Shares;
				}
				else
				{
					sharesShort.Add(order.OrderSymbol, order.Shares);
				}
			}
		}

		/// <summary>
		/// Removes shares that have been sold or covered.
		/// </summary>
		/// <param name="order">The filled order.</param>
		private void RemoveShares(BrokerOrder order)
		{
			// Only filled orders can go here!
			Debug.Assert(order.OrderState == BrokerOrderState.Filled);

			if (order.TransactionType == TransactionType.Sell)
			{
				if (sharesLong.ContainsKey(order.OrderSymbol))
				{
					sharesLong[order.OrderSymbol] -= order.Shares;
				}
				else
				{
					// Something got screwed and needs to be fixed.
					Debug.Assert(false);
				}
			}
			else
			{
				if (sharesShort.ContainsKey(order.OrderSymbol))
				{
					sharesShort[order.OrderSymbol] -= order.Shares;
				}
				else
				{
					// Something got screwed and needs to be fixed.
					Debug.Assert(false);
				}
			}
		}
	}
}
