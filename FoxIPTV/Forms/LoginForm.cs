// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Forms
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Forms;
    using Classes;
    using Properties;

    /// <inheritdoc/>
    public partial class LoginForm : Form
    {
        /// <summary>Store original form size, when switching services that have more or less fields than the form started with</summary>
        private readonly Size _originalFormSize;

        /// <summary>A list of labels that the service requires</summary>
        private readonly List<Label> _labels = new List<Label>();

        /// <summary>A list of inputs that the service requires</summary>
        private readonly List<Control> _inputs = new List<Control>();

        /// <inheritdoc/>
        public LoginForm()
        {
            InitializeComponent();

            usernameTextBox.Name = "Username";
            passwordTextBox.Name = "Password";

            _originalFormSize = Size;
        }

        /// <summary>An event handler when the form is loaded</summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The event arguments</param>
        private void LoginForm_Load(object sender, EventArgs e)
        {
            if (TvCore.Services.Count == 0)
            {
                // This shouldn't happen, but eh
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
                    // No point allowing a single selection
                    servicesComboBox.Enabled = false;
                }
            }
        }

        /// <summary>An event handler to handle when the service is selected</summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The event arguments</param>
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
                    Text = string.Format(Resources.LoginForm_DynamicFieldLabel, field.Key),
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

        /// <summary>Returns a control matching a type provided</summary>
        /// <param name="fieldValue">The type to match a control to</param>
        /// <returns>A matched control for the type, or null</returns>
        private static Control DetermineControl(Type fieldValue)
        {
            if (fieldValue == typeof(string))
            {
                return new TextBox();
            }

            return null;
        }

        /// <summary>A <see cref="Button"/> click handler, used to trigger the login process</summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The event arguments</param>
        private void LoginButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(usernameTextBox.Text) || string.IsNullOrWhiteSpace(passwordTextBox.Text))
            {
                MessageBox.Show(Resources.LoginForm_LoginCredentialsNeededWarning, Resources.LoginForm_LoginCredentialsNeededWarningTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;

            Close();
        }
    }
}
