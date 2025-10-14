using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Client
{
    class Program
    {
        private static Process shellProcess;
        private static Dictionary<string, string> activeUploads = new Dictionary<string, string>();
        private static bool isStreamingDesktop = false;
        private static Thread desktopStreamThread;

        #region Win32 APIs

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const int SRCCOPY = 0x00CC0020;

        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        private const uint KEYEVENTF_KEYUP = 0x0002;

        #endregion

        [STAThread]
        static void Main(string[] args)
        {
            // Hook assembly resolver to load embedded DLLs
            AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;

            // Call the actual async main
            MainAsync(args).GetAwaiter().GetResult();
        }

        private static async Task MainAsync(string[] args)
        {
            if (args.Length > 0)
            {
                if (args[0] == "--install")
                {
                    InstallStartup();
                    Console.WriteLine("Installed to startup.");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }

                if (args[0] == "--uninstall")
                {
                    UninstallStartup();
                    Console.WriteLine("Removed from startup.");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }
            }

            // Initialize configuration
            ClientConfig.Initialize();

            Console.WriteLine("RemoteAdmin Client Starting...");
            Console.WriteLine($"Connecting to {ClientConfig.ServerIP}:{ClientConfig.ServerPort}");

            await RunClient();
        }

        private static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var assemblyName = new AssemblyName(args.Name);

            var path = assemblyName.Name + ".dll";
            if (!assemblyName.CultureInfo.Equals(System.Globalization.CultureInfo.InvariantCulture))
            {
                path = $"{assemblyName.CultureInfo}\\{path}";
            }

            using var stream = executingAssembly.GetManifestResourceStream(path);
            if (stream == null)
                return null;

            var assemblyRawBytes = new byte[stream.Length];
            stream.Read(assemblyRawBytes, 0, assemblyRawBytes.Length);
            return Assembly.Load(assemblyRawBytes);
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

        static readonly HttpClient PublicIpHttp = new HttpClient(
            new HttpClientHandler { Proxy = null, UseProxy = false })
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        static async Task RunClient()
        {
            Console.WriteLine($"Server: {ClientConfig.ServerIP}:{ClientConfig.ServerPort}");
            Console.WriteLine($"Reconnect Delay: {ClientConfig.ReconnectInterval}s");

            while (true)
            {
                try
                {
                    using (var tcpClient = new TcpClient())
                    {
                        await tcpClient.ConnectAsync(ClientConfig.ServerIP, ClientConfig.ServerPort);
                        Console.WriteLine("Connected to server!");

                        using (var stream = tcpClient.GetStream())
                        {
                            var ep = (System.Net.IPEndPoint)tcpClient.Client.LocalEndPoint;
                            var ip = ep.Address;
                            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

                            var clientInfo = new ClientInfoMessage
                            {
                                ComputerName = Environment.MachineName,
                                Username = Environment.UserName,
                                OSVersion = Environment.OSVersion.ToString(),
                                IPAddress = ip.ToString(),
                                PublicIP = await GetPublicIPAsync()
                            };

                            await NetworkHelper.SendMessageAsync(stream, clientInfo);
                            Console.WriteLine("Sent client info to server");

                            _ = Task.Run(async () => await ListenForMessages(stream));

                            while (tcpClient.Connected)
                            {
                                await Task.Delay(10000);

                                if (tcpClient.Connected)
                                {
                                    await NetworkHelper.SendMessageAsync(stream, new HeartbeatMessage());
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Connection error: {ex.Message}");
                    Console.WriteLine($"Reconnecting in {ClientConfig.ReconnectInterval} seconds...");

                    if (shellProcess != null && !shellProcess.HasExited)
                    {
                        shellProcess.Kill();
                        shellProcess = null;
                    }

                    await Task.Delay(ClientConfig.ReconnectInterval * 1000);
                }
            }
        }


        static async Task ListenForMessages(NetworkStream stream)
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
                        _ = Task.Run(async () => await HandleShellCommand(stream, shellCmd.Command, shellCmd.ShellType));
                    }
                    else if (message is PowerCommandMessage powerCmd)
                    {
                        Console.WriteLine($"Received power command: {powerCmd.Command}");
                        _ = Task.Run(async () => await HandlePowerCommand(stream, powerCmd.Command));
                    }
                    else if (message is ProcessListRequestMessage)
                    {
                        Console.WriteLine("Received process list request");
                        _ = Task.Run(async () => await HandleProcessListRequest(stream));
                    }
                    else if (message is KillProcessMessage killMsg)
                    {
                        Console.WriteLine($"Received kill process request: {killMsg.ProcessName} (PID: {killMsg.ProcessId})");
                        _ = Task.Run(async () => await HandleKillProcess(stream, killMsg));
                    }
                    else if (message is ServiceListRequestMessage)
                    {
                        Console.WriteLine("Received service list request");
                        _ = Task.Run(async () => await HandleServiceListRequest(stream));
                    }
                    else if (message is ServiceControlMessage serviceMsg)
                    {
                        Console.WriteLine($"Received service control: {serviceMsg.Action} - {serviceMsg.ServiceName}");
                        _ = Task.Run(async () => await HandleServiceControl(stream, serviceMsg));
                    }
                    else if (message is DirectoryListRequestMessage dirRequest)
                    {
                        Console.WriteLine($"Received directory list request: {dirRequest.Path}");
                        _ = Task.Run(async () => await HandleDirectoryListRequest(stream, dirRequest));
                    }
                    else if (message is DownloadFileRequestMessage downloadRequest)
                    {
                        Console.WriteLine($"Received download request: {downloadRequest.FilePath}");
                        _ = Task.Run(async () => await HandleDownloadRequest(stream, downloadRequest));
                    }
                    else if (message is UploadFileStartMessage uploadStart)
                    {
                        Console.WriteLine($"Received upload start: {uploadStart.FileName} to {uploadStart.DestinationPath}");
                        string destinationPath = Path.Combine(uploadStart.DestinationPath, uploadStart.FileName);
                        activeUploads[uploadStart.TransferId] = destinationPath;
                        Console.WriteLine($"Will save to: {destinationPath}");
                    }
                    else if (message is FileChunkMessage fileChunk)
                    {
                        Console.WriteLine($"Received file chunk: {fileChunk.FileName} at offset {fileChunk.Offset}");
                        _ = Task.Run(async () => await HandleFileChunk(stream, fileChunk));
                    }
                    else if (message is DeleteFileMessage deleteMsg)
                    {
                        Console.WriteLine($"Received delete request: {deleteMsg.Path}");
                        _ = Task.Run(async () => await HandleDeleteFile(stream, deleteMsg));
                    }
                    else if (message is RenameFileMessage renameMsg)
                    {
                        Console.WriteLine($"Received rename request: {renameMsg.OldPath} -> {renameMsg.NewName}");
                        _ = Task.Run(async () => await HandleRenameFile(stream, renameMsg));
                    }
                    else if (message is CreateDirectoryMessage createDirMsg)
                    {
                        Console.WriteLine($"Received create directory request: {createDirMsg.Path}");
                        _ = Task.Run(async () => await HandleCreateDirectory(stream, createDirMsg));
                    }
                    else if (message is StartRemoteDesktopMessage startDesktop)
                    {
                        Console.WriteLine("Received start remote desktop request");
                        _ = Task.Run(() => HandleStartRemoteDesktop(startDesktop, stream));
                    }
                    else if (message is StopRemoteDesktopMessage)
                    {
                        Console.WriteLine("Received stop remote desktop request");
                        HandleStopRemoteDesktop();
                    }
                    else if (message is MouseInputMessage mouseInput)
                    {
                        _ = Task.Run(() => HandleMouseInput(mouseInput));
                    }
                    else if (message is KeyboardInputMessage keyboardInput)
                    {
                        _ = Task.Run(() => HandleKeyboardInput(keyboardInput));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listening for messages: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        static async Task HandleShellCommand(NetworkStream stream, string command, string shellType)
        {
            try
            {
                string fileName;
                string arguments;

                if (shellType == "PowerShell")
                {
                    fileName = "powershell.exe";
                    arguments = $"-NoProfile -NonInteractive -Command \"{command}\"";
                }
                else
                {
                    fileName = "cmd.exe";
                    arguments = $"/c {command}";
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    }
                };

                var output = new StringBuilder();
                var error = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        error.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool completed = process.WaitForExit(60000);

                if (!completed)
                {
                    process.Kill();
                    output.AppendLine("\n[Command timed out after 60 seconds]");
                }

                string result = output.ToString();
                if (error.Length > 0)
                {
                    result += "\nERROR:\n" + error.ToString();
                }

                if (string.IsNullOrEmpty(result))
                {
                    result = "[Command completed with no output]\n";
                }

                var outputMessage = new ShellOutputMessage
                {
                    Output = result,
                    IsError = error.Length > 0
                };

                await NetworkHelper.SendMessageAsync(stream, outputMessage);
                Console.WriteLine("Sent command output back to server");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing command: {ex.Message}");

                var errorMessage = new ShellOutputMessage
                {
                    Output = $"Error executing command: {ex.Message}\n",
                    IsError = true
                };

                await NetworkHelper.SendMessageAsync(stream, errorMessage);
            }
        }

        static async Task HandlePowerCommand(NetworkStream stream, string command)
        {
            try
            {
                string arguments = command switch
                {
                    "Restart" => "/r /t 5",
                    "Shutdown" => "/s /t 5",
                    "Sleep" => "/h",
                    "LogOff" => "/l",
                    _ => null
                };

                if (arguments != null)
                {
                    Process.Start("shutdown", arguments);

                    var response = new OperationResultMessage
                    {
                        Success = true,
                        Message = $"{command} command executed successfully"
                    };
                    await NetworkHelper.SendMessageAsync(stream, response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing power command: {ex.Message}");

                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error executing power command: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        static async Task HandleProcessListRequest(NetworkStream stream)
        {
            try
            {
                var processList = new List<ProcessInfo>();
                var processes = Process.GetProcesses();

                foreach (var process in processes)
                {
                    try
                    {
                        processList.Add(new ProcessInfo
                        {
                            Name = process.ProcessName,
                            Id = process.Id,
                            MemoryMB = process.WorkingSet64 / 1024 / 1024,
                            CpuPercent = "N/A"
                        });
                    }
                    catch { }
                }

                var response = new ProcessListResponseMessage
                {
                    Processes = processList
                };

                await NetworkHelper.SendMessageAsync(stream, response);
                Console.WriteLine($"Sent {processList.Count} processes to server");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting process list: {ex.Message}");
            }
        }

        static async Task HandleKillProcess(NetworkStream stream, KillProcessMessage killMsg)
        {
            try
            {
                var process = Process.GetProcessById(killMsg.ProcessId);
                process.Kill();
                process.WaitForExit(5000);

                var response = new OperationResultMessage
                {
                    Success = true,
                    Message = $"Process {killMsg.ProcessName} killed successfully"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error killing process: {ex.Message}");

                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error killing process: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        static async Task HandleServiceListRequest(NetworkStream stream)
        {
            try
            {
                var serviceList = new List<ServiceInfo>();
                var services = ServiceController.GetServices();

                foreach (var service in services)
                {
                    try
                    {
                        serviceList.Add(new ServiceInfo
                        {
                            Name = service.ServiceName,
                            DisplayName = service.DisplayName,
                            Status = service.Status.ToString(),
                            StartupType = GetServiceStartupType(service.ServiceName)
                        });
                    }
                    catch { }
                }

                var response = new ServiceListResponseMessage
                {
                    Services = serviceList
                };

                await NetworkHelper.SendMessageAsync(stream, response);
                Console.WriteLine($"Sent {serviceList.Count} services to server");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting service list: {ex.Message}");
            }
        }

        static string GetServiceStartupType(string serviceName)
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}"))
                {
                    if (key != null)
                    {
                        var startValue = key.GetValue("Start");
                        return startValue switch
                        {
                            2 => "Automatic",
                            3 => "Manual",
                            4 => "Disabled",
                            _ => "Unknown"
                        };
                    }
                }
            }
            catch { }
            return "Unknown";
        }

        static async Task HandleServiceControl(NetworkStream stream, ServiceControlMessage serviceMsg)
        {
            try
            {
                var service = new ServiceController(serviceMsg.ServiceName);

                switch (serviceMsg.Action)
                {
                    case "Start":
                        if (service.Status != ServiceControllerStatus.Running)
                        {
                            service.Start();
                            service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        }
                        break;

                    case "Stop":
                        if (service.Status != ServiceControllerStatus.Stopped)
                        {
                            service.Stop();
                            service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        }
                        break;

                    case "Restart":
                        if (service.Status == ServiceControllerStatus.Running)
                        {
                            service.Stop();
                            service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        }
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        break;
                }

                var response = new OperationResultMessage
                {
                    Success = true,
                    Message = $"Service {serviceMsg.Action.ToLower()} completed successfully"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error controlling service: {ex.Message}");

                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error controlling service: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        static async Task HandleDirectoryListRequest(NetworkStream stream, DirectoryListRequestMessage request)
        {
            try
            {
                var items = new List<FileSystemItem>();
                var dirInfo = new DirectoryInfo(request.Path);

                foreach (var dir in dirInfo.GetDirectories())
                {
                    try
                    {
                        items.Add(new FileSystemItem
                        {
                            Name = dir.Name,
                            FullPath = dir.FullName,
                            IsDirectory = true,
                            Size = 0,
                            LastModified = dir.LastWriteTime
                        });
                    }
                    catch { }
                }

                foreach (var file in dirInfo.GetFiles())
                {
                    try
                    {
                        items.Add(new FileSystemItem
                        {
                            Name = file.Name,
                            FullPath = file.FullName,
                            IsDirectory = false,
                            Size = file.Length,
                            LastModified = file.LastWriteTime
                        });
                    }
                    catch { }
                }

                var response = new DirectoryListResponseMessage
                {
                    Path = request.Path,
                    Items = items,
                    Success = true
                };

                await NetworkHelper.SendMessageAsync(stream, response);
                Console.WriteLine($"Sent {items.Count} items for directory: {request.Path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing directory: {ex.Message}");

                var response = new DirectoryListResponseMessage
                {
                    Path = request.Path,
                    Items = new List<FileSystemItem>(),
                    Success = false,
                    Error = ex.Message
                };

                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        static async Task HandleDownloadRequest(NetworkStream stream, DownloadFileRequestMessage request)
        {
            try
            {
                var fileInfo = new FileInfo(request.FilePath);
                int chunkNumber = 0;
                long totalChunks = (fileInfo.Length / FileSplitHelper.MaxChunkSize) + 1;

                foreach (var chunk in FileSplitHelper.ReadFileChunks(request.FilePath))
                {
                    chunkNumber++;
                    bool isLast = chunkNumber >= totalChunks;

                    var chunkMsg = new FileChunkMessage
                    {
                        TransferId = request.TransferId,
                        FileName = fileInfo.Name,
                        FileSize = fileInfo.Length,
                        Offset = chunk.Offset,
                        Data = chunk.Data,
                        IsLastChunk = isLast
                    };

                    await NetworkHelper.SendMessageAsync(stream, chunkMsg);
                }

                Console.WriteLine($"Sent {chunkNumber} chunks for file: {request.FilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending file: {ex.Message}");
            }
        }

        static async Task HandleFileChunk(NetworkStream stream, FileChunkMessage chunk)
        {
            try
            {
                if (!activeUploads.TryGetValue(chunk.TransferId, out string filePath))
                {
                    Console.WriteLine($"Warning: Unknown upload transfer ID: {chunk.TransferId}");
                    return;
                }

                FileSplitHelper.WriteFileChunk(filePath, new FileChunk
                {
                    Data = chunk.Data,
                    Offset = chunk.Offset
                });

                if (chunk.IsLastChunk)
                {
                    activeUploads.Remove(chunk.TransferId);
                    Console.WriteLine($"Upload complete: {filePath}");

                    var response = new OperationResultMessage
                    {
                        Success = true,
                        Message = "File uploaded successfully"
                    };
                    await NetworkHelper.SendMessageAsync(stream, response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing file chunk: {ex.Message}");

                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error uploading file: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        static async Task HandleDeleteFile(NetworkStream stream, DeleteFileMessage deleteMsg)
        {
            try
            {
                if (deleteMsg.IsDirectory)
                {
                    Directory.Delete(deleteMsg.Path, true);
                }
                else
                {
                    File.Delete(deleteMsg.Path);
                }

                var response = new OperationResultMessage
                {
                    Success = true,
                    Message = "Deleted successfully"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting: {ex.Message}");

                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error deleting: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        static async Task HandleRenameFile(NetworkStream stream, RenameFileMessage renameMsg)
        {
            try
            {
                string directory = Path.GetDirectoryName(renameMsg.OldPath);
                string newPath = Path.Combine(directory, renameMsg.NewName);

                if (Directory.Exists(renameMsg.OldPath))
                {
                    Directory.Move(renameMsg.OldPath, newPath);
                }
                else if (File.Exists(renameMsg.OldPath))
                {
                    File.Move(renameMsg.OldPath, newPath);
                }

                var response = new OperationResultMessage
                {
                    Success = true,
                    Message = "Renamed successfully"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error renaming: {ex.Message}");

                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error renaming: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        static async Task HandleCreateDirectory(NetworkStream stream, CreateDirectoryMessage createMsg)
        {
            try
            {
                Directory.CreateDirectory(createMsg.Path);

                var response = new OperationResultMessage
                {
                    Success = true,
                    Message = "Directory created successfully"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating directory: {ex.Message}");

                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error creating directory: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        #region Remote Desktop

        private static async Task HandleStartRemoteDesktop(StartRemoteDesktopMessage message, NetworkStream stream)
        {
            try
            {
                isStreamingDesktop = true;
                int quality = message.Quality;

                Console.WriteLine($"Starting desktop streaming with quality: {quality}%");

                desktopStreamThread = new Thread(() => StreamDesktop(stream, quality));
                desktopStreamThread.IsBackground = true;
                desktopStreamThread.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting remote desktop: {ex.Message}");
            }
        }

        private static void HandleStopRemoteDesktop()
        {
            isStreamingDesktop = false;

            if (desktopStreamThread != null && desktopStreamThread.IsAlive)
            {
                desktopStreamThread.Join(1000);
            }

            Console.WriteLine("Stopped desktop streaming");
        }

        private static void StreamDesktop(NetworkStream stream, int quality)
        {
            try
            {
                while (isStreamingDesktop)
                {
                    try
                    {
                        var screenshot = CaptureScreen();

                        if (screenshot != null)
                        {
                            byte[] imageData = CompressImage(screenshot, quality);

                            var frameMessage = new ScreenFrameMessage
                            {
                                ImageData = imageData,
                                Width = screenshot.Width,
                                Height = screenshot.Height
                            };

                            NetworkHelper.SendMessageAsync(stream, frameMessage).Wait();

                            screenshot.Dispose();
                        }

                        Thread.Sleep(66);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error streaming frame: {ex.Message}");
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Desktop streaming thread error: {ex.Message}");
            }
        }

        private static Bitmap CaptureScreen()
        {
            try
            {
                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);

                IntPtr hDesk = GetDesktopWindow();
                IntPtr hSrce = GetWindowDC(hDesk);
                IntPtr hDest = CreateCompatibleDC(hSrce);
                IntPtr hBmp = CreateCompatibleBitmap(hSrce, screenWidth, screenHeight);
                IntPtr hOldBmp = SelectObject(hDest, hBmp);

                bool result = BitBlt(hDest, 0, 0, screenWidth, screenHeight, hSrce, 0, 0, SRCCOPY);

                Bitmap bitmap = Image.FromHbitmap(hBmp);

                SelectObject(hDest, hOldBmp);
                DeleteObject(hBmp);
                DeleteDC(hDest);
                ReleaseDC(hDesk, hSrce);

                return bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error capturing screen: {ex.Message}");
                return null;
            }
        }

        private static byte[] CompressImage(Bitmap bitmap, int quality)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    var encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(
                        System.Drawing.Imaging.Encoder.Quality,
                        (long)quality);

                    var jpegCodec = GetEncoder(ImageFormat.Jpeg);

                    bitmap.Save(ms, jpegCodec, encoderParams);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error compressing image: {ex.Message}");
                return null;
            }
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private static void HandleMouseInput(MouseInputMessage message)
        {
            try
            {
                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);

                int absX = (message.X * 65535) / screenWidth;
                int absY = (message.Y * 65535) / screenHeight;

                switch (message.Action)
                {
                    case "Move":
                        mouse_event(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, absX, absY, 0, 0);
                        break;

                    case "Down":
                        mouse_event(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, absX, absY, 0, 0);
                        if (message.Button == "Left")
                            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                        else if (message.Button == "Right")
                            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                        else if (message.Button == "Middle")
                            mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, 0);
                        break;

                    case "Up":
                        mouse_event(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, absX, absY, 0, 0);
                        if (message.Button == "Left")
                            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                        else if (message.Button == "Right")
                            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                        else if (message.Button == "Middle")
                            mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
                        break;

                    case "DoubleClick":
                        mouse_event(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, absX, absY, 0, 0);
                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                        Thread.Sleep(50);
                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                        break;

                    case "Wheel":
                        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)message.Delta, 0);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling mouse input: {ex.Message}");
            }
        }

        private static void HandleKeyboardInput(KeyboardInputMessage message)
        {
            try
            {
                byte vkCode = (byte)message.KeyCode;

                if (message.IsKeyDown)
                {
                    keybd_event(vkCode, 0, 0, 0);
                }
                else
                {
                    keybd_event(vkCode, 0, KEYEVENTF_KEYUP, 0);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling keyboard input: {ex.Message}");
            }
        }

        #endregion

        static void InstallStartup()
        {
            try
            {
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                exePath = exePath.Replace(".dll", ".exe");

                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    key?.SetValue("RemoteAdmin", exePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error installing to startup: {ex.Message}");
            }
        }

        static void UninstallStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    key?.DeleteValue("RemoteAdmin", false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing from startup: {ex.Message}");
            }
        }
    }
}