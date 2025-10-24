using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using RemoteAdmin.Client.Config;
using RemoteAdmin.Client.Modules;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Client.Networking
{
    internal class CommandHandler
    {

       public static async Task HandleShellCommand(Stream stream, string command, string shellType)
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

        public static async Task HandleElevationRequest(Stream stream)
        {
            var user = new UserAccount();
            if (user.Type == RemoteAdmin.Shared.Enums.AccountType.Admin)
            {
                await NetworkHelper.SendMessageAsync(stream, new OperationResultMessage
                {
                    Success = true,
                    Message = "Process already elevated."
                });
                return;
            }

            // Relaunch elevated
            try
            {
                string exe = Process.GetCurrentProcess().MainModule!.FileName; // or: Process.GetCurrentProcess().MainModule!.FileName
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    // Optional: pass a flag so the new instance knows it was relaunched
                    Arguments = "--elevated-relaunch",
                    UseShellExecute = true,
                    Verb = "runas",                // triggers UAC
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(psi);

                // Tell server we are attempting elevation and will exit
                await NetworkHelper.SendMessageAsync(stream, new OperationResultMessage
                {
                    Success = true,
                    Message = "Elevation accepted. Relaunching as administrator."
                });

                // Give the message time to flush, then exit current client
                await Task.Delay(300);
                Environment.Exit(0);
            }
            catch (Exception)
            {
                await NetworkHelper.SendMessageAsync(stream, new OperationResultMessage
                {
                    Success = false,
                    Message = "User refused the elevation request."
                });
            }
        }

        public static async Task HandlePowerCommand(Stream stream, string command)
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

        public static async Task HandleProcessListRequest(Stream stream)
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

        public static async Task HandleKillProcess(Stream stream, KillProcessMessage killMsg)
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

        public static async Task HandleServiceListRequest(Stream stream)
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

        public static string GetServiceStartupType(string serviceName)
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

        public static async Task HandleServiceControl(Stream stream, ServiceControlMessage serviceMsg)
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
    }
}
