using System;
using System.ComponentModel;
using System.Net.Sockets;

namespace RemoteAdmin.Server
{
    public class ConnectedClient : INotifyPropertyChanged
    {
        private string _status;
        private DateTime _lastSeen;

        public string Id { get; set; }
        public string ComputerName { get; set; }
        public string Username { get; set; }
        public string OSVersion { get; set; }
        public string IPAddress { get; set; }
        public string PublicIP { get; set; }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public DateTime LastSeen
        {
            get => _lastSeen;
            set
            {
                _lastSeen = value;
                OnPropertyChanged(nameof(LastSeen));
                OnPropertyChanged(nameof(LastSeenFormatted));
            }
        }

        public string LastSeenFormatted => LastSeen.ToString("HH:mm:ss");

        public TcpClient Connection { get; set; }
        public NetworkStream Stream { get; set; }

        public ShellWindow ShellWindow { get; set; }
        public TaskManagerWindow TaskManagerWindow { get; set; }
        public FileManagerWindow FileManagerWindow { get; set; }

        public RemoteDesktopWindow RemoteDesktopWindow { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}