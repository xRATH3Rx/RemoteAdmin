namespace RemoteAdmin.Server.Build
{
    /// <summary>
    /// Options for building a custom client.
    /// </summary>
    public class BuildOptions
    {
        // Connection Settings
        public string ServerIP { get; set; }
        public int ServerPort { get; set; }
        public int ReconnectDelay { get; set; }

        // Installation Settings
        public bool InstallClient { get; set; }
        public string InstallLocation { get; set; } // "AppData", "ProgramFiles", "System"
        public string InstallSubDirectory { get; set; }
        public string InstallName { get; set; }
        public bool SetFileHidden { get; set; }
        public bool SetSubDirHidden { get; set; }

        // Startup Settings
        public bool InstallOnStartup { get; set; }
        public string StartupName { get; set; }

        // Assembly Settings
        public string AssemblyTitle { get; set; }
        public string AssemblyCompany { get; set; }
        public string IconPath { get; set; }

        // Advanced
        public bool Obfuscate { get; set; }

        // Output
        public string OutputPath { get; set; }

        public BuildOptions()
        {
            // Connection defaults
            ReconnectDelay = 30;

            // Installation defaults
            InstallClient = true;
            InstallLocation = "AppData";
            InstallSubDirectory = "SubDir";
            InstallName = "Client";
            SetFileHidden = false;
            SetSubDirHidden = false;

            // Startup defaults
            InstallOnStartup = true;
            StartupName = "Client";

            // Assembly defaults
            AssemblyTitle = "Client Application";
            AssemblyCompany = "Company Name";

            // Advanced defaults
            Obfuscate = false;
        }
    }
}