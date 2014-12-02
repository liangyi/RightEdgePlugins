using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace IQFeed
{
	public partial class IQFeedSettings : Form
	{
        public IQFeedService.Settings Settings { get; set; }

		public IQFeedSettings()
		{
			InitializeComponent();
		}

		private void IQFeedSettings_Load(object sender, EventArgs e)
		{
            checkBoxEnableLogging.Checked = Settings.EnableLogging;
            checkBoxIgnoreLastHistBar.Checked = Settings.IgnoreLastHistBar;
            checkBoxFilterBasedOnCurrentTime.Checked = Settings.FilterUsingCurrentTime;
            textBoxExchangeTimeDiff.Text = Settings.ExchangeTimeDiff.ToString();
            textBoxMaxTimeDelta.Text = Settings.MaxTimeDelta.ToString();
		}

		private void buttonOK_Click(object sender, EventArgs e)
		{
            Settings.EnableLogging = checkBoxEnableLogging.Checked;
            Settings.IgnoreLastHistBar = checkBoxIgnoreLastHistBar.Checked;
            Settings.FilterUsingCurrentTime = checkBoxFilterBasedOnCurrentTime.Checked;
            TimeSpan timeDiff;
            if (!TimeSpan.TryParse(textBoxExchangeTimeDiff.Text, out timeDiff))
            {
                MessageBox.Show(this, "Invalid time span: " + textBoxExchangeTimeDiff.Text, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                textBoxExchangeTimeDiff.Select();
                DialogResult = System.Windows.Forms.DialogResult.None;
                return;
            }
            Settings.ExchangeTimeDiff = timeDiff;

            TimeSpan timeDelta;
            if (!TimeSpan.TryParse(textBoxMaxTimeDelta.Text, out timeDelta))
            {
                MessageBox.Show(this, "Invalid time span: " + textBoxMaxTimeDelta.Text, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                textBoxMaxTimeDelta.Select();
                DialogResult = System.Windows.Forms.DialogResult.None;
                return;
            }
            Settings.MaxTimeDelta = timeDelta;
		}
	}
}
