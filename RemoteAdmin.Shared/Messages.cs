using System;
using System.Collections.Generic;

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
    public class SelectMonitorMessage : Message  // Add : Message here
    {
        public SelectMonitorMessage()
        {
            Type = "SelectMonitor";  // Add this
        }

        public int MonitorIndex { get; set; }
    }
}