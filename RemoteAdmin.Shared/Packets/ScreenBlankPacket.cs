using System;

namespace RemoteAdmin.Shared.Packets
{
    [Serializable]
    public class ScreenBlankPacket
    {
        public bool EnableBlank { get; set; }

        public ScreenBlankPacket(bool enableBlank)
        {
            EnableBlank = enableBlank;
        }
    }

    [Serializable]
    public class ScreenBlankResponsePacket
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        public ScreenBlankResponsePacket(bool success, string message)
        {
            Success = success;
            Message = message;
        }
    }
}