// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.UWP
{
    using FoxIPTV.Library;
    using FoxIPTV.Library.Services;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using Windows.UI;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;

    public sealed partial class LoginContentDialog : ContentDialog
    {
        private IService _selectedService;

        private JObject _loginData;

        public LoginContentDialog()
        {
            InitializeComponent();

            _loginData = new JObject();

            textBoxUsername.Tag = Core.UsernameKey;
            passwordBoxPassword.Tag = Core.PasswordKey;

            Loaded += (s, e) =>
            {
                serviceProviderComboBox.DisplayMemberPath = "Key";
                serviceProviderComboBox.SelectedValuePath = "Value";

                foreach (var service in Core.Services)
                {
                    serviceProviderComboBox.Items.Add(new KeyValuePair<string, Guid>(service.Title, service.Id));
                }

                _panel.MinWidth = 420;

                errorTextBlock.MaxWidth = _panel.ActualWidth;
            };
        }

        private void ServiceProviderComboBox_SelectionChanged(object _, SelectionChangedEventArgs e)
        {
            var selectedItem = (KeyValuePair<string, Guid>)e.AddedItems[0];

            Log.Debug($"[LoginDialog] Changed provider selection to: {selectedItem}");

            LoadServiceFields(selectedItem.Value);
        }

        private async void ButtonLogin_Click(object sender, RoutedEventArgs e)
        {
            var validated = true;

            foreach (var field in _selectedService.Fields)
            {
                var control = _panel.Children.FirstOrDefault(x => ((FrameworkElement)x).Tag?.ToString() == field.Key);

                if (control is TextBox)
                {
                    var textBox = control as TextBox;

                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        textBox.BorderBrush = new SolidColorBrush(Colors.Red);

                        if (validated)
                        {
                            textBox.Focus(FocusState.Programmatic);
                            validated = false;
                        }
                    }
                    else
                    {
                        textBox.BorderBrush = serviceProviderComboBox.BorderBrush;

                        _loginData[field.Key] = textBox.Text;
                    }
                }

                if (control is PasswordBox)
                {
                    var passwordBox = control as PasswordBox;

                    if (string.IsNullOrWhiteSpace(passwordBox.Password))
                    {
                        passwordBox.BorderBrush = new SolidColorBrush(Colors.Red);

                        if (validated)
                        {
                            passwordBox.Focus(FocusState.Programmatic);
                            validated = false;
                        }
                    }
                    else
                    {
                        passwordBox.BorderBrush = serviceProviderComboBox.BorderBrush;
                        _loginData[field.Key] = passwordBox.Password;
                    }
                }
            }

            if (validated)
            {
                var result = await _selectedService.IsAuthenticated(_loginData);

                validated = result.IsSuccess;

                if (!validated)
                {
                    errorTextBlock.Visibility = Visibility.Visible;
                    errorTextBlock.Text = result.Message;
                }
                else
                {
                    errorTextBlock.Visibility = Visibility.Collapsed;
                    errorTextBlock.Text = string.Empty;
                }
            }

            if (validated)
            {
                Core.AddAccount(_selectedService.Id, _loginData);

                Hide();
            }
        }

        private void LoadServiceFields(Guid uuid)
        {
            _selectedService = Core.Services.FirstOrDefault(x => x.Id == uuid);

            if (!_selectedService.Fields.Any(x => x.Key == Core.UsernameKey))
            {
                textBoxUsername.Visibility = Visibility.Collapsed;
            }

            if (!_selectedService.Fields.Any(x => x.Key == Core.PasswordKey))
            {
                passwordBoxPassword.Visibility = Visibility.Collapsed;
            }

            foreach (var field in _selectedService.Fields)
            {
                if (field.Key == Core.UsernameKey || field.Key == Core.PasswordKey)
                {
                    continue;
                }

                var newTextBox = new TextBox
                {
                    Header = field.Header,
                    PlaceholderText = field.Placeholder,
                    Tag = field.Key,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 10, 0, 10)
                };

                _panel.Children.Insert(_panel.Children.Count - 1, newTextBox);
            }

            textBoxUsername.IsEnabled = true;
            passwordBoxPassword.IsEnabled = true;
            buttonLogin.IsEnabled = true;
        }
    }
}
