using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Server
{
    public partial class WebcamViewerWindow : Window
    {
        private ConnectedClient _client;
        private bool _isStreaming;
        private DispatcherTimer _fpsTimer;
        private int _frameCount;
        private int _lastFrameCount;
        private Stopwatch _fpsStopwatch;

        public WebcamViewerWindow(ConnectedClient client)
        {
            InitializeComponent();
            _client = client;
            _client.WebcamViewerWindow = this;

            // Setup FPS timer
            _fpsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _fpsTimer.Tick += FpsTimer_Tick;
            _fpsStopwatch = Stopwatch.StartNew();

            // Request webcam list from client
            RequestWebcamList();

            Closed += WebcamViewerWindow_Closed;
        }

        private async void RequestWebcamList()
        {
            try
            {
                if (_client?.Stream != null)
                {
                    await NetworkHelper.SendMessageAsync(_client.Stream, new GetWebcamListMessage());
                    StatusText.Text = "● Loading cameras...";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to request webcam list: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void UpdateWebcamList(WebcamInfo[] cameras)
        {
            CameraComboBox.Items.Clear();

            if (cameras == null || cameras.Length == 0)
            {
                StatusText.Text = "⚠ No cameras found";
                StatusText.Foreground = System.Windows.Media.Brushes.Orange;
                return;
            }

            foreach (var camera in cameras)
            {
                CameraComboBox.Items.Add($"[{camera.Index}] {camera.Name}");
            }

            if (CameraComboBox.Items.Count > 0)
            {
                CameraComboBox.SelectedIndex = 0;
                StatusText.Text = $"● Ready - {cameras.Length} camera(s) found";
                StatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_client?.Stream == null)
            {
                MessageBox.Show("Client is not connected.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (CameraComboBox.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a camera first.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Parse resolution
                var resTag = (ResolutionComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag.ToString();
                var resParts = resTag?.Split(',');
                int width = int.Parse(resParts[0]);
                int height = int.Parse(resParts[1]);

                // Parse quality
                var qualityTag = (QualityComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag.ToString();
                int quality = int.Parse(qualityTag);

                // Send start message
                var startMessage = new StartWebcamMessage
                {
                    CameraIndex = CameraComboBox.SelectedIndex,
                    Width = width,
                    Height = height,
                    Quality = quality
                };

                await NetworkHelper.SendMessageAsync(_client.Stream, startMessage);

                _isStreaming = true;
                _frameCount = 0;
                _lastFrameCount = 0;
                _fpsStopwatch.Restart();
                _fpsTimer.Start();

                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                CameraComboBox.IsEnabled = false;
                ResolutionComboBox.IsEnabled = false;
                QualityComboBox.IsEnabled = false;

                PlaceholderPanel.Visibility = Visibility.Collapsed;
                StatusText.Text = "● Streaming";
                StatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start webcam: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_client?.Stream == null)
                return;

            try
            {
                await NetworkHelper.SendMessageAsync(_client.Stream, new StopWebcamMessage());

                _isStreaming = false;
                _fpsTimer.Stop();

                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                CameraComboBox.IsEnabled = true;
                ResolutionComboBox.IsEnabled = true;
                QualityComboBox.IsEnabled = true;

                PlaceholderPanel.Visibility = Visibility.Visible;
                StatusText.Text = "● Stopped";
                StatusText.Foreground = System.Windows.Media.Brushes.Gray;
                ResolutionText.Text = "Resolution: --";
                FpsText.Text = "FPS: --";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to stop webcam: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void UpdateWebcamFrame(byte[] imageData, int width, int height, long frameNumber)
        {
            if (!_isStreaming)
                return;

            try
            {
                // Convert byte array to BitmapImage
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(imageData);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                WebcamImage.Source = bitmap;
                
                _frameCount++;
                FrameCountText.Text = $"Frames: {frameNumber}";
                ResolutionText.Text = $"Resolution: {width}x{height}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error displaying webcam frame: {ex.Message}");
            }
        }

        private void FpsTimer_Tick(object sender, EventArgs e)
        {
            int currentFps = _frameCount - _lastFrameCount;
            FpsText.Text = $"FPS: {currentFps}";
            _lastFrameCount = _frameCount;
        }

        private async void WebcamViewerWindow_Closed(object sender, EventArgs e)
        {
            if (_isStreaming && _client?.Stream != null)
            {
                try
                {
                    await NetworkHelper.SendMessageAsync(_client.Stream, new StopWebcamMessage());
                }
                catch { }
            }

            _fpsTimer?.Stop();
            
            if (_client != null)
                _client.WebcamViewerWindow = null;
        }
    }
}
