namespace RemoteAdmin.Server.Build
{
    /// <summary>
    /// Options for building a custom client.
    /// </summary>
    public class BuildOptions
    {
        /// <summary>
        /// Server IP address or hostname.
        /// </summary>
        public string ServerIP { get; set; }

        /// <summary>
        /// Server port.
        /// </summary>
        public int ServerPort { get; set; }

        /// <summary>
        /// Reconnect delay in seconds.
        /// </summary>
        public int ReconnectDelay { get; set; }

        /// <summary>
        /// Whether to install on startup.
        /// </summary>
        public bool InstallOnStartup { get; set; }

        /// <summary>
        /// The output path for the built client.
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// Optional: Path to custom icon file.
        /// </summary>
        public string IconPath { get; set; }

        /// <summary>
        /// Optional: Assembly title/product name.
        /// </summary>
        public string AssemblyTitle { get; set; }

        /// <summary>
        /// Optional: Assembly company name.
        /// </summary>
        public string AssemblyCompany { get; set; }

        /// <summary>
        /// Whether to obfuscate the client.
        /// </summary>
        public bool Obfuscate { get; set; }

        public BuildOptions()
        {
            // Defaults
            ReconnectDelay = 30;
            InstallOnStartup = true;
            Obfuscate = true;
            AssemblyTitle = "Client Application";
            AssemblyCompany = "Company Name";
        }
    }
}