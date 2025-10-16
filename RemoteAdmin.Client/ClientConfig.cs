using System;

namespace RemoteAdmin.Client
{
    public static class ClientConfig
    {
        public static string ServerIP { get; private set; }
        public static int ServerPort { get; private set; }
        public static int ReconnectInterval { get; private set; }

        static ClientConfig()
        {
            ServerIP = "###PLACEHOLDER_IP_ADDR_X9K2P7L4M###";
            ServerPort = 999999999;      // Large positive int
            ReconnectInterval = 888888888; // Large positive int  
        }

        public static void Initialize()
        {
            Console.WriteLine($"Client configured to connect to {ServerIP}:{ServerPort}");
        }
    }
}