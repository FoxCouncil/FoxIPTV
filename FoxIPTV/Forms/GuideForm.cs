// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Forms
{
    using System;
    using System.Windows.Forms;
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

            buttonResetView.Click += async (s, a) => await guideControl.ResetView();

            ResizeEnd += async (s, a) => await guideControl.Draw(); 
        }

        private void GuideForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.UserClosing)
            {
                return;
            }

            e.Cancel = true;

            Settings.Default.GuideOpen = false;
            Settings.Default.Save();

            Hide();
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            labelDateTime.Text = $"{DateTime.Now:F}";
        }
    }
}
