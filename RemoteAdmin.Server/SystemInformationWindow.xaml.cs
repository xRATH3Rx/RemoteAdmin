using RemoteAdmin.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace RemoteAdmin.Server
{
    public partial class SystemInformationWindow : Window
    {
        private readonly ConnectedClient _client;
        private List<SystemInfoItem> _systemInfo = new List<SystemInfoItem>();

        public SystemInformationWindow(ConnectedClient client)
        {
            InitializeComponent();
            _client = client;

            this.Title = $"System Information - {client.ComputerName} ({client.Username})";
            this.Loaded += SystemInformationWindow_Loaded;
            this.Closing += SystemInformationWindow_Closing;
        }

        private async void SystemInformationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _client.SystemInformationWindow = this;
            await RefreshSystemInformation();
        }

        private void SystemInformationWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_client.SystemInformationWindow == this)
                _client.SystemInformationWindow = null;
        }

        public void UpdateSystemInformationList(List<SystemInfoItem> items)
        {
            _systemInfo = items ?? new List<SystemInfoItem>();

            lvSystemInfo.ItemsSource = _systemInfo;

            txtItemCount.Text = _systemInfo.Count.ToString();
            txtStatus.Text = $"Loaded {_systemInfo.Count} system properties";
        }

        private async Task RefreshSystemInformation()
        {
            try
            {
                txtStatus.Text = "Loading system information...";
                btnRefresh.IsEnabled = false;

                // Clear existing items
                lvSystemInfo.ItemsSource = null;

                var message = new GetSystemInfoMessage();
                await NetworkHelper.SendMessageAsync(_client.Stream, message);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh system information: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Error loading information";
            }
            finally
            {
                btnRefresh.IsEnabled = true;
            }
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshSystemInformation();
        }

        private void btnCopyAll_Click(object sender, RoutedEventArgs e)
        {
            if (_systemInfo.Count == 0)
            {
                MessageBox.Show("No system information to copy.",
                    "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"System Information - {_client.ComputerName}");
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine(new string('=', 80));
                sb.AppendLine();

                foreach (var item in _systemInfo)
                {
                    sb.AppendLine($"{item.Key}: {item.Value}");
                }

                Clipboard.SetText(sb.ToString());
                txtStatus.Text = "System information copied to clipboard";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnCopySelected_Click(object sender, RoutedEventArgs e)
        {
            if (lvSystemInfo.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select one or more items to copy.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"System Information - {_client.ComputerName} (Selected)");
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine(new string('=', 80));
                sb.AppendLine();

                foreach (SystemInfoItem item in lvSystemInfo.SelectedItems)
                {
                    sb.AppendLine($"{item.Key}: {item.Value}");
                }

                Clipboard.SetText(sb.ToString());
                txtStatus.Text = $"Copied {lvSystemInfo.SelectedItems.Count} item(s) to clipboard";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}