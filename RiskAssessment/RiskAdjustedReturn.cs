using System;
using System.Collections.Generic;
using System.Text;

using RightEdge.Common;
using RightEdge.Shared;

namespace RightEdge.RiskAssessment
{
	public class RiskAdjustedReturn : IRiskAssessment
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
				if (barStat.AverageExposurePct != 0)
				{
					value = barStat.APR / barStat.AverageExposurePct;
				}

			}

			return value;
		}

		#endregion

		//private double CalcBuyAndHold(SystemData baseSystem)
		//{
		//    //TradeResultAnalysis tradingAnalysis = new TradeResultAnalysis(baseSystem);

		//    //List<Position> positions = tradingAnalysis.BuildBuyAndHoldPositions();

		//    //double apr = tradingAnalysis.BuyAndHoldAPR(positions);
		//    //double exposure = tradingAnalysis.BuyAndHoldExposure(positions);

		//    //if (exposure != 0)
		//    //{
		//    //    return apr / exposure;
		//    //}
		//    //else
		//    //{
		//    //    return double.NaN;
		//    //}
		//    BarStatistic barStat = baseSystem.BuyAndHoldStatistics.GetLastStatistic();
		//    double riskAdjustedReturn = 0.0;

		//    if (barStat.AvgExposurePct != 0)
		//    {
		//        riskAdjustedReturn = barStat.APR / barStat.AvgExposurePct;
		//    }

		//    return riskAdjustedReturn;
		//}

		//private double CalcLongAndShort(SystemData baseSystem)
		//{
		//    //TradeResultAnalysis tradingAnalysis = new TradeResultAnalysis(baseSystem);

		//    //double apr = tradingAnalysis.AnnualizedPercentageReturn();
		//    //double exposure = tradingAnalysis.Exposure();

		//    //if (exposure != 0)
		//    //{
		//    //    return apr / exposure;
		//    //}
		//    //else
		//    //{
		//    //    return double.NaN;
		//    //}
		//    BarStatistic barStat = baseSystem.SystemStatistics.GetLastStatistic();
		//    double riskAdjustedReturn = 0.0;

		//    if (barStat.AvgExposurePct != 0)
		//    {
		//        riskAdjustedReturn = barStat.APR / barStat.AvgExposurePct;
		//    }

		//    return riskAdjustedReturn;
		//}

		//private double CalcLongOnly(SystemData baseSystem)
		//{
		//    //TradeResultAnalysis tradingAnalysis = new TradeResultAnalysis(baseSystem);

		//    //double apr = tradingAnalysis.AnnualizedPercentageReturn(PositionType.Long);
		//    //double exposure = tradingAnalysis.Exposure(PositionType.Long);

		//    //if (exposure != 0)
		//    //{
		//    //    return apr / exposure;
		//    //}
		//    //else
		//    //{
		//    //    return double.NaN;
		//    //}
		//    BarStatistic barStat = baseSystem.LongStatistics.GetLastStatistic();
		//    double riskAdjustedReturn = 0.0;

		//    if (barStat.AvgExposurePct != 0)
		//    {
		//        riskAdjustedReturn = barStat.APR / barStat.AvgExposurePct;
		//    }

		//    return riskAdjustedReturn;
		//}

		//private double CalcShortOnly(SystemData baseSystem)
		//{
		//    //TradeResultAnalysis tradingAnalysis = new TradeResultAnalysis(baseSystem);

		//    //double apr = tradingAnalysis.AnnualizedPercentageReturn(PositionType.Short);
		//    //double exposure = tradingAnalysis.Exposure(PositionType.Short);

		//    //if (exposure != 0)
		//    //{
		//    //    return apr / exposure;
		//    //}
		//    //else
		//    //{
		//    //    return double.NaN;
		//    //}
		//    BarStatistic barStat = baseSystem.ShortStatistics.GetLastStatistic();
		//    double riskAdjustedReturn = 0.0;

		//    if (barStat.AvgExposurePct != 0)
		//    {
		//        riskAdjustedReturn = barStat.APR / barStat.AvgExposurePct;
		//    }

		//    return riskAdjustedReturn;
		//}
	}
}
