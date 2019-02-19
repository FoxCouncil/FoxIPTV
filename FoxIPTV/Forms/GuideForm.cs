// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Forms
{
    using System;
    using System.Windows.Forms;
    using Classes;
    using Controls;
    using Properties;

    /// <inheritdoc/>
    public sealed partial class GuideForm : Form
    {
        /// <inheritdoc/>
        public GuideForm()
        {
            DoubleBuffered = true;

            InitializeComponent();

            var guideControl = new GuideLayoutPanel();

            tableLayoutPanel.Controls.Add(guideControl, 0, 1);
            tableLayoutPanel.SetColumnSpan(guideControl, 3);

            buttonResetView.Click += (s, a) => guideControl.ResetView();

            // For some reason, only Forms have this event
            ResizeEnd += (s, a) => guideControl.DrawGuide(); 
        }

        /// <summary>The <see cref="Form"/> close event handler, we don't want to dispose of this window</summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The event arguments</param>
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

        /// <summary>A <see cref="Timer"/> event, used to update the current time on the header of the form</summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The event arguments</param>
        private void Timer_Tick(object sender, EventArgs e)
        {
            labelDateTime.Text = string.Format(Resources.GuideForm_HeaderTimeFormat, DateTime.Now);
        }
    }
}
