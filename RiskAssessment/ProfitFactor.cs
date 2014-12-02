using System;
using System.Collections.Generic;
using System.Text;

using RightEdge.Common;
using RightEdge.Shared;

namespace RightEdge.RiskAssessment
{
	public class ProfitFactor : IRiskAssessment
	{
		#region IRiskAssessment Members

		public double PerformCalculation(RiskAssessmentCalculationType calculationType,
			SystemData systemData)
		{
			double profitFactor = double.NaN;

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
				if (barStat.RealizedGrossLoss != 0)
				{
					profitFactor = Math.Abs(barStat.RealizedGrossProfit / barStat.RealizedGrossLoss);
				}

			}

			return profitFactor;
		}

		#endregion

		//private double CalcBuyAndHold(SystemData baseSystem)
		//{
		//    return double.NaN;
		//}

		//private double CalcLongAndShort(SystemData baseSystem)
		//{
		//    //TradeResultAnalysis tradingAnalysis = new TradeResultAnalysis(baseSystem);

		//    //double grossProfit = tradingAnalysis.LongGrossProfit() + tradingAnalysis.ShortGrossProfit();
		//    //double grossLoss = tradingAnalysis.LongGrossLoss() + tradingAnalysis.ShortGrossLoss();

		//    //if (grossLoss != 0)
		//    //{
		//    //    return Math.Abs(grossProfit / grossLoss);
		//    //}
		//    //else
		//    //{
		//    //    return double.NaN;
		//    //}

		//    BarStatistic barStat = baseSystem.SystemStatistics.GetLastStatistic();
		//    double profitFactor = 0.0;

		//    if (barStat.RealizedGrossLoss != 0)
		//    {
		//        profitFactor = Math.Abs(barStat.RealizedGrossProfit / barStat.RealizedGrossLoss);
		//    }

		//    return profitFactor;
		//}

		//private double CalcLongOnly(SystemData baseSystem)
		//{
		//    //TradeResultAnalysis tradingAnalysis = new TradeResultAnalysis(baseSystem);

		//    //if (tradingAnalysis.LongGrossLoss() != 0)
		//    //{
		//    //    return Math.Abs(tradingAnalysis.LongGrossProfit() / tradingAnalysis.LongGrossLoss());
		//    //}
		//    //else
		//    //{
		//    //    return double.NaN;
		//    //}
		//    BarStatistic barStat = baseSystem.LongStatistics.GetLastStatistic();
		//    double profitFactor = 0.0;

		//    if (barStat.RealizedGrossLoss != 0)
		//    {
		//        profitFactor = Math.Abs(barStat.RealizedGrossProfit / barStat.RealizedGrossLoss);
		//    }

		//    return profitFactor;
		//}

		//private double CalcShortOnly(SystemData baseSystem)
		//{
		//    //TradeResultAnalysis tradingAnalysis = new TradeResultAnalysis(baseSystem);

		//    //if (tradingAnalysis.ShortGrossLoss() != 0)
		//    //{
		//    //    return Math.Abs(tradingAnalysis.ShortGrossProfit() / tradingAnalysis.ShortGrossLoss());
		//    //}
		//    //else
		//    //{
		//    //    return double.NaN;
		//    //}
		//    BarStatistic barStat = baseSystem.ShortStatistics.GetLastStatistic();
		//    double profitFactor = 0.0;

		//    if (barStat.RealizedGrossLoss != 0)
		//    {
		//        profitFactor = Math.Abs(barStat.RealizedGrossProfit / barStat.RealizedGrossLoss);
		//    }

		//    return profitFactor;
		//}
	}
}
