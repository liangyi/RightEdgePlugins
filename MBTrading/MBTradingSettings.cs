using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MBTrading
{
	public partial class MBTradingSettings : Form
	{
		private string sdkId;
		public string SDKID
		{
			get
			{
				return sdkId;
			}
			set
			{
				sdkId = value;
			}
		}

		public MBTradingSettings()
		{
			InitializeComponent();
		}

		private void MBTradingSettings_Load(object sender, EventArgs e)
		{
			textBoxSDKID.Text = sdkId;
		}

		private void buttonOK_Click(object sender, EventArgs e)
		{
			sdkId = textBoxSDKID.Text;
		}
	}
}