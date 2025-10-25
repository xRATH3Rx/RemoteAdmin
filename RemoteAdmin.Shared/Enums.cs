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
}
