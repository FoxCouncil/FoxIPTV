// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Forms
{
    using System;
    using System.Windows.Forms;
    using Classes;
    using Controls;
    using Properties;

    public sealed partial class GuideForm : Form
    {
        public GuideForm()
        {
            DoubleBuffered = true;

            InitializeComponent();

            var guideControl = new GuideLayoutPanel();

            tableLayoutPanel.Controls.Add(guideControl, 0, 1);
            tableLayoutPanel.SetColumnSpan(guideControl, 3);

            buttonResetView.Click += (s, a) => guideControl.ResetView();

            ResizeEnd += (s, a) => guideControl.DrawGuide(); 
        }

        private void GuideForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing)
            {
                return;
            }

            e.Cancel = true;

            TvCore.Settings.GuideOpen = false;
            TvCore.Settings.Save();

            Hide();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            labelDateTime.Text = string.Format(Resources.GuideForm_HeaderTimeFormat, DateTime.Now);
        }
    }
}
