using System;
using System.Net;
using System.IO;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;

using RightEdge.Common;
using Serilog;

namespace IQFeed
{
	public class IQFeedService : IService, ITickRetrieval, IBarDataRetrieval
	{
		private string lastError = "";
		bool connected = false;
		bool watching = false;
		bool hadError = false;
		GotTickData tickListener = null;
		private List<Symbol> watchedSymbols = new List<Symbol>();
		private IQFeed iqFeed;
		//private ManualResetEvent connectDone = new ManualResetEvent(false);
		private Dictionary<string, Symbol> symbolMapping = new Dictionary<string, Symbol>();
		private DateTime lastGoodTickTime = DateTime.MinValue;
		private DateTime _lastTickTime;

        public class Settings
        {
            public bool EnableLogging { get; set; }
            public bool IgnoreLastHistBar { get; set; }
            public bool FilterUsingCurrentTime { get; set; }
            //  Difference between exchange time and local time
            public TimeSpan ExchangeTimeDiff { get; set; }
            public TimeSpan MaxTimeDelta { get; set; }

            public Settings()
            {
                EnableLogging = true;
                MaxTimeDelta = TimeSpan.FromMinutes(40);
            }

            public void SaveTo(IDictionary<string, string> dict)
            {
                dict["EnableLogging"] = EnableLogging.ToString();
                dict["IgnoreLastHistBar"] = IgnoreLastHistBar.ToString();
                dict["FilterUsingCurrentTime"] = FilterUsingCurrentTime.ToString();
                dict["ExchangeTimeDiff"] = ExchangeTimeDiff.ToString("c");
                dict["MaxTimeDelta"] = MaxTimeDelta.ToString("c");
            }

            public void LoadFrom(IDictionary<string, string> settings)
            {
                string enableLogging;
                if (settings.TryGetValue("EnableLogging", out enableLogging))
                {
                    EnableLogging = Convert.ToBoolean(enableLogging);
                }
                string ignorelast;
                if (settings.TryGetValue("IgnoreLastHistBar", out ignorelast))
                {
                    IgnoreLastHistBar = Convert.ToBoolean(ignorelast);
                }
                string filterUsingCurrentTime;
                if (settings.TryGetValue("FilterUsingCurrentTime", out filterUsingCurrentTime))
                {
                    FilterUsingCurrentTime = Convert.ToBoolean(filterUsingCurrentTime);
                }
                string exchangeTimeDiff;
                if (settings.TryGetValue("ExchangeTimeDiff", out exchangeTimeDiff))
                {
                    TimeSpan ts;
                    if (TimeSpan.TryParse(exchangeTimeDiff, out ts))
                    {
                        ExchangeTimeDiff = ts;
                    }
                }
                string maxTimeDelta;
                if (settings.TryGetValue("MaxTimeDelta", out maxTimeDelta))
                {
                    TimeSpan ts;
                    if (TimeSpan.TryParse(maxTimeDelta, out ts))
                    {
                        MaxTimeDelta = ts;
                    }
                }
            }

            public Settings Clone()
            {
                return (Settings)this.MemberwiseClone();
            }
        }

        private Settings _settings = new Settings();
        
        
        Serilog.ILogger _logger;

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
			ClearError();
			watchedSymbols = symbols;

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
			symbolMapping.Clear();

			foreach (Symbol symbol in watchedSymbols)
			{
				SymbolSubscribe(symbol);
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

			foreach (Symbol symbol in watchedSymbols)
			{
				SymbolUnsubscribe(symbol);
			}

			return CheckError();
		}

		public IService GetService()
		{
			return this;
		}

		#endregion

		#region IService Members

		public event EventHandler<ServiceEventArgs> ServiceEvent;

		public string ServiceName()
		{
			return "IQFeed";
		}

		public string Author()
		{
			return "Yye Software";
		}

		public string Description()
		{
			return "Real-time data feed for IQFeed subscribers";
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
			return "{8876A28E-D7B4-4844-B4AE-026BA7C33248}";
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
			return true;
		}

		public bool SupportsMultipleInstances()
		{
			return true;
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

		public string UserName { get; set; }

		public string Password { get; set; }

		public bool BarDataAvailable
		{
			get
			{
				return true;
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
				return false;
			}
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
			return null;
		}

		public bool HasCustomSettings()
		{
			return true;
		}

		public bool ShowCustomSettingsForm(ref SerializableDictionary<string, string> settings)
		{
			IQFeedSettings dlg = new IQFeedSettings();

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


            var loggerConfig = new LoggerConfiguration();

            loggerConfig.MinimumLevel.Debug();

            Serilog.Events.LogEventLevel traceLevel = Serilog.Events.LogEventLevel.Debug;

            if (_settings.EnableLogging)
            {
                string logPath;
                if (!string.IsNullOrEmpty(CommonGlobals.UserAppDataPath))
                {
                    logPath = Path.Combine(CommonGlobals.UserAppDataPath, "IQFeedLogs");
                }
                else
                {
                    logPath = Path.Combine(Environment.CurrentDirectory, "IQFeedLogs");
                }

                loggerConfig.WriteTo.RollingFile(Path.Combine(logPath, "RightEdgeIQFeedLog-{Date}.txt"), retainedFileCountLimit: 30);

                loggerConfig.WriteTo.ColoredConsole(restrictedToMinimumLevel: traceLevel);
                loggerConfig.WriteTo.Trace(restrictedToMinimumLevel: traceLevel);
            }

            _logger = loggerConfig.CreateLogger();

			return true;
		}

		public bool Connect(ServiceConnectOptions connectOptions)
		{
			hadError = false;
            lastError = string.Empty;

			if (connected)
				return true;

			if (iqFeed == null)
			{
                //if (!CreateIQFeed())
                //{
                //    hadError = true;
                //    lastError = "Error connecting to IQFeed.";
                //}
                //else
                //{
                //    connected = true;
                //}

                iqFeed = new IQFeed();
                iqFeed.IQSummaryMessage += new EventHandler<IQSummaryEventArgs>(iqFeed_IQSummaryMessage);
                iqFeed.IQUpdateMessage += new EventHandler<IQSummaryEventArgs>(iqFeed_IQUpdateMessage);
                iqFeed.IQTimeMessage += new EventHandler<IQTimeEventArgs>(iqFeed_IQTimeMessage);
                iqFeed.Disconnected += iqFeed_Disconnected;
			}

            if (iqFeed.Connect(UserName, Password) && !hadError)
            {
                connected = true;
            }
            else
            {
                if (string.IsNullOrEmpty(lastError))
                {
                    lastError = "Unable to connect.";
                }
                return false;
            }

			return !hadError;
		}

		public bool Disconnect()
		{
			if (connected)
			{
				foreach (Symbol symbol in watchedSymbols)
				{
					SymbolUnsubscribe(symbol);
				}

				iqFeed.Disconnect();
				connected = false;
			}

			return hadError;
		}

		public string GetError()
		{
			return lastError;
		}

		#endregion

		#region IDisposable Members

		public void Dispose()
		{
			if (iqFeed != null && connected)
			{
				iqFeed.Disconnect();
			}
		}

		#endregion

		private void SymbolSubscribe(Symbol symbol)
		{
			try
			{
				if (connected)
				{
					string symbolName = iqFeed.SymbolSubscribe(symbol);

					if (symbolName.Length > 0)
					{
						symbolMapping.Add(symbolName, symbol);
					}
				}
			}
			catch (Exception ex)
			{
				lastError = ex.Message;
				hadError = true;
			}
		}

		private void SymbolUnsubscribe(Symbol symbol)
		{
			try
			{
				if (connected)
				{
					iqFeed.SymbolUnsubscribe(symbol);
				}
			}
			catch (Exception ex)
			{
				lastError = ex.Message;
				hadError = true;
			}
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

		void iqFeed_IQTimeMessage(object sender, IQTimeEventArgs e)
		{
		}

		void iqFeed_IQUpdateMessage(object sender, IQSummaryEventArgs e)
		{
			if (!connected)
			{
				return;
			}

			TickData tick = new TickData();
			List<TickData> ticks = new List<TickData>();

			if (e.SummaryMessage.Level1.LastTradeTime == DateTime.MinValue)
			{
				Console.WriteLine("UpdateMessage DateTime.MinValue encountered for tickType " + e.SummaryMessage.Level1.UpdateType.ToString());
                _logger.Warning("UpdateMessage DateTime.MinValue encountered for tickType {TickType}", e.SummaryMessage.Level1.UpdateType.ToString());
			}

			switch (e.SummaryMessage.Level1.UpdateType)
			{
				case UpdateType.AskUpdate:
					tick.tickType = TickType.Ask;
					tick.size = (ulong)e.SummaryMessage.Level1.AskSize;
					tick.price = e.SummaryMessage.Level1.Ask;
					tick.time = e.SummaryMessage.Level1.LastTradeTime;
					ticks.Add(tick);
					break;

				case UpdateType.BidUpdate:
					tick.tickType = TickType.Bid;
					tick.size = (ulong)e.SummaryMessage.Level1.BidSize;
					tick.price = e.SummaryMessage.Level1.Bid;
					tick.time = e.SummaryMessage.Level1.LastTradeTime;
					ticks.Add(tick);

					// I never get an "AskUpdate" it appears, so I'm going to update
					// the bid and ask here
					TickData askTick = new TickData();
					askTick.tickType = TickType.Ask;
					askTick.size = (ulong)e.SummaryMessage.Level1.AskSize;
					askTick.price = e.SummaryMessage.Level1.Ask;
					askTick.time = e.SummaryMessage.Level1.LastTradeTime;
					ticks.Add(askTick);
					break;

				case UpdateType.TradeUpdate:
				case UpdateType.ExtendedTradeUpdate:
					tick.tickType = TickType.Trade;
					tick.size = e.SummaryMessage.Level1.LastSize;
					tick.price = e.SummaryMessage.Level1.LastPrice;
					tick.time = e.SummaryMessage.Level1.LastTradeTime;
					ticks.Add(tick);
					break;

				default:
					break;
			}

			if (ticks.Count > 0)
			{
				Symbol symbol = symbolMapping[e.SummaryMessage.Level1.SymbolString];
				ProcessTicks(symbol, ticks, e.SummaryMessage.Message);
			}
		}

		void iqFeed_IQSummaryMessage(object sender, IQSummaryEventArgs e)
		{
			if (!connected)
			{
				return;
			}

			TickData bid = new TickData();
			TickData ask = new TickData();
			List<TickData> ticks = new List<TickData>();

			bid.tickType = TickType.Bid;
			ask.tickType = TickType.Ask;

			bid.price = e.SummaryMessage.Level1.Bid;
			ask.price = e.SummaryMessage.Level1.Ask;
			bid.size = (ulong)e.SummaryMessage.Level1.BidSize;
			ask.size = (ulong)e.SummaryMessage.Level1.AskSize;

			if (e.SummaryMessage.Level1.LastTradeTime == DateTime.MinValue)
			{
				Console.WriteLine("Summary Message DateTime.MinValue encountered for tickType " + e.SummaryMessage.Level1.UpdateType.ToString());
                _logger.Warning("Summary Message DateTime.MinValue encountered for tickType {TickType}", e.SummaryMessage.Level1.UpdateType.ToString());
			}

			if (e.SummaryMessage.Level1.LastTradeTime != DateTime.MinValue)
			{
				bid.time = ask.time = e.SummaryMessage.Level1.LastTradeTime;
				lastGoodTickTime = e.SummaryMessage.Level1.LastTradeTime;
			}
			else
			{
				bid.time = ask.time = lastGoodTickTime;
			}

			TickData totalVolume = new TickData();
			totalVolume.size = e.SummaryMessage.Level1.TotalVolume;
			totalVolume.time = lastGoodTickTime;
			totalVolume.tickType = TickType.DailyVolume;

			TickData lastPrice = new TickData();
			lastPrice.tickType = TickType.Trade;
			lastPrice.time = lastGoodTickTime;
			lastPrice.price = e.SummaryMessage.Level1.LastPrice;

			TickData prevClose = new TickData();
			prevClose.tickType = TickType.PreviousClose;
			prevClose.time = lastGoodTickTime;
			prevClose.price = e.SummaryMessage.Level1.LastPrice - e.SummaryMessage.Level1.TodaysChange;

			TickData open = new TickData();
			open.tickType = TickType.OpenPrice;
			open.time = lastGoodTickTime;
			open.price = e.SummaryMessage.Level1.Open;

			TickData high = new TickData();
			high.tickType = TickType.HighPrice;
			high.time = lastGoodTickTime;
			high.price = e.SummaryMessage.Level1.High;

			TickData low = new TickData();
			low.tickType = TickType.LowPrice;
			low.price = e.SummaryMessage.Level1.Low;
			low.time = lastGoodTickTime;

			ticks.Add(bid);
			ticks.Add(ask);
			ticks.Add(totalVolume);
			ticks.Add(lastPrice);
			ticks.Add(prevClose);
			ticks.Add(open);
			ticks.Add(high);
			ticks.Add(low);

			Symbol symbol = symbolMapping[e.SummaryMessage.Level1.SymbolString];
			ProcessTicks(symbol, ticks, e.SummaryMessage.Message);

		}

		//void iqFeed_IQStatusChanged(object sender, IQFeedEventArgs e)
		//{
		//    if (e.IQFeedStatus == IQFeedStatusTypes.ConnectionOK)
		//    {
		//        connectDone.Set();
		//    }
		//}

		#region IBarDataRetrieval Members

		public List<BarData> RetrieveData(Symbol symbol, int frequency, DateTime startDate, DateTime endDate, BarConstructionType barConstruction)
		{
			ClearError();

            if (!connected)
            {
                lastError = "Not connected";
                hadError = true;
                return null;
            }

			ReturnValue<List<BarData>> ret = iqFeed.GetHistoricalBarData(symbol, frequency, startDate, endDate);
			if (!ret.Success)
			{
				lastError = ret.ReturnCode.Message;
				hadError = true;
				return null;
			}

			//	Filter out bars outside of the time frame we requested.
			List<BarData> bars = new List<BarData>(ret.Value.Count);
			foreach (BarData bar in ret.Value)
			{
				if (bar.BarStartTime >= startDate && bar.BarStartTime <= endDate)
				{
					bars.Add(bar);
				}
			}

			if (_settings.IgnoreLastHistBar && bars.Count > 0)
			{
				bars.RemoveAt(bars.Count - 1);
			}

			return bars;
		}

		#endregion

        Dictionary<Symbol, DateTime> _lastLoggedTickTimes = new Dictionary<Symbol, DateTime>();
        Dictionary<Symbol, DateTime> _lastLoggedTickLocalTimes = new Dictionary<Symbol, DateTime>();
        bool _isOutOfOrder;

		private void ProcessTicks(Symbol symbol, List<TickData> ticks, string rawMessage)
		{
            DateTime lastTickTime;
            DateTime lastTickLocalTime;

            if (!_lastLoggedTickTimes.TryGetValue(symbol, out lastTickTime))
            {
                lastTickTime = DateTime.MinValue;
            }

            if (!_lastLoggedTickLocalTimes.TryGetValue(symbol, out lastTickLocalTime))
            {
                lastTickLocalTime = DateTime.MinValue;
            }

            DateTime tickTime = ticks.First().time;
            TimeSpan period = TimeSpan.FromMinutes(1);
            if (tickTime - lastTickTime >= period ||
                DateTime.Now - lastTickLocalTime >= period)
            {
                _lastLoggedTickTimes[symbol] = tickTime;
                _lastLoggedTickLocalTimes[symbol] = DateTime.Now;

                _logger.Information("{Symbol} {TickType} @{TickPrice} Time: {TickTime} Time diff: {TickTimeDiff} Raw Message: {RawMessage}",
                    symbol, ticks.First().tickType, ticks.First().price, tickTime, tickTime - DateTime.Now , rawMessage);
            }

			if (tickListener != null)
			{
				foreach (TickData t in ticks)
				{
					TickData tick = t;

                    if (_settings.FilterUsingCurrentTime && (tick.time - DateTime.Now) > (_settings.ExchangeTimeDiff + _settings.MaxTimeDelta))
                    {
                        _logger.Warning("Filtered tick due to future timestamp: {Symbol} {TickType} @{TickPrice} Time: {TickTime}", symbol, ticks.First().tickType, ticks.First().price, tick.time);
                        continue;
                    }

					if (tick.time < _lastTickTime)
					{
                        if (!_isOutOfOrder)
                        {
                            _isOutOfOrder = true;
                            _logger.Warning("Out of order tick: {Symbol} {TickType} @{TickPrice} Time: {TickTime}", symbol, ticks.First().tickType, ticks.First().price, tickTime);
                        }

                        //  Summary messages look like they use the timestamp of the last tick for a symbol, so they may not be in order
                        if (rawMessage.StartsWith("P,"))
                        {
                            _logger.Information("Adjusting summary message tick time from {RecievedTickTime} to {AdjustedTickTime} for symbol {Symbol}", tick.time, _lastTickTime, symbol);
                            //	Avoid sending out of order ticks for summary messages
                            tick.time = _lastTickTime;
                        }
					}
					else
					{
						_lastTickTime = tick.time;
                        _isOutOfOrder = false;
					}

                    tickListener(symbol, tick);					
				}
			}
		}

        private bool CreateIQFeed()
        {
            bool success = true;

            iqFeed = new IQFeed();
            //iqFeed.IQStatusChanged += new EventHandler<IQFeedEventArgs>(iqFeed_IQStatusChanged);
            if (iqFeed.Connect(UserName, Password))
            {
                iqFeed.IQSummaryMessage += new EventHandler<IQSummaryEventArgs>(iqFeed_IQSummaryMessage);
                iqFeed.IQUpdateMessage += new EventHandler<IQSummaryEventArgs>(iqFeed_IQUpdateMessage);
                iqFeed.IQTimeMessage += new EventHandler<IQTimeEventArgs>(iqFeed_IQTimeMessage);
                iqFeed.Disconnected += iqFeed_Disconnected;

                //for (int index = 0; index < 100; index++)
                //{
                //    Application.DoEvents();
                //    if (connectDone.WaitOne(100, false))
                //    {
                //        break;
                //    }
                //}
            }
            else
            {
                lastError = "Unable to connect.";
                success = false;
            }

            return success;
        }

        void iqFeed_Disconnected(Exception ex)
        {
            _logger.Warning("Disconnected: {Exception}", ex);
            
            connected = false;

            var args = new ServiceEventArgs();
            args.EventType = ServiceEventType.Disconnected;
            args.Message = ex.Message;

            RaiseServiceEvent(args);

        }

        private void RaiseServiceEvent(ServiceEventArgs args)
        {
            var eventHandler = ServiceEvent;
            if (eventHandler != null)
            {
                eventHandler(this, args);
            }
        }

	}
}
