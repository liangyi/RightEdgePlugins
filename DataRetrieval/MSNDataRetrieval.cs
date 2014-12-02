using System;
using System.Net;
using System.IO;
using System.Text;
using System.Globalization;
using System.Diagnostics;
using System.Collections.Generic;

using RightEdge.Common;

namespace RightEdge.DataRetrieval
{
    public class MSNDataService : IService, IBarDataRetrieval
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
            return "MSN";
        }

        public string Author()
        {
            return "Yye Software";
        }

        public string Description()
        {
            return "Retrieves end of day data from the MSN Finance web site.";
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
            return "{1A2A522C-CD3C-4951-9F12-1C1781E01A12}";
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
            if (startDate == DateTime.MinValue)
            {
                startDate = new DateTime(1960, 1, 1);
            }

            if (endDate == DateTime.MaxValue)
            {
                endDate = new DateTime(DateTime.Today.Year, 12, 31);
            }

            startDate = startDate.AddMonths(-1);
            int startMonth = startDate.Month;
            int endMonth = endDate.Month;


            string baseURL = "http://data.moneycentral.msn.com/scripts/chrtsrv.dll?";
            string url = baseURL + "Symbol=" + symbol.Name + "&FileDownload=&C1=2&C2=" + startDate.Day.ToString() + "&C5=" + startMonth.ToString() +
                "&C6=" + startDate.Year.ToString() + "&C7=" + endMonth.ToString() +
                "&C8=" + endDate.Year.ToString() + "&C9=0&CE=0&CF=0&D0=1&D3=0&D4=1&D9=1";

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
                        bar.Open = double.Parse(items[1], NumberStyles.Number, culture);
                        bar.High = double.Parse(items[2], NumberStyles.Number, culture);
                        bar.Low = double.Parse(items[3], NumberStyles.Number, culture);
                        bar.Close = double.Parse(items[4], NumberStyles.Number, culture);
                        bar.Volume = ulong.Parse(items[5], NumberStyles.Number, culture);

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
