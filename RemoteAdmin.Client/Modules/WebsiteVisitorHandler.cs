using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Threading.Tasks;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Client.Networking
{
    internal class WebsiteVisitorHandler
    {
        public static async Task HandleVisitWebsite(SslStream stream, VisitWebsiteMessage message)
        {
            try
            {
                string url = message.Url;

                // Ensure URL has protocol
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "http://" + url;
                }

                // Validate URL
                if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    var errorResponse = new WebsiteVisitResultMessage
                    {
                        Success = false,
                        Message = "Invalid URL format",
                        Url = message.Url
                    };
                    await NetworkHelper.SendMessageAsync(stream, errorResponse);
                    return;
                }

                if (message.Hidden)
                {
                    // Silent visit - just make HTTP request without opening browser
                    await VisitWebsiteHidden(url);
                }
                else
                {
                    // Open in default browser
                    VisitWebsiteVisible(url);
                }

                var successResponse = new WebsiteVisitResultMessage
                {
                    Success = true,
                    Message = message.Hidden ? "Website visited silently" : "Website opened in browser",
                    Url = url
                };
                await NetworkHelper.SendMessageAsync(stream, successResponse);

                Console.WriteLine($"Visited website: {url} (Hidden: {message.Hidden})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error visiting website: {ex.Message}");

                var errorResponse = new WebsiteVisitResultMessage
                {
                    Success = false,
                    Message = $"Error: {ex.Message}",
                    Url = message.Url
                };
                await NetworkHelper.SendMessageAsync(stream, errorResponse);
            }
        }

        private static void VisitWebsiteVisible(string url)
        {
            try
            {
                // Open URL in default browser
                var psi = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to open browser: {ex.Message}", ex);
            }
        }

        private static async Task VisitWebsiteHidden(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
                request.AllowAutoRedirect = true;
                request.Timeout = 10000; // 10 seconds
                request.Method = "GET";

                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                {
                    // Request completed successfully
                    Console.WriteLine($"Hidden visit completed. Status: {response.StatusCode}");
                }
            }
            catch (WebException ex)
            {
                throw new Exception($"HTTP request failed: {ex.Message}", ex);
            }
        }
    }
}