using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Threading.Tasks;
using Microsoft.Win32;
using RemoteAdmin.Shared;
using RemoteAdmin.Shared.Enums;

namespace RemoteAdmin.Client.Handlers
{
    public static class SystemInformationHandler
    {
        public static async Task HandleGetSystemInfo(SslStream stream)
        {
            try
            {
                Console.WriteLine("[INFO] Getting system information...");

                var systemInfo = GetSystemInformation();

                var response = new GetSystemInfoResponseMessage
                {
                    SystemInfo = systemInfo
                };

                await NetworkHelper.SendMessageAsync(stream, response);

                Console.WriteLine($"[INFO] Sent {systemInfo.Count} system info items to server");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Getting system info: {ex.Message}");
            }
        }

        public static List<SystemInfoItem> GetSystemInformation()
        {
            var info = new List<SystemInfoItem>();

            try
            {
                info.Add(new SystemInfoItem("Computer Name", Environment.MachineName));
                info.Add(new SystemInfoItem("Username", Environment.UserName));
                info.Add(new SystemInfoItem("Domain", Environment.UserDomainName));
                info.AddRange(GetOSInfo());
                info.AddRange(GetCPUInfo());
                info.AddRange(GetMemoryInfo());
                info.AddRange(GetGPUInfo());
                info.AddRange(GetMotherboardInfo());
                info.AddRange(GetNetworkInfo());
                info.AddRange(GetDiskInfo());
                info.Add(new SystemInfoItem("System Uptime", GetUptime()));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Getting system information: {ex.Message}");
                info.Add(new SystemInfoItem("Error", ex.Message));
            }

            return info;
        }

        private static List<SystemInfoItem> GetOSInfo()
        {
            var items = new List<SystemInfoItem>();

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject os in searcher.Get())
                    {
                        items.Add(new SystemInfoItem("OS Name", os["Caption"]?.ToString()));
                        items.Add(new SystemInfoItem("OS Version", os["Version"]?.ToString()));
                        items.Add(new SystemInfoItem("OS Manufacturer", os["Manufacturer"]?.ToString()));
                        items.Add(new SystemInfoItem("OS Architecture", os["OSArchitecture"]?.ToString()));
                        items.Add(new SystemInfoItem("OS Build", os["BuildNumber"]?.ToString()));
                        items.Add(new SystemInfoItem("OS Serial Number", os["SerialNumber"]?.ToString()));

                        // Install Date
                        string installDate = os["InstallDate"]?.ToString();
                        if (!string.IsNullOrEmpty(installDate))
                        {
                            DateTime date = ManagementDateTimeConverter.ToDateTime(installDate);
                            items.Add(new SystemInfoItem("OS Install Date", date.ToString("yyyy-MM-dd HH:mm:ss")));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                items.Add(new SystemInfoItem("OS Info Error", ex.Message));
            }

            return items;
        }

        private static List<SystemInfoItem> GetCPUInfo()
        {
            var items = new List<SystemInfoItem>();

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
                {
                    foreach (ManagementObject cpu in searcher.Get())
                    {
                        items.Add(new SystemInfoItem("CPU Name", cpu["Name"]?.ToString().Trim()));
                        items.Add(new SystemInfoItem("CPU Manufacturer", cpu["Manufacturer"]?.ToString()));
                        items.Add(new SystemInfoItem("CPU Cores", cpu["NumberOfCores"]?.ToString()));
                        items.Add(new SystemInfoItem("CPU Logical Processors", cpu["NumberOfLogicalProcessors"]?.ToString()));
                        items.Add(new SystemInfoItem("CPU Max Clock Speed", $"{cpu["MaxClockSpeed"]} MHz"));
                        items.Add(new SystemInfoItem("CPU Socket", cpu["SocketDesignation"]?.ToString()));
                    }
                }
            }
            catch (Exception ex)
            {
                items.Add(new SystemInfoItem("CPU Info Error", ex.Message));
            }

            return items;
        }

        private static List<SystemInfoItem> GetMemoryInfo()
        {
            var items = new List<SystemInfoItem>();

            try
            {
                long totalMemory = 0;

                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory"))
                {
                    int slotCount = 0;
                    foreach (ManagementObject mem in searcher.Get())
                    {
                        slotCount++;
                        long capacity = Convert.ToInt64(mem["Capacity"]);
                        totalMemory += capacity;

                        items.Add(new SystemInfoItem($"RAM Slot {slotCount}", $"{capacity / (1024 * 1024 * 1024)} GB"));
                        items.Add(new SystemInfoItem($"RAM Speed {slotCount}", $"{mem["Speed"]} MHz"));
                        items.Add(new SystemInfoItem($"RAM Manufacturer {slotCount}", mem["Manufacturer"]?.ToString()));
                    }
                }

                items.Insert(0, new SystemInfoItem("Total RAM", $"{totalMemory / (1024 * 1024 * 1024)} GB"));
            }
            catch (Exception ex)
            {
                items.Add(new SystemInfoItem("Memory Info Error", ex.Message));
            }

            return items;
        }

        private static List<SystemInfoItem> GetGPUInfo()
        {
            var items = new List<SystemInfoItem>();

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController"))
                {
                    int gpuCount = 0;
                    foreach (ManagementObject gpu in searcher.Get())
                    {
                        gpuCount++;
                        items.Add(new SystemInfoItem($"GPU {gpuCount}", gpu["Name"]?.ToString()));

                        var ram = gpu["AdapterRAM"];
                        if (ram != null)
                        {
                            long ramBytes = Convert.ToInt64(ram);
                            if (ramBytes > 0)
                            {
                                items.Add(new SystemInfoItem($"GPU {gpuCount} Memory", $"{ramBytes / (1024 * 1024)} MB"));
                            }
                        }

                        items.Add(new SystemInfoItem($"GPU {gpuCount} Driver Version", gpu["DriverVersion"]?.ToString()));
                    }
                }
            }
            catch (Exception ex)
            {
                items.Add(new SystemInfoItem("GPU Info Error", ex.Message));
            }

            return items;
        }

        private static List<SystemInfoItem> GetMotherboardInfo()
        {
            var items = new List<SystemInfoItem>();

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject board in searcher.Get())
                    {
                        items.Add(new SystemInfoItem("Motherboard Manufacturer", board["Manufacturer"]?.ToString()));
                        items.Add(new SystemInfoItem("Motherboard Product", board["Product"]?.ToString()));
                        items.Add(new SystemInfoItem("Motherboard Serial", board["SerialNumber"]?.ToString()));
                    }
                }

                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS"))
                {
                    foreach (ManagementObject bios in searcher.Get())
                    {
                        items.Add(new SystemInfoItem("BIOS Manufacturer", bios["Manufacturer"]?.ToString()));
                        items.Add(new SystemInfoItem("BIOS Version", bios["SMBIOSBIOSVersion"]?.ToString()));
                    }
                }
            }
            catch (Exception ex)
            {
                items.Add(new SystemInfoItem("Motherboard Info Error", ex.Message));
            }

            return items;
        }

        private static List<SystemInfoItem> GetNetworkInfo()
        {
            var items = new List<SystemInfoItem>();

            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                               n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                int adapterCount = 0;
                foreach (var adapter in interfaces)
                {
                    adapterCount++;
                    items.Add(new SystemInfoItem($"Network Adapter {adapterCount}", adapter.Name));
                    items.Add(new SystemInfoItem($"Adapter {adapterCount} Type", adapter.NetworkInterfaceType.ToString()));
                    items.Add(new SystemInfoItem($"Adapter {adapterCount} Speed", $"{adapter.Speed / 1000000} Mbps"));
                    items.Add(new SystemInfoItem($"Adapter {adapterCount} MAC", adapter.GetPhysicalAddress().ToString()));

                    var ipProps = adapter.GetIPProperties();
                    var ipv4 = ipProps.UnicastAddresses
                        .FirstOrDefault(ip => ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                    if (ipv4 != null)
                    {
                        items.Add(new SystemInfoItem($"Adapter {adapterCount} IPv4", ipv4.Address.ToString()));
                    }
                }
            }
            catch (Exception ex)
            {
                items.Add(new SystemInfoItem("Network Info Error", ex.Message));
            }

            return items;
        }

        private static List<SystemInfoItem> GetDiskInfo()
        {
            var items = new List<SystemInfoItem>();

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))
                {
                    int diskCount = 0;
                    foreach (ManagementObject disk in searcher.Get())
                    {
                        diskCount++;
                        items.Add(new SystemInfoItem($"Disk {diskCount}", disk["Caption"]?.ToString()));

                        var size = disk["Size"];
                        if (size != null)
                        {
                            long sizeBytes = Convert.ToInt64(size);
                            items.Add(new SystemInfoItem($"Disk {diskCount} Size", $"{sizeBytes / (1024L * 1024 * 1024)} GB"));
                        }

                        items.Add(new SystemInfoItem($"Disk {diskCount} Interface", disk["InterfaceType"]?.ToString()));
                    }
                }

                var drives = System.IO.DriveInfo.GetDrives().Where(d => d.IsReady);
                foreach (var drive in drives)
                {
                    items.Add(new SystemInfoItem($"Drive {drive.Name}",
                        $"{drive.TotalSize / (1024L * 1024 * 1024)} GB Total, " +
                        $"{drive.AvailableFreeSpace / (1024L * 1024 * 1024)} GB Free"));
                }
            }
            catch (Exception ex)
            {
                items.Add(new SystemInfoItem("Disk Info Error", ex.Message));
            }

            return items;
        }

        private static string GetUptime()
        {
            try
            {
                TimeSpan uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                return $"{uptime.Days} days, {uptime.Hours} hours, {uptime.Minutes} minutes";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}