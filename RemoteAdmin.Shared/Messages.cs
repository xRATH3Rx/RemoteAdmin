using System;
using System.Collections.Generic;
using RemoteAdmin.Shared.Enums;

namespace RemoteAdmin.Shared
{
    public class Message
    {
        public string Type { get; set; }
        public string MessageType => Type;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
    public class ClientInfoMessage : Message
    {
        public ClientInfoMessage()
        {
            Type = "ClientInfo";
        }

        public string ComputerName { get; set; }
        public string Username { get; set; }
        public string OSVersion { get; set; }
        public string IPAddress { get; set; }
        public string PublicIP { get; set; }
        public string AccountType { get; set; }
    }

    public class HeartbeatMessage : Message
    {
        public HeartbeatMessage()
        {
            Type = "Heartbeat";
        }
    }

    public class CommandMessage : Message
    {
        public CommandMessage()
        {
            Type = "Command";
        }

        public string Command { get; set; }
        public string Data { get; set; }
    }

    public class ResponseMessage : Message
    {
        public ResponseMessage()
        {
            Type = "Response";
        }

        public string Data { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    // Shell command from server to client
    public class ShellCommandMessage : Message
    {
        public ShellCommandMessage()
        {
            Type = "ShellCommand";
        }

        public string Command { get; set; }
        public string ShellType { get; set; } = "PowerShell";
    }

    // Shell output from client to server
    public class ShellOutputMessage : Message
    {
        public ShellOutputMessage()
        {
            Type = "ShellOutput";
        }

        public string Output { get; set; }
        public bool IsError { get; set; }
    }

    // Power command from server to client
    public class PowerCommandMessage : Message
    {
        public PowerCommandMessage()
        {
            Type = "PowerCommand";
        }

        public string Command { get; set; }
    }

    // Request process list
    public class ProcessListRequestMessage : Message
    {
        public ProcessListRequestMessage()
        {
            Type = "ProcessListRequest";
        }
    }

    // Process list response
    public class ProcessListResponseMessage : Message
    {
        public ProcessListResponseMessage()
        {
            Type = "ProcessListResponse";
        }

        public List<ProcessInfo> Processes { get; set; }
    }

    // Process info class
    public class ProcessInfo
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public long MemoryMB { get; set; }
        public string CpuPercent { get; set; }
    }

    // Kill process command
    public class KillProcessMessage : Message
    {
        public KillProcessMessage()
        {
            Type = "KillProcess";
        }

        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
    }

    // Request service list
    public class ServiceListRequestMessage : Message
    {
        public ServiceListRequestMessage()
        {
            Type = "ServiceListRequest";
        }
    }

    // Service list response
    public class ServiceListResponseMessage : Message
    {
        public ServiceListResponseMessage()
        {
            Type = "ServiceListResponse";
        }

        public List<ServiceInfo> Services { get; set; }
    }

    // Service info class
    public class ServiceInfo
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Status { get; set; }
        public string StartupType { get; set; }
    }

    // Service control command
    public class ServiceControlMessage : Message
    {
        public ServiceControlMessage()
        {
            Type = "ServiceControl";
        }

        public string ServiceName { get; set; }
        public string Action { get; set; } // Start, Stop, Restart
    }

    // Generic success/error response
    public class OperationResultMessage : Message
    {
        public OperationResultMessage()
        {
            Type = "OperationResult";
        }

        public bool Success { get; set; }
        public string Message { get; set; }
    }

    // File system item (file or directory)
    public class FileSystemItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }

    // Request directory listing
    public class DirectoryListRequestMessage : Message
    {
        public DirectoryListRequestMessage()
        {
            Type = "DirectoryListRequest";
        }

        public string Path { get; set; }
    }

    // Directory listing response
    public class DirectoryListResponseMessage : Message
    {
        public DirectoryListResponseMessage()
        {
            Type = "DirectoryListResponse";
        }

        public string Path { get; set; }
        public List<FileSystemItem> Items { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }

    // File chunk for transfer
    public class FileChunk
    {
        public byte[] Data { get; set; }
        public long Offset { get; set; }
    }

    // Download file request
    public class DownloadFileRequestMessage : Message
    {
        public DownloadFileRequestMessage()
        {
            Type = "DownloadFileRequest";
        }

        public string FilePath { get; set; }
        public string TransferId { get; set; }
    }

    public class DownloadFolderRequestMessage : Message
    {
        public DownloadFolderRequestMessage()
        {
            Type = "DownloadFolderRequest";
        }

        public string FolderPath { get; set; }
        public string TransferId { get; set; }
    }

    public class FolderStructureMessage : Message
    {
        public FolderStructureMessage()
        {
            Type = "FolderStructure";
        }

        public string TransferId { get; set; }
        public string FolderName { get; set; }
        public List<FolderFileInfo> Files { get; set; }
        public int TotalFiles { get; set; }
    }

    public class FolderFileInfo
    {
        public string RelativePath { get; set; } // Relative to the root folder being downloaded
        public string FullPath { get; set; }
        public long FileSize { get; set; }
    }

    public class FolderFileChunkMessage : Message
    {
        public FolderFileChunkMessage()
        {
            Type = "FolderFileChunk";
        }

        public string TransferId { get; set; }
        public string RelativePath { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public long Offset { get; set; }
        public byte[] Data { get; set; }
        public bool IsLastChunk { get; set; }
        public int FileIndex { get; set; }
        public int TotalFiles { get; set; }
    }


// File chunk message
public class FileChunkMessage : Message
    {
        public FileChunkMessage()
        {
            Type = "FileChunk";
        }

        public string TransferId { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public long Offset { get; set; }
        public byte[] Data { get; set; }
        public bool IsLastChunk { get; set; }
    }

    // Upload file start
    public class UploadFileStartMessage : Message
    {
        public UploadFileStartMessage()
        {
            Type = "UploadFileStart";
        }

        public string TransferId { get; set; }
        public string DestinationPath { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
    }

    // Delete file/folder
    public class DeleteFileMessage : Message
    {
        public DeleteFileMessage()
        {
            Type = "DeleteFile";
        }

        public string Path { get; set; }
        public bool IsDirectory { get; set; }
    }

    // Rename file/folder
    public class RenameFileMessage : Message
    {
        public RenameFileMessage()
        {
            Type = "RenameFile";
        }

        public string OldPath { get; set; }
        public string NewName { get; set; }
    }

    // Create directory
    public class CreateDirectoryMessage : Message
    {
        public CreateDirectoryMessage()
        {
            Type = "CreateDirectory";
        }

        public string Path { get; set; }
    }

    public class StartRemoteDesktopMessage : Message
    {
        public StartRemoteDesktopMessage() { Type = "StartRemoteDesktop"; }
        public int Quality { get; set; } = 75;
        public int MonitorIndex { get; set; } = 0;
    }

    public class StopRemoteDesktopMessage : Message
    {
        public StopRemoteDesktopMessage() { Type = "StopRemoteDesktop"; }
    }

    public class ScreenFrameMessage : Message
    {
        public ScreenFrameMessage() { Type = "ScreenFrame"; }
        public byte[] ImageData { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class MouseInputMessage : Message
    {
        public MouseInputMessage() { Type = "MouseInput"; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Button { get; set; }
        public string Action { get; set; }
        public int Delta { get; set; }
    }

    public class KeyboardInputMessage : Message
    {
        public KeyboardInputMessage() { Type = "KeyboardInput"; }
        public int KeyCode { get; set; }
        public bool IsKeyDown { get; set; }
        public bool Shift { get; set; }
        public bool Ctrl { get; set; }
        public bool Alt { get; set; }
    }

    public class VisitWebsiteMessage : Message
    {
        public VisitWebsiteMessage()
        {
            Type = "VisitWebsite";
        }

        public string Url { get; set; }
        public bool Hidden { get; set; }
    }

    // Visit website result
    public class WebsiteVisitResultMessage : Message
    {
        public WebsiteVisitResultMessage()
        {
            Type = "WebsiteVisitResult";
        }

        public bool Success { get; set; }
        public string Message { get; set; }
        public string Url { get; set; }
    }

    [Serializable]
    public class MonitorInfo
    {
        public int Index { get; set; }
        public string DeviceName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsPrimary { get; set; }
    }

    [Serializable]
    public class MonitorInfoMessage : Message  // Add : Message here
    {
        public MonitorInfoMessage()
        {
            Type = "MonitorInfo";  // Add this
        }

        public List<MonitorInfo> Monitors { get; set; }
    }

    [Serializable]
    public class SelectMonitorMessage : Message
    {
        public SelectMonitorMessage()
        {
            Type = "SelectMonitor";
        }

        public int MonitorIndex { get; set; }
    }
    // Request to open registry editor
    [Serializable]
    public class OpenRegistryEditorMessage : Message
    {
        public OpenRegistryEditorMessage()
        {
            Type = "OpenRegistryEditor";
        }
    }

    // Request to enumerate registry keys
    [Serializable]
    public class RegistryEnumerateMessage : Message
    {
        public RegistryEnumerateMessage()
        {
            Type = "RegistryEnumerate";
        }

        public string KeyPath { get; set; }
    }

    // Response with registry keys and values
    [Serializable]
    public class RegistryDataMessage : Message
    {
        public RegistryDataMessage()
        {
            Type = "RegistryData";
        }

        public string CurrentPath { get; set; }
        public List<RegistryKeyInfo> SubKeys { get; set; }
        public List<RegistryValueInfo> Values { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    [Serializable]
    public class RegistryKeyInfo
    {
        public string Name { get; set; }
        public int SubKeyCount { get; set; }
        public int ValueCount { get; set; }
    }

    [Serializable]
    public class RegistryValueInfo
    {
        public string Name { get; set; }
        public RegistryValueType ValueType { get; set; }
        public object Data { get; set; }

        public string Type => GetRegistryTypeString(ValueType);

        private string GetRegistryTypeString(RegistryValueType type)
        {
            switch (type)
            {
                case RegistryValueType.String:
                    return "REG_SZ";
                case RegistryValueType.ExpandString:
                    return "REG_EXPAND_SZ";
                case RegistryValueType.Binary:
                    return "REG_BINARY";
                case RegistryValueType.DWord:
                    return "REG_DWORD";
                case RegistryValueType.MultiString:
                    return "REG_MULTI_SZ";
                case RegistryValueType.QWord:
                    return "REG_QWORD";
                default:
                    return "REG_UNKNOWN";
            }
        }
    }

    // Create a new registry key
    [Serializable]
    public class RegistryCreateKeyMessage : Message
    {
        public RegistryCreateKeyMessage()
        {
            Type = "RegistryCreateKey";
        }

        public string ParentPath { get; set; }
        public string KeyName { get; set; }
    }

    // Delete a registry key
    [Serializable]
    public class RegistryDeleteKeyMessage : Message
    {
        public RegistryDeleteKeyMessage()
        {
            Type = "RegistryDeleteKey";
        }

        public string KeyPath { get; set; }
    }

    // Create or modify a registry value
    [Serializable]
    public class RegistrySetValueMessage : Message
    {
        public RegistrySetValueMessage()
        {
            Type = "RegistrySetValue";
        }

        public string KeyPath { get; set; }
        public string ValueName { get; set; }
        public RegistryValueType ValueType { get; set; }
        public object ValueData { get; set; }
    }

    // Delete a registry value
    [Serializable]
    public class RegistryDeleteValueMessage : Message
    {
        public RegistryDeleteValueMessage()
        {
            Type = "RegistryDeleteValue";
        }

        public string KeyPath { get; set; }
        public string ValueName { get; set; }
    }

    // Response for registry operations (create, delete, set)
    [Serializable]
    public class RegistryOperationResultMessage : Message
    {
        public RegistryOperationResultMessage()
        {
            Type = "RegistryOperationResult";
        }

        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public RegistryOperation Operation { get; set; }
    }

    public class ElevationRequestMessage : Message
    {
        public ElevationRequestMessage()
        {
            Type = "Elevate";
        }
    }

    [Serializable]
    public class StartupItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public StartupType Type { get; set; }
    }

    [Serializable]
    public class GetStartupItemsMessage : Message
    {
        public GetStartupItemsMessage()
        {
            Type = "GetStartupItems";
        }
    }

    [Serializable]
    public class GetStartupItemsResponseMessage : Message
    {
        public GetStartupItemsResponseMessage()
        {
            Type = "GetStartupItemsResponse";
        }

        public List<StartupItem> StartupItems { get; set; }
    }

    [Serializable]
    public class AddStartupItemMessage : Message
    {
        public AddStartupItemMessage()
        {
            Type = "AddStartupItem";
        }

        public StartupItem Item { get; set; }
    }

    [Serializable]
    public class RemoveStartupItemMessage : Message
    {
        public RemoveStartupItemMessage()
        {
            Type = "RemoveStartupItem";
        }

        public StartupItem Item { get; set; }
    }


    [Serializable]
    public class StartupItemOperationResponseMessage : Message
    {
        public StartupItemOperationResponseMessage()
        {
            Type = "StartupItemOperationResponse";
        }

        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    [Serializable]
    public class GetSystemInfoMessage : Message
    {
        public GetSystemInfoMessage()
        {
            Type = "GetSystemInfo";
        }
    }

    [Serializable]
    public class GetSystemInfoResponseMessage : Message
    {
        public GetSystemInfoResponseMessage()
        {
            Type = "GetSystemInfoResponse";
        }

        public List<SystemInfoItem> SystemInfo { get; set; }
    }

    [Serializable]
    public class SystemInfoItem
    {
        public string Key { get; set; }
        public string Value { get; set; }

        public SystemInfoItem() { }

        public SystemInfoItem(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }

    public static class TaskResultCodes
    {
        public const int Success = 0x0;
        public const int OperationAborted = 0x41306;
        public const int OperationTimedOut = 0x102;
        public const int TaskNotRun = 0x41303;
        public const int TaskDisabled = 0x41302;
        public const int TaskQueued = 0x41325;
        public const int TaskRunning = 0x41301;

        public static string GetResultDescription(int code)
        {
            return code switch
            {
                Success => "The operation completed successfully",
                OperationAborted => "The task was terminated by the user",
                OperationTimedOut => "The task has timed out",
                TaskNotRun => "The task has not yet run",
                TaskDisabled => "The task is disabled",
                TaskQueued => "The task is queued",
                TaskRunning => "The task is currently running",
                _ => $"Unknown result code: 0x{code:X}"
            };
        }
    }

    [Serializable]
    public class ScheduledTask
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public TaskState State { get; set; }
        public DateTime? LastRunTime { get; set; }
        public DateTime? NextRunTime { get; set; }
        public int LastTaskResult { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public List<TaskTrigger> Triggers { get; set; }
        public List<TaskAction> Actions { get; set; }
        public bool Enabled { get; set; }
        public bool Hidden { get; set; }
        public string RunAsUser { get; set; }
        public bool RunWithHighest { get; set; }
        public bool RunOnlyWhenLoggedOn { get; set; }

    }

    [Serializable]
    public class TaskTrigger
    {
        public TriggerType Type { get; set; }
        public bool Enabled { get; set; }
        public string Schedule { get; set; }
        public DateTime? StartBoundary { get; set; }
        public DateTime? EndBoundary { get; set; }
        public string Details { get; set; }
    }

    [Serializable]
    public class TaskAction
    {
        public ActionType Type { get; set; }
        public string Path { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
    }

    [Serializable]
    public class GetScheduledTasksMessage : Message
    {
        public GetScheduledTasksMessage()
        {
            Type = "GetScheduledTasks";
        }
    }

    [Serializable]
    public class GetScheduledTasksResponseMessage : Message
    {
        public GetScheduledTasksResponseMessage()
        {
            Type = "GetScheduledTasksResponse";
        }

        public List<ScheduledTask> Tasks { get; set; }
    }

    [Serializable]
    public class CreateScheduledTaskMessage : Message
    {
        public CreateScheduledTaskMessage()
        {
            Type = "CreateScheduledTask";
        }

        public ScheduledTask Task { get; set; }
    }


    [Serializable]
    public class DeleteScheduledTaskMessage : Message
    {
        public DeleteScheduledTaskMessage()
        {
            Type = "DeleteScheduledTask";
        }

        public string TaskPath { get; set; }
    }

    [Serializable]
    public class ToggleScheduledTaskMessage : Message
    {
        public ToggleScheduledTaskMessage()
        {
            Type = "ToggleScheduledTask";
        }

        public string TaskPath { get; set; }
        public bool Enable { get; set; }
    }


    [Serializable]
    public class RunScheduledTaskMessage : Message
    {
        public RunScheduledTaskMessage()
        {
            Type = "RunScheduledTask";
        }

        public string TaskPath { get; set; }
    }

    [Serializable]
    public class ScheduledTaskOperationResponseMessage : Message
    {
        public ScheduledTaskOperationResponseMessage()
        {
            Type = "ScheduledTaskOperationResponse";
        }

        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public TaskOperation Operation { get; set; }
    }

    [Serializable]
    public class ExportScheduledTaskMessage : Message
    {
        public ExportScheduledTaskMessage()
        {
            Type = "ExportScheduledTask";
        }

        public string TaskPath { get; set; }
    }

    [Serializable]
    public class ExportScheduledTaskResponseMessage : Message
    {
        public ExportScheduledTaskResponseMessage()
        {
            Type = "ExportScheduledTaskResponse";
        }

        public bool Success { get; set; }
        public string TaskXml { get; set; }
        public string ErrorMessage { get; set; }
    }
}
