using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Client.Modules
{
    internal class RemoteDesktopHandler
    {
        public static bool isStreamingDesktop = false;
        public static Thread? desktopStreamThread;
        private static int currentMonitorIndex = 0; // Track which monitor we're streaming
        private static int currentQuality = 75;
        private static Stream currentStream;

        #region Win32 APIs

        [DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        // NEW: Multi-monitor support
        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        public delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;
        public const int SM_XVIRTUALSCREEN = 76;
        public const int SM_YVIRTUALSCREEN = 77;
        public const int SM_CXVIRTUALSCREEN = 78;
        public const int SM_CYVIRTUALSCREEN = 79;
        public const int SRCCOPY = 0x00CC0020;

        public const uint MOUSEEVENTF_MOVE = 0x0001;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        public const uint MOUSEEVENTF_WHEEL = 0x0800;
        public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        public const uint KEYEVENTF_KEYUP = 0x0002;

        public const uint MONITOR_DEFAULTTOPRIMARY = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public DisplayDeviceStateFlags StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [Flags]
        public enum DisplayDeviceStateFlags : int
        {
            AttachedToDesktop = 0x1,
            MultiDriver = 0x2,
            PrimaryDevice = 0x4,
            MirroringDriver = 0x8,
            VGACompatible = 0x10,
            Removable = 0x20,
            ModesPruned = 0x8000000,
            Remote = 0x4000000,
            Disconnect = 0x2000000
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        public const int ENUM_CURRENT_SETTINGS = -1;

        #endregion

        // NEW: Get all available monitors using Win32 API
        private static List<MonitorData> monitors = new List<MonitorData>();

        private class MonitorData
        {
            public int Index { get; set; }
            public string DeviceName { get; set; }
            public RECT Bounds { get; set; }
            public bool IsPrimary { get; set; }
        }

        public static List<MonitorInfo> GetMonitors()
        {
            monitors.Clear();
            Console.WriteLine("Starting monitor enumeration...");

            int index = 0;

            // Use a different approach - enumerate using DISPLAY_DEVICE
            try
            {
                for (uint i = 0; ; i++)
                {
                    DISPLAY_DEVICE displayDevice = new DISPLAY_DEVICE();
                    displayDevice.cb = Marshal.SizeOf(displayDevice);

                    if (!EnumDisplayDevices(null, i, ref displayDevice, 0))
                        break;

                    // Skip non-active displays
                    if ((displayDevice.StateFlags & DisplayDeviceStateFlags.AttachedToDesktop) == 0)
                        continue;

                    // Get monitor info for this device
                    DEVMODE devMode = new DEVMODE();
                    devMode.dmSize = (short)Marshal.SizeOf(devMode);

                    if (EnumDisplaySettings(displayDevice.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode))
                    {
                        bool isPrimary = (displayDevice.StateFlags & DisplayDeviceStateFlags.PrimaryDevice) != 0;

                        var monitorData = new MonitorData
                        {
                            Index = index,
                            DeviceName = displayDevice.DeviceName,
                            Bounds = new RECT
                            {
                                Left = devMode.dmPositionX,
                                Top = devMode.dmPositionY,
                                Right = devMode.dmPositionX + devMode.dmPelsWidth,
                                Bottom = devMode.dmPositionY + devMode.dmPelsHeight
                            },
                            IsPrimary = isPrimary
                        };

                        monitors.Add(monitorData);

                        Console.WriteLine($"Found monitor {index}: {displayDevice.DeviceName}, " +
                                        $"{devMode.dmPelsWidth}x{devMode.dmPelsHeight}, " +
                                        $"Primary: {isPrimary}, " +
                                        $"Position: ({devMode.dmPositionX},{devMode.dmPositionY})");

                        index++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enumerating displays: {ex.Message}");
            }

            Console.WriteLine($"Total monitors found: {monitors.Count}");

            // Create the return list
            var result = monitors.Select(m => new MonitorInfo
            {
                Index = m.Index,
                DeviceName = m.DeviceName,
                Width = m.Bounds.Width,
                Height = m.Bounds.Height,
                IsPrimary = m.IsPrimary
            }).ToList();

            // If still no monitors found, create a default one for primary screen
            if (result.Count == 0)
            {
                Console.WriteLine("WARNING: No monitors detected! Creating default monitor entry...");
                int width = GetSystemMetrics(SM_CXSCREEN);
                int height = GetSystemMetrics(SM_CYSCREEN);

                monitors.Add(new MonitorData
                {
                    Index = 0,
                    DeviceName = "Primary Display",
                    Bounds = new RECT { Left = 0, Top = 0, Right = width, Bottom = height },
                    IsPrimary = true
                });

                result.Add(new MonitorInfo
                {
                    Index = 0,
                    DeviceName = "Primary Display",
                    Width = width,
                    Height = height,
                    IsPrimary = true
                });
            }

            return result;
        }

        public static async Task HandleStartRemoteDesktop(StartRemoteDesktopMessage message, SslStream stream)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(stream);

            try
            {
                currentStream = stream;

                // ALWAYS enumerate monitors first
                var monitorsList = GetMonitors();
                Console.WriteLine($"Enumerated {monitorsList.Count} monitors");

                // Send monitor info to server
                var monitorInfo = new MonitorInfoMessage
                {
                    Monitors = monitorsList
                };
                await NetworkHelper.SendMessageAsync(stream, monitorInfo);
                Console.WriteLine($"Sent monitor info: {monitorInfo.Monitors.Count} monitors");

                // If Quality is 0, just send monitor info (not starting stream)
                if (message.Quality == 0)
                {
                    Console.WriteLine("Quality is 0, only sending monitor info");
                    return;
                }

                // Start actual streaming
                isStreamingDesktop = true;
                currentQuality = Math.Clamp(message.Quality, 1, 100);
                currentMonitorIndex = message.MonitorIndex;

                // Validate monitor index
                if (currentMonitorIndex < 0 || currentMonitorIndex >= monitors.Count)
                {
                    Console.WriteLine($"Invalid monitor index {currentMonitorIndex}, defaulting to 0");
                    currentMonitorIndex = 0;
                }

                Console.WriteLine($"Starting desktop streaming on monitor {currentMonitorIndex} with quality: {currentQuality}%");

                desktopStreamThread = new Thread(() => StreamDesktop(stream, currentQuality, currentMonitorIndex))
                {
                    IsBackground = true,
                    Name = "DesktopStream"
                };
                desktopStreamThread.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting remote desktop: {ex}");
            }
        }

        // NEW: Handle monitor switching
        public static void HandleSelectMonitor(SelectMonitorMessage message)
        {
            try
            {
                Console.WriteLine($"Switching from monitor {currentMonitorIndex} to monitor {message.MonitorIndex}");

                // Validate new monitor index
                if (monitors.Count == 0)
                    GetMonitors();

                if (message.MonitorIndex < 0 || message.MonitorIndex >= monitors.Count)
                {
                    Console.WriteLine($"Invalid monitor index: {message.MonitorIndex}");
                    return;
                }

                // Simply update the monitor index - the streaming loop will pick it up
                currentMonitorIndex = message.MonitorIndex;
                Console.WriteLine($"Now streaming monitor {currentMonitorIndex}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error switching monitor: {ex.Message}");
            }
        }

        internal static void HandleStopRemoteDesktop()
        {
            isStreamingDesktop = false;

            if (desktopStreamThread != null && desktopStreamThread.IsAlive)
            {
                desktopStreamThread.Join(1000);
            }

            Console.WriteLine("Stopped desktop streaming");
        }

        public static void StreamDesktop(SslStream stream, int quality, int monitorIndex)
        {
            try
            {
                while (isStreamingDesktop)
                {
                    try
                    {
                        // Use the current monitor index (it may change during streaming)
                        var screenshot = CaptureScreen(currentMonitorIndex);

                        if (screenshot != null)
                        {
                            byte[] imageData = CompressImage(screenshot, currentQuality);

                            var frameMessage = new ScreenFrameMessage
                            {
                                ImageData = imageData,
                                Width = screenshot.Width,
                                Height = screenshot.Height
                            };

                            NetworkHelper.SendMessageAsync(stream, frameMessage).Wait();

                            screenshot.Dispose();
                        }

                        Thread.Sleep(66); // ~15 FPS
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error streaming frame: {ex.Message}");
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Desktop streaming thread error: {ex.Message}");
            }
        }

        // UPDATED: Now accepts monitor index parameter
        public static Bitmap CaptureScreen(int monitorIndex = 0)
        {
            try
            {
                // Ensure monitors are enumerated
                if (monitors.Count == 0)
                    GetMonitors();

                // Validate monitor index
                if (monitorIndex < 0 || monitorIndex >= monitors.Count)
                {
                    monitorIndex = 0; // Fall back to primary
                }

                // Get the selected monitor
                var selectedMonitor = monitors[monitorIndex];
                RECT bounds = selectedMonitor.Bounds;

                int width = bounds.Width;
                int height = bounds.Height;

                IntPtr hDesk = GetDesktopWindow();
                IntPtr hSrce = GetWindowDC(hDesk);
                IntPtr hDest = CreateCompatibleDC(hSrce);
                IntPtr hBmp = CreateCompatibleBitmap(hSrce, width, height);
                IntPtr hOldBmp = SelectObject(hDest, hBmp);

                // Capture from the monitor's position
                bool result = BitBlt(hDest, 0, 0, width, height, hSrce, bounds.Left, bounds.Top, SRCCOPY);

                Bitmap bitmap = Image.FromHbitmap(hBmp);

                SelectObject(hDest, hOldBmp);
                DeleteObject(hBmp);
                DeleteDC(hDest);
                ReleaseDC(hDesk, hSrce);

                return bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error capturing screen: {ex.Message}");
                return null;
            }
        }

        public static byte[] CompressImage(Bitmap bitmap, int quality)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    var encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(
                        System.Drawing.Imaging.Encoder.Quality,
                        (long)quality);

                    var jpegCodec = GetEncoder(ImageFormat.Jpeg);

                    bitmap.Save(ms, jpegCodec, encoderParams);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error compressing image: {ex.Message}");
                return null;
            }
        }

        public static ImageCodecInfo GetEncoder(ImageFormat format)
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

        public static void HandleMouseInput(MouseInputMessage message)
        {
            try
            {
                // Ensure monitors are enumerated
                if (monitors.Count == 0)
                    GetMonitors();

                // Get the bounds of the current monitor
                if (currentMonitorIndex < 0 || currentMonitorIndex >= monitors.Count)
                    currentMonitorIndex = 0;

                var selectedMonitor = monitors[currentMonitorIndex];
                RECT bounds = selectedMonitor.Bounds;

                // Convert relative coordinates to absolute screen coordinates
                int absoluteX = bounds.Left + message.X;
                int absoluteY = bounds.Top + message.Y;

                // Get full virtual screen dimensions
                int virtualScreenWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
                int virtualScreenHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);
                int virtualScreenLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
                int virtualScreenTop = GetSystemMetrics(SM_YVIRTUALSCREEN);

                // Convert to normalized coordinates (0-65535) relative to virtual screen
                int absX = ((absoluteX - virtualScreenLeft) * 65535) / virtualScreenWidth;
                int absY = ((absoluteY - virtualScreenTop) * 65535) / virtualScreenHeight;

                switch (message.Action)
                {
                    case "Move":
                        mouse_event(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, absX, absY, 0, 0);
                        break;

                    case "Down":
                        mouse_event(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, absX, absY, 0, 0);
                        if (message.Button == "Left")
                            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                        else if (message.Button == "Right")
                            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
                        else if (message.Button == "Middle")
                            mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, 0);
                        break;

                    case "Up":
                        mouse_event(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, absX, absY, 0, 0);
                        if (message.Button == "Left")
                            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                        else if (message.Button == "Right")
                            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                        else if (message.Button == "Middle")
                            mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
                        break;

                    case "DoubleClick":
                        mouse_event(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, absX, absY, 0, 0);
                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                        Thread.Sleep(50);
                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                        break;

                    case "Wheel":
                        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)message.Delta, 0);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling mouse input: {ex.Message}");
            }
        }

        public static void HandleKeyboardInput(KeyboardInputMessage message)
        {
            try
            {
                byte vkCode = (byte)message.KeyCode;

                if (message.IsKeyDown)
                {
                    keybd_event(vkCode, 0, 0, 0);
                }
                else
                {
                    keybd_event(vkCode, 0, KEYEVENTF_KEYUP, 0);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling keyboard input: {ex.Message}");
            }
        }
    }
}