using System;
using System.Collections.Generic;
using System.Text;

using RightEdge.Common;
using RightEdge.Shared;

namespace RightEdge.RiskAssessment
{
	public class RecoveryFactor : IRiskAssessment
	{
		#region IRiskAssessment Members

		public double PerformCalculation(RiskAssessmentCalculationType calculationType,
			SystemData systemData)
		{
			double value = double.NaN;

			SystemStatistics stat = null;

			switch (calculationType)
			{
				case RiskAssessmentCalculationType.BuyAndHold:
					stat = systemData.BuyAndHoldStatistics;
					//value = CalcBuyAndHold(systemData);
					break;

				case RiskAssessmentCalculationType.LongAndShort:
					stat = systemData.SystemStatistics;
					//value = CalcLongAndShort(systemData);
					break;

				case RiskAssessmentCalculationType.LongOnly:
					stat = systemData.LongStatistics;
					//value = CalcLongOnly(systemData);
					break;

				case RiskAssessmentCalculationType.ShortOnly:
					stat = systemData.ShortStatistics;
					//value = CalcShortOnly(systemData);
					break;
			}

			if (stat != null)
			{
				BarStatistic barStat = systemData.SystemHistory.GetFinalStatistics(stat);
				if (barStat.MaxDrawDown != 0)
				{
					value = barStat.NetProfit / barStat.MaxDrawDown;
				}

			}

			return value;
		}

		#endregion

		//private double CalcBuyAndHold(SystemData baseSystem)
		//{
		//    //TradeResultAnalysis tradingAnalysis = new TradeResultAnalysis(baseSystem);
		//    //List<Position> positions = tradingAnalysis.BuildBuyAndHoldPositions();
		//    //DateTime date = DateTime.MinValue;
		//    //double drawDown = tradingAnalysis.CalculateMaxDrawDown(positions, ref date);

		//    //double netProfit = tradingAnalysis.BuyAndHoldNetProfit(positions);

		//    //if (drawDown != 0)
		//    //{
		//    //    return Math.Abs(netProfit) / drawDown;
		//    //}
		//    //else
		//    //{
		//    //    return double.NaN;
		//    //}

		//    BarStatistic barStat = baseSystem.BuyAndHoldStatistics.GetLastStatistic();
		//    double recoveryFactor = 0.0;

		//    if (barStat.DrawDown != 0)
		//    {
		//        recoveryFactor = Math.Abs(barStat.NetProfit) / barStat.DrawDown;
		//    }

		//    return recoveryFactor;
		//}

		//private double CalcLongAndShort(SystemData baseSystem)
		//{
		//    //TradeResultAnalysis tradingAnalysis = new TradeResultAnalysis(baseSystem);

		//    //double netProfit = tradingAnalysis.LongNetProfit() + tradingAnalysis.ShortNetProfit();
		//    //DateTime date = DateTime.MinValue;
		//    //double drawdownDollar = 0.0;
		//    //double maxDrawDown = tradingAnalysis.CalculateMaxDrawDown(ref date, ref drawdownDollar);

		//    //if (drawdownDollar != 0)
		//    //{
		//    //    return Math.Abs(netProfit) / drawdownDollar;
		//    //}
		//    //else
		//    //{
		//    //    return double.NaN;
		//    //}
		//    BarStatistic barStat = baseSystem.SystemStatistics.GetLastStatistic();
		//    double recoveryFactor = 0.0;

		//    if (barStat.DrawDown != 0)
		//    {
		//        recoveryFactor = Math.Abs(barStat.NetProfit) / barStat.MaxDrawDown;
		//    }

		//    return recoveryFactor;
		//}

		//private double CalcLongOnly(SystemData baseSystem)
		//{
		//    //TradeResultAnalysis tradingAnalysis = new TradeResultAnalysis(baseSystem);

		//    //double netProfit = tradingAnalysis.LongNetProfit();
		//    //DateTime date = DateTime.MinValue;
		//    //double drawdownDollar = 0.0;
		//    //double maxDrawDown = tradingAnalysis.CalculateMaxDrawDown(PositionType.Long, ref date, ref drawdownDollar);

		//    //if (drawdownDollar != 0)
		//    //{
		//    //    return Math.Abs(netProfit) / drawdownDollar;
		//    //}
		//    //else
		//    //{
		//    //    return double.NaN;
		//    //}
		//    BarStatistic barStat = baseSystem.LongStatistics.GetLastStatistic();
		//    double recoveryFactor = 0.0;

		//    if (barStat.DrawDown != 0)
		//    {
		//        recoveryFactor = Math.Abs(barStat.NetProfit) / barStat.MaxDrawDown;
		//    }

		//    return recoveryFactor;
		//}

		//private double CalcShortOnly(SystemData baseSystem)
		//{
		//    //TradeResultAnalysis tradingAnalysis = new TradeResultAnalysis(baseSystem);

		//    //double netProfit = tradingAnalysis.ShortNetProfit();
		//    //DateTime date = DateTime.MinValue;
		//    //double drawdownDollar = 0.0;
		//    //double maxDrawDown = tradingAnalysis.CalculateMaxDrawDown(PositionType.Short, ref date, ref drawdownDollar);

		//    //if (drawdownDollar != 0)
		//    //{
		//    //    return Math.Abs(netProfit) / drawdownDollar;
		//    //}
		//    //else
		//    //{
		//    //    return double.NaN;
		//    //}
		//    BarStatistic barStat = baseSystem.ShortStatistics.GetLastStatistic();
		//    double recoveryFactor = 0.0;

		//    if (barStat.DrawDown != 0)
		//    {
		//        recoveryFactor = Math.Abs(barStat.NetProfit) / barStat.MaxDrawDown;
		//    }

		//    return recoveryFactor;
		//}
	}
}
