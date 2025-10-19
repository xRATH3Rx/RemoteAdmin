using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Server
{
    public partial class ShellWindow : Window
    {
        private ConnectedClient client;
        public event EventHandler<string> CommandSent;

        public ShellWindow(ConnectedClient client)
        {
            InitializeComponent();
            this.client = client;

            txtClientInfo.Text = $"Connected to: {client.ComputerName} ({client.Username})";

            // Wait for window to fully load before appending output
            this.Loaded += (s, e) =>
            {
                txtCommand.Focus();
                AppendOutput($"Remote Shell connected to {client.ComputerName}\n");
                AppendOutput($"Default shell: PowerShell (you can switch to CMD using the dropdown)\n");
                AppendOutput($"Type commands and press Enter to execute\n");
                AppendOutput($"--------------------------------------------------\n\n");
            };
        }

        public void AppendOutput(string text)
        {
            Dispatcher.Invoke(() =>
            {
                txtOutput.AppendText(text);
                OutputScroller.ScrollToEnd();
            });
        }

        public void UpdateStatus(string status, bool isError = false)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = $"Status: {status}";
                txtStatus.Foreground = isError ?
                    System.Windows.Media.Brushes.Red :
                    System.Windows.Media.Brushes.LimeGreen;
            });
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            SendCommand();
        }

        private void txtCommand_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendCommand();
            }
        }

        private void cboShellType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Don't do anything if window isn't fully loaded yet
            if (!this.IsLoaded || txtOutput == null)
                return;

            string? shellType = (cboShellType.SelectedItem as ComboBoxItem)?.Content.ToString();
            AppendOutput($"\n[Shell type changed to: {shellType}]\n\n");
        }

        private async void SendCommand()
        {
            string command = txtCommand.Text.Trim();

            if (string.IsNullOrEmpty(command))
                return;

            // Get selected shell type
            string shellType = (cboShellType.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString() ?? "PowerShell";

            // Display the command in output
            string prefix = shellType == "PowerShell" ? "PS>" : ">";
            AppendOutput($"{prefix} {command}\n");

            // Clear input
            txtCommand.Text = "";

            try
            {
                // Send command to client
                var cmdMessage = new ShellCommandMessage
                {
                    Command = command,
                    ShellType = shellType
                };

                await NetworkHelper.SendMessageAsync(client.Stream, cmdMessage);
                UpdateStatus("Command sent", false);
            }
            catch (Exception ex)
            {
                AppendOutput($"Error sending command: {ex.Message}\n");
                UpdateStatus("Error", true);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // Notify that this window is closing
            client.ShellWindow = null;
        }
    }
}