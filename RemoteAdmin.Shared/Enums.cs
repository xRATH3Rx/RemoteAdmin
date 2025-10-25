namespace RemoteAdmin.Shared.Enums
{
    public enum AccountType
    {
        Admin,
        User,
        Guest,
        Unknown
    }

    public enum MessageType
    {
        OpenRegistryEditor,
        RegistryEnumerate,
        RegistryData,
        RegistryCreateKey,
        RegistryDeleteKey,
        RegistrySetValue,
        RegistryDeleteValue,
        RegistryOperationResult
    }

    [Serializable]
    public enum RegistryValueType
    {
        String,          // REG_SZ
        ExpandString,    // REG_EXPAND_SZ
        Binary,          // REG_BINARY
        DWord,           // REG_DWORD
        MultiString,     // REG_MULTI_SZ
        QWord            // REG_QWORD
    }

    // Enum for registry operations
    [Serializable]
    public enum RegistryOperation
    {
        CreateKey,
        DeleteKey,
        SetValue,
        DeleteValue
    }

    [Serializable]
    public enum StartupType
    {
        LocalMachineRun = 0,
        LocalMachineRunOnce = 1,
        CurrentUserRun = 2,
        CurrentUserRunOnce = 3,
        StartMenu = 4,
        LocalMachineRunX86 = 5,
        LocalMachineRunOnceX86 = 6
    }

    public enum TaskState
    {
        Unknown = 0,
        Disabled = 1,
        Queued = 2,
        Ready = 3,
        Running = 4
    }

    public enum TriggerType
    {
        Event = 0,
        Time = 1,
        Daily = 2,
        Weekly = 3,
        Monthly = 4,
        MonthlyDOW = 5,
        Idle = 6,
        Registration = 7,
        Boot = 8,
        Logon = 9,
        SessionStateChange = 11,
        Custom = 12
    }

    public enum ActionType
    {
        Execute = 0,
        ComHandler = 5,
        SendEmail = 6,
        ShowMessage = 7
    }

    public enum TaskOperation
    {
        Create,
        Delete,
        Enable,
        Disable,
        Run,
        Export
    }
}
