using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
        private Dictionary<string, FolderTransfer> activeFolderTransfers;
        private string downloadsBasePath;

        public FileManagerWindow(ConnectedClient client)
        {
            InitializeComponent();
            this.client = client;

            folders = new ObservableCollection<FileSystemItemViewModel>();
            files = new ObservableCollection<FileSystemItemViewModel>();
            activeTransfers = new Dictionary<string, FileTransfer>();
            activeFolderTransfers = new Dictionary<string, FolderTransfer>();

            // Create downloads directory structure: Downloads/[ComputerName]/
            downloadsBasePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Downloads",
                SanitizeFileName(client.ComputerName)
            );

            try
            {
                Directory.CreateDirectory(downloadsBasePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to create downloads directory: {ex.Message}\nDownloads will be prompted for location.",
                    "Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                downloadsBasePath = null;
            }

            lstFolders.ItemsSource = folders;
            gridFiles.ItemsSource = files;

            txtClientInfo.Text = $"File Manager: {client.ComputerName}";
            client.FileManagerWindow = this;

            this.Loaded += async (s, e) =>
            {
                await LoadDirectory(currentPath);
            };
        }

        private async Task LoadDirectory(string path)
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
                MessageBox.Show(
                    $"Error loading directory: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        public void UpdateDirectoryListing(DirectoryListResponseMessage response)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    folders.Clear();
                    files.Clear();

                    if (!response.Success)
                    {
                        UpdateStatus($"Error: {response.Error ?? "Unknown error"}");
                        MessageBox.Show(
                            $"Error loading directory: {response.Error ?? "Unknown error"}",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
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
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error: {ex.Message}");
                    MessageBox.Show(
                        $"Error updating directory listing: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
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
                        // Start new transfer - this happens on first chunk
                        string localPath = downloadsBasePath != null
                            ? Path.Combine(downloadsBasePath, chunk.FileName)
                            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), chunk.FileName);

                        activeTransfers[chunk.TransferId] = new FileTransfer
                        {
                            FilePath = localPath,
                            FileName = chunk.FileName,
                            TotalSize = chunk.FileSize,
                            BytesTransferred = 0
                        };
                    }

                    var transfer = activeTransfers[chunk.TransferId];

                    // Update total size if not set yet
                    if (transfer.TotalSize == 0)
                    {
                        transfer.TotalSize = chunk.FileSize;
                    }

                    // Write chunk to file
                    FileSplitHelper.WriteFileChunk(transfer.FilePath, new FileChunk
                    {
                        Data = chunk.Data,
                        Offset = chunk.Offset
                    });

                    transfer.BytesTransferred += chunk.Data.Length;

                    // Update progress (with divide by zero protection)
                    if (transfer.TotalSize > 0)
                    {
                        int progress = (int)((transfer.BytesTransferred * 100) / transfer.TotalSize);
                        UpdateStatus($"Downloading {transfer.FileName}: {progress}% ({FormatBytes(transfer.BytesTransferred)} / {FormatBytes(transfer.TotalSize)})");
                    }
                    else
                    {
                        UpdateStatus($"Downloading {transfer.FileName}: {FormatBytes(transfer.BytesTransferred)}");
                    }

                    if (chunk.IsLastChunk)
                    {
                        activeTransfers.Remove(chunk.TransferId);

                        // Check if all transfers are complete
                        if (activeTransfers.Count == 0 && activeFolderTransfers.Count == 0)
                        {
                            UpdateStatus("All downloads complete!");
                            MessageBox.Show(
                                $"Download(s) completed successfully!\nLocation: {downloadsBasePath}",
                                "Download Complete",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information
                            );
                        }
                        else
                        {
                            int remaining = activeTransfers.Count + activeFolderTransfers.Count;
                            UpdateStatus($"Downloaded {transfer.FileName}. {remaining} remaining...");
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error: {ex.Message}");
                    MessageBox.Show(
                        $"Error downloading file: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            });
        }

        public void HandleFolderStructure(FolderStructureMessage folderStructure)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // Initialize folder transfer tracking
                    activeFolderTransfers[folderStructure.TransferId] = new FolderTransfer
                    {
                        FolderName = folderStructure.FolderName,
                        TotalFiles = folderStructure.TotalFiles,
                        CompletedFiles = 0,
                        Files = folderStructure.Files
                    };

                    UpdateStatus($"Downloading folder '{folderStructure.FolderName}': 0/{folderStructure.TotalFiles} files");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error: {ex.Message}");
                    MessageBox.Show(
                        $"Error initializing folder download: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            });
        }

        public void HandleFolderFileChunk(FolderFileChunkMessage chunk)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    if (!activeFolderTransfers.TryGetValue(chunk.TransferId, out var folderTransfer))
                    {
                        UpdateStatus($"Warning: Unknown folder transfer ID: {chunk.TransferId}");
                        return;
                    }

                    // Create unique transfer ID for this file within the folder
                    string fileTransferId = $"{chunk.TransferId}_{chunk.FileIndex}";

                    if (!activeTransfers.ContainsKey(fileTransferId))
                    {
                        // Create the file path preserving folder structure
                        string localFolderPath = Path.Combine(downloadsBasePath, folderTransfer.FolderName);
                        string fileDirectory = Path.GetDirectoryName(Path.Combine(localFolderPath, chunk.RelativePath));

                        // Ensure directory exists
                        Directory.CreateDirectory(fileDirectory);

                        string localFilePath = Path.Combine(localFolderPath, chunk.RelativePath);

                        activeTransfers[fileTransferId] = new FileTransfer
                        {
                            FilePath = localFilePath,
                            FileName = chunk.FileName,
                            TotalSize = chunk.FileSize,
                            BytesTransferred = 0
                        };
                    }

                    var transfer = activeTransfers[fileTransferId];

                    // Update total size if needed
                    if (transfer.TotalSize == 0)
                    {
                        transfer.TotalSize = chunk.FileSize;
                    }

                    // Write chunk to file
                    FileSplitHelper.WriteFileChunk(transfer.FilePath, new FileChunk
                    {
                        Data = chunk.Data,
                        Offset = chunk.Offset
                    });

                    transfer.BytesTransferred += chunk.Data.Length;

                    if (chunk.IsLastChunk)
                    {
                        activeTransfers.Remove(fileTransferId);
                        folderTransfer.CompletedFiles++;

                        // Update progress
                        int folderProgress = (folderTransfer.CompletedFiles * 100) / folderTransfer.TotalFiles;
                        UpdateStatus($"Downloading folder '{folderTransfer.FolderName}': {folderTransfer.CompletedFiles}/{folderTransfer.TotalFiles} files ({folderProgress}%)");

                        // Check if folder download is complete
                        if (folderTransfer.CompletedFiles >= folderTransfer.TotalFiles)
                        {
                            activeFolderTransfers.Remove(chunk.TransferId);

                            if (activeTransfers.Count == 0 && activeFolderTransfers.Count == 0)
                            {
                                UpdateStatus("All downloads complete!");
                                MessageBox.Show(
                                    $"Download(s) completed successfully!\nLocation: {downloadsBasePath}",
                                    "Download Complete",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information
                                );
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error: {ex.Message}");
                    MessageBox.Show(
                        $"Error downloading folder file: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
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
                else
                {
                    UpdateStatus("Already at root directory");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                MessageBox.Show(
                    $"Error navigating up: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
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
            try
            {
                // Get all selected items from both folders and files
                var selectedFolders = lstFolders.SelectedItems.Cast<FileSystemItemViewModel>().ToList();
                var selectedFiles = gridFiles.SelectedItems.Cast<FileSystemItemViewModel>().ToList();

                if (selectedFolders.Count == 0 && selectedFiles.Count == 0)
                {
                    MessageBox.Show(
                        "Please select one or more files or folders to download.",
                        "No Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    return;
                }

                // Warn about folder downloads
                if (selectedFolders.Count > 0)
                {
                    var result = MessageBox.Show(
                        $"You are about to download {selectedFolders.Count} folder(s) and {selectedFiles.Count} file(s).\n\n" +
                        "Folders will be downloaded recursively with all their contents.\n" +
                        "This may take some time depending on the size.\n\n" +
                        "Continue?",
                        "Confirm Download",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );

                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                UpdateStatus("Preparing download...");

                // Download all selected folders
                foreach (var folder in selectedFolders)
                {
                    await DownloadFolder(folder.FullPath, folder.Name);
                }

                // Download all selected files
                foreach (var file in selectedFiles)
                {
                    await DownloadFile(file.FullPath, file.Name);
                }

                if (activeTransfers.Count == 0 && activeFolderTransfers.Count == 0)
                {
                    UpdateStatus("Ready");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                MessageBox.Show(
                    $"Error initiating download: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async Task DownloadFolder(string remoteFolderPath, string folderName)
        {
            try
            {
                string transferId = Guid.NewGuid().ToString();

                UpdateStatus($"Requesting folder: {folderName}...");

                var request = new DownloadFolderRequestMessage
                {
                    FolderPath = remoteFolderPath,
                    TransferId = transferId
                };

                await NetworkHelper.SendMessageAsync(client.Stream, request);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                MessageBox.Show(
                    $"Error downloading folder '{folderName}': {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async Task DownloadFile(string remoteFilePath, string fileName)
        {
            try
            {
                string transferId = Guid.NewGuid().ToString();

                // Register the transfer (will be updated with actual size from first chunk)
                activeTransfers[transferId] = new FileTransfer
                {
                    FilePath = Path.Combine(downloadsBasePath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName),
                    FileName = fileName,
                    TotalSize = 0, // Will be updated from first chunk
                    BytesTransferred = 0
                };

                var request = new DownloadFileRequestMessage
                {
                    FilePath = remoteFilePath,
                    TransferId = transferId
                };

                await NetworkHelper.SendMessageAsync(client.Stream, request);
                UpdateStatus($"Downloading {fileName}...");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                MessageBox.Show(
                    $"Error downloading file '{fileName}': {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async void btnUpload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openDialog = new OpenFileDialog
                {
                    Title = "Select File to Upload",
                    Multiselect = false
                };

                if (openDialog.ShowDialog() != true)
                {
                    return;
                }

                string transferId = Guid.NewGuid().ToString();
                string fileName = Path.GetFileName(openDialog.FileName);
                long fileSize = new FileInfo(openDialog.FileName).Length;

                UpdateStatus("Uploading...");

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
                MessageBox.Show(
                    "File uploaded successfully!",
                    "Upload Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                // Refresh directory
                await LoadDirectory(currentPath);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                MessageBox.Show(
                    $"Error uploading file: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
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
                    await Task.Delay(500);
                    await LoadDirectory(currentPath);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error: {ex.Message}");
                    MessageBox.Show(
                        $"Error creating folder: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedFolders = lstFolders.SelectedItems.Cast<FileSystemItemViewModel>().ToList();
                var selectedFiles = gridFiles.SelectedItems.Cast<FileSystemItemViewModel>().ToList();

                if (selectedFolders.Count == 0 && selectedFiles.Count == 0)
                {
                    MessageBox.Show(
                        "Please select one or more files or folders to delete.",
                        "No Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    return;
                }

                var totalCount = selectedFolders.Count + selectedFiles.Count;
                var result = MessageBox.Show(
                    $"Are you sure you want to delete {totalCount} item(s)?\n\nThis action cannot be undone!",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                UpdateStatus($"Deleting {totalCount} item(s)...");

                // Delete all selected folders
                foreach (var folder in selectedFolders)
                {
                    try
                    {
                        var msg = new DeleteFileMessage
                        {
                            Path = folder.FullPath,
                            IsDirectory = true
                        };
                        await NetworkHelper.SendMessageAsync(client.Stream, msg);
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Error deleting folder '{folder.Name}': {ex.Message}",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    }
                }

                // Delete all selected files
                foreach (var file in selectedFiles)
                {
                    try
                    {
                        var msg = new DeleteFileMessage
                        {
                            Path = file.FullPath,
                            IsDirectory = false
                        };
                        await NetworkHelper.SendMessageAsync(client.Stream, msg);
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"Error deleting file '{file.Name}': {ex.Message}",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    }
                }

                // Refresh after deletion
                await Task.Delay(500);
                await LoadDirectory(currentPath);
                UpdateStatus("Deletion complete");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                MessageBox.Show(
                    $"Error during deletion: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileSystemItemViewModel selectedItem = null;

                if (lstFolders.SelectedItems.Count == 1)
                    selectedItem = lstFolders.SelectedItem as FileSystemItemViewModel;
                else if (gridFiles.SelectedItems.Count == 1)
                    selectedItem = gridFiles.SelectedItem as FileSystemItemViewModel;

                if (selectedItem == null)
                {
                    MessageBox.Show(
                        "Please select exactly one file or folder to rename.",
                        "Invalid Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    return;
                }

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

                        await Task.Delay(500);
                        await LoadDirectory(currentPath);
                        UpdateStatus("Rename complete");
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"Error: {ex.Message}");
                        MessageBox.Show(
                            $"Error renaming: {ex.Message}",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                MessageBox.Show(
                    $"Error during rename: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private string SanitizeFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            string sanitized = new string(fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
            return sanitized;
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
        public string FileName { get; set; }
        public long TotalSize { get; set; }
        public long BytesTransferred { get; set; }
    }

    // Helper class for tracking folder transfers
    public class FolderTransfer
    {
        public string FolderName { get; set; }
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public List<FolderFileInfo> Files { get; set; }
    }
}