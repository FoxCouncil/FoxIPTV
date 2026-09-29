// Copyright (c) Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Views
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Avalonia.Controls.Templates;
    using Avalonia.Interactivity;
    using Avalonia.Layout;
    using Avalonia.Media;
    using Avalonia.Media.Imaging;
    using Classes;
    using Newtonsoft.Json.Linq;
    using Services;

    public class CountryChoice
    {
        public string Value { get; set; }

        public string Name { get; set; }

        public Bitmap Flag { get; set; }

        public override string ToString() => Name;
    }

    public partial class ProviderWindow : Window
    {
        private readonly Dictionary<string, Control> _inputs = new Dictionary<string, Control>();

        private readonly TaskCompletionSource<bool> _result = new TaskCompletionSource<bool>();

        public IService SelectedService => ServicesComboBox.SelectedItem as IService;

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

        public ProviderWindow()
        {
            InitializeComponent();

            ServicesComboBox.ItemsSource = TvCore.Services;
            ServicesComboBox.SelectionChanged += (sender, args) => BuildFields();

            ServicesComboBox.SelectedIndex = 0;

            if (TvCore.Services.Count == 0)
            {
                LoginButton.IsEnabled = false;
            }

            LoginButton.Click += LoginButton_Click;

            Closed += (sender, args) => _result.TrySetResult(false);
        }

        public static Task<bool> ChooseProvider()
        {
            var picker = new ProviderWindow();

            picker.Show();

            return picker._result.Task;
        }

        private readonly List<Control> _fieldControls = new List<Control>();

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
                foreach (var field in service.Fields)
                {
                    FormGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                    var label = new TextBlock
                    {
                        Text = $"{field.Label ?? field.Key}:",
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Right
                    };

                    var input = CreateInput(field);

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

        private static Control CreateInput(ProviderField field)
        {
            if (field.Kind == ProviderFieldKind.Choice)
            {
                var combo = new ComboBox { ItemsSource = field.Choices };

                if (Countries.IsCountryList(field.Choices))
                {
                    combo.ItemsSource = field.Choices.Select(x => new CountryChoice { Value = x, Name = Countries.Name(x), Flag = Countries.Flag(x) }).ToList();
                    combo.ItemTemplate = new FuncDataTemplate<CountryChoice>((choice, scope) => new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            new Image { Source = choice?.Flag, Width = 28, Height = 16, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center },
                            new TextBlock { Text = choice?.Name, VerticalAlignment = VerticalAlignment.Center }
                        }
                    });
                }

                var index = field.Default == null ? -1 : field.Choices.IndexOf(field.Default);

                combo.SelectedIndex = index >= 0 ? index : (field.Choices.Count > 0 ? 0 : -1);

                return combo;
            }

            var textBox = new TextBox { Text = field.Default ?? string.Empty };

            if (field.Kind == ProviderFieldKind.Password)
            {
                textBox.PasswordChar = '•';
            }

            return textBox;
        }

        private static string InputText(Control control)
        {
            switch (control)
            {
                case ComboBox combo:
                {
                    var selected = combo.SelectedItem is CountryChoice choice ? choice.Value : combo.SelectedItem?.ToString();

                    return selected?.Trim() ?? string.Empty;
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

            TvCore.LogDebug($"[.NET] ProviderWindow: Checking authentication for {service.Title}");

            IsEnabled = false;

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

            if (!authenticated)
            {
                TvCore.LogDebug("[.NET] ProviderWindow: Authentication details incorrect, service rejected them, retrying.");

                await Dialogs.Message(this, failure ?? $"{service.Title} rejected the details you entered, please check them and try again.", "Fox IPTV - Sign In Failed");

                return;
            }

            _result.TrySetResult(true);

            Close();
        }

    }
}
