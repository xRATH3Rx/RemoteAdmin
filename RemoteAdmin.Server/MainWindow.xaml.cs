using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Newtonsoft.Json;
using RemoteAdmin.Shared;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace RemoteAdmin.Server
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<ConnectedClient> clients;
        private TcpListener listener;
        private int listenPort = 5900;
        private bool isRunning = false;

        private X509Certificate2 serverCert;
        private X509Certificate2 caCert;

        public MainWindow()
        {
            InitializeComponent();
            clients = new ObservableCollection<ConnectedClient>();
            ClientsGrid.ItemsSource = clients;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadAvailableIPs();
                // Load server certificate (with private key)
                serverCert = new X509Certificate2(
                    Path.Combine("Certificates", "server.pfx"),
                    "RemoteAdminServer",
                    X509KeyStorageFlags.Exportable |
                    X509KeyStorageFlags.MachineKeySet |
                    X509KeyStorageFlags.PersistKeySet);

                if (!serverCert.HasPrivateKey)
                    throw new Exception("Server certificate missing private key.");

                // Load CA certificate (public only)
                caCert = new X509Certificate2(
                    Path.Combine("Certificates", "ca.crt"));

                StartServer();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load certificates:\n{ex.Message}", "Certificate Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

                        // NOTE: pass the TLS stream here
                        await HandleClient(tcpClient, sslStream);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"TLS handshake failed: {ex.Message}");
                        tcpClient.Close();
                    }
                });
            }
        }

        private async Task HandleClient(TcpClient tcpClient, Stream stream)
        {
            ConnectedClient? client = null;

            try
            {
                // DO NOT call tcpClient.GetStream(); use the passed-in 'stream'
                var message = await NetworkHelper.ReceiveMessageAsync(stream);

                if (message is ClientInfoMessage clientInfo)
                {
                    client = new ConnectedClient
                    {
                        Id = Guid.NewGuid().ToString(),
                        ComputerName = clientInfo.ComputerName,
                        Username = clientInfo.Username,
                        OSVersion = clientInfo.OSVersion,
                        IPAddress = clientInfo.IPAddress,
                        PublicIP = clientInfo.PublicIP,
                        Status = "Online",
                        LastSeen = DateTime.Now,
                        Connection = tcpClient,
                        Stream = stream // store the TLS stream
                    };

                    Dispatcher.Invoke(() =>
                    {
                        clients.Add(client);
                        UpdateClientCount();
                    });

                    // Listen loop (still using the passed-in TLS stream)
                    while (tcpClient.Connected)
                    {
                        var msg = await NetworkHelper.ReceiveMessageAsync(stream);
                        if (msg == null)
                            break;

                        if (msg is HeartbeatMessage)
                        {
                            Dispatcher.Invoke(() => client.LastSeen = DateTime.Now);
                        }
                        else if (msg is ShellOutputMessage shellOutput)
                        {
                            Dispatcher.Invoke(() => client.ShellWindow?.AppendOutput(shellOutput.Output));
                        }
                        else if (msg is ProcessListResponseMessage processListResponse)
                        {
                            Dispatcher.Invoke(() => client.TaskManagerWindow?.UpdateProcessList(processListResponse.Processes));
                        }
                        else if (msg is ServiceListResponseMessage serviceListResponse)
                        {
                            Dispatcher.Invoke(() => client.TaskManagerWindow?.UpdateServiceList(serviceListResponse.Services));
                        }
                        else if (msg is OperationResultMessage operationResult)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (!operationResult.Success)
                                {
                                    MessageBox.Show(operationResult.Message, "Operation Failed",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            });
                        }
                        else if (msg is DirectoryListResponseMessage dirResponse)
                        {
                            Dispatcher.Invoke(() => client.FileManagerWindow?.UpdateDirectoryListing(dirResponse));
                        }
                        else if (msg is FileChunkMessage fileChunk)
                        {
                            Dispatcher.Invoke(() => client.FileManagerWindow?.HandleFileChunk(fileChunk));
                        }
                        else if (msg is ScreenFrameMessage screenFrame)
                        {
                            Dispatcher.Invoke(() => client.RemoteDesktopWindow?.UpdateScreen(
                                screenFrame.ImageData, screenFrame.Width, screenFrame.Height));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client handler error: {ex.Message}");
            }
            finally
            {
                if (client != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        client.Status = "Offline";
                        clients.Remove(client);
                        UpdateClientCount();
                    });
                }
                try { stream?.Dispose(); } catch { }
                try { tcpClient?.Close(); } catch { }
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
                    MessageBox.Show("Please enter a valid port number", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Stop current server
                isRunning = false;
                listener?.Stop();

                // Disconnect all clients
                foreach (var client in clients.ToList())
                {
                    client.Connection?.Close();
                }
                clients.Clear();

                // Start with new port
                listenPort = newPort;
                StartServer();

                MessageBox.Show($"Server restarted on port {listenPort}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error restarting server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                        certPath, "RemoteAdminServer",
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

        private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}