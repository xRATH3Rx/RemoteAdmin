using System;
using System.IO;
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
                byte[] fileBytes = File.ReadAllBytes(_templatePath);

                if (!InjectString(ref fileBytes, IP_PLACEHOLDER, _options.ServerIP))
                {
                    throw new Exception("Failed to inject ServerIP - placeholder not found!");
                }

                // Inject Port
                if (!InjectInt32(ref fileBytes, PORT_PLACEHOLDER, _options.ServerPort))
                {
                    throw new Exception("Failed to inject ServerPort - placeholder not found!");
                }

                if (!InjectInt32(ref fileBytes, INTERVAL_PLACEHOLDER, _options.ReconnectDelay))
                {
                    throw new Exception("Failed to inject ReconnectDelay - placeholder not found!");
                }

                // Write output
                File.WriteAllBytes(_options.OutputPath, fileBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nERROR: {ex.Message}");
                throw;
            }
        }

        private bool InjectString(ref byte[] data, string placeholder, string value)
        {
            byte[] placeholderBytes = Encoding.Unicode.GetBytes(placeholder);
            byte[] valueBytes = Encoding.Unicode.GetBytes(value);

            int index = FindPattern(data, placeholderBytes);
            if (index == -1)
            {
                return false;
            }

            if (valueBytes.Length > placeholderBytes.Length)
            {
                throw new Exception($"Value '{value}' is too long! Max length: {placeholderBytes.Length / 2} chars");
            }

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
            {
                return false;
            }
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
                {
                    return i;
                }
            }
            return -1;
        }
    }
}