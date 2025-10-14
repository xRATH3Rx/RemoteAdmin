using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Server
{
    public partial class FileManagerWindow : Window
    {
        private ConnectedClient client;
        private ObservableCollection<FileSystemItemViewModel> folders;
        private ObservableCollection<FileSystemItemViewModel> files;
        private string currentPath = "C:\\";
        private Dictionary<string, FileTransfer> activeTransfers;

        public FileManagerWindow(ConnectedClient client)
        {
            InitializeComponent();
            this.client = client;

            folders = new ObservableCollection<FileSystemItemViewModel>();
            files = new ObservableCollection<FileSystemItemViewModel>();
            activeTransfers = new Dictionary<string, FileTransfer>();

            lstFolders.ItemsSource = folders;
            gridFiles.ItemsSource = files;

            txtClientInfo.Text = $"File Manager: {client.ComputerName}";
            client.FileManagerWindow = this;

            this.Loaded += async (s, e) =>
            {
                await LoadDirectory(currentPath);
            };
        }

        private async System.Threading.Tasks.Task LoadDirectory(string path)
        {
            try
            {
                UpdateStatus("Loading...");
                currentPath = path;
                txtPath.Text = path;

                folders.Clear();
                files.Clear();

                var request = new DirectoryListRequestMessage { Path = path };
                await NetworkHelper.SendMessageAsync(client.Stream, request);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                MessageBox.Show($"Error loading directory: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void UpdateDirectoryListing(DirectoryListResponseMessage response)
        {
            Dispatcher.Invoke(() =>
            {
                folders.Clear();
                files.Clear();

                if (!response.Success)
                {
                    UpdateStatus($"Error: {response.Error}");
                    MessageBox.Show($"Error loading directory: {response.Error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                foreach (var item in response.Items.Where(i => i.IsDirectory).OrderBy(i => i.Name))
                {
                    folders.Add(new FileSystemItemViewModel(item));
                }

                foreach (var item in response.Items.Where(i => !i.IsDirectory).OrderBy(i => i.Name))
                {
                    files.Add(new FileSystemItemViewModel(item));
                }

                UpdateStatus($"{folders.Count} folders, {files.Count} files");
            });
        }

        public void HandleFileChunk(FileChunkMessage chunk)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (!activeTransfers.ContainsKey(chunk.TransferId))
                    {
                        // Start new download
                        var saveDialog = new SaveFileDialog
                        {
                            FileName = chunk.FileName,
                            Title = "Save Downloaded File"
                        };

                        if (saveDialog.ShowDialog() != true)
                        {
                            UpdateStatus("Download cancelled");
                            return;
                        }

                        activeTransfers[chunk.TransferId] = new FileTransfer
                        {
                            FilePath = saveDialog.FileName,
                            TotalSize = chunk.FileSize,
                            BytesTransferred = 0
                        };
                    }

                    var transfer = activeTransfers[chunk.TransferId];

                    // Write chunk to file
                    FileSplitHelper.WriteFileChunk(transfer.FilePath, new FileChunk
                    {
                        Data = chunk.Data,
                        Offset = chunk.Offset
                    });

                    transfer.BytesTransferred += chunk.Data.Length;

                    // Update progress
                    int progress = (int)((transfer.BytesTransferred * 100) / transfer.TotalSize);
                    UpdateStatus($"Downloading: {progress}% ({FormatBytes(transfer.BytesTransferred)} / {FormatBytes(transfer.TotalSize)})");

                    if (chunk.IsLastChunk)
                    {
                        activeTransfers.Remove(chunk.TransferId);
                        UpdateStatus("Download complete!");
                        MessageBox.Show($"File downloaded successfully to:\n{transfer.FilePath}", "Download Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error: {ex.Message}");
                    MessageBox.Show($"Error downloading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        private void UpdateStatus(string status)
        {
            txtStatus.Text = status;
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadDirectory(currentPath);
        }

        private async void btnUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var parent = Directory.GetParent(currentPath);
                if (parent != null)
                {
                    await LoadDirectory(parent.FullName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error navigating up: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void txtPath_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await LoadDirectory(txtPath.Text);
            }
        }

        private async void lstFolders_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstFolders.SelectedItem is FileSystemItemViewModel folder)
            {
                await LoadDirectory(folder.FullPath);
            }
        }

        private async void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (lstFolders.SelectedItem is FileSystemItemViewModel folder)
            {
                await LoadDirectory(folder.FullPath);
            }
        }

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            if (gridFiles.SelectedItem is FileSystemItemViewModel file)
            {
                try
                {
                    string transferId = Guid.NewGuid().ToString();

                    var request = new DownloadFileRequestMessage
                    {
                        FilePath = file.FullPath,
                        TransferId = transferId
                    };

                    await NetworkHelper.SendMessageAsync(client.Stream, request);
                    UpdateStatus("Requesting download...");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error requesting download: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a file to download.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void btnUpload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openDialog = new OpenFileDialog
                {
                    Title = "Select File to Upload"
                };

                if (openDialog.ShowDialog() == true)
                {
                    string transferId = Guid.NewGuid().ToString();
                    string fileName = Path.GetFileName(openDialog.FileName);
                    long fileSize = new FileInfo(openDialog.FileName).Length;

                    // Send upload start message
                    var startMsg = new UploadFileStartMessage
                    {
                        TransferId = transferId,
                        DestinationPath = currentPath,
                        FileName = fileName,
                        FileSize = fileSize
                    };

                    await NetworkHelper.SendMessageAsync(client.Stream, startMsg);

                    // Send file chunks
                    int chunkNumber = 0;
                    long totalChunks = (fileSize / FileSplitHelper.MaxChunkSize) + 1;

                    foreach (var chunk in FileSplitHelper.ReadFileChunks(openDialog.FileName))
                    {
                        chunkNumber++;
                        bool isLast = chunkNumber >= totalChunks;

                        var chunkMsg = new FileChunkMessage
                        {
                            TransferId = transferId,
                            FileName = fileName,
                            FileSize = fileSize,
                            Offset = chunk.Offset,
                            Data = chunk.Data,
                            IsLastChunk = isLast
                        };

                        await NetworkHelper.SendMessageAsync(client.Stream, chunkMsg);

                        int progress = (int)((chunkNumber * 100) / totalChunks);
                        UpdateStatus($"Uploading: {progress}%");
                    }

                    UpdateStatus("Upload complete!");
                    MessageBox.Show("File uploaded successfully!", "Upload Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Refresh directory
                    await LoadDirectory(currentPath);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                MessageBox.Show($"Error uploading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnNewFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("Create New Folder", "Folder name:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
            {
                try
                {
                    var msg = new CreateDirectoryMessage
                    {
                        Path = Path.Combine(currentPath, dialog.ResponseText)
                    };

                    await NetworkHelper.SendMessageAsync(client.Stream, msg);
                    UpdateStatus("Creating folder...");

                    // Refresh after a moment
                    await System.Threading.Tasks.Task.Delay(500);
                    await LoadDirectory(currentPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error creating folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            FileSystemItemViewModel selectedItem = null;

            if (lstFolders.SelectedItem is FileSystemItemViewModel folder)
                selectedItem = folder;
            else if (gridFiles.SelectedItem is FileSystemItemViewModel file)
                selectedItem = file;

            if (selectedItem != null)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete '{selectedItem.Name}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var msg = new DeleteFileMessage
                        {
                            Path = selectedItem.FullPath,
                            IsDirectory = selectedItem.IsDirectory
                        };

                        await NetworkHelper.SendMessageAsync(client.Stream, msg);
                        UpdateStatus("Deleting...");

                        // Refresh after a moment
                        await System.Threading.Tasks.Task.Delay(500);
                        await LoadDirectory(currentPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a file or folder to delete.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            FileSystemItemViewModel selectedItem = null;

            if (lstFolders.SelectedItem is FileSystemItemViewModel folder)
                selectedItem = folder;
            else if (gridFiles.SelectedItem is FileSystemItemViewModel file)
                selectedItem = file;

            if (selectedItem != null)
            {
                var dialog = new InputDialog("Rename", "New name:", selectedItem.Name);
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResponseText))
                {
                    try
                    {
                        var msg = new RenameFileMessage
                        {
                            OldPath = selectedItem.FullPath,
                            NewName = dialog.ResponseText
                        };

                        await NetworkHelper.SendMessageAsync(client.Stream, msg);
                        UpdateStatus("Renaming...");

                        // Refresh after a moment
                        await System.Threading.Tasks.Task.Delay(500);
                        await LoadDirectory(currentPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error renaming: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            client.FileManagerWindow = null;
        }
    }

    // View model for file system items
    public class FileSystemItemViewModel
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }

        public string SizeFormatted => IsDirectory ? "<DIR>" : FormatBytes(Size);
        public string LastModifiedFormatted => LastModified.ToString("yyyy-MM-dd HH:mm");

        public FileSystemItemViewModel(FileSystemItem item)
        {
            Name = item.Name;
            FullPath = item.FullPath;
            IsDirectory = item.IsDirectory;
            Size = item.Size;
            LastModified = item.LastModified;
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }

    // Helper class for tracking file transfers
    public class FileTransfer
    {
        public string FilePath { get; set; }
        public long TotalSize { get; set; }
        public long BytesTransferred { get; set; }
    }
}