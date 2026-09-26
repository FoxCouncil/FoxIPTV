// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Avalonia.Interactivity;
    using Avalonia.Layout;
    using Classes;
    using Newtonsoft.Json.Linq;
    using Services;
    using Services.Scripting;

    /// <summary>The provider picker shown at start up; lists every provider and builds its input fields on the fly</summary>
    public partial class ProviderWindow : Window
    {
        /// <summary>The input controls for the current provider, keyed by field key</summary>
        private readonly Dictionary<string, Control> _inputs = new Dictionary<string, Control>();

        /// <summary>Completed with true once a provider is chosen and signed in, false if the window is closed first</summary>
        private readonly TaskCompletionSource<bool> _result = new TaskCompletionSource<bool>();

        /// <summary>The provider the user picked</summary>
        public IService SelectedService => ServicesComboBox.SelectedItem as IService;

        /// <summary>Should the field values be remembered for next time</summary>
        public bool RememberMe => RememberMeCheckBox.IsChecked == true;

        /// <summary>The values the user entered, keyed by field key; defaults are filled in for blanks</summary>
        public JObject FieldValues
        {
            get
            {
                var values = new JObject();

                var service = SelectedService;

                if (service == null)
                {
                    return values;
                }

                foreach (var providerField in service.Fields)
                {
                    var text = _inputs.TryGetValue(providerField.Key, out var control) ? InputText(control) : null;

                    if (string.IsNullOrEmpty(text))
                    {
                        text = providerField.Default ?? string.Empty;
                    }

                    values[providerField.Key] = text;
                }

                return values;
            }
        }

        /// <inheritdoc/>
        public ProviderWindow()
        {
            InitializeComponent();

            ServicesComboBox.ItemsSource = TvCore.Services;
            ServicesComboBox.SelectionChanged += (sender, args) => BuildFields();

            var remembered = TvCore.Services.FindIndex(x => string.Equals(x.Id, TvCore.Settings.ProviderId, StringComparison.OrdinalIgnoreCase));

            ServicesComboBox.SelectedIndex = remembered >= 0 ? remembered : 0;

            if (TvCore.Services.Count == 0)
            {
                LoginButton.IsEnabled = false;
                RememberMeCheckBox.IsEnabled = false;
            }

            var errors = ScriptLoader.Errors;

            if (errors.Count > 0)
            {
                PluginErrorsLabel.IsVisible = true;
                PluginErrorsLabel.Text = errors.Count == 1 ? "1 plugin failed to load, hover for details" : $"{errors.Count} plugins failed to load, hover for details";

                ToolTip.SetTip(PluginErrorsLabel, string.Join(Environment.NewLine, errors.Select(x => $"{x.Key}: {x.Value}")));
            }

            LoginButton.Click += LoginButton_Click;
            PluginsLinkButton.Click += PluginsLinkButton_Click;

            Closed += (sender, args) => _result.TrySetResult(false);
        }

        /// <summary>Show the provider picker until the user signs in to a provider or gives up</summary>
        /// <returns>True when a provider is selected and authenticated</returns>
        public static Task<bool> ChooseProvider()
        {
            var picker = new ProviderWindow();

            picker.Show();

            return picker._result.Task;
        }

        /// <summary>The label and input controls added for the current provider, removed again when it changes</summary>
        private readonly List<Control> _fieldControls = new List<Control>();

        /// <summary>Rebuild the fields when the provider changes; they go in the same table as the provider box, one row each, with the note and the checkbox after them</summary>
        private void BuildFields()
        {
            var service = SelectedService;

            foreach (var control in _fieldControls)
            {
                FormGrid.Children.Remove(control);
            }

            _fieldControls.Clear();
            _inputs.Clear();

            while (FormGrid.RowDefinitions.Count > 1)
            {
                FormGrid.RowDefinitions.RemoveAt(FormGrid.RowDefinitions.Count - 1);
            }

            var row = 1;

            if (service != null)
            {
                var remembered = ProviderStore.Load(service.Id);

                RememberMeCheckBox.IsChecked = remembered != null;

                foreach (var field in service.Fields)
                {
                    FormGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                    var label = new TextBlock
                    {
                        Text = $"{field.Label ?? field.Key}:",
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Right
                    };

                    var input = CreateInput(field, remembered?[field.Key]?.ToString());

                    input.Name = field.Key;
                    input.Tag = field;
                    input.HorizontalAlignment = HorizontalAlignment.Stretch;

                    Grid.SetRow(label, row);
                    Grid.SetColumn(label, 0);
                    Grid.SetRow(input, row);
                    Grid.SetColumn(input, 1);

                    FormGrid.Children.Add(label);
                    FormGrid.Children.Add(input);

                    _fieldControls.Add(label);
                    _fieldControls.Add(input);

                    _inputs[field.Key] = input;

                    row++;
                }
            }

            NoFieldsLabel.IsVisible = service != null && row == 1;

            if (NoFieldsLabel.IsVisible)
            {
                FormGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(NoFieldsLabel, row);
                row++;
            }

            FormGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(RememberMeCheckBox, row);

            var input0 = _inputs.Values.FirstOrDefault();

            if (input0 != null && string.IsNullOrEmpty(InputText(input0)))
            {
                input0.Focus();
            }
            else
            {
                LoginButton.Focus();
            }
        }

        /// <summary>Build the input control for a field</summary>
        /// <param name="field">The field</param>
        /// <param name="remembered">A remembered value, or null</param>
        /// <returns>The control, pre-filled</returns>
        private static Control CreateInput(ProviderField field, string remembered)
        {
            if (field.Kind == ProviderFieldKind.Choice)
            {
                var combo = new ComboBox { ItemsSource = field.Choices };

                var wanted = remembered ?? field.Default;

                var index = wanted == null ? -1 : field.Choices.IndexOf(wanted);

                combo.SelectedIndex = index >= 0 ? index : (field.Choices.Count > 0 ? 0 : -1);

                return combo;
            }

            var textBox = new TextBox { Text = remembered ?? field.Default ?? string.Empty };

            if (field.Kind == ProviderFieldKind.Password)
            {
                textBox.PasswordChar = '•';
            }

            return textBox;
        }

        /// <summary>The text an input holds</summary>
        private static string InputText(Control control)
        {
            switch (control)
            {
                case ComboBox combo:
                {
                    return combo.SelectedItem?.ToString()?.Trim() ?? string.Empty;
                }

                case TextBox textBox:
                {
                    return textBox.Text?.Trim() ?? string.Empty;
                }

                default:
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>Check the fields, then sign in to the provider</summary>
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var service = SelectedService;

            if (service == null)
            {
                return;
            }

            foreach (var field in service.Fields)
            {
                var control = _inputs[field.Key];
                var text = InputText(control);

                if (field.Required && string.IsNullOrEmpty(text) && string.IsNullOrEmpty(field.Default))
                {
                    await Dialogs.Message(this, $"{field.Label ?? field.Key} is required.", "Fox IPTV");

                    control.Focus();

                    return;
                }

                if (field.Kind == ProviderFieldKind.Url && !string.IsNullOrEmpty(text) && !Uri.IsWellFormedUriString(text, UriKind.Absolute))
                {
                    await Dialogs.Message(this, $"{field.Label ?? field.Key} must be a full URL, including http:// or https://.", "Fox IPTV");

                    control.Focus();

                    return;
                }
            }

            TvCore.SelectService(service.Id);

            service.Data = FieldValues;
            service.SaveAuthentication = RememberMe;

            TvCore.LogDebug($"[.NET] ProviderWindow: Checking authentication for {service.Title}");

            IsEnabled = false;
            SigningInLabel.Text = $"Signing in to {service.Title}...";
            SigningInLabel.IsVisible = true;

            bool authenticated;

            string failure = null;

            try
            {
                authenticated = await Task.Run(service.IsAuthenticated);
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[.NET] ProviderWindow: {service.Title} authentication threw: {ex.Message}");

                authenticated = false;
                failure = ex.Message;
            }

            IsEnabled = true;
            SigningInLabel.IsVisible = false;

            if (!authenticated)
            {
                TvCore.LogDebug("[.NET] ProviderWindow: Authentication details incorrect, service rejected them, retrying.");

                await Dialogs.Message(this, failure ?? $"{service.Title} rejected the details you entered, please check them and try again.", "Fox IPTV - Sign In Failed");

                return;
            }

            if (RememberMe)
            {
                ProviderStore.Save(service.Id, service.Data);
            }
            else
            {
                ProviderStore.Delete(service.Id);
            }

            TvCore.Settings.ProviderId = service.Id;
            TvCore.Settings.Save();

            _result.TrySetResult(true);

            Close();
        }

        /// <summary>Open the user's plugin folder in the system file browser</summary>
        private void PluginsLinkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(ScriptLoader.UserPluginPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                TvCore.LogError($"[ProviderWindow] Unable to open plugin folder: {ex.Message}");
            }
        }
    }
}
