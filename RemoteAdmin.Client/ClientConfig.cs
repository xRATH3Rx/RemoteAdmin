using System;

namespace RemoteAdmin.Client
{
    /// <summary>
    /// Client configuration that gets modified at build time.
    /// The builder injects values into the static constructor.
    /// </summary>
    public static class ClientConfig
    {
        public static string ServerIP { get; private set; }
        public static int ServerPort { get; private set; }
        public static int ReconnectInterval { get; private set; }

        // Static constructor - the builder will inject IL code here
        static ClientConfig()
        {
            // These default values will be REPLACED by the builder
            ServerIP = "127.0.0.1";
            ServerPort = 5900;
            ReconnectInterval = 30;
        }

        public static void Initialize()
        {
            // Optional: Add any initialization logic here
            Console.WriteLine($"Client configured to connect to {ServerIP}:{ServerPort}");
        }
    }
}