using System;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RemoteAdmin.Shared
{
    public static class NetworkHelper
    {
        private const int HeaderSize = 4; // 4 bytes for message length

        /// <summary>
        /// Send a message over a TLS stream (works with both Stream and SslStream)
        /// </summary>
        public static async Task SendMessageAsync(Stream stream, object message)
        {
            try
            {
                // Serialize message
                string json = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    WriteIndented = false
                });

                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                // Create header with message length
                byte[] header = BitConverter.GetBytes(jsonBytes.Length);

                // Send header
                await stream.WriteAsync(header, 0, header.Length);

                // Send message
                await stream.WriteAsync(jsonBytes, 0, jsonBytes.Length);
                await stream.FlushAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send message: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Receive a message from a TLS stream (works with both Stream and SslStream)
        /// </summary>
        public static async Task<object> ReceiveMessageAsync(Stream stream)
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
                        return null; // Connection closed

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
                        return null; // Connection closed

                    bytesRead += read;
                }

                // Deserialize
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
                _ => throw new Exception($"Unknown message type: {messageType}")
            };
        }

        /// <summary>
        /// Create a server-side SSL stream with client certificate validation
        /// </summary>
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
                    // Validate client certificate against our CA
                    if (certificate == null)
                    {
                        Console.WriteLine("Client did not provide a certificate");
                        return false;
                    }

                    // Build chain with our CA
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

                    // Verify the root is our CA
                    var chainRoot = customChain.ChainElements[customChain.ChainElements.Count - 1].Certificate;
                    if (chainRoot.Thumbprint != caCertificate.Thumbprint)
                    {
                        Console.WriteLine("Client certificate not signed by our CA");
                        return false;
                    }

                    Console.WriteLine("Client certificate validated successfully");
                    return true;
                });

            // Authenticate as server and require client certificate
            await sslStream.AuthenticateAsServerAsync(
                serverCertificate,
                clientCertificateRequired: true,
                enabledSslProtocols: SslProtocols.Tls12 | SslProtocols.Tls13,
                checkCertificateRevocation: false);

            Console.WriteLine($"TLS established: {sslStream.SslProtocol}");
            Console.WriteLine($"Cipher: {sslStream.CipherAlgorithm} ({sslStream.CipherStrength} bits)");

            return sslStream;
        }

        /// <summary>
        /// Create a client-side SSL stream with server certificate validation
        /// </summary>
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
                    // Validate server certificate against our CA
                    if (certificate == null)
                    {
                        Console.WriteLine("Server did not provide a certificate");
                        return false;
                    }

                    // Build chain with our CA
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

                    // Verify the root is our CA
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
                    // Provide our client certificate
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
    }
}