using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Threading.Tasks;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Client.Modules
{
    internal class FileSystemHandler
    {
        public static Dictionary<string, string> activeUploads = new Dictionary<string, string>();

        public static async Task HandleDirectoryListRequest(SslStream stream, DirectoryListRequestMessage request)
        {
            try
            {
                var items = new List<FileSystemItem>();

                if (!Directory.Exists(request.Path))
                {
                    var response = new DirectoryListResponseMessage
                    {
                        Path = request.Path,
                        Items = new List<FileSystemItem>(),
                        Success = false,
                        Error = "Directory does not exist"
                    };
                    await NetworkHelper.SendMessageAsync(stream, response);
                    return;
                }

                var dirInfo = new DirectoryInfo(request.Path);

                // Get directories
                try
                {
                    foreach (var dir in dirInfo.GetDirectories())
                    {
                        try
                        {
                            items.Add(new FileSystemItem
                            {
                                Name = dir.Name,
                                FullPath = dir.FullName,
                                IsDirectory = true,
                                Size = 0,
                                LastModified = dir.LastWriteTime
                            });
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Skip directories we don't have access to
                            Console.WriteLine($"Access denied to directory: {dir.FullName}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error accessing directory {dir.Name}: {ex.Message}");
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    var response = new DirectoryListResponseMessage
                    {
                        Path = request.Path,
                        Items = new List<FileSystemItem>(),
                        Success = false,
                        Error = $"Access denied: {ex.Message}"
                    };
                    await NetworkHelper.SendMessageAsync(stream, response);
                    return;
                }

                // Get files
                try
                {
                    foreach (var file in dirInfo.GetFiles())
                    {
                        try
                        {
                            items.Add(new FileSystemItem
                            {
                                Name = file.Name,
                                FullPath = file.FullName,
                                IsDirectory = false,
                                Size = file.Length,
                                LastModified = file.LastWriteTime
                            });
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Skip files we don't have access to
                            Console.WriteLine($"Access denied to file: {file.FullName}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error accessing file {file.Name}: {ex.Message}");
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    var response = new DirectoryListResponseMessage
                    {
                        Path = request.Path,
                        Items = items, // Return what we got so far
                        Success = false,
                        Error = $"Access denied to some files: {ex.Message}"
                    };
                    await NetworkHelper.SendMessageAsync(stream, response);
                    return;
                }

                var successResponse = new DirectoryListResponseMessage
                {
                    Path = request.Path,
                    Items = items,
                    Success = true
                };

                await NetworkHelper.SendMessageAsync(stream, successResponse);
                Console.WriteLine($"Sent {items.Count} items for directory: {request.Path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing directory: {ex.Message}");

                var response = new DirectoryListResponseMessage
                {
                    Path = request.Path,
                    Items = new List<FileSystemItem>(),
                    Success = false,
                    Error = ex.Message
                };

                try
                {
                    await NetworkHelper.SendMessageAsync(stream, response);
                }
                catch (Exception sendEx)
                {
                    Console.WriteLine($"Error sending error response: {sendEx.Message}");
                }
            }
        }

        public static async Task HandleDownloadRequest(SslStream stream, DownloadFileRequestMessage request)
        {
            try
            {
                if (!File.Exists(request.FilePath))
                {
                    Console.WriteLine($"File not found: {request.FilePath}");
                    var errorMsg = new OperationResultMessage
                    {
                        Success = false,
                        Message = "File not found"
                    };
                    await NetworkHelper.SendMessageAsync(stream, errorMsg);
                    return;
                }

                var fileInfo = new FileInfo(request.FilePath);
                int chunkNumber = 0;
                long totalChunks = (fileInfo.Length / FileSplitHelper.MaxChunkSize) + 1;

                Console.WriteLine($"Starting download: {request.FilePath} ({fileInfo.Length} bytes, {totalChunks} chunks)");

                foreach (var chunk in FileSplitHelper.ReadFileChunks(request.FilePath))
                {
                    chunkNumber++;
                    bool isLast = chunkNumber >= totalChunks;

                    var chunkMsg = new FileChunkMessage
                    {
                        TransferId = request.TransferId,
                        FileName = fileInfo.Name,
                        FileSize = fileInfo.Length,
                        Offset = chunk.Offset,
                        Data = chunk.Data,
                        IsLastChunk = isLast
                    };

                    await NetworkHelper.SendMessageAsync(stream, chunkMsg);

                    if (chunkNumber % 10 == 0 || isLast)
                    {
                        Console.WriteLine($"Sent chunk {chunkNumber}/{totalChunks} for {fileInfo.Name}");
                    }
                }

                Console.WriteLine($"Download complete: {request.FilePath}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Access denied: {ex.Message}");
                var errorMsg = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Access denied: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, errorMsg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending file: {ex.Message}");
                var errorMsg = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error downloading file: {ex.Message}"
                };
                try
                {
                    await NetworkHelper.SendMessageAsync(stream, errorMsg);
                }
                catch (Exception sendEx)
                {
                    Console.WriteLine($"Error sending error message: {sendEx.Message}");
                }
            }
        }

        public static async Task HandleDownloadFolderRequest(SslStream stream, DownloadFolderRequestMessage request)
        {
            try
            {
                if (!Directory.Exists(request.FolderPath))
                {
                    Console.WriteLine($"Folder not found: {request.FolderPath}");
                    var errorMsg = new OperationResultMessage
                    {
                        Success = false,
                        Message = "Folder not found"
                    };
                    await NetworkHelper.SendMessageAsync(stream, errorMsg);
                    return;
                }

                var folderInfo = new DirectoryInfo(request.FolderPath);
                var allFiles = new List<FolderFileInfo>();

                // Recursively get all files
                GetAllFilesRecursive(folderInfo, folderInfo.FullName, allFiles);

                Console.WriteLine($"Found {allFiles.Count} files in folder: {request.FolderPath}");

                // Send folder structure first
                var structureMsg = new FolderStructureMessage
                {
                    TransferId = request.TransferId,
                    FolderName = folderInfo.Name,
                    Files = allFiles,
                    TotalFiles = allFiles.Count
                };

                await NetworkHelper.SendMessageAsync(stream, structureMsg);

                // Send each file
                int fileIndex = 0;
                foreach (var fileInfo in allFiles)
                {
                    fileIndex++;
                    try
                    {
                        await SendFolderFile(stream, request.TransferId, fileInfo, fileIndex, allFiles.Count);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending file {fileInfo.RelativePath}: {ex.Message}");
                        // Continue with next file
                    }
                }

                Console.WriteLine($"Folder download complete: {request.FolderPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading folder: {ex.Message}");
                var errorMsg = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error downloading folder: {ex.Message}"
                };
                try
                {
                    await NetworkHelper.SendMessageAsync(stream, errorMsg);
                }
                catch (Exception sendEx)
                {
                    Console.WriteLine($"Error sending error message: {sendEx.Message}");
                }
            }
        }

        private static void GetAllFilesRecursive(DirectoryInfo directory, string rootPath, List<FolderFileInfo> files)
        {
            try
            {
                foreach (var file in directory.GetFiles())
                {
                    try
                    {
                        string relativePath = file.FullName.Substring(rootPath.Length).TrimStart(Path.DirectorySeparatorChar);
                        files.Add(new FolderFileInfo
                        {
                            RelativePath = relativePath,
                            FullPath = file.FullName,
                            FileSize = file.Length
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error accessing file {file.Name}: {ex.Message}");
                    }
                }

                foreach (var subDir in directory.GetDirectories())
                {
                    try
                    {
                        GetAllFilesRecursive(subDir, rootPath, files);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Console.WriteLine($"Access denied to directory: {subDir.FullName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error accessing directory {subDir.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enumerating directory {directory.FullName}: {ex.Message}");
            }
        }

        private static async Task SendFolderFile(SslStream stream, string transferId, FolderFileInfo fileInfo, int fileIndex, int totalFiles)
        {
            var file = new FileInfo(fileInfo.FullPath);
            int chunkNumber = 0;
            long totalChunks = (file.Length / FileSplitHelper.MaxChunkSize) + 1;

            foreach (var chunk in FileSplitHelper.ReadFileChunks(fileInfo.FullPath))
            {
                chunkNumber++;
                bool isLast = chunkNumber >= totalChunks;

                var chunkMsg = new FolderFileChunkMessage
                {
                    TransferId = transferId,
                    RelativePath = fileInfo.RelativePath,
                    FileName = file.Name,
                    FileSize = file.Length,
                    Offset = chunk.Offset,
                    Data = chunk.Data,
                    IsLastChunk = isLast,
                    FileIndex = fileIndex,
                    TotalFiles = totalFiles
                };

                await NetworkHelper.SendMessageAsync(stream, chunkMsg);
            }
        }

        public static async Task HandleFileChunk(SslStream stream, FileChunkMessage chunk)
        {
            try
            {
                if (!activeUploads.TryGetValue(chunk.TransferId, out string filePath))
                {
                    Console.WriteLine($"Warning: Unknown upload transfer ID: {chunk.TransferId}");
                    var errorMsg = new OperationResultMessage
                    {
                        Success = false,
                        Message = "Unknown transfer ID"
                    };
                    await NetworkHelper.SendMessageAsync(stream, errorMsg);
                    return;
                }

                FileSplitHelper.WriteFileChunk(filePath, new FileChunk
                {
                    Data = chunk.Data,
                    Offset = chunk.Offset
                });

                if (chunk.IsLastChunk)
                {
                    activeUploads.Remove(chunk.TransferId);
                    Console.WriteLine($"Upload complete: {filePath}");

                    var response = new OperationResultMessage
                    {
                        Success = true,
                        Message = "File uploaded successfully"
                    };
                    await NetworkHelper.SendMessageAsync(stream, response);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Access denied writing file chunk: {ex.Message}");
                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Access denied: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing file chunk: {ex.Message}");
                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error uploading file: {ex.Message}"
                };
                try
                {
                    await NetworkHelper.SendMessageAsync(stream, response);
                }
                catch (Exception sendEx)
                {
                    Console.WriteLine($"Error sending error response: {sendEx.Message}");
                }
            }
        }

        public static async Task HandleDeleteFile(SslStream stream, DeleteFileMessage deleteMsg)
        {
            try
            {
                if (deleteMsg.IsDirectory)
                {
                    if (!Directory.Exists(deleteMsg.Path))
                    {
                        var response = new OperationResultMessage
                        {
                            Success = false,
                            Message = "Directory not found"
                        };
                        await NetworkHelper.SendMessageAsync(stream, response);
                        return;
                    }

                    Directory.Delete(deleteMsg.Path, true);
                    Console.WriteLine($"Deleted directory: {deleteMsg.Path}");
                }
                else
                {
                    if (!File.Exists(deleteMsg.Path))
                    {
                        var response = new OperationResultMessage
                        {
                            Success = false,
                            Message = "File not found"
                        };
                        await NetworkHelper.SendMessageAsync(stream, response);
                        return;
                    }

                    File.Delete(deleteMsg.Path);
                    Console.WriteLine($"Deleted file: {deleteMsg.Path}");
                }

                var successResponse = new OperationResultMessage
                {
                    Success = true,
                    Message = "Deleted successfully"
                };
                await NetworkHelper.SendMessageAsync(stream, successResponse);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Access denied deleting: {ex.Message}");
                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Access denied: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting: {ex.Message}");
                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error deleting: {ex.Message}"
                };
                try
                {
                    await NetworkHelper.SendMessageAsync(stream, response);
                }
                catch (Exception sendEx)
                {
                    Console.WriteLine($"Error sending error response: {sendEx.Message}");
                }
            }
        }

        public static async Task HandleRenameFile(SslStream stream, RenameFileMessage renameMsg)
        {
            try
            {
                if (!File.Exists(renameMsg.OldPath) && !Directory.Exists(renameMsg.OldPath))
                {
                    var response = new OperationResultMessage
                    {
                        Success = false,
                        Message = "File or directory not found"
                    };
                    await NetworkHelper.SendMessageAsync(stream, response);
                    return;
                }

                string directory = Path.GetDirectoryName(renameMsg.OldPath);
                string newPath = Path.Combine(directory, renameMsg.NewName);

                if (Directory.Exists(renameMsg.OldPath))
                {
                    Directory.Move(renameMsg.OldPath, newPath);
                    Console.WriteLine($"Renamed directory: {renameMsg.OldPath} -> {newPath}");
                }
                else if (File.Exists(renameMsg.OldPath))
                {
                    File.Move(renameMsg.OldPath, newPath);
                    Console.WriteLine($"Renamed file: {renameMsg.OldPath} -> {newPath}");
                }

                var successResponse = new OperationResultMessage
                {
                    Success = true,
                    Message = "Renamed successfully"
                };
                await NetworkHelper.SendMessageAsync(stream, successResponse);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Access denied renaming: {ex.Message}");
                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Access denied: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error renaming: {ex.Message}");
                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error renaming: {ex.Message}"
                };
                try
                {
                    await NetworkHelper.SendMessageAsync(stream, response);
                }
                catch (Exception sendEx)
                {
                    Console.WriteLine($"Error sending error response: {sendEx.Message}");
                }
            }
        }

        public static async Task HandleCreateDirectory(SslStream stream, CreateDirectoryMessage createMsg)
        {
            try
            {
                if (Directory.Exists(createMsg.Path))
                {
                    var response = new OperationResultMessage
                    {
                        Success = false,
                        Message = "Directory already exists"
                    };
                    await NetworkHelper.SendMessageAsync(stream, response);
                    return;
                }

                Directory.CreateDirectory(createMsg.Path);
                Console.WriteLine($"Created directory: {createMsg.Path}");

                var successResponse = new OperationResultMessage
                {
                    Success = true,
                    Message = "Directory created successfully"
                };
                await NetworkHelper.SendMessageAsync(stream, successResponse);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Access denied creating directory: {ex.Message}");
                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Access denied: {ex.Message}"
                };
                await NetworkHelper.SendMessageAsync(stream, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating directory: {ex.Message}");
                var response = new OperationResultMessage
                {
                    Success = false,
                    Message = $"Error creating directory: {ex.Message}"
                };
                try
                {
                    await NetworkHelper.SendMessageAsync(stream, response);
                }
                catch (Exception sendEx)
                {
                    Console.WriteLine($"Error sending error response: {sendEx.Message}");
                }
            }
        }
    }
}