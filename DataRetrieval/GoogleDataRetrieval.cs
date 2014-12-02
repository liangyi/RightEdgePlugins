using System;
using System.Net;
using System.IO;
using System.Text;
using System.Globalization;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web;

using RightEdge.Common;

namespace RightEdge.DataRetrieval
{
	public class GoogleDataRetrieval : IService, IBarDataRetrieval
	{
		string lastError = "";

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
			return true;
		}

		public string ServiceName()
		{
			return "Google";
		}

		public string Author()
		{
			return "Yye Software";
		}

		public string Description()
		{
			return "Retrieves end of day data from the Google Finance web site.";
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
			return "{7DBA7E1E-901F-42a1-B127-9C4D145C4D08}";
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
		//    return ret;
		//}

		public List<BarData> RetrieveData(Symbol symbol, int frequency, DateTime startDate, DateTime endDate, BarConstructionType barConstruction)
		{
			if (frequency != (int) BarFrequency.Daily)
			{
				return new List<BarData>();
			}

			if (startDate == DateTime.MinValue)
			{
				// I think this is about as far back as Google will go
				startDate = new DateTime(1970, 1, 2);
			}

			if (endDate == DateTime.MaxValue)
			{
				endDate = DateTime.Now;
			}

			string baseURL = "http://finance.google.com/finance/historical?";
			string page = "q=" + symbol.Name + "&startdate=" + System.Web.HttpUtility.UrlEncode(startDate.ToString("MMM d, yyyy")) + "&enddate=" + 
				System.Web.HttpUtility.UrlEncode(endDate.ToString("MMM d, yyyy")) + "&output=csv";

			string url = baseURL + page;

			WebResponse result = null;
			WebRequest req = WebRequest.Create(url);
			result = req.GetResponse();
			Stream receiveStream = result.GetResponseStream();
			Encoding encode = System.Text.Encoding.GetEncoding("utf-8");
			StreamReader sr = new StreamReader(receiveStream, encode);

			string pageString = sr.ReadToEnd();

			sr.Close();

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

			foreach (string line in lines)
			{
				string[] items = line.Split(',');

				if (items.Length >= 6)
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
						//  This is necesary because of splits.
						//double adjustedClose = double.Parse(items[6], NumberStyles.Number, culture);
						//double realClose = double.Parse(items[4], NumberStyles.Number, culture);
						//double adjustmentFactor = realClose / adjustedClose;

						bar.Open = double.Parse(items[1], NumberStyles.Number, culture);
						bar.High = double.Parse(items[2], NumberStyles.Number, culture);
						bar.Low = double.Parse(items[3], NumberStyles.Number, culture);
						bar.Close = double.Parse(items[4], NumberStyles.Number, culture);
						bar.Volume = (ulong)(ulong.Parse(items[5], NumberStyles.Number, culture));

						bars.Add(bar);
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
