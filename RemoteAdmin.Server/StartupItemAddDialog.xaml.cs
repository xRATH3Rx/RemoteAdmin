using System;
using System.Windows;
using RemoteAdmin.Shared;
using RemoteAdmin.Shared.Enums;

namespace RemoteAdmin.Server
{
    public partial class StartupItemAddDialog : Window
    {
        public StartupItem StartupItem { get; private set; }

        public StartupItemAddDialog()
        {
            InitializeComponent();
            PopulateStartupTypes();
        }

        /// <summary>
        /// Populates the startup type ComboBox
        /// </summary>
        private void PopulateStartupTypes()
        {
            cboType.Items.Add("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
            cboType.Items.Add("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\RunOnce");
            cboType.Items.Add("HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
            cboType.Items.Add("HKEY_CURRENT_USER\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\RunOnce");
            cboType.Items.Add("%APPDATA%\\Microsoft\\Windows\\Start Menu\\Programs\\Startup");
            cboType.Items.Add("HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Run");
            cboType.Items.Add("HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\RunOnce");
            cboType.SelectedIndex = 0;
        }

        /// <summary>
        /// Add button click handler
        /// </summary>
        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Please enter a name for the startup item.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtPath.Text))
            {
                MessageBox.Show("Please enter a path for the startup item.",
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPath.Focus();
                return;
            }

            // Create the startup item
            StartupItem = new StartupItem
            {
                Name = txtName.Text.Trim(),
                Path = txtPath.Text.Trim(),
                Type = (StartupType)cboType.SelectedIndex
            };

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Cancel button click handler
        /// </summary>
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}