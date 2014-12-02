using System;
using System.Net;
using System.IO;
using System.Text;
using System.Globalization;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.InteropServices;

using RightEdge.Common;

namespace RightEdge.DataRetrieval
{
	public class QuoteTracker : IService, ITickRetrieval
	{
		private int port = 16240;
		private string address = "127.0.0.1";
		private ManualResetEvent connectDone = new ManualResetEvent(false);
		private ManualResetEvent sendDone = new ManualResetEvent(false);
		private ManualResetEvent receiveDone = new ManualResetEvent(false);
		GotTickData tickListener = null;
		//private Dictionary<Symbol, int?> watchedSymbols = new Dictionary<Symbol, int?>();
		private List<Symbol> watchedSymbols = new List<Symbol>();
		private Dictionary<Symbol, uint> symbolVolume = new Dictionary<Symbol, uint>();

		bool connected = false;
		bool watching = false;
		bool hadError = false;
		string lastError = "";

		private Socket clientSocket;
		//private string mReqType;

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
			return "Medved QuoteTracker";
		}

		public string Author()
		{
			return "Yye Software";
		}

		public string Description()
		{
			return "Retrieves real time data through Medved QuoteTracker";
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
			return "{19A4913C-B099-48fb-9B36-AFE9751D6F55}";
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
			get
			{
				return address;
			}
			set
			{
				address = value;
			}
		}

		public int Port
		{
			get
			{
				return port;
			}
			set
			{
				port = value;
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
			//  Create a TCP/IP socket.
			clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

			System.Net.IPAddress ipAdd = System.Net.IPAddress.Parse(ServerAddress);

			IPEndPoint endPoint = new IPEndPoint(ipAdd, port);

			// Connect to the remote endpoint.
			clientSocket.Connect(endPoint);

			// Setup XML login request to send to QT server.
			string data = "<LOGIN><AppName>RightEdge</AppName><AppVer>1.0</AppVer></LOGIN>";

			// Convert XML string to byte array
			byte[] byteData = Encoding.ASCII.GetBytes(data + "\0");
			byteData[byteData.Length - 1] = 255;

			// Send XML message to QT server.
			clientSocket.Send(byteData, 0, byteData.Length, SocketFlags.None);
			StateObject state = new StateObject();

			// Begin asynchronous receive to capture response from QT server.
			clientSocket.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
				new AsyncCallback(ReceiveCallback), state);

			connected = true;
			return true;
		}

		public bool Disconnect()
		{
			if (clientSocket != null && connected)
			{
				Socket socket = clientSocket;
				clientSocket = null;
				socket.Close();
			}

			return true;
		}

		public void Send(Socket client, string data)
		{
			try
			{
				// Convert the string data to byte data using ASCII encoding.
				byte[] byteData = Encoding.ASCII.GetBytes(data + "\0");
				byteData[byteData.Length - 1] = 255;

				// Begin sending the data to the remote device.
				client.BeginSend(byteData, 0, byteData.Length, SocketFlags.None,
					new AsyncCallback(SendCallback), client);
			}
			catch (Exception ex)
			{
				lastError = ex.Message;
				hadError = true;
			}
		}

		private void SendCallback(IAsyncResult ar)
		{
			try
			{
				// Retrieve the socket from the state object.
				Socket client = (Socket)ar.AsyncState;

				// Complete sending the data to the remote device.
				int bytesSent = client.EndSend(ar);

				// Signal that all bytes have been sent.
				sendDone.Set();
			}
			catch (Exception ex)
			{
				lastError = ex.Message;
				hadError = true;
			}
		}

		public void Receive(Socket client)
		{
			try
			{
				// Create the state object.
				StateObject state = new StateObject();

				// Begin receiving the data from the remote device.
				client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
					new AsyncCallback(ReceiveCallback), state);
			}
			catch (Exception ex)
			{
				lastError = ex.Message;
				hadError = true;
			}
		}

		void CopyTo(byte[] source_bytes, byte[] destination_bytes, int start, int length)
		{
			for (int i = 0; i < length; i++)
			{
				destination_bytes[i] = source_bytes[start + i];
			}
		}


		private void ReceiveCallback(IAsyncResult ar)
		{
			// Retrieve the state object
			// from the asynchronous state object.
			if (clientSocket != null)
			{
				StateObject state = (StateObject)ar.AsyncState;

				// Read data from the remote device.
				int bytesRead = clientSocket.EndReceive(ar);
				if (bytesRead > 0)
				{
					byte[] data = new Byte[bytesRead];
					CopyTo(state.buffer, data, 0, data.Length);
					this.ParseIt(data);
					clientSocket.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
						new AsyncCallback(ReceiveCallback), state);
					receiveDone.Set();
				}
			}
		}

		public string GetError()
		{
			return lastError;
		}

		public IService GetService()
		{
			return this;
		}

		#endregion

		#region IDisposable Members

		public void Dispose()
		{
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
			ClearError();
			//foreach (Symbol symbol in symbols)
			//{
			//    if (!watchedSymbols.ContainsKey(symbol))
			//    {
			//        watchedSymbols[symbol] = null;
			//    }
			//}
			//foreach (Symbol symbol in new List<Symbol>(watchedSymbols.Keys))
			//{
			//    if (!symbols.Contains(symbol))
			//    {
			//        if (watching)
			//        {
			//            //axTWS.cancelMktData(watchedSymbols[symbol].Value);
			//            //_OTClient.cancelTickStream(watchedSymbols[symbol].Value);
			//        }
			//        watchedSymbols.Remove(symbol);
			//    }
			//}

			////	Check error here because StartWatching() will clear error status
			//if (!CheckError())
			//{
			//    return false;
			//}

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

		// Subscribes to Level I quotes (L1QUOTES) 
		//
		private void SymbolSubscribe(Symbol symbol)
		{
			try
			{
				if (connected)
				{
					// Setup XML string to send
					// !!! SYMBOL Change symbol string to get the local symbol name !!!
					string data = "<L1QUOTES><ReqType>SUB</ReqType><Symbols>" + symbol.Name + "</Symbols></L1QUOTES>";
					byte[] byteData = Encoding.ASCII.GetBytes(data + "\0");
					byteData[byteData.Length - 1] = 255;

					// Send XML message to QT server.
					clientSocket.Send(byteData, 0, byteData.Length, SocketFlags.None);

					StateObject state = new StateObject();
					clientSocket.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
						new AsyncCallback(ReceiveCallback), state);
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
			if (connected)
			{
				// !!! SYMBOL Change symbol.Name to the local symbol
				// Setup XML string to send
				string data = "<L1QUOTES><ReqType>UNSUB</ReqType><Symbols>" + symbol.Name + "</Symbols></L1QUOTES>";
				byte[] byteData = Encoding.ASCII.GetBytes(data + "\0");
				byteData[byteData.Length - 1] = 255;

				// Send XML message to QT server.
				clientSocket.Send(byteData, 0, byteData.Length, SocketFlags.None);
			}
		}

		private void SendFullQuote(string message)
		{
			TickData bid = new TickData();
			TickData ask = new TickData();
			//TickData bidSize = new TickData();
			//TickData askSize = new TickData();
			//TickData lastSize = new TickData();
			//TickData lastPrice = new TickData();
			TickData trade = new TickData();
			TickData highPrice = new TickData();
			TickData lowPrice = new TickData();
			TickData openPrice = new TickData();
			TickData prevClosePrice = new TickData();
			TickData volumeValue = new TickData();

			string symbol = GetItem("Symbol=", message);
			string buffer = GetItem("Bid=", message);
			double bidPrice = Convert.ToDouble(buffer);
			buffer = GetItem("Ask=", message);
			double askPrice = Convert.ToDouble(buffer);
			buffer = GetItem("BidSize=", message);
			int bidSizeValue = Convert.ToInt32(buffer);
			buffer = GetItem("AskSize=", message);
			int askSizeValue = Convert.ToInt32(buffer);
			buffer = GetItem("LastSize=", message);
			int lastSizeValue = Convert.ToInt32(buffer);
			buffer = GetItem("Last=", message);
			double last = Convert.ToDouble(buffer);
			buffer = GetItem("High=", message);
			double high = Convert.ToDouble(buffer);
			buffer = GetItem("Low=", message);
			double low = Convert.ToDouble(buffer);
			buffer = GetItem("DateTime=", message);
			DateTime theDate = ParseQTDate(buffer);
			buffer = GetItem("Open=", message);
			double open = Convert.ToDouble(buffer);
			buffer = GetItem("PrevClose=", message);
			double prevClose = Convert.ToDouble(buffer);
			buffer = GetItem("Volume=", message);
			UInt64 volume = Convert.ToUInt64(buffer);

			bid.tickType = TickType.Bid;
			bid.time = theDate;
			bid.price = bidPrice;
			bid.size = (UInt64) bidSizeValue;

			ask.tickType = TickType.Ask;
			ask.time = theDate;
			ask.price = askPrice;
			ask.size = (UInt64) askSizeValue;

			trade.tickType = TickType.Trade;
			trade.time = theDate;
			trade.price = last;
			trade.size = (UInt64) lastSizeValue;

			lowPrice.tickType = TickType.LowPrice;
			lowPrice.time = theDate;
			lowPrice.price = low;
			highPrice.tickType = TickType.HighPrice;
			highPrice.time = theDate;
			highPrice.price = high;

			openPrice.tickType = TickType.OpenPrice;
			openPrice.time = theDate;
			openPrice.price = open;

			prevClosePrice.tickType = TickType.PreviousClose;
			prevClosePrice.price = prevClose;
			prevClosePrice.time = theDate;

			volumeValue.tickType = TickType.DailyVolume;
			volumeValue.time = theDate;
			volumeValue.size = volume;

			// !!! SYMBOL Convert symbol string back to an actual symbol instance
			if (tickListener != null)
			{
				tickListener((Symbol)symbol, bid);
				tickListener((Symbol)symbol, ask);
				//tickListener((Symbol)symbol, bidSize);
				//tickListener((Symbol)symbol, askSize);
				//tickListener((Symbol)symbol, lastSize);
				//tickListener((Symbol)symbol, lastPrice);
				tickListener((Symbol)symbol, trade);
				tickListener((Symbol)symbol, highPrice);
				tickListener((Symbol)symbol, lowPrice);
				tickListener((Symbol)symbol, openPrice);
				tickListener((Symbol)symbol, prevClosePrice);
				tickListener((Symbol)symbol, volumeValue);
			}

			Symbol volumeSymbol = (Symbol)symbol;

			if (symbolVolume.ContainsKey(volumeSymbol))
			{
				symbolVolume[volumeSymbol] = (uint)volume;
			}
			else
			{
				symbolVolume.Add(volumeSymbol, (uint)volume);
			}
		}

		private void SendUpdateQuote(string message)
		{
			// List of tick pieces that have come through
			List<TickData> ticks = new List<TickData>();

			// We should always have a symbol and date, the rest are
			// optional for this message.
			string symbol = GetItem("Symbol=", message);
			string buffer = GetItem("DateTime=", message);
			DateTime theDate = ParseQTDate(buffer);

			LastInfo lastInfo;
			if (_lastInfo.ContainsKey((Symbol)symbol))
			{
				lastInfo = _lastInfo[(Symbol)symbol];
			}
			else
			{
				lastInfo = new LastInfo();
				_lastInfo[(Symbol)symbol] = lastInfo;
			}

			TickData bidTick = new TickData();
			TickData askTick = new TickData();
			TickData tradeTick = new TickData();

			bidTick.price = lastInfo.lastBid;
			bidTick.size = lastInfo.lastBidSize;
			bidTick.time = theDate;

			askTick.price = lastInfo.lastAsk;
			askTick.size = lastInfo.lastAskSize;
			askTick.time = theDate;

			tradeTick.price = lastInfo.lastPrice;
			tradeTick.size = lastInfo.lastSize;
			tradeTick.time = theDate;

			buffer = GetItem("Bid=", message);

			if (buffer.Length > 0)
			{
				double bidPrice = Convert.ToDouble(buffer);
				bidTick.price = bidPrice;
				bidTick.tickType = TickType.Bid;

				lastInfo.lastBid = bidPrice;
			}

			buffer = GetItem("Ask=", message);

			if (buffer.Length > 0)
			{
				double askPrice = Convert.ToDouble(buffer);
				askTick.price = askPrice;
				askTick.tickType = TickType.Ask;

				lastInfo.lastAsk = askPrice;
			}

			buffer = GetItem("BidSize=", message);
			if (buffer.Length > 0)
			{
				double bidSize = Convert.ToDouble(buffer);
				bidTick.size = (UInt64)bidSize;
				bidTick.tickType = TickType.Bid;

				lastInfo.lastBidSize = (UInt64)bidSize;
			}

			buffer = GetItem("AskSize=", message);
			if (buffer.Length > 0)
			{
				double askSize = Convert.ToDouble(buffer);
				askTick.size = (UInt64)askSize;
				askTick.tickType = TickType.Ask;

				lastInfo.lastAskSize = (UInt64)askSize;
			}

			if (bidTick.tickType == TickType.Bid)
			{
				ticks.Add(bidTick);
			}
			if (askTick.tickType == TickType.Ask)
			{
				ticks.Add(askTick);
			}

			uint totalVolume = 0;
			buffer = GetItem("Volume=", message);
			if (buffer.Length > 0)
			{
				totalVolume = Convert.ToUInt32(buffer);
				TickData volumeTick = new TickData();

				if (symbolVolume.ContainsKey((Symbol)symbol))
				{
					volumeTick.size = totalVolume;
					lastInfo.lastSize = totalVolume - symbolVolume[(Symbol)symbol];
					volumeTick.tickType = TickType.DailyVolume;
					symbolVolume[(Symbol)symbol] = totalVolume;
				}
				else
				{
					volumeTick.tickType = TickType.DailyVolume;
					volumeTick.size = totalVolume;
					symbolVolume[(Symbol)symbol] = totalVolume;
				}

				volumeTick.time = theDate;
				ticks.Add(volumeTick);
			}

			uint lastSize = 0;
			buffer = GetItem("LastSize=", message);
			if (buffer.Length > 0)
			{
				lastSize = Convert.ToUInt32(buffer) * 100;

				symbolVolume[(Symbol)symbol] += lastSize;

				TickData volumeTick = new TickData();
				tradeTick.size = (UInt64)symbolVolume[(Symbol)symbol] + (ulong)lastSize;
				//volumeTick.time = tradeTick.time;
				//volumeTick.tickType = TickType.Trade;
				//ticks.Add(volumeTick);

				//tradeTick.size = (UInt64)lastSize;
				//tradeTick.tickType = TickType.Trade;

				//lastInfo.lastSize = (UInt64)lastSize;
			}

			buffer = GetItem("Last=", message);
			if (buffer.Length > 0)
			{
				double last = Convert.ToDouble(buffer);
				tradeTick.price = last;
				//tradeTick.size = (UInt64)lastSize;
				tradeTick.tickType = TickType.Trade;

				if (symbolVolume.ContainsKey((Symbol)symbol))
				{
					tradeTick.size = totalVolume - symbolVolume[(Symbol)symbol];
				}

				lastInfo.lastPrice = last;
			}

			if (tradeTick.tickType == TickType.Trade)
			{
				ticks.Add(tradeTick);
			}

			buffer = GetItem("High=", message);
			if (buffer.Length > 0)
			{
				double high = Convert.ToDouble(buffer);
				TickData highTick = new TickData();
				highTick.price = high;
				highTick.tickType = TickType.HighPrice;
				highTick.time = theDate;
				ticks.Add(highTick);
			}

			buffer = GetItem("Low=", message);
			if (buffer.Length > 0)
			{
				double low = Convert.ToDouble(buffer);
				TickData lowTick = new TickData();
				lowTick.price = low;
				lowTick.tickType = TickType.LowPrice;
				lowTick.time = theDate;
				ticks.Add(lowTick);
			}

			if (tickListener != null)
			{
				foreach (TickData tick in ticks)
				{
					// !!! SYMBOL Convert string symbol to actual Symbol class
					tickListener((Symbol)symbol, tick);
				}
			}
		}

		private DateTime ParseQTDate(string date)
		{
			DateTime theDate = new DateTime(Convert.ToInt32(date.Substring(4, 4)),
				Convert.ToInt32(date.Substring(2, 2)),
				Convert.ToInt32(date.Substring(0, 2)),
				Convert.ToInt32(date.Substring(8, 2)),
				Convert.ToInt32(date.Substring(10, 2)),
				Convert.ToInt32(date.Substring(12, 2)));

			return theDate;

		}

		private string GetItem(string name, string message)
		{
			int startIndex = message.IndexOf(name);
			string item = "";

			if (startIndex != -1)
			{
				startIndex += name.Length;
				int endIndex = message.IndexOf(" ", startIndex);
				item = message.Substring(startIndex, endIndex - startIndex);
			}

			return item;
		}

		#region QT parse functions.
		// I stole these from their example program.  If you ask me, at first
		// glance, these seem like a bit of wheel reinvention, but we'll use
		// them for now to get the thing work.

		// Takes in the outbound message as an array of bytes from QT Server
		void ParseIt(byte[] bytes)
		{
			try
			{
				// All messages sent FROM QT Server start with a Record ID
				string RecordID = System.Text.Encoding.UTF8.GetString(bytes, 0, 2);
				string message = "";

				switch (RecordID)
				{
					// Parse a Level 1 FULL QUOTE message
					case "S1":
						message = ParseS1(bytes);
						SendFullQuote(message);
						break;

					// Parse a Level1 UPDATE QUOTE message
					case "L1":
						message = ParseL1(bytes);
						SendUpdateQuote(message);
						break;

					// Parse a HISTORY TICK record
					case "HT":
						message = ParseHT(bytes);
						break;

					// Parse a HISTORY OHLC record
					case "HO":
						message = ParseHO(bytes);
						break;

					// Parse a HISTORY END record
					case "HE":
						message = ParseHO(bytes);
						break;

					// Parse a LOGIN feedback
					case "LO":
						message = ParseXML(bytes);
						break;

					// Parse a ER feedback
					case "ER":
						message = ParseXML(bytes);
						break;

					case "OK":
						message = ParseXML(bytes);
						break;

					case "ST":
						message = ParseXML(bytes);
						break;


					default:
						//ParseXML(bytes);
						break;
				}
			}
			catch (Exception ex)
			{
				lastError = ex.Message;
				hadError = true;
			}
		}


		public struct L1Quote
		{
			//BitConverter BitConverter = new BitConverter
			public string Symbol;
			public double Bid; //  Float (8 bytes) 
			public double Ask; //  Float (8 bytes) 
			public double Last;//  Float (8 bytes) 
			public double Change; //  Float (8 bytes) 
			public long Volume; //  Int (8 bytes) 
			public double Open;  //  Float (8 bytes) 
			public double PrevClose; //  Float (8 bytes) 
			public int BidSize; //  Int (4 bytes) 
			public int AskSize; //  Int (4 bytes) 
			public int LastSize; //  Int (4 bytes) 
			public double High; //  Float (8 bytes) 
			public double Low; //  Float (8 bytes) 
			public char Tick; // Char (1 byte)

			public string DateTime; //   Text (14 bytes) 
		}


		// Breaks down single XML string to multiple lines for easier readability.
		public string ParseXML(byte[] Data)
		{
			try
			{
				string msg = Encoding.ASCII.GetString(Data, 0, Data.Length);
				string message = "";

				int i = 0;
				while (i < Data.Length)
				{
					int i1;
					i1 = msg.IndexOf("\0", i);

					if (i1 < 0)
						i1 = Data.Length - 1;

					string text = msg.Substring(i, i1 - i);
					i = i1 + 1;
					message += Environment.NewLine + text;
				}

				return message;
			}
			catch (Exception e)
			{
				lastError = e.Message;
				hadError = true;
				return "";
			}
		}

		// Parse LEVEL I FULL QUOTE    ID=S1 (ASCII)
		// displaying the fields and values for a full quote to the user form
		public string ParseS1(byte[] Data)
		{

			L1Quote L1Quote = new L1Quote();

			short RecordLength = BitConverter.ToInt16(Data, 2);

			string full_message = "";

			for (int i = 4; i < RecordLength - 1; )
			{
				short FieldID = BitConverter.ToInt16(Data, i);
				i += 2;

				lock (this)
				{
					string message = "";
					i = ParseField(ref L1Quote, FieldID, ref Data, i, ref message);
					full_message += message;
				}

			}

			return full_message;
		}

		public string ParseL1(byte[] Data)
		{
			L1Quote L1Quote = new L1Quote();

			string message = ParseQuote(ref L1Quote, ref Data);

			return message;
		}

		public string ParseHT(byte[] Data)
		{

			L1Quote L1HistoryQuote = new L1Quote();

			string message = ParseQuote(ref L1HistoryQuote, ref Data);

			return message;
		}

		public string ParseHO(byte[] Data)
		{

			L1Quote L1HOQuote = new L1Quote();

			string message = ParseQuote(ref L1HOQuote, ref Data);

			return message;
		}

		public string ParseHE(byte[] Data)
		{

			L1Quote L1HEQuote = new L1Quote();

			string message = ParseQuote(ref L1HEQuote, ref Data);
			return message;
		}

		public string ParseQuote(ref L1Quote L1Quote, ref byte[] Data)
		{
			try
			{
				string RecordID = Encoding.ASCII.GetString(Data, 0, 2);
				short RecordLength = BitConverter.ToInt16(Data, 2);

				string full_message = "";

				for (int i = 4; i < RecordLength - 1; )
				{
					short FieldID = BitConverter.ToInt16(Data, i);
					i += 2;

					string message = "";
					i = ParseField(ref L1Quote, FieldID, ref Data, i, ref message);

					full_message += message;
				}

				return full_message;
			}
			catch (Exception ex)
			{
				lastError = ex.Message;
				hadError = true;
				return "";
			}
		}

		// Stores value from parsing bytes Data starting from position i to appropriate
		// property in L1Quote, converting to appropriate type.
		int ParseField(ref L1Quote L1Quote, short FieldID, ref byte[] Data, int i, ref string message)
		{
			try
			{
				switch (FieldID)
				{
					case 1: int i1 = IndexOf0(Data, i);
						L1Quote.Symbol = Encoding.ASCII.GetString(Data, i, i1 - i);
						message = Environment.NewLine + Environment.NewLine + "Symbol=" + L1Quote.Symbol + " ";
						i = i1 + 1;
						break;

					case 2: L1Quote.Bid = BitConverter.ToDouble(Data, i);
						message = "Bid=" + L1Quote.Bid.ToString() + " ";
						i += 8; //  Float (8 bytes)
						break;

					case 3: L1Quote.Ask = BitConverter.ToDouble(Data, i);
						message = "Ask=" + L1Quote.Ask.ToString() + " ";
						i += 8; //  Float (8 bytes)
						break;

					case 4: L1Quote.Last = BitConverter.ToDouble(Data, i);
						message = "Last=" + L1Quote.Last.ToString() + " ";
						i += 8; //  Float (8 bytes)
						break;

					case 5: L1Quote.Change = BitConverter.ToDouble(Data, i);
						message = "Change=" + L1Quote.Change.ToString() + " ";
						i += 8; //  Float (8 bytes)
						break;

					case 6: L1Quote.Volume = BitConverter.ToInt64(Data, i);
						message = "Volume=" + L1Quote.Volume.ToString() + " ";
						i += 8; //  Int (8 bytes)
						break;

					case 7: L1Quote.Open = BitConverter.ToDouble(Data, i);
						message = "Open=" + L1Quote.Open.ToString() + " ";
						i += 8; //  Float (8 bytes)
						break;

					case 8: L1Quote.PrevClose = BitConverter.ToDouble(Data, i);
						message = "PrevClose=" + L1Quote.PrevClose.ToString() + " ";
						i += 8; //  Float (8 bytes)
						break;

					case 9: L1Quote.BidSize = BitConverter.ToInt32(Data, i);
						message = "BidSize=" + L1Quote.BidSize.ToString() + " ";
						i += 4; //  Int (4 bytes) 				
						break;

					case 10: L1Quote.AskSize = BitConverter.ToInt32(Data, i);
						message = "AskSize=" + L1Quote.AskSize.ToString() + " ";
						i += 4; //  Int (4 bytes) 				
						break;

					case 11: L1Quote.LastSize = BitConverter.ToInt32(Data, i);
						message = "LastSize=" + L1Quote.LastSize.ToString() + " ";
						i += 4; //  Int (4 bytes) 				
						break;

					case 12: L1Quote.High = BitConverter.ToDouble(Data, i);
						message = "High=" + L1Quote.High.ToString() + " ";
						i += 8; //  Float (8 bytes)
						break;

					case 13: L1Quote.Low = BitConverter.ToDouble(Data, i);
						message = "Low=" + L1Quote.Low.ToString() + " ";
						i += 8; //  Float (8 bytes)
						break;

					case 14: L1Quote.Tick = BitConverter.ToChar(Data, i);
						message = "Tick=" + L1Quote.Tick.ToString() + " ";
						i += 1; // Char (1 byte)				
						break;

					case 15: 	//int i1 = BinaryUtilities.IndexOf0( Data, i);
						//							QuoteField2.DateTime = Encoding.ASCII.GetString(Data, i, i1-i); i=i1+1; break; //   Text (14 bytes) 				
						L1Quote.DateTime = Encoding.ASCII.GetString(Data, i, 14);
						message = "DateTime=" + L1Quote.DateTime.ToString() + " ";
						i += 14; //   Text (14 bytes) 				
						break;

					case 16: i += 8;
						break;

					case 17: i += 8;
						break;

					case 18: i1 = IndexOf0(Data, i); i = i1 + 1;
						break;

					case 19: i += 8;
						break;

					case 20: i += 4;
						break;

					case 21: i += 4;
						break;

					case 22: i += 8;
						break;

					case 23: i += 8;
						break;

					case 24: i += 8;
						break;
					case 25: i1 = IndexOf0(Data, i); i = i1 + 1;
						break;

					case 26: i1 = IndexOf0(Data, i); i = i1 + 1;
						break;

					case 27: i += 8;
						break;

					case 28: i += 4;
						break;

					case 29: i += 1;
						break;

					case 30: i += 8;
						break;

					default:
						break;
				}

				return i;
			}
			catch (Exception ex)
			{
				lastError = ex.Message;
				hadError = true;
				return 0;
			}
		}

		// Utility to find the position of byte with value 0 in array of bytes
		private static int IndexOf0(byte[] data, int pos)
		{
			try
			{
				for (int i = pos; i < data.Length; i++)
				{
					if (data[i] == 0)
					{
						return i;
					}
				}
				return -1;
			}
			catch (Exception ex)
			{
				string s = ex.Message;
				return 0;
			}
		}
		#endregion

		private class LastInfo
		{
			public double lastBid;
			public UInt64 lastBidSize;
			public double lastAsk;
			public UInt64 lastAskSize;
			public double lastPrice;
			public UInt64 lastSize;
		}

		Dictionary<Symbol, LastInfo> _lastInfo = new Dictionary<Symbol, LastInfo>();
	}

	// Contains buffer that holds bytes returned from socket connection
	public class StateObject
	{
		public const int BufferSize = 2048;
		public byte[] buffer = new byte[BufferSize];
	}
}

