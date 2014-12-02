using System;
using System.Collections.Generic;
using System.Text;
using RightEdge.Common;
using System.Linq;
using System.Threading;
using System.Diagnostics;

namespace RightEdge.DataRetrieval
{
	public class RandomDataService : IService, ITickRetrieval
	{
		GotTickData tickListener = null;
		Dictionary<Symbol, TradeGenInfo> watchedSymbols = new Dictionary<Symbol, TradeGenInfo>();
		private object lockObject = new object();
		private bool isWatching = false;
		Thread runningThread = null;

		public bool SequentialPrices { get; set; }

		public RandomDataService()
		{
			//SequentialPrices = true;
		}


		#region IService Members

		public event EventHandler<ServiceEventArgs> ServiceEvent;

		public bool Initialize(SerializableDictionary<string, string> settings)
		{
			return true;
		}

		public bool HasCustomSettings()
		{
			return false;
		}

		public bool ShowCustomSettingsForm(ref SerializableDictionary<string, string> settings)
		{
			return true;
		}

		public string ServiceName()
		{
			return "Random Data";
		}

		public string Author()
		{
			return "Yye Software";
		}

		public string Description()
		{
			return "Generates random price data";
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
			return "{19B6986B-0E1E-404f-BE43-CB9E2DECF143}";
		}

		public bool NeedsServerAddress()
		{
			return false;
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
				return false;
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
			return null;
		}

		public bool Connect(ServiceConnectOptions connectOptions)
		{
			return true;
		}

		public bool Disconnect()
		{
			return true;
		}

		public string GetError()
		{
			return "";
		}

		#endregion

		#region IDisposable Members

		public void Dispose()
		{
			StopWatching();
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
			lock (lockObject)
			{
				Dictionary<Symbol, TradeGenInfo> newDict = new Dictionary<Symbol, TradeGenInfo>();
				foreach (Symbol symbol in symbols)
				{
					if (watchedSymbols.ContainsKey(symbol))
					{
						newDict[symbol] = watchedSymbols[symbol];
					}
					else
					{
						newDict[symbol] = new TradeGenInfo(30) { SequentialPrices = SequentialPrices };
					}
				}
				watchedSymbols = newDict;
			}
			return true;
		}

		public bool IsWatching()
		{
			return isWatching;
		}

		public bool StartWatching()
		{
			if (!isWatching)
			{
				isWatching = true;
				runningThread = new Thread(new ThreadStart(ThreadFunc));
				runningThread.Start();
			}
			return true;
		}

		public bool StopWatching()
		{
			if (isWatching)
			{
				isWatching = false;
				if (!runningThread.Join(200))
				{
					runningThread.Abort();
				}
			}
			return true;
		}

		public IService GetService()
		{
			return this;
		}

		#endregion

		struct SymbolTick
		{
			public Symbol symbol;
			public TickData tick;
		}

		void ThreadFunc()
		{
			bool sendOutOfOrderTicks = false;
			Symbol bugSymbol = null;
			DateTime mostRecentSentTime = DateTime.MinValue;
			Random rnd = new Random();
			DateTime startTime = DateTime.Now;
			long ticksPerSecond = 2000;
			if (Port != 0)
			{
				ticksPerSecond = Port;
			}
			long ticksSent = 0;

			Queue<SymbolTick> ticks = new Queue<SymbolTick>();

			while (isWatching)
			{
				if (ticksSent > ticksPerSecond)
				{
					long seconds = ticksSent / ticksPerSecond;
					ticksSent = ticksSent % ticksPerSecond;
					startTime = startTime.AddSeconds(seconds);
				}

				TimeSpan timePassed = DateTime.Now.Subtract(startTime);
				long ticksToSend = (long)(timePassed.TotalSeconds * ticksPerSecond);

				if (ticksToSend <= 0 || watchedSymbols.Count == 0)
				{
					Thread.Sleep(5);
					continue;
				}

				//	Send ticks outside of lock to avoid deadlock
				lock (lockObject)
				{
					while (ticks.Count < ticksToSend)
					{
						if (bugSymbol == null)
						{
							bugSymbol = watchedSymbols.Keys.LastOrDefault();
						}

						foreach (Symbol symbol in watchedSymbols.Keys)
						{
							TradeGenInfo info = watchedSymbols[symbol];

							if (/*info.TickAvailable() &&*/
								!sendOutOfOrderTicks ||
								(symbol != bugSymbol || info.LastTime < DateTime.Now.AddMilliseconds(-60)))
							{
								double price = info.GeneratePrice();
								int size = info.GenerateVolume();

								SymbolTick st = new SymbolTick();

								TickData tick = new TickData();
								if (sendOutOfOrderTicks && symbol == bugSymbol)
								{
									tick.time = DateTime.Now.AddMilliseconds(-40);
									if (tick.time < info.LastTime)
									{
										tick.time = info.LastTime;
									}
								}
								else
								{
									tick.time = DateTime.Now;
								}
								tick.price = price;
								tick.size = (UInt64)size;
								tick.tickType = TickType.Trade;

								st.symbol = symbol;
								st.tick = tick;

								if (symbol == bugSymbol)
								{
									//Trace.WriteLine(symbol.ToString() + ": " + tick.time);
								}

								ticks.Enqueue(st);

								info.LastTime = tick.time;
								if (info.LastTime > mostRecentSentTime)
								{
									mostRecentSentTime = info.LastTime;
								}

							}
						}
					}
				}
				if (tickListener != null)
				{
					while (ticks.Count > 0 && ticksToSend > 0)
					{
						SymbolTick st = ticks.Dequeue();
						tickListener(st.symbol, st.tick);
						ticksToSend--;
						ticksSent++;
					}
				}
			}
		}
	}

	class TradeGenInfo
	{
		// This time constant assumes that we're asking
		// for a price every 1/2 second
		// 250 trading days, 8 hours per day of trading, 7200 half seconds per hour
		private double timeConstant = (1.0 / (250.0 * 8.0 * 7200));
		private double tickFrequency = 1.0;
		private Random randTick = new Random();

		public DateTime LastTime { get; set; }

		public bool SequentialPrices { get; set; }

		private double lastPrice = 0;
		public double LastPrice
		{
			get
			{
				return lastPrice;
			}
		}

		private double annualReturn;
		public double AnnualReturn
		{
			get
			{
				return annualReturn;
			}
			set
			{
				annualReturn = value;
			}
		}

		private double annualVolatility;
		public double AnnualVolatility
		{
			get
			{
				return annualVolatility;
			}
			set
			{
				annualVolatility = value;
			}
		}

		public TradeGenInfo()
		{
			// Generate all variables randomly
			Initialize();
		}

		public TradeGenInfo(double startingPrice)
		{
			Initialize();
			this.lastPrice = startingPrice;
		}

		public bool TickAvailable()
		{
			Random rand = new Random();
			if (rand.NextDouble() < tickFrequency)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		public double GeneratePrice()
		{
			if (SequentialPrices)
			{
				long fraction = ((long) Math.Floor(lastPrice * 1000)) % 1000;
				fraction++;
				fraction = fraction % 1000;

				lastPrice = Math.Floor(lastPrice) + (fraction / 1000.0);
				return lastPrice;
			}
			else
			{
				double randStdNormalDist = GenerateRandomSampleNormalDistribution();

				// Geometric Brownian Formula
				// Price * (1 + (Annual return * Time Constant) + (Volatility * RandomStdNormDist * Sqrt(Time Constant)))
				double price = lastPrice * (1 + (annualReturn * timeConstant) + (annualVolatility * randStdNormalDist * Math.Sqrt(timeConstant)));
				lastPrice = price;

				return Math.Round(price * 100) / 100.00;
			}
		}

		public int GenerateVolume()
		{
			return randTick.Next(10, 10000);
		}

		private void Initialize()
		{
			this.lastPrice = GenerateStartingPrice();
			this.annualReturn = GenerateAnnualReturn();
			this.tickFrequency = GenerateFrequency();
			this.annualVolatility = GenerateVolatility();
		}

		private double GenerateVolatility()
		{
			byte[] bytes = Guid.NewGuid().ToByteArray();
			int seed = bytes[0] & bytes[1];
			Random rand = new Random(seed);
			int val = rand.Next(3, 80);

			return (double)Convert.ToDouble(val) / 100.0;
		}

		private double GenerateAnnualReturn()
		{
			byte[] bytes = Guid.NewGuid().ToByteArray();
			int seed = bytes[0] & bytes[1];
			Random rand = new Random(seed);
			int val = rand.Next(-50, 50);

			return (double)Convert.ToDouble(val) / 100.0;
		}

		private double GenerateFrequency()
		{
			byte[] bytes = Guid.NewGuid().ToByteArray();
			int seed = bytes[0] & bytes[1];
			Random rand = new Random(seed);
			return rand.NextDouble();
		}

		private double GenerateStartingPrice()
		{
			byte[] bytes = Guid.NewGuid().ToByteArray();
			int seed = bytes[0] & bytes[1];

			Random rand = new Random(seed);
			double rndPrice = (double)rand.Next(15, 100);
			rndPrice += ((double)rand.Next(0, 99) / 100.0);

			return rndPrice;
		}

		/// <summary>
		/// Approximates a random sample from a normal distribution
		/// </summary>
		/// <returns></returns>
		private double GenerateRandomSampleNormalDistribution()
		{
			double sum = 0.0;

			for (int index = 0; index < 12; index++)
			{
				sum += randTick.NextDouble();
			}

			double sample = sum - 6.0;

			//sample = -1.516355517;

			return sample;
		}
	}
}
