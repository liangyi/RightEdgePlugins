using System;
using System.Collections.Generic;
using System.Text;

using RightEdge.Common;
using RightEdge.Shared;
using System.IO;
using System.Globalization;

namespace RightEdge.RiskAssessment
{
	public class SharpeRatio : IRiskAssessment
	{
		private double riskFreeRate = 4.20;
		private List<PositionInfo> allPositions = null;
		private List<PositionInfo> longPositions = null;
		private List<PositionInfo> shortPositions = null;

		public SharpeRatio()
		{
		}

		[RiskAssessmentArgument("Risk-Free Rate of Return", RiskAssessmentArgumentType.Double, "4.2", 1)]
		//[ConstructorArgument("Risk-Free Rate of Return", ConstructorArgumentType.Double, 4.2, 1)]
		public SharpeRatio(double riskFreeRate)
		{
			this.riskFreeRate = riskFreeRate;
		}

		#region IRiskAssessment Members

		public double PerformCalculation(RiskAssessmentCalculationType calculationType,
			SystemData baseSystem)
		{
			double value = double.NaN;

			if (allPositions == null)
			{
				BuildPositionList(baseSystem);
			}

			double value2 = 0.0;

			switch (calculationType)
			{
				case RiskAssessmentCalculationType.BuyAndHold:
					value = CalcBuyAndHold(baseSystem);
					value2 = CalcSharpe(baseSystem.SystemHistory.BuyAndHoldStatistics, calculationType);
					break;

				case RiskAssessmentCalculationType.LongAndShort:
					value = CalcLongAndShort(baseSystem);
					value2 = CalcSharpe(baseSystem.SystemHistory.SystemStatistics, calculationType);
					break;

				case RiskAssessmentCalculationType.LongOnly:
					value = CalcLongOnly(baseSystem);
					value2 = CalcSharpe(baseSystem.SystemHistory.LongStatistics, calculationType);
					break;

				case RiskAssessmentCalculationType.ShortOnly:
					value = CalcShortOnly(baseSystem);
					value2 = CalcSharpe(baseSystem.SystemHistory.ShortStatistics, calculationType);
					break;
			}

			string s = string.Format("Old Sharpe: {0}\tNewSharpe: {1}", value, value2);

			System.Diagnostics.Trace.WriteLine(s);

			return value2;
		}

		#endregion

		private void BuildPositionList(SystemData systemData)
		{
			IList<Position> openPositions = systemData.PositionManager.GetOpenPositions();
			IList<Position> closedPositions = systemData.PositionManager.GetClosedPositions();

			allPositions = new List<PositionInfo>();
			longPositions = new List<PositionInfo>();
			shortPositions = new List<PositionInfo>();

			foreach (Position position in openPositions)
			{
				allPositions.Add(position.Info);

				if (position.Type == PositionType.Long)
				{
					longPositions.Add(position.Info);
				}
				else if (position.Type == PositionType.Short)
				{
					shortPositions.Add(position.Info);
				}
				else
				{
					throw new RightEdgeError("Unexpected position type: " + position.Type);
				}
			}

			foreach (Position position in closedPositions)
			{
				allPositions.Add(position.Info);

				if (position.Type == PositionType.Long)
				{
					longPositions.Add(position.Info);
				}
				else if (position.Type == PositionType.Short)
				{
					shortPositions.Add(position.Info);
				}
				else
				{
					throw new RightEdgeError("Unexpected position type: " + position.Type);
				}
			}
		}

		private double CalcBuyAndHold(SystemData baseSystem)
		{
			IList<PositionInfo> positions = baseSystem.SystemHistory.BuyAndHoldPositions;

			return CalcSharpe(positions, baseSystem);
		}

		private double CalcLongAndShort(SystemData baseSystem)
		{
			return CalcSharpe(allPositions, baseSystem);
		}

		private double CalcLongOnly(SystemData baseSystem)
		{
			return CalcSharpe(longPositions, baseSystem);
		}

		private double CalcShortOnly(SystemData baseSystem)
		{
			return CalcSharpe(shortPositions, baseSystem);
		}

		//private double CalcSharpe(List<Position> positions, SystemData baseSystem)
		//{
		//    List<double> calcSet = new List<double>();
		//    double averageProfit = 0.0;
		//    double returnDeviation = 0.0;
		//    double averageBarsHeld = 0.0;

		//    foreach (Position position in positions)
		//    {
		//        List<BarData> bars = baseSystem.BarCollections[position.Symbol];
		//        int openIndex = BarUtils.BarIndexFromDate(bars, position.OpenDate);
		//        int closeIndex = BarUtils.BarIndexFromDate(bars, position.CloseDate);
		//        BarData lastBar = BarUtils.LastValidBar(baseSystem.BarCollections[position.Symbol]);

		//        if (closeIndex == -1)
		//        {
		//            closeIndex = BarUtils.BarIndexFromDate(bars, lastBar.PriceDateTime);
		//        }

		//        PositionStats stats = position.GetCloseStats(lastBar.Close, lastBar.PriceDateTime);

		//        calcSet.Add(stats.RealizedProfit);
		//        averageProfit += stats.RealizedProfit;
		//        averageBarsHeld += closeIndex - openIndex;
		//    }

		//    averageProfit /= (double)positions.Count;
		//    averageBarsHeld /= (double)positions.Count;
		//    returnDeviation = StdDev(calcSet);

		//    double annualizedProfit = averageProfit / averageBarsHeld;
		//    double annualizedDeviation = returnDeviation / averageBarsHeld;

		//    return annualizedProfit / annualizedDeviation;
		//}

		private double CalcSharpe(SystemStatistics statistics, RiskAssessmentCalculationType calculationType)
		{
			DateTime currentStart = DateTime.MinValue;
			double monthEndValue = -1;
			double monthStartValue = 0.0;
			double prevValue = 0.0;
			double totalProfit = 0.0;
			//double prevValue = -1;
			List<double> monthlyReturns = new List<double>();
			double totalExcess = 0.0;

			bool monthStarted = false;

			//StreamWriter sw = new StreamWriter(@"C:\RE\Sharpe\sharpe" + "-" + calculationType.ToString() + ".csv");

			foreach (BarStatistic barStat in statistics.BarStats.Values)
			{
				if (monthEndValue < 0)
				{
					monthEndValue = barStat.AccountValue;
					monthStartValue = barStat.AccountValue;
					prevValue = barStat.AccountValue;
				}

				if (barStat.DisplayDate < barStat.TradeStartDate)
				{
					continue;
				}

				if (currentStart == DateTime.MinValue)
				{
					currentStart = barStat.DisplayDate;
				}

				if (currentStart.Month != barStat.DisplayDate.Month || currentStart.Year != barStat.DisplayDate.Year)
				{
					//	New month
					//double profit = (barStat.AccountValue / prevValue) - 1.0;
					//double profit = prevProfit - monthEndProfit;

					double profit;
					if (false)
					{
						//profit = barStat.AccountValue / monthStartValue - 1.0;
					}
					else
					{
						profit = prevValue / monthEndValue - 1.0;
					}

					
					double excess = profit - (riskFreeRate / (12.0 * 100.0));

					monthlyReturns.Add(profit);

					//sw.WriteLine(currentStart.ToString() + "," +
					//    prevValue.ToString(CultureInfo.InvariantCulture) + "," +
					//    //barStat.AccountValue.ToString(CultureInfo.InvariantCulture) + "," +
					//    (profit * 100).ToString(CultureInfo.InvariantCulture));

					totalProfit += profit;
					totalExcess += excess;

					//monthEndProfit = prevProfit;
					//prevProfit = barStat.NetProfit;
					//currentStart = DateTime.MinValue;
					currentStart = barStat.DisplayDate;
					monthEndValue = prevValue;
					monthStartValue = barStat.AccountValue;
					prevValue = barStat.AccountValue;
					monthStarted = true;
				}
				else
				{
					//prevProfit = barStat.NetProfit;
					prevValue = barStat.AccountValue;
					monthStarted = true;
				}

				
			}

			if (monthStarted)
			{
				double profit = statistics.BarStats.Values[statistics.BarStats.Values.Count - 1].AccountValue / monthEndValue - 1.0;
				monthlyReturns.Add(profit);

				double excess = profit - (riskFreeRate / (12.0 * 100.0));
				totalProfit += profit;
				totalExcess += excess;
			}

			//sw.Close();
			

			double avgProfit = totalProfit / (double)monthlyReturns.Count;
			double avgExcess = totalExcess / (double)monthlyReturns.Count;
			double stddev = StdDev(monthlyReturns);
			if (stddev == 0.0)
			{
				return double.NaN;
			}
			double sharpe = (avgExcess * 12) / (stddev * Math.Sqrt(12));
			//double sharpe = (avgProfit ) / (stddev * Math.Sqrt(12));

			//double avgpr = StatisticalFunctions.Mean(monthlyReturns);
			//double stddev2 = StatisticalFunctions.StdDev(monthlyReturns);

			//double sharpe2 = (avgpr * 12 - riskFreeRate / 100.0) / (stddev2 * Math.Sqrt(12));

			return sharpe;
		}

		private double CalcSharpe(IList<PositionInfo> positions, SystemData systemData)
		{
			if (positions.Count == 0)
			{
				return 0;
			}

			// Returns for each month
			List<double> monthlyReturns = new List<double>();
			DateTime currentMonth = DateTime.MinValue;
			double profitLoss = 0.0;
			double totalProfit = 0.0;

			foreach (PositionInfo position in positions)
			{
				RList<BarData> bars = systemData.SystemBars[position.Symbol];
				//int openIndex = BarUtils.BarIndexFromDate(bars, position.OpenDate);
				//int closeIndex = BarUtils.BarIndexFromDate(bars, position.CloseDate);
				BarData lastBar = bars.Current;

				//if (closeIndex == -1)
				//{
				//    closeIndex = 0;
				//}

				PositionStats stats = position.GetCloseStats(lastBar.Close, lastBar.BarStartTime, systemData.AccountInfo);

				if (currentMonth == DateTime.MinValue)
				{
					currentMonth = lastBar.BarStartTime;
				}

				if (currentMonth.Month != lastBar.BarStartTime.Month)
				{
					monthlyReturns.Add(profitLoss);
					profitLoss = 0.0;
				}

				profitLoss += stats.RealizedProfit;
				totalProfit += stats.RealizedProfit;
			}

			double avgProfit = totalProfit / (double)monthlyReturns.Count;
			double stddev = StdDev(monthlyReturns);
			double sharpe = avgProfit / stddev;

			return sharpe;
		}

		private double StdDev(List<double> calcSet)
		{
			double sumOfSet = 0.00;
			double sumOfSquares = 0.00;
			double mean = 0.00;
			double stdev = 0.00;
			double[] squares = new double[calcSet.Count];

			foreach (double val in calcSet)
			{
				sumOfSet += val;
			}

			// 1. Calculate the mean of the set
			mean = sumOfSet / calcSet.Count;

			// 2. Square the difference of the mean from each value
			// 3. Sum the squares
			foreach (double val in calcSet)
			{
				sumOfSquares += System.Math.Pow(val - mean, 2);
			}

			// 4. Take square root of the summed squares divided by sample or population count
			stdev = System.Math.Sqrt(sumOfSquares / (calcSet.Count));

			return stdev;
		}
	}
}
