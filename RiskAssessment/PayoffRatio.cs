using System;
using System.Collections.Generic;
using System.Text;

using RightEdge.Common;
using RightEdge.Shared;

namespace RightEdge.RiskAssessment
{
	// Ratio average win / average loss
	public class PayoffRatio : IRiskAssessment
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
				if (barStat.AverageLoss != 0)
				{
					value = barStat.AverageWin / Math.Abs(barStat.AverageLoss);
				}

			}

			return value;
		}

		#endregion

		//private double CalcBuyAndHold(SystemData baseSystem)
		//{
		//    return double.NaN;
		//}

		//private double CalcLongAndShort(SystemData baseSystem)
		//{
		//    //TradeResultAnalysis tradingAnalysis = new TradeResultAnalysis(baseSystem);

		//    //double longAvgWinning = tradingAnalysis.LongAvgWinningProfit();
		//    //double shortAvgWinning = tradingAnalysis.ShortAvgWinningProfit();
		//    //double winningAvg = 0.0;

		//    //if (longAvgWinning > 0 && shortAvgWinning > 0)
		//    //{
		//    //    winningAvg = (longAvgWinning + shortAvgWinning) / 2.0;
		//    //}
		//    //else
		//    //{
		//    //    winningAvg = longAvgWinning + shortAvgWinning;
		//    //}

		//    //double longAvgLosing = tradingAnalysis.LongAvgLosingProfit();
		//    //double shortAvgLosing = tradingAnalysis.ShortAvgLosingProfit();
		//    //double losingAvg = 0.0;

		//    //if (longAvgLosing > 0 && shortAvgLosing > 0)
		//    //{
		//    //    losingAvg = (longAvgLosing + shortAvgLosing) / 2.0;
		//    //}
		//    //else
		//    //{
		//    //    losingAvg = longAvgLosing + shortAvgLosing;
		//    //}

		//    //return winningAvg / Math.Abs(losingAvg);
		//    BarStatistic barStat = baseSystem.SystemStatistics.GetLastStatistic();
		//    double payoffRatio = 0.0;

		//    if (barStat.AverageLoss != 0)
		//    {
		//        payoffRatio = ((double)barStat.AverageWin / Math.Abs(barStat.AverageLoss));
		//    }

		//    return payoffRatio;
		//}

		//private double CalcLongOnly(SystemData baseSystem)
		//{
		//    //TradeResultAnalysis tradingAnalysis = new TradeResultAnalysis(baseSystem);

		//    //double longAvgWinning = tradingAnalysis.LongAvgWinningProfit();
		//    //double longAvgLosing = tradingAnalysis.LongAvgLosingProfit();

		//    //return longAvgWinning / Math.Abs(longAvgLosing);
		//    BarStatistic barStat = baseSystem.LongStatistics.GetLastStatistic();
		//    double payoffRatio = 0.0;

		//    if (barStat.AverageLoss != 0)
		//    {
		//        payoffRatio = ((double)barStat.AverageWin / Math.Abs(barStat.AverageLoss));
		//    }

		//    return payoffRatio;
		//}

		//private double CalcShortOnly(SystemData baseSystem)
		//{
		//    //TradeResultAnalysis tradingAnalysis = new TradeResultAnalysis(baseSystem);

		//    //double shortAvgWinning = tradingAnalysis.ShortAvgWinningProfit();
		//    //double shortAvgLosing = tradingAnalysis.ShortAvgLosingProfit();

		//    //return shortAvgWinning / Math.Abs(shortAvgLosing);

		//    BarStatistic barStat = baseSystem.LongStatistics.GetLastStatistic();
		//    double payoffRatio = 0.0;

		//    if (barStat.AverageLoss != 0)
		//    {
		//        payoffRatio = ((double)barStat.AverageWin / Math.Abs(barStat.AverageLoss));
		//    }

		//    return payoffRatio;
		//}
	}
}
