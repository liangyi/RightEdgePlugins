using System;
using System.Collections.Generic;
using System.Text;

using RightEdge.Common;
using RightEdge.Shared;

namespace RightEdge.RiskAssessment
{
	public class RiskAdjustedReturnPlugin : IRiskAssessmentPlugin
	{
		private string helpString = "The Risk Adjusted Return is the annual return " + 
			"percentage divided by the exposure percentage.";

		#region IRiskAssessmentPlugin Members

		public string GetName()
		{
			return "Risk Adjusted Return";
		}

		public string GetDescription()
		{
			return "Risk Adjusted Return";
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
			return "F5896028-208B-44b4-9210-A4A893487B30";
		}

		public string GetHelp()
		{
			return helpString;
		}

		public string GetClassName()
		{
			return Globals.Namespace + "RiskAdjustedReturn";
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
