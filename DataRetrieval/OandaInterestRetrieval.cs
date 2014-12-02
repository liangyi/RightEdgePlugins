using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;

using RightEdge.Common;
using System.Globalization;
using System.Diagnostics;

namespace RightEdge.DataRetrieval
{
	public class OandaInterestRetrieval : IService, IBarDataRetrieval
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
			return true;
		}

		public bool HasCustomSettings()
		{
			return false;
		}

		public bool ShowCustomSettingsForm(ref SerializableDictionary<string, string> settings)
		{
			return false;
		}

		public string ServiceName()
		{
			return "Oanda Interest Rates";
		}

		public string Author()
		{
			return "Yye Software";
		}

		public string Description()
		{
			return "Retrieves interest rate information from Oanda.";
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
			return "{56801ED6-B14E-45f3-A813-F2884E103861}";
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
		//    ret.Add((int)BarFrequency.Daily);
		//    return ret;
		//}

		public List<BarData> RetrieveData(Symbol symbol, int frequency, DateTime startDate, DateTime endDate, BarConstructionType barConstruction)
		{
			//	Ignore frequency for now

			//	Make sure interest rate information is being requested
			if (symbol.AssetClass != AssetClass.InterestRate)
			{
				lastError = "This plugin only supports downloading interest rates.";
				return null;
			}

			if (startDate == DateTime.MinValue)
			{
				//	Oanda doesn't seem to have data before 2001 anyway
				startDate = new DateTime(1960, 1, 1);
			}

			if (endDate > DateTime.Now.AddYears(5))
			{
				endDate = DateTime.Now.AddYears(5);
			}

			

			string baseURL = "https://fx1.oanda.com/mod_perl/user/interestrates.pl?";
			string url = baseURL + "startdate=" + startDate.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) + "&enddate=" + endDate.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) +
				"&currency=" + symbol.Name;

			List<BarData> ret = new List<BarData>();

			WebRequest request = WebRequest.Create(url);
			WebResponse response = request.GetResponse();
			using (StreamReader sr = new StreamReader(response.GetResponseStream()))
			{
				ret = ParseBarData(sr, symbol, frequency);
				sr.Close();
			}

			return ret;

			//string url = baseURL + "s=" + symbol.Name + "&a=" + startMonth.ToString() + "&b=" + startDate.Day.ToString() +
			//    "&c=" + startDate.Year.ToString() + "&d=" + endMonth.ToString() + "&e=" + endDate.Day +
			//    "&f=" + endDate.Year.ToString() + "&g=d&ignore=.csv";

			//WebResponse result = null;
			//WebRequest req = WebRequest.Create(url);
			//result = req.GetResponse();
			//Stream receiveStream = result.GetResponseStream();
			//Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
			//StreamReader sr = new StreamReader(receiveStream, encode);

			//string pageString = sr.ReadToEnd();

			//sr.Close();

			//List<BarData> bars = new List<BarData>();

			//PutRawDataInBarCollection(pageString, bars);

			//return new List<BarData>();

		}

		public IService GetService()
		{
			return this;
		}

		#endregion

		private List<BarData> ParseBarData(TextReader reader, Symbol symbol, int frequency)
		{
			HTMLParser parser = new HTMLParser(reader);
			List<BarData> ret = new List<BarData>();

			if (parser.ScanFor("CURRENCY") && parser.ScanFor("BID") &&
				parser.ScanFor("ASK") && parser.ScanFor("DATE"))
			{
				DateTimeFormatInfo formatInfo = new DateTimeFormatInfo();
				formatInfo.FullDateTimePattern = "ddd MMM  d HH:mm:ss yyyy";

				while (parser.ScanForTag("td", HTMLTagType.Open))
				{
					while (parser.ReadToken() && parser.CurrentString == null)
					{
						//	Loop until a non-tag is found
					}
					//	Make sure this is a row with interest rate data
					if (parser.CurrentString == null || parser.CurrentString.Trim().ToLowerInvariant() != symbol.Name.ToLowerInvariant())
					{
						break;
					}

					double bid;
					double ask;
					DateTime date;

					if (!parser.ScanForSpan())
						break;

					if (!double.TryParse(parser.CurrentString, NumberStyles.Float, CultureInfo.InvariantCulture, out bid))
						break;

					if (!parser.ScanForSpan())
						break;

					if (!double.TryParse(parser.CurrentString, NumberStyles.Float, CultureInfo.InvariantCulture, out ask))
						break;

					if (!parser.ScanForSpan())
						break;

					//if (!DateTime.TryParse(parser.CurrentString, formatInfo, DateTimeStyles.AllowWhiteSpaces, out date))
					if (!DateTime.TryParseExact(parser.CurrentString, "F", formatInfo, DateTimeStyles.AllowWhiteSpaces, out date))
						break;

					//	There are some bars of USD with bid and ask values of 0 for feb 2, 2001
					//	Filter these out
					//	Note that a bid or ask of zero (or negative value) could be OK for such currencies as JPY
					if (bid == 0 && ask == 0)
					{
						continue;
					}

					bid /= 100.0;
					ask /= 100.0;

					date = TimeFrequency.RoundTime(date, TimeSpan.FromMinutes(frequency));
					if (ret.Count == 0 || date != ret[ret.Count - 1].BarStartTime)
					{
						BarData bar = new BarData();
						bar.BarStartTime = date;
						bar.Bid = bid;
						bar.Ask = ask;
						bar.Close = (bid + ask) / 2;
						bar.Open = bar.High = bar.Low = bar.Close;

						ret.Add(bar);
					}

				}
			}

			ret.Sort(delegate(BarData b1, BarData b2)
			{
				return b1.BarStartTime.CompareTo(b2.BarStartTime);
			});


			return ret;
		}

		//private void PutRawDataInBarCollection(string webPage, List<BarData> bars)
		//{
		//    string[] lines = webPage.Split('\n');

		//    foreach (string line in lines)
		//    {
		//        string[] items = line.Split(',');

		//        if (items.Length >= 7)
		//        {
		//            double temp;

		//            IFormatProvider culture = new CultureInfo("en-US");

		//            // need to set culture info for non-US folks since we're getting
		//            // data from a US source
		//            if (double.TryParse(items[1], NumberStyles.AllowDecimalPoint, culture, out temp))
		//            {
		//                BarData bar = new BarData();
		//                bar.PriceDateTime = DateTime.Parse(items[0], culture);

		//                //  We need to calculate a factor so we can
		//                //  to normalize the open/high/low/close.
		//                //  This is necesary because of splits and dividends

		//                double realClose = double.Parse(items[4], NumberStyles.Number, culture);

		//                if (useAdjustedClose)
		//                {
		//                    double adjustedClose = double.Parse(items[6], NumberStyles.Number, culture);
		//                    double adjustmentFactor = realClose / adjustedClose;

		//                    bar.Open = double.Parse(items[1], NumberStyles.Number, culture) / adjustmentFactor;
		//                    bar.High = double.Parse(items[2], NumberStyles.Number, culture) / adjustmentFactor;
		//                    bar.Low = double.Parse(items[3], NumberStyles.Number, culture) / adjustmentFactor;
		//                    bar.Close = double.Parse(items[4], NumberStyles.Number, culture) / adjustmentFactor;
		//                    bar.Volume = (ulong)(ulong.Parse(items[5], NumberStyles.Number, culture) * adjustmentFactor);
		//                }
		//                else
		//                {
		//                    bar.Open = double.Parse(items[1], NumberStyles.Number, culture);
		//                    bar.High = double.Parse(items[2], NumberStyles.Number, culture);
		//                    bar.Low = double.Parse(items[3], NumberStyles.Number, culture);
		//                    bar.Close = double.Parse(items[4], NumberStyles.Number, culture);
		//                    bar.Volume = (ulong)(ulong.Parse(items[5], NumberStyles.Number, culture));
		//                }

		//                bars.Add(bar);
		//            }
		//            else
		//            {
		//                Trace.WriteLine("Error parsing data from source.  Source line = " + line);
		//            }
		//        }
		//    }
		//}
	}
}
