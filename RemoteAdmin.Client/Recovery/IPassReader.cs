using System.Collections.Generic;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Client.Recovery
{
    public interface IAccountReader
    {

        IEnumerable<RecoveredAccount> ReadAccounts();

        string ApplicationName { get; }
    }
}
