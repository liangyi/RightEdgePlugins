using System;
using System.Collections.Generic;
using System.Text;

using RightEdge.Common;
using RightEdge.Shared;

namespace RightEdge.RiskAssessment
{
	public class PayoffRatioPlugin : IRiskAssessmentPlugin
	{
		private string helpString = "Payoff Ratio is the absolute value of the system's average win per trade divided by the average loss per trade.  " +
			"The higher the number, the better.";

		#region IRiskAssessmentPlugin Members

		public string GetName()
		{
			return "Payoff Ratio";
		}

		public string GetDescription()
		{
			return "Payoff Ratio";
		}

		public string GetAuthor()
		{
			return Globals.Author;
		}

		public string GetCompanyName()
		{
			return Globals.Author;
		}

		public string GetVersion()
		{
			return Globals.Version;
		}

		public string id()
		{
			return "D79F00DB-7A8B-4e9e-B0F2-E3438114A6BD";
		}

		public string GetHelp()
		{
			return helpString;
		}

		public string GetClassName()
		{
			return Globals.Namespace + "PayoffRatio";
		}

		public List<RiskAssessmentArgument> GetArgumentValues()
		{
			return null;
		}

		public void SetArgumentValues(List<RiskAssessmentArgument> values)
		{
		}

		public RiskAssessmentResultType GetResultType()
		{
			return RiskAssessmentResultType.Value;
		}

		#endregion
	}
}
