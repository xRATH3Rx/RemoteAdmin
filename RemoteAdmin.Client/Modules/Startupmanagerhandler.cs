using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Win32;
using RemoteAdmin.Shared;
using RemoteAdmin.Shared.Enums;

namespace RemoteAdmin.Client.Handlers
{
    public static class StartupManagerHandler
    {
        public static async Task HandleGetStartupItems(SslStream stream)
        {
            try
            {
                Console.WriteLine("[INFO] Getting startup items...");

                var items = GetAllStartupItems();

                var response = new GetStartupItemsResponseMessage
                {
                    StartupItems = items
                };

                await NetworkHelper.SendMessageAsync(stream, response);

                Console.WriteLine($"[INFO] Sent {items.Count} startup items to server");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Getting startup items: {ex.Message}");
            }
        }

        public static async Task HandleAddStartupItem(SslStream stream, AddStartupItemMessage message)
        {
            try
            {
                Console.WriteLine($"[INFO] Adding startup item: {message.Item.Name}");

                bool success = AddStartupItem(message.Item, out string errorMessage);

                var response = new StartupItemOperationResponseMessage
                {
                    Success = success,
                    ErrorMessage = errorMessage
                };

                await NetworkHelper.SendMessageAsync(stream, response);

                if (success)
                {
                    Console.WriteLine($"[INFO] Successfully added startup item: {message.Item.Name}");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Failed to add startup item: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Adding startup item: {ex.Message}");

                var response = new StartupItemOperationResponseMessage
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };

                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        public static async Task HandleRemoveStartupItem(SslStream stream, RemoveStartupItemMessage message)
        {
            try
            {
                Console.WriteLine($"[INFO] Removing startup item: {message.Item.Name}");

                bool success = RemoveStartupItem(message.Item, out string errorMessage);

                var response = new StartupItemOperationResponseMessage
                {
                    Success = success,
                    ErrorMessage = errorMessage
                };

                await NetworkHelper.SendMessageAsync(stream, response);

                if (success)
                {
                    Console.WriteLine($"[INFO] Successfully removed startup item: {message.Item.Name}");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Failed to remove startup item: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Removing startup item: {ex.Message}");

                var response = new StartupItemOperationResponseMessage
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };

                await NetworkHelper.SendMessageAsync(stream, response);
            }
        }

        public static List<StartupItem> GetAllStartupItems()
        {
            var items = new List<StartupItem>();

            try
            {
                items.AddRange(GetStartupItemsFromRegistry(StartupType.LocalMachineRun));
                items.AddRange(GetStartupItemsFromRegistry(StartupType.LocalMachineRunOnce));
                items.AddRange(GetStartupItemsFromRegistry(StartupType.CurrentUserRun));
                items.AddRange(GetStartupItemsFromRegistry(StartupType.CurrentUserRunOnce));
                items.AddRange(GetStartupItemsFromRegistry(StartupType.LocalMachineRunX86));
                items.AddRange(GetStartupItemsFromRegistry(StartupType.LocalMachineRunOnceX86));
                items.AddRange(GetStartupItemsFromStartMenu());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Getting startup items: {ex.Message}");
            }

            return items;
        }

        private static List<StartupItem> GetStartupItemsFromRegistry(StartupType type)
        {
            var items = new List<StartupItem>();
            string keyPath = GetRegistryPath(type);

            if (string.IsNullOrEmpty(keyPath))
                return items;

            try
            {
                RegistryKey baseKey = type == StartupType.CurrentUserRun || type == StartupType.CurrentUserRunOnce
                    ? Registry.CurrentUser
                    : Registry.LocalMachine;

                using (var key = baseKey.OpenSubKey(keyPath, false))
                {
                    if (key == null)
                        return items;

                    foreach (string valueName in key.GetValueNames())
                    {
                        try
                        {
                            var value = key.GetValue(valueName)?.ToString();
                            if (!string.IsNullOrEmpty(value))
                            {
                                items.Add(new StartupItem
                                {
                                    Name = valueName,
                                    Path = value,
                                    Type = type
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Reading registry value {valueName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (SecurityException)
            {
                Console.WriteLine($"[ERROR] Access denied to registry path: {keyPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Reading registry: {ex.Message}");
            }

            return items;
        }

        private static List<StartupItem> GetStartupItemsFromStartMenu()
        {
            var items = new List<StartupItem>();

            try
            {
                string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);

                if (Directory.Exists(startupPath))
                {
                    var files = Directory.GetFiles(startupPath, "*.lnk")
                        .Concat(Directory.GetFiles(startupPath, "*.exe"))
                        .Concat(Directory.GetFiles(startupPath, "*.bat"));

                    foreach (string file in files)
                    {
                        items.Add(new StartupItem
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            Path = file,
                            Type = StartupType.StartMenu
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Reading start menu folder: {ex.Message}");
            }

            return items;
        }

        public static bool AddStartupItem(StartupItem item, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                if (item.Type == StartupType.StartMenu)
                {
                    return AddToStartMenu(item, out errorMessage);
                }
                else
                {
                    return AddToRegistry(item, out errorMessage);
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to add startup item: {ex.Message}";
                return false;
            }
        }

        public static bool RemoveStartupItem(StartupItem item, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                if (item.Type == StartupType.StartMenu)
                {
                    return RemoveFromStartMenu(item, out errorMessage);
                }
                else
                {
                    return RemoveFromRegistry(item, out errorMessage);
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Failed to remove startup item: {ex.Message}";
                return false;
            }
        }

        private static bool AddToRegistry(StartupItem item, out string errorMessage)
        {
            errorMessage = null;
            string keyPath = GetRegistryPath(item.Type);

            if (string.IsNullOrEmpty(keyPath))
            {
                errorMessage = "Invalid startup type";
                return false;
            }

            try
            {
                RegistryKey baseKey = item.Type == StartupType.CurrentUserRun || item.Type == StartupType.CurrentUserRunOnce
                    ? Registry.CurrentUser
                    : Registry.LocalMachine;

                using (var key = baseKey.OpenSubKey(keyPath, true))
                {
                    if (key == null)
                    {
                        errorMessage = "Could not open registry key";
                        return false;
                    }

                    key.SetValue(item.Name, item.Path, RegistryValueKind.String);
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                errorMessage = "Access denied. Administrator privileges may be required.";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static bool RemoveFromRegistry(StartupItem item, out string errorMessage)
        {
            errorMessage = null;
            string keyPath = GetRegistryPath(item.Type);

            if (string.IsNullOrEmpty(keyPath))
            {
                errorMessage = "Invalid startup type";
                return false;
            }

            try
            {
                RegistryKey baseKey = item.Type == StartupType.CurrentUserRun || item.Type == StartupType.CurrentUserRunOnce
                    ? Registry.CurrentUser
                    : Registry.LocalMachine;

                using (var key = baseKey.OpenSubKey(keyPath, true))
                {
                    if (key == null)
                    {
                        errorMessage = "Could not open registry key";
                        return false;
                    }

                    key.DeleteValue(item.Name, false);
                }

                return true;
            }
            catch (UnauthorizedAccessException)
            {
                errorMessage = "Access denied. Administrator privileges may be required.";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static bool AddToStartMenu(StartupItem item, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string targetPath = Path.Combine(startupPath, item.Name + ".lnk");

                if (File.Exists(item.Path))
                {
                    File.Copy(item.Path, targetPath, true);
                    return true;
                }
                else
                {
                    errorMessage = "Source file does not exist";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static bool RemoveFromStartMenu(StartupItem item, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                if (File.Exists(item.Path))
                {
                    File.Delete(item.Path);
                    return true;
                }
                else
                {
                    errorMessage = "File does not exist";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static string GetRegistryPath(StartupType type)
        {
            return type switch
            {
                StartupType.LocalMachineRun => @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                StartupType.LocalMachineRunOnce => @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
                StartupType.CurrentUserRun => @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                StartupType.CurrentUserRunOnce => @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
                StartupType.LocalMachineRunX86 => @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
                StartupType.LocalMachineRunOnceX86 => @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce",
                _ => null
            };
        }
    }
}