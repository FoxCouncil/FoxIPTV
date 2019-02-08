// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FoxIPTV.Classes;

namespace FoxIPTV.Forms
{
    public partial class LoginForm : Form
    {
        private Size _originalFormSize;

        private List<Label> _labels = new List<Label>();

        private List<Control> _inputs = new List<Control>();

        public LoginForm()
        {
            InitializeComponent();

            usernameTextBox.Name = "Username";
            passwordTextBox.Name = "Password";

            _originalFormSize = Size;
        }

        private void LoginForm_Load(object sender, EventArgs e)
        {
            if (TvCore.Services.Count == 0)
            {
                usernameTextBox.Enabled = false;
                passwordTextBox.Enabled = false;
                loginButton.Enabled = false;
                rememberMeCheckBox.Enabled = false;
            }
            else
            {
                servicesComboBox.SelectedIndexChanged += ServicesComboBoxOnSelectedIndexChanged;

                foreach (var service in TvCore.Services)
                {
                    servicesComboBox.Items.Add(service.Title);
                }

                servicesComboBox.SelectedIndex = 0;

                if (servicesComboBox.Items.Count == 1)
                {
                    servicesComboBox.Enabled = false;
                }
            }
        }

        private void ServicesComboBoxOnSelectedIndexChanged(object sender, EventArgs e)
        {
            Size = _originalFormSize;

            foreach (var label in _labels)
            {
                Controls.Remove(label);
            }

            foreach (var input in _inputs)
            {
                Controls.Remove(input);
            }

            var serviceFields = new Dictionary<string, Type>(TvCore.Services[servicesComboBox.SelectedIndex].Fields);

            // Eventually we'll stop assuming that all services will require a username and password pair
            serviceFields.Remove("Username");
            serviceFields.Remove("Password");

            var labelSize = passwordLabel.Size;
            var labelLocation = passwordLabel.Location;

            var inputSize = passwordTextBox.Size;
            var inputLocation = passwordTextBox.Location;

            var tabIndex = passwordTextBox.TabIndex;

            foreach (var field in serviceFields)
            {
                labelLocation += new Size(0, labelSize.Height + 19);

                var newLabel = new Label
                {
                    Name = $"{field.Key}Label",
                    Text = $"{field.Key}:",
                    TextAlign = ContentAlignment.TopRight,
                    Size = labelSize,
                    Location = labelLocation
                };

                _labels.Add(newLabel);

                Controls.Add(newLabel);

                var newInput = DetermineControl(field.Value);

                if (newInput != null)
                {
                    inputLocation += new Size(0, inputSize.Height + 12);

                    newInput.Name = $"{field.Key}";
                    newInput.Size = inputSize;
                    newInput.Location = inputLocation;
                    newInput.Tag = field.Key;
                    newInput.TabIndex = tabIndex++;

                    _inputs.Add(newInput);
                    Controls.Add(newInput);
                }

                Size += new Size(0, 32);
            }
        }

        private Control DetermineControl(Type fieldValue)
        {
            if (fieldValue == typeof(string))
            {
                return new TextBox();
            }

            return null;
        }

        private void loginButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(usernameTextBox.Text) || string.IsNullOrWhiteSpace(passwordTextBox.Text))
            {
                MessageBox.Show("Please enter a username and/or password.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;

            Close();
        }
    }
}
