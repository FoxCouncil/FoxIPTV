// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV
{
    using Classes;
    using Forms;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    public static class Program
    {
        /// <summary>The main entry point for the application.</summary>
        [STAThread]
        private static async Task Main()
        {
            TvCore.LogInfo("[.NET] Main(): Starting Windows Application...");

            Application.ApplicationExit += (sender, args) => TvCore.LogInfo("[.NET] Main(): Quitting Windows Application...");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            TvCore.LogDebug("[.NET] Main(): Begin Auth Check");

            if (!await TvCore.CurrentService.IsAuthenticated())
            {
                TvCore.LogDebug("[.NET] Main(): Not Already Authenticated");

                retry:

                var loginForm = new LoginForm();

                var dResult = loginForm.ShowDialog();

                if (dResult != DialogResult.OK)
                {
                    return;
                }

                TvCore.ServiceSelected = loginForm.servicesComboBox.SelectedIndex;

                TvCore.CurrentService.Data = new JObject();

                foreach (var field in TvCore.CurrentService.Fields)
                {
                    var fieldControl = loginForm.Controls.Find(field.Key, false).FirstOrDefault();

                    if (fieldControl is null)
                    {
                        continue;
                    }

                    TvCore.CurrentService.Data.Add(field.Key, fieldControl.Text);
                }

                TvCore.CurrentService.SaveAuthentication = loginForm.rememberMeCheckBox.Checked;

                if (!await TvCore.CurrentService.IsAuthenticated())
                {
                    TvCore.LogDebug("[.NET] Main(): Authentication details incorrect, service rejected them, retrying.");

                    goto retry;
                }
            }

            Application.Run(new TvForm());
        }
    }
}
