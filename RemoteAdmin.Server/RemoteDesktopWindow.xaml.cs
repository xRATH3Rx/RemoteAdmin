using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Server
{
    public partial class RemoteDesktopWindow : Window
    {
        private ConnectedClient client;
        private bool isStreaming = false;
        private bool mouseControlEnabled = false;
        private bool keyboardControlEnabled = false;

        public RemoteDesktopWindow(ConnectedClient connectedClient)
        {
            InitializeComponent();
            client = connectedClient;
            txtClientInfo.Text = $"Remote Desktop - {client.ComputerName} ({client.Username})";

            // Focus the image so it can receive keyboard events
            imgDesktop.Focus();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            StartRemoteDesktop();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            StopRemoteDesktop();
        }

        private async void StartRemoteDesktop()
        {
            try
            {
                int quality = GetQualityValue();
                var message = new StartRemoteDesktopMessage { Quality = quality };
                await NetworkHelper.SendMessageAsync(client.Stream, message);

                isStreaming = true;
                btnStart.IsEnabled = false;
                btnStop.IsEnabled = true;
                cboQuality.IsEnabled = false;

                UpdateStatus("Connected", true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start remote desktop: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StopRemoteDesktop()
        {
            try
            {
                var message = new StopRemoteDesktopMessage();
                await NetworkHelper.SendMessageAsync(client.Stream, message);

                isStreaming = false;
                btnStart.IsEnabled = true;
                btnStop.IsEnabled = false;
                cboQuality.IsEnabled = true;

                // Disable controls when stopping
                if (mouseControlEnabled)
                {
                    ToggleMouseControl();
                }
                if (keyboardControlEnabled)
                {
                    ToggleKeyboardControl();
                }

                UpdateStatus("Disconnected", false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to stop remote desktop: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void UpdateScreen(byte[] imageData, int width, int height)
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
                    txtResolution.Text = $"{width} x {height}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating screen: {ex.Message}");
            }
        }

        private void UpdateStatus(string status, bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = status;
                statusIndicator.Fill = isConnected ?
                    (SolidColorBrush)FindResource("SuccessGreen") :
                    (SolidColorBrush)FindResource("DangerRed");
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
            if (isStreaming)
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

        // Toggle Mouse Control
        private void btnToggleMouse_Click(object sender, RoutedEventArgs e)
        {
            ToggleMouseControl();
        }

        private void ToggleMouseControl()
        {
            mouseControlEnabled = !mouseControlEnabled;

            if (mouseControlEnabled)
            {
                btnToggleMouse.Content = "Disable";
                btnToggleMouse.Background = (SolidColorBrush)FindResource("DangerRed");
                txtMouseStatus.Text = "Enabled";
                txtMouseStatus.Foreground = (SolidColorBrush)FindResource("SuccessGreen");
            }
            else
            {
                btnToggleMouse.Content = "Enable";
                btnToggleMouse.Background = (SolidColorBrush)FindResource("SuccessGreen");
                txtMouseStatus.Text = "Disabled";
                txtMouseStatus.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            }
        }

        // Toggle Keyboard Control
        private void btnToggleKeyboard_Click(object sender, RoutedEventArgs e)
        {
            ToggleKeyboardControl();
        }

        private void ToggleKeyboardControl()
        {
            keyboardControlEnabled = !keyboardControlEnabled;

            if (keyboardControlEnabled)
            {
                btnToggleKeyboard.Content = "Disable";
                btnToggleKeyboard.Background = (SolidColorBrush)FindResource("DangerRed");
                txtKeyboardStatus.Text = "Enabled";
                txtKeyboardStatus.Foreground = (SolidColorBrush)FindResource("SuccessGreen");
                imgDesktop.Focus(); // Ensure image has focus for keyboard events
            }
            else
            {
                btnToggleKeyboard.Content = "Enable";
                btnToggleKeyboard.Background = (SolidColorBrush)FindResource("SuccessGreen");
                txtKeyboardStatus.Text = "Disabled";
                txtKeyboardStatus.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170));
            }
        }

        // Mouse Events - Only work when enabled
        private async void imgDesktop_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!mouseControlEnabled || !isStreaming) return;

            var pos = e.GetPosition(imgDesktop);
            string button = e.ChangedButton switch
            {
                MouseButton.Left => "Left",
                MouseButton.Right => "Right",
                MouseButton.Middle => "Middle",
                _ => "Left"
            };

            // Scale coordinates to actual screen resolution
            int scaledX = (int)(pos.X * (imgDesktop.Source.Width / imgDesktop.ActualWidth));
            int scaledY = (int)(pos.Y * (imgDesktop.Source.Height / imgDesktop.ActualHeight));

            var message = new MouseInputMessage
            {
                X = scaledX,
                Y = scaledY,
                Button = button,
                Action = "Down"
            };

            await NetworkHelper.SendMessageAsync(client.Stream, message);
        }

        private async void imgDesktop_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!mouseControlEnabled || !isStreaming) return;

            var pos = e.GetPosition(imgDesktop);
            string button = e.ChangedButton switch
            {
                MouseButton.Left => "Left",
                MouseButton.Right => "Right",
                MouseButton.Middle => "Middle",
                _ => "Left"
            };

            int scaledX = (int)(pos.X * (imgDesktop.Source.Width / imgDesktop.ActualWidth));
            int scaledY = (int)(pos.Y * (imgDesktop.Source.Height / imgDesktop.ActualHeight));

            var message = new MouseInputMessage
            {
                X = scaledX,
                Y = scaledY,
                Button = button,
                Action = "Up"
            };

            await NetworkHelper.SendMessageAsync(client.Stream, message);
        }

        private async void imgDesktop_MouseMove(object sender, MouseEventArgs e)
        {
            if (!mouseControlEnabled || !isStreaming) return;

            var pos = e.GetPosition(imgDesktop);

            int scaledX = (int)(pos.X * (imgDesktop.Source.Width / imgDesktop.ActualWidth));
            int scaledY = (int)(pos.Y * (imgDesktop.Source.Height / imgDesktop.ActualHeight));

            var message = new MouseInputMessage
            {
                X = scaledX,
                Y = scaledY,
                Action = "Move"
            };

            await NetworkHelper.SendMessageAsync(client.Stream, message);
        }

        private async void imgDesktop_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!mouseControlEnabled || !isStreaming) return;

            var message = new MouseInputMessage
            {
                Delta = e.Delta,
                Action = "Wheel"
            };

            await NetworkHelper.SendMessageAsync(client.Stream, message);
        }

        // Keyboard Events - Only work when enabled
        private async void imgDesktop_KeyDown(object sender, KeyEventArgs e)
        {
            if (!keyboardControlEnabled || !isStreaming) return;

            var message = new KeyboardInputMessage
            {
                KeyCode = KeyInterop.VirtualKeyFromKey(e.Key),
                IsKeyDown = true,
                Shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift),
                Ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl),
                Alt = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)
            };

            await NetworkHelper.SendMessageAsync(client.Stream, message);
            e.Handled = true;
        }

        private async void imgDesktop_KeyUp(object sender, KeyEventArgs e)
        {
            if (!keyboardControlEnabled || !isStreaming) return;

            var message = new KeyboardInputMessage
            {
                KeyCode = KeyInterop.VirtualKeyFromKey(e.Key),
                IsKeyDown = false,
                Shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift),
                Ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl),
                Alt = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)
            };

            await NetworkHelper.SendMessageAsync(client.Stream, message);
            e.Handled = true;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (isStreaming)
            {
                StopRemoteDesktop();
            }
            client.RemoteDesktopWindow = null;
        }
    }
}