// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Forms
{
    using System;
    using System.Reflection;
    using System.Windows.Forms;

    /// <inheritdoc/>
    public partial class AboutForm : Form
    {
        /// <inheritdoc/>
        public AboutForm()
        {
            InitializeComponent();

            labelTitle.Text += $" V{((AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(), typeof(AssemblyFileVersionAttribute), false)).Version}";
        }

        /// <summary>The click handler for the amazing person that shared their icons with the world!</summary>
        /// <param name="sender">The object that generated this event</param>
        /// <param name="e">The event arguments</param>
        private void LinkLabelIconAttribution_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("http://p.yusukekamiyamane.com/");
        }

        /// <summary>The click handler for the close button on the form</summary>
        /// <param name="sender">The object that generated this event</param>
        /// <param name="e">The event arguments</param>
        private void ButtonClose_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
