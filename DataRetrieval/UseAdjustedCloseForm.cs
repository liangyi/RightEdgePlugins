using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace RightEdge.DataRetrieval
{
	public partial class UseAdjustedCloseForm : Form
	{
		private bool useAdjustedClose;
		public bool UseAdjustedClose
		{
			get
			{
				return useAdjustedClose;
			}
			set
			{
				useAdjustedClose = value;
			}
		}

		public UseAdjustedCloseForm()
		{
			InitializeComponent();
		}

		private void UseAdjustedCloseForm_Load(object sender, EventArgs e)
		{
			checkBoxUseAdjustedClose.Checked = useAdjustedClose;
		}

		private void buttonOK_Click(object sender, EventArgs e)
		{
			useAdjustedClose = checkBoxUseAdjustedClose.Checked;
		}
	}
}