using System;
using System.Windows;
using System.Windows.Threading;
using NAudio.Wave;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Server
{
    public partial class AudioMonitorWindow : Window
    {
        private ConnectedClient _client;
        private BufferedWaveProvider _microphoneBuffer;
        private BufferedWaveProvider _systemAudioBuffer;
        private WaveOutEvent _microphoneOutput;
        private WaveOutEvent _systemAudioOutput;
        private bool _isMicStreaming;
        private bool _isSystemAudioStreaming;
        private int _packetsReceived;
        private DispatcherTimer _volumeMeterTimer;

        public AudioMonitorWindow(ConnectedClient client)
        {
            InitializeComponent();
            _client = client;
            _client.AudioMonitorWindow = this;

            // Setup volume meter update timer
            _volumeMeterTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _volumeMeterTimer.Tick += VolumeMeterTimer_Tick;
            _volumeMeterTimer.Start();

            Closed += AudioMonitorWindow_Closed;
        }

        private async void StartMicButton_Click(object sender, RoutedEventArgs e)
        {
            if (_client?.Stream == null)
            {
                MessageBox.Show("Client is not connected.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var startMessage = new StartAudioStreamMessage
                {
                    SourceType = AudioSourceType.Microphone,
                    SampleRate = 44100,
                    Channels = 2,
                    BitsPerSample = 16
                };

                await NetworkHelper.SendMessageAsync(_client.Stream, startMessage);

                // Initialize audio output
                var waveFormat = new WaveFormat(44100, 16, 2);
                _microphoneBuffer = new BufferedWaveProvider(waveFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(3),
                    DiscardOnBufferOverflow = true
                };

                _microphoneOutput = new WaveOutEvent();
                _microphoneOutput.Init(_microphoneBuffer);
                _microphoneOutput.Play();

                _isMicStreaming = true;
                StartMicButton.IsEnabled = false;
                StopMicButton.IsEnabled = true;

                MicStatusText.Text = "● Streaming";
                MicStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                MicInfoText.Text = "Sample Rate: 44100 Hz | Channels: Stereo";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start microphone: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StopMicButton_Click(object sender, RoutedEventArgs e)
        {
            if (_client?.Stream == null)
                return;

            try
            {
                var stopMessage = new StopAudioStreamMessage
                {
                    SourceType = AudioSourceType.Microphone
                };

                await NetworkHelper.SendMessageAsync(_client.Stream, stopMessage);

                StopMicrophone();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to stop microphone: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StartSystemAudioButton_Click(object sender, RoutedEventArgs e)
        {
            if (_client?.Stream == null)
            {
                MessageBox.Show("Client is not connected.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var startMessage = new StartAudioStreamMessage
                {
                    SourceType = AudioSourceType.SystemAudio,
                    SampleRate = 44100,
                    Channels = 2,
                    BitsPerSample = 16
                };

                await NetworkHelper.SendMessageAsync(_client.Stream, startMessage);

                // Note: System audio format will be determined by the client's actual audio format
                // This is just a placeholder, actual format will be set when first chunk arrives
                _isSystemAudioStreaming = true;
                StartSystemAudioButton.IsEnabled = false;
                StopSystemAudioButton.IsEnabled = true;

                SystemAudioStatusText.Text = "● Streaming";
                SystemAudioStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                SystemAudioInfoText.Text = "Waiting for audio data...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start system audio: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StopSystemAudioButton_Click(object sender, RoutedEventArgs e)
        {
            if (_client?.Stream == null)
                return;

            try
            {
                var stopMessage = new StopAudioStreamMessage
                {
                    SourceType = AudioSourceType.SystemAudio
                };

                await NetworkHelper.SendMessageAsync(_client.Stream, stopMessage);

                StopSystemAudio();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to stop system audio: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void PlayMicrophoneAudio(AudioChunkMessage audioChunk)
        {
            if (!_isMicStreaming)
                return;

            try
            {
                // Ensure buffer is initialized with correct format
                if (_microphoneBuffer == null)
                {
                    var waveFormat = new WaveFormat(audioChunk.SampleRate, audioChunk.BitsPerSample, audioChunk.Channels);
                    _microphoneBuffer = new BufferedWaveProvider(waveFormat)
                    {
                        BufferDuration = TimeSpan.FromSeconds(3),
                        DiscardOnBufferOverflow = true
                    };

                    _microphoneOutput = new WaveOutEvent();
                    _microphoneOutput.Init(_microphoneBuffer);
                    _microphoneOutput.Play();
                }

                _microphoneBuffer.AddSamples(audioChunk.AudioData, 0, audioChunk.AudioData.Length);
                _packetsReceived++;
                PacketsReceivedText.Text = $"Packets: {_packetsReceived}";

                // Update buffer status
                UpdateBufferStatus(_microphoneBuffer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing microphone audio: {ex.Message}");
            }
        }

        public void PlaySystemAudio(AudioChunkMessage audioChunk)
        {
            if (!_isSystemAudioStreaming)
                return;

            try
            {
                // Initialize buffer with actual format from client on first chunk
                if (_systemAudioBuffer == null)
                {
                    var waveFormat = new WaveFormat(audioChunk.SampleRate, audioChunk.BitsPerSample, audioChunk.Channels);
                    _systemAudioBuffer = new BufferedWaveProvider(waveFormat)
                    {
                        BufferDuration = TimeSpan.FromSeconds(3),
                        DiscardOnBufferOverflow = true
                    };

                    _systemAudioOutput = new WaveOutEvent();
                    _systemAudioOutput.Init(_systemAudioBuffer);
                    _systemAudioOutput.Play();

                    SystemAudioInfoText.Text = $"Sample Rate: {audioChunk.SampleRate} Hz | Channels: {(audioChunk.Channels == 2 ? "Stereo" : "Mono")}";
                }

                _systemAudioBuffer.AddSamples(audioChunk.AudioData, 0, audioChunk.AudioData.Length);
                _packetsReceived++;
                PacketsReceivedText.Text = $"Packets: {_packetsReceived}";

                // Update buffer status
                UpdateBufferStatus(_systemAudioBuffer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing system audio: {ex.Message}");
            }
        }

        private void UpdateBufferStatus(BufferedWaveProvider buffer)
        {
            if (buffer == null)
                return;

            var bufferedMs = buffer.BufferedDuration.TotalMilliseconds;
            
            if (bufferedMs < 100)
            {
                BufferStatusText.Text = "Buffer: Low";
                BufferStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
            else if (bufferedMs > 2000)
            {
                BufferStatusText.Text = "Buffer: High";
                BufferStatusText.Foreground = System.Windows.Media.Brushes.Orange;
            }
            else
            {
                BufferStatusText.Text = "Buffer: OK";
                BufferStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
        }

        private void VolumeMeterTimer_Tick(object sender, EventArgs e)
        {
            // Update microphone volume meter
            if (_microphoneBuffer != null && _isMicStreaming)
            {
                var level = CalculateBufferLevel(_microphoneBuffer);
                MicVolumeBar.Value = level;
            }
            else
            {
                MicVolumeBar.Value = 0;
            }

            // Update system audio volume meter
            if (_systemAudioBuffer != null && _isSystemAudioStreaming)
            {
                var level = CalculateBufferLevel(_systemAudioBuffer);
                SystemAudioVolumeBar.Value = level;
            }
            else
            {
                SystemAudioVolumeBar.Value = 0;
            }
        }

        private double CalculateBufferLevel(BufferedWaveProvider buffer)
        {
            if (buffer == null || buffer.BufferedBytes == 0)
                return 0;

            // Simple level calculation based on buffer fullness
            var percentage = (double)buffer.BufferedBytes / buffer.BufferLength * 100;
            return Math.Min(100, percentage * 2); // Multiply for better visual feedback
        }

        private void StopMicrophone()
        {
            _isMicStreaming = false;

            _microphoneOutput?.Stop();
            _microphoneOutput?.Dispose();
            _microphoneOutput = null;
            _microphoneBuffer = null;

            StartMicButton.IsEnabled = true;
            StopMicButton.IsEnabled = false;

            MicStatusText.Text = "● Stopped";
            MicStatusText.Foreground = System.Windows.Media.Brushes.Gray;
            MicInfoText.Text = "Sample Rate: -- | Channels: --";
            MicVolumeBar.Value = 0;
        }

        private void StopSystemAudio()
        {
            _isSystemAudioStreaming = false;

            _systemAudioOutput?.Stop();
            _systemAudioOutput?.Dispose();
            _systemAudioOutput = null;
            _systemAudioBuffer = null;

            StartSystemAudioButton.IsEnabled = true;
            StopSystemAudioButton.IsEnabled = false;

            SystemAudioStatusText.Text = "● Stopped";
            SystemAudioStatusText.Foreground = System.Windows.Media.Brushes.Gray;
            SystemAudioInfoText.Text = "Sample Rate: -- | Channels: --";
            SystemAudioVolumeBar.Value = 0;
        }

        private async void AudioMonitorWindow_Closed(object sender, EventArgs e)
        {
            _volumeMeterTimer?.Stop();

            // Stop both streams if active
            if (_client?.Stream != null)
            {
                try
                {
                    if (_isMicStreaming)
                    {
                        await NetworkHelper.SendMessageAsync(_client.Stream, 
                            new StopAudioStreamMessage { SourceType = AudioSourceType.Microphone });
                    }

                    if (_isSystemAudioStreaming)
                    {
                        await NetworkHelper.SendMessageAsync(_client.Stream, 
                            new StopAudioStreamMessage { SourceType = AudioSourceType.SystemAudio });
                    }
                }
                catch { }
            }

            StopMicrophone();
            StopSystemAudio();

            if (_client != null)
                _client.AudioMonitorWindow = null;
        }
    }
}
