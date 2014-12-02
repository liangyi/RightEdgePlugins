using System;
using System.Collections.Generic;
using System.Text;

using RightEdge.Common;

namespace RightEdge.RiskAssessment
{
	public class ProfitFactorPlugin : IRiskAssessmentPlugin
	{
		private string helpString = "The Profit Factor is calculated by dividing " +
			"the gross profits by the gross losses.  This gives a general idea " +
			"of the potential reward (or risk) of the system.  A higher profit " +
			"factor is better.  The ideal figure for profit factor is anything " +
			"2.0 or higher.";

		#region IRiskAssessmentPlugin Members

		public string GetName()
		{
			return "Profit Factor";
		}

		public string GetDescription()
		{
			return "Profit Factor";
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
			return "2CC62B3D-9FD9-4b0c-B6D3-8BE9359FAEDC";
		}

		public string GetHelp()
		{
			return helpString;
		}

		public string GetClassName()
		{
			return Globals.Namespace + "ProfitFactor";
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
