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
        private readonly object _micLock = new object();
        private readonly object _systemLock = new object();

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
                // Request audio stream - client will respond with actual format
                var startMessage = new StartAudioStreamMessage
                {
                    SourceType = AudioSourceType.Microphone,
                    SampleRate = 44100,  // Preferred, but client may use different
                    Channels = 2,        // Preferred, but client may use different
                    BitsPerSample = 16
                };

                await NetworkHelper.SendMessageAsync(_client.Stream, startMessage);

                lock (_micLock)
                {
                    _isMicStreaming = true;
                }

                StartMicButton.IsEnabled = false;
                StopMicButton.IsEnabled = true;

                MicStatusText.Text = "● Starting...";
                MicStatusText.Foreground = System.Windows.Media.Brushes.Yellow;
                MicInfoText.Text = "Waiting for format info...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start microphone: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                lock (_micLock)
                {
                    _isMicStreaming = false;
                }
                StartMicButton.IsEnabled = true;
                StopMicButton.IsEnabled = false;
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
                    SampleRate = 48000,  // Common system default
                    Channels = 2,
                    BitsPerSample = 16
                };

                await NetworkHelper.SendMessageAsync(_client.Stream, startMessage);

                lock (_systemLock)
                {
                    _isSystemAudioStreaming = true;
                }

                StartSystemAudioButton.IsEnabled = false;
                StopSystemAudioButton.IsEnabled = true;

                SystemAudioStatusText.Text = "● Starting...";
                SystemAudioStatusText.Foreground = System.Windows.Media.Brushes.Yellow;
                SystemAudioInfoText.Text = "Waiting for format info...";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start system audio: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                lock (_systemLock)
                {
                    _isSystemAudioStreaming = false;
                }
                StartSystemAudioButton.IsEnabled = true;
                StopSystemAudioButton.IsEnabled = false;
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
            bool isStreaming;
            lock (_micLock)
            {
                isStreaming = _isMicStreaming;
            }

            if (!isStreaming)
                return;

            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Initialize buffer with actual format from client on first chunk
                    if (_microphoneBuffer == null)
                    {
                        var waveFormat = new WaveFormat(audioChunk.SampleRate, audioChunk.BitsPerSample, audioChunk.Channels);
                        _microphoneBuffer = new BufferedWaveProvider(waveFormat)
                        {
                            BufferDuration = TimeSpan.FromSeconds(3),
                            DiscardOnBufferOverflow = true
                        };

                        _microphoneOutput = new WaveOutEvent
                        {
                            DesiredLatency = 200  // 200ms latency for better stability
                        };
                        _microphoneOutput.Init(_microphoneBuffer);
                        _microphoneOutput.Play();

                        MicStatusText.Text = "● Streaming";
                        MicStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                        MicInfoText.Text = $"Sample Rate: {audioChunk.SampleRate} Hz | Channels: {(audioChunk.Channels == 2 ? "Stereo" : "Mono")} | {audioChunk.BitsPerSample}-bit";
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
            });
        }

        public void PlaySystemAudio(AudioChunkMessage audioChunk)
        {
            bool isStreaming;
            lock (_systemLock)
            {
                isStreaming = _isSystemAudioStreaming;
            }

            if (!isStreaming)
                return;

            Dispatcher.Invoke(() =>
            {
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

                        _systemAudioOutput = new WaveOutEvent
                        {
                            DesiredLatency = 200  // 200ms latency for better stability
                        };
                        _systemAudioOutput.Init(_systemAudioBuffer);
                        _systemAudioOutput.Play();

                        SystemAudioStatusText.Text = "● Streaming";
                        SystemAudioStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                        SystemAudioInfoText.Text = $"Sample Rate: {audioChunk.SampleRate} Hz | Channels: {(audioChunk.Channels == 2 ? "Stereo" : "Mono")} | {audioChunk.BitsPerSample}-bit";
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
            });
        }

        private void UpdateBufferStatus(BufferedWaveProvider buffer)
        {
            if (buffer == null)
                return;

            try
            {
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating buffer status: {ex.Message}");
            }
        }

        private void VolumeMeterTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // Update microphone volume meter
                lock (_micLock)
                {
                    if (_microphoneBuffer != null && _isMicStreaming)
                    {
                        var level = CalculateBufferLevel(_microphoneBuffer);
                        MicVolumeBar.Value = level;
                    }
                    else
                    {
                        MicVolumeBar.Value = 0;
                    }
                }

                // Update system audio volume meter
                lock (_systemLock)
                {
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating volume meters: {ex.Message}");
            }
        }

        private double CalculateBufferLevel(BufferedWaveProvider buffer)
        {
            if (buffer == null || buffer.BufferedBytes == 0)
                return 0;

            try
            {
                // Simple level calculation based on buffer fullness
                var percentage = (double)buffer.BufferedBytes / buffer.BufferLength * 100;
                return Math.Min(100, percentage * 2); // Multiply for better visual feedback
            }
            catch
            {
                return 0;
            }
        }

        private void StopMicrophone()
        {
            lock (_micLock)
            {
                _isMicStreaming = false;
            }

            Dispatcher.Invoke(() =>
            {
                try
                {
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
                catch (Exception ex)
                {
                    Console.WriteLine($"Error stopping microphone UI: {ex.Message}");
                }
            });
        }

        private void StopSystemAudio()
        {
            lock (_systemLock)
            {
                _isSystemAudioStreaming = false;
            }

            Dispatcher.Invoke(() =>
            {
                try
                {
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
                catch (Exception ex)
                {
                    Console.WriteLine($"Error stopping system audio UI: {ex.Message}");
                }
            });
        }

        private async void AudioMonitorWindow_Closed(object sender, EventArgs e)
        {
            _volumeMeterTimer?.Stop();

            // Stop both streams if active
            if (_client?.Stream != null)
            {
                try
                {
                    bool micActive, systemActive;

                    lock (_micLock)
                    {
                        micActive = _isMicStreaming;
                    }

                    lock (_systemLock)
                    {
                        systemActive = _isSystemAudioStreaming;
                    }

                    if (micActive)
                    {
                        await NetworkHelper.SendMessageAsync(_client.Stream,
                            new StopAudioStreamMessage { SourceType = AudioSourceType.Microphone });
                    }

                    if (systemActive)
                    {
                        await NetworkHelper.SendMessageAsync(_client.Stream,
                            new StopAudioStreamMessage { SourceType = AudioSourceType.SystemAudio });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during window close: {ex.Message}");
                }
            }

            StopMicrophone();
            StopSystemAudio();

            if (_client != null)
                _client.AudioMonitorWindow = null;
        }
    }
}