using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Server
{
    public partial class RemoteDesktopWindow : Window
    {
        private ConnectedClient client;
        private DateTime lastFrameTime = DateTime.Now;
        private int frameCount = 0;
        private bool isMouseCaptured = false;

        public RemoteDesktopWindow(ConnectedClient client)
        {
            InitializeComponent();
            this.client = client;

            txtClientInfo.Text = $"Remote Desktop: {client.ComputerName} ({client.Username})";

            // Start remote desktop streaming
            StartRemoteDesktop();

            // Update FPS counter every second
            var fpsTimer = new System.Windows.Threading.DispatcherTimer();
            fpsTimer.Interval = TimeSpan.FromSeconds(1);
            fpsTimer.Tick += (s, e) =>
            {
                txtFPS.Text = $"FPS: {frameCount}";
                frameCount = 0;
            };
            fpsTimer.Start();
        }

        private async void StartRemoteDesktop()
        {
            try
            {
                int quality = GetQualityValue();

                var message = new StartRemoteDesktopMessage
                {
                    Quality = quality,
                    ScreenIndex = 0
                };

                await NetworkHelper.SendMessageAsync(client.Stream, message);
                UpdateStatus("Streaming...", false);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}", true);
            }
        }

        private async void StopRemoteDesktop()
        {
            try
            {
                await NetworkHelper.SendMessageAsync(client.Stream, new StopRemoteDesktopMessage());
            }
            catch { }
        }

        public void UpdateScreen(byte[] imageData, int width, int height)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    using (var ms = new MemoryStream(imageData))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        imgDesktop.Source = bitmap;
                    }

                    txtResolution.Text = $"{width}x{height}";
                    frameCount++;
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Frame error: {ex.Message}", true);
                }
            });
        }

        public void UpdateStatus(string status, bool isError = false)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = $"Status: {status}";
                txtStatus.Foreground = isError ?
                    System.Windows.Media.Brushes.Red :
                    System.Windows.Media.Brushes.LimeGreen;
            });
        }

        private int GetQualityValue()
        {
            return cboQuality.SelectedIndex switch
            {
                0 => 50,  // Low
                1 => 75,  // Medium
                2 => 90,  // High
                _ => 75
            };
        }

        private void cboQuality_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (client != null)
            {
                StopRemoteDesktop();
                System.Threading.Thread.Sleep(100);
                StartRemoteDesktop();
            }
        }

        private void btnFullscreen_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                WindowState = WindowState.Maximized;
                WindowStyle = WindowStyle.None;
                btnFullscreen.Content = "Exit Fullscreen";
            }
            else
            {
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.SingleBorderWindow;
                btnFullscreen.Content = "Fullscreen";
            }
        }

        // Mouse Events
        private async void imgDesktop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(imgDesktop);
            string button = e.ChangedButton switch
            {
                MouseButton.Left => "Left",
                MouseButton.Right => "Right",
                MouseButton.Middle => "Middle",
                _ => "Left"
            };

            var message = new MouseInputMessage
            {
                X = (int)pos.X,
                Y = (int)pos.Y,
                Button = button,
                Action = "Down"
            };

            await NetworkHelper.SendMessageAsync(client.Stream, message);
        }

        private async void imgDesktop_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(imgDesktop);
            string button = e.ChangedButton switch
            {
                MouseButton.Left => "Left",
                MouseButton.Right => "Right",
                MouseButton.Middle => "Middle",
                _ => "Left"
            };

            var message = new MouseInputMessage
            {
                X = (int)pos.X,
                Y = (int)pos.Y,
                Button = button,
                Action = "Up"
            };

            await NetworkHelper.SendMessageAsync(client.Stream, message);
        }

        private async void imgDesktop_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(imgDesktop);

            var message = new MouseInputMessage
            {
                X = (int)pos.X,
                Y = (int)pos.Y,
                Action = "Move"
            };

            await NetworkHelper.SendMessageAsync(client.Stream, message);
        }

        private async void imgDesktop_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(imgDesktop);

            var message = new MouseInputMessage
            {
                X = (int)pos.X,
                Y = (int)pos.Y,
                Action = "Wheel",
                Delta = e.Delta
            };

            await NetworkHelper.SendMessageAsync(client.Stream, message);
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            var message = new KeyboardInputMessage
            {
                KeyCode = KeyInterop.VirtualKeyFromKey(e.Key),
                IsKeyDown = true,
                Shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift),
                Ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl),
                Alt = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)
            };

            await NetworkHelper.SendMessageAsync(client.Stream, message);
        }

        protected override async void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);

            var message = new KeyboardInputMessage
            {
                KeyCode = KeyInterop.VirtualKeyFromKey(e.Key),
                IsKeyDown = false,
                Shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift),
                Ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl),
                Alt = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)
            };

            await NetworkHelper.SendMessageAsync(client.Stream, message);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            StopRemoteDesktop();
            client.RemoteDesktopWindow = null;
        }
    }
}