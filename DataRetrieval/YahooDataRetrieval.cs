using System;
using System.Net;
using System.IO;
using System.Text;
using System.Globalization;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

using RightEdge.Common;
using RightEdge.Common.Internal;


namespace RightEdge.DataRetrieval
{
	public class YahooDataService : IService, IBarDataRetrieval
	{
		string lastError = "";
		bool useAdjustedClose = true;

		#region IDisposable Members

		public void Dispose()
		{

		}

		#endregion

		#region IService Members

		public event EventHandler<ServiceEventArgs> ServiceEvent;

		public bool Initialize(SerializableDictionary<string, string> settings)
		{
			string adjustedClose = "";

			if (settings.TryGetValue("UseAdjustedClose", out adjustedClose))
			{
				useAdjustedClose = Convert.ToBoolean(adjustedClose);
			}

			return true;
		}

		public bool HasCustomSettings()
		{
			return true;
		}

		public bool ShowCustomSettingsForm(ref SerializableDictionary<string, string> settings)
		{
			UseAdjustedCloseForm dlg = new UseAdjustedCloseForm();

			string adjustedClose = "";

			if (settings.TryGetValue("UseAdjustedClose", out adjustedClose))
			{
				useAdjustedClose = Convert.ToBoolean(adjustedClose);
			}

			dlg.UseAdjustedClose = useAdjustedClose;

			if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				useAdjustedClose = dlg.UseAdjustedClose;
				settings["UseAdjustedClose"] = useAdjustedClose.ToString();
			}

			return true;
		}

		public string ServiceName()
		{
			return "Yahoo";
		}

		public string Author()
		{
			return "Yye Software";
		}

		public string Description()
		{
			return "Retrieves end of day data from the Yahoo Finance web site.";
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
			return "{99BDBE09-A457-42a9-8BC7-E471272B2F21}";
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
			get { return false; }
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
			return null;
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
		//    ret.Add((int) BarFrequency.Daily);
		//    ret.Add((int)BarFrequency.Monthly);
		//    ret.Add((int)BarFrequency.Weekly);
		//    return ret;
		//}

		public List<BarData> RetrieveData(Symbol symbol, int frequency, DateTime startDate, DateTime endDate, BarConstructionType barConstruction)
		{
			if (frequency != (int) BarFrequency.Daily &&
				frequency != (int) BarFrequency.Weekly &&
				frequency != (int) BarFrequency.Monthly)
			{
				return new List<BarData>();
			}

			if (symbol.AssetClass == AssetClass.Forex)
			{
				return GetForexData(symbol, frequency, startDate, endDate);
			}

			if (startDate == DateTime.MinValue)
			{
				// I think this is about as far back as Yahoo will go
				startDate = new DateTime(1960, 1, 1);
			}

			string frequencyString = "&g=d";

			if (frequency == (int)BarFrequency.Weekly)
			{
				frequencyString = "&g=w";
			}

			if (frequency == (int)BarFrequency.Monthly)
			{
				frequencyString = "&g=m";
			}

			int startMonth = startDate.Month - 1;
			int endMonth = endDate.Month - 1;

			string baseURL = "http://ichart.finance.yahoo.com/table.csv?";
			string url = baseURL + "s=" + symbol.Name + "&a=" + startMonth.ToString() + "&b=" + startDate.Day.ToString() +
				"&c=" + startDate.Year.ToString() + "&d=" + endMonth.ToString() + "&e=" + endDate.Day +
				"&f=" + endDate.Year.ToString() + frequencyString + "&ignore=.csv";

			WebResponse result = null;
			WebRequest req = WebRequest.Create(url);
			result = req.GetResponse();
			Stream receiveStream = result.GetResponseStream();
			Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
			StreamReader sr = new StreamReader(receiveStream, encode);

			string pageString = sr.ReadToEnd();

			sr.Close();

			List<BarData> bars = new List<BarData>();

			if ((bars.Count >= 2) && (bars[0].BarStartTime == bars[1].BarStartTime)) // when last day bar is duplicated
			{
				bars.RemoveAt(1); // removing bar at index 1 (bars are reversed), bar at index 0 better reflect price changes during the day
			} 

			PutRawDataInBarCollection(pageString, bars);

			List<BarData> ret = new List<BarData>();
			//	Sort bars and remove duplicates
			foreach (BarData bar in bars.OrderBy(b => b.BarStartTime))
			{
				if (ret.Count > 0 && ret[ret.Count - 1].BarStartTime == bar.BarStartTime)
				{
					//	Avoid duplicate bars (but take the "later" one)
					ret[ret.Count - 1] = bar;
				}
				else
				{
					ret.Add(bar);
				}
			}

			return ret;

			//return bars.OrderBy(b => b.BarStartTime).ToList();

		}

		List<BarData> GetForexData(Symbol symbol, int frequency, DateTime startDate, DateTime endDate)
		{
			string symbolName = symbol.Name.Replace("/", "");
			string baseURL = "http://chartapi.finance.yahoo.com/instrument/1.0/" + symbolName + "=x/chartdata;type=quote;ys=";
			List<BarData> bars = new List<BarData>();

			if (startDate == DateTime.MinValue || startDate.Year < 1999)
			{
				startDate = new DateTime(1999, 1, 1);
			}

			if (endDate == DateTime.MaxValue)
			{
				endDate = DateTime.Now;
			}

			int startYear = startDate.Year;
			int endYear = endDate.Year;

			for (int year = startYear; year < endYear + 1; year += 2)
			{
				string url = baseURL + year.ToString() + ";";
				if (frequency == (int)BarFrequency.Monthly)
				{
					url += "yz=3/csv";
				}
				else
				{
					url += "yz=2/csv";
				}

				bars.AddRange(GetYear(url));
			}

            List<BarData> ret = new List<BarData>();

            //  The separate downloads may have overlapping data - Yahoo seems to return 3 years instead of 2
            DateTime lastBarStart = DateTime.MinValue;
            foreach (var b in bars.Where(p => p.BarStartTime >= startDate && p.BarStartTime <= endDate).OrderBy(p => p.BarStartTime))
            {
                if (b.BarStartTime == lastBarStart)
                {
                    continue;
                }
                ret.Add(b);
                lastBarStart = b.BarStartTime;
            }

            return ret;
		}

		List<BarData> GetYear(string url)
		{
			WebResponse result = null;
			WebRequest req = WebRequest.Create(url);
			result = req.GetResponse();
			Stream receiveStream = result.GetResponseStream();
			Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
			StreamReader sr = new StreamReader(receiveStream, encode);

			string pageString = sr.ReadToEnd();

			sr.Close();
			List<BarData> bars = new List<BarData>();
			PutRawForexDataInBarCollection(pageString, bars);

			return bars;
		}

		void PutRawForexDataInBarCollection(string pageString, List<BarData> bars)
		{
			int index = pageString.IndexOf("volume:");
			index = pageString.IndexOf("\n", index);
			string webPage = pageString.Substring(index + 1);

			string[] lines = webPage.Split('\n');

			foreach (string line in lines)
			{
				string[] items = line.Split(',');
				if (items.Count() < 5)
				{
					continue;
				}

				double temp;

				IFormatProvider culture = new CultureInfo("en-US");

				// need to set culture info for non-US folks since we're getting
				// data from a US source
				if (double.TryParse(items[1], NumberStyles.AllowDecimalPoint, culture, out temp))
				{
					BarData bar = new BarData();
					string format = "yyyyMMdd";
					bar.BarStartTime = DateTime.ParseExact(items[0], format, culture);
					bar.Close = double.Parse(items[1], NumberStyles.Number, culture);
					bar.High = double.Parse(items[2], NumberStyles.Number, culture);
					bar.Low = double.Parse(items[3], NumberStyles.Number, culture);
					bar.Open = double.Parse(items[4], NumberStyles.Number, culture);

					string error;
					if (BarUtils.IsValidBar(bar, out error))
					{
						bars.Add(bar);
					}
				}
			}
		}

		public IService GetService()
		{
			return this;
		}

		#endregion

		private void PutRawDataInBarCollection(string webPage, List<BarData> bars)
		{
			string[] lines = webPage.Split('\n');

			foreach (string line in lines)
			{
				string[] items = line.Split(',');

				if (items.Length >= 7)
				{
					double temp;

					IFormatProvider culture = new CultureInfo("en-US");

					// need to set culture info for non-US folks since we're getting
					// data from a US source
					if (double.TryParse(items[1], NumberStyles.AllowDecimalPoint, culture, out temp))
					{
						BarData bar = new BarData();
						bar.BarStartTime = DateTime.Parse(items[0], culture);

						//  We need to calculate a factor so we can
						//  to normalize the open/high/low/close.
						//  This is necesary because of splits and dividends

						double realClose = double.Parse(items[4], NumberStyles.Number, culture);

						if (useAdjustedClose)
						{
							double adjustedClose = double.Parse(items[6], NumberStyles.Number, culture);
							double adjustmentFactor = realClose / adjustedClose;

							bar.Open = double.Parse(items[1], NumberStyles.Number, culture) / adjustmentFactor;
							bar.High = double.Parse(items[2], NumberStyles.Number, culture) / adjustmentFactor;
							bar.Low = double.Parse(items[3], NumberStyles.Number, culture) / adjustmentFactor;
							bar.Close = double.Parse(items[4], NumberStyles.Number, culture) / adjustmentFactor;
							bar.Volume = (ulong)(ulong.Parse(items[5], NumberStyles.Number, culture) * adjustmentFactor);
						}
						else
						{
							bar.Open = double.Parse(items[1], NumberStyles.Number, culture);
							bar.High = double.Parse(items[2], NumberStyles.Number, culture);
							bar.Low = double.Parse(items[3], NumberStyles.Number, culture);
							bar.Close = double.Parse(items[4], NumberStyles.Number, culture);
							bar.Volume = (ulong)(ulong.Parse(items[5], NumberStyles.Number, culture));
						}

						//	Ignore bars which don't have arounded timestamp
						if (bar.BarStartTime.TimeOfDay == TimeSpan.Zero)
						{
							bars.Add(bar);
						}
					}
					else
					{
						Trace.WriteLine("Error parsing data from source.  Source line = " + line);
					}
				}
			}
		}
	}
}
