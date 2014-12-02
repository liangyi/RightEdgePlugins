﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RightEdge.Common;
using System.ComponentModel;
using System.Diagnostics;
using System.Data.Common;
using System.Text.RegularExpressions;
using System.Data.Linq;
using System.Reflection;
using RightEdge.Shared;
using System.Data.SqlClient;
using System.IO;
using System.Data;
using System.Data.SqlTypes;

namespace RightEdge.DataStorage
{
	//	TODO: Automatically create database tables if database has been created but is empty

	[DisplayName("SQL Server Data Store")]
	[Description("Stores bar data and symbol information in a Microsoft SQL Server database.")]
	[PluginEditor(typeof(LinqToSqlSettingsEditor))]
	public sealed class LinqToSQLStorage : IDataStore, ILinqToSQLStorage
	{
		[DisplayName("Server Address")]
		[Description("The name or network address of the SQL Server to connect to.\r\n" +
			"For a default installation of SQL Server Express the address is \"(local)\\SQLEXPRESS\".")]
		public string ServerAddress { get; set; }

		[Description("The name of the database to use.")]
		public string Database { get; set; }

		public string UserName { get; set; }
		public string Password { get; set; }

		[DisplayName("Database Format")]
		public DatabaseSchema DatabaseSchema { get; set; }

		[DisplayName("Authentication Mode")]
		public AuthenticationMode AuthenticationMode { get; set; }

		private OldDataStoreWrapper _wrapper;

		public LinqToSQLStorage()
		{
			ServerAddress = "(local)\\SQLEXPRESS";
			Database = "RIGHTEDGE_DATA";
			UserName = "";
			Password = "";

			AuthenticationMode = AuthenticationMode.Windows;
		}

		//  How to connect to a specific dbf file: Server=.\SQLExpress;AttachDbFilename=c:\mydbfile.mdf;Database=dbname;Trusted_Connection=Yes;
		private string ConnectionString
		{
			get
			{
				return GetBuilder().ToString();
			}
		}

		private SqlConnectionStringBuilder GetBuilder()
		{
			var builder = new System.Data.SqlClient.SqlConnectionStringBuilder();

			builder.DataSource = ServerAddress;
			builder.InitialCatalog = Database;
			if (UseIntegratedSecurity)
			{
				//	Use windows authentication
				builder.IntegratedSecurity = true;
			}
			else
			{
				builder.UserID = UserName;
				builder.Password = Password;
			}
			builder.ConnectTimeout = 5;
			

			return builder;
		}

		private bool UseIntegratedSecurity
		{
			get
			{
				return AuthenticationMode == AuthenticationMode.Windows;
				//if (string.IsNullOrEmpty(UserName) && string.IsNullOrEmpty(Password))
				//{
				//    return true;
				//}
				//return false;
			}
		}

		private OldDataStoreWrapper OldWrapper
		{
			get
			{
				if (_wrapper == null)
				{
					var oldPlugin = new SQLServerStorage(false);
					oldPlugin.Server = ServerAddress;
					oldPlugin.Database = Database;
					oldPlugin.UserName = UserName;
					oldPlugin.Password = Password;
					oldPlugin.SqlAuth = !UseIntegratedSecurity;

					_wrapper = new OldDataStoreWrapper();
					_wrapper.OldStore = oldPlugin;
				}
				return _wrapper;
			}
		}

		public IDataAccessor<BarData> GetBarStorage(SymbolFreq symbol)
		{
			if (DatabaseSchema == DatabaseSchema.BackwardsCompatible)
			{
				return OldWrapper.GetBarStorage(symbol);
			}
			return new LinqToSQLBarDataAccessor(ConnectionString, symbol);
		}

		public IDataAccessor<TickData> GetTickStorage(Symbol symbol)
		{
			if (DatabaseSchema == DatabaseSchema.BackwardsCompatible)
			{
				throw new NotSupportedException("Tick data storage not supported with the backwards compatible database format.  " + 
					"You can upgrade to the new database format from the data store settings.");
			}
			return new LinqToSQLTickDataAccessor(ConnectionString, symbol);
		}

		public void FlushAll()
		{
			if (_wrapper != null)
			{
				OldWrapper.FlushAll();
			}
		}


		public ILinqToSQLStorage Clone()
		{
			return (ILinqToSQLStorage)this.MemberwiseClone();
		}

		public TestConnectionResult TestConnection()
		{
			TestConnectionResult result = new TestConnectionResult();

			bool initialConnectionSuccessful = false;
			Exception ex;
			//	Try to connect to the database
			ex = Run(() =>
			{
				var builder = GetBuilder();
				builder.ConnectTimeout = 1;
				using (SqlConnection dbConnection = new SqlConnection(builder.ToString()))
				{
					dbConnection.Open();
					initialConnectionSuccessful = true;

					//	Successfully connected to database, now try to see if the database tables exist
					//	Check if version table exists
					if (!TableExists(dbConnection, "RightEdgeDatabaseVersion"))
					{
						//	Check to see if the database has a BarData and TickData
						if (TableExists(dbConnection, "BarData") && TableExists(dbConnection, "TickData"))
						{
							result.Result = ConnectionResult.DatabaseNeedsConversion;
							result.AdditionalInformation = "The database needs to be upgraded to the new format.";
							return;
						}
						else
						{
							result.Result = ConnectionResult.DatabaseTablesNotCreated;
							result.AdditionalInformation = "Database tables not created.";
							return;
						}
					}

					//	Check to make sure version is correct
					string query = "SELECT TOP 1 Version FROM RightEdgeDatabaseVersion ORDER BY Version DESC";
					using (var command = new SqlCommand(query, dbConnection))
					{
						var commandResult = command.ExecuteScalar();
						if (commandResult == null || commandResult == DBNull.Value)
						{
							result.Result = ConnectionResult.DatabaseTablesNotCreated;
							result.AdditionalInformation = "Database tables not created.";
							return;
						}
						else
						{
							int version = Convert.ToInt32(commandResult);
							if (version != 1)
							{
								result.Result = ConnectionResult.WrongDatabaseVersion;
								result.AdditionalInformation = "Wrong database version";
								return;
							}
						}
					}
				}
			});

			//	Check if the server wasn't able to be contacted at all.  Full message text is as follows:
			//		A network-related or instance-specific error occurred while establishing a connection to SQL Server.
			//		The server was not found or was not accessible. Verify that the instance name is correct and that SQL
			//		Server is configured to allow remote connections. (provider: SQL Network Interfaces, error: 26 - Error
			//		Locating Server/Instance Specified)
			//	Another variation ends like this: (provider: Named Pipes Provider, error: 40 - Could not open a connection to SQL Server)
			//	Hopefully just looking for "error: nn" will work even if the exception message is localized.
			if (ex != null &&
				(ex.Message.Contains("error: 26") || ex.Message.Contains("error: 40")))
			{
				result.Result = ConnectionResult.ServerNotFound;
				result.AdditionalInformation = ex.Message;
				return result;
			}

			if (ex != null && !initialConnectionSuccessful && ex is SqlException)
			{
				//Try to connect to the master database to see if the problem was that the database didn't exist
				Exception ex2 = Run(() =>
				{
					var builder = GetBuilder();
					builder.InitialCatalog = "master";
					builder.ConnectTimeout = 1;

					using (SqlConnection dbConnection = new SqlConnection(builder.ToString()))
					{
						dbConnection.Open();
					}
				});

				if (ex2 == null)
				{
					//	Successfully connected to the master database
					result.Result = ConnectionResult.DatabaseNotFound;
					result.AdditionalInformation = "The database \"" + Database + "\" does not appear to exist on the server.";
					return result;
				}
			}


			if (ex != null)
			{
				result.Result = ConnectionResult.Failed;
				result.AdditionalInformation = ex.Message;
			}
			return result;
		}

		public ReturnCode CreateDatabase()
		{
			try
			{
				var builder = GetBuilder();
				builder.InitialCatalog = "master";

				using (SqlConnection dbConnection = new SqlConnection(builder.ToString()))
				{
					dbConnection.Open();

					string script = @"USE [master]
GO

IF  NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'RIGHTEDGE_DATA')
CREATE DATABASE [RIGHTEDGE_DATA]
GO";
					script = script.Replace("RIGHTEDGE_DATA", Database);

					ExecuteSql(dbConnection, script, false);

					script = LoadEmbeddedString("RightEdge.DataStorage.Resources.SQLDatabaseCreationScript.txt");

					script = script.Replace("RIGHTEDGE_DATA", Database);

					ExecuteSql(dbConnection, script, true);
				}

				return ReturnCode.Succeed;
			}
			catch (Exception ex)
			{
				return ReturnCode.Fail(ex);
			}
			
		}

		public ReturnCode UpgradeDatabase()
		{
			ReturnCode retCode;
			try
			{
				var builder = GetBuilder();
				using (SqlConnection dbConnection = new SqlConnection(builder.ToString()))
				{
					dbConnection.Open();

					if (!TableExists(dbConnection, "BarData") || !TableExists(dbConnection, "TickData"))
					{
						return ReturnCode.Fail("The tables for the old database format were not found.");
					}

					//	Check if version table exists
					if (!TableExists(dbConnection, "RightEdgeDatabaseVersion"))
					{
						retCode = CreateDatabase();
						if (!retCode.Success)
						{
							return retCode;
						}
					}

					long totalCount;
					using (var context = new SQLDataContext(dbConnection))
					{
						totalCount = context.DBBars.LongCount() + context.DBTicks.LongCount();
					}

					if (totalCount > 0)
					{
						return ReturnCode.Fail("The new database tables must be empty before converting data from the old format.");
					}

					var script = LoadEmbeddedString("RightEdge.DataStorage.Resources.SQLDatabaseConversionScript.txt");
					script = script.Replace("[OLD_DATABASE]", "[" + Database + "]");
					script = script.Replace("[NEW_DATABASE]", "[" + Database + "]");

					ExecuteSql(dbConnection, script, true);

					return ReturnCode.Succeed;
				}
			}
			catch (Exception ex)
			{
				return ReturnCode.Fail(ex);
			}
		}

		private string LoadEmbeddedString(string resourceName)
		{
			using (var sr = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)))
			{
				return sr.ReadToEnd();
			}
		}

		//	Modified from: http://www.mattberther.com/2005/04/11/executing-a-sql-script-using-adonet/
		public void ExecuteSql(SqlConnection connection, string sql, bool useTransaction)
		{
			Regex regex = new Regex("^GO", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			string[] lines = regex.Split(sql);

			SqlTransaction transaction = null;
			if (useTransaction)
			{
				transaction = connection.BeginTransaction();
			}
			using (SqlCommand cmd = connection.CreateCommand())
			{
				cmd.Connection = connection;
				if (useTransaction)
				{
					cmd.Transaction = transaction;
				}

				foreach (string line in lines)
				{
					if (line.Length > 0)
					{
						cmd.CommandText = line;
						cmd.CommandType = CommandType.Text;

						try
						{
							cmd.ExecuteNonQuery();
						}
						catch (SqlException)
						{
							if (useTransaction)
							{
								transaction.Rollback();
							}
							throw;
						}
					}
				}
			}

			if (useTransaction)
			{
				transaction.Commit();
			}
		}

		private Exception Run(Action action)
		{
			try
			{
				action();
				return null;
			}
			catch (Exception ex)
			{
				return ex;
			}
		}

		private bool TableExists(SqlConnection dbConnection, string tableName)
		{
			var query = "SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[" + tableName + "]') AND type in (N'U')";
			using (var command = new SqlCommand(query, dbConnection))
			{
				int commandResult = Convert.ToInt32(command.ExecuteScalar());
				return commandResult > 0;
			}
		}

	}

	abstract class LinqToSQLDataAccessor<T, DBType> : IDataAccessor<T>
	{
		string _connectionString;

		protected Symbol _symbol;
		protected string _symbolUniqueID;

		public LinqToSQLDataAccessor(string connectionString, Symbol symbol)
		{
			_connectionString = connectionString;

			_symbol = symbol;
			_symbolUniqueID = symbol.ToUniqueId();

		}

		protected SQLDataContext GetContext()
		{
			//	According to http://stackoverflow.com/questions/1570866/linq-doesnt-recognize-changes-in-the-database, it
			//	is best to have the DataContext be short-lived.
			return new SQLDataContext(_connectionString);
		}

		protected DBSymbol GetSymbol(SQLDataContext context, bool createIfNeeded)
		{
			DBSymbol dbSymbol;

			dbSymbol = context.DBSymbols.FirstOrDefault(s => s.SymbolUniqueID == _symbolUniqueID);
			if (dbSymbol == null && createIfNeeded)
			{
				dbSymbol = new DBSymbol();
				dbSymbol.SymbolUniqueID = _symbolUniqueID;
				dbSymbol.SymbolGuid = Guid.NewGuid();

				context.DBSymbols.InsertOnSubmit(dbSymbol);
				context.SubmitChanges();
			}
			return dbSymbol;
		}


		//protected abstract IQueryable<DBType> Items { get; }
		protected abstract IQueryable<DBType> GetItemsByDate(SQLDataContext context, SqlDateTime start, SqlDateTime end);

		protected abstract DateTime GetItemTime(T item);

		protected abstract T ConvertToRightEdgeType(DBType item);
		//protected abstract DBType ConvertToDBType(SQLDataContext context, T item, int order, DBSymbol dbSymbol);

		protected abstract System.Collections.IEnumerable GetDataReaderItems(IEnumerable<T> items, Guid symbolGuid);

		//protected abstract void InsertOnSubmit(SQLDataContext context, DBType item);
		
		protected abstract void InternalDelete(DateTime start, DateTime end);

		protected abstract string TableName { get; }

		protected SqlDateTime MakeValidSQLDate(DateTime date, bool roundUp)
		{
			return SqlDateUtil.MakeValidSQLDate(date, roundUp);
		}

		public List<T> Load(DateTime start, DateTime end, long maxItems, bool loadFromEnd)
		{
			using (var context = GetContext())
			{
				var SqlStart = MakeValidSQLDate(start, true);
				var SqlEnd = MakeValidSQLDate(end, false);

				IQueryable<DBType> itemQuery = GetItemsByDate(context, SqlStart, SqlEnd);
				if (maxItems > 0)
				{
					if (loadFromEnd)
					{
						long count = itemQuery.LongCount();
						if (count > maxItems)
						{
							long toSkip = count - maxItems;
							//  TODO: Support long
							itemQuery = itemQuery.Skip((int)toSkip);
						}
					}
					else
					{
						//  TODO: Support long
						itemQuery = itemQuery.Take((int)maxItems);
					}
				}


				List<T> ret = new List<T>();
				foreach (var item in itemQuery)
				{
					ret.Add(ConvertToRightEdgeType(item));
				}

				return ret;
			}
		}

		public long GetCount(DateTime start, DateTime end)
		{
			var SqlStart = MakeValidSQLDate(start, true);
			var SqlEnd = MakeValidSQLDate(end, false);
			using (var context = GetContext())
			{
				return GetItemsByDate(context, SqlStart, SqlEnd).LongCount();
			}

		}

		//	How to speed up inserts: http://stackoverflow.com/questions/334919/mass-insert-into-sql-server
		public long Save(IList<T> items)
		{
			items = items.OrderBy(i => GetItemTime(i)).ToList();
			if (items.Count == 0)
			{
				return 0;
			}
			DateTime start = GetItemTime(items.First());
			DateTime end = GetItemTime(items.Last());

			InternalDelete(start, end);

			using (new TimeOperation("Overall save"))
			{
				Guid symbolGuid;
				using (var context = GetContext())
				{
					DBSymbol dbSymbol = GetSymbol(context, true);
					symbolGuid = dbSymbol.SymbolGuid;
				}


				using (var s = new SqlBulkCopy(this._connectionString, SqlBulkCopyOptions.TableLock))
				{
					s.DestinationTableName = TableName;
					s.BatchSize = 5000;

					s.WriteToServer(new EnumerableDataReader(GetDataReaderItems(items, symbolGuid)));

					s.Close();

					return items.Count;
				}
			}
		}

		public long Delete(DateTime start, DateTime end)
		{
			InternalDelete(start, end);
			//Context.SubmitChanges();

			return 0;
		}

		public abstract DateTime GetDateTimeAtIndex(long index, out long numSameDatePreceding);

		public void Dispose()
		{
		}


	}

	sealed class LinqToSQLBarDataAccessor : LinqToSQLDataAccessor<BarData, DBBar>
	{
		int _frequency;

		public LinqToSQLBarDataAccessor(string connectionString, SymbolFreq symbol)
			: base(connectionString, symbol.Symbol)
		{
			_frequency = symbol.Frequency;
		}

		IQueryable<DBBar> GetItems(SQLDataContext context)
		{
            var dbSymbol = GetSymbol(context, false);
            if (dbSymbol == null)
            {
                return Enumerable.Empty<DBBar>().AsQueryable();
            }

            return context.DBBars.Where(b => b.SymbolGuid == dbSymbol.SymbolGuid && b.Frequency == _frequency).OrderBy(b => b.BarStartTime).ThenBy(b => b.Order);

			//return context.DBBars.Where(b => b.DBSymbol.SymbolUniqueID == _symbolUniqueID && b.Frequency == _frequency).OrderBy(b => b.BarStartTime).ThenBy(b => b.Order);
		}

		protected override IQueryable<DBBar> GetItemsByDate(SQLDataContext context, SqlDateTime start, SqlDateTime end)
		{
			return GetItems(context).Where(b => b.BarStartTime >= start.Value && b.BarStartTime <= end.Value);
		}

		protected override DateTime GetItemTime(BarData item)
		{
			return MakeValidSQLDate(item.BarStartTime, false).Value;
		}

		protected override BarData ConvertToRightEdgeType(DBBar dbBar)
		{
			var bar = new BarData();
			bar.BarStartTime = dbBar.BarStartTime;
			bar.Open = dbBar.Open;
			bar.Close = dbBar.Close;
			bar.High = dbBar.High;
			bar.Low = dbBar.Low;
			bar.Bid = dbBar.Bid;
			bar.Ask = dbBar.Ask;
			bar.Volume = (ulong)dbBar.Volume;
			bar.OpenInterest = dbBar.OpenInterest;
			bar.EmptyBar = dbBar.EmptyBar;

			return bar;
		}

		//protected override DBBar ConvertToDBType(SQLDataContext context, BarData bar, int order, DBSymbol dbSymbol)
		//{
		//    DBBar b = new DBBar();
		//    b.Frequency = _frequency;
		//    b.BarStartTime = MakeValidSQLDate(bar.BarStartTime);
		//    b.Open = bar.Open;
		//    b.Close = bar.Close;
		//    b.High = bar.High;
		//    b.Low = bar.Low;
		//    b.Bid = bar.Bid;
		//    b.Ask = bar.Ask;
		//    b.Volume = (long)bar.Volume;
		//    b.OpenInterest = bar.OpenInterest;
		//    b.EmptyBar = bar.EmptyBar;
		//    b.DBSymbol = dbSymbol;
		//    b.Order = order;

		//    return b;
		//}


		//protected override void InsertOnSubmit(SQLDataContext context, DBBar item)
		//{
		//    context.DBBars.InsertOnSubmit(item);
		//}

		protected override string TableName
		{
			get { return "Bar"; }
		}

		private class DataReaderBar
		{
			public Guid SymbolGuid { get; set; }
			public int Frequency { get; set; }
			public DateTime BarStartTime { get; set; }
			public int Order { get; set; }
			public double Open { get; set; }
			public double Close { get; set; }
			public double High { get; set; }
			public double Low { get; set; }
			public double Bid { get; set; }
			public double Ask { get; set; }
			public long Volume { get; set; }
			public int OpenInterest { get; set; }
			public bool EmptyBar { get; set; }
		}

		protected override System.Collections.IEnumerable GetDataReaderItems(IEnumerable<BarData> items, Guid symbolGuid)
		{
			return GetDataReaderBars(items, symbolGuid);
		}

		private IEnumerable<DataReaderBar> GetDataReaderBars(IEnumerable<BarData> bars, Guid symbolGuid)
		{
			DateTime lastDate = DateTime.MinValue;
			int order = 0;
			foreach (var b in bars)
			{
				if (b.BarStartTime == lastDate)
				{
					order++;
				}
				else
				{
					order = 0;
				}

				var drb = new DataReaderBar();

				drb.SymbolGuid = symbolGuid;
				drb.Frequency = _frequency;
				drb.BarStartTime = MakeValidSQLDate(b.BarStartTime, false).Value;
				drb.Order = order;
				drb.Open = b.Open;
				drb.Close = b.Close;
				drb.High = b.High;
				drb.Low = b.Low;
				drb.Bid = b.Bid;
				drb.Ask = b.Ask;
				drb.Volume = (long) b.Volume;
				drb.OpenInterest = b.OpenInterest;
				drb.EmptyBar = b.EmptyBar;

				yield return drb;
				lastDate = b.BarStartTime;
			}
		}

		//	TODO: Figure out how to make the Stored procedure call part of the transaction, so that when saving, the delete/add is part of the same transaction
		protected override void InternalDelete(DateTime start, DateTime end)
		{
			var SqlStart = MakeValidSQLDate(start, true);
			var SqlEnd = MakeValidSQLDate(end, false);
			using (var context = GetContext())
			{
				DBSymbol dbSymbol = GetSymbol(context, false);
				if (dbSymbol == null)
				{
					//  Symbol isn't in database at all, so there are no bars to delete
					return;
				}
				using (new TimeOperation("Delete bars"))
				{
					context.DeleteBars(dbSymbol.SymbolGuid, _frequency, SqlStart.Value, SqlEnd.Value);
				}
			}
			//Context.DBBars.DeleteAllOnSubmit(BarsForSymbol.Where(b => b.BarStartTime >= start && b.BarStartTime <= end));
		}

		public override DateTime GetDateTimeAtIndex(long index, out long numSameDatePreceding)
		{
			using (var context = GetContext())
			{
				//  TODO: Support long
				DBBar bar = GetItems(context).Skip((int)index).First();
				numSameDatePreceding = GetItems(context).LongCount(b => b.BarStartTime == bar.BarStartTime && b.Order < bar.Order);
				return bar.BarStartTime;
			}
		}
	}

	class LinqToSQLTickDataAccessor : LinqToSQLDataAccessor<TickData, DBTick>
	{
		public LinqToSQLTickDataAccessor(string connectionString, Symbol symbol)
			: base(connectionString, symbol)
		{
		}

		IQueryable<DBTick> GetItems(SQLDataContext context)
		{
            var dbSymbol = GetSymbol(context, false);
            if (dbSymbol == null)
            {
                return Enumerable.Empty<DBTick>().AsQueryable();
            }

            return context.DBTicks.Where(t => t.SymbolGuid == dbSymbol.SymbolGuid).OrderBy(t => t.Time).ThenBy(t => t.Order);

			//return context.DBTicks.Where(t => t.DBSymbol.SymbolUniqueID == _symbolUniqueID).OrderBy(t => t.Time).ThenBy(t => t.Order);
		}

		protected override IQueryable<DBTick> GetItemsByDate(SQLDataContext context, SqlDateTime start, SqlDateTime end)
		{
			return GetItems(context).Where(t => t.Time >= start.Value && t.Time <= end.Value);
		}

		protected override DateTime GetItemTime(TickData item)
		{
			return MakeValidSQLDate(item.time, false).Value;
		}

		protected override TickData ConvertToRightEdgeType(DBTick dbTick)
		{
			TickData ret = new TickData();
			ret.time = dbTick.Time;
			ret.tickType = (TickType)dbTick.TickType;
			ret.price = dbTick.Price;
			ret.size = (ulong) dbTick.Size;

			return ret;
		}

		//protected override DBTick ConvertToDBType(SQLDataContext context, TickData tick, int order, DBSymbol dbSymbol)
		//{
		//    DBTick dbTick = new DBTick();
		//    dbTick.DBSymbol = dbSymbol;
		//    dbTick.Time = tick.time;
		//    dbTick.Order = order;
		//    dbTick.TickType = (int)tick.tickType;
		//    dbTick.Price = tick.price;
		//    dbTick.Size = (long) tick.size;

		//    return dbTick;
		//}

		//protected override void InsertOnSubmit(SQLDataContext context, DBTick item)
		//{
		//    context.DBTicks.InsertOnSubmit(item);
		//}

		protected override string TableName
		{
			get { return "Tick"; }
		}

		private class DataReaderTick
		{
			public Guid SymbolGuid { get; set; }
			public DateTime Time { get; set; }
			public int Order { get; set; }
			public int TickType { get; set; }
			public double Price { get; set; }
			public long Size { get; set; }
		}

		protected override System.Collections.IEnumerable GetDataReaderItems(IEnumerable<TickData> items, Guid symbolGuid)
		{
			return GetDataReaderTicks(items, symbolGuid);
		}

		private IEnumerable<DataReaderTick> GetDataReaderTicks(IEnumerable<TickData> ticks, Guid symbolGuid)
		{
			DateTime lastDate = DateTime.MinValue;
			int order = 0;
			foreach (var t in ticks)
			{
				var drt = new DataReaderTick();

				drt.SymbolGuid = symbolGuid;
				drt.Time = MakeValidSQLDate(t.time, false).Value;

				if (drt.Time == lastDate)
				{
					order++;
				}
				else
				{
					order = 0;
				}


				drt.Order = order;
				drt.TickType = (int)t.tickType;
				drt.Price = t.price;
				drt.Size = (long)t.size;

				//Trace.WriteLine("Yielding tick: " + Util.FormatTimeStamp(drt.Time) + " " + drt.Order + " " + t.tickType + " " + drt.Price + " " + drt.Size);
				yield return drt;

				lastDate = drt.Time;
			}
		}


		//	TODO: Figure out how to make the Stored procedure call part of the transaction, so that when saving, the delete/add is part of the same transaction
		protected override void InternalDelete(DateTime start, DateTime end)
		{
			var SqlStart = MakeValidSQLDate(start, true);
			var SqlEnd = MakeValidSQLDate(end, false);

			using (var context = GetContext())
			{
				DBSymbol dbSymbol = GetSymbol(context, false);
				if (dbSymbol == null)
				{
					//  Symbol isn't in database at all, so there are no ticks to delete
					return;
				}
				using (new TimeOperation("Delete ticks"))
				{
					context.DeleteTicks(dbSymbol.SymbolGuid, SqlStart.Value, SqlEnd.Value);
				}
			}
		}

		public override DateTime GetDateTimeAtIndex(long index, out long numSameDatePreceding)
		{
			using (var context = GetContext())
			{
				DBTick tick = GetItems(context).Skip((int)index).First();
				numSameDatePreceding = GetItems(context).LongCount(t => t.Time == tick.Time && t.Order < tick.Order);
				return tick.Time;
			}
		}


	}
}
