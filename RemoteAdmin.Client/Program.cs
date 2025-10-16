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
using RemoteAdmin.Client.Config;
using RemoteAdmin.Client.Modules;
using RemoteAdmin.Client.Networking;
using RemoteAdmin.Shared;



namespace RemoteAdmin.Client
{
    class Program
    {

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
            // Handle command line arguments
            if (args.Length > 0)
            {
                if (args[0] == "--install")
                {
                    InstallationManager.InstallStartup();
                    Console.WriteLine("Installed to startup.");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }

                if (args[0] == "--uninstall")
                {
                    InstallationManager.Uninstall();
                    Console.WriteLine("Uninstalled successfully.");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }
            }

            // Initialize configuration
            ClientConfig.Initialize();

            // Perform installation if configured and not already installed
            if (ClientConfig.InstallClient)
            {
                InstallationManager.PerformInstallation();
                // Note: PerformInstallation will exit the process if it installs
                // So code after this only runs if already installed
            }

            Console.WriteLine("RemoteAdmin Client Starting...");
            Console.WriteLine($"Connecting to {ClientConfig.ServerIP}:{ClientConfig.ServerPort}");

            await ConnectionManager.RunClient();
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
    }
}