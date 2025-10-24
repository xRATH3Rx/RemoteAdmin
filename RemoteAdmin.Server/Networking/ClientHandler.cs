using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Threading;
using RemoteAdmin.Shared;


namespace RemoteAdmin.Server.Networking
{
    public class ClientHandler
    {
        private readonly ObservableCollection<ConnectedClient> _clients;
        private readonly Dispatcher _dispatcher;
        private readonly Action _updateClientCount;

        public ClientHandler(
            ObservableCollection<ConnectedClient> clients,
            Dispatcher dispatcher,
            Action updateClientCount)
        {
            _clients = clients;
            _dispatcher = dispatcher;
            _updateClientCount = updateClientCount;
        }

        public async Task HandleAsync(TcpClient tcpClient, Stream stream)
        {
            ConnectedClient? client = null;

            try
            {
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
                        AccountType = clientInfo.AccountType,
                        Stream = stream
                    };

                    _dispatcher.Invoke(() =>
                    {
                        _clients.Add(client);
                        _updateClientCount();
                    });

                    while (tcpClient.Connected)
                    {
                        var msg = await NetworkHelper.ReceiveMessageAsync(stream);
                        if (msg == null) break;

                        if (msg is HeartbeatMessage)
                        {
                            _dispatcher.Invoke(() => client.LastSeen = DateTime.Now);
                        }
                        else if (msg is ShellOutputMessage shellOutput)
                        {
                            _dispatcher.Invoke(() => client.ShellWindow?.AppendOutput(shellOutput.Output));
                        }
                        else if (msg is ProcessListResponseMessage processListResponse)
                        {
                            _dispatcher.Invoke(() => client.TaskManagerWindow?.UpdateProcessList(processListResponse.Processes));
                        }
                        else if (msg is ServiceListResponseMessage serviceListResponse)
                        {
                            _dispatcher.Invoke(() => client.TaskManagerWindow?.UpdateServiceList(serviceListResponse.Services));
                        }
                        else if (msg is OperationResultMessage operationResult)
                        {
                            _dispatcher.Invoke(() =>
                            {
                                if (!operationResult.Success)
                                {
                                    System.Windows.MessageBox.Show(
                                        operationResult.Message,
                                        "Operation Failed",
                                        System.Windows.MessageBoxButton.OK,
                                        System.Windows.MessageBoxImage.Error);
                                }
                            });
                        }
                        else if (msg is DirectoryListResponseMessage dirResponse)
                        {
                            _dispatcher.Invoke(() => client.FileManagerWindow?.UpdateDirectoryListing(dirResponse));
                        }
                        else if (msg is FileChunkMessage fileChunk)
                        {
                            _dispatcher.Invoke(() => client.FileManagerWindow?.HandleFileChunk(fileChunk));
                        }
                        else if (msg is ScreenFrameMessage screenFrame)
                        {
                            _dispatcher.Invoke(() => client.RemoteDesktopWindow?.UpdateScreen(
                                screenFrame.ImageData, screenFrame.Width, screenFrame.Height));
                        }
                        else if (msg is WebsiteVisitResultMessage websiteResult)
                        {
                            _dispatcher.Invoke(() =>
                            {
                                string icon = websiteResult.Success ? "✓" : "✗";
                                string title = websiteResult.Success ? "Success" : "Failed";
                                System.Windows.MessageBox.Show(
                                    $"{icon} {websiteResult.Message}\n\nURL: {websiteResult.Url}",
                                    $"Website Visit {title}",
                                    System.Windows.MessageBoxButton.OK,
                                    websiteResult.Success
                                        ? System.Windows.MessageBoxImage.Information
                                        : System.Windows.MessageBoxImage.Error);
                            });
                        }
                        else if (msg is MonitorInfoMessage monitorInfo)
                        {
                            Console.WriteLine($"Received monitor info: {monitorInfo.Monitors.Count} monitors");
                            _dispatcher.Invoke(() =>
                            {
                                client.RemoteDesktopWindow?.UpdateMonitorList(monitorInfo.Monitors);
                            });
                        }
                        else if (msg is RegistryDataMessage registryData)
                        {
                            Console.WriteLine($"Received registry data: {registryData.SubKeys.Count} keys, {registryData.Values.Count} values");
                            _dispatcher.Invoke(() => client.RegistryEditorWindow?.UpdateRegistryData(registryData));
                        }
                        else if (msg is RegistryOperationResultMessage registryOperationResult)
                        {
                            Console.WriteLine($"Received registry operation result: {registryOperationResult.Operation} - {registryOperationResult.Success}");
                            _dispatcher.Invoke(() => client.RegistryEditorWindow?.HandleOperationResult(registryOperationResult));
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
                    _dispatcher.Invoke(() =>
                    {
                        client.Status = "Offline";
                        _clients.Remove(client);
                        _updateClientCount();
                    });
                }
                try { stream?.Dispose(); } catch { }
                try { tcpClient?.Close(); } catch { }
            }
        }
    }
}
