using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace RemoteAdmin.Client.Certificates
{
    internal class EmbeddedLoader
    {
        internal static X509Certificate2 LoadClientCertificate()
        {
            var pfxBase64 = EmbeddedCerts.CLIENT_PFX_BASE64;
            var pfxPassword = EmbeddedCerts.CLIENT_PFX_PASSWORD;

            // Trim possible trailing \0 from fixed-width injection
            pfxBase64 = pfxBase64.TrimEnd('\0');
            pfxPassword = pfxPassword.TrimEnd('\0');

            // DIAGNOSTIC OUTPUT
            Console.WriteLine($"[DEBUG] PFX Password length: {pfxPassword.Length}");
            Console.WriteLine($"[DEBUG] PFX Password: '{pfxPassword}'");
            Console.WriteLine($"[DEBUG] PFX Base64 length: {pfxBase64.Length}");
            Console.WriteLine($"[DEBUG] PFX Base64 first 100 chars: {pfxBase64.Substring(0, Math.Min(100, pfxBase64.Length))}");
            Console.WriteLine($"[DEBUG] PFX Base64 last 100 chars: {pfxBase64.Substring(Math.Max(0, pfxBase64.Length - 100))}");

            try
            {
                var bytes = Convert.FromBase64String(pfxBase64);
                Console.WriteLine($"[DEBUG] PFX decoded to {bytes.Length} bytes");

                return new X509Certificate2(
                    bytes,
                    pfxPassword,
                    // Use Exportable flag instead of EphemeralKeySet for better compatibility
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet
                );
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"[ERROR] Invalid Base64 format: {ex.Message}");
                throw;
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                Console.WriteLine($"[ERROR] Cryptographic error: {ex.Message}");
                Console.WriteLine($"[ERROR] This usually means wrong password or corrupted certificate data");
                throw;
            }
        }

        internal static X509Certificate2 LoadCaCertificate()
        {
            var crtBase64 = EmbeddedCerts.CA_CRT_BASE64.TrimEnd('\0');

            Console.WriteLine($"[DEBUG] CA CRT Base64 length: {crtBase64.Length}");

            var bytes = Convert.FromBase64String(crtBase64);
            return new X509Certificate2(bytes);
        }
    }
}