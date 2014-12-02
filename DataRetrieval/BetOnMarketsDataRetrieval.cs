using System;
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Diagnostics;
using System.Collections.Generic;

using RightEdge.Common;

namespace RightEdge.DataRetrieval
{
	public class BetOnMarketsDataRetrieval : IService, IBarDataRetrieval, ITickRetrieval
	{
		string lastError = "";
		GotTickData tickListener = null;
		List<Symbol> watchedSymbols = new List<Symbol>();
		private object lockObject = new object();
		private bool isWatching = false;
		Thread runningThread = null;
		Dictionary<Symbol, TickData> previousTicks = new Dictionary<Symbol, TickData>();

		#region IDisposable Members

		public void Dispose()
		{
			StopWatching();
		}

		#endregion

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
			return "Bet On Markets";
		}

		public string Author()
		{
			return "Yye Software";
		}

		public string Description()
		{
			return "Retrieves end of day data or tick data from BetOnMarkets.com.";
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
			return "{DD801773-28B5-4d91-8BB0-473E5E79EBB1}";
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
				return null;
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
				return null;
			}
			set
			{

			}
		}

		public string Password
		{
			get
			{
				return null;
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
			get { return false; }
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
			return lastError;
		}

		#endregion

		#region IBarDataRetrieval Members

		//public List<int> GetAvailableFrequencies()
		//{
		//    List<int> ret = new List<int>();
		//    ret.Add((int)BarFrequency.Daily);
		//    return ret;
		//}

		public List<BarData> RetrieveData(Symbol symbol, int frequency, DateTime startDate, DateTime endDate, BarConstructionType barConstruction)
		{
			if (frequency != (int)BarFrequency.Daily)
			{
				return new List<BarData>();
			}

			// set the url
			string baseURL = "https://pic.betonmarkets.com/cgi-m/api_dataserver.cgi?";
			string url = baseURL + "p=h65jb39" + "&gzip=1" + "&instr=" + symbol.Name +
				"&scale=daily";

			if (startDate != DateTime.MinValue)
			{
				url = url + "&ignoreuntil=" + startDate.ToString("d-MMM-yy");
			}

			// get the raw data from the web
			string pageString = GetRawData(url);

			// create a list of bardata and parse string into bardata
			List<BarData> bars = new List<BarData>();
			PutRawDataInBarCollection(pageString, bars);

			return bars;
		}

		public IService GetService()
		{
			return this;
		}

		#endregion

		private void PutRawDataInBarCollection(string webPage, List<BarData> bars)
		{
			string[] lines = webPage.Split('\n');

			if (!lines[0].ToLower().Contains("error"))
			{
				foreach (string line in lines)
				{
					string[] items = line.Split('\u0020');

					double temp;

					IFormatProvider culture = new CultureInfo("en-US");

					if (items.Length > 1 && double.TryParse(items[1], NumberStyles.AllowDecimalPoint, culture, out temp))
					{
						BarData bar = new BarData();

						bar.BarStartTime = DateTime.Parse(items[0], culture);
						bar.Open = double.Parse(items[1], NumberStyles.Number, culture);
						bar.High = double.Parse(items[2], NumberStyles.Number, culture);
						bar.Low = double.Parse(items[3], NumberStyles.Number, culture);
						bar.Close = double.Parse(items[4], NumberStyles.Number, culture);

						bars.Add(bar);
					}
					else
					{
						Trace.WriteLine("Error parsing data from source.  Source line = " + line);
					}
				}
			}
			else
			{
				Trace.WriteLine("Error retrieving data from source. " + lines[0]);
				throw new Exception(lines[0]);
			}
		}
		
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
			watchedSymbols = symbols;
			foreach (Symbol s in symbols)
			{
				bool exists = false;
				foreach (KeyValuePair<Symbol, TickData> kvp in previousTicks)
				{
					if (kvp.Key == s)
					{
						exists = true;
						break;
					}
				}

				if (!exists)
				{
					TickData tick = new TickData();
					previousTicks.Add(s, tick);
				}
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
				runningThread.Join();
			}
			return true;
		}

		#endregion

		private void ThreadFunc()
		{
			while (isWatching)
			{
				// pull a new file from the internet every 2 seconds
				Thread.Sleep(2000);

				// set the url
				string baseURL = "https://pic.betonmarkets.com/cgi-m/api_dataserver.cgi?";

				foreach (Symbol symbol in watchedSymbols)
				{
					string url = baseURL + "p=h65jb39" + "&gzip=1" + "&instr=" + symbol.Name +
						"&scale=tick&date=" + DateTime.Now.Date.ToString("d-MMM-yy");
					
					// get the previous tick
					TickData previousTick = previousTicks[symbol];

					// if there is a previous tick, only get the latest ticks
					if (previousTick.time != new DateTime())
					{
						url = url + "&ignoreuntil=" + previousTick.time.ToUniversalTime().TimeOfDay.ToString();
					}

					// get the raw data from the web
					string pageString = GetRawData(url);
										
					// parse the string and get the new ticks
					List<TickData> newTicks = GetNewTicksFromRawData(pageString);
					
					// if there was new data
					if(newTicks.Count > 0)
					{
						// set the previous tick to the last tick pulled
						previousTicks[symbol] = newTicks[newTicks.Count - 1];

						if (tickListener != null)
						{
							// add the new ticks to the listener
							foreach (TickData tick in newTicks)
							{
								tickListener(symbol, tick);
							}
						}
					}
				}
			}
		}

		private List<TickData> GetNewTicksFromRawData(string webPage)
		{
			// create a new tickdata object
			List<TickData> newTicks = new List<TickData>();

			if (webPage != "")
			{
				// split the lines in the string
				string[] lines = webPage.Split('\n');

				// if there is no error, parse the data
				if (!lines[0].ToLower().Contains("error"))
				{
					IFormatProvider culture = new CultureInfo("en-US");

					// parse each lines data
					foreach (string line in lines)
					{
						string[] items = line.Split('\u0020');

						double temp;

						// if this line contains data and we can parse that data, continue parsing
						if (items.Length > 1 && double.TryParse(items[1], out temp))
						{
							DateTime tickTime = DateTime.Parse(items[0], culture);
							// convert from GMT to current time zone
							TimeSpan offset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);
							tickTime = tickTime.Add(offset);

							TickData tick = new TickData();
							// the current tick data is today
							// the time is given in the data
							tick.time = tickTime;
							tick.tickType = TickType.Trade;
							tick.price = double.Parse(items[1], NumberStyles.Number, culture);
							tick.size = 0;

							newTicks.Add(tick);
						}
						else
						{
							Trace.WriteLine("Error parsing data from source.  Source line = " + line);
						}
					}
				}
				else
				{
					Trace.WriteLine("Error retrieving data from source. " + lines[0]);
				}
			}
			return newTicks;
		}

		private string GetRawData(string url)
		{
			string pageString = "";

			// send the URL
			HttpWebResponse result = null;
			HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(url);
			// set the decompression type
			req.AutomaticDecompression = DecompressionMethods.GZip;
			// get response from url
			result = (HttpWebResponse)req.GetResponse();

			// get response as a stream and create a GZip decompression stream
			Stream responseStream = result.GetResponseStream();
			GZipStream reader = new GZipStream(responseStream, CompressionMode.Decompress);

			// create a memory stream and binary writer for this memory stream
			MemoryStream ms = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(ms, System.Text.Encoding.UTF7);

			// read the bytes from the GZip stream
			// write them to the memory stream with the specified encoding
			int data = reader.ReadByte();
			byte byteMe = (byte)data;

			do
			{
				writer.Write(byteMe);
				data = reader.ReadByte();
				byteMe = (byte)data;
			}
			while (data != -1);

			// set the position of the memory stream back to zero and read to string
			ms.Position = 0;
			StreamReader rd = new StreamReader(ms);
			pageString = rd.ReadToEnd();

			// close all open streams;
			writer.Close();
			reader.Close();
			rd.Close();

			return pageString;
		}
	}
}
