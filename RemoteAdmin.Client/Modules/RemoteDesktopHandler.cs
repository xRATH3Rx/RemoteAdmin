using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Client.Modules
{
    internal class RemoteDesktopHandler
    {
        public static bool isStreamingDesktop = false;
        public static Thread desktopStreamThread;

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

        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;
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

        #endregion

        public static async Task HandleStartRemoteDesktop(StartRemoteDesktopMessage message, NetworkStream stream)
        {
            try
            {
                isStreamingDesktop = true;
                int quality = message.Quality;

                Console.WriteLine($"Starting desktop streaming with quality: {quality}%");

                desktopStreamThread = new Thread(() => StreamDesktop(stream, quality));
                desktopStreamThread.IsBackground = true;
                desktopStreamThread.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting remote desktop: {ex.Message}");
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

        public static void StreamDesktop(NetworkStream stream, int quality)
        {
            try
            {
                while (isStreamingDesktop)
                {
                    try
                    {
                        var screenshot = CaptureScreen();

                        if (screenshot != null)
                        {
                            byte[] imageData = CompressImage(screenshot, quality);

                            var frameMessage = new ScreenFrameMessage
                            {
                                ImageData = imageData,
                                Width = screenshot.Width,
                                Height = screenshot.Height
                            };

                            NetworkHelper.SendMessageAsync(stream, frameMessage).Wait();

                            screenshot.Dispose();
                        }

                        Thread.Sleep(66);
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

        public static Bitmap CaptureScreen()
        {
            try
            {
                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);

                IntPtr hDesk = GetDesktopWindow();
                IntPtr hSrce = GetWindowDC(hDesk);
                IntPtr hDest = CreateCompatibleDC(hSrce);
                IntPtr hBmp = CreateCompatibleBitmap(hSrce, screenWidth, screenHeight);
                IntPtr hOldBmp = SelectObject(hDest, hBmp);

                bool result = BitBlt(hDest, 0, 0, screenWidth, screenHeight, hSrce, 0, 0, SRCCOPY);

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
                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);

                int absX = (message.X * 65535) / screenWidth;
                int absY = (message.Y * 65535) / screenHeight;

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
