using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Win32;

using RightEdge.Common;

namespace RightEdge.RiskAssessment
{
	public class SharpeRatioPlugin : IRiskAssessmentPlugin
	{
		private string helpString = "The Sharpe Ratio is a measure of the risk-adjusted return of an " +
			"investment.  It was conceived by Professor William Sharpe who received the Nobel Prize in" +
			"Economics in 1990.  It is calculated by subtracting the risk-free rate from the rate of " +
			"return for a portfolio and dividing the result by the standard deviation of the portfolio " +
			"returns.\r\nThe risk-free rate of return is a variable that is typically tied to the interest " +
			"rate on the three month U.S. Treasury Bill.  While theoretically, a risk-free rate of return " +
			"should be zero since all investment vehicles carry some degree of risk, it is generally " +
			"considered that a short term government issued securities will most likely not default.\r\n" +
			"A trading system with a smoothly increasing equity curve will have very consistent monthly " +
			"returns, a low standard deviation of returns, and a high Sharpe ratio.  In other words, " +
			"the higher the value of the Sharpe ratio the less chance to go broke.  A Sharpe ratio of " +
			"2.0 is considered good and a 3.0 is outstanding.  The ratio is also useful in determining " +
			"if the system was successful simply because of luck.  Be leary of a system that has a " +
			"dollar return but a low Sharpe ratio.";
		private string argName = "Risk-Free Rate of Return";

		#region IRiskAssessmentPlugin Members

		public string GetName()
		{
			return "Sharpe Ratio";
		}

		public string GetDescription()
		{
			return "Sharpe Ratio";
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
			return "B3842140-2253-4a52-909E-0646143EBFBF";
		}

		public string GetHelp()
		{
			return helpString;
		}

		public string GetClassName()
		{
			return Globals.Namespace + "SharpeRatio";
		}

		// Why yes I am storing these in the registry.  It would seem silly
		// to have an XML file for each risk assessment plugin that wanted
		// to persist values, or worse yet, to have some sort of "manager"
		// that kept a grouping of risk assessment plugin args.
		public List<RiskAssessmentArgument> GetArgumentValues()
		{
			List<RiskAssessmentArgument> args = null;
			RegistryKey regKey = Registry.CurrentUser.CreateSubKey(@"Software\Yye Software\" + id());
			object value = null;

			value = regKey.GetValue(argName);
			string argValue = "";

			if (value != null)
			{
				argValue = value.ToString();
			}
			else
			{
				argValue = "4.20";
			}

			args = new List<RiskAssessmentArgument>();
			RiskAssessmentArgument riskArg = new RiskAssessmentArgument(
				argName, RiskAssessmentArgumentType.Double, argValue, 1);
			args.Add(riskArg);

			regKey.Close();

			return args;
		}

		public void SetArgumentValues(List<RiskAssessmentArgument> values)
		{
			double riskFreeRate = 0.0;

			foreach (RiskAssessmentArgument arg in values)
			{
				if (arg.Name == argName)
				{
					riskFreeRate = Convert.ToDouble(arg.ArgumentValue);
				}
			}

			RegistryKey regKey = Registry.CurrentUser.CreateSubKey(@"Software\Yye Software");
			RegistryKey serverKey = regKey.CreateSubKey(id(), RegistryKeyPermissionCheck.ReadWriteSubTree);
			serverKey.SetValue(argName, riskFreeRate.ToString());
		}

		public RiskAssessmentResultType GetResultType()
		{
			return RiskAssessmentResultType.Value;
		}

		#endregion
	}
}
