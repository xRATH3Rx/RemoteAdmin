using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RemoteAdmin.Shared
{
    public static class NetworkHelper
    {
        public static async Task SendMessageAsync(NetworkStream stream, Message message)
        {
            try
            {
                string json = JsonConvert.SerializeObject(message);
                byte[] data = Encoding.UTF8.GetBytes(json);

                byte[] lengthBytes = BitConverter.GetBytes(data.Length);
                await stream.WriteAsync(lengthBytes, 0, 4);

                await stream.WriteAsync(data, 0, data.Length);
                await stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
                throw;
            }
        }

        public static async Task<Message> ReceiveMessageAsync(NetworkStream stream)
        {
            try
            {
                byte[] lengthBytes = new byte[4];
                int bytesRead = await stream.ReadAsync(lengthBytes, 0, 4);
                if (bytesRead == 0) return null;

                int length = BitConverter.ToInt32(lengthBytes, 0);

                byte[] data = new byte[length];
                int totalRead = 0;
                while (totalRead < length)
                {
                    bytesRead = await stream.ReadAsync(data, totalRead, length - totalRead);
                    if (bytesRead == 0) return null;
                    totalRead += bytesRead;
                }

                string json = Encoding.UTF8.GetString(data);

                // Determine message type and deserialize accordingly
                var baseMsg = JsonConvert.DeserializeObject<Message>(json);

                return baseMsg.Type switch
                {
                    "ClientInfo" => JsonConvert.DeserializeObject<ClientInfoMessage>(json),
                    "Heartbeat" => JsonConvert.DeserializeObject<HeartbeatMessage>(json),
                    "Command" => JsonConvert.DeserializeObject<CommandMessage>(json),
                    "Response" => JsonConvert.DeserializeObject<ResponseMessage>(json),
                    "ShellCommand" => JsonConvert.DeserializeObject<ShellCommandMessage>(json),
                    "ShellOutput" => JsonConvert.DeserializeObject<ShellOutputMessage>(json),
                    "PowerCommand" => JsonConvert.DeserializeObject<PowerCommandMessage>(json),
                    "ProcessListRequest" => JsonConvert.DeserializeObject<ProcessListRequestMessage>(json),
                    "ProcessListResponse" => JsonConvert.DeserializeObject<ProcessListResponseMessage>(json),
                    "KillProcess" => JsonConvert.DeserializeObject<KillProcessMessage>(json),
                    "ServiceListRequest" => JsonConvert.DeserializeObject<ServiceListRequestMessage>(json),
                    "ServiceListResponse" => JsonConvert.DeserializeObject<ServiceListResponseMessage>(json),
                    "ServiceControl" => JsonConvert.DeserializeObject<ServiceControlMessage>(json),
                    "OperationResult" => JsonConvert.DeserializeObject<OperationResultMessage>(json),
                    "DirectoryListRequest" => JsonConvert.DeserializeObject<DirectoryListRequestMessage>(json),
                    "DirectoryListResponse" => JsonConvert.DeserializeObject<DirectoryListResponseMessage>(json),
                    "DownloadFileRequest" => JsonConvert.DeserializeObject<DownloadFileRequestMessage>(json),
                    "FileChunk" => JsonConvert.DeserializeObject<FileChunkMessage>(json),
                    "UploadFileStart" => JsonConvert.DeserializeObject<UploadFileStartMessage>(json),
                    "DeleteFile" => JsonConvert.DeserializeObject<DeleteFileMessage>(json),
                    "RenameFile" => JsonConvert.DeserializeObject<RenameFileMessage>(json),
                    "CreateDirectory" => JsonConvert.DeserializeObject<CreateDirectoryMessage>(json),
                    "StartRemoteDesktop" => JsonConvert.DeserializeObject<StartRemoteDesktopMessage>(json),
                    "StopRemoteDesktop" => JsonConvert.DeserializeObject<StopRemoteDesktopMessage>(json),
                    "ScreenFrame" => JsonConvert.DeserializeObject<ScreenFrameMessage>(json),
                    "MouseInput" => JsonConvert.DeserializeObject<MouseInputMessage>(json),
                    "KeyboardInput" => JsonConvert.DeserializeObject<KeyboardInputMessage>(json),
                    _ => baseMsg
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
                return null;
            }
        }
    }
}