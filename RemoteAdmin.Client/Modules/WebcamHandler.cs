using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AForge.Video;
using AForge.Video.DirectShow;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Client.Handlers
{
    internal class WebcamHandler
    {
        private static VideoCaptureDevice _videoDevice;
        private static SslStream _stream;
        private static bool _isStreaming;
        private static long _frameNumber;
        private static int _quality = 75;

        public static async Task HandleGetWebcamList(SslStream stream)
        {
            try
            {
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                var cameras = new WebcamInfo[videoDevices.Count];

                for (int i = 0; i < videoDevices.Count; i++)
                {
                    cameras[i] = new WebcamInfo
                    {
                        Index = i,
                        Name = videoDevices[i].Name
                    };
                }

                await NetworkHelper.SendMessageAsync(stream, new WebcamListMessage
                {
                    Cameras = cameras
                });

                Console.WriteLine($"Sent list of {cameras.Length} webcams");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting webcam list: {ex.Message}");
            }
        }

        public static async Task HandleStartWebcam(SslStream stream, StartWebcamMessage msg)
        {
            if (_isStreaming)
                return;

            _stream = stream;
            _quality = msg.Quality;
            _frameNumber = 0;

            try
            {
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                
                if (videoDevices.Count == 0)
                {
                    await NetworkHelper.SendMessageAsync(stream, new OperationResultMessage
                    {
                        Success = false,
                        Message = "No webcam devices found"
                    });
                    return;
                }

                int cameraIndex = msg.CameraIndex;
                if (cameraIndex >= videoDevices.Count)
                    cameraIndex = 0;

                _videoDevice = new VideoCaptureDevice(videoDevices[cameraIndex].MonikerString);

                // Try to set desired resolution
                var capabilities = _videoDevice.VideoCapabilities;
                var selectedCapability = capabilities
                    .OrderBy(c => Math.Abs(c.FrameSize.Width - msg.Width) + Math.Abs(c.FrameSize.Height - msg.Height))
                    .FirstOrDefault();

                if (selectedCapability != null)
                    _videoDevice.VideoResolution = selectedCapability;

                _videoDevice.NewFrame += VideoDevice_NewFrame;
                _videoDevice.Start();
                _isStreaming = true;

                Console.WriteLine($"Started webcam streaming: {videoDevices[cameraIndex].Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting webcam: {ex.Message}");
                await NetworkHelper.SendMessageAsync(stream, new OperationResultMessage
                {
                    Success = false,
                    Message = $"Failed to start webcam: {ex.Message}"
                });
            }
        }

        private static async void VideoDevice_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            if (!_isStreaming || _stream == null)
                return;

            try
            {
                using (var bitmap = (Bitmap)eventArgs.Frame.Clone())
                {
                    byte[] imageData = BitmapToJpeg(bitmap, _quality);

                    var frameMessage = new WebcamFrameMessage
                    {
                        ImageData = imageData,
                        Width = bitmap.Width,
                        Height = bitmap.Height,
                        FrameNumber = Interlocked.Increment(ref _frameNumber)
                    };

                    await NetworkHelper.SendMessageAsync(_stream, frameMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending webcam frame: {ex.Message}");
            }
        }

        private static byte[] BitmapToJpeg(Bitmap bitmap, int quality)
        {
            using (var ms = new MemoryStream())
            {
                var encoderParameters = new EncoderParameters(1);
                encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                
                var jpegCodec = ImageCodecInfo.GetImageEncoders()
                    .First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);

                bitmap.Save(ms, jpegCodec, encoderParameters);
                return ms.ToArray();
            }
        }

        public static void HandleStopWebcam()
        {
            _isStreaming = false;

            if (_videoDevice != null && _videoDevice.IsRunning)
            {
                _videoDevice.SignalToStop();
                _videoDevice.WaitForStop();
                _videoDevice.NewFrame -= VideoDevice_NewFrame;
                _videoDevice = null;
            }

            Console.WriteLine("Stopped webcam streaming");
        }
    }
}
