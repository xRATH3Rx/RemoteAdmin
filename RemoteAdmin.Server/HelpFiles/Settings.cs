using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace RemoteAdmin.Server
{
    [Serializable]
    public class ServerSettings
    {
        public string BindIP { get; set; } = "127.0.0.1";
        public int ListenPort { get; set; } = 5900;
        public bool AutoStart { get; set; } = true;

        // base64 of DPAPI-encrypted bytes
        public string EncryptedCaPassword { get; set; } = "";
        public DateTime LastSavedUtc { get; set; } = DateTime.UtcNow;
    }

    internal static class SecretBox
    {
        // Optional app-specific entropy to bind the blob to your app
        private static readonly byte[] _entropy = Encoding.UTF8.GetBytes("RemoteAdmin.Server v1");

        public static string ProtectToBase64(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return "";
            var data = Encoding.UTF8.GetBytes(plaintext);
            var cipher = ProtectedData.Protect(data, _entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(cipher);
        }

        public static string UnprotectFromBase64(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return "";
            var cipher = Convert.FromBase64String(base64);
            var data = ProtectedData.Unprotect(cipher, _entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
    }

    internal static class SettingsManager
    {
        private const string FileName = "server.xml";

        public static string PathOnDisk =>
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);

        public static ServerSettings Load()
        {
            try
            {
                if (!File.Exists(PathOnDisk))
                    return new ServerSettings(); // defaults

                var ser = new XmlSerializer(typeof(ServerSettings));
                using var fs = File.OpenRead(PathOnDisk);
                return (ServerSettings)ser.Deserialize(fs);
            }
            catch
            {
                // Corrupt or unreadable—fall back to defaults
                return new ServerSettings();
            }
        }

        public static void Save(ServerSettings s)
        {
            s.LastSavedUtc = DateTime.UtcNow;
            var ser = new XmlSerializer(typeof(ServerSettings));
            using var fs = File.Create(PathOnDisk);
            ser.Serialize(fs, s);
        }
    }
}
