using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Krs.Ats.IBNet;

namespace RightEdge.TWSCSharpPlugin
{
	public partial class TWSSettings : Form
	{
        public TWSPlugin.Settings Settings { get; set; }


		public TWSSettings()
		{
			InitializeComponent();
		}

		private void TWSSettings_Load(object sender, EventArgs e)
		{
			checkBoxRetrieveRTHOnly.Checked = Settings.UseRTH;
            checkBoxIgnoreLastHistBar.Checked = Settings.IgnoreLastHistBar;

            checkBoxEnableLogging.Checked = Settings.EnableLogging;
            textBoxLogPath.Text = Settings.LogPath;
            checkBoxDeleteLogs.Checked = Settings.CleanupLogs;
            textBoxDaysToKeepLogs.Text = Settings.DaysToKeepLogs.ToString();

            textBoxClientIDBroker.Text = Settings.ClientIDBroker.ToString();
            textBoxClientIDLiveData.Text = Settings.ClientIDLiveData.ToString();
            textBoxClientIDHist.Text = Settings.ClientIDHist.ToString();

			foreach (var faValue in Enum.GetValues(typeof(FinancialAdvisorAllocationMethod)))
			{
				comboBoxFAMethod.Items.Add(faValue);
			}

            textBoxAccountCode.Text = Settings.AccountCode;
            textBoxFAProfile.Text = Settings.FAProfile;
            comboBoxFAMethod.SelectedItem = Settings.FAMethod;
            textBoxFAPercentage.Text = Settings.FAPercentage;
		}

		private void buttonOK_Click(object sender, EventArgs e)
		{
            Settings.UseRTH = checkBoxRetrieveRTHOnly.Checked;
            Settings.IgnoreLastHistBar = checkBoxIgnoreLastHistBar.Checked;

            Settings.EnableLogging = checkBoxEnableLogging.Checked;
            Settings.LogPath = textBoxLogPath.Text;
            Settings.CleanupLogs = checkBoxDeleteLogs.Checked;

            int i;

            if (int.TryParse(textBoxDaysToKeepLogs.Text, out i))
            {
                Settings.DaysToKeepLogs = i;
            }
            else
            {
                MessageBox.Show(this, "Days to keep logs must be an integer");
                DialogResult = System.Windows.Forms.DialogResult.None;
                textBoxDaysToKeepLogs.Select();
            }

            if (int.TryParse(textBoxClientIDBroker.Text, out i))
            {
                Settings.ClientIDBroker = i;
            }
            else
            {
                MessageBox.Show(this, "Broker client ID must be an integer");
                DialogResult = System.Windows.Forms.DialogResult.None;
                textBoxClientIDBroker.Select();
            }

            if (int.TryParse(textBoxClientIDLiveData.Text, out i))
            {
                Settings.ClientIDLiveData = i;
            }
            else
            {
                MessageBox.Show(this, "Live Data client ID must be an integer");
                DialogResult = System.Windows.Forms.DialogResult.None;
                textBoxClientIDLiveData.Select();
            }

            if (int.TryParse(textBoxClientIDHist.Text, out i))
            {
                Settings.ClientIDHist = i;
            }
            else
            {
                MessageBox.Show(this, "Historical Data client ID must be an integer");
                DialogResult = System.Windows.Forms.DialogResult.None;
                textBoxClientIDHist.Select();
            }

            Settings.AccountCode = textBoxAccountCode.Text;
            Settings.FAProfile = textBoxFAProfile.Text;
            Settings.FAMethod = (FinancialAdvisorAllocationMethod)comboBoxFAMethod.SelectedItem;
            Settings.FAPercentage = textBoxFAPercentage.Text;			
		}
	}
}