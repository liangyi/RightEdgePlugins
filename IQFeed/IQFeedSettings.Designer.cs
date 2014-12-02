namespace IQFeed
{
	partial class IQFeedSettings
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.checkBoxIgnoreLastHistBar = new System.Windows.Forms.CheckBox();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.buttonOK = new System.Windows.Forms.Button();
            this.checkBoxFilterBasedOnCurrentTime = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.textBoxExchangeTimeDiff = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.textBoxMaxTimeDelta = new System.Windows.Forms.TextBox();
            this.checkBoxEnableLogging = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // checkBoxIgnoreLastHistBar
            // 
            this.checkBoxIgnoreLastHistBar.AutoSize = true;
            this.checkBoxIgnoreLastHistBar.Location = new System.Drawing.Point(9, 35);
            this.checkBoxIgnoreLastHistBar.Name = "checkBoxIgnoreLastHistBar";
            this.checkBoxIgnoreLastHistBar.Size = new System.Drawing.Size(142, 17);
            this.checkBoxIgnoreLastHistBar.TabIndex = 1;
            this.checkBoxIgnoreLastHistBar.Text = "&Ignore last historical bar";
            this.checkBoxIgnoreLastHistBar.UseVisualStyleBackColor = true;
            // 
            // buttonCancel
            // 
            this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.buttonCancel.Location = new System.Drawing.Point(183, 165);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.buttonCancel.TabIndex = 8;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            // 
            // buttonOK
            // 
            this.buttonOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.buttonOK.Location = new System.Drawing.Point(101, 165);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(75, 23);
            this.buttonOK.TabIndex = 7;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // checkBoxFilterBasedOnCurrentTime
            // 
            this.checkBoxFilterBasedOnCurrentTime.AutoSize = true;
            this.checkBoxFilterBasedOnCurrentTime.Location = new System.Drawing.Point(9, 58);
            this.checkBoxFilterBasedOnCurrentTime.Name = "checkBoxFilterBasedOnCurrentTime";
            this.checkBoxFilterBasedOnCurrentTime.Size = new System.Drawing.Size(163, 17);
            this.checkBoxFilterBasedOnCurrentTime.TabIndex = 2;
            this.checkBoxFilterBasedOnCurrentTime.Text = "&Filter ticks using current time";
            this.checkBoxFilterBasedOnCurrentTime.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 78);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(243, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "&Difference between exchange time and local time";
            // 
            // textBoxExchangeTimeDiff
            // 
            this.textBoxExchangeTimeDiff.Location = new System.Drawing.Point(12, 94);
            this.textBoxExchangeTimeDiff.Name = "textBoxExchangeTimeDiff";
            this.textBoxExchangeTimeDiff.Size = new System.Drawing.Size(160, 21);
            this.textBoxExchangeTimeDiff.TabIndex = 4;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(9, 118);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(101, 13);
            this.label2.TabIndex = 5;
            this.label2.Text = "&Maximum time delta";
            // 
            // textBoxMaxTimeDelta
            // 
            this.textBoxMaxTimeDelta.Location = new System.Drawing.Point(12, 134);
            this.textBoxMaxTimeDelta.Name = "textBoxMaxTimeDelta";
            this.textBoxMaxTimeDelta.Size = new System.Drawing.Size(160, 21);
            this.textBoxMaxTimeDelta.TabIndex = 6;
            // 
            // checkBoxEnableLogging
            // 
            this.checkBoxEnableLogging.AutoSize = true;
            this.checkBoxEnableLogging.Location = new System.Drawing.Point(9, 12);
            this.checkBoxEnableLogging.Name = "checkBoxEnableLogging";
            this.checkBoxEnableLogging.Size = new System.Drawing.Size(98, 17);
            this.checkBoxEnableLogging.TabIndex = 0;
            this.checkBoxEnableLogging.Text = "Enable &Logging";
            this.checkBoxEnableLogging.UseVisualStyleBackColor = true;
            // 
            // IQFeedSettings
            // 
            this.AcceptButton = this.buttonOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.buttonCancel;
            this.ClientSize = new System.Drawing.Size(270, 200);
            this.Controls.Add(this.checkBoxEnableLogging);
            this.Controls.Add(this.textBoxMaxTimeDelta);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textBoxExchangeTimeDiff);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.checkBoxFilterBasedOnCurrentTime);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.checkBoxIgnoreLastHistBar);
            this.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "IQFeedSettings";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "IQFeed Settings";
            this.Load += new System.EventHandler(this.IQFeedSettings_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.CheckBox checkBoxIgnoreLastHistBar;
		private System.Windows.Forms.Button buttonCancel;
		private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.CheckBox checkBoxFilterBasedOnCurrentTime;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox textBoxExchangeTimeDiff;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textBoxMaxTimeDelta;
        private System.Windows.Forms.CheckBox checkBoxEnableLogging;
	}
}