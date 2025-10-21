using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using RemoteAdmin.Shared.Enums;

namespace RemoteAdmin.Client.Modules
{
    public class UserAccount
    {
        public string UserName { get; }

        public AccountType Type { get; }

        public UserAccount()
        {
            UserName = Environment.UserName;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);

                if (principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    Type = AccountType.Admin;
                }
                else if (principal.IsInRole(WindowsBuiltInRole.User))
                {
                    Type = AccountType.User;
                }
                else if (principal.IsInRole(WindowsBuiltInRole.Guest))
                {
                    Type = AccountType.Guest;
                }
                else
                {
                    Type = AccountType.Unknown;
                }
            }
        }
    }
}
