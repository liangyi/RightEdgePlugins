using System;
using System.Collections.Generic;
using System.Text;

using RightEdge.Common;
using RightEdge.Shared;

namespace RightEdge.RiskAssessment
{
	public class RecoveryFactorPlugin : IRiskAssessmentPlugin
	{
		private string helpString = "Recovery Factor is equal to the absolute value of Net Profit divided by Max Drawdown.  " + 
			"This value is particularly valuable for determining volatility.  A high recovery factor means that the " +
			"system has overcome a drawdown, however, a high drawdown may be unsatisfactory for the risk averse.";

		#region IRiskAssessmentPlugin Members

		public string GetName()
		{
			return "Recovery Factor";
		}

		public string GetDescription()
		{
			return "Recovery Factor";
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
			return "32000FE7-1AF3-450a-8C8A-4F8F46527A4C";
		}

		public string GetHelp()
		{
			return helpString;
		}

		public string GetClassName()
		{
			return Globals.Namespace + "RecoveryFactor";
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
